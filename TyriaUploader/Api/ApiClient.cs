using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TyriaUploader.Api;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private string? _accessToken;

    private const int MaxErrorBodyChars = 4096;

    private const int GzipThresholdBytes = 512 * 1024;

    public ApiClient(string baseUrl, HttpClient? http = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public void SetAccessToken(string? token) => _accessToken = token;

    public sealed record UploadResult(bool Success, bool Deduplicated, bool Skipped, string? Error, string? Reason = null, int? StatusCode = null, bool Wipe = false);

    public async Task<UploadResult> UploadEiJsonAsync(string eiJsonText, string logHash, CancellationToken ct = default)
    {

        try
        {
            using var _ = JsonDocument.Parse(eiJsonText);
        }
        catch (JsonException ex)
        {
            return new UploadResult(false, false, false, $"Invalid EI JSON: {ex.Message}");
        }

        var bodyText = "{\"eiJson\":" + eiJsonText + ",\"logHash\":" + JsonSerializer.Serialize(logHash) + "}";
        var bodyBytes = Encoding.UTF8.GetBytes(bodyText);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/logs/eijson")
        {
            Content = bodyBytes.Length >= GzipThresholdBytes
                ? BuildGzippedJsonContent(bodyBytes)
                : BuildJsonContent(bodyBytes),
        };
        AttachAuth(req);

        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        var status = (int)resp.StatusCode;

        if (resp.IsSuccessStatusCode)
        {
            var payload = JsonSerializer.Deserialize<UploadEiJsonResponse>(text);
            return new UploadResult(true, payload?.Deduplicated ?? false, payload?.Skipped ?? false, null, payload?.Reason, status, payload?.Wipe ?? false);
        }
        var snippet = text.Length > MaxErrorBodyChars ? text[..MaxErrorBodyChars] + "…" : text;
        return new UploadResult(false, false, false, $"HTTP {status}: {snippet}", null, status);
    }

    private static HttpContent BuildJsonContent(byte[] bodyBytes)
    {
        var c = new ByteArrayContent(bodyBytes);
        c.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return c;
    }

    private static HttpContent BuildGzippedJsonContent(byte[] bodyBytes)
    {
        using var ms = new MemoryStream(bodyBytes.Length / 4);
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(bodyBytes, 0, bodyBytes.Length);
        var c = new ByteArrayContent(ms.ToArray());
        c.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        c.Headers.ContentEncoding.Add("gzip");
        return c;
    }

    public Task<IReadOnlyList<string>?> GetKnownHashesAsync(CancellationToken ct = default) =>
        FetchHashesAsync("/api/logs/known-hashes", ct);

    public Task<IReadOnlyList<string>?> GetIgnoredHashesAsync(CancellationToken ct = default) =>
        FetchHashesAsync("/api/logs/ignored-hashes", ct);

    private async Task<IReadOnlyList<string>?> FetchHashesAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_accessToken)) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}{path}");
            AttachAuth(req);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;
            await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            var payload = await JsonSerializer.DeserializeAsync<KnownHashesResponse>(stream, cancellationToken: cts.Token);
            return payload?.Hashes ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch
        {
            return null;
        }
    }

    private void AttachAuth(HttpRequestMessage req)
    {
        if (!string.IsNullOrEmpty(_accessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private sealed class UploadEiJsonResponse
    {
        [JsonPropertyName("id")]            public string? Id { get; set; }
        [JsonPropertyName("deduplicated")]  public bool Deduplicated { get; set; }
        [JsonPropertyName("skipped")]       public bool Skipped { get; set; }

        [JsonPropertyName("reason")]        public string? Reason { get; set; }

        [JsonPropertyName("wipe")]          public bool Wipe { get; set; }
    }

    private sealed class KnownHashesResponse
    {
        [JsonPropertyName("hashes")] public List<string> Hashes { get; set; } = new();
    }
}
