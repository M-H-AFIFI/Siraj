namespace Siraj.Core.Quran;

public sealed class KhatmaState
{
    public string StartDate { get; set; } = "";
    public int TargetDays { get; set; } = 30;
    public int StartPage { get; set; } = 1;
    public List<int> CompletedDays { get; set; } = [];
    public int CompletedCount { get; set; }
}

public sealed record KhatmaDayPortion(int DayNumber, int FromPage, int ToPage, bool Completed, bool IsToday)
{
    public int PageCount => Math.Max(0, ToPage - FromPage + 1);
}

public sealed record KhatmaSummary(
    int TargetDays, int CompletedDays, int TotalPages, int PagesRead,
    int CurrentDayNumber, KhatmaDayPortion TodayPortion, DateOnly ProjectedEnd,
    int Streak, IReadOnlyList<KhatmaDayPortion> Portions)
{
    public double Progress => TargetDays > 0 ? Math.Clamp((double)CompletedDays / TargetDays, 0, 1) : 0;
    public bool IsFinished => CompletedDays >= TargetDays && TargetDays > 0;
}

public static class KhatmaPlanner
{
    public static readonly int[] Presets = [7, 15, 30, 60];

    public static KhatmaState Create(DateOnly today, int targetDays, int startPage = 1) => new()
    {
        StartDate = today.ToString("yyyy-MM-dd"),
        TargetDays = Math.Clamp(targetDays, 1, 365),
        StartPage = Math.Clamp(startPage, 1, QuranDatabase.PageCount),
        CompletedDays = [],
    };

    public static int PagesPerDay(int targetDays, int startPage = 1)
    {
        int remaining = QuranDatabase.PageCount - Math.Clamp(startPage, 1, QuranDatabase.PageCount) + 1;
        return Math.Max(1, (int)Math.Ceiling((double)remaining / Math.Clamp(targetDays, 1, 365)));
    }

    public static KhatmaSummary Summarize(KhatmaState state, DateOnly today)
    {
        int days = Math.Clamp(state.TargetDays, 1, 365);
        int startPage = Math.Clamp(state.StartPage, 1, QuranDatabase.PageCount);
        int totalPages = QuranDatabase.PageCount - startPage + 1;
        var done = new HashSet<int>(state.CompletedDays ?? []);

        if (!DateOnly.TryParse(state.StartDate, out var startDate)) startDate = today;
        int elapsed = Math.Clamp(today.DayNumber - startDate.DayNumber + 1, 1, days);

        var portions = new List<KhatmaDayPortion>(days);
        int pagesRead = 0;
        for (int d = 1; d <= days; d++)
        {
            int from = startPage + (int)Math.Floor((d - 1) * (double)totalPages / days);
            int to = d == days
                ? QuranDatabase.PageCount
                : Math.Max(from, startPage + (int)Math.Floor(d * (double)totalPages / days) - 1);
            bool isDone = done.Contains(d);
            if (isDone) pagesRead += Math.Max(0, to - from + 1);
            portions.Add(new KhatmaDayPortion(d, from, to, isDone, d == elapsed));
        }

        // First incomplete portion, or the final portion if all are complete.
        var focus = portions.FirstOrDefault(p => !p.Completed) ?? portions[^1];

        int streak = 0;
        for (int d = elapsed; d >= 1; d--)
        {
            if (done.Contains(d)) streak++;
            else if (d < elapsed) break;
        }

        int remainingDays = Math.Max(0, days - done.Count);
        var projectedEnd = today.AddDays(remainingDays);

        return new KhatmaSummary(days, done.Count, totalPages, pagesRead, elapsed, focus, projectedEnd, streak, portions);
    }
}
