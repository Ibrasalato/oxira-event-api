using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Oxira.Events.Api.Infrastructure;
using Oxira.Events.Application.Common;
using Oxira.Events.Application.Emails;
using Oxira.Events.Application.Features.Admin;
using Oxira.Events.Application.Features.Alerts;
using Oxira.Events.Application.Features.Bookings;
using Oxira.Events.Application.Features.Chat;
using Oxira.Events.Application.Features.Leads;
using Oxira.Events.Application.Features.Renders;

namespace Oxira.Events.Api.Controllers;

/// <summary>Website AI assistant.</summary>
[ApiController, Route("api/chat")]
public class ChatController(ISender mediator) : ControllerBase
{
    /// <summary>One visitor message → the assistant's reply. Saves a lead when the visitor confirms a request.</summary>
    [HttpPost, EnableRateLimiting("chat")]
    public Task<ChatReply> Send(ChatInput body, CancellationToken ct) => mediator.Send(new SendChatCommand(body), ct);

    /// <summary>The visible history of a chat session (to restore the widget after a reload).</summary>
    [HttpGet("{sessionId}")]
    public Task<List<ChatTurnDto>> History(string sessionId, CancellationToken ct) => mediator.Send(new GetChatQuery(sessionId), ct);
}

/// <summary>Quote requests (with the 3D planner's files).</summary>
[ApiController, Route("api/leads")]
public class LeadsController(ISender mediator) : ControllerBase
{
    /// <summary>The quote form. Returns the request number.</summary>
    [HttpPost, EnableRateLimiting("forms"), RequestSizeLimit(20 * 1024 * 1024)]
    public Task<SubmitResult> Submit(QuoteInput body, CancellationToken ct) => mediator.Send(new SubmitQuoteCommand(body), ct);
}

/// <summary>Consultation bookings.</summary>
[ApiController, Route("api/bookings")]
public class BookingsController(ISender mediator) : ControllerBase
{
    [HttpPost, EnableRateLimiting("forms")]
    public Task<SubmitResult> Create(BookingInput body, CancellationToken ct) => mediator.Send(new CreateBookingCommand(body), ct);

    /// <summary>Slots already taken (Riyadh time, yyyy-MM-ddTHH:mm) so the site can grey them out.</summary>
    [HttpGet("taken")]
    public Task<SlotsResult> Taken([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => mediator.Send(new GetTakenSlotsQuery(from, to), ct);
}

/// <summary>Exhibition alert emails.</summary>
[ApiController, Route("api/alerts")]
public class AlertsController(ISender mediator) : ControllerBase
{
    [HttpPost, EnableRateLimiting("forms")]
    public Task<SuccessResult> Subscribe(AlertsInput body, CancellationToken ct) => mediator.Send(new SubscribeAlertsCommand(body), ct);

    /// <summary>The link in every alert email. Returns a small HTML page.</summary>
    [HttpGet("unsubscribe")]
    public async Task<ContentResult> Unsubscribe([FromQuery] string t, CancellationToken ct)
    {
        var ok = await mediator.Send(new UnsubscribeCommand(t ?? ""), ct);
        return new ContentResult { Content = Templates.UnsubscribedPage(ok), ContentType = "text/html; charset=utf-8", StatusCode = ok ? 200 : 404 };
    }
}

/// <summary>AI booth renders from the 3D planner (free daily quota).</summary>
[ApiController, Route("api/renders")]
public class RendersController(ISender mediator) : ControllerBase
{
    [HttpPost, EnableRateLimiting("render"), RequestSizeLimit(8 * 1024 * 1024)]
    public Task<RenderResult> Render(RenderInput body, CancellationToken ct) => mediator.Send(new RenderBoothCommand(body), ct);

    /// <summary>Renders left today for this session.</summary>
    [HttpGet("quota/{sessionId}")]
    public Task<RenderResult> Quota(string sessionId, CancellationToken ct) => mediator.Send(new RenderQuotaQuery(sessionId), ct);
}

public record LeadUpdate(string? Status, string? StaffNote);
public record StatusBody(string Status);

/// <summary>The Oxira team dashboard (Oxira account with role staff or admin).</summary>
[ApiController, Route("api/admin"), Authorize(Policy = "staff")]
public class AdminController(ISender mediator) : ControllerBase
{
    [HttpGet("overview")]
    public Task<OverviewDto> Overview(CancellationToken ct) => mediator.Send(new GetOverviewQuery(), ct);

    [HttpGet("leads")]
    public Task<PageOf<LeadSummaryDto>> Leads([FromQuery] string? status, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int size = 30, CancellationToken ct = default) =>
        mediator.Send(new ListLeadsQuery(status, q, page, size), ct);

    [HttpGet("leads/{id:int}")]
    public Task<LeadDto> Lead(int id, CancellationToken ct) => mediator.Send(new GetLeadQuery(id), ct);

    [HttpPatch("leads/{id:int}")]
    public Task<LeadDto> Update(int id, LeadUpdate body, CancellationToken ct) => mediator.Send(new UpdateLeadCommand(id, body.Status, body.StaffNote), ct);

    /// <summary>Download a planner file (JPG preview, GLB, OBJ).</summary>
    [HttpGet("leads/{id:int}/files/{fileId:int}")]
    public async Task<FileContentResult> File(int id, int fileId, CancellationToken ct)
    {
        var f = await mediator.Send(new GetLeadFileQuery(id, fileId), ct);
        return File(f.Content, f.Mime, f.Name);
    }

    [HttpGet("bookings")]
    public Task<List<BookingDto>> Bookings([FromQuery] bool upcoming = true, [FromQuery] string? status = null, CancellationToken ct = default) =>
        mediator.Send(new ListBookingsQuery(upcoming, status), ct);

    [HttpPut("bookings/{id:int}/status")]
    public Task<BookingDto> BookingStatus(int id, StatusBody body, CancellationToken ct) => mediator.Send(new UpdateBookingCommand(id, body.Status), ct);

    [HttpGet("subscribers")]
    public Task<List<SubscriberDto>> Subscribers([FromQuery] bool active = true, CancellationToken ct = default) => mediator.Send(new ListSubscribersQuery(active), ct);

    /// <summary>Run the exhibition alerts job now (it also runs every hour).</summary>
    [HttpPost("jobs/alerts")]
    public async Task<object> RunAlerts(CancellationToken ct) => new { success = true, sent = await mediator.Send(new SendDueAlertsCommand(), ct) };
}

public record DevTokenBody(string Email = "team@oxira.sa", string Role = "staff");

/// <summary>Development only (404 elsewhere): a staff token to try the dashboard in Swagger.</summary>
[ApiController, Route("api/dev")]
public class DevController(IWebHostEnvironment env, IOptions<JwtOptions> jwt) : ControllerBase
{
    [HttpPost("token")]
    public IActionResult Token(DevTokenBody body)
    {
        if (!env.IsDevelopment()) return NotFound();
        return Ok(new { token = DevTokens.Issue(jwt.Value, body.Email, body.Role, TimeSpan.FromHours(8)) });
    }
}
