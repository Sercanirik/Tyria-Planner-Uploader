using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace TyriaUploader.OAuth;

public sealed class OAuthClient
{
    private const string ClientId = "tyria-uploader";
    private const string Scope = "logs:write";
    private const int CallbackTimeoutSeconds = 180;

    private readonly string _apiBaseUrl;
    private readonly string? _webBaseOverride;
    private readonly HttpClient _http;

    public OAuthClient(string apiBaseUrl, HttpClient http, string? webBaseUrl = null)
    {
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _webBaseOverride = string.IsNullOrWhiteSpace(webBaseUrl) ? null : webBaseUrl.TrimEnd('/');
        _http = http;
    }

    public sealed record UserInfo(string Id, string Username, string DisplayName);
    public sealed record SignInResult(string AccessToken, string Scope, UserInfo? User);

    public async Task<SignInResult> SignInAsync(CancellationToken ct = default)
    {
        var verifier = PkceUtil.CreateCodeVerifier();
        var challenge = PkceUtil.ComputeChallenge(verifier);
        var state = PkceUtil.CreateState();

        using var listener = new HttpListener();
        var port = ReserveLoopbackPort();
        var redirectUri = $"http://127.0.0.1:{port}/callback";
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var webBase = _webBaseOverride ?? WebBaseFromApi(_apiBaseUrl);
        var authorizeUrl =
            $"{webBase}/oauth/authorize" +
            $"?client_id={ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&code_challenge={challenge}" +
            $"&code_challenge_method=S256" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(Scope)}" +
            $"&state={state}";

        OpenInBrowser(authorizeUrl);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(CallbackTimeoutSeconds));

        string? code = null;
        string? returnedState = null;
        string? error = null;

        try
        {
            var contextTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(-1, timeoutCts.Token));
            if (completed != contextTask)
            {
                throw new OperationCanceledException("Sign-in timed out, no callback received.");
            }
            var ctx = await contextTask;

            var query = ctx.Request.QueryString;
            code = query["code"];
            returnedState = query["state"];
            error = query["error"];

            var html = error == null
                ? "<html><body style='font-family:Segoe UI,sans-serif;background:#0f0e17;color:#efe6d3;padding:64px;text-align:center;'>" +
                  "<h2 style='color:#c8a97e;font-weight:600;letter-spacing:0.02em;'>Tyria Uploader</h2>" +
                  "<p style='font-size:1.05em;'>Signed in successfully, you can close this tab.</p></body></html>"
                : $"<html><body style='font-family:Segoe UI,sans-serif;background:#0f0e17;color:#efe6d3;padding:64px;text-align:center;'>" +
                  $"<h2 style='color:#e85e6c;font-weight:600;'>Sign-in failed</h2><p>{WebUtility.HtmlEncode(error)}</p></body></html>";

            var bytes = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, ct);
            ctx.Response.OutputStream.Close();
        }
        finally
        {
            listener.Stop();
        }

        if (error != null)
            throw new InvalidOperationException($"OAuth authorization denied: {error}");
        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("OAuth callback missing code");
        if (returnedState != state)
            throw new InvalidOperationException("OAuth state mismatch, possible CSRF attempt");

        var tokenRequest = new TokenRequest(
            grant_type: "authorization_code",
            code: code!,
            code_verifier: verifier,
            client_id: ClientId,
            redirect_uri: redirectUri);
        using var resp = await _http.PostAsJsonAsync($"{_apiBaseUrl}/api/oauth/token", tokenRequest, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Token exchange failed ({(int)resp.StatusCode}): {body}");

        var token = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(body)
            ?? throw new InvalidOperationException("Invalid token response");

        var user = token.User != null
            ? new UserInfo(token.User.Id, token.User.Username, token.User.DisplayName)
            : null;
        return new SignInResult(token.AccessToken, token.Scope ?? "", user);
    }

    private static int ReserveLoopbackPort()
    {

        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static string WebBaseFromApi(string apiBaseUrl)
    {
        if (apiBaseUrl.Contains("localhost") || apiBaseUrl.Contains("127.0.0.1"))
            return "http://localhost:5173";
        return apiBaseUrl;
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {

            try { Clipboard.SetText(url); } catch { }
        }
    }

    private sealed record TokenRequest(
        string grant_type,
        string code,
        string code_verifier,
        string client_id,
        string redirect_uri);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("user")]
        public TokenUserPayload? User { get; set; }
    }

    private sealed class TokenUserPayload
    {
        [JsonPropertyName("id")]          public string Id { get; set; } = "";
        [JsonPropertyName("username")]    public string Username { get; set; } = "";
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    }
}
