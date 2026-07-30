namespace LT1Diagnostics.Hardware.Tests;

public sealed class HardwareTestPlaceholder
{
    [Fact(Skip = "Requires an explicitly configured FTDI ALDL cable and vehicle/emulator.")]
    [Trait("Category", "Hardware")]
    public void SerialCableLoopback()
    {
    }
}

