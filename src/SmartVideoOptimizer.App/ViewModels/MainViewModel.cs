using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Interfaces;
using SmartVideoOptimizer.Media.Probing;

namespace SmartVideoOptimizer.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IMediaProbe _mediaProbe;
    private MediaAsset? _currentAsset;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private string? _errorMessage;
    private bool _isPersian;

    public MainViewModel() : this(new MediaProbe())
    {
    }

    public MainViewModel(IMediaProbe mediaProbe)
    {
        _mediaProbe = mediaProbe;

        BrowseFileCommand = new RelayCommand(OnBrowseFile);
        LoadFileCommand = new AsyncRelayCommand(async p =>
        {
            if (p is string path)
                await LoadVideoAsync(path);
        });
        ClearFileCommand = new RelayCommand(OnClearFile);
        ToggleLanguageCommand = new RelayCommand(() => IsPersian = !IsPersian);
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
            }
        }
    }

    public bool HasAsset => CurrentAsset != null;
    public VideoStream? PrimaryVideo => CurrentAsset?.PrimaryVideo;
    public AudioStream? PrimaryAudio => CurrentAsset?.PrimaryAudio;
    public int AudioTracksCount => CurrentAsset?.AudioStreams.Count ?? 0;
    public int SubtitleTracksCount => CurrentAsset?.SubtitleStreams.Count ?? 0;
    public int ChaptersCount => CurrentAsset?.Chapters.Count ?? 0;

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
                OnPropertyChanged(nameof(FileDetailsHeader));
                OnPropertyChanged(nameof(VideoDetailsHeader));
                OnPropertyChanged(nameof(AudioDetailsHeader));
                OnPropertyChanged(nameof(SubtitleDetailsHeader));
                OnPropertyChanged(nameof(LanguageToggleText));
                OnPropertyChanged(nameof(StatusReadyText));
            }
        }
    }

    public string FlowDirectionText => IsPersian ? "RightToLeft" : "LeftToRight";
    public string DropBoxTitle => IsPersian ? "فایل ویدیو را اینجا بکشید و رها کنید" : "Drag & Drop video file here";
    public string DropBoxSubtitle => IsPersian ? "پشتیبانی از فرمت‌های MKV ،MP4 ،MOV ،WebM ،AVI ،TS" : "Supports MKV, MP4, MOV, WebM, AVI, TS, M2TS, FLV";
    public string BrowseButtonText => IsPersian ? "انتخاب فایل ویدیو..." : "Browse Video File...";
    public string ClearButtonText => IsPersian ? "حذف و انتخاب فایل دیگر" : "Clear & Select Another";
    public string FileDetailsHeader => IsPersian ? "مشخصات کلی پرونده" : "File Overview";
    public string VideoDetailsHeader => IsPersian ? "مشخصات استریم تصویر" : "Video Stream Details";
    public string AudioDetailsHeader => IsPersian ? "ترک‌های صوتی موجود" : "Audio Tracks";
    public string SubtitleDetailsHeader => IsPersian ? "ترک‌های زیرنویس موجود" : "Subtitle Tracks";
    public string LanguageToggleText => IsPersian ? "English" : "فارسی";
    public string StatusReadyText => IsPersian ? "آماده تحلیل فایل" : "Ready for analysis";

    public ICommand BrowseFileCommand { get; }
    public ICommand LoadFileCommand { get; }
    public ICommand ClearFileCommand { get; }
    public ICommand ToggleLanguageCommand { get; }

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
            StatusMessage = IsPersian ? "تحلیل فایل با موفقیت انجام شد." : "File analysis completed successfully.";
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

    private void OnBrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = IsPersian ? "انتخاب فایل ویدیویی" : "Select Video File",
            Filter = "Video Files (*.mp4;*.mkv;*.mov;*.webm;*.avi;*.ts;*.m2ts)|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.ts;*.m2ts|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            _ = LoadVideoAsync(dialog.FileName);
        }
    }

    private void OnClearFile()
    {
        CurrentAsset = null;
        ErrorMessage = null;
        StatusMessage = string.Empty;
    }
}
