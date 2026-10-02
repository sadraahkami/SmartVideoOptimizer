using Microsoft.Data.Sqlite;
using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Interfaces;

namespace SmartVideoOptimizer.Platform.Windows.Storage;

public sealed class SqliteQueueRepository : IQueueRepository
{
    private readonly string _connectionString;

    public SqliteQueueRepository(string? customDbPath = null)
    {
        var dbPath = customDbPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "queue.db");
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
        CREATE TABLE IF NOT EXISTS jobs (
            id TEXT PRIMARY KEY,
            source_path TEXT NOT NULL,
            output_path TEXT NOT NULL,
            goal INTEGER NOT NULL,
            state INTEGER NOT NULL,
            progress REAL NOT NULL DEFAULT 0.0,
            original_size INTEGER NOT NULL DEFAULT 0,
            output_size INTEGER NULL,
            error_message TEXT NULL,
            created_at TEXT NOT NULL,
            completed_at TEXT NULL,
            retry_count INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS idx_jobs_state ON jobs(state);
        """;

        await using var cmd = new SqliteCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task InsertJobAsync(JobItem job, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
        INSERT INTO jobs (id, source_path, output_path, goal, state, progress, original_size, output_size, error_message, created_at, completed_at, retry_count)
        VALUES (@id, @source_path, @output_path, @goal, @state, @progress, @original_size, @output_size, @error_message, @created_at, @completed_at, @retry_count);
        """;

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", job.Id.ToString());
        cmd.Parameters.AddWithValue("@source_path", job.SourcePath);
        cmd.Parameters.AddWithValue("@output_path", job.OutputPath);
        cmd.Parameters.AddWithValue("@goal", (int)job.Goal);
        cmd.Parameters.AddWithValue("@state", (int)job.State);
        cmd.Parameters.AddWithValue("@progress", job.ProgressPercentage);
        cmd.Parameters.AddWithValue("@original_size", job.OriginalSizeBytes);
        cmd.Parameters.AddWithValue("@output_size", (object?)job.OutputSizeBytes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@error_message", (object?)job.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at", job.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@completed_at", job.CompletedAt.HasValue ? job.CompletedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@retry_count", job.RetryCount);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateJobStateAsync(Guid id, JobState state, double progress = 0.0, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
        UPDATE jobs
        SET state = @state, progress = @progress, error_message = @error_message
        WHERE id = @id;
        """;

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@state", (int)state);
        cmd.Parameters.AddWithValue("@progress", progress);
        cmd.Parameters.AddWithValue("@error_message", (object?)errorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteJobAsync(Guid id, long outputSizeBytes, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
        UPDATE jobs
        SET state = @state, progress = 100.0, output_size = @output_size, completed_at = @completed_at, error_message = NULL
        WHERE id = @id;
        """;

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@state", (int)JobState.Completed);
        cmd.Parameters.AddWithValue("@output_size", outputSizeBytes);
        cmd.Parameters.AddWithValue("@completed_at", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<JobItem>> GetUnfinishedJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        // State < Completed means active, queued or failed
        const string sql = """
        SELECT id, source_path, output_path, goal, state, progress, original_size, output_size, error_message, created_at, completed_at, retry_count
        FROM jobs
        WHERE state IN (@s1, @s2, @s3, @s4)
        ORDER BY created_at ASC;
        """;

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@s1", (int)JobState.Queued);
        cmd.Parameters.AddWithValue("@s2", (int)JobState.Preparing);
        cmd.Parameters.AddWithValue("@s3", (int)JobState.Encoding);
        cmd.Parameters.AddWithValue("@s4", (int)JobState.Validating);

        return await ReadJobsAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<JobItem>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
        SELECT id, source_path, output_path, goal, state, progress, original_size, output_size, error_message, created_at, completed_at, retry_count
        FROM jobs
        ORDER BY created_at DESC;
        """;

        await using var cmd = new SqliteCommand(sql, conn);
        return await ReadJobsAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "DELETE FROM jobs WHERE id = @id;";
        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<JobItem>> ReadJobsAsync(SqliteCommand cmd, CancellationToken cancellationToken)
    {
        var list = new List<JobItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = Guid.Parse(reader.GetString(0));
            var sourcePath = reader.GetString(1);
            var outputPath = reader.GetString(2);
            var goal = (CompressionGoal)reader.GetInt32(3);
            var state = (JobState)reader.GetInt32(4);
            var progress = reader.GetDouble(5);
            var originalSize = reader.GetInt64(6);
            long? outputSize = reader.IsDBNull(7) ? null : reader.GetInt64(7);
            var error = reader.IsDBNull(8) ? null : reader.GetString(8);
            var createdAt = DateTime.Parse(reader.GetString(9));
            DateTime? completedAt = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10));
            var retryCount = reader.GetInt32(11);

            list.Add(new JobItem
            {
                Id = id,
                SourcePath = sourcePath,
                OutputPath = outputPath,
                Goal = goal,
                State = state,
                ProgressPercentage = progress,
                OriginalSizeBytes = originalSize,
                OutputSizeBytes = outputSize,
                ErrorMessage = error,
                CreatedAt = createdAt,
                CompletedAt = completedAt,
                RetryCount = retryCount
            });
        }

        return list;
    }
}
