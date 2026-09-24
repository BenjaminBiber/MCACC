using System.Net.Http.Headers;
using System.Reflection;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Usage;

namespace Mcacc.Platform.Common;

/// <summary>
/// Calls the unofficial usage and profile endpoints Claude Code itself uses. The token is only put into the
/// Authorization header and never logged.
/// </summary>
public sealed class UsageHttpClient : IUsageHttp, IDisposable
{
    private const string UsageEndpoint = "https://api.anthropic.com/api/oauth/usage";
    private const string ProfileEndpoint = "https://api.anthropic.com/api/oauth/profile";
    private const string BetaHeader = "anthropic-beta";
    private const string BetaValue = "oauth-2025-04-20";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly Lazy<HttpClient> Shared = new(() => Create(handler: null));

    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public UsageHttpClient()
    {
        _client = Shared.Value;
        _ownsClient = false;
    }

    /// <summary>For tests: every request goes through <paramref name="handler"/>.</summary>
    public UsageHttpClient(HttpMessageHandler handler)
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

    public Task<UsageHttpResponse> GetUsageAsync(string accessToken, CancellationToken ct) =>
        GetAsync(UsageEndpoint, accessToken, ct);

    public Task<UsageHttpResponse> GetProfileAsync(string accessToken, CancellationToken ct) =>
        GetAsync(ProfileEndpoint, accessToken, ct);

    private async Task<UsageHttpResponse> GetAsync(string endpoint, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add(BetaHeader, BetaValue);
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
