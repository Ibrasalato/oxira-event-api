using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Oxira.Events.Application.Common;
using Oxira.Events.Application.Features.Alerts;
using Oxira.Events.Domain;
using MediatR;

namespace Oxira.Events.Api.Infrastructure;

/// <summary>
/// "Jwt" section. Oxira accounts are issued by oxira-web-api; every Oxira API validates the same issuer, audience and key,
/// so one sign-in works on oxira.sa, design and events. Jwt__Key is set as an environment variable on the server.
/// </summary>
public class JwtOptions
{
    public string Issuer { get; set; } = "https://api.oxira.sa";
    public string Audience { get; set; } = "oxira";
    public string Key { get; set; } = "";
    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key.Length >= 32 ? Key : throw new InvalidOperationException("Jwt:Key must be at least 32 characters")));
}

public static class OxiraClaims
{
    public const string Role = "role";
    public const string Email = "email";
    public const string Name = "name";
    /// <summary>Oxira team members (staff, admin) can open the events dashboard</summary>
    public static readonly string[] StaffRoles = ["staff", "admin"];
}

public class HttpCurrentUser(IHttpContextAccessor http, IConfiguration cfg) : ICurrentUser
{
    private ClaimsPrincipal? User => http.HttpContext?.User;
    public string? Id => User?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Email => User?.FindFirstValue(OxiraClaims.Email) ?? User?.FindFirstValue(ClaimTypes.Email);
    public bool IsStaff => User?.Claims.Any(c => c.Type is OxiraClaims.Role or ClaimTypes.Role && OxiraClaims.StaffRoles.Contains(c.Value)) == true;

    /// <summary>
    /// Behind Cloudflare the visitor's address is in CF-Connecting-IP. It is believed only when Proxy:TrustCloudflare is on
    /// AND the connection itself comes from a Cloudflare edge range, so a caller reaching IIS directly cannot fake it.
    /// </summary>
    public string Ip
    {
        get
        {
            var ctx = http.HttpContext;
            if (ctx == null) return "unknown";
            var remote = ctx.Connection.RemoteIpAddress;
            if (remote?.IsIPv4MappedToIPv6 == true) remote = remote.MapToIPv4();
            if (cfg.GetValue("Proxy:TrustCloudflare", false) && remote != null && CloudflareRanges.Contains(cfg, remote)
                && ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cf) && IPAddress.TryParse(cf.ToString(), out var visitor))
                return (visitor.IsIPv4MappedToIPv6 ? visitor.MapToIPv4() : visitor).ToString();
            return remote?.ToString() ?? "unknown";
        }
    }
}

/// <summary>Cloudflare's published edge ranges (https://www.cloudflare.com/ips/); Proxy:CloudflareRanges replaces the list.</summary>
public static class CloudflareRanges
{
    public static readonly string[] Default =
    [
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18", "108.162.192.0/18", "190.93.240.0/20",
        "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13", "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
        "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32",
    ];

    private static IReadOnlyList<IPNetwork>? _cache;
    private static string[]? _source;

    public static bool Contains(IConfiguration cfg, IPAddress ip)
    {
        var list = cfg.GetSection("Proxy:CloudflareRanges").Get<string[]>() is { Length: > 0 } custom ? custom : Default;
        if (!ReferenceEquals(list, _source) && (_source == null || !list.SequenceEqual(_source)))
        {
            _cache = list.Select(IPNetwork.Parse).ToList();
            _source = list;
        }
        return _cache!.Any(n => n.Contains(ip));
    }
}

/// <summary>Business errors become {"success":false,"reason":"…","message":"…"} with their status; anything else is logged and hidden.</summary>
public class ErrorHandler(ILogger<ErrorHandler> log, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception e, CancellationToken ct)
    {
        var (status, reason, message) = e switch
        {
            DomainException d => (d.Status, d.Reason, d.Message),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (409, "taken", "البيانات مسجلة مسبقًا"),
            BadHttpRequestException b => (b.StatusCode, "invalid", "طلب غير صالح"),
            OperationCanceledException => (499, "cancelled", "أُلغي الطلب"),
            _ => (500, "server", "حدث خطأ غير متوقع، حاول مرة أخرى"),
        };
        if (status >= 500) log.LogError(e, "Unhandled error on {Path}", ctx.Request.Path);
        ctx.Response.StatusCode = status;
        // development: the exception text helps while trying endpoints in Swagger (never in production)
        if (status >= 500 && env.IsDevelopment()) await ctx.Response.WriteAsJsonAsync(new { success = false, reason, message, error = e.ToString()[..Math.Min(e.ToString().Length, 3000)] }, ct);
        else await ctx.Response.WriteAsJsonAsync(new { success = false, reason, message }, ct);
        return true;
    }
}

/// <summary>
/// The site posts some JSON as text/plain on purpose (no CORS preflight). Treat a text/plain body that looks like JSON as JSON.
/// </summary>
public class TextPlainJsonMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        var type = ctx.Request.ContentType;
        if (HttpMethods.IsPost(ctx.Request.Method) && type != null && type.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
            ctx.Request.ContentType = "application/json; charset=utf-8";
        return next(ctx);
    }
}

/// <summary>OpenAPI document: title and the JWT bearer scheme (Swagger UI → Authorize).</summary>
internal sealed class OpenApiInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Oxira Events API",
            Version = "v1",
            Description = "events.oxira.sa: المحادثة الذكية، طلبات عروض الأسعار مع ملفات الجناح ثلاثي الأبعاد، حجز الاستشارات، تنبيهات المعارض، وتصاميم الذكاء الاصطناعي. لوحة الفريق تحتاج رمز حساب أوكسيرا (staff/admin).",
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
            Description = "Oxira account access token (from oxira-web-api /api/auth).",
        };
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
        return Task.CompletedTask;
    }
}

// ───── SignalR ─────

/// <summary>/hubs/live: the team dashboard. Events: "lead" (LeadSummaryDto), "booking" (BookingDto).</summary>
[Microsoft.AspNetCore.Authorization.Authorize(Policy = "staff")]
public class LiveHub : Hub
{
    public const string Staff = "staff";
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Staff);
        await base.OnConnectedAsync();
    }
}

public class SignalRRealtime(IHubContext<LiveHub> hub, ILogger<SignalRRealtime> log) : IRealtime
{
    public Task LeadCreatedAsync(LeadSummaryDto lead) => Send("lead", lead);
    public Task BookingCreatedAsync(BookingDto booking) => Send("booking", booking);

    private async Task Send(string evt, object payload)
    {
        try { await hub.Clients.Group(LiveHub.Staff).SendAsync(evt, payload); }
        catch (Exception e) { log.LogWarning(e, "Realtime {Event} failed", evt); }
    }
}

// ───── scheduled jobs ─────

/// <summary>Hourly: exhibition alerts (each stage is recorded, so running often never sends twice).</summary>
public class JobsHost(IServiceScopeFactory scopes, ILogger<JobsHost> log, IConfiguration cfg) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var every = TimeSpan.FromMinutes(Math.Max(5, cfg.GetValue("Jobs:Minutes", 60)));
        using var timer = new PeriodicTimer(every);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SendDueAlertsCommand(), stop);
            }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "Alerts job failed"); }
        } while (await timer.WaitForNextTickAsync(stop));
    }
}

/// <summary>Development only: a staff token for trying the dashboard endpoints in Swagger.</summary>
public static class DevTokens
{
    public static string Issue(JwtOptions o, string email, string role, TimeSpan life)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()), new Claim(OxiraClaims.Email, email),
            new Claim(OxiraClaims.Name, email), new Claim(OxiraClaims.Role, role),
        };
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, DateTime.UtcNow, DateTime.UtcNow.Add(life),
            new SigningCredentials(o.SigningKey(), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
