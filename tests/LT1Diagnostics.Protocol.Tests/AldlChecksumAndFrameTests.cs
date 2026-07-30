using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class AldlChecksumAndFrameTests
{
    [Fact]
    public void ModeOneMessageOneRequestMatchesA276ReferenceBytes()
    {
        byte[] actual = A276MessageFactory.CreateMode1Request(1);

        Assert.Equal([0xF4, 0x57, 0x01, 0x01, 0xB3], actual);
        Assert.True(AldlChecksum.IsValid(actual));
    }

    [Theory]
    [InlineData(0x00, 0xB6)]
    [InlineData(0x08, 0xAE)]
    [InlineData(0x09, 0xAD)]
    public void ControlRequestMatchesDocumentedEnvelope(byte mode, byte expectedChecksum)
    {
        byte[] actual = mode switch
        {
            0x00 => A276MessageFactory.CreateReturnToNormalModeRequest(),
            0x08 => A276MessageFactory.CreateDisableNormalCommunicationsRequest(),
            0x09 => A276MessageFactory.CreateEnableNormalCommunicationsRequest(),
            _ => throw new InvalidOperationException(),
        };

        Assert.Equal([0xF4, 0x56, mode, expectedChecksum], actual);
        Assert.True(AldlChecksum.IsValid(actual));
    }

    [Fact]
    public void ParserRejectsLengthMismatch()
    {
        Assert.False(AldlFrameBuilder.TryParse([0xF4, 0x57, 0x01, 0x01], out AldlFrame? frame));
        Assert.Null(frame);
    }

    [Fact]
    public void BuilderRejectsOversizedPayload()
    {
        var payload = new byte[AldlProtocolConstants.MaximumFrameLength];

        Assert.Throws<ArgumentOutOfRangeException>(() => AldlFrameBuilder.Build(0xF4, 0x01, payload));
    }
}
