using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Oxira.Events.Api.Infrastructure;
using Oxira.Events.Application.Common;
using Oxira.Events.Infrastructure.Data;

namespace Oxira.Events.Tests;

/// <summary>The real API on a real SQL Server (OXIRA_TEST_DB), with fakes for email, AI and the exhibitions feed.</summary>
public class TestApp : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 13, 7, 0, 0, TimeSpan.Zero)); // Tue 10:00 Riyadh
    public FakeEmail Email { get; } = new();
    public FakeAnthropic Ai { get; } = new();
    public FakeImages Images { get; } = new();
    public FakeFeed Feed { get; } = new();
    private readonly string _db = "OxiraEventsTest_" + Guid.NewGuid().ToString("N")[..8];

    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        var server = Environment.GetEnvironmentVariable("OXIRA_TEST_DB") ?? "Server=localhost,1433;User Id=sa;Password=Oxira_dev_2026!;TrustServerCertificate=True";
        b.UseEnvironment("Development");
        b.UseSetting("ConnectionStrings:Events", $"{server};Database={_db}");
        b.UseSetting("RateLimit:FormsPerMinute", "100000");
        b.UseSetting("RateLimit:ChatPerMinute", "100000");
        b.UseSetting("RateLimit:RenderPerMinute", "100000");
        b.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(Email);
            s.RemoveAll<IAnthropic>();
            s.AddSingleton<IAnthropic>(Ai);
            s.RemoveAll<IImageEditor>();
            s.AddSingleton<IImageEditor>(Images);
            s.RemoveAll<IExhibitionFeed>();
            s.AddSingleton<IExhibitionFeed>(Feed);
            var jobs = s.FirstOrDefault(d => d.ImplementationType == typeof(JobsHost));
            if (jobs != null) s.Remove(jobs);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database.EnsureDeletedAsync();
        }
        catch { /* best effort */ }
        await base.DisposeAsync();
    }

    public HttpClient Client(string? role = null)
    {
        var c = CreateClient();
        if (role != null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
        return c;
    }

    public string Token(string role) => DevTokens.Issue(Services.GetRequiredService<IOptions<JwtOptions>>().Value, $"{role}@oxira.sa", role, TimeSpan.FromHours(1));
}

public static class Http
{
    public static async Task<(HttpStatusCode Status, JsonElement Body)> PostJson(this HttpClient c, string url, object body)
    {
        var res = await c.PostAsJsonAsync(url, body);
        return (res.StatusCode, await Read(res));
    }

    public static async Task<(HttpStatusCode Status, JsonElement Body)> GetJson(this HttpClient c, string url)
    {
        var res = await c.GetAsync(url);
        return (res.StatusCode, await Read(res));
    }

    private static async Task<JsonElement> Read(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        return text.Length > 0 && (text[0] == '{' || text[0] == '[') ? JsonDocument.Parse(text).RootElement.Clone() : default;
    }
}

public class FakeEmail : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();
    public void Enqueue(EmailMessage message) => Sent.Enqueue(message);
    public List<EmailMessage> To(string address) => Sent.Where(m => m.To.Contains(address)).ToList();
}

/// <summary>Scripted Claude: each call returns the next queued response (or a plain answer).</summary>
public class FakeAnthropic : IAnthropic
{
    public ConcurrentQueue<AiResponse> Script { get; } = new();
    public List<AiRequest> Calls { get; } = [];

    public Task<AiResponse> CreateMessageAsync(AiRequest request, CancellationToken ct = default)
    {
        lock (Calls) Calls.Add(request with { Messages = (JsonArray)request.Messages.DeepClone() });
        return Task.FromResult(Script.TryDequeue(out var r) ? r : Text("أهلاً بيك، كيف أقدر أساعدك؟"));
    }

    public static AiResponse Text(string s) => new([new JsonObject { ["type"] = "text", ["text"] = s }], "end_turn");
    public static AiResponse Tool(string name, JsonObject input) =>
        new([new JsonObject { ["type"] = "tool_use", ["id"] = "toolu_1", ["name"] = name, ["input"] = input }], "tool_use");
}

public class FakeImages : IImageEditor
{
    public bool Fail { get; set; }
    public int Calls;
    public Task<byte[]> EditAsync(byte[] image, string mime, string prompt, CancellationToken ct = default)
    {
        Interlocked.Increment(ref Calls);
        if (Fail) throw new HttpRequestException("provider down");
        return Task.FromResult(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
    }
}

public class FakeFeed : IExhibitionFeed
{
    public List<Exhibition> Shows { get; set; } = [];
    public Task<IReadOnlyList<Exhibition>> GetAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Exhibition>>(Shows);
}
