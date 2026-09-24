using Mcacc.Core.Config;
using Mcacc.Core.FileSystem;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.FileSystem;

public sealed class JsonFileTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string File(string name) => Path.Join(_temp.Path, "sub", name);

    private static readonly Dictionary<string, UsageSnapshot> Value = new()
    {
        ["privat"] = new UsageSnapshot(new UsageWindow(42, null), null, new DateTimeOffset(2026, 9, 24, 8, 0, 0, TimeSpan.Zero)),
    };

    [Fact]
    public void Writes_into_a_missing_folder_and_reads_it_back()
    {
        JsonFile.Write(File("a.json"), Value, McaccJsonContext.Default.DictionaryStringUsageSnapshot);

        var read = JsonFile.Read(File("a.json"), McaccJsonContext.Default.DictionaryStringUsageSnapshot);

        read.ShouldNotBeNull();
        read["privat"].ShouldBe(Value["privat"]);
    }

    [Fact]
    public void Leaves_no_temp_file_behind()
    {
        JsonFile.Write(File("a.json"), Value, McaccJsonContext.Default.DictionaryStringUsageSnapshot);

        Directory.GetFiles(Path.Join(_temp.Path, "sub")).Select(Path.GetFileName).ShouldBe(["a.json"]);
    }

    [Fact]
    public void A_missing_file_reads_as_null()
    {
        JsonFile.Read(File("missing.json"), McaccJsonContext.Default.DictionaryStringUsageSnapshot).ShouldBeNull();
    }

    [Fact]
    public void A_broken_file_reads_as_null()
    {
        Directory.CreateDirectory(Path.Join(_temp.Path, "sub"));
        System.IO.File.WriteAllText(File("broken.json"), "{ \"privat\": ");

        JsonFile.Read(File("broken.json"), McaccJsonContext.Default.DictionaryStringUsageSnapshot).ShouldBeNull();
    }
}
