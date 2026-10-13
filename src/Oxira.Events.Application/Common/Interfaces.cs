using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Oxira.Events.Domain;

namespace Oxira.Events.Application.Common;

/// <summary>The database as the application sees it (EF Core + SQL Server in Infrastructure).</summary>
public interface IAppDbContext
{
    DbSet<Lead> Leads { get; }
    DbSet<LeadFile> LeadFiles { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<AlertSubscriber> AlertSubscribers { get; }
    DbSet<AlertSent> AlertsSent { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<RenderLog> RenderLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Atomic daily counters (SQL MERGE with HOLDLOCK), keyed by name and Riyadh day.</summary>
public interface IQuota
{
    /// <returns>the count after this use, or null when the limit was already reached</returns>
    Task<int?> TryConsumeAsync(string key, int limit, CancellationToken ct = default);
    Task RefundAsync(string key, CancellationToken ct = default);
    Task<int> UsedAsync(string key, CancellationToken ct = default);
}

/// <summary>The caller: JWT user (Oxira account, issued by oxira-web-api) and network details.</summary>
public interface ICurrentUser
{
    string? Id { get; }
    string? Email { get; }
    bool IsStaff { get; }
    string Ip { get; }
}

// ───── email ─────
public record EmailAttachment(string Name, string Mime, byte[] Content, string? ContentId = null);
public record EmailMessage(IReadOnlyList<string> To, string Subject, string Html, string? ReplyTo = null, IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>Queues an email; delivery happens in the background (SMTP, Zoho Mail).</summary>
public interface IEmailSender
{
    void Enqueue(EmailMessage message);
}

// ───── AI ─────
/// <summary>One call to Anthropic's Messages API. Content and tool inputs are raw JSON so tools can be described freely.</summary>
public record AiRequest(string System, JsonArray Messages, JsonArray? Tools = null, int MaxTokens = 900, string? Model = null);
public record AiResponse(JsonArray Content, string StopReason);

public interface IAnthropic
{
    Task<AiResponse> CreateMessageAsync(AiRequest request, CancellationToken ct = default);
}

/// <summary>OpenAI image edit: keeps the composition of the input, re-renders it photorealistically.</summary>
public interface IImageEditor
{
    Task<byte[]> EditAsync(byte[] image, string mime, string prompt, CancellationToken ct = default);
}

// ───── live updates ─────
/// <summary>Pushes to the team dashboard over SignalR (/hubs/live).</summary>
public interface IRealtime
{
    Task LeadCreatedAsync(LeadSummaryDto lead);
    Task BookingCreatedAsync(BookingDto booking);
}

/// <summary>The site's public exhibitions calendar (events.oxira.sa/events.json).</summary>
public record Exhibition(string Name, string? NameAr, string Sector, DateOnly Start, DateOnly End, string City, string Venue, string? Url)
{
    public string Key => $"{Start:yyyyMMdd}:{Name}";
}

public interface IExhibitionFeed
{
    Task<IReadOnlyList<Exhibition>> GetAsync(CancellationToken ct = default);
}
