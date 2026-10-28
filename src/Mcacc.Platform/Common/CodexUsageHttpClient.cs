using System.Net.Http.Headers;
using System.Reflection;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Usage;

namespace Mcacc.Platform.Common;

/// <summary>
/// Calls the usage endpoint the Codex CLI itself uses. It is undocumented, so a failure is never fatal: the service
/// falls back to the limits Codex has written to disk. The token only ever goes into the Authorization header.
/// </summary>
public sealed class CodexUsageHttpClient : ICodexUsageHttp, IDisposable
{
    private const string UsageEndpoint = "https://chatgpt.com/backend-api/wham/usage";
    private const string AccountHeader = "chatgpt-account-id";
    private const string OriginatorHeader = "originator";
    private const string OriginatorValue = "codex_cli_rs";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly Lazy<HttpClient> Shared = new(() => Create(handler: null));

    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public CodexUsageHttpClient()
    {
        _client = Shared.Value;
        _ownsClient = false;
    }

    /// <summary>For tests: every request goes through <paramref name="handler"/>.</summary>
    public CodexUsageHttpClient(HttpMessageHandler handler)
    {
        _client = Create(handler);
        _ownsClient = true;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    public async Task<UsageHttpResponse> GetUsageAsync(string accessToken, string? accountId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add(OriginatorHeader, OriginatorValue);
        if (accountId is not null)
        {
            request.Headers.Add(AccountHeader, accountId);
        }

        using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new UsageHttpResponse((int)response.StatusCode, body);
    }

    private static HttpClient Create(HttpMessageHandler? handler)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = Timeout;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MCACC", Version()));
        return client;
    }

    private static string Version() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(fieldCount: 3) ?? "0.0.0";
}
