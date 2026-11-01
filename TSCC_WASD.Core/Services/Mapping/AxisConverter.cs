namespace TSCC_WASD.Core.Services.Mapping;

public static class AxisConverter
{
    // Promote to double before abs/negation so short.MinValue is safe.
    public static byte Convert(short value, double deadZone, bool invert = false)
    {
        if (!double.IsFinite(deadZone) || deadZone < 0 || deadZone >= 1)
            throw new ArgumentOutOfRangeException(nameof(deadZone));
        double normalized = value < 0 ? value / 32768.0 : value / 32767.0;
        double magnitude = Math.Abs(normalized);
        if (magnitude <= deadZone) return 128;
        normalized = Math.Sign(normalized) * (magnitude - deadZone) / (1 - deadZone);
        if (invert) normalized = -normalized;
        return (byte)Math.Clamp((int)Math.Round(128 + normalized * (normalized < 0 ? 128 : 127)), 0, 255);
    }

    /// <summary>
    /// Radial dead zone for one stick: the X/Y pair is judged by its combined distance from centre, and
    /// the travel between the dead-zone edge and full deflection is rescaled along the same direction.
    /// Returns signed 16-bit values (0 at rest, short.MinValue/MaxValue at full deflection).
    /// </summary>
    public static (short X, short Y) ApplyRadial(short x, short y, double deadZone)
    {
        if (!double.IsFinite(deadZone) || deadZone < 0 || deadZone >= 1)
            throw new ArgumentOutOfRangeException(nameof(deadZone));
        double nx = x < 0 ? x / 32768.0 : x / 32767.0;
        double ny = y < 0 ? y / 32768.0 : y / 32767.0;
        double magnitude = Math.Sqrt(nx * nx + ny * ny);
        if (magnitude <= deadZone) return (0, 0);
        // Rescaled radius, capped at 1 so a diagonal full push stays inside the circle.
        double radius = Math.Min((magnitude - deadZone) / (1 - deadZone), 1.0);
        double scale = radius / magnitude;
        return (ToShort(nx * scale), ToShort(ny * scale));
    }

    /// <summary>Maps a signed 16-bit axis to the DS4 byte range (128 centre), optionally inverted.</summary>
    public static byte ToByte(short value, bool invert = false)
    {
        double normalized = value < 0 ? value / 32768.0 : value / 32767.0;
        if (invert) normalized = -normalized;
        return (byte)Math.Clamp((int)Math.Round(128 + normalized * (normalized < 0 ? 128 : 127)), 0, 255);
    }

    private static short ToShort(double normalized)
        => (short)Math.Clamp((int)Math.Round(normalized * (normalized < 0 ? 32768.0 : 32767.0)), short.MinValue, short.MaxValue);
}
