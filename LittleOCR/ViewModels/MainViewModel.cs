using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LittleOCR.Models;
using LittleOCR.Services;
using Microsoft.Win32;

namespace LittleOCR.ViewModels;

// ── Enums ──────────────────────────────────────────────────────────────────────

public enum OcrState { Idle, Running, Success, Error }

// ── Overlay item ───────────────────────────────────────────────────────────────

/// <summary>
/// Wraps an <see cref="OcrLine"/> and adds view-state (selection, canvas placement).
/// Canvas coordinates are set by the View after computing the image transform.
/// </summary>
public sealed class OcrOverlayItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public OcrLine Line { get; }

    // Canvas placement (set by the View, not the ViewModel)
    public double CanvasX { get; set; }
    public double CanvasY { get; set; }
    public double CanvasW { get; set; }
    public double CanvasH { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public OcrOverlayItem(OcrLine line) => Line = line;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Main ViewModel ─────────────────────────────────────────────────────────────

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly WindowsOcrService _ocrService = new();

    private BitmapSource? _imageSource;
    private string? _imagePath;
    private OcrResult? _ocrResult;
    private OcrState _ocrState = OcrState.Idle;
    private bool _isOverlayVisible = true;
    private string _statusMessage = string.Empty;

    // ── Properties ────────────────────────────────────────────────────────────

    public BitmapSource? ImageSource
    {
        get => _imageSource;
        private set
        {
            _imageSource = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsImageLoaded));
        }
    }

    public bool IsImageLoaded => _imageSource != null;

    public OcrResult? OcrResult
    {
        get => _ocrResult;
        private set
        {
            _ocrResult = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOcrResult));
            RebuildOverlayItems();
        }
    }

    public bool HasOcrResult => _ocrResult != null;

    public OcrState OcrState
    {
        get => _ocrState;
        private set
        {
            _ocrState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOcrRunning));
            OnPropertyChanged(nameof(OcrButtonLabel));
        }
    }

    public bool IsOcrRunning => _ocrState == OcrState.Running;

    public string OcrButtonLabel => _ocrState switch
    {
        OcrState.Running => "Analyse en cours…",
        OcrState.Success => "✓  Relancer l'OCR",
        OcrState.Error   => "↺  Réessayer",
        _                => "⌕  Lire le texte",
    };

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        set { _isOverlayVisible = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    /// <summary>Lines returned by the last OCR run, each wrapped with selection state.</summary>
    public ObservableCollection<OcrOverlayItem> OverlayItems { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand OpenImageCommand    { get; }
    public ICommand RunOcrCommand       { get; }
    public ICommand CopyAllCommand      { get; }
    public ICommand CopySelectionCommand{ get; }
    public ICommand ToggleOverlayCommand{ get; }
    public ICommand ClearStatusCommand  { get; }

    public MainViewModel()
    {
        OpenImageCommand     = new RelayCommand(OpenImage);
        RunOcrCommand        = new RelayCommand(async () => await RunOcrAsync(),
                                                () => IsImageLoaded && !IsOcrRunning);
        CopyAllCommand       = new RelayCommand(CopyAll,
                                                () => HasOcrResult);
        CopySelectionCommand = new RelayCommand(CopySelection,
                                                () => OverlayItems.Any(i => i.IsSelected));
        ToggleOverlayCommand = new RelayCommand(
                                    () => IsOverlayVisible = !IsOverlayVisible,
                                    () => HasOcrResult);
        ClearStatusCommand   = new RelayCommand(() => StatusMessage = string.Empty);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void LoadImage(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            ImageSource   = bitmap;
            _imagePath    = path;
            OcrResult     = null;
            OcrState      = OcrState.Idle;
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossible d'ouvrir l'image : {ex.Message}";
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OpenImage()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Ouvrir une image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Tous les fichiers|*.*",
        };

        if (dlg.ShowDialog() == true)
            LoadImage(dlg.FileName);
    }

    private async Task RunOcrAsync()
    {
        if (_imagePath is null) return;

        OcrState      = OcrState.Running;
        StatusMessage = string.Empty;

        try
        {
            var result = await _ocrService.RecognizeAsync(_imagePath);

            OcrResult = result;
            OcrState  = OcrState.Success;

            if (!result.Lines.Any())
                StatusMessage = "Aucun texte n'a été détecté dans cette image.";
            else
                IsOverlayVisible = true;
        }
        catch (InvalidOperationException ex)
        {
            OcrState      = OcrState.Error;
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            OcrState      = OcrState.Error;
            StatusMessage = $"Erreur OCR : {ex.Message}";
        }
    }

    private void RebuildOverlayItems()
    {
        OverlayItems.Clear();

        if (_ocrResult is null) return;

        foreach (var line in _ocrResult.Lines)
            OverlayItems.Add(new OcrOverlayItem(line));
    }

    private void CopyAll()
    {
        if (_ocrResult?.FullText is { Length: > 0 } text)
            Clipboard.SetText(text);
    }

    private void CopySelection()
    {
        var text = string.Join(
            Environment.NewLine,
            OverlayItems.Where(i => i.IsSelected).Select(i => i.Line.Text));

        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
