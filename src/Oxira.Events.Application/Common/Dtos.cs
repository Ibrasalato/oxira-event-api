using System.Text.Json;
using System.Text.Json.Serialization;
using Oxira.Events.Domain;

namespace Oxira.Events.Application.Common;

// ───── inputs (field names match the website) ─────

/// <summary>Quote form. design/files are present when the visitor applied a stand from the 3D planner.</summary>
public record QuoteInput(
    string? Name, string? Company, string? Phone, string? Email, string? Service,
    [property: JsonPropertyName("event_name")] string? EventName,
    [property: JsonPropertyName("event_date")] string? EventDate,
    [property: JsonPropertyName("stand_size")] string? StandSize,
    string? Details,
    [property: JsonPropertyName("company_website")] string? CompanyWebsite,
    string? Lang, string? Page, JsonElement? Source, JsonElement? Design, List<FileInput>? Files);

/// <summary>base64 without the data: prefix</summary>
public record FileInput(string? Role, string? Name, string? Mime, string? B64);

public record BookingInput(
    string? Date, string? Time, string? Channel, string? Name, string? Phone, string? Email, string? Company, string? Topic,
    [property: JsonPropertyName("company_website")] string? CompanyWebsite,
    string? Lang, string? Page, JsonElement? Source);

public record AlertsInput(string? Email, List<string>? Sectors, string? Lang, JsonElement? Source,
    [property: JsonPropertyName("company_website")] string? CompanyWebsite = null);

public record ChatInput(string? SessionId, string? Message, string? Lang, string? Page, JsonElement? Source);

/// <summary>image is a data URL (JPEG/PNG/WebP) of the planner view</summary>
public record RenderInput(string? SessionId, string? Lang, string? Image, JsonElement? Config);

// ───── results (shapes the website already reads) ─────
public record SubmitResult(bool Success, int Id);
public record SuccessResult(bool Success = true);
public record ChatReply(bool Success, string Reply);
public record RenderResult(bool Success, string? Image, int Left, string? Reason = null);
public record SlotsResult(string From, string To, List<string> Taken);

// ───── staff dashboard ─────
public record PageOf<T>(List<T> Items, int Total, int Page, int Size);

public record LeadSummaryDto(int Id, string ContactName, string Phone, string Email, string Company, string Service, string EventName,
    string StandSize, string Channel, string Status, string Lang, string? Source, string? Medium, bool HasDesign, int Files, DateTimeOffset CreatedAt);

public record LeadFileDto(int Id, string Name, string Mime, string Role, long Size);

public record LeadDto(int Id, string ContactName, string Phone, string Email, string Company, string Service, string EventName, string EventDate,
    string City, string StandSize, string Needs, string PreferredContact, string Channel, string Status, string Lang, string Page,
    JsonElement? Design, LeadSource Source, string? StaffNote, List<LeadFileDto> Files, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record BookingDto(int Id, string ContactName, string Phone, string Email, string Company, string Topic, string Channel,
    DateTimeOffset StartsAt, string Status, string Lang, string? Source, DateTimeOffset CreatedAt);

public record SubscriberDto(Guid Id, string Email, List<string> Sectors, string Lang, bool Active, DateTimeOffset CreatedAt);

public record OverviewDto(int NewLeads, int LeadsThisMonth, int UpcomingBookings, int Subscribers, int RendersToday, List<CountDto> LeadsBySource);
public record CountDto(string Key, int Count);

public record FileContent(string Name, string Mime, byte[] Content);

public static class Map
{
    public static LeadSummaryDto Summary(Lead l) => new(l.Id, l.ContactName, l.Phone, l.Email, l.Company, l.Service, l.EventName, l.StandSize,
        l.Channel, l.Status, l.Lang, l.Source.Source, l.Source.Medium, l.DesignJson != null, l.Files.Count, l.CreatedAt);

    public static LeadDto Lead(Lead l) => new(l.Id, l.ContactName, l.Phone, l.Email, l.Company, l.Service, l.EventName, l.EventDate, l.City,
        l.StandSize, l.Needs, l.PreferredContact, l.Channel, l.Status, l.Lang, l.Page,
        l.DesignJson is { } d ? JsonDocument.Parse(d).RootElement.Clone() : null, l.Source, l.StaffNote,
        l.Files.Select(f => new LeadFileDto(f.Id, f.Name, f.Mime, f.Role, f.Size)).ToList(), l.CreatedAt, l.UpdatedAt);

    public static BookingDto Booking(Booking b) => new(b.Id, b.ContactName, b.Phone, b.Email, b.Company, b.Topic, b.Channel, b.StartsAt,
        b.Status, b.Lang, b.Source.Source, b.CreatedAt);

    public static SubscriberDto Subscriber(AlertSubscriber s) => new(s.Id, s.Email, s.Sectors.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
        s.Lang, s.UnsubscribedAt == null, s.CreatedAt);

    /// <summary>the site's first-touch source object → stored columns + raw JSON</summary>
    public static LeadSource Source(JsonElement? e)
    {
        if (e is not { ValueKind: JsonValueKind.Object } o) return new LeadSource();
        string? S(string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? Text.Clip(v.GetString(), 200) : null;
        var raw = o.GetRawText();
        return new LeadSource
        {
            Source = S("utm_source") ?? S("source"),
            Medium = S("utm_medium") ?? S("medium"),
            Campaign = S("utm_campaign"),
            Referrer = S("referrer"),
            Landing = S("landing"),
            Raw = raw.Length <= 4000 ? raw : null,
        };
    }
}
