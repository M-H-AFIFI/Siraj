namespace Siraj.Core.Hijri;

public sealed record IslamicOccasion(string Id, int HijriMonth, int HijriDay, string TitleAr, string TitleEn, string TitleTr, string TitleUr)
{
    public string Title(string language) => language switch
    {
        _ when language.StartsWith("ar") => TitleAr,
        _ when language.StartsWith("ur") => TitleUr,
        _ when language.StartsWith("tr") => TitleTr,
        _ => TitleEn,
    };
}

public sealed record DatedOccasion(IslamicOccasion Occasion, DateOnly Date, int HijriYear, int DaysUntil)
{
    public bool IsToday => DaysUntil == 0;
}

public static class IslamicOccasions
{
    public static readonly IReadOnlyList<IslamicOccasion> All =
    [
        new("islamic_new_year", 1, 1, "رأس السنة الهجرية", "Islamic New Year", "Hicri Yılbaşı", "نیا اسلامی سال"),
        new("ashura", 1, 10, "يوم عاشوراء", "Day of Ashura", "Aşure Günü", "یومِ عاشورہ"),
        new("isra_miraj", 7, 27, "الإسراء والمعراج", "Al-Isra' wal-Mi'raj", "Miraç Kandili", "اسراء و معراج"),
        new("mid_shaban", 8, 15, "ليلة النصف من شعبان", "Mid-Sha'ban", "Berat Kandili", "شبِ برات"),
        new("ramadan_start", 9, 1, "أول أيام شهر رمضان", "First Day of Ramadan", "Ramazan-ı Şerif'in Başlangıcı", "رمضان المبارک کا آغاز"),
        new("last_ten_ramadan", 9, 21, "العشر الأواخر من رمضان", "Last Ten Days of Ramadan", "Ramazan'ın Son On Günü", "رمضان کے آخری دس دن"),
        new("eid_al_fitr", 10, 1, "عيد الفطر المبارك", "Eid al-Fitr", "Ramazan Bayramı", "عید الفطر"),
        new("day_of_arafah", 12, 9, "يوم عرفة", "Day of Arafah", "Arefe Günü", "یومِ عرفہ"),
        new("eid_al_adha", 12, 10, "عيد الأضحى المبارك", "Eid al-Adha", "Kurban Bayramı", "عید الاضحیٰ"),
    ];

    public static DatedOccasion Next(IslamicOccasion occasion, DateOnly today, HijriCalendar calendar)
    {
        int year = calendar.ToHijri(today).Year;
        var date = calendar.ToGregorian(year, occasion.HijriMonth, occasion.HijriDay);
        if (date < today)
        {
            year++;
            date = calendar.ToGregorian(year, occasion.HijriMonth, occasion.HijriDay);
        }
        return new DatedOccasion(occasion, date, year, date.DayNumber - today.DayNumber);
    }

    public static IReadOnlyList<DatedOccasion> Upcoming(DateOnly today, HijriCalendar calendar) =>
        All.Select(o => Next(o, today, calendar)).OrderBy(d => d.DaysUntil).ToList();
}
