using System.Globalization;
using Avalonia.Media;
using LT1Diagnostics.App.Converters;

namespace LT1Diagnostics.App.Tests;

public sealed class StatusToBrushConverterTests
{
    [Theory]
    [InlineData("CONNECTED")]
    [InlineData("READY")]
    [InlineData("CLEAN")]
    public void HealthyStatusesRenderGreen(string status) =>
        Assert.Equal(Color.Parse("#86EFAC"), ToColor(status));

    [Fact]
    public void FlaggedQualityRendersAmberInsteadOfGreen() =>
        Assert.Equal(Color.Parse("#FBBF24"), ToColor("FLAGGED"));

    [Theory]
    [InlineData("NOT CONNECTED")]
    [InlineData("WAITING")]
    [InlineData("NO DATA")]
    [InlineData("REPLAY LOADED")]
    public void InformationalStatusesRenderNeutral(string status) =>
        Assert.Equal(Color.Parse("#94A3B8"), ToColor(status));

    private static Color ToColor(string status)
    {
        object converted = StatusToBrushConverter.Instance.Convert(
            status,
            typeof(IBrush),
            parameter: null,
            CultureInfo.InvariantCulture);
        return Assert.IsType<SolidColorBrush>(converted).Color;
    }
}
