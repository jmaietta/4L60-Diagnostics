using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class AldlStreamParserTests
{
    [Fact]
    public void FragmentedFrameCompletesWithoutLosingBytes()
    {
        byte[] request = A276MessageFactory.CreateMode1Request(1);
        var parser = new AldlStreamParser([A276MessageFactory.DeviceAddress]);

        Assert.Empty(parser.Push(request.AsSpan(0, 2)));
        IReadOnlyList<AldlParseResult> results = parser.Push(request.AsSpan(2));

        AldlParseResult result = Assert.Single(results);
        Assert.Equal(AldlParseDisposition.ValidFrame, result.Disposition);
        Assert.Equal(request, result.RawBytes.ToArray());
        Assert.Equal(0, parser.BufferedByteCount);
    }

    [Fact]
    public void NoiseAndBackToBackFramesAreReportedSeparately()
    {
        byte[] first = A276MessageFactory.CreateMode1Request(0);
        byte[] second = A276MessageFactory.CreateMode1Request(1);
        byte[] input = [0x00, 0xAA, .. first, .. second];
        var parser = new AldlStreamParser([A276MessageFactory.DeviceAddress]);

        IReadOnlyList<AldlParseResult> results = parser.Push(input);

        Assert.Equal(3, results.Count);
        Assert.Equal(AldlParseDisposition.Noise, results[0].Disposition);
        Assert.Equal([0x00, 0xAA], results[0].RawBytes.ToArray());
        Assert.All(results.Skip(1), result => Assert.Equal(AldlParseDisposition.ValidFrame, result.Disposition));
    }

    [Fact]
    public void InvalidChecksumNeverBecomesValidFrame()
    {
        byte[] corrupt = A276MessageFactory.CreateMode1Request(1);
        corrupt[^1] ^= 0x01;
        var parser = new AldlStreamParser([A276MessageFactory.DeviceAddress]);

        AldlParseResult result = Assert.Single(parser.Push(corrupt));

        Assert.Equal(AldlParseDisposition.InvalidChecksum, result.Disposition);
        Assert.NotNull(result.Frame);
        Assert.False(result.Frame.ChecksumValid);
    }

    [Fact]
    public void InvalidLengthResynchronizesToFollowingFrame()
    {
        byte[] valid = A276MessageFactory.CreateMode1Request(1);
        byte[] input = [0xF4, 0x10, .. valid];
        var parser = new AldlStreamParser([A276MessageFactory.DeviceAddress]);

        IReadOnlyList<AldlParseResult> results = parser.Push(input);

        Assert.Contains(results, result => result.Disposition == AldlParseDisposition.InvalidLength);
        Assert.Contains(results, result => result.Disposition == AldlParseDisposition.ValidFrame);
    }

    [Fact]
    public void DeterministicFuzzNeverThrowsOrGrowsWithoutBound()
    {
        var random = new Random(276);
        var parser = new AldlStreamParser([A276MessageFactory.DeviceAddress]);

        for (int iteration = 0; iteration < 2_000; iteration++)
        {
            var bytes = new byte[random.Next(0, 65)];
            random.NextBytes(bytes);
            _ = parser.Push(bytes);
            Assert.InRange(parser.BufferedByteCount, 0, AldlProtocolConstants.MaximumFrameLength - 1);
        }
    }
}
