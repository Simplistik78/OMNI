using OMNI.Models;
using System.Diagnostics;

namespace OMNI.Services.Capture;

public class TestCoordinateService
{
    private readonly Queue<Coordinates> _testCoordinates;
    private bool _isTestMode = false;

    public TestCoordinateService()
    {
        _testCoordinates = new Queue<Coordinates>(new[]
        {
            // Four corners of an area with different headings
            new Coordinates(3474.14f, 3766.78f, 45),    // Northeast corner, facing northeast
            new Coordinates(3474.14f, 3266.78f, 135),   // Southeast corner, facing southeast
            new Coordinates(2974.14f, 3266.78f, 225),   // Southwest corner, facing southwest
            new Coordinates(2974.14f, 3766.78f, 315),   // Northwest corner, facing northwest
        });
    }

    public void ToggleTestMode()
    {
        _isTestMode = !_isTestMode;
        Debug.WriteLine($"Test mode {(_isTestMode ? "enabled" : "disabled")}");
    }

    public bool IsTestModeEnabled => _isTestMode;

    public Coordinates? GetNextTestCoordinate()
    {
        if (!_isTestMode) return null;

        var coord = _testCoordinates.Dequeue();
        _testCoordinates.Enqueue(coord); // Put it back at the end for cycling
        Debug.WriteLine($"Test coordinate: {coord}");
        return coord;
    }
}