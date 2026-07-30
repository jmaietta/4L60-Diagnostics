using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class A276TransmissionDecoderTests
{
    [Fact]
    public void DocumentaryVectorDecodesExactlyAndRemainsProductionIneligible()
    {
        byte[] data = CreateDataBytes();
        byte[] rawFrame = AldlFrameBuilder.Build(A276MessageFactory.DeviceAddress, 0x01, data);
        Assert.True(AldlFrameBuilder.TryParse(rawFrame, out AldlFrame? frame));

        A276TransmissionSample sample = A276TransmissionDecoder.DecodeMode1Message1(frame!);

        Assert.Equal(800, sample.EngineSpeedRpm);
        Assert.Equal(15, sample.VehicleSpeedMph);
        Assert.Equal(90, sample.CurrentTorqueSignalPressurePsi);
        Assert.Equal(13.2, sample.TransmissionIgnitionVoltage, precision: 3);
        Assert.Equal(3, sample.CommandedGear);
        Assert.Equal(-80, sample.SlipRpm);
        Assert.Equal(1000, sample.OutputSpeedRpm);
        Assert.Equal(50, sample.CoolantTemperatureCelsius);
        Assert.Equal(35, sample.TransmissionFluidTemperatureCelsius);
        Assert.Equal(50, sample.TccDutyCyclePercent, precision: 3);
        Assert.True(sample.TccControlCommanded);
        Assert.True(sample.TccEnabled);
        Assert.True(sample.ShiftSolenoidACommanded);
        Assert.True(sample.ShiftSolenoidBCommanded);
        A276LoggedDtc dtc = Assert.Single(sample.LoggedTransmissionDtcs);
        Assert.Equal(82, dtc.Code);
        Assert.Equal(VerificationStatus.Unverified, sample.VerificationStatus);
        Assert.Equal(data, sample.RawDataBytes.ToArray());
    }

    private static byte[] CreateDataBytes()
    {
        var data = new byte[46];
        data[3] = 1 << 4;
        data[5] = 128;
        data[6] = 100;
        data[7] = 0x19;
        data[8] = 0x00;
        data[9] = 30;
        data[10] = 90;
        data[11] = 51;
        data[12] = 52;
        data[13] = 128;
        data[14] = 1 << 6;
        data[15] = 132;
        data[16] = 2;
        data[19] = 4;
        data[20] = 5;
        data[21] = 0xFD;
        data[22] = 0x80;
        data[23] = 20;
        data[24] = 25;
        data[25] = 0x12;
        data[26] = 0x34;
        data[27] = 64;
        data[29] = 0x1F;
        data[30] = 0x40;
        data[31] = 120;
        data[32] = 100;
        data[33] = 0x80;
        data[34] = 0x00;
        data[37] = 0x0F;
        return data;
    }
}
