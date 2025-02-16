using System.Drawing;

namespace OMNI.Models;

public class CaptureEntry
{
    public Bitmap Image { get; }
    public DateTime Timestamp { get; }
    public Coordinates? ParsedCoordinates { get; }
    public string RawText { get; }
    public bool WasSuccessful { get; }

    public CaptureEntry(Bitmap image, string rawText, Coordinates? coordinates)
    {
        Image = new Bitmap(image); // Creating a copy to avoid disposal issue
        Timestamp = DateTime.Now;
        ParsedCoordinates = coordinates;
        RawText = rawText;
        WasSuccessful = coordinates != null;
    }

    public void Dispose()
    {
        Image.Dispose();
    }
}