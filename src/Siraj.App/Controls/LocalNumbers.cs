using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Siraj.App.Services;
using Siraj.Core.Text;
using Windows.Globalization.NumberFormatting;

namespace Siraj.App.Controls;

internal sealed class LocalNumbers : INumberFormatter2, INumberParser
{
    private readonly string _language;

    private LocalNumbers(string language) => _language = language;

    public static void Apply(NumberBox box)
    {
        var loc = Loc.Current;
        if (Digits.UseEastern(App.Settings.Current.NumeralSystem, loc.Language)) box.NumberFormatter = new LocalNumbers(loc.Language);
    }

    private string Write(long n) => Digits.Number(n, eastern: true, _language);

    public string FormatInt(long value) => Write(value);

    public string FormatUInt(ulong value) => Write((long)Math.Min(value, long.MaxValue));

    public string FormatDouble(double value) => Write((long)Math.Round(value));

    public long? ParseInt(string text) =>
        long.TryParse(Digits.ToWestern(text.Trim()), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long n) ? n : null;

    public ulong? ParseUInt(string text) => ParseInt(text) is long n and >= 0 ? (ulong)n : null;

    public double? ParseDouble(string text) => ParseInt(text);
}
