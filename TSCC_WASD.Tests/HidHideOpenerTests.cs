using System.ComponentModel;
using Xunit;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Tests;

[Collection(AppLogCollection.Name)]
public class HidHideOpenerTests
{
    private const string Path = @"\?\hid#vid_057e&pid_2009#7&1&0&0000#{guid}";

    private sealed class FakeRecovery(bool hiddenByUs, bool reRegisterFixes = false, bool unhideWorks = true) : IHidHideRecovery
    {
        public int ReRegisters, Unhides, Checks;
        public bool Fixed;
        public bool IsHiddenByUs(string hidInterfacePath) { Checks++; return hiddenByUs; }
        public void ReRegisterApp() { ReRegisters++; Fixed = reRegisterFixes; }
        public T OpenWhileUnhidden<T>(string hidInterfacePath, Func<T> open)
        {
            Unhides++;
            if (!unhideWorks) throw new InvalidOperationException("cli failed");
            Fixed = true;
            return open();
        }
    }

    private static Func<(string, string)> Open(FakeRecovery? r, int error = 5, int[]? calls = null) => () =>
    {
        if (calls is not null) calls[0]++;
        if (r is { Fixed: true }) return ("read", "write"); // both handles come from the same open call
        throw new Win32Exception(error);
    };

    [Fact]
    public void ReRegistersFirstThenUnhidesAndReturnsBothHandles()
    {
        var r = new FakeRecovery(true);
        var opener = new HidHideOpener("Switch Pro", () => r);
        var (read, write) = opener.Open(Path, Open(r));
        Assert.Equal(("read", "write"), (read, write));
        Assert.Equal(1, r.ReRegisters);
        Assert.Equal(1, r.Unhides);
    }

    [Fact]
    public void ReRegisterAloneIsEnoughWhenItWorks()
    {
        var r = new FakeRecovery(true, reRegisterFixes: true);
        var opener = new HidHideOpener("PlayStation", () => r);
        Assert.Equal(("read", "write"), opener.Open(Path, Open(r)));
        Assert.Equal(1, r.ReRegisters);
        Assert.Equal(0, r.Unhides);
    }

    [Fact]
    public void NotHiddenByUsThrowsOriginalErrorWithoutRecovery()
    {
        var r = new FakeRecovery(false);
        var opener = new HidHideOpener("Switch Pro", () => r);
        var ex = Assert.Throws<Win32Exception>(() => opener.Open(Path, Open(r)));
        Assert.IsNotType<HidHideDeniedException>(ex);
        Assert.Equal(5, ex.NativeErrorCode);
        Assert.Equal(0, r.ReRegisters);
        Assert.Equal(0, r.Unhides);
    }

    [Fact]
    public void OtherErrorsDoNotTouchRecovery()
    {
        var r = new FakeRecovery(true);
        var opener = new HidHideOpener("Switch Pro", () => r);
        var ex = Assert.Throws<Win32Exception>(() => opener.Open(Path, Open(r, error: 32)));
        Assert.Equal(32, ex.NativeErrorCode);
        Assert.Equal(0, r.Checks);
        Assert.Equal(0, r.ReRegisters);
        Assert.Equal(0, r.Unhides);
    }

    [Fact]
    public void NoRecoveryKeepsOriginalError()
    {
        var opener = new HidHideOpener("Switch Pro", () => null);
        Assert.Equal(5, Assert.Throws<Win32Exception>(() => opener.Open(Path, Open(null))).NativeErrorCode);
    }

    [Fact]
    public void FailedRecoveryIsNotRepeatedForTheSamePath()
    {
        var r = new FakeRecovery(true, unhideWorks: false);
        var opener = new HidHideOpener("PlayStation", () => r);
        Assert.Throws<HidHideDeniedException>(() => opener.Open(Path, Open(r)));
        Assert.Equal(1, r.ReRegisters);
        Assert.Equal(1, r.Unhides);

        Assert.Throws<HidHideDeniedException>(() => opener.Open(Path, Open(r)));
        Assert.Equal(1, r.ReRegisters);
        Assert.Equal(1, r.Unhides);
    }
}
