using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Core.Services.Calibration;

/// <summary>
/// One axis on normalized values (the <c>short</c> stick values of <see cref="Gamepad"/>): the resting value and how far
/// the stick travels each way from it. The travel is the effective one, already reduced by the outer saturation.
/// </summary>
public readonly record struct AxisCalibration(short Center, int Positive, int Negative)
{
    public static AxisCalibration Identity => new(0, short.MaxValue, -(int)short.MinValue);

    public bool IsValid => Positive >= 1 && Negative >= 1;

    /// <summary>Rest maps to 0, <c>Center + Positive</c> to full positive, <c>Center - Negative</c> to full negative; beyond that clamps.</summary>
    public short Apply(short value)
    {
        if (!IsValid) return value;
        int delta = value - Center;
        if (delta >= 0)
            return (short)Math.Round(Math.Min(1.0, delta / (double)Positive) * short.MaxValue);
        return (short)Math.Round(Math.Max(-1.0, delta / (double)Negative) * -(double)short.MinValue);
    }
}

public readonly record struct StickCalibration(AxisCalibration X, AxisCalibration Y)
{
    public bool IsValid => X.IsValid && Y.IsValid;
}

/// <summary>Calibration of both sticks. Applied to the normalized state before the dead zone.</summary>
public sealed record StickCalibrationSet(StickCalibration Left, StickCalibration Right)
{
    public bool IsValid => Left.IsValid && Right.IsValid;

    public Gamepad Apply(Gamepad gamepad)
    {
        gamepad.LeftThumbX = Left.X.Apply(gamepad.LeftThumbX);
        gamepad.LeftThumbY = Left.Y.Apply(gamepad.LeftThumbY);
        gamepad.RightThumbX = Right.X.Apply(gamepad.RightThumbX);
        gamepad.RightThumbY = Right.Y.Apply(gamepad.RightThumbY);
        return gamepad;
    }

    public State Apply(State state)
    {
        state.Gamepad = Apply(state.Gamepad);
        return state;
    }
}
