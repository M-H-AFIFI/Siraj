using System.Globalization;
using Siraj.Core.Hijri;

namespace Siraj.Core.Text;

public static class HijriText
{
    private static readonly string[] MonthsAr =
        ["محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة", "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"];

    private static readonly string[] MonthsEn =
        ["Muharram", "Safar", "Rabi' al-Awwal", "Rabi' al-Thani", "Jumada al-Ula", "Jumada al-Akhirah", "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qi'dah", "Dhu al-Hijjah"];

    private static readonly string[] MonthsTr =
        ["Muharrem", "Safer", "Rebiülevvel", "Rebiülahir", "Cemaziyelevvel", "Cemaziyelahir", "Recep", "Şaban", "Ramazan", "Şevval", "Zilkade", "Zilhicce"];

    public static string MonthName(int month, string language)
    {
        var names = language is "ar" or "ur" ? MonthsAr : language.StartsWith("tr") ? MonthsTr : MonthsEn;
        return names[Math.Clamp(month, 1, 12) - 1];
    }

    public static string Year(int year, string language, bool eastern)
    {
        var y = Digits.Localize(year.ToString(CultureInfo.InvariantCulture), eastern, language);
        return language switch
        {
            "ar" => $"{y} هـ",
            "ur" => $"{y} ھ",
            "tr" => y,
            _ => $"{y} AH"
        };
    }

    public static string Format(HijriDay day, string language, bool eastern) =>
        $"{Digits.Localize(day.Day.ToString(CultureInfo.InvariantCulture), eastern, language)} {MonthName(day.Month, language)} {Year(day.Year, language, eastern)}";

    public static string Gregorian(DateOnly date, string language, bool eastern) =>
        Gregorian(date, language, eastern, language is "ar" or "ur" ? "dddd، d MMMM yyyy" : "dddd, d MMMM yyyy");

    public static string Gregorian(DateOnly date, string language, bool eastern, string pattern)
    {
        var culture = CultureInfo.GetCultureInfo(language switch { "ar" => "ar-EG", "tr" => "tr-TR", "ur" => "ur-PK", _ => "en-GB" });
        // Force Gregorian calendar regardless of culture defaults.
        var format = (DateTimeFormatInfo)culture.DateTimeFormat.Clone();
        format.Calendar = new GregorianCalendar();
        var text = date.ToDateTime(TimeOnly.MinValue).ToString(pattern, format);
        return Digits.Localize(Digits.ToWestern(text), eastern, language);
    }
}
