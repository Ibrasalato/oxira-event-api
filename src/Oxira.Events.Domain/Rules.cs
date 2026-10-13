using System.Text.RegularExpressions;

namespace Oxira.Events.Domain;

/// <summary>A rule was broken. Reason is the short code the website switches on (invalid, limit, slot …).</summary>
public class DomainException(string message, string reason = "invalid", int status = 400) : Exception(message)
{
    public string Reason { get; } = reason;
    public int Status { get; } = status;

    public static DomainException NotFound(string message = "غير موجود") => new(message, "not_found", 404);
    public static DomainException Limit(string message) => new(message, "limit", 429);
}

public static class Langs
{
    public const string Default = "ar";
    public static readonly string[] All = ["ar", "en", "de", "fr", "ru"];
    public static string Of(string? lang) => lang != null && All.Contains(lang) ? lang : Default;
}

public static class LeadChannels
{
    public const string Form = "website-form";
    public const string Chat = "website-chat";
}

public static class LeadStatuses
{
    public const string New = "new";
    public static readonly string[] All = ["new", "contacted", "quoted", "won", "lost", "spam"];
}

public static class BookingChannels
{
    public const string Call = "call";
    public static readonly string[] All = ["call", "whatsapp", "video", "visit"];
}

public static class BookingStatuses
{
    public const string New = "new";
    public const string Cancelled = "cancelled";
    public static readonly string[] All = ["new", "confirmed", "cancelled", "done"];
}

public static class Sectors
{
    public static readonly string[] All =
        ["agriculture", "health", "construction", "energy", "realestate", "food", "mobility", "tech", "auto", "industry", "entertainment", "beauty", "hospitality", "interiors"];
}

/// <summary>Saudi Arabia keeps UTC+3 all year (no daylight saving).</summary>
public static class Riyadh
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);
    public static DateTimeOffset Now(TimeProvider clock) => clock.GetUtcNow().ToOffset(Offset);
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(Now(clock).DateTime);
}

/// <summary>Consultation slots: 30 minutes, Sunday–Thursday, 10:00 to 16:30 (last start), at least an hour ahead, at most 45 days ahead.</summary>
public static class BookingRules
{
    public static readonly TimeOnly First = new(10, 0);
    public static readonly TimeOnly Last = new(16, 30);
    public const int SlotMinutes = 30;
    public const int MaxDaysAhead = 45;
    public static readonly TimeSpan MinLead = TimeSpan.FromHours(1);

    /// <returns>null when the slot is bookable, otherwise the reason</returns>
    public static string? Check(DateOnly date, TimeOnly time, DateTimeOffset nowRiyadh)
    {
        if (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday) return "اختر يومًا من الأحد إلى الخميس";
        if (time < First || time > Last || time.Minute % SlotMinutes != 0 || time.Second != 0) return "اختر وقتًا بين 10:00 و16:30 كل نصف ساعة";
        var start = new DateTimeOffset(date.ToDateTime(time), Riyadh.Offset);
        if (start - nowRiyadh < MinLead) return "اختر موعدًا بعد ساعة على الأقل من الآن";
        if (start - nowRiyadh > TimeSpan.FromDays(MaxDaysAhead)) return $"المواعيد متاحة خلال {MaxDaysAhead} يومًا فقط";
        return null;
    }
}

public static partial class Text
{
    /// <summary>trim, drop control characters, cut to max</summary>
    public static string Clip(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = Controls().Replace(s.Trim(), " ");
        return t.Length <= max ? t : t[..max];
    }

    public static bool IsEmail(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 160 && EmailRe().IsMatch(s.Trim());

    /// <summary>at least 7 digits once spaces, dashes and the leading + are removed</summary>
    public static bool IsPhone(string? s) => !string.IsNullOrWhiteSpace(s) && Digits(s).Length is >= 7 and <= 15;

    public static string Digits(string s) => new(s.Where(char.IsAsciiDigit).ToArray());

    public static string SessionKey(string? sid) => Clip(SafeId().Replace(sid ?? "", ""), 64);

    public static string Html(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "").Replace("\n", "<br>");

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F]")] private static partial Regex Controls();
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")] private static partial Regex EmailRe();
    [GeneratedRegex(@"[^A-Za-z0-9_-]")] private static partial Regex SafeId();
}
