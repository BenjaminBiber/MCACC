using System.Net;
using Mcacc.Platform.Common;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.Common;

public class UsageHttpClientTests
{
    private const string Token = "sk-ant-oat01-EXAMPLE-NOT-A-REAL-TOKEN";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sends_the_bearer_token_the_beta_header_and_the_user_agent()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        using var client = new UsageHttpClient(handler);

        var response = await client.GetUsageAsync(Token, Ct);

        response.StatusCode.ShouldBe(200);
        response.Body.ShouldBe("{}");
        var request = handler.Request.ShouldNotBeNull();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.ToString().ShouldBe("https://api.anthropic.com/api/oauth/usage");
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe(Token);
        request.Headers.GetValues("anthropic-beta").ShouldBe(["oauth-2025-04-20"]);
        request.Headers.Accept.Select(a => a.MediaType).ShouldContain("application/json");
        request.Headers.UserAgent.ShouldHaveSingleItem().Product!.Name.ShouldBe("MCACC");
    }

    [Fact]
    public async Task Returns_the_status_of_a_rejected_request()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("slow down"),
        });
        using var client = new UsageHttpClient(handler);

        var response = await client.GetUsageAsync(Token, Ct);

        response.StatusCode.ShouldBe(429);
        response.Body.ShouldBe("slow down");
    }

    [Fact]
    public async Task A_transport_failure_surfaces_as_an_http_request_exception()
    {
        var handler = new ThrowingHandler();
        using var client = new UsageHttpClient(handler);

        await Should.ThrowAsync<HttpRequestException>(() => client.GetUsageAsync(Token, Ct));
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                response.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("no route to host");
    }
}
