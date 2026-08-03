using HidSharp;

namespace ControllerBattery.Tests.Fakes;

internal sealed class FakeHidDevice : HidDevice
{
    private readonly Func<FakeHidStream> _open;

    internal FakeHidDevice(int vendorId, int productId, string path, int inputLength,
        int outputLength, Func<FakeHidStream> open, string? productName = null,
        string? serialNumber = null, byte[]? descriptor = null)
    {
        Vendor = vendorId;
        Product = productId;
        Path = path;
        InputLength = inputLength;
        OutputLength = outputLength;
        _open = open;
        ProductNameValue = productName;
        SerialNumberValue = serialNumber;
        Descriptor = descriptor ?? [0x05, 0x01, 0x09, 0x05];
    }

    internal int Vendor { get; }
    internal int Product { get; }
    internal string Path { get; }
    internal int InputLength { get; }
    internal int OutputLength { get; }
    internal string? ProductNameValue { get; }
    internal string? SerialNumberValue { get; }
    internal byte[] Descriptor { get; }
    internal Exception? ProductException { get; init; }
    internal Exception? SerialException { get; init; }
    internal Exception? DescriptorException { get; init; }
    public override int VendorID => Vendor;
    public override int ProductID => Product;
    public override int ReleaseNumberBcd => 0x0100;
    public override string DevicePath => Path;
    public override int GetMaxInputReportLength() => InputLength;
    public override int GetMaxOutputReportLength() => OutputLength;
    public override int GetMaxFeatureReportLength() => 64;
    public override string GetFileSystemName() => Path;
    [Obsolete("Overrides HidSharp's legacy test seam.")]
    public override string ProductName => ProductException is null
        ? ProductNameValue!
        : throw ProductException;
    [Obsolete("Overrides HidSharp's legacy test seam.")]
    public override string SerialNumber => SerialException is null
        ? SerialNumberValue!
        : throw SerialException;
    public override byte[] GetRawReportDescriptor() => DescriptorException is null
        ? Descriptor
        : throw DescriptorException;
    protected override DeviceStream OpenDeviceDirectly(OpenConfiguration openConfig) => _open();
}

internal sealed class FakeHidStream : HidStream
{
    private readonly Queue<byte[]> _reports;

    internal FakeHidStream(HidDevice device, IEnumerable<byte[]>? reports = null) : base(device) =>
        _reports = new Queue<byte[]>(reports ?? []);

    internal List<byte[]> Writes { get; } = [];
    internal Exception? ReadException { get; init; }
    internal Exception? WriteException { get; init; }
    public override int ReadTimeout { get; set; }
    public override int WriteTimeout { get; set; }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (ReadException is not null) throw ReadException;
        if (_reports.Count == 0) throw new TimeoutException();
        var report = _reports.Dequeue();
        Array.Copy(report, 0, buffer, offset, Math.Min(count, report.Length));
        return Math.Min(count, report.Length);
    }
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (WriteException is not null) throw WriteException;
        Writes.Add(buffer.AsSpan(offset, count).ToArray());
    }
    public override void GetFeature(byte[] buffer, int offset, int count) { }
    public override void SetFeature(byte[] buffer, int offset, int count) { }
}
