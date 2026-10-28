using System.Text;
using Mcacc.Core.Codex;
using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Codex;

public class CodexRolloutLineReaderTests
{
    private const string Meta =
        """{"timestamp":"2026-10-01T06:28:01.000Z","type":"session_meta","payload":{"id":"s1","cwd":"C:\\Repos\\A","model":"gpt-6.1-sol"}}""";

    private const string Turn =
        """{"timestamp":"2026-10-01T06:28:04.000Z","type":"turn_context","payload":{"turn_id":"t1","cwd":"C:\\Repos\\B","model":"gpt-5-codex"}}""";

    /// <summary>Exactly the shape of a real line: total = input + output, with the cached part inside input.</summary>
    private const string Usage =
        """{"timestamp":"2026-10-01T06:28:09.508Z","type":"token_usage_record","payload":{"thread_id":"th1","turn_id":"t1","response_id":"resp_1","usage":{"input_tokens":17376,"cached_input_tokens":13184,"cache_write_input_tokens":100,"output_tokens":14,"reasoning_output_tokens":0,"total_tokens":17390}}}""";

    private static LogUsage? Read(CodexRolloutLineReader reader, string line)
    {
        reader.TryRead(Encoding.UTF8.GetBytes(line), out var usage).ShouldBeTrue();
        return usage;
    }

    /// <summary>
    /// The guard against the one mistake that would silently double every session: Codex counts the cached and
    /// written parts inside input_tokens, Anthropic beside it.
    /// </summary>
    [Fact]
    public void The_counted_tokens_add_up_to_what_the_line_says()
    {
        var reader = new CodexRolloutLineReader();
        Read(reader, Meta);

        var usage = Read(reader, Usage).ShouldNotBeNull();

        usage.Tokens.Total.ShouldBe(17390);
        usage.Tokens.Input.ShouldBe(17376 - 13184 - 100);
        usage.Tokens.CacheRead.ShouldBe(13184);
        usage.Tokens.CacheWrite5Min.ShouldBe(100);
        usage.Tokens.CacheWrite1Hour.ShouldBe(0);
        usage.Tokens.Output.ShouldBe(14);
    }

    [Fact]
    public void A_usage_line_takes_the_model_and_the_project_of_the_turn_before_it()
    {
        var reader = new CodexRolloutLineReader();
        Read(reader, Meta);
        Read(reader, Turn);

        var usage = Read(reader, Usage).ShouldNotBeNull();

        usage.Model.ShouldBe("gpt-5-codex");
        usage.Project.ShouldBe(@"C:\Repos\B");
        usage.Session.ShouldBe("th1");
        usage.Timestamp.ShouldBe(new DateTimeOffset(2026, 10, 1, 6, 28, 9, 508, TimeSpan.Zero));
    }

    [Fact]
    public void Without_a_turn_the_session_header_decides()
    {
        var reader = new CodexRolloutLineReader();
        Read(reader, Meta);

        var usage = Read(reader, Usage).ShouldNotBeNull();

        usage.Model.ShouldBe("gpt-6.1-sol");
        usage.Project.ShouldBe(@"C:\Repos\A");
    }

    /// <summary>
    /// A scan that resumes in the middle of a file has read no header at all; without the carried state every line
    /// after the resume point would land under an unknown model.
    /// </summary>
    [Fact]
    public void A_resumed_reader_starts_from_the_state_it_was_given()
    {
        var reader = new CodexRolloutLineReader { State = new LogReaderState("gpt-5-codex", @"C:\Repos\B") };

        var usage = Read(reader, Usage).ShouldNotBeNull();

        usage.Model.ShouldBe("gpt-5-codex");
        usage.Project.ShouldBe(@"C:\Repos\B");
    }

    [Fact]
    public void The_state_is_what_a_resumed_scan_would_need()
    {
        var reader = new CodexRolloutLineReader();

        Read(reader, Meta);
        reader.State.ShouldBe(new LogReaderState("gpt-6.1-sol", @"C:\Repos\A"));

        Read(reader, Turn);
        reader.State.ShouldBe(new LogReaderState("gpt-5-codex", @"C:\Repos\B"));
    }

    /// <summary>A turn that names only one of the two keeps the other.</summary>
    [Fact]
    public void A_partial_turn_keeps_what_it_does_not_name()
    {
        var reader = new CodexRolloutLineReader();
        Read(reader, Meta);

        Read(reader, """{"timestamp":"2026-10-01T06:29:00.000Z","type":"turn_context","payload":{"cwd":"C:\\Repos\\C"}}""");

        reader.State.ShouldBe(new LogReaderState("gpt-6.1-sol", @"C:\Repos\C"));
    }

    [Fact]
    public void Without_any_context_the_model_is_unknown()
    {
        var usage = Read(new CodexRolloutLineReader(), Usage).ShouldNotBeNull();

        usage.Model.ShouldBe("unknown");
        usage.Project.ShouldBe("");
    }

    [Theory]
    [InlineData("""{"timestamp":"2026-10-01T06:28:09.000Z","type":"event_msg","payload":{"type":"agent_message","text":"hi"}}""")]
    [InlineData("""{"timestamp":"2026-10-01T06:28:09.000Z","type":"token_usage_record","payload":{"response_id":"r","usage":{"input_tokens":0,"output_tokens":0,"total_tokens":0}}}""")]
    [InlineData("""{"type":"token_usage_record","payload":{"response_id":"r","usage":{"input_tokens":5,"output_tokens":1}}}""")]
    public void A_line_without_countable_usage_yields_nothing(string line)
    {
        Read(new CodexRolloutLineReader(), line).ShouldBeNull();
    }

    [Fact]
    public void A_broken_line_is_reported_as_broken()
    {
        var reader = new CodexRolloutLineReader();

        reader.TryRead(Encoding.UTF8.GetBytes("""{"type":"token_usage_record","payload":{"usage":{"""), out var usage)
            .ShouldBeFalse();

        usage.ShouldBeNull();
    }

    /// <summary>The same response must count once, however often it appears.</summary>
    [Fact]
    public void The_id_comes_from_the_response_so_a_repeat_is_recognised()
    {
        var first = Read(new CodexRolloutLineReader(), Usage).ShouldNotBeNull();
        var again = Read(new CodexRolloutLineReader(), Usage).ShouldNotBeNull();

        again.Id.ShouldBe(first.Id);
    }
}
