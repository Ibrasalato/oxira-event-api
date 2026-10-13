using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Oxira.Events.Application.Common;

namespace Oxira.Events.Infrastructure.Services;

/// <summary>"Anthropic" and "OpenAi" sections; keys come from environment variables (Anthropic__ApiKey, OpenAi__ApiKey).</summary>
public class AnthropicOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public string Model { get; set; } = "claude-sonnet-5";
}

public class OpenAiOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openai.com";
    public string ImageModel { get; set; } = "gpt-image-1-mini";
    public string Size { get; set; } = "1536x1024";
    public string Quality { get; set; } = "medium";
}

public class AnthropicClient(HttpClient http, IOptions<AnthropicOptions> opt) : IAnthropic
{
    public async Task<AiResponse> CreateMessageAsync(AiRequest r, CancellationToken ct = default)
    {
        var o = opt.Value;
        if (string.IsNullOrEmpty(o.ApiKey)) throw new InvalidOperationException("Anthropic:ApiKey is not set");
        var body = new JsonObject
        {
            ["model"] = r.Model ?? o.Model,
            ["max_tokens"] = r.MaxTokens,
            ["system"] = r.System,
            ["messages"] = r.Messages.DeepClone(),
        };
        if (r.Tools is { Count: > 0 }) body["tools"] = r.Tools.DeepClone();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{o.BaseUrl.TrimEnd('/')}/v1/messages") { Content = JsonContent.Create(body) };
        req.Headers.Add("x-api-key", o.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new HttpRequestException($"Anthropic {(int)res.StatusCode}: {text[..Math.Min(text.Length, 400)]}");
        var json = JsonNode.Parse(text)!.AsObject();
        return new AiResponse(json["content"]?.AsArray() ?? [], (string?)json["stop_reason"] ?? "end_turn");
    }
}

public class OpenAiImageEditor(HttpClient http, IOptions<OpenAiOptions> opt) : IImageEditor
{
    public async Task<byte[]> EditAsync(byte[] image, string mime, string prompt, CancellationToken ct = default)
    {
        var o = opt.Value;
        if (string.IsNullOrEmpty(o.ApiKey)) throw new InvalidOperationException("OpenAi:ApiKey is not set");
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(file, "image", "view" + (mime.EndsWith("png") ? ".png" : mime.EndsWith("webp") ? ".webp" : ".jpg"));
        form.Add(new StringContent(o.ImageModel), "model");
        form.Add(new StringContent(prompt), "prompt");
        form.Add(new StringContent(o.Size), "size");
        form.Add(new StringContent(o.Quality), "quality");
        form.Add(new StringContent("jpeg"), "output_format");
        form.Add(new StringContent("86"), "output_compression");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{o.BaseUrl.TrimEnd('/')}/v1/images/edits") { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new HttpRequestException($"OpenAI {(int)res.StatusCode}: {text[..Math.Min(text.Length, 400)]}");
        using var doc = JsonDocument.Parse(text);
        var b64 = doc.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString();
        return Convert.FromBase64String(b64 ?? throw new HttpRequestException("OpenAI returned no image"));
    }
}

/// <summary>"Exhibitions" section: reads the site's calendar (events.oxira.sa/events.json), cached for an hour.</summary>
public class ExhibitionFeedOptions
{
    public string Url { get; set; } = "https://events.oxira.sa/events.json";
}

public class ExhibitionFeed(HttpClient http, IOptions<ExhibitionFeedOptions> opt, TimeProvider clock) : IExhibitionFeed
{
    private static IReadOnlyList<Exhibition>? _cache;
    private static DateTimeOffset _at;

    public async Task<IReadOnlyList<Exhibition>> GetAsync(CancellationToken ct = default)
    {
        if (_cache != null && clock.GetUtcNow() - _at < TimeSpan.FromHours(1)) return _cache;
        using var doc = JsonDocument.Parse(await http.GetStringAsync(opt.Value.Url, ct));
        var list = new List<Exhibition>();
        foreach (var e in doc.RootElement.GetProperty("events").EnumerateArray())
        {
            string S(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            if (!DateOnly.TryParse(S("start"), out var start)) continue;
            var end = DateOnly.TryParse(S("end"), out var x) ? x : start;
            list.Add(new Exhibition(S("name"), S("name_ar"), S("sector"), start, end, S("city"), S("venue"), S("url")));
        }
        _cache = list;
        _at = clock.GetUtcNow();
        return list;
    }
}
