using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Oxira.Events.Application.Common;

namespace Oxira.Events.Tests;

public class LeadTests(TestApp app) : IClassFixture<TestApp>
{
    private static object Quote(object? extra = null) => new
    {
        name = "سارة القحطاني", company = "شركة الأفق", phone = "+966500000001", email = "sara@example.com", service = "جناح معرض",
        event_name = "ليب 2027", event_date = "فبراير 2027", stand_size = "36", details = "جناح زاوية مع شاشة LED وغرفة اجتماعات",
        lang = "ar", page = "/", source = new { source = "google", medium = "cpc", utm_campaign = "stands", referrer = "google.com", landing = "/", first_seen = "2026-10-01" },
    };

    [Fact]
    public async Task Quote_with_planner_files_is_saved_emailed_and_pushed_live()
    {
        await using var hub = new HubConnectionBuilder().WithUrl(new Uri(app.Server.BaseAddress, "hubs/live"), o =>
        {
            o.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            o.Transports = HttpTransportType.LongPolling;
            o.AccessTokenProvider = () => Task.FromResult<string?>(app.Token("staff"));
        }).Build();
        var live = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<JsonElement>("lead", e => live.TrySetResult(e));
        await hub.StartAsync();

        var jpg = Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xD9]);
        var glb = Convert.ToBase64String(Encoding.ASCII.GetBytes("glTF...."));
        var (status, body) = await app.Client().PostJson("/api/leads", new
        {
            name = "Lena Fischer", phone = "+4915112345678", email = "lena@example.de", service = "Exhibition stand", details = "Island stand", stand_size = "100+",
            lang = "de", page = "/de/", design = new { size = 100, width_m = 10, depth_m = 10, type = "island", build = "custom", extras = new[] { "led", "meeting" }, wall = "white", floor = "grey", accent = "#F5A800", logo = true },
            files = new object[]
            {
                new { role = "preview", name = "oxira-stand-20261013.jpg", mime = "image/jpeg", b64 = jpg },
                new { name = "oxira-stand-20261013.glb", mime = "model/gltf-binary", b64 = glb },
                new { name = "../../evil.exe", mime = "application/x-msdownload", b64 = glb },
            },
        });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body.GetProperty("success").GetBoolean());
        var id = body.GetProperty("id").GetInt32();
        Assert.True(id > 0);

        var pushed = await live.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(id, pushed.GetProperty("id").GetInt32());
        Assert.True(pushed.GetProperty("hasDesign").GetBoolean());

        var team = app.Email.Sent.Single(m => m.To.Contains("info@oxira.sa") && m.Subject.Contains($"رقم {id}"));
        Assert.Equal("lena@example.de", team.ReplyTo);
        Assert.Equal(2, team.Attachments!.Count); // the .exe was dropped
        Assert.Equal("preview", team.Attachments.Single(a => a.Name.EndsWith(".jpg")).ContentId);
        var client = app.Email.To("lena@example.de").Single();
        Assert.Contains($"Nr. {id}", client.Subject);
        Assert.Contains("cid:preview", client.Html);

        var staff = app.Client("staff");
        var (_, lead) = await staff.GetJson($"/api/admin/leads/{id}");
        Assert.Equal("website-form", lead.GetProperty("channel").GetString());
        Assert.Equal("island", lead.GetProperty("design").GetProperty("type").GetString());
        var files = lead.GetProperty("files").EnumerateArray().ToList();
        Assert.Equal(2, files.Count);
        var file = await staff.GetAsync($"/api/admin/leads/{id}/files/{files[0].GetProperty("id").GetInt32()}");
        Assert.Equal("image/jpeg", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal(4, (await file.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Text_plain_json_is_accepted_and_source_is_stored()
    {
        var res = await app.Client().PostAsync("/api/leads", new StringContent(JsonSerializer.Serialize(Quote()), Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var id = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
        var (_, lead) = await app.Client("admin").GetJson($"/api/admin/leads/{id}");
        Assert.Equal("google", lead.GetProperty("source").GetProperty("source").GetString());
        Assert.Equal("stands", lead.GetProperty("source").GetProperty("campaign").GetString());
        Assert.Contains(app.Email.To("sara@example.com"), m => m.Subject.Contains($"رقم {id}"));
    }

    [Fact]
    public async Task Missing_fields_and_honeypot()
    {
        var (status, body) = await app.Client().PostJson("/api/leads", new { name = "x", details = "" });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("missing", body.GetProperty("reason").GetString());

        var before = app.Email.Sent.Count;
        (status, body) = await app.Client().PostJson("/api/leads", new { name = "bot", details = "spam", phone = "123456789", company_website = "http://spam" });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, body.GetProperty("id").GetInt32());
        Assert.Equal(before, app.Email.Sent.Count);
    }

    [Fact]
    public async Task Dashboard_needs_an_oxira_staff_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client().GetJson("/api/admin/leads")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client("customer").GetJson("/api/admin/leads")).Status);
        var (status, page) = await app.Client("staff").GetJson("/api/admin/leads?size=5");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(page.GetProperty("total").GetInt32() >= 0);
        var (s2, overview) = await app.Client("staff").GetJson("/api/admin/overview");
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.True(overview.TryGetProperty("newLeads", out _));
    }
}

public class BookingTests(TestApp app) : IClassFixture<TestApp>
{
    // the fixture clock is Tuesday 2026-10-13 10:00 Riyadh
    [Fact]
    public async Task Booking_sends_invites_and_blocks_the_slot()
    {
        var c = app.Client();
        var (status, body) = await c.PostJson("/api/bookings", new { date = "2026-10-18", time = "11:30", channel = "video", name = "Omar", phone = "+966511111111", email = "omar@example.com", topic = "Stand for LEAP", lang = "en" });
        Assert.Equal(HttpStatusCode.OK, status);
        var id = body.GetProperty("id").GetInt32();

        var client = app.Email.To("omar@example.com").Single();
        Assert.Contains($"#{id}", client.Subject);
        var ics = Encoding.UTF8.GetString(client.Attachments!.Single().Content);
        Assert.Contains("DTSTART:20261018T083000Z", ics); // 11:30 Riyadh
        Assert.Contains($"UID:booking-{id}@events.oxira.sa", ics);
        Assert.Contains(app.Email.To("info@oxira.sa"), m => m.Subject.Contains($"رقم {id}"));

        (status, body) = await c.PostJson("/api/bookings", new { date = "2026-10-18", time = "11:30", name = "Ali", phone = "+966522222222" });
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("taken", body.GetProperty("reason").GetString());

        var (_, taken) = await c.GetJson("/api/bookings/taken?from=2026-10-18&to=2026-10-18");
        Assert.Contains("2026-10-18T11:30", taken.GetProperty("taken").EnumerateArray().Select(x => x.GetString()));
    }

    [Theory]
    [InlineData("2026-10-16", "11:00")] // Friday
    [InlineData("2026-10-18", "17:00")] // after 16:30
    [InlineData("2026-10-18", "10:15")] // not on the half hour
    [InlineData("2026-10-13", "10:30")] // less than an hour ahead
    [InlineData("2027-01-10", "10:00")] // more than 45 days ahead
    public async Task Invalid_slots_are_rejected(string date, string time)
    {
        var (status, body) = await app.Client().PostJson("/api/bookings", new { date, time, name = "Ali", phone = "+966522222222" });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("slot", body.GetProperty("reason").GetString());
    }
}

public class AlertTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Subscribers_get_each_stage_once_and_can_unsubscribe()
    {
        var c = app.Client();
        var (status, _) = await c.PostJson("/api/alerts", new { email = "Buyer@Example.com", sectors = new[] { "tech", "unknown" }, lang = "en" });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Single(app.Email.To("buyer@example.com"));

        // today is 2026-10-13: one tech show 20 days ahead (second stage), one 50 days ahead (first stage), one in another sector
        app.Feed.Shows =
        [
            new Exhibition("Tech Expo", "معرض التقنية", "tech", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4), "Riyadh", "RFECC", null),
            new Exhibition("Future Tech", null, "tech", new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 4), "Riyadh", "RICEC", null),
            new Exhibition("Food Show", null, "food", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4), "Jeddah", "JIECC", null),
        ];
        var staff = app.Client("staff");
        var (_, run) = await staff.PostJson("/api/admin/jobs/alerts", new { });
        Assert.Equal(1, run.GetProperty("sent").GetInt32());
        var alert = app.Email.To("buyer@example.com").Last();
        Assert.Contains("Tech Expo", alert.Html);
        Assert.Contains("Future Tech", alert.Html);
        Assert.DoesNotContain("Food Show", alert.Html);

        (_, run) = await staff.PostJson("/api/admin/jobs/alerts", new { });
        Assert.Equal(0, run.GetProperty("sent").GetInt32());

        var token = System.Text.RegularExpressions.Regex.Match(alert.Html, "unsubscribe\\?t=([a-f0-9]{32})").Groups[1].Value;
        var page = await c.GetAsync($"/api/alerts/unsubscribe?t={token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("text/html", page.Content.Headers.ContentType!.ToString());
        var (_, subs) = await staff.GetJson("/api/admin/subscribers");
        Assert.DoesNotContain(subs.EnumerateArray(), s => s.GetProperty("email").GetString() == "buyer@example.com");
    }

    [Fact]
    public async Task Invalid_subscription()
    {
        var (status, body) = await app.Client().PostJson("/api/alerts", new { email = "nope", sectors = new[] { "tech" } });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid", body.GetProperty("reason").GetString());
    }
}

public class ChatTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Agent_saves_a_lead_with_its_tool_and_keeps_memory()
    {
        var c = app.Client();
        var (_, first) = await c.PostJson("/api/chat", new { sessionId = "sess_abc123", message = "السلام عليكم", lang = "ar", page = "/" });
        Assert.True(first.GetProperty("success").GetBoolean());

        app.Ai.Script.Enqueue(FakeAnthropic.Tool("create_lead", new JsonObject
        {
            ["contact_name"] = "خالد", ["phone"] = "966500000009", ["service"] = "جناح معرض", ["event_name"] = "سيتي سكيب", ["needs"] = "جناح 54 م²",
        }));
        app.Ai.Script.Enqueue(FakeAnthropic.Text("تم تسجيل طلبك، وسيتواصل معك الفريق."));
        var (status, reply) = await c.PostJson("/api/chat", new { sessionId = "sess_abc123", message = "أكد الطلب", lang = "ar", page = "/" });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("تم تسجيل", reply.GetProperty("reply").GetString());

        // the tool result carried the new id back to the model, and the system prompt has the Riyadh time
        var last = app.Ai.Calls.Last();
        var toolResult = last.Messages.Last()!["content"]![0]!;
        var id = JsonDocument.Parse((string)toolResult["content"]!).RootElement.GetProperty("id").GetInt32();
        Assert.Contains("2026-10-13 10:", last.System);
        Assert.Equal("user", (string?)last.Messages[0]!["role"]);
        Assert.Equal("السلام عليكم", (string?)last.Messages[0]!["content"]);

        var (_, lead) = await app.Client("staff").GetJson($"/api/admin/leads/{id}");
        Assert.Equal("website-chat", lead.GetProperty("channel").GetString());
        Assert.Equal("خالد", lead.GetProperty("contactName").GetString());
        Assert.Contains(app.Email.To("info@oxira.sa"), m => m.Subject.Contains($"رقم {id}"));

        var (_, history) = await c.GetJson("/api/chat/sess_abc123");
        Assert.Equal(4, history.GetArrayLength());
    }

    [Fact]
    public async Task Missing_message_is_rejected()
    {
        var (status, body) = await app.Client().PostJson("/api/chat", new { sessionId = "x", message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.False(body.GetProperty("success").GetBoolean());
    }
}

public class RenderTests(TestApp app) : IClassFixture<TestApp>
{
    private const string Image = "data:image/jpeg;base64,/9j/2Q==";

    [Fact]
    public async Task Free_quota_per_session_and_refund_on_failure()
    {
        var c = app.Client();
        var config = new { size = 36, type = "corner", build = "custom", extras = new[] { "led" }, wall = "navy", carpet = "blue", accent = "#00AEEF" };

        app.Images.Fail = true;
        var (_, failed) = await c.PostJson("/api/renders", new { sessionId = "render_s1", lang = "ar", image = Image, config });
        Assert.Equal("failed", failed.GetProperty("reason").GetString());
        Assert.Equal(3, failed.GetProperty("left").GetInt32()); // refunded
        app.Images.Fail = false;

        for (var i = 2; i >= 0; i--)
        {
            var (_, ok) = await c.PostJson("/api/renders", new { sessionId = "render_s1", lang = "ar", image = Image, config });
            Assert.True(ok.GetProperty("success").GetBoolean());
            Assert.StartsWith("data:image/jpeg;base64,", ok.GetProperty("image").GetString());
            Assert.Equal(i, ok.GetProperty("left").GetInt32());
        }
        var (_, limit) = await c.PostJson("/api/renders", new { sessionId = "render_s1", lang = "ar", image = Image, config });
        Assert.False(limit.GetProperty("success").GetBoolean());
        Assert.Equal("limit", limit.GetProperty("reason").GetString());
        Assert.Equal(0, limit.GetProperty("left").GetInt32());

        var (_, quota) = await c.GetJson("/api/renders/quota/render_s1");
        Assert.Equal(0, quota.GetProperty("left").GetInt32());
    }

    [Fact]
    public async Task Bad_images_are_rejected()
    {
        var (status, body) = await app.Client().PostJson("/api/renders", new { sessionId = "render_s2", image = "http://example.com/x.jpg" });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid", body.GetProperty("reason").GetString());
    }

    [Fact]
    public void Prompt_keeps_geometry_and_uses_the_choices()
    {
        var p = Application.Features.Renders.RenderHandlers.Prompt(JsonDocument.Parse("""{"type":"island","size":100,"wall":"wood","carpet":"red","extras":["led","counter"],"accent":"#123456"}""").RootElement);
        Assert.Contains("same layout", p);
        Assert.Contains("island", p);
        Assert.Contains("red carpet", p);
        Assert.Contains("LED video wall", p);
        Assert.Contains("#123456", p);
    }
}

public class SwaggerTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task OpenApi_and_ui()
    {
        var (status, doc) = await app.Client().GetJson("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, status);
        foreach (var p in new[] { "/api/chat", "/api/leads", "/api/bookings", "/api/alerts", "/api/renders", "/api/admin/leads" })
            Assert.True(doc.GetProperty("paths").TryGetProperty(p, out _), p);
        Assert.Equal("bearer", doc.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.Equal(HttpStatusCode.OK, (await app.Client().GetAsync("/swagger/index.html")).StatusCode);
    }
}
