using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LT1Diagnostics.App.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    public static readonly StatusToBrushConverter Instance = new();

    private static readonly SolidColorBrush HealthyBrush = new(Color.Parse("#86EFAC"));
    private static readonly SolidColorBrush WarningBrush = new(Color.Parse("#FBBF24"));
    private static readonly SolidColorBrush NeutralBrush = new(Color.Parse("#94A3B8"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string status)
        {
            return NeutralBrush;
        }

        return status.ToUpperInvariant() switch
        {
            "CONNECTED" or "READY" or "CLEAN" => HealthyBrush,
            "FLAGGED" => WarningBrush,
            _ => NeutralBrush,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
