namespace LittleOCR.ViewModels;

public enum OcrEngineMode { WindowsWord, WindowsLine, Tesseract }

/// <summary>Represents one selectable OCR engine in the toolbar dropdown.</summary>
public sealed record OcrEngineOption(OcrEngineMode Mode, string Label)
{
    public override string ToString() => Label;
}
