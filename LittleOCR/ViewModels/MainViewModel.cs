using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
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
/// A single selectable unit on the OCR overlay canvas.
/// May represent a line, a word, or a single character depending on the active engine.
/// <para>
/// <see cref="GroupId"/> is a word-level identifier used by <c>CopySelection</c> to decide
/// whether to concatenate directly (same word) or insert a space (different words).
/// <see cref="LineId"/> drives newline insertion between lines.
/// </para>
/// </summary>
public sealed class OcrOverlayItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string  Text        { get; }
    public OcrRect BoundingBox { get; }

    /// <summary>Document-wide word index. Characters from the same word share this value.</summary>
    public int GroupId { get; }

    /// <summary>Document-wide line index.</summary>
    public int LineId { get; }

    // Canvas placement (set by the View after computing the image transform)
    public double CanvasX { get; set; }
    public double CanvasY { get; set; }
    public double CanvasW { get; set; }
    public double CanvasH { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public OcrOverlayItem(string text, OcrRect boundingBox, int lineId, int groupId)
    {
        Text        = text;
        BoundingBox = boundingBox;
        LineId      = lineId;
        GroupId     = groupId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Main ViewModel ─────────────────────────────────────────────────────────────

public sealed class MainViewModel : INotifyPropertyChanged
{
    // ── Engine options (toolbar dropdown) ─────────────────────────────────────

    public IReadOnlyList<OcrEngineOption> EngineOptions { get; } =
    [
        new(OcrEngineMode.WindowsWord,  "Windows OCR  (mots)"),
        new(OcrEngineMode.WindowsLine,  "Windows OCR  (lignes)"),
        new(OcrEngineMode.Tesseract,    "Tesseract  (caractères)"),
    ];

    private OcrEngineOption _selectedEngineOption;

    public OcrEngineOption SelectedEngineOption
    {
        get => _selectedEngineOption;
        set
        {
            if (_selectedEngineOption == value) return;

            var previous = _selectedEngineOption;
            _selectedEngineOption = value;
            OnPropertyChanged();

            if (_ocrResult is null) return;

            // If both the previous and new engine are Windows-based we can reuse
            // the existing result and just change overlay granularity.
            bool prevIsWindows = previous.Mode != OcrEngineMode.Tesseract;
            bool newIsWindows  = value.Mode    != OcrEngineMode.Tesseract;

            if (prevIsWindows && newIsWindows)
            {
                RebuildOverlayItems();
            }
            else
            {
                // Different engine family → the result is no longer valid.
                _resultEngineMode = null;
                OcrResult         = null;
                OcrState          = OcrState.Idle;
            }
        }
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private BitmapSource? _imageSource;
    private string?       _imagePath;
    private OcrResult?    _ocrResult;
    private OcrState      _ocrState = OcrState.Idle;
    private bool          _isOverlayVisible = true;
    private string        _statusMessage = string.Empty;
    private OcrEngineMode? _resultEngineMode;   // which engine produced the current result

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

    /// <summary>Overlay units (line / word / character) produced by the last OCR run.</summary>
    public ObservableCollection<OcrOverlayItem> OverlayItems { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand OpenImageCommand     { get; }
    public ICommand RunOcrCommand        { get; }
    public ICommand CopyAllCommand       { get; }
    public ICommand CopySelectionCommand { get; }
    public ICommand ToggleOverlayCommand { get; }
    public ICommand ClearStatusCommand   { get; }

    public MainViewModel()
    {
        _selectedEngineOption = EngineOptions[0]; // default: Windows OCR (mots)

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
            bitmap.UriSource  = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            ImageSource       = bitmap;
            _imagePath        = path;
            _resultEngineMode = null;
            OcrResult         = null;
            OcrState          = OcrState.Idle;
            StatusMessage     = string.Empty;
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
            IOcrService service = _selectedEngineOption.Mode switch
            {
                OcrEngineMode.Tesseract   => new TesseractOcrService(),
                _                         => new WindowsOcrService(),
            };

            var result = await service.RecognizeAsync(_imagePath);

            _resultEngineMode = _selectedEngineOption.Mode;
            OcrResult         = result;
            OcrState          = OcrState.Success;

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

    /// <summary>
    /// Rebuilds <see cref="OverlayItems"/> from the current OCR result using the
    /// granularity that matches the selected engine mode:
    /// <list type="bullet">
    ///   <item>Tesseract → one item per <see cref="OcrChar"/> (character-level)</item>
    ///   <item>WindowsWord → one item per <see cref="OcrWord"/> (word-level)</item>
    ///   <item>WindowsLine → one item per <see cref="OcrLine"/> (line-level)</item>
    /// </list>
    /// </summary>
    private void RebuildOverlayItems()
    {
        OverlayItems.Clear();

        if (_ocrResult is null) return;

        var mode = _selectedEngineOption.Mode;

        if (mode == OcrEngineMode.Tesseract && _ocrResult.Chars is { Count: > 0 } chars)
        {
            foreach (var ch in chars)
                OverlayItems.Add(new OcrOverlayItem(ch.Text, ch.BoundingBox, ch.LineId, ch.WordId));
        }
        else if (mode == OcrEngineMode.WindowsLine)
        {
            int lineId = 0;
            foreach (var line in _ocrResult.Lines)
            {
                OverlayItems.Add(new OcrOverlayItem(line.Text, line.BoundingBox, lineId, lineId));
                lineId++;
            }
        }
        else // WindowsWord (or Tesseract without char data — fallback to word level)
        {
            int lineId = 0, wordId = 0;
            foreach (var line in _ocrResult.Lines)
            {
                foreach (var word in line.Words)
                    OverlayItems.Add(new OcrOverlayItem(word.Text, word.BoundingBox, lineId, wordId++));
                lineId++;
            }
        }
    }

    private void CopyAll()
    {
        if (_ocrResult?.FullText is { Length: > 0 } text)
            Clipboard.SetText(text);
    }

    /// <summary>
    /// Copies the selected overlay items to the clipboard.
    /// Items are sorted left-to-right within each line, then top-to-bottom.
    /// Grouping rules:
    /// <list type="bullet">
    ///   <item>Same <see cref="OcrOverlayItem.GroupId"/> → concatenate without separator (same word)</item>
    ///   <item>Different GroupId, same <see cref="OcrOverlayItem.LineId"/> → insert a space</item>
    ///   <item>Different LineId → insert a line break</item>
    /// </list>
    /// </summary>
    private void CopySelection()
    {
        var selected = OverlayItems
            .Where(i => i.IsSelected)
            .OrderBy(i => i.LineId)
            .ThenBy(i => i.BoundingBox.X)
            .ToList();

        if (selected.Count == 0) return;

        var sb        = new StringBuilder();
        int prevLine  = -1;
        int prevGroup = -1;

        foreach (var item in selected)
        {
            if (prevLine >= 0)
            {
                if (item.LineId != prevLine)
                    sb.AppendLine();
                else if (item.GroupId != prevGroup)
                    sb.Append(' ');
            }

            sb.Append(item.Text);
            prevLine  = item.LineId;
            prevGroup = item.GroupId;
        }

        var text = sb.ToString();
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

