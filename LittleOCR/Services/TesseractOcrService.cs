using System.IO;
using System.Text;
using LittleOCR.Models;
using Tesseract;

namespace LittleOCR.Services;

/// <summary>
/// OCR service backed by Tesseract 5 (offline).
/// Returns character-level bounding boxes (<see cref="OcrChar"/>) grouped into words and lines,
/// enabling character-by-character overlay selection in the UI.
///
/// Tessdata layout expected:
///   &lt;tessdata_dir&gt;/fra.traineddata   (French — preferred)
///   &lt;tessdata_dir&gt;/eng.traineddata   (English — fallback)
///
/// The service looks for tessdata in the following directories (first match wins):
///   1. &lt;exe directory&gt;/tessdata/
///   2. &lt;AppContext.BaseDirectory&gt;/tessdata/
///   3. %APPDATA%/LittleOCR/tessdata/
/// </summary>
public sealed class TesseractOcrService : IOcrService
{
    private static readonly string[] TessdataSearchPaths =
    [
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "tessdata"),
        Path.Combine(AppContext.BaseDirectory, "tessdata"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "LittleOCR", "tessdata"),
    ];

    /// <summary>Returns the first tessdata directory that contains at least one language file.</summary>
    public static string? FindTessdata()
    {
        foreach (var path in TessdataSearchPaths)
        {
            if (Directory.Exists(path) &&
                (File.Exists(Path.Combine(path, "fra.traineddata")) ||
                 File.Exists(Path.Combine(path, "eng.traineddata"))))
                return path;
        }
        return null;
    }

    public async Task<OcrResult> RecognizeAsync(string imagePath)
    {
        var tessdata = FindTessdata()
            ?? throw new InvalidOperationException(
                "Tesseract : aucun dossier tessdata trouvé.\n" +
                "Placez fra.traineddata (ou eng.traineddata) dans l'un de ces emplacements :\n" +
                $"  • {TessdataSearchPaths[0]}\n" +
                $"  • {TessdataSearchPaths[1]}\n" +
                $"  • {TessdataSearchPaths[2]}");

        return await Task.Run(() => RunTesseract(imagePath, tessdata));
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static OcrResult RunTesseract(string imagePath, string tessdata)
    {
        var lang = File.Exists(Path.Combine(tessdata, "fra.traineddata")) ? "fra" : "eng";

        using var engine = new TesseractEngine(tessdata, lang, EngineMode.Default);
        using var pix    = Pix.LoadFromFile(imagePath);
        using var page   = engine.Process(pix);

        var lines            = new List<OcrLine>();
        var chars            = new List<OcrChar>();
        var fullText         = new StringBuilder();
        var currentLineWords = new List<OcrWord>();
        var currentWordChars = new List<OcrChar>();

        int lineId = -1;
        int wordId = -1;

        using var iter = page.GetIterator();
        iter.Begin();

        do
        {
            // ── Entering a new text line ───────────────────────────────────────
            if (iter.IsAtBeginningOf(PageIteratorLevel.TextLine))
            {
                // Flush the last word of the previous line before closing it
                if (currentWordChars.Count > 0)
                {
                    CommitWord(currentWordChars, currentLineWords);
                    currentWordChars.Clear();
                }
                if (currentLineWords.Count > 0)
                {
                    CommitLine(currentLineWords, lines, fullText);
                    currentLineWords.Clear();
                }
                lineId++;
            }

            // ── Entering a new word (also fires at start of every line) ────────
            if (iter.IsAtBeginningOf(PageIteratorLevel.Word))
            {
                // currentWordChars was already flushed by the line handler above
                // when both events fire together; only flush here for mid-line words.
                if (currentWordChars.Count > 0)
                {
                    CommitWord(currentWordChars, currentLineWords);
                    currentWordChars.Clear();
                }
                wordId++;
            }

            // ── Process the current character (Symbol) ─────────────────────────
            var charText = iter.GetText(PageIteratorLevel.Symbol);
            if (!string.IsNullOrWhiteSpace(charText) &&
                iter.TryGetBoundingBox(PageIteratorLevel.Symbol, out var cb))
            {
                var ocrChar = new OcrChar(
                    charText,
                    new OcrRect(cb.X1, cb.Y1, cb.Width, cb.Height),
                    wordId,
                    lineId);

                chars.Add(ocrChar);
                currentWordChars.Add(ocrChar);
            }
        }
        while (iter.Next(PageIteratorLevel.Symbol));

        // Flush the last word and line after the loop ends
        if (currentWordChars.Count > 0)
            CommitWord(currentWordChars, currentLineWords);
        if (currentLineWords.Count > 0)
            CommitLine(currentLineWords, lines, fullText);

        return new OcrResult(fullText.ToString().TrimEnd(), lines, chars);
    }

    private static void CommitWord(List<OcrChar> wordChars, List<OcrWord> lineWords)
    {
        var text = string.Concat(wordChars.Select(c => c.Text));
        if (string.IsNullOrWhiteSpace(text)) return;

        var bbox = OcrRect.Union(wordChars.Select(c => c.BoundingBox));
        lineWords.Add(new OcrWord(text, bbox));
    }

    private static void CommitLine(List<OcrWord> words, List<OcrLine> lines, StringBuilder fullText)
    {
        var text = string.Join(" ", words.Select(w => w.Text));
        if (string.IsNullOrWhiteSpace(text)) return;

        var bbox = OcrRect.Union(words.Select(w => w.BoundingBox));
        lines.Add(new OcrLine(text, words.ToList(), bbox));
        fullText.AppendLine(text);
    }
}
