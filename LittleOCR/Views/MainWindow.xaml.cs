using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LittleOCR.Models;
using LittleOCR.ViewModels;

namespace LittleOCR.Views;

public partial class MainWindow : Window
{
    // ── Overlay appearance constants ──────────────────────────────────────────
    private static readonly Color OverlayNormalColor   = Color.FromArgb(0x28, 0x00, 0xBF, 0xFF); // deep-sky-blue / 16 %
    private static readonly Color OverlaySelectedColor = Color.FromArgb(0x50, 0xFF, 0xC0, 0x00); // amber / 31 %
    private static readonly Color StrokeNormal         = Color.FromArgb(0xCC, 0x00, 0xBF, 0xFF);
    private static readonly Color StrokeSelected       = Color.FromArgb(0xFF, 0xFF, 0xC0, 0x00);
    private const double StrokeThickness = 1.5;
    private const double CornerRadius    = 3.0;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();

        // Subscribe to ViewModel changes to keep the canvas in sync
        ViewModel.PropertyChanged      += OnViewModelPropertyChanged;
        ViewModel.OverlayItems.CollectionChanged += (_, _) => RedrawOverlay();
    }

    // ── Convenience accessor ──────────────────────────────────────────────────

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    // ── ViewModel change handlers ─────────────────────────────────────────────

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsOverlayVisible):
                // Visibility is handled by XAML binding; we only need canvas positions
                break;
            case nameof(MainViewModel.HasOcrResult):
                RedrawOverlay();
                break;
        }
    }

    // ── Image size change → re-compute overlay positions ─────────────────────

    private void OnImageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Sync the overlay canvas size to the image control size so hit-testing works
        OcrOverlayCanvas.Width  = DisplayedImage.ActualWidth;
        OcrOverlayCanvas.Height = DisplayedImage.ActualHeight;

        RedrawOverlay();
    }

    // ── Canvas background click → deselect all ────────────────────────────────

    private void OnCanvasBackgroundClicked(object sender, MouseButtonEventArgs e)
    {
        // Only deselect when the user clicks the canvas itself, not a child rect
        if (e.Source == OcrOverlayCanvas)
        {
            foreach (var item in ViewModel.OverlayItems)
                item.IsSelected = false;
        }
    }

    // ── Drag & Drop ──────────────────────────────────────────────────────────

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            ViewModel.LoadImage(files[0]);
    }

    // ── Overlay rendering ─────────────────────────────────────────────────────

    /// <summary>
    /// Clears and redraws all OCR overlay rectangles on the canvas.
    /// Called when OCR results change, when overlay items are added/removed,
    /// or when the image control is resized (e.g., window resize).
    /// </summary>
    private void RedrawOverlay()
    {
        OcrOverlayCanvas.Children.Clear();

        if (!ViewModel.HasOcrResult || ViewModel.OverlayItems.Count == 0)
            return;

        var transform = ComputeImageTransform();
        if (transform is null)
            return;

        var (scaleX, scaleY, offsetX, offsetY) = transform.Value;

        foreach (var item in ViewModel.OverlayItems)
        {
            var box = item.BoundingBox;

            double cx = offsetX + box.X * scaleX;
            double cy = offsetY + box.Y * scaleY;
            double cw = box.Width  * scaleX;
            double ch = box.Height * scaleY;

            // Update cached canvas coords for selection info, etc.
            item.CanvasX = cx;
            item.CanvasY = cy;
            item.CanvasW = cw;
            item.CanvasH = ch;

            var rect = BuildOverlayRect(item, cx, cy, cw, ch);
            OcrOverlayCanvas.Children.Add(rect);
        }
    }

    /// <summary>
    /// Computes the scale factors and offset needed to map image-pixel coordinates
    /// onto the WPF Canvas that is stacked on top of the displayed Image control.
    ///
    /// The Image control uses <c>Stretch="Uniform"</c>, so the rendered content
    /// may be letterboxed inside the control bounds. This method accounts for that.
    /// </summary>
    private (double scaleX, double scaleY, double offsetX, double offsetY)? ComputeImageTransform()
    {
        var src = ViewModel.ImageSource;
        if (src is null) return null;

        double ctrlW = DisplayedImage.ActualWidth;
        double ctrlH = DisplayedImage.ActualHeight;
        if (ctrlW <= 0 || ctrlH <= 0) return null;

        double imgW = src.PixelWidth;
        double imgH = src.PixelHeight;
        if (imgW <= 0 || imgH <= 0) return null;

        double imageAspect   = imgW / imgH;
        double controlAspect = ctrlW / ctrlH;

        double renderedW, renderedH;
        if (imageAspect > controlAspect)
        {
            // Image is wider than the container → letterboxed (bars on top and bottom)
            renderedW = ctrlW;
            renderedH = ctrlW / imageAspect;
        }
        else
        {
            // Image is taller than the container → pillarboxed (bars on left and right)
            renderedH = ctrlH;
            renderedW = ctrlH * imageAspect;
        }

        double offsetX = (ctrlW - renderedW) / 2.0;
        double offsetY = (ctrlH - renderedH) / 2.0;
        double scaleX  = renderedW / imgW;
        double scaleY  = renderedH / imgH;

        return (scaleX, scaleY, offsetX, offsetY);
    }

    /// <summary>Creates a styled Rectangle for one OCR overlay item.</summary>
    private Rectangle BuildOverlayRect(OcrOverlayItem item,
                                       double cx, double cy, double cw, double ch)
    {
        var rect = new Rectangle
        {
            Width           = Math.Max(cw, 4),
            Height          = Math.Max(ch, 4),
            RadiusX         = CornerRadius,
            RadiusY         = CornerRadius,
            StrokeThickness = StrokeThickness,
            Fill            = new SolidColorBrush(item.IsSelected ? OverlaySelectedColor : OverlayNormalColor),
            Stroke          = new SolidColorBrush(item.IsSelected ? StrokeSelected       : StrokeNormal),
            Cursor          = Cursors.Hand,
            ToolTip         = item.Text,
            Tag             = item,
        };

        Canvas.SetLeft(rect, cx);
        Canvas.SetTop(rect,  cy);

        rect.MouseLeftButtonDown += OnOverlayRectClicked;

        // Re-render when selection changes (item.IsSelected is set from the click handler)
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(OcrOverlayItem.IsSelected))
                UpdateRectStyle(rect, item.IsSelected);
        };

        return rect;
    }

    private static void UpdateRectStyle(Rectangle rect, bool selected)
    {
        rect.Fill   = new SolidColorBrush(selected ? OverlaySelectedColor : OverlayNormalColor);
        rect.Stroke = new SolidColorBrush(selected ? StrokeSelected       : StrokeNormal);
    }

    /// <summary>
    /// Handles a click on an overlay rectangle.
    /// Ctrl+Click toggles the item; plain click toggles and deselects others.
    /// </summary>
    private void OnOverlayRectClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle { Tag: OcrOverlayItem clickedItem })
            return;

        bool ctrlHeld = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        if (!ctrlHeld)
        {
            // Deselect all items that are NOT the clicked one
            foreach (var item in ViewModel.OverlayItems)
            {
                if (item != clickedItem)
                    item.IsSelected = false;
            }
        }

        clickedItem.IsSelected = !clickedItem.IsSelected;
        e.Handled = true; // Prevent canvas background handler from firing
    }
}
