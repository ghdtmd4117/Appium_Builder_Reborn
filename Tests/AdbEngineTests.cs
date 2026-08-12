using AppiumBuilder.Core;
using Xunit;

namespace AppiumBuilder.Tests;

public sealed class AdbEngineTests
{
    [Fact]
    public void BuildAdbArguments_UsesSelectedSerialForDeviceCommands()
    {
        AdbEngine.SetSelectedSerial("device-123");
        Assert.Equal("-s \"device-123\" shell getprop ro.product.model", AdbEngine.BuildAdbArguments("shell getprop ro.product.model"));
        Assert.Equal("devices -l", AdbEngine.BuildAdbArguments("devices -l"));
        AdbEngine.SetSelectedSerial(null);
    }


    [Fact]
    public void ParseDevicesOutput_AcceptsSpaceSeparatedWindowsOutput()
    {
        const string output = "List of devices attached\r\nR3CTEST123          device product:e3qks model:SM_S908N device:e3q transport_id:1\r\n";
        var devices = AdbEngine.ParseDevicesOutput(output);
        Assert.Single(devices);
        Assert.Equal("R3CTEST123", devices[0].Serial);
        Assert.Equal("device", devices[0].State);
    }

    [Fact]
    public void ParseDevicesOutput_AcceptsTabSeparatedOutputAndUnauthorizedState()
    {
        const string output = "List of devices attached\nABC123\tunauthorized usb:1-2 transport_id:2\n";
        var devices = AdbEngine.ParseDevicesOutput(output);
        Assert.Single(devices);
        Assert.Equal("ABC123", devices[0].Serial);
        Assert.Equal("unauthorized", devices[0].State);
    }
}
