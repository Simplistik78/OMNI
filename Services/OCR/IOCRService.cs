using System.Drawing;
using OMNI.Models;

namespace OMNI.Services.OCR;

public interface IOCRService : IDisposable
{
    Task<(Coordinates? Coordinates, string RawText)> ProcessImageAsync(Bitmap image);
    void UpdateSettings(OCRSettings settings);
}

public class OCRSettings
{
    public int BrightnessThreshold { get; set; } = 200;
    public bool InvertColors { get; set; } = false;
    public string AllowedCharacters { get; set; } = "Your location:.0123456789 ";
}