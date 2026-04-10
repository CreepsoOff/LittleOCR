namespace LittleOCR.Models;

/// <summary>
/// Bounding box in image pixel coordinates (top-left origin).
/// Uses our own type to avoid Windows.Foundation.Rect namespace pollution throughout the codebase.
/// </summary>
public readonly record struct OcrRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>Computes the smallest rectangle that contains all given rects.</summary>
    /// <returns>The union rect, or <see cref="Empty"/> if <paramref name="rects"/> is empty.</returns>
    public static OcrRect Union(IEnumerable<OcrRect> rects)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxR = double.MinValue, maxB = double.MinValue;
        bool any = false;

        foreach (var r in rects)
        {
            any = true;
            if (r.X < minX) minX = r.X;
            if (r.Y < minY) minY = r.Y;
            if (r.Right > maxR) maxR = r.Right;
            if (r.Bottom > maxB) maxB = r.Bottom;
        }

        return any ? new OcrRect(minX, minY, maxR - minX, maxB - minY) : Empty;
    }

    /// <summary>An empty/zero rectangle.</summary>
    public static readonly OcrRect Empty = new(0, 0, 0, 0);
}

/// <summary>Individual recognized word with its bounding box in image coordinates.</summary>
public record OcrWord(string Text, OcrRect BoundingBox);

/// <summary>Recognized line containing one or more words.</summary>
public record OcrLine(string Text, IReadOnlyList<OcrWord> Words, OcrRect BoundingBox);

/// <summary>Complete OCR analysis result for a single image.</summary>
public record OcrResult(string FullText, IReadOnlyList<OcrLine> Lines);
