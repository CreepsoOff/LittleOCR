using LittleOCR.Models;

namespace LittleOCR.Services;

public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(string imagePath);
}
