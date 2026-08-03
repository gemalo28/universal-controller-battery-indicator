using ControllerBattery.Interop;

namespace ControllerBattery.Tests.Interop;

public sealed class WindowsBluetoothTests
{
    [Theory]
    [InlineData("01:23:45:67:89:AB", true)]
    [InlineData("01-23-45-67-89-AB", true)]
    [InlineData("0123456789AB", true)]
    [InlineData(null, false)]
    [InlineData("short", false)]
    [InlineData("GGGGGGGGGGGG", false)]
    public void TryParseAddress_ValidatesCommonBluetoothFormats(string? serial, bool expected)
    {
        Assert.Equal(expected, WindowsBluetooth.TryParseAddress(serial, out var address));
        if (expected) Assert.NotEqual(0, address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public void Disconnect_RejectsMissingAddressWithoutNativeCalls(string? serial) =>
        Assert.Throws<IOException>(() => WindowsBluetooth.Disconnect(serial));

    [Fact]
    public void Disconnect_TriesRadiosUntilOneAcceptsTheRequest_AndClosesEveryHandle()
    {
        var native = new FakeBluetoothNative([new IntPtr(10), new IntPtr(20)], succeedAt: 1);

        WindowsBluetooth.Disconnect("01:23:45:67:89:AB", native);

        Assert.Equal([new IntPtr(10), new IntPtr(20)], native.ClosedHandles);
        Assert.Equal(2, native.DisconnectAttempts);
        Assert.True(native.FindClosed);
        Assert.NotEqual(0, native.LastAddress);
    }

    [Fact]
    public void Disconnect_WhenAllRadiosReject_ReportsLastErrorAndClosesResources()
    {
        var native = new FakeBluetoothNative([new IntPtr(10)], succeedAt: -1)
        {
            LastError = 1167
        };

        var exception = Assert.Throws<IOException>(() =>
            WindowsBluetooth.Disconnect("0123456789AB", native));

        Assert.IsType<System.ComponentModel.Win32Exception>(exception.InnerException);
        Assert.Single(native.ClosedHandles);
        Assert.True(native.FindClosed);
    }

    [Fact]
    public void Disconnect_WhenNoRadioExists_ReportsNativeError()
    {
        var native = new FakeBluetoothNative([], succeedAt: -1) { LastError = 2 };

        var exception = Assert.Throws<IOException>(() =>
            WindowsBluetooth.Disconnect("0123456789AB", native));

        Assert.IsType<System.ComponentModel.Win32Exception>(exception.InnerException);
        Assert.False(native.FindClosed);
    }

    private sealed class FakeBluetoothNative(IntPtr[] radios, int succeedAt)
        : WindowsBluetooth.IWindowsBluetoothNative
    {
        private int _next;
        public int LastError { get; set; }
        public int DisconnectAttempts { get; private set; }
        public long LastAddress { get; private set; }
        public List<IntPtr> ClosedHandles { get; } = [];
        public bool FindClosed { get; private set; }

        public IntPtr FindFirstRadio(out IntPtr radio)
        {
            radio = radios.FirstOrDefault();
            _next = radio == IntPtr.Zero ? 0 : 1;
            return radio == IntPtr.Zero ? IntPtr.Zero : new IntPtr(99);
        }

        public bool FindNextRadio(IntPtr find, out IntPtr radio)
        {
            radio = _next < radios.Length ? radios[_next++] : IntPtr.Zero;
            return radio != IntPtr.Zero;
        }

        public bool DisconnectDevice(IntPtr handle, uint code, ref long address)
        {
            LastAddress = address;
            return DisconnectAttempts++ == succeedAt;
        }

        public void CloseHandle(IntPtr handle) => ClosedHandles.Add(handle);
        public void CloseFind(IntPtr find) => FindClosed = true;
    }
}
