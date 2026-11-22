using System.ComponentModel;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>Thrown when HidHide denied access to a controller we hid ourselves and recovery did not help.</summary>
internal sealed class HidHideDeniedException() : Win32Exception(5);

/// <summary>
/// Opens a HID interface and, if HidHide denies a device TSCC_WASD hid itself (error 5), registers the app again and,
/// if that is not enough, unhides that one device just long enough to open every handle in one go
/// (open handles stay valid once it is hidden again). After a failed recovery the same path is not retried.
/// </summary>
internal sealed class HidHideOpener(string label, Func<IHidHideRecovery?>? recovery = null)
{
    /// <summary>Set by the mapping coordinator while it hides controllers; null otherwise.</summary>
    public static volatile IHidHideRecovery? Shared;

    private readonly Func<IHidHideRecovery?> _recovery = recovery ?? (() => Shared);
    private string? _deniedPath; // HID path whose recovery already failed

    /// <summary>True when a recovery is available (used to word the status text).</summary>
    public bool HasRecovery => _recovery() is not null;

    /// <summary>
    /// Runs <paramref name="open"/>, which must open all handles and throw a <see cref="Win32Exception"/> when the
    /// required one fails (and release anything it opened before throwing). Any other failure is rethrown unchanged.
    /// </summary>
    public T Open<T>(string path, Func<T> open)
    {
        try
        {
            var result = open();
            _deniedPath = null;
            return result;
        }
        catch (Win32Exception ex) when (ex is not HidHideDeniedException && ex.NativeErrorCode == 5
            && _recovery() is { } recovery && recovery.IsHiddenByUs(path))
        {
            // Recovery already failed for this path: do not hammer the CLI every second.
            if (string.Equals(_deniedPath, path, StringComparison.OrdinalIgnoreCase)) throw new HidHideDeniedException();
            AppLog.Warn($"{label} HID open denied (error 5) while hidden by HidHide: {path}");
            try
            {
                recovery.ReRegisterApp();
                try
                {
                    var again = open();
                    AppLog.Info($"{label} HID open succeeded after re-registering the app with HidHide");
                    _deniedPath = null;
                    return again;
                }
                catch (Win32Exception) { }
                var result = recovery.OpenWhileUnhidden(path, open);
                AppLog.Info($"{label} HID open succeeded after unhiding the device for a moment");
                _deniedPath = null;
                return result;
            }
            catch (Exception e) when (e is Win32Exception or IOException or TimeoutException or InvalidOperationException)
            {
                AppLog.Warn($"{label} HID open still denied after HidHide recovery: {e.Message}");
                _deniedPath = path;
                throw new HidHideDeniedException();
            }
        }
    }
}
