namespace OMNI.Models;

public class Coordinates : IEquatable<Coordinates>
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Heading { get; init; }
    public DateTime Timestamp { get; init; }

    // Tolerance for coordinate comparison
    private const float CoordinateTolerance = 0.1f;
    private const float HeadingTolerance = 5.0f;

    public Coordinates(float x, float y, float heading = 0)
    {
        X = x;
        Y = y;
        Heading = heading;
        Timestamp = DateTime.Now;
    }

    public bool Equals(Coordinates? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Math.Abs(X - other.X) < CoordinateTolerance &&
               Math.Abs(Y - other.Y) < CoordinateTolerance &&
               (Math.Abs(Heading - other.Heading) < HeadingTolerance ||
                Math.Abs(Heading - other.Heading + 360) < HeadingTolerance ||
                Math.Abs(Heading - other.Heading - 360) < HeadingTolerance);
    }

    public override bool Equals(object? obj) => Equals(obj as Coordinates);

    public override int GetHashCode() => HashCode.Combine(
        Math.Round(X / CoordinateTolerance),
        Math.Round(Y / CoordinateTolerance),
        Math.Round(Heading / HeadingTolerance)
    );

    public override string ToString() => $"X: {X:F2}, Y: {Y:F2}, Heading: {Heading:F0}°";
}