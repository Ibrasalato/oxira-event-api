namespace Oxira.Events.Domain;

/// <summary>A quote request from the website form or the AI chat. The id is the public request number.</summary>
public class Lead
{
    public int Id { get; set; }
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Company { get; set; } = "";
    public string Service { get; set; } = "";
    public string EventName { get; set; } = "";
    public string EventDate { get; set; } = "";
    public string City { get; set; } = "";
    public string StandSize { get; set; } = "";
    public string Needs { get; set; } = "";
    public string PreferredContact { get; set; } = "";
    /// <summary>website-form | website-chat</summary>
    public string Channel { get; set; } = LeadChannels.Form;
    public string Lang { get; set; } = Langs.Default;
    public string Page { get; set; } = "";
    public string Status { get; set; } = LeadStatuses.New;
    /// <summary>stand planner choices (JSON), when the visitor applied a 3D design</summary>
    public string? DesignJson { get; set; }
    public LeadSource Source { get; set; } = new();
    public string? StaffNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<LeadFile> Files { get; set; } = [];
}

/// <summary>First-touch marketing source sent by the site (UTM, ad click ids, referrer).</summary>
public class LeadSource
{
    public string? Source { get; set; }
    public string? Medium { get; set; }
    public string? Campaign { get; set; }
    public string? Referrer { get; set; }
    public string? Landing { get; set; }
    /// <summary>everything the site sent, as JSON</summary>
    public string? Raw { get; set; }
}

/// <summary>Planner exports attached to a quote: preview JPG, GLB (colours + logo), OBJ for 3ds Max.</summary>
public class LeadFile
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public string Name { get; set; } = "";
    public string Mime { get; set; } = "application/octet-stream";
    /// <summary>preview | model | other</summary>
    public string Role { get; set; } = "other";
    public long Size { get; set; }
    public byte[] Content { get; set; } = [];
}

/// <summary>A 30-minute consultation request (Sun–Thu, 10:00–16:30 Riyadh).</summary>
public class Booking
{
    public int Id { get; set; }
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Company { get; set; } = "";
    public string Topic { get; set; } = "";
    /// <summary>call | whatsapp | video | visit</summary>
    public string Channel { get; set; } = BookingChannels.Call;
    public DateTimeOffset StartsAt { get; set; }
    public string Lang { get; set; } = Langs.Default;
    public string Page { get; set; } = "";
    public string Status { get; set; } = BookingStatuses.New;
    public LeadSource Source { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Someone who asked to be emailed before exhibitions in their sectors.</summary>
public class AlertSubscriber
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    /// <summary>comma-separated sector ids</summary>
    public string Sectors { get; set; } = "";
    public string Lang { get; set; } = Langs.Default;
    /// <summary>unsubscribe token (in every alert email)</summary>
    public string Token { get; set; } = "";
    public LeadSource Source { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UnsubscribedAt { get; set; }
}

/// <summary>One alert stage already sent for one exhibition, so a subscriber never gets it twice.</summary>
public class AlertSent
{
    public long Id { get; set; }
    public Guid SubscriberId { get; set; }
    public string EventKey { get; set; } = "";
    /// <summary>60 (about two months before) or 21 (three weeks before)</summary>
    public int Stage { get; set; }
    public DateTimeOffset SentAt { get; set; }
}

/// <summary>Chat memory: the visible turns of one website chat session.</summary>
public class ChatMessage
{
    public long Id { get; set; }
    public string SessionId { get; set; } = "";
    /// <summary>user | assistant</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public string Lang { get; set; } = Langs.Default;
    public string Page { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Daily usage counters (render quotas, chat caps). Key + Riyadh day.</summary>
public class UsageCounter
{
    public string Key { get; set; } = "";
    public DateOnly Day { get; set; }
    public int Count { get; set; }
}

/// <summary>One AI booth render attempt.</summary>
public class RenderLog
{
    public long Id { get; set; }
    public string SessionId { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Lang { get; set; } = Langs.Default;
    public string ConfigJson { get; set; } = "";
    public bool Ok { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
