using Mcacc.Core.Usage;

namespace Mcacc.Core.Abstractions;

public interface IUsageHttp
{
    /// <exception cref="HttpRequestException">The request did not reach the endpoint.</exception>
    /// <exception cref="TaskCanceledException">The request timed out or was canceled.</exception>
    Task<UsageHttpResponse> GetUsageAsync(string accessToken, CancellationToken ct);

    /// <summary>Asks which account the token belongs to.</summary>
    /// <exception cref="HttpRequestException">The request did not reach the endpoint.</exception>
    /// <exception cref="TaskCanceledException">The request timed out or was canceled.</exception>
    Task<UsageHttpResponse> GetProfileAsync(string accessToken, CancellationToken ct);
}
