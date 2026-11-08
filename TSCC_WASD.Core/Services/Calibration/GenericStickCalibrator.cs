using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Core.Services.Calibration;

/// <summary>
/// Calibration wizard logic for any controller, working on normalized (uncalibrated) stick values:
/// step 1 averages the resting position, step 2 records the furthest travel in every direction while
/// the user circles both sticks at the edge. Same flow as <see cref="StickCalibrator"/> for the NS2 Pro.
/// </summary>
/// <remarks>
/// Wizard use: call <see cref="StickCalibrationStore.BeginCalibration"/> when calibration starts (clears an old NS2 Pro user calibration so the samples are factory-calibrated), then feed <c>MappingSession.LatestUncalibratedInput</c> samples to <see cref="AddCenterSample"/> while the sticks
/// are released until <see cref="CenterReady"/>, then to <see cref="AddRangeSample"/> until <see cref="RangeReady"/>
/// (show <see cref="Progress"/>), then save <see cref="Result"/> in a <see cref="StickCalibrationStore"/>.
/// </remarks>
public enum CenterCheck { Ok, Unsteady, OffCenter }

public sealed class GenericStickCalibrator
{
    /// <summary>Each direction must travel at least this far (normalized units, about 29% of full travel, like the NS2 wizard).</summary>
    public const int MinimumTravel = 9600;
    public const int CenterSamplesNeeded = 50;
    /// <summary>The 50 centre samples may spread at most this far per axis (max - min, normalized units, about 4.6% of full travel).</summary>
    public const int MaxCenterSpread = 1500;
    /// <summary>A measured centre further than this from 0 on any axis is refused (about 24% of full travel; a Switch Pro rests near -3800, the NS2 factory centres are well inside).</summary>
    public const int MaxCenterOffset = 8000;
    /// <summary>The outermost part of the measured travel still counts as full deflection.</summary>
    private const double OuterDeadZone = 0.05;

    private readonly long[] _centerSum = new long[4];
    private int _centerSamples;
    private int[] _center = new int[4];
    private readonly int[] _min = new int[4], _max = new int[4];
    private readonly int[] _positive = new int[4], _negative = new int[4];

    public bool CenterReady => _centerSamples >= CenterSamplesNeeded;

    /// <summary>Step 1: feed samples while the sticks are released.</summary>
    public void AddCenterSample(Gamepad sample)
    {
        int[] values = Values(sample);
        for (int i = 0; i < 4; i++)
        {
            _centerSum[i] += values[i];
            if (_centerSamples == 0 || values[i] < _min[i]) _min[i] = values[i];
            if (_centerSamples == 0 || values[i] > _max[i]) _max[i] = values[i];
        }
        _centerSamples++;
        _center = [.. _centerSum.Select(sum => (int)Math.Round(sum / (double)_centerSamples))];
    }

    /// <summary>Checks the finished centre: steady (small spread) and plausible (near 0).</summary>
    public CenterCheck CheckCenter()
    {
        if (!CenterReady) return CenterCheck.Ok;
        for (int i = 0; i < 4; i++) if (_max[i] - _min[i] > MaxCenterSpread) return CenterCheck.Unsteady;
        for (int i = 0; i < 4; i++) if (Math.Abs(_center[i]) > MaxCenterOffset) return CenterCheck.OffCenter;
        return CenterCheck.Ok;
    }

    /// <summary>Step 2: feed samples while the sticks circle the edge.</summary>
    public void AddRangeSample(Gamepad sample)
    {
        int[] values = Values(sample);
        for (int i = 0; i < 4; i++)
        {
            int delta = values[i] - _center[i];
            if (delta > _positive[i]) _positive[i] = delta;
            else if (-delta > _negative[i]) _negative[i] = -delta;
        }
    }

    /// <summary>Starts step 2 over, e.g. after the user retries.</summary>
    public void ResetRange()
    {
        Array.Clear(_positive);
        Array.Clear(_negative);
    }

    /// <summary>Progress per direction, 0–1, in the order left +X, −X, +Y, −Y, then right +X, −X, +Y, −Y.</summary>
    public IReadOnlyList<double> Progress =>
        [.. Enumerable.Range(0, 4).SelectMany(i => new[] { _positive[i], _negative[i] })
            .Select(travel => Math.Min(1.0, travel / (double)MinimumTravel))];

    public bool RangeReady => Progress.All(p => p >= 1.0);

    public StickCalibrationSet Result()
    {
        if (!CenterReady || !RangeReady) throw new InvalidOperationException("Calibration is incomplete.");
        AxisCalibration Axis(int i) => new((short)_center[i], Effective(_positive[i]), Effective(_negative[i]));
        return new(new(Axis(0), Axis(1)), new(Axis(2), Axis(3)));
    }

    private static int Effective(int travel) => Math.Max(1, (int)(travel * (1 - OuterDeadZone)));
    private static int[] Values(Gamepad g) => [g.LeftThumbX, g.LeftThumbY, g.RightThumbX, g.RightThumbY];
}
