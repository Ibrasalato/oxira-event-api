using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oxira.Events.Application.Common;
using Oxira.Events.Domain;
using static Oxira.Events.Domain.Text;

namespace Oxira.Events.Application.Features.Renders;

public record RenderBoothCommand(RenderInput Input) : ICommand<RenderResult>;
public record RenderQuotaQuery(string SessionId) : IQuery<RenderResult>;

/// <summary>
/// Turns the planner's view into a photorealistic stand render. Free renders are counted per session, per IP and per day;
/// a failed render gives its quota back.
/// </summary>
public partial class RenderHandlers(IAppDbContext db, TimeProvider clock, IQuota quota, IImageEditor images, ICurrentUser me,
    IOptions<RenderOptions> opt, ILogger<RenderHandlers> log)
    : IRequestHandler<RenderBoothCommand, RenderResult>, IRequestHandler<RenderQuotaQuery, RenderResult>
{
    [GeneratedRegex(@"^data:image/(jpeg|png|webp);base64,")] private static partial Regex DataUrl();

    public async Task<RenderResult> Handle(RenderQuotaQuery q, CancellationToken ct)
    {
        var used = await quota.UsedAsync($"render:s:{SessionKey(q.SessionId)}", ct);
        return new RenderResult(true, null, Math.Max(0, opt.Value.PerSession - used));
    }

    public async Task<RenderResult> Handle(RenderBoothCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var sid = SessionKey(i.SessionId);
        var m = DataUrl().Match(i.Image ?? "");
        if (sid.Length < 6 || !m.Success) throw new DomainException("صورة غير صالحة", "invalid");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(i.Image![m.Length..]); } catch (FormatException) { throw new DomainException("صورة غير صالحة", "invalid"); }
        if (bytes.Length > opt.Value.MaxImageBytes) throw new DomainException("الصورة كبيرة جدًا", "too_big", 413);

        var keys = new[] { ($"render:s:{sid}", opt.Value.PerSession), ($"render:ip:{me.Ip}", opt.Value.PerIp), ("render:all", opt.Value.PerDay) };
        var taken = new List<string>();
        int? sessionCount = null;
        foreach (var (key, limit) in keys)
        {
            var n = await quota.TryConsumeAsync(key, limit, ct);
            if (n == null)
            {
                foreach (var k in taken) await quota.RefundAsync(k, ct);
                var left = Math.Max(0, opt.Value.PerSession - await quota.UsedAsync(keys[0].Item1, ct));
                return new RenderResult(false, null, key == keys[0].Item1 ? 0 : left, "limit");
            }
            sessionCount ??= n;
            taken.Add(key);
        }

        var config = i.Config is { ValueKind: JsonValueKind.Object } c ? c : default;
        var log1 = new RenderLog { SessionId = sid, Ip = me.Ip, Lang = Langs.Of(i.Lang), ConfigJson = config.ValueKind == JsonValueKind.Object ? Clip(config.GetRawText(), 2000) : "", CreatedAt = clock.GetUtcNow() };
        try
        {
            var jpeg = await images.EditAsync(bytes, "image/" + m.Groups[1].Value, Prompt(config), ct);
            log1.Ok = true;
            db.RenderLogs.Add(log1);
            await db.SaveChangesAsync(ct);
            return new RenderResult(true, "data:image/jpeg;base64," + Convert.ToBase64String(jpeg), Math.Max(0, opt.Value.PerSession - sessionCount!.Value));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogError(e, "Booth render failed for {Session}", sid);
            foreach (var k in taken) await quota.RefundAsync(k, ct);
            db.RenderLogs.Add(log1);
            await db.SaveChangesAsync(ct);
            return new RenderResult(false, null, Math.Max(0, opt.Value.PerSession - sessionCount!.Value + 1), "failed");
        }
    }

    private static readonly Dictionary<string, string> Walls = new() { ["white"] = "glossy white", ["navy"] = "deep navy blue", ["black"] = "matte black", ["wood"] = "warm natural wood veneer" };
    private static readonly Dictionary<string, string> Floors = new() { ["grey"] = "grey carpet", ["blue"] = "blue carpet", ["red"] = "red carpet", ["wood"] = "light wooden raised floor" };
    private static readonly Dictionary<string, string> Types = new() { ["row"] = "an inline stand open on one side", ["corner"] = "a corner stand open on two sides", ["peninsula"] = "a peninsula stand open on three sides", ["island"] = "an island stand open on all four sides" };
    private static readonly Dictionary<string, string> Extras = new()
    {
        ["led"] = "a large LED video wall", ["meeting"] = "a small meeting area with chairs", ["storage"] = "a lockable storage room", ["counter"] = "a branded reception counter",
        ["lighting"] = "track spotlights and a lit header", ["sound"] = "discreet speakers", ["print"] = "large-format printed graphics", ["giveaways"] = "a giveaway display shelf", ["media"] = "a camera crew corner",
    };

    /// <summary>keeps the planner's geometry; only materials, light and realism change</summary>
    public static string Prompt(JsonElement c)
    {
        string S(string k, string d) => c.ValueKind == JsonValueKind.Object && c.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? d : d;
        var size = c.ValueKind == JsonValueKind.Object && c.TryGetProperty("size", out var sz) && sz.TryGetDouble(out var n) ? $"{n:0} m²" : "";
        var extras = c.ValueKind == JsonValueKind.Object && c.TryGetProperty("extras", out var ex) && ex.ValueKind == JsonValueKind.Array
            ? string.Join(", ", ex.EnumerateArray().Select(x => Extras.GetValueOrDefault(x.GetString() ?? "")).Where(x => x != null)) : "";
        var floor = S("carpet", S("floor", "grey"));
        var accent = S("accent", "#F5A800");
        return "Photorealistic professional render of this trade-show exhibition stand, as built by a premium stand contractor in a Saudi exhibition hall. " +
               "Keep exactly the same layout, camera angle, proportions, walls, openings and furniture positions as the input image; do not add or remove structures. " +
               $"It is {Types.GetValueOrDefault(S("type", "row"), Types["row"])}{(size != "" ? ", " + size : "")}. " +
               $"Walls: {Walls.GetValueOrDefault(S("wall", "white"), "glossy white")}; floor: {Floors.GetValueOrDefault(floor, "grey carpet")}; brand accent colour {accent}. " +
               (extras != "" ? $"Include {extras}. " : "") +
               "Bright even exhibition-hall lighting, soft shadows, crisp materials, subtle visitors in the background, no text artefacts, no watermarks.";
    }
}
