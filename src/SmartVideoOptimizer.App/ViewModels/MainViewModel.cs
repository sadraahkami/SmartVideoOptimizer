using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Interfaces;
using SmartVideoOptimizer.Core.Planning;
using SmartVideoOptimizer.Core.Quality;
using SmartVideoOptimizer.Core.Queue;
using SmartVideoOptimizer.Core.Validation;
using SmartVideoOptimizer.Media.Encoding;
using SmartVideoOptimizer.Media.Preview;
using SmartVideoOptimizer.Media.Probing;
using SmartVideoOptimizer.Platform.Windows.Hardware;
using SmartVideoOptimizer.Platform.Windows.Storage;

namespace SmartVideoOptimizer.App.ViewModels;

public enum AppScreen
{
    Home,
    Optimize,
    Queue,
    History,
    Results
}

public sealed class MainViewModel : ViewModelBase
{
    private readonly IMediaProbe _mediaProbe;
    private readonly EncodingJobOrchestrator _orchestrator;
    private readonly PreviewService _previewService;
    private readonly CapabilityDetector _capabilityDetector;
    private readonly QueueManager _queueManager;

    private MediaAsset? _currentAsset;
    private AppScreen _currentScreen = AppScreen.Home;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private string? _errorMessage;
    private bool _isPersian;

    // Optimization Settings
    private CompressionGoal _selectedGoal = CompressionGoal.ExactSize;
    private int _targetSizeMb = 100;
    private EncodingPriority _selectedPriority = EncodingPriority.Balanced;
    private ResolutionPolicy _selectedResolution = ResolutionPolicy.KeepOriginal;
    private bool _neverExceedTarget = true;
    private bool _isAdvancedOpen;
    private string? _customOutputFolder;

    // Execution State
    private bool _isEncoding;
    private bool _isPreviewing;
    private EncodingProgress? _progress;
    private QualityRiskReport? _qualityRisk;
    private SmartRecommendation? _smartRecommendation;
    private PreviewResult? _previewResult;
    private ValidationResult? _lastValidationResult;
    private string? _lastOutputFile;
    private CancellationTokenSource? _activeEncodeCts;

    public MainViewModel() : this(new MediaProbe())
    {
    }

    public MainViewModel(IMediaProbe mediaProbe)
    {
        _mediaProbe = mediaProbe;
        _capabilityDetector = new CapabilityDetector();
        _previewService = new PreviewService();
        _queueManager = new QueueManager(new SqliteQueueRepository());
        _orchestrator = new EncodingJobOrchestrator(_mediaProbe, new EncodingEngine(), _capabilityDetector);

        // Commands
        BrowseFileCommand = new RelayCommand(OnBrowseFile);
        LoadFileCommand = new AsyncRelayCommand(async p =>
        {
            if (p is string path) await LoadVideoAsync(path);
        });
        ClearFileCommand = new RelayCommand(OnClearFile);
        ToggleLanguageCommand = new RelayCommand(() => IsPersian = !IsPersian);
        ToggleAdvancedCommand = new RelayCommand(() => IsAdvancedOpen = !IsAdvancedOpen);

        SetScreenCommand = new RelayCommand(p =>
        {
            if (p is AppScreen s) CurrentScreen = s;
        });

        SetGoalCommand = new RelayCommand(p =>
        {
            if (p is CompressionGoal g)
            {
                SelectedGoal = g;
                UpdateRiskAndRecommendations();
            }
        });

        SetPriorityCommand = new RelayCommand(p =>
        {
            if (p is EncodingPriority pr)
            {
                SelectedPriority = pr;
                UpdateRiskAndRecommendations();
            }
        });

        SelectOutputFolderCommand = new RelayCommand(OnSelectOutputFolder);
        ResetOutputFolderCommand = new RelayCommand(() => CustomOutputFolder = null);
        CopyPathCommand = new RelayCommand(OnCopyPath);

        StartEncodingCommand = new AsyncRelayCommand(ExecuteEncodingAsync, () => !IsEncoding && HasAsset);
        CancelEncodingCommand = new RelayCommand(CancelEncoding, () => IsEncoding);
        GeneratePreviewCommand = new AsyncRelayCommand(ExecutePreviewAsync, () => !IsPreviewing && !IsEncoding && HasAsset);

        OpenFileCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(LastOutputFile) && File.Exists(LastOutputFile))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = LastOutputFile,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ErrorMessage = IsPersian ? $"خطا در پخش ویدیو: {ex.Message}" : $"Failed to play video: {ex.Message}";
                }
            }
            else
            {
                ErrorMessage = IsPersian
                    ? $"فایل خروجی در مسیر زیر یافت نشد:\n{LastOutputFile}"
                    : $"Output file not found at path:\n{LastOutputFile}";
            }
        });

        OpenFolderCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(LastOutputFile) && File.Exists(LastOutputFile))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{LastOutputFile}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ErrorMessage = IsPersian ? $"خطا در باز کردن پوشه: {ex.Message}" : $"Failed to open folder: {ex.Message}";
                }
            }
            else if (!string.IsNullOrEmpty(LastOutputDirectory) && Directory.Exists(LastOutputDirectory))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{LastOutputDirectory}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ErrorMessage = IsPersian ? $"خطا در باز کردن پوشه: {ex.Message}" : $"Failed to open folder: {ex.Message}";
                }
            }
            else
            {
                ErrorMessage = IsPersian ? "پوشه خروجی یافت نشد." : "Output directory not found.";
            }
        });

        PlayJobFileCommand = new RelayCommand(p =>
        {
            if (p is string path && !string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = IsPersian ? $"خطا در اجرای ویدیو: {ex.Message}" : $"Failed to play video: {ex.Message}";
                    }
                }
                else
                {
                    ErrorMessage = IsPersian ? $"فایل ویدیو یافت نشد:\n{path}" : $"Video file not found:\n{path}";
                }
            }
        });

        OpenJobFolderCommand = new RelayCommand(p =>
        {
            if (p is string path && !string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{path}\"", UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = IsPersian ? $"خطا در باز کردن پوشه: {ex.Message}" : $"Failed to open folder: {ex.Message}";
                    }
                }
                else
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{dir}\"", UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            ErrorMessage = IsPersian ? $"خطا در باز کردن پوشه: {ex.Message}" : $"Failed to open folder: {ex.Message}";
                        }
                    }
                    else
                    {
                        ErrorMessage = IsPersian ? "پوشه فایل یافت نشد." : "Folder not found.";
                    }
                }
            }
        });

        CopyJobPathCommand = new RelayCommand(p =>
        {
            if (p is string path && !string.IsNullOrEmpty(path))
            {
                Clipboard.SetText(path);
                StatusMessage = IsPersian ? "مسیر فایل در کلیپ‌بورد کپی شد." : "File path copied to clipboard.";
            }
        });

        DeleteJobCommand = new AsyncRelayCommand(async p =>
        {
            if (p is Guid id)
            {
                await _queueManager.DeleteJobAsync(id);
                NotifyHistoryProperties();
            }
        });

        ClearHistoryCommand = new AsyncRelayCommand(async () =>
        {
            await _queueManager.ClearHistoryAsync();
            NotifyHistoryProperties();
            StatusMessage = IsPersian ? "سوابق بهینه‌سازی پاکسازی شدند." : "Optimization history cleared.";
        });

        RefreshHistoryCommand = new AsyncRelayCommand(async () =>
        {
            await _queueManager.RefreshAsync();
            NotifyHistoryProperties();
            StatusMessage = IsPersian ? "سوابق به‌روزرسانی شدند." : "History refreshed.";
        });

        _queueManager.JobStateChanged += _ =>
        {
            Application.Current?.Dispatcher?.Invoke(NotifyHistoryProperties);
        };

        _ = InitializeAsync();
    }

    private void NotifyHistoryProperties()
    {
        OnPropertyChanged(nameof(QueueJobs));
        OnPropertyChanged(nameof(HistoryJobs));
        OnPropertyChanged(nameof(HasHistoryJobs));
        OnPropertyChanged(nameof(TotalCompletedJobsCount));
        OnPropertyChanged(nameof(TotalSpaceSavedFormatted));
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _queueManager.InitializeAndRecoverAsync();
            await _capabilityDetector.DetectCapabilitiesAsync();
            NotifyHistoryProperties();
        }
        catch { /* ignored */ }
    }

    public AppScreen CurrentScreen
    {
        get => _currentScreen;
        set => SetField(ref _currentScreen, value);
    }

    public MediaAsset? CurrentAsset
    {
        get => _currentAsset;
        private set
        {
            if (SetField(ref _currentAsset, value))
            {
                OnPropertyChanged(nameof(HasAsset));
                OnPropertyChanged(nameof(PrimaryVideo));
                OnPropertyChanged(nameof(PrimaryAudio));
                OnPropertyChanged(nameof(AudioTracksCount));
                OnPropertyChanged(nameof(SubtitleTracksCount));
                OnPropertyChanged(nameof(ChaptersCount));
                OnPropertyChanged(nameof(EffectiveOutputPath));
                OnPropertyChanged(nameof(EffectiveOutputDirectory));
                UpdateRiskAndRecommendations();
            }
        }
    }

    public bool HasAsset => CurrentAsset != null;
    public VideoStream? PrimaryVideo => CurrentAsset?.PrimaryVideo;
    public AudioStream? PrimaryAudio => CurrentAsset?.PrimaryAudio;
    public int AudioTracksCount => CurrentAsset?.AudioStreams.Count ?? 0;
    public int SubtitleTracksCount => CurrentAsset?.SubtitleStreams.Count ?? 0;
    public int ChaptersCount => CurrentAsset?.Chapters.Count ?? 0;

    public CompressionGoal SelectedGoal
    {
        get => _selectedGoal;
        set
        {
            if (SetField(ref _selectedGoal, value))
            {
                OnPropertyChanged(nameof(IsExactSizeGoal));
                OnPropertyChanged(nameof(IsBestQualityGoal));
                OnPropertyChanged(nameof(IsSmartCompressGoal));
                OnPropertyChanged(nameof(EffectiveOutputPath));
                UpdateRiskAndRecommendations();
            }
        }
    }

    public bool IsExactSizeGoal => SelectedGoal == CompressionGoal.ExactSize;
    public bool IsBestQualityGoal => SelectedGoal == CompressionGoal.BestQuality;
    public bool IsSmartCompressGoal => SelectedGoal == CompressionGoal.SmartCompress;

    public int TargetSizeMb
    {
        get => _targetSizeMb;
        set
        {
            if (SetField(ref _targetSizeMb, Math.Max(5, value)))
            {
                UpdateRiskAndRecommendations();
            }
        }
    }

    public EncodingPriority SelectedPriority
    {
        get => _selectedPriority;
        set
        {
            if (SetField(ref _selectedPriority, value))
            {
                UpdateRiskAndRecommendations();
            }
        }
    }

    public ResolutionPolicy SelectedResolution
    {
        get => _selectedResolution;
        set
        {
            if (SetField(ref _selectedResolution, value))
            {
                UpdateRiskAndRecommendations();
            }
        }
    }

    public bool NeverExceedTarget
    {
        get => _neverExceedTarget;
        set => SetField(ref _neverExceedTarget, value);
    }

    public bool IsAdvancedOpen
    {
        get => _isAdvancedOpen;
        set => SetField(ref _isAdvancedOpen, value);
    }

    public bool IsEncoding
    {
        get => _isEncoding;
        private set => SetField(ref _isEncoding, value);
    }

    public bool IsPreviewing
    {
        get => _isPreviewing;
        private set => SetField(ref _isPreviewing, value);
    }

    public EncodingProgress? Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    public QualityRiskReport? QualityRisk
    {
        get => _qualityRisk;
        private set => SetField(ref _qualityRisk, value);
    }

    public SmartRecommendation? SmartRecommendation
    {
        get => _smartRecommendation;
        private set => SetField(ref _smartRecommendation, value);
    }

    public PreviewResult? PreviewResult
    {
        get => _previewResult;
        private set => SetField(ref _previewResult, value);
    }

    public ValidationResult? LastValidationResult
    {
        get => _lastValidationResult;
        private set => SetField(ref _lastValidationResult, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsPersian
    {
        get => _isPersian;
        set
        {
            if (SetField(ref _isPersian, value))
            {
                OnPropertyChanged(nameof(FlowDirectionText));
                OnPropertyChanged(nameof(DropBoxTitle));
                OnPropertyChanged(nameof(DropBoxSubtitle));
                OnPropertyChanged(nameof(BrowseButtonText));
                OnPropertyChanged(nameof(ClearButtonText));
                OnPropertyChanged(nameof(LanguageToggleText));
                OnPropertyChanged(nameof(QualityRiskText));
            }
        }
    }

    public string FlowDirectionText => IsPersian ? "RightToLeft" : "LeftToRight";
    public string DropBoxTitle => IsPersian ? "فایل ویدیو را اینجا بکشید و رها کنید" : "Drag & Drop video file here";
    public string DropBoxSubtitle => IsPersian ? "پشتیبانی از فرمت‌های MKV ،MP4 ،MOV ،WebM ،AVI ،TS" : "Supports MKV, MP4, MOV, WebM, AVI, TS, M2TS, FLV";
    public string BrowseButtonText => IsPersian ? "انتخاب فایل ویدیو..." : "Browse Video File...";
    public string ClearButtonText => IsPersian ? "حذف و انتخاب فایل دیگر" : "Clear & Select Another";
    public string LanguageToggleText => IsPersian ? "English" : "فارسی";
    public string QualityRiskText => IsPersian
        ? (QualityRisk?.RecommendationPersian ?? "در حال ارزیابی کیفیت...")
        : (QualityRisk?.RecommendationEnglish ?? "Evaluating quality profile...");

    public string? CustomOutputFolder
    {
        get => _customOutputFolder;
        set
        {
            if (SetField(ref _customOutputFolder, value))
            {
                OnPropertyChanged(nameof(EffectiveOutputPath));
                OnPropertyChanged(nameof(EffectiveOutputDirectory));
                OnPropertyChanged(nameof(HasCustomOutputFolder));
            }
        }
    }

    public bool HasCustomOutputFolder => !string.IsNullOrWhiteSpace(_customOutputFolder);

    public string EffectiveOutputDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_customOutputFolder) && Directory.Exists(_customOutputFolder))
            {
                return _customOutputFolder;
            }
            if (CurrentAsset != null && !string.IsNullOrWhiteSpace(CurrentAsset.FilePath))
            {
                return Path.GetDirectoryName(CurrentAsset.FilePath) ?? string.Empty;
            }
            return string.Empty;
        }
    }

    public string EffectiveOutputPath
    {
        get
        {
            if (CurrentAsset == null || string.IsNullOrWhiteSpace(CurrentAsset.FilePath))
                return string.Empty;

            var dir = EffectiveOutputDirectory;
            var name = Path.GetFileNameWithoutExtension(CurrentAsset.FilePath);
            var ext = Path.GetExtension(CurrentAsset.FilePath);
            if (string.IsNullOrEmpty(ext)) ext = ".mp4";

            var suffix = SelectedGoal switch
            {
                CompressionGoal.ExactSize => "_optimized",
                CompressionGoal.BestQuality => "_hq",
                CompressionGoal.SmartCompress => "_smart",
                _ => "_optimized"
            };

            return Path.Combine(dir, $"{name}{suffix}{ext}");
        }
    }

    public string? LastOutputFile
    {
        get => _lastOutputFile;
        private set
        {
            if (SetField(ref _lastOutputFile, value))
            {
                OnPropertyChanged(nameof(LastOutputDirectory));
                OnPropertyChanged(nameof(HasLastOutputFile));
            }
        }
    }

    public string? LastOutputDirectory => !string.IsNullOrEmpty(_lastOutputFile) ? Path.GetDirectoryName(_lastOutputFile) : null;
    public bool HasLastOutputFile => !string.IsNullOrEmpty(_lastOutputFile);

    public IReadOnlyList<JobItem> QueueJobs => _queueManager.Jobs;
    public IReadOnlyList<JobItem> HistoryJobs => _queueManager.Jobs;
    public bool HasHistoryJobs => _queueManager.Jobs.Count > 0;
    public int TotalCompletedJobsCount => _queueManager.Jobs.Count(j => j.State == JobState.Completed);

    public string TotalSpaceSavedFormatted
    {
        get
        {
            long totalOriginal = 0;
            long totalOutput = 0;
            foreach (var j in _queueManager.Jobs)
            {
                if (j.State == JobState.Completed && j.OutputSizeBytes.HasValue)
                {
                    totalOriginal += j.OriginalSizeBytes;
                    totalOutput += j.OutputSizeBytes.Value;
                }
            }
            var saved = Math.Max(0, totalOriginal - totalOutput);
            const double mb = 1024.0 * 1024.0;
            const double gb = 1024.0 * 1024.0 * 1024.0;
            if (saved >= gb)
            {
                return $"{saved / gb:F2} GB";
            }
            return $"{saved / mb:F1} MB";
        }
    }

    public ICommand BrowseFileCommand { get; }
    public ICommand LoadFileCommand { get; }
    public ICommand ClearFileCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand ToggleAdvancedCommand { get; }
    public ICommand SetScreenCommand { get; }
    public ICommand SetGoalCommand { get; }
    public ICommand SetPriorityCommand { get; }
    public ICommand StartEncodingCommand { get; }
    public ICommand CancelEncodingCommand { get; }
    public ICommand GeneratePreviewCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand SelectOutputFolderCommand { get; }
    public ICommand ResetOutputFolderCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand PlayJobFileCommand { get; }
    public ICommand OpenJobFolderCommand { get; }
    public ICommand CopyJobPathCommand { get; }
    public ICommand DeleteJobCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand RefreshHistoryCommand { get; }

    public async Task LoadVideoAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        ErrorMessage = null;
        IsLoading = true;
        StatusMessage = IsPersian ? "در حال استخراج مشخصات با ffprobe..." : "Extracting media streams with ffprobe...";

        try
        {
            var asset = await _mediaProbe.ProbeAsync(filePath);
            CurrentAsset = asset;
            CurrentScreen = AppScreen.Optimize;
            StatusMessage = IsPersian ? "فایل آماده بهینه‌سازی است." : "Media ready for optimization.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = IsPersian ? "خطا در تحلیل فایل." : "Error analyzing media file.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateRiskAndRecommendations()
    {
        if (CurrentAsset?.PrimaryVideo == null) return;

        var v = CurrentAsset.PrimaryVideo;
        var dur = CurrentAsset.Duration;
        var targetBytes = (long)TargetSizeMb * 1024 * 1024;

        var req = BuildCurrentJobRequest();

        // 1. Calculate Quality Guard Risk
        var videoBps = (targetBytes * 8.0) / Math.Max(1.0, dur.TotalSeconds);
        var kbps = (int)(videoBps / 1000.0);
        QualityRisk = QualityGuard.AssessRisk(kbps, v.Width, v.Height, v.FrameRate, dur, "libx265");

        // 2. Calculate Smart Compress Lite recommendation
        SmartRecommendation = SmartPlanner.CreateSmartRecommendation(req, CurrentAsset);
    }

    private JobRequest BuildCurrentJobRequest()
    {
        var input = CurrentAsset?.FilePath ?? string.Empty;
        var targetBytes = (long)TargetSizeMb * 1024 * 1024;
        var output = EffectiveOutputPath;

        return new JobRequest
        {
            InputPath = input,
            OutputPath = output,
            Goal = SelectedGoal,
            TargetSizeBytes = targetBytes,
            Resolution = SelectedResolution,
            Priority = SelectedPriority,
            Hardware = HardwarePolicy.Auto,
            NeverExceedTarget = NeverExceedTarget
        };
    }

    private async Task ExecutePreviewAsync()
    {
        if (CurrentAsset == null) return;

        IsPreviewing = true;
        StatusMessage = IsPersian ? "در حال تولید نمونه ۱۰ ثانیه‌ای پیش‌نمایش..." : "Generating 10-second preview sample...";

        try
        {
            var req = BuildCurrentJobRequest();
            var plan = ExactSizePlanner.CreatePlan(req, CurrentAsset);
            var result = await _previewService.GenerateQuickPreviewAsync(CurrentAsset.FilePath, plan, 10.0);
            PreviewResult = result;
            StatusMessage = IsPersian ? "پیش‌نمایش آماده شد." : "Preview sample ready.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Preview failed: {ex.Message}";
        }
        finally
        {
            IsPreviewing = false;
        }
    }

    private async Task ExecuteEncodingAsync()
    {
        if (CurrentAsset == null) return;

        IsEncoding = true;
        CurrentScreen = AppScreen.Queue;
        _activeEncodeCts = new CancellationTokenSource();

        var req = BuildCurrentJobRequest();
        var plannedOut = req.OutputPath ?? EffectiveOutputPath;

        var jobItem = new JobItem
        {
            SourcePath = CurrentAsset.FilePath,
            OutputPath = plannedOut,
            Goal = SelectedGoal,
            OriginalSizeBytes = CurrentAsset.FileSizeBytes,
            State = JobState.Encoding
        };

        await _queueManager.EnqueueAsync(jobItem);
        OnPropertyChanged(nameof(QueueJobs));

        var progressReporter = new Progress<EncodingProgress>(p =>
        {
            Progress = p;
            _ = _queueManager.UpdateProgressAsync(jobItem.Id, p.Percentage);
        });

        try
        {
            StatusMessage = IsPersian ? "در حال فشرده‌سازی با موتور پیشرفته..." : "Encoding media with precision engine...";
            var result = await _orchestrator.RunJobWithRetryAsync(
                req,
                CurrentAsset,
                progressReporter,
                _queueManager,
                jobItem.Id,
                _activeEncodeCts.Token);

            LastValidationResult = result;
            LastOutputFile = !string.IsNullOrWhiteSpace(result.OutputFilePath) ? result.OutputFilePath : plannedOut;

            if (result.IsValid)
            {
                CurrentScreen = AppScreen.Results;
                StatusMessage = IsPersian ? "فشرده‌سازی با موفقیت انجام و تایید شد." : "Compression completed and verified.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (OperationCanceledException)
        {
            await _queueManager.CancelJobAsync(jobItem.Id);
            StatusMessage = IsPersian ? "عملیات لغو شد." : "Encoding cancelled.";
        }
        catch (Exception ex)
        {
            await _queueManager.FailJobAsync(jobItem.Id, ex.Message);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsEncoding = false;
            OnPropertyChanged(nameof(QueueJobs));
            _activeEncodeCts?.Dispose();
            _activeEncodeCts = null;
        }
    }

    private void CancelEncoding()
    {
        _activeEncodeCts?.Cancel();
    }

    private void OnBrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = IsPersian ? "انتخاب فایل ویدیویی" : "Select Video File",
            Filter = "Video Files (*.mp4;*.mkv;*.mov;*.webm;*.avi;*.ts)|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.ts|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _ = LoadVideoAsync(dialog.FileName);
        }
    }

    private void OnSelectOutputFolder()
    {
        var initialDir = EffectiveOutputDirectory;
        var dialog = new OpenFolderDialog
        {
            Title = IsPersian ? "انتخاب پوشه ذخیره‌سازی فایل خروجی" : "Select Output Folder",
            InitialDirectory = Directory.Exists(initialDir) ? initialDir : null
        };

        if (dialog.ShowDialog() == true)
        {
            CustomOutputFolder = dialog.FolderName;
        }
    }

    private void OnCopyPath()
    {
        if (!string.IsNullOrEmpty(LastOutputFile))
        {
            Clipboard.SetText(LastOutputFile);
            StatusMessage = IsPersian ? "مسیر فایل با موفقیت در کلیپ‌بورد کپی شد." : "File path successfully copied to clipboard.";
        }
    }

    private void OnClearFile()
    {
        CurrentAsset = null;
        ErrorMessage = null;
        StatusMessage = string.Empty;
        CurrentScreen = AppScreen.Home;
        PreviewResult = null;
        LastValidationResult = null;
        LastOutputFile = null;
        CustomOutputFolder = null;
    }
}
