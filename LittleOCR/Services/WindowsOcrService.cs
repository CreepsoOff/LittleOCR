using System.IO;
using System.Text;
using LittleOCR.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinOcr = Windows.Media.Ocr;

namespace LittleOCR.Services;

/// <summary>
/// OCR service backed by the native Windows.Media.Ocr API.
/// Works 100% offline; no cloud calls or external processes are involved.
/// Requirements: Windows 10 build 19041+ with at least one OCR language pack installed.
/// </summary>
public sealed class WindowsOcrService : IOcrService
{
    /// <summary>Returns true if at least one OCR language is available on this machine.</summary>
    public static bool IsAvailable() => WinOcr.OcrEngine.AvailableRecognizerLanguages.Count > 0;

    /// <summary>
    /// Runs OCR on the file at <paramref name="imagePath"/>.
    /// Prefers French; falls back to English, then to any installed language.
    /// </summary>
    /// <exception cref="InvalidOperationException">No OCR language pack is installed.</exception>
    /// <exception cref="IOException">Image file could not be read.</exception>
    public async Task<OcrResult> RecognizeAsync(string imagePath)
    {
        var engine = CreateBestEngine()
            ?? throw new InvalidOperationException(
                "Aucun moteur OCR n'est disponible sur ce système.\n" +
                "Ajoutez un pack de langue Windows (ex. Français) dans Paramètres › Heure et langue › Langue.");

        var softwareBitmap = await LoadBitmapAsync(imagePath);

        var rawResult = await engine.RecognizeAsync(softwareBitmap);

        return MapResult(rawResult);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static WinOcr.OcrEngine? CreateBestEngine()
    {
        // Preferred language order: French → English → any available
        var candidates = new[] { "fr", "en" };

        foreach (var tag in candidates)
        {
            var lang = new Language(tag);
            if (WinOcr.OcrEngine.IsLanguageSupported(lang))
                return WinOcr.OcrEngine.TryCreateFromLanguage(lang);
        }

        return WinOcr.OcrEngine.TryCreateFromUserProfileLanguages();
    }

    private static async Task<SoftwareBitmap> LoadBitmapAsync(string imagePath)
    {
        // Read the file into an in-memory stream to avoid StorageFile permission issues
        // in a regular WPF desktop application.
        await using var fileStream = File.OpenRead(imagePath);
        using var raStream = new InMemoryRandomAccessStream();
        await fileStream.CopyToAsync(raStream.AsStreamForWrite());
        raStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(raStream);
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
    }

    private static OcrResult MapResult(WinOcr.OcrResult raw)
    {
        var lines = new List<OcrLine>(raw.Lines.Count);
        var fullText = new StringBuilder();

        foreach (var rawLine in raw.Lines)
        {
            var words = rawLine.Words
                .Select(w => new OcrWord(
                    w.Text,
                    new OcrRect(w.BoundingRect.X, w.BoundingRect.Y,
                                w.BoundingRect.Width, w.BoundingRect.Height)))
                .ToList();

            // Skip empty lines (defensive: the Windows OCR API should not return these)
            if (words.Count == 0) continue;

            var lineText = string.Join(" ", words.Select(w => w.Text));
            var lineBbox = OcrRect.Union(words.Select(w => w.BoundingBox));

            lines.Add(new OcrLine(lineText, words, lineBbox));
            fullText.AppendLine(lineText);
        }

        return new OcrResult(fullText.ToString().TrimEnd(), lines);
    }
}
