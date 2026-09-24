using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public class CostEstimatorTests
{
    private static readonly TokenCounts OneMillionEach = new(1_000_000, 1_000_000, 1_000_000, 1_000_000, 1_000_000);

    [Fact]
    public void Prices_every_kind_of_token()
    {
        // 5 input + 25 output + 6.25 cache write 5 min + 10 cache write 1 h + 0.5 cache read
        CostEstimator.Estimate("claude-opus-4-5", OneMillionEach).ShouldBe(46.75m);
    }

    [Fact]
    public void Scales_with_the_token_count()
    {
        CostEstimator.Estimate("claude-sonnet-4-5", new TokenCounts(1000, 0, 0, 0, 0)).ShouldBe(0.003m);
    }

    [Theory]
    [InlineData("claude-opus-4-5-20251101", "claude-opus-4-5")]
    [InlineData("claude-opus-4-5[1m]", "claude-opus-4-5")]
    [InlineData("claude-sonnet-4-20250514", "claude-sonnet-4")]
    [InlineData("Claude-Haiku-4-5-20251001", "claude-haiku-4-5")]
    [InlineData("claude-fable-5-1", "claude-fable-5-1")]
    public void The_date_and_the_context_suffix_do_not_change_the_model(string model, string expected)
    {
        ModelPricing.Normalize(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData("claude-fable-5-1", 10, 50, 0.25)]
    [InlineData("claude-mythos-5-1", 10, 50, 0.25)]
    [InlineData("claude-fable-5", 10, 50, 1)]
    [InlineData("claude-opus-5-5", 4, 20, 0.20)]
    [InlineData("claude-opus-5", 5, 25, 0.5)]
    [InlineData("claude-opus-4-8", 5, 25, 0.5)]
    [InlineData("claude-sonnet-5", 2, 10, 0.2)]
    [InlineData("claude-sonnet-4-6", 3, 15, 0.3)]
    [InlineData("claude-3-7-sonnet-20250219", 3, 15, 0.3)]
    [InlineData("claude-haiku-4-5-20251001", 1, 5, 0.1)]
    [InlineData("claude-opus-4-20250514", 15, 75, 1.5)]
    [InlineData("claude-opus-4-1-20250805", 15, 75, 1.5)]
    [InlineData("claude-3-5-haiku-20241022", 0.8, 4, 0.08)]
    [InlineData("claude-3-haiku-20240307", 0.25, 1.25, 0.025)]
    public void Knows_the_price_of_each_model(string model, double input, double output, double cacheRead)
    {
        var price = ModelPricing.Find(model).ShouldNotBeNull();

        price.Input.ShouldBe((decimal)input);
        price.Output.ShouldBe((decimal)output);
        price.CacheRead.ShouldBe((decimal)cacheRead);
        price.CacheWrite5Min.ShouldBe((decimal)input * 1.25m);
        price.CacheWrite1Hour.ShouldBe((decimal)input * 2m);
    }

    [Theory]
    [InlineData("claude-opus-4-9")]
    [InlineData("<synthetic>")]
    [InlineData("unknown")]
    [InlineData("gpt-4o")]
    public void An_unknown_model_has_no_estimate(string model)
    {
        CostEstimator.Estimate(model, OneMillionEach).ShouldBeNull();
    }
}
