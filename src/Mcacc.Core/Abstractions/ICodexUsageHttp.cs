using Mcacc.Core.Usage;

namespace Mcacc.Core.Abstractions;

public interface ICodexUsageHttp
{
    /// <param name="accountId">The account the token belongs to; the endpoint wants it in a header.</param>
    /// <exception cref="HttpRequestException">The request did not reach the endpoint.</exception>
    /// <exception cref="TaskCanceledException">The request timed out or was canceled.</exception>
    Task<UsageHttpResponse> GetUsageAsync(string accessToken, string? accountId, CancellationToken ct);
}
