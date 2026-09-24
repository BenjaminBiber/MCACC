# Usage Tracker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an "Info & Statistik" page per account, opened from the tile's ⋮ menu. It shows every readable account fact, the limits with their history, and the Claude Code token statistics from the local session logs, drawn with custom chart controls.

**Architecture:**
- **Core** gets three new stores under `~/.mcacc/`: the profile cache, the limit history (JSONL) and the stats index. It also gets a log scanner, a pure statistics query and a price table.
- `UsageService` writes the profile and the history as a side effect of the refresh it already does.
- **App** gets a 5-minute `UsagePoller`, an `AccountInfoViewModel` with its view, and four `Render()`-based chart controls whose colors come from new theme tokens.

**Tech Stack:** .NET 10, C# latest, Avalonia 12.1.3 (Fluent, compiled bindings, headless Skia tests), CommunityToolkit.Mvvm, System.Text.Json source generation, xUnit v3 + Shouldly.

Spec: `docs/superpowers/specs/2026-09-24-usage-tracker-design.md` · Ticket: MCA-0009 (board `mcacc`).

## Global Constraints

- **Build:** `Directory.Build.props` sets `TreatWarningsAsErrors`, `Nullable` and `ImplicitUsings`. Every task must build with zero warnings.
- **Dependencies:** Add no new NuGet packages. That rules out a chart library and `System.IO.Hashing`.
- **Visibility:** `Mcacc.Core` has **no** `InternalsVisibleTo`, so every Core type a test touches is `public`. `Mcacc.App` does have `InternalsVisibleTo Mcacc.App.Tests`.
- **Strings:** Every string the user sees lives in `src/Mcacc.App/Localization/Texts.cs` (the info page: `Texts.Info.cs`) and is German. Code, identifiers and comments are English, in the style of the surrounding code: short `<summary>` comments that give the reason.
- **Tokens:** Access and refresh tokens are never stored, logged or shown. The info page keeps only `SubscriptionType`, `Scopes` and `ExpiresAt` from the credentials.
- **Colors:** They come from theme tokens only (`Mcacc.*` in `Theme/Tokens.axaml`). View models hold `ChartColor` values, never brushes (the avatar brush is the existing exception).
- **Warning color:** From `UsageBar.WarningThreshold` (90 %) on, use `Mcacc.Danger`.
- **Costs:** They are always marked with "≈" and carry the note "API-Äquivalent, Schätzung". A model without a price gets no amount and does not count as 0.
- **Missing values:** A field with no source is hidden, not shown as "—". The model table is the one exception: its cost column and the history tooltip show `Texts.InfoNoValue` ("—").
- **JSON:** Serialize with source generation only (`McaccJsonContext`, or the new `McaccCompactJsonContext`). Write atomically (temp file, then `File.Move(overwrite: true)`).
- **Record equality:** Records compare collections by reference, so optional collections (`UsageSnapshot.Extra`, `OAuthCredentials.Scopes`) stay `null` when empty.
- **Test commands:**
  - Core: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~<Class>"`
  - App: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~<Class>"`
  - Full: `dotnet build MCACC.slnx` and `dotnet test MCACC.slnx`
- **Commits:** One commit per task, in the style `feat(core): …` / `feat(app): …` / `refactor(core): …`. Write the message to a file and run `git commit -F <file>`, because PowerShell drops `-F -`. Every message ends with a blank line and `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

---

## File Map

**Core (`src/Mcacc.Core/`)**

| File | Status | Responsibility |
|---|---|---|
| `Paths/McaccPaths.cs` | modify | + `ProfileCacheFile`, `UsageHistoryDir`, `StatsDir`, `UsageHistoryFile(id)`, `StatsFile(id)` |
| `FileSystem/JsonFile.cs` | new | read a json document, or null; write it atomically |
| `Config/McaccJsonContext.cs` | modify | + `Dictionary<string, AccountProfile>` |
| `Config/McaccCompactJsonContext.cs` | new | unindented context for JSONL lines and the stats index |
| `Usage/AccountProfile.cs` | new | what `/api/oauth/profile` says, + `ToOwner()` |
| `Usage/ProfileParser.cs` | new (replaces `TokenOwnerParser.cs`) | parse the profile reply |
| `Usage/ProfileCache.cs` | new | `profile-cache.json` |
| `Usage/UsageCache.cs` | modify | use `JsonFile` |
| `Usage/UsageSnapshot.cs` | modify | + `Extra` windows |
| `Usage/UsageResponseParser.cs` | modify | read `seven_day_*` windows |
| `Usage/UsageHistoryPoint.cs` | new | one line of the history |
| `Usage/UsageHistoryStore.cs` | new | append, read, prune, delete `usage-history/<id>.jsonl` |
| `Usage/UsageService.cs` | modify | store the profile and the history; `GetProfile`, `GetHistory`, `Forget` |
| `Abstractions/IUsageProvider.cs` | modify | + `GetProfile`, `GetHistory`, `Forget` |
| `ClaudeCode/OAuthCredentials.cs`, `CredentialsParser.cs` | modify | + `SubscriptionType`, `Scopes` |
| `ClaudeCode/ClaudeLoginInfo.cs`, `ClaudeJsonLoginReader.cs` | modify | + `OrganizationRole`, `NumStartups`, `InstallMethod`, `ProjectCount` |
| `Stats/TokenCounts.cs`, `TokenBucket.cs`, `LogUsage.cs` | new | stats models |
| `Stats/LogLineParser.cs` | new | one JSONL line to a `LogUsage` |
| `Stats/StatsIndex.cs`, `StatsIndexStore.cs` | new | `stats/<id>.json` |
| `Stats/ClaudeLogScanner.cs` | new | incremental scan of `projects/**/*.jsonl` |
| `Stats/ModelPrice.cs`, `ModelPricing.cs`, `CostEstimator.cs` | new | USD estimate |
| `Stats/AccountStats.cs`, `AccountStatsQuery.cs` | new | buckets to stats for N local days |
| `Abstractions/IClaudeStatsProvider.cs`, `Stats/ClaudeStatsProvider.cs` | new | scan in the background, one scan per account at a time |

**App (`src/Mcacc.App/`)**

| File | Status | Responsibility |
|---|---|---|
| `Services/UsagePoller.cs` | new | 5-min refresh of all accounts, `Updated` event |
| `Services/AppServices.cs` | modify | + `Poller`, `Stats`, `Credentials` |
| `Services/Navigator.cs`, `ViewModels/PageViewModel.cs` | modify | `Deactivate()` on the page being left |
| `CompositionRoot.cs` | modify | wire the new services, start the poller |
| `Theme/Tokens.axaml`, `Theme/ThemeKeys.cs` | modify | `Mcacc.Series1..6` |
| `Theme/Controls.axaml` | modify | `Button.segment` |
| `Controls/MIconKind.cs`, `MIconGlyphs.cs` | modify | `Insights` |
| `Controls/ChartScale.cs`, `ChartData.cs`, `ChartControl.cs` | new | chart basics |
| `Controls/LineChart.cs`, `StackedBarChart.cs`, `DonutChart.cs`, `BarList.cs`, `ChartSwatch.cs` | new | charts |
| `Localization/Texts.cs`, `Texts.Info.cs` | modify / new | `partial`; info texts and format helpers |
| `ViewModels/AccountInfoViewModel.cs`, `AccountInfoItems.cs` | new | the page and its row/ring/KPI items |
| `ViewModels/AccountPickerViewModel.cs` | modify | `InfoText`, `OpenInfo`, poll updates, forget on remove |
| `ViewModels/AccountTileViewModel.cs` | modify | `HintFor` becomes `internal static` |
| `Views/AccountInfoView.axaml(.cs)` | new | the page |
| `Views/AccountPickerView.axaml`, `App.axaml` | modify | menu entry, data template |

**Tests:** see the Files block of each task. New helpers:
- Core: `TestSupport/LogLines.cs`
- App: `FakeClaudeStatsProvider` and `FakeCredentialSource` in `Fakes.cs`. The info formatters use a fixed German culture, so no culture helper is needed.

---

### Task 1: Profile parser, profile cache, new paths and `JsonFile`

**Files:**
- Modify: `src/Mcacc.Core/Paths/McaccPaths.cs`
- Create: `src/Mcacc.Core/FileSystem/JsonFile.cs`
- Modify: `src/Mcacc.Core/Usage/UsageCache.cs`
- Create: `src/Mcacc.Core/Usage/AccountProfile.cs`, `src/Mcacc.Core/Usage/ProfileParser.cs`, `src/Mcacc.Core/Usage/ProfileCache.cs`
- Delete: `src/Mcacc.Core/Usage/TokenOwnerParser.cs`
- Modify: `src/Mcacc.Core/Config/McaccJsonContext.cs`
- Modify: `src/Mcacc.Core/Usage/UsageService.cs` (switch to `ProfileParser`)
- Test: `tests/Mcacc.Core.Tests/Paths/McaccPathsTests.cs`, `tests/Mcacc.Core.Tests/FileSystem/JsonFileTests.cs` (new), `tests/Mcacc.Core.Tests/Usage/ProfileParserTests.cs` (created with `git mv` from `TokenOwnerParserTests.cs`), `tests/Mcacc.Core.Tests/Usage/ProfileCacheTests.cs` (new)

**Interfaces:**
- Consumes: `TokenOwner(string? AccountUuid, string? Email)`, `McaccPaths(string root, PathStyle style)`, `McaccJsonContext`.
- Produces:
  - `McaccPaths.ProfileCacheFile`, `UsageHistoryDir`, `StatsDir`, `UsageHistoryFile(string id)`, `StatsFile(string id)`
  - `JsonFile.Read<T>(string path, JsonTypeInfo<T> type) : T?` (T is a class) and `JsonFile.Write<T>(string path, T value, JsonTypeInfo<T> type)`
  - `AccountProfile(...)` with `TokenOwner ToOwner()`
  - `ProfileParser.Parse(string json) : AccountProfile?`
  - `ProfileCache(McaccPaths)` with `Load() : IReadOnlyDictionary<string, AccountProfile>` and `Save(IReadOnlyDictionary<string, AccountProfile>)`
  - `McaccJsonContext.Default.DictionaryStringAccountProfile`

- [ ] **Step 1: Write the failing path tests**

In `tests/Mcacc.Core.Tests/Paths/McaccPathsTests.cs`, add these lines at the end of `Windows_layout_lives_under_the_root`:

```csharp
        paths.ProfileCacheFile.ShouldBe(@"C:\Users\ben\.mcacc\profile-cache.json");
        paths.UsageHistoryDir.ShouldBe(@"C:\Users\ben\.mcacc\usage-history");
        paths.StatsDir.ShouldBe(@"C:\Users\ben\.mcacc\stats");
        paths.UsageHistoryFile("privat").ShouldBe(@"C:\Users\ben\.mcacc\usage-history\privat.jsonl");
        paths.StatsFile("privat").ShouldBe(@"C:\Users\ben\.mcacc\stats\privat.json");
```

and these at the end of `Posix_layout_lives_under_the_root`:

```csharp
        paths.ProfileCacheFile.ShouldBe("/Users/ben/.mcacc/profile-cache.json");
        paths.UsageHistoryFile("privat").ShouldBe("/Users/ben/.mcacc/usage-history/privat.jsonl");
        paths.StatsFile("privat").ShouldBe("/Users/ben/.mcacc/stats/privat.json");
```

- [ ] **Step 2: Write the failing `JsonFile` tests**

Create `tests/Mcacc.Core.Tests/FileSystem/JsonFileTests.cs`:

```csharp
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
```

- [ ] **Step 3: Move and rewrite the parser tests**

Run: `git mv tests/Mcacc.Core.Tests/Usage/TokenOwnerParserTests.cs tests/Mcacc.Core.Tests/Usage/ProfileParserTests.cs`

Replace its content with:

```csharp
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public class ProfileParserTests
{
    [Fact]
    public void Reads_the_account_and_the_organization()
    {
        var profile = ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json"));

        profile.ShouldBe(new AccountProfile(
            "00000000-0000-0000-0000-000000000001",
            "max.muster@example.com",
            "Benjamin Biber",
            "Benjamin",
            HasClaudeMax: false,
            HasClaudePro: true,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "Example GmbH",
            "claude_pro",
            "stripe_subscription",
            "default_claude_ai"));
    }

    [Fact]
    public void The_owner_is_the_uuid_and_the_email()
    {
        ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json"))!.ToOwner()
            .ShouldBe(new TokenOwner("00000000-0000-0000-0000-000000000001", "max.muster@example.com"));
    }

    [Fact]
    public void The_email_is_optional()
    {
        ProfileParser.Parse("""{ "account": { "uuid": "abc" } }""")!.ToOwner().ShouldBe(new TokenOwner("abc", null));
    }

    [Fact]
    public void The_uuid_is_optional()
    {
        ProfileParser.Parse("""{ "account": { "email": "privat@example.com" } }""")!.ToOwner()
            .ShouldBe(new TokenOwner(null, "privat@example.com"));
    }

    [Fact]
    public void Without_an_organization_its_fields_stay_null()
    {
        var profile = ProfileParser.Parse("""{ "account": { "uuid": "abc", "has_claude_max": "yes" }, "organization": null }""");

        profile.ShouldBe(new AccountProfile("abc", null, null, null, null, null, null, null, null, null, null));
    }

    [Theory]
    [InlineData("""{ "account": { "uuid": " ", "email": "" } }""")]
    [InlineData("""{ "account": null }""")]
    [InlineData("""{ "organization": { "uuid": "abc" } }""")]
    [InlineData("[1, 2, 3]")]
    [InlineData("<html>rate limited</html>")]
    public void A_body_without_an_account_reads_as_null(string json)
    {
        ProfileParser.Parse(json).ShouldBeNull();
    }
}
```

- [ ] **Step 4: Write the failing profile cache test**

Create `tests/Mcacc.Core.Tests/Usage/ProfileCacheTests.cs`:

```csharp
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public sealed class ProfileCacheTests : IDisposable
{
    private static readonly AccountProfile Profile = new(
        "abc", "privat@example.com", "Max Muster", "Max", true, false,
        new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), "Privat", "claude_max", "stripe_subscription", "default_claude_max_5x");

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private McaccPaths Paths => new(
        Path.Join(_temp.Path, ".mcacc"),
        OperatingSystem.IsWindows() ? PathStyle.Windows : PathStyle.Posix);

    [Fact]
    public void Saves_and_loads_the_profiles()
    {
        new ProfileCache(Paths).Save(new Dictionary<string, AccountProfile> { ["privat"] = Profile });

        new ProfileCache(Paths).Load()["privat"].ShouldBe(Profile);
    }

    [Fact]
    public void Without_a_file_there_are_no_profiles()
    {
        new ProfileCache(Paths).Load().ShouldBeEmpty();
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~McaccPathsTests|FullyQualifiedName~JsonFileTests|FullyQualifiedName~ProfileParserTests|FullyQualifiedName~ProfileCacheTests"`
Expected: the build fails with CS0103/CS0117. `ProfileCacheFile`, `JsonFile`, `ProfileParser`, `AccountProfile` and `ProfileCache` do not exist yet.

- [ ] **Step 6: Add the paths**

In `src/Mcacc.Core/Paths/McaccPaths.cs`, add to the constructor after `LogsDir = …`:

```csharp
        ProfileCacheFile = style.Join(root, "profile-cache.json");
        UsageHistoryDir = style.Join(root, "usage-history");
        StatsDir = style.Join(root, "stats");
```

Add these properties after `LogsDir`:

```csharp
    public string ProfileCacheFile { get; }

    public string UsageHistoryDir { get; }

    public string StatsDir { get; }
```

Add these methods after `ClaudeConfigDir`:

```csharp
    public string UsageHistoryFile(string id) => Style.Join(UsageHistoryDir, id + ".jsonl");

    public string StatsFile(string id) => Style.Join(StatsDir, id + ".json");
```

- [ ] **Step 7: Create `JsonFile` and move `UsageCache` onto it**

Create `src/Mcacc.Core/FileSystem/JsonFile.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mcacc.Core.FileSystem;

/// <summary>One json document on disk. A write goes to a temp file first, so a crash never leaves half a file.</summary>
public static class JsonFile
{
    /// <returns>Null when the file is missing, unreadable or not valid json for <paramref name="type"/>.</returns>
    public static T? Read<T>(string path, JsonTypeInfo<T> type)
        where T : class
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(stream, type);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void Write<T>(string path, T value, JsonTypeInfo<T> type)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempFile = path + ".tmp";
        using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, type);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempFile, path, overwrite: true);
    }
}
```

Replace the body of `src/Mcacc.Core/Usage/UsageCache.cs` with this (the class doc comment stays as it is):

```csharp
using Mcacc.Core.Config;
using Mcacc.Core.FileSystem;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

// (keep the existing <summary> of the class here)
public sealed class UsageCache(McaccPaths paths)
{
    public IReadOnlyDictionary<string, UsageSnapshot> Load() =>
        JsonFile.Read(paths.UsageCacheFile, McaccJsonContext.Default.DictionaryStringUsageSnapshot) ?? [];

    public void Save(IReadOnlyDictionary<string, UsageSnapshot> snapshots) =>
        JsonFile.Write(
            paths.UsageCacheFile,
            snapshots.ToDictionary(entry => entry.Key, entry => entry.Value),
            McaccJsonContext.Default.DictionaryStringUsageSnapshot);
}
```

- [ ] **Step 8: Create the profile types**

Create `src/Mcacc.Core/Usage/AccountProfile.cs`:

```csharp
namespace Mcacc.Core.Usage;

/// <summary>
/// What <c>/api/oauth/profile</c> says about the owner of a token. A null field was not in the reply.
/// At least the uuid or the e-mail is set.
/// </summary>
public sealed record AccountProfile(
    string? AccountUuid,
    string? Email,
    string? FullName,
    string? DisplayName,
    bool? HasClaudeMax,
    bool? HasClaudePro,
    DateTimeOffset? CreatedAt,
    string? OrganizationName,
    string? OrganizationType,
    string? BillingType,
    string? RateLimitTier)
{
    public TokenOwner ToOwner() => new(AccountUuid, Email);
}
```

Create `src/Mcacc.Core/Usage/ProfileParser.cs`:

```csharp
using System.Globalization;
using System.Text.Json;

namespace Mcacc.Core.Usage;

/// <summary>Reads the reply of the unofficial <c>/api/oauth/profile</c> endpoint; unknown fields are ignored.</summary>
public static class ProfileParser
{
    /// <returns>Null when the body is not a json object or its account has neither a uuid nor an e-mail.</returns>
    public static AccountProfile? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("account", out var account)
                || account.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var uuid = Text(account, "uuid");
            var email = Text(account, "email");
            if (uuid is null && email is null)
            {
                return null;
            }

            JsonElement? organization = root.TryGetProperty("organization", out var found) && found.ValueKind == JsonValueKind.Object
                ? found
                : null;
            string? Organization(string name) => organization is { } element ? Text(element, name) : null;

            return new AccountProfile(
                uuid,
                email,
                Text(account, "full_name"),
                Text(account, "display_name"),
                Flag(account, "has_claude_max"),
                Flag(account, "has_claude_pro"),
                Date(account, "created_at"),
                Organization("name"),
                Organization("organization_type"),
                Organization("billing_type"),
                Organization("rate_limit_tier"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static bool? Flag(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateTimeOffset? Date(JsonElement owner, string name) =>
        Text(owner, name) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date
            : null;
}
```

Create `src/Mcacc.Core/Usage/ProfileCache.cs`:

```csharp
using Mcacc.Core.Config;
using Mcacc.Core.FileSystem;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>The last profile per account, so the info page shows the account facts offline too. Holds no tokens.</summary>
public sealed class ProfileCache(McaccPaths paths)
{
    public IReadOnlyDictionary<string, AccountProfile> Load() =>
        JsonFile.Read(paths.ProfileCacheFile, McaccJsonContext.Default.DictionaryStringAccountProfile) ?? [];

    public void Save(IReadOnlyDictionary<string, AccountProfile> profiles) =>
        JsonFile.Write(
            paths.ProfileCacheFile,
            profiles.ToDictionary(entry => entry.Key, entry => entry.Value),
            McaccJsonContext.Default.DictionaryStringAccountProfile);
}
```

In `src/Mcacc.Core/Config/McaccJsonContext.cs`, add below the `DictionaryStringUsageSnapshot` line:

```csharp
[JsonSerializable(typeof(Dictionary<string, AccountProfile>), TypeInfoPropertyName = "DictionaryStringAccountProfile")]
```

- [ ] **Step 9: Replace `TokenOwnerParser`**

Run: `git rm src/Mcacc.Core/Usage/TokenOwnerParser.cs`

In `src/Mcacc.Core/Usage/UsageService.cs`, in `OwnerAsync`, replace

```csharp
        if (body is null || TokenOwnerParser.Parse(body) is not { } owner)
        {
            return (UsageState.Error, null);
        }
```

with

```csharp
        if (body is null || ProfileParser.Parse(body)?.ToOwner() is not { } owner)
        {
            return (UsageState.Error, null);
        }
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests`
Expected: PASS. That includes the unchanged `UsageCacheTests` and `UsageServiceTests`.

- [ ] **Step 11: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): parse the whole profile and cache it per account"
```

---

### Task 2: Extra limit windows

**Files:**
- Modify: `src/Mcacc.Core/Usage/UsageSnapshot.cs`, `src/Mcacc.Core/Usage/UsageResponseParser.cs`
- Test: `tests/Mcacc.Core.Tests/Usage/UsageResponseParserTests.cs`, `tests/Mcacc.Core.Tests/Usage/UsageCacheTests.cs`

**Interfaces:**
- Produces: `UsageSnapshot(UsageWindow? FiveHour, UsageWindow? SevenDay, DateTimeOffset FetchedAt, IReadOnlyDictionary<string, UsageWindow>? Extra = null)`. The keys are the raw API names (e.g. `seven_day_opus`). The dictionary is null when the reply has no extra window object.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Mcacc.Core.Tests/Usage/UsageResponseParserTests.cs`:

```csharp
    [Fact]
    public void Reads_the_other_seven_day_windows_as_extra()
    {
        var json = """
            {
              "five_hour": { "utilization": 1, "resets_at": null },
              "seven_day": { "utilization": 2, "resets_at": null },
              "seven_day_opus": { "utilization": 55.5, "resets_at": "2026-09-27T09:00:00Z" },
              "seven_day_sonnet": { "utilization": null, "resets_at": null },
              "seven_day_oauth_apps": null,
              "extra_usage": { "utilization": 99 }
            }
            """;

        var extra = UsageResponseParser.Parse(json, FetchedAt)!.Extra;

        extra.ShouldNotBeNull();
        extra.Keys.Order().ShouldBe(["seven_day_opus", "seven_day_sonnet"]);
        extra["seven_day_opus"].ShouldBe(new UsageWindow(55.5, new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero)));
        extra["seven_day_sonnet"].ShouldBe(new UsageWindow(null, null));
    }

    [Fact]
    public void Without_extra_windows_extra_stays_null()
    {
        UsageResponseParser.Parse(FixtureFiles.ReadAllText("usage-ok.json"), FetchedAt)!.Extra.ShouldBeNull();
    }
```

Add to `tests/Mcacc.Core.Tests/Usage/UsageCacheTests.cs` (it already has `Paths`, `_temp` and the usings for `Mcacc.Core.Usage`):

```csharp
    [Fact]
    public void The_extra_windows_survive_the_round_trip()
    {
        var snapshot = new UsageSnapshot(
            null,
            null,
            new DateTimeOffset(2026, 9, 24, 8, 0, 0, TimeSpan.Zero),
            new Dictionary<string, UsageWindow> { ["seven_day_opus"] = new(12, null) });

        new UsageCache(Paths).Save(new Dictionary<string, UsageSnapshot> { ["privat"] = snapshot });

        new UsageCache(Paths).Load()["privat"].Extra!["seven_day_opus"].ShouldBe(new UsageWindow(12, null));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~UsageResponseParserTests|FullyQualifiedName~UsageCacheTests"`
Expected: the build fails. `UsageSnapshot` has no `Extra`.

- [ ] **Step 3: Implement**

Replace `src/Mcacc.Core/Usage/UsageSnapshot.cs` with:

```csharp
namespace Mcacc.Core.Usage;

/// <param name="Extra">
/// Further windows such as <c>seven_day_opus</c>, keyed by their API name; null when the reply has none. Null rather than
/// empty keeps the record equality of snapshots without extra windows.
/// </param>
public sealed record UsageSnapshot(
    UsageWindow? FiveHour,
    UsageWindow? SevenDay,
    DateTimeOffset FetchedAt,
    IReadOnlyDictionary<string, UsageWindow>? Extra = null);
```

In `src/Mcacc.Core/Usage/UsageResponseParser.cs`, replace the return expression in `Parse` with:

```csharp
            return root.ValueKind != JsonValueKind.Object
                ? null
                : new UsageSnapshot(Window(root, "five_hour"), Window(root, "seven_day"), fetchedAt, Extra(root));
```

Add this method below `Window`:

```csharp
    private const string SevenDayPrefix = "seven_day_";

    /// <summary>Every <c>seven_day_*</c> object besides <c>seven_day</c>; a null window means the plan has none.</summary>
    private static Dictionary<string, UsageWindow>? Extra(JsonElement root)
    {
        Dictionary<string, UsageWindow>? extra = null;
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.StartsWith(SevenDayPrefix, StringComparison.Ordinal)
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                extra ??= new Dictionary<string, UsageWindow>(StringComparer.Ordinal);
                extra[property.Name] = new UsageWindow(Utilization(property.Value), ResetsAt(property.Value));
            }
        }

        return extra;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): read the extra seven-day limit windows"
```

---

### Task 3: Plan, scopes and the `.claude.json` facts

**Files:**
- Modify: `src/Mcacc.Core/ClaudeCode/OAuthCredentials.cs`, `CredentialsParser.cs`, `ClaudeLoginInfo.cs`, `ClaudeJsonLoginReader.cs`
- Test: `tests/Mcacc.Core.Tests/ClaudeCode/CredentialsParserTests.cs`, `tests/Mcacc.Core.Tests/ClaudeCode/ClaudeJsonLoginReaderTests.cs`

**Interfaces:**
- Produces:
  - `OAuthCredentials(string AccessToken, DateTimeOffset? ExpiresAt, string? SubscriptionType = null, IReadOnlyList<string>? Scopes = null)`. `Scopes` is null when the list is missing or empty.
  - `ClaudeLoginInfo(string Email, string? OrganizationName, string? DisplayName, string? AccountUuid = null, string? OrganizationRole = null, int? NumStartups = null, string? InstallMethod = null, int? ProjectCount = null)`

- [ ] **Step 1: Write the failing tests**

Add to `CredentialsParserTests`:

```csharp
    [Fact]
    public void Reads_the_plan_and_the_scopes()
    {
        var credentials = CredentialsParser.Parse(FixtureFiles.ReadAllText("credentials-ok.json"))!;

        credentials.SubscriptionType.ShouldBe("max");
        credentials.Scopes.ShouldBe(["user:inference", "user:profile"]);
    }

    [Fact]
    public void An_empty_scope_list_stays_null()
    {
        CredentialsParser.Parse("""{ "claudeAiOauth": { "accessToken": "t", "scopes": [] } }""")!.Scopes.ShouldBeNull();
    }

    [Fact]
    public void The_plan_shows_in_the_string_but_the_token_does_not()
    {
        var text = new OAuthCredentials("sk-ant-oat01-SECRET", null, "max").ToString();

        text.ShouldContain("max");
        text.ShouldNotContain("SECRET");
    }
```

In `ClaudeJsonLoginReaderTests.Reads_the_login_past_project_keys_that_differ_only_in_case`, replace the expected value with:

```csharp
        info.ShouldBe(new ClaudeLoginInfo(
            "max.muster@example.com",
            "Example GmbH",
            "Benjamin Biber",
            "00000000-0000-0000-0000-000000000001",
            OrganizationRole: "admin",
            NumStartups: 412,
            InstallMethod: "winget",
            ProjectCount: 3));
```

`Organization_and_display_name_are_optional` stays unchanged: all new fields are null there.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~CredentialsParserTests|FullyQualifiedName~ClaudeJsonLoginReaderTests"`
Expected: the build fails because the new members are unknown.

- [ ] **Step 3: Implement the credentials part**

Replace `src/Mcacc.Core/ClaudeCode/OAuthCredentials.cs` with:

```csharp
namespace Mcacc.Core.ClaudeCode;

/// <param name="ExpiresAt">Null when the file does not say; then the token is tried.</param>
/// <param name="SubscriptionType">The plan as Claude Code wrote it, e.g. <c>max</c>; null when missing.</param>
/// <param name="Scopes">Null when missing or empty, so two parses of the same file compare equal.</param>
public sealed record OAuthCredentials(
    string AccessToken,
    DateTimeOffset? ExpiresAt,
    string? SubscriptionType = null,
    IReadOnlyList<string>? Scopes = null)
{
    /// <summary>Keeps the token out of logs and test output; the generated record ToString would print it.</summary>
    public override string ToString() =>
        $"OAuthCredentials {{ AccessToken = <hidden>, ExpiresAt = {ExpiresAt:o}, SubscriptionType = {SubscriptionType} }}";
}
```

In `CredentialsParser.Parse`, replace `return new OAuthCredentials(accessToken, ExpiresAt(oauth));` with:

```csharp
            return new OAuthCredentials(accessToken, ExpiresAt(oauth), SubscriptionType(oauth), Scopes(oauth));
```

Add these methods to `CredentialsParser`:

```csharp
    private static string? SubscriptionType(JsonElement oauth) =>
        oauth.TryGetProperty("subscriptionType", out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static List<string>? Scopes(JsonElement oauth)
    {
        if (!oauth.TryGetProperty("scopes", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var scopes = value.EnumerateArray()
            .Where(scope => scope.ValueKind == JsonValueKind.String)
            .Select(scope => scope.GetString()!)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToList();
        return scopes.Count > 0 ? scopes : null;
    }
```

- [ ] **Step 4: Implement the `.claude.json` part**

Replace `src/Mcacc.Core/ClaudeCode/ClaudeLoginInfo.cs` with:

```csharp
namespace Mcacc.Core.ClaudeCode;

/// <param name="AccountUuid">Null when <c>.claude.json</c> does not name it; the e-mail then identifies the account.</param>
/// <param name="OrganizationRole">The role in the organization, e.g. <c>admin</c>.</param>
/// <param name="NumStartups">How often Claude Code has started with this config.</param>
/// <param name="InstallMethod">How Claude Code was installed, e.g. <c>winget</c> or <c>native</c>.</param>
/// <param name="ProjectCount">The number of folders Claude Code remembers.</param>
public sealed record ClaudeLoginInfo(
    string Email,
    string? OrganizationName,
    string? DisplayName,
    string? AccountUuid = null,
    string? OrganizationRole = null,
    int? NumStartups = null,
    string? InstallMethod = null,
    int? ProjectCount = null);
```

In `ClaudeJsonLoginReader.ReadLogin`, replace the final `return` with:

```csharp
        var email = Text(account, "emailAddress");
        return email is null
            ? null
            : new ClaudeLoginInfo(
                email,
                Text(account, "organizationName"),
                Text(account, "displayName"),
                Text(account, "accountUuid"),
                Text(account, "organizationRole"),
                Number(root, "numStartups"),
                Text(root, "installMethod"),
                root.TryGetProperty("projects", out var projects) && projects.ValueKind == JsonValueKind.Object
                    ? projects.EnumerateObject().Count()
                    : null);
```

(The line `var email = …` already exists; keep it once.) Add this method:

```csharp
    private static int? Number(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : null;
```

`EnumerateObject().Count()` counts keys that differ only in case separately. That is correct: the fixture has three entries and Claude Code keeps three.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): read the plan, the scopes and the claude code facts"
```

---

### Task 4: Limit history store

**Files:**
- Create: `src/Mcacc.Core/Usage/UsageHistoryPoint.cs`, `src/Mcacc.Core/Usage/UsageHistoryStore.cs`, `src/Mcacc.Core/Config/McaccCompactJsonContext.cs`
- Test: `tests/Mcacc.Core.Tests/Usage/UsageHistoryStoreTests.cs` (new)

**Interfaces:**
- Consumes: `McaccPaths.UsageHistoryDir`, `McaccPaths.UsageHistoryFile(id)` (Task 1), `UsageSnapshot` (Task 2).
- Produces:
  - `UsageHistoryPoint(DateTimeOffset At, double? FiveHour, DateTimeOffset? FiveHourResetsAt, double? SevenDay, DateTimeOffset? SevenDayResetsAt)` with `static UsageHistoryPoint From(UsageSnapshot snapshot)`
  - `UsageHistoryStore(McaccPaths paths, TimeProvider? time = null)` with:
    - `static TimeSpan Retention` (90 days)
    - `void Append(string accountId, UsageHistoryPoint point)`
    - `IReadOnlyList<UsageHistoryPoint> Read(string accountId, DateTimeOffset since)` (sorted by `At`)
    - `void Delete(string accountId)`
  - `McaccCompactJsonContext` (camelCase, not indented, null values left out) with `UsageHistoryPoint`. Task 7 adds `StatsIndex`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Mcacc.Core.Tests/Usage/UsageHistoryStoreTests.cs`:

```csharp
using System.Text.Json;
using Mcacc.Core.Config;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Usage;

public sealed class UsageHistoryStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(Now, TimeZoneInfo.Utc);

    public void Dispose() => _temp.Dispose();

    private McaccPaths Paths => new(
        Path.Join(_temp.Path, ".mcacc"),
        OperatingSystem.IsWindows() ? PathStyle.Windows : PathStyle.Posix);

    private string File => Paths.UsageHistoryFile("privat");

    private UsageHistoryStore Store() => new(Paths, _time);

    private static UsageHistoryPoint Point(DateTimeOffset at) => new(at, 10, null, 5, null);

    private static string Line(UsageHistoryPoint point) =>
        JsonSerializer.Serialize(point, McaccCompactJsonContext.Default.UsageHistoryPoint) + "\n";

    private void WriteLines(params UsageHistoryPoint[] points)
    {
        Directory.CreateDirectory(Paths.UsageHistoryDir);
        System.IO.File.WriteAllText(File, string.Concat(points.Select(Line)));
    }

    [Fact]
    public void Appended_points_read_back_in_time_order()
    {
        var store = Store();

        store.Append("privat", Point(Now));
        store.Append("privat", Point(Now - TimeSpan.FromHours(1)));

        store.Read("privat", DateTimeOffset.MinValue).ShouldBe([Point(Now - TimeSpan.FromHours(1)), Point(Now)]);
    }

    [Fact]
    public void Read_starts_at_since()
    {
        var store = Store();
        store.Append("privat", Point(Now - TimeSpan.FromDays(2)));
        store.Append("privat", Point(Now));

        store.Read("privat", Now - TimeSpan.FromDays(1)).ShouldBe([Point(Now)]);
    }

    [Fact]
    public void Each_point_is_one_compact_line()
    {
        var store = Store();
        store.Append("privat", Point(Now));
        store.Append("privat", Point(Now));

        var lines = System.IO.File.ReadAllText(File).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(2);
        lines.ShouldAllBe(line => line.StartsWith('{') && !line.Contains("  "));
    }

    [Fact]
    public void A_broken_line_is_skipped()
    {
        WriteLines(Point(Now - TimeSpan.FromHours(1)));
        System.IO.File.AppendAllText(File, "{\"at\": \"2026-09\n");

        Store().Read("privat", DateTimeOffset.MinValue).ShouldBe([Point(Now - TimeSpan.FromHours(1))]);
    }

    [Fact]
    public void The_first_append_of_a_day_drops_points_older_than_90_days()
    {
        WriteLines(Point(Now - TimeSpan.FromDays(91)), Point(Now - TimeSpan.FromDays(89)));

        Store().Append("privat", Point(Now));

        Store().Read("privat", DateTimeOffset.MinValue).ShouldBe([Point(Now - TimeSpan.FromDays(89)), Point(Now)]);
    }

    [Fact]
    public void Pruning_runs_at_most_once_a_day()
    {
        var store = Store();
        store.Append("privat", Point(Now));
        System.IO.File.AppendAllText(File, Line(Point(Now - TimeSpan.FromDays(100))));

        store.Append("privat", Point(Now + TimeSpan.FromMinutes(5)));
        store.Read("privat", DateTimeOffset.MinValue).Count.ShouldBe(3);

        _time.Advance(TimeSpan.FromDays(1));
        store.Append("privat", Point(Now + TimeSpan.FromDays(1)));
        store.Read("privat", DateTimeOffset.MinValue).Count.ShouldBe(3);
        store.Read("privat", DateTimeOffset.MinValue).ShouldAllBe(point => point.At >= Now);
    }

    [Fact]
    public void Delete_removes_the_history()
    {
        var store = Store();
        store.Append("privat", Point(Now));

        store.Delete("privat");

        System.IO.File.Exists(File).ShouldBeFalse();
        store.Read("privat", DateTimeOffset.MinValue).ShouldBeEmpty();
    }

    [Fact]
    public void Without_a_file_the_history_is_empty()
    {
        Store().Read("privat", DateTimeOffset.MinValue).ShouldBeEmpty();
        Store().Delete("privat");
    }

    [Fact]
    public void A_point_takes_both_windows_of_a_snapshot()
    {
        var resets = Now + TimeSpan.FromHours(2);
        var snapshot = new UsageSnapshot(new UsageWindow(42, resets), new UsageWindow(null, null), Now);

        UsageHistoryPoint.From(snapshot).ShouldBe(new UsageHistoryPoint(Now, 42, resets, null, null));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~UsageHistoryStoreTests"`
Expected: the build fails because `UsageHistoryPoint`, `UsageHistoryStore` and `McaccCompactJsonContext` are unknown.

- [ ] **Step 3: Implement**

Create `src/Mcacc.Core/Usage/UsageHistoryPoint.cs`:

```csharp
namespace Mcacc.Core.Usage;

/// <summary>One line of the limit history: the two main windows at the time of a successful refresh.</summary>
public sealed record UsageHistoryPoint(
    DateTimeOffset At,
    double? FiveHour,
    DateTimeOffset? FiveHourResetsAt,
    double? SevenDay,
    DateTimeOffset? SevenDayResetsAt)
{
    public static UsageHistoryPoint From(UsageSnapshot snapshot) => new(
        snapshot.FetchedAt,
        snapshot.FiveHour?.Utilization,
        snapshot.FiveHour?.ResetsAt,
        snapshot.SevenDay?.Utilization,
        snapshot.SevenDay?.ResetsAt);
}
```

Create `src/Mcacc.Core/Config/McaccCompactJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Config;

/// <summary>Unindented json for the files that are line-based or large: the limit history and the stats index.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UsageHistoryPoint))]
public sealed partial class McaccCompactJsonContext : JsonSerializerContext;
```

Create `src/Mcacc.Core/Usage/UsageHistoryStore.cs`:

```csharp
using System.Text.Json;
using Mcacc.Core.Config;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>
/// The limit history of each account, one json object per line. Lines are only ever appended; the first append of a
/// UTC day also drops the points older than <see cref="Retention"/>.
/// </summary>
public sealed class UsageHistoryStore(McaccPaths paths, TimeProvider? time = null)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateOnly> _prunedOn = [];

    /// <summary>A failed write is dropped: the history must never cost the caller its fresh limits.</summary>
    public void Append(string accountId, UsageHistoryPoint point)
    {
        var file = paths.UsageHistoryFile(accountId);
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(paths.UsageHistoryDir);
                PruneOncePerDay(accountId, file);
                File.AppendAllText(file, Line(point));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The next refresh appends again; a gap in the history is all that is lost.
            }
        }
    }

    public IReadOnlyList<UsageHistoryPoint> Read(string accountId, DateTimeOffset since)
    {
        lock (_gate)
        {
            return ReadAll(paths.UsageHistoryFile(accountId))
                .Where(point => point.At >= since)
                .OrderBy(point => point.At)
                .ToList();
        }
    }

    public void Delete(string accountId)
    {
        lock (_gate)
        {
            _prunedOn.Remove(accountId);
            var file = paths.UsageHistoryFile(accountId);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private void PruneOncePerDay(string accountId, string file)
    {
        var now = _time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (_prunedOn.GetValueOrDefault(accountId) == today)
        {
            return;
        }

        _prunedOn[accountId] = today;
        var all = ReadAll(file);
        var kept = all.Where(point => point.At >= now - Retention).ToList();
        if (kept.Count == all.Count)
        {
            return;
        }

        var tempFile = file + ".tmp";
        File.WriteAllText(tempFile, string.Concat(kept.Select(Line)));
        File.Move(tempFile, file, overwrite: true);
    }

    /// <summary>A broken line, e.g. from a crash mid-write, is skipped; a missing file reads as empty.</summary>
    private static List<UsageHistoryPoint> ReadAll(string file)
    {
        var points = new List<UsageHistoryPoint>();
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    if (JsonSerializer.Deserialize(line, McaccCompactJsonContext.Default.UsageHistoryPoint) is { } point)
                    {
                        points.Add(point);
                    }
                }
                catch (JsonException)
                {
                    // Skipped, see the summary.
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Whatever was read so far stands.
        }

        return points;
    }

    private static string Line(UsageHistoryPoint point) =>
        JsonSerializer.Serialize(point, McaccCompactJsonContext.Default.UsageHistoryPoint) + "\n";
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~UsageHistoryStoreTests"`
Expected: PASS (9 tests).

- [ ] **Step 5: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): keep a 90-day limit history per account"
```

---

### Task 5: `UsageService` stores the profile and the history

**Files:**
- Modify: `src/Mcacc.Core/Abstractions/IUsageProvider.cs`, `src/Mcacc.Core/Usage/UsageService.cs`
- Modify: `src/Mcacc.App/CompositionRoot.cs` (constructor call)
- Modify: `tests/Mcacc.App.Tests/TestSupport/Fakes.cs` (`FakeUsageProvider`)
- Test: `tests/Mcacc.Core.Tests/Usage/UsageServiceTests.cs`

**Interfaces:**
- Consumes: `ProfileCache`, `AccountProfile`, `ProfileParser` (Task 1), `UsageHistoryStore`, `UsageHistoryPoint` (Task 4).
- Produces:
  - `IUsageProvider.GetProfile(Account) : AccountProfile?`, which reads from the cache and does no IO after the first load
  - `IUsageProvider.GetHistory(Account, DateTimeOffset since) : IReadOnlyList<UsageHistoryPoint>`
  - `IUsageProvider.Forget(Account)`, which drops the limits, profile, history and remembered answers of a removed account
  - the constructor `UsageService(ICredentialSource credentials, ILoginInfoReader login, IUsageHttp http, UsageCache cache, ProfileCache profiles, UsageHistoryStore history, AccountPathResolver resolver, TimeProvider? time = null)`
  - `FakeUsageProvider.Profiles` (`Dictionary<string, AccountProfile>`), `.History` (`Dictionary<string, List<UsageHistoryPoint>>`) and `.Forgotten` (`List<string>`)

Behaviour:
- A refresh returning `Ok` appends one history point.
- An own token with a verified owner stores the profile, but only when it changed.
- On `WrongAccount` only the limits are dropped. The history and the profile describe this account, not the stray token, so they stay.

- [ ] **Step 1: Write the failing tests**

In `tests/Mcacc.Core.Tests/Usage/UsageServiceTests.cs`, replace the `Service()` member with the following. Add the two helpers above it:

```csharp
    private ProfileCache Profiles => new(Paths);

    private UsageHistoryStore History => new(Paths, _time);

    private UsageService Service() =>
        new(_credentials, _login, _http, Cache, Profiles, History, new AccountPathResolver(Paths, Platform), _time);
```

Append these tests before the nested fake classes:

```csharp
    [Fact]
    public async Task A_fresh_snapshot_is_appended_to_the_history()
    {
        await Service().RefreshAsync(Privat, Ct);

        History.Read("privat", DateTimeOffset.MinValue).ShouldBe([UsageHistoryPoint.From(Fresh(Now))]);
    }

    [Fact]
    public async Task A_failed_request_adds_nothing_to_the_history()
    {
        _http.Response = new UsageHttpResponse(500, null);

        await Service().RefreshAsync(Privat, Ct);

        History.Read("privat", DateTimeOffset.MinValue).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_profile_of_the_own_token_is_cached()
    {
        _login.Info = OwnLogin;

        await Service().RefreshAsync(Privat, Ct);

        Service().GetProfile(Privat).ShouldBe(ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json")));
    }

    [Fact]
    public void Without_a_cached_profile_there_is_none()
    {
        Service().GetProfile(Privat).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_of_another_account_keeps_the_history_and_the_profile()
    {
        var own = ProfileParser.Parse(FixtureFiles.ReadAllText("profile-ok.json"))!;
        Profiles.Save(new Dictionary<string, AccountProfile> { ["privat"] = own });
        History.Append("privat", UsageHistoryPoint.From(Old));
        _login.Info = OwnLogin;
        _http.ProfileResponse = new UsageHttpResponse(200, OtherProfile);

        (await Service().RefreshAsync(Privat, Ct)).State.ShouldBe(UsageState.WrongAccount);

        Service().GetProfile(Privat).ShouldBe(own);
        History.Read("privat", DateTimeOffset.MinValue).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Forget_drops_the_limits_the_profile_and_the_history()
    {
        _login.Info = OwnLogin;
        var service = Service();
        await service.RefreshAsync(Privat, Ct);

        service.Forget(Privat);

        service.GetCached(Privat).State.ShouldBe(UsageState.NotLoggedIn);
        service.GetProfile(Privat).ShouldBeNull();
        Cache.Load().ShouldBeEmpty();
        Profiles.Load().ShouldBeEmpty();
        History.Read("privat", DateTimeOffset.MinValue).ShouldBeEmpty();
    }

    [Fact]
    public async Task After_forget_the_next_refresh_asks_again()
    {
        var service = Service();
        await service.RefreshAsync(Privat, Ct);

        service.Forget(Privat);
        await service.RefreshAsync(Privat, Ct);

        _http.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetHistory_returns_the_points_since_the_given_time()
    {
        History.Append("privat", UsageHistoryPoint.From(Old));

        await Service().RefreshAsync(Privat, Ct);

        Service().GetHistory(Privat, Now - TimeSpan.FromHours(1)).ShouldBe([UsageHistoryPoint.From(Fresh(Now))]);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~UsageServiceTests"`
Expected: the build fails. The constructor takes no `ProfileCache`, and `GetProfile`, `GetHistory` and `Forget` do not exist.

- [ ] **Step 3: Extend the interface**

Replace `src/Mcacc.Core/Abstractions/IUsageProvider.cs` with:

```csharp
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.Core.Abstractions;

public interface IUsageProvider
{
    /// <summary>The last known value, without any IO.</summary>
    AccountUsage GetCached(Account account);

    Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct);

    /// <summary>The last profile the endpoint reported for the account's own token; null when none is known.</summary>
    AccountProfile? GetProfile(Account account);

    /// <summary>The history points from <paramref name="since"/> on, oldest first.</summary>
    IReadOnlyList<UsageHistoryPoint> GetHistory(Account account, DateTimeOffset since);

    /// <summary>For a removed account: drops its limits, profile and history.</summary>
    void Forget(Account account);
}
```

- [ ] **Step 4: Rewrite `UsageService`**

Replace `src/Mcacc.Core/Usage/UsageService.cs` with this complete file. The unchanged parts are copied from the current file.

```csharp
using System.Security.Cryptography;
using System.Text;
using Mcacc.Core.Abstractions;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Usage;

/// <summary>
/// Reads the usage limits of an account with its Claude Code token. The token is never refreshed and never stored;
/// an expired one simply leaves the last cached value standing. A token of another account than the one
/// <c>.claude.json</c> names shows no limits at all, so a tile never carries someone else's numbers.
/// Every fresh value also goes into the history, and a verified profile into the profile cache.
/// </summary>
public sealed class UsageService(
    ICredentialSource credentials,
    ILoginInfoReader login,
    IUsageHttp http,
    UsageCache cache,
    ProfileCache profiles,
    UsageHistoryStore history,
    AccountPathResolver resolver,
    TimeProvider? time = null) : IUsageProvider
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(60);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Attempt> _attempts = [];
    private readonly Dictionary<string, CheckedToken> _owners = [];
    private Dictionary<string, UsageSnapshot>? _snapshots;
    private Dictionary<string, AccountProfile>? _profiles;

    public AccountUsage GetCached(Account account) =>
        Cached(account.Id) is { } snapshot
            ? new AccountUsage(UsageState.Ok, snapshot)
            : new AccountUsage(UsageState.NotLoggedIn, null);

    public AccountProfile? GetProfile(Account account)
    {
        lock (_gate)
        {
            return Profiles().GetValueOrDefault(account.Id);
        }
    }

    public IReadOnlyList<UsageHistoryPoint> GetHistory(Account account, DateTimeOffset since) =>
        history.Read(account.Id, since);

    public void Forget(Account account)
    {
        lock (_gate)
        {
            _attempts.Remove(account.Id);
            _owners.Remove(account.Id);
            var known = Profiles();
            if (known.Remove(account.Id))
            {
                profiles.Save(known);
            }
        }

        ForgetLimits(account.Id);
        history.Delete(account.Id);
    }

    /// <summary>Asks the endpoint at most once per minute and account; in between it repeats the last result.</summary>
    public async Task<AccountUsage> RefreshAsync(Account account, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (_attempts.TryGetValue(account.Id, out var last) && now - last.At < MinimumInterval)
            {
                return last.Result;
            }
        }

        var usage = await FetchAsync(account, now, ct).ConfigureAwait(false);
        lock (_gate)
        {
            _attempts[account.Id] = new Attempt(now, usage);
        }

        return usage;
    }

    private async Task<AccountUsage> FetchAsync(Account account, DateTimeOffset now, CancellationToken ct)
    {
        var cached = Cached(account.Id);
        var paths = resolver.Resolve(account);
        var read = await credentials.ReadAsync(account, paths, ct).ConfigureAwait(false);
        if (read is not { Status: CredentialReadStatus.Found, Credentials: { } found })
        {
            return new AccountUsage(
                read.Status == CredentialReadStatus.NotLoggedIn ? UsageState.NotLoggedIn : UsageState.Error,
                cached);
        }

        if (found.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return new AccountUsage(UsageState.Expired, cached);
        }

        // Without a login in .claude.json there is nothing to compare with, and the tile says so anyway.
        if (login.Read(paths) is { } expected)
        {
            var check = await ProfileAsync(account.Id, found.AccessToken, ct).ConfigureAwait(false);
            if (check.Profile is not { } profile)
            {
                return new AccountUsage(check.State, cached);
            }

            var owner = profile.ToOwner();
            if (!IsSameAccount(expected, owner))
            {
                // The history and the profile stay: they describe this account, not the stray token.
                ForgetLimits(account.Id);
                return new AccountUsage(UsageState.WrongAccount, null, owner.Email);
            }

            StoreProfile(account.Id, profile);
        }

        var (state, body) = await GetAsync(() => http.GetUsageAsync(found.AccessToken, ct), ct).ConfigureAwait(false);
        if (state != UsageState.Ok)
        {
            return new AccountUsage(state, cached);
        }

        if (body is null || UsageResponseParser.Parse(body, now) is not { } snapshot)
        {
            return new AccountUsage(UsageState.Error, cached);
        }

        Store(account.Id, snapshot);
        return new AccountUsage(UsageState.Ok, snapshot);
    }

    /// <summary>Asks the profile endpoint once per token; only an answer that names the owner is remembered.</summary>
    /// <returns>The profile with <see cref="UsageState.Ok"/>, otherwise no profile and the state of the failure.</returns>
    private async Task<(UsageState State, AccountProfile? Profile)> ProfileAsync(
        string accountId,
        string accessToken,
        CancellationToken ct)
    {
        var tokenHash = Hash(accessToken);
        lock (_gate)
        {
            if (_owners.TryGetValue(accountId, out var known) && known.TokenHash == tokenHash)
            {
                return (UsageState.Ok, known.Profile);
            }
        }

        var (state, body) = await GetAsync(() => http.GetProfileAsync(accessToken, ct), ct).ConfigureAwait(false);
        if (state != UsageState.Ok)
        {
            return (state, null);
        }

        if (body is null || ProfileParser.Parse(body) is not { } profile)
        {
            return (UsageState.Error, null);
        }

        lock (_gate)
        {
            _owners[accountId] = new CheckedToken(tokenHash, profile);
        }

        return (UsageState.Ok, profile);
    }

    /// <summary>A request that does not reach the endpoint counts as an error; a canceled one still throws.</summary>
    private static async Task<(UsageState State, string? Body)> GetAsync(
        Func<Task<UsageHttpResponse>> request,
        CancellationToken ct)
    {
        UsageHttpResponse response;
        try
        {
            response = await request().ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return (UsageState.Error, null);
        }

        var state = response.StatusCode switch
        {
            401 or 403 => UsageState.Unauthorized,
            429 => UsageState.RateLimited,
            200 => UsageState.Ok,
            _ => UsageState.Error,
        };
        return (state, response.Body);
    }

    /// <summary>The uuid decides when both sides have one; older files only carry the e-mail.</summary>
    private static bool IsSameAccount(ClaudeLoginInfo expected, TokenOwner owner) =>
        expected.AccountUuid is { } uuid && owner.AccountUuid is { } ownerUuid
            ? string.Equals(uuid, ownerUuid, StringComparison.OrdinalIgnoreCase)
            : string.Equals(expected.Email, owner.Email, StringComparison.OrdinalIgnoreCase);

    /// <summary>Only the hash is remembered, so the token itself does not outlive the refresh.</summary>
    private static string Hash(string accessToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));

    private UsageSnapshot? Cached(string id)
    {
        lock (_gate)
        {
            return Snapshots().GetValueOrDefault(id);
        }
    }

    private void Store(string id, UsageSnapshot snapshot)
    {
        lock (_gate)
        {
            var snapshots = Snapshots();
            snapshots[id] = snapshot;
            cache.Save(snapshots);
        }

        history.Append(id, UsageHistoryPoint.From(snapshot));
    }

    /// <summary>Writes the file only when the profile changed, since every refresh of a known token passes here.</summary>
    private void StoreProfile(string id, AccountProfile profile)
    {
        lock (_gate)
        {
            var known = Profiles();
            if (Equals(known.GetValueOrDefault(id), profile))
            {
                return;
            }

            known[id] = profile;
            profiles.Save(known);
        }
    }

    private void ForgetLimits(string id)
    {
        lock (_gate)
        {
            var snapshots = Snapshots();
            if (snapshots.Remove(id))
            {
                cache.Save(snapshots);
            }
        }
    }

    /// <summary>Call under <see cref="_gate"/>: the cache file is read once and written from here on.</summary>
    private Dictionary<string, UsageSnapshot> Snapshots() => _snapshots ??= new Dictionary<string, UsageSnapshot>(cache.Load());

    /// <summary>Call under <see cref="_gate"/>, like <see cref="Snapshots"/>.</summary>
    private Dictionary<string, AccountProfile> Profiles() => _profiles ??= new Dictionary<string, AccountProfile>(profiles.Load());

    private sealed record Attempt(DateTimeOffset At, AccountUsage Result);

    private sealed record CheckedToken(string TokenHash, AccountProfile Profile);
}
```

- [ ] **Step 5: Update the composition root and the app fake**

In `src/Mcacc.App/CompositionRoot.cs`, replace

```csharp
            new UsageService(platform.Credentials, login, platform.UsageHttp, new UsageCache(platform.Paths), resolver),
```

with

```csharp
            new UsageService(
                platform.Credentials,
                login,
                platform.UsageHttp,
                new UsageCache(platform.Paths),
                new ProfileCache(platform.Paths),
                new UsageHistoryStore(platform.Paths),
                resolver),
```

In `tests/Mcacc.App.Tests/TestSupport/Fakes.cs`, add to `FakeUsageProvider`:

```csharp
    public Dictionary<string, AccountProfile> Profiles { get; } = [];

    public Dictionary<string, List<UsageHistoryPoint>> History { get; } = [];

    public List<string> Forgotten { get; } = [];

    public AccountProfile? GetProfile(Account account) => Profiles.GetValueOrDefault(account.Id);

    public IReadOnlyList<UsageHistoryPoint> GetHistory(Account account, DateTimeOffset since) =>
        History.TryGetValue(account.Id, out var points) ? points.Where(point => point.At >= since).ToList() : [];

    public void Forget(Account account) => Forgotten.Add(account.Id);
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet build MCACC.slnx` and then `dotnet test tests/Mcacc.Core.Tests`
Expected: the build succeeds and the tests PASS, including the existing `A_token_of_another_account_shows_no_limits_and_forgets_the_cached_ones`.

- [ ] **Step 7: Commit**

```bash
git add -A src tests
git commit -F <message-file>   # "feat(core): record the limit history and the profile on every refresh"
```

---

### Task 6: Stats models and the log line parser

**Files:**
- Create: `src/Mcacc.Core/Stats/TokenCounts.cs`, `src/Mcacc.Core/Stats/TokenBucket.cs`, `src/Mcacc.Core/Stats/LogUsage.cs`, `src/Mcacc.Core/Stats/LogLineParser.cs`
- Create: `tests/Mcacc.Core.Tests/TestSupport/LogLines.cs`
- Test: `tests/Mcacc.Core.Tests/Stats/LogLineParserTests.cs` (new)

**Interfaces:**
- Produces:
  - `TokenCounts(long Input, long Output, long CacheWrite5Min, long CacheWrite1Hour, long CacheRead)` with `Zero`, the `[JsonIgnore]` properties `Total` and `CacheWrite`, and `operator +`
  - `TokenBucket(DateTimeOffset Hour, string Model, string Project, string Session, TokenCounts Tokens, int Messages)`
  - `LogUsage(long Id, DateTimeOffset Timestamp, string Model, string Project, string Session, TokenCounts Tokens)`
  - `LogLineParser.TryParse(ReadOnlyMemory<byte> line, out LogUsage? usage) : bool`
  - `LogLineParser.Hash(string text) : long`
  - test helpers `LogLines.Assistant(...)` and `LogLines.User()`

A log line looks like this (shortened):

```json
{"type":"assistant","timestamp":"2026-09-20T10:15:00.000Z","cwd":"C:\\Code\\Mcacc","sessionId":"s1","requestId":"req_1",
 "message":{"id":"msg_1","model":"claude-opus-4-5","usage":{"input_tokens":10,"output_tokens":20,"cache_read_input_tokens":0,
 "cache_creation_input_tokens":0,"cache_creation":{"ephemeral_5m_input_tokens":0,"ephemeral_1h_input_tokens":0}}}}
```

- [ ] **Step 1: Create the test helper**

Create `tests/Mcacc.Core.Tests/TestSupport/LogLines.cs`:

```csharp
using System.Text.Json.Nodes;

namespace Mcacc.Core.Tests.TestSupport;

/// <summary>Lines of a Claude Code session log, built as json so paths with backslashes stay valid.</summary>
internal static class LogLines
{
    public static string Assistant(
        string id,
        string requestId = "req_1",
        string timestamp = "2026-09-20T10:15:00.000Z",
        string model = "claude-opus-4-5",
        string cwd = @"C:\Code\Mcacc",
        string session = "s1",
        long input = 10,
        long output = 20,
        long cacheRead = 0,
        long cache5m = 0,
        long cache1h = 0,
        long? legacyCacheWrite = null)
    {
        var usage = new JsonObject
        {
            ["input_tokens"] = input,
            ["output_tokens"] = output,
            ["cache_read_input_tokens"] = cacheRead,
        };
        if (legacyCacheWrite is { } legacy)
        {
            usage["cache_creation_input_tokens"] = legacy;
        }
        else
        {
            usage["cache_creation_input_tokens"] = cache5m + cache1h;
            usage["cache_creation"] = new JsonObject
            {
                ["ephemeral_5m_input_tokens"] = cache5m,
                ["ephemeral_1h_input_tokens"] = cache1h,
            };
        }

        return new JsonObject
        {
            ["type"] = "assistant",
            ["timestamp"] = timestamp,
            ["cwd"] = cwd,
            ["sessionId"] = session,
            ["requestId"] = requestId,
            ["message"] = new JsonObject
            {
                ["id"] = id,
                ["model"] = model,
                ["role"] = "assistant",
                ["usage"] = usage,
            },
        }.ToJsonString();
    }

    public static string User(string text = "hallo") => new JsonObject
    {
        ["type"] = "user",
        ["timestamp"] = "2026-09-20T10:14:00.000Z",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = text },
    }.ToJsonString();
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Mcacc.Core.Tests/Stats/LogLineParserTests.cs`:

```csharp
using System.Text;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public class LogLineParserTests
{
    private static LogUsage? Parse(string line)
    {
        LogLineParser.TryParse(Encoding.UTF8.GetBytes(line), out var usage).ShouldBeTrue();
        return usage;
    }

    [Fact]
    public void Reads_the_tokens_the_model_the_project_the_session_and_the_time()
    {
        var usage = Parse(LogLines.Assistant("msg_1", input: 10, output: 20, cacheRead: 300))!;

        usage.Tokens.ShouldBe(new TokenCounts(10, 20, 0, 0, 300));
        usage.Model.ShouldBe("claude-opus-4-5");
        usage.Project.ShouldBe(@"C:\Code\Mcacc");
        usage.Session.ShouldBe("s1");
        usage.Timestamp.ShouldBe(new DateTimeOffset(2026, 9, 20, 10, 15, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Splits_the_cache_writes_by_lifetime()
    {
        Parse(LogLines.Assistant("msg_1", cache5m: 100, cache1h: 50))!.Tokens
            .ShouldBe(new TokenCounts(10, 20, 100, 50, 0));
    }

    [Fact]
    public void Older_logs_count_the_whole_cache_write_as_5_minutes()
    {
        Parse(LogLines.Assistant("msg_1", legacyCacheWrite: 70))!.Tokens.ShouldBe(new TokenCounts(10, 20, 70, 0, 0));
    }

    [Theory]
    [InlineData("""{"type":"user","message":{"role":"user","content":"hallo"}}""")]
    [InlineData("not json and no marker")]
    [InlineData("")]
    public void A_line_without_usage_holds_none(string line)
    {
        Parse(line).ShouldBeNull();
    }

    [Fact]
    public void A_user_line_that_mentions_usage_holds_none()
    {
        Parse(LogLines.User("what is \"usage\"?")).ShouldBeNull();
    }

    [Fact]
    public void A_cut_off_line_with_usage_is_broken()
    {
        var line = Encoding.UTF8.GetBytes("""{"type":"assistant","message":{"usage":{"input_tokens":1""");

        LogLineParser.TryParse(line, out var usage).ShouldBeFalse();
        usage.ShouldBeNull();
    }

    [Fact]
    public void The_same_response_gets_the_same_id()
    {
        var first = Parse(LogLines.Assistant("msg_1", "req_1"))!;

        Parse(LogLines.Assistant("msg_1", "req_1", output: 99))!.Id.ShouldBe(first.Id);
        Parse(LogLines.Assistant("msg_1", "req_2"))!.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public void A_response_without_tokens_is_skipped()
    {
        Parse(LogLines.Assistant("msg_1", model: "<synthetic>", input: 0, output: 0)).ShouldBeNull();
    }

    [Fact]
    public void A_line_without_a_message_id_or_time_is_skipped()
    {
        Parse("""{"type":"assistant","timestamp":"2026-09-20T10:15:00Z","message":{"usage":{"input_tokens":5}}}""")
            .ShouldBeNull();
        Parse("""{"type":"assistant","message":{"id":"m","usage":{"input_tokens":5}}}""").ShouldBeNull();
    }

    [Fact]
    public void The_hash_is_stable()
    {
        LogLineParser.Hash("msg_1|req_1").ShouldBe(LogLineParser.Hash("msg_1|req_1"));
        LogLineParser.Hash("msg_1|req_1").ShouldNotBe(LogLineParser.Hash("msg_1|req_2"));
    }

    [Fact]
    public void Token_counts_add_up()
    {
        var sum = new TokenCounts(1, 2, 3, 4, 5) + new TokenCounts(10, 20, 30, 40, 50);

        sum.ShouldBe(new TokenCounts(11, 22, 33, 44, 55));
        sum.Total.ShouldBe(165);
        sum.CacheWrite.ShouldBe(77);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~LogLineParserTests"`
Expected: the build fails because the `Mcacc.Core.Stats` namespace does not exist.

- [ ] **Step 4: Implement the models**

Create `src/Mcacc.Core/Stats/TokenCounts.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Mcacc.Core.Stats;

/// <summary>
/// The tokens of one response or the sum of many. Cache writes are split by their lifetime because the two are
/// priced differently.
/// </summary>
public sealed record TokenCounts(long Input, long Output, long CacheWrite5Min, long CacheWrite1Hour, long CacheRead)
{
    public static readonly TokenCounts Zero = new(0, 0, 0, 0, 0);

    [JsonIgnore]
    public long Total => Input + Output + CacheWrite5Min + CacheWrite1Hour + CacheRead;

    [JsonIgnore]
    public long CacheWrite => CacheWrite5Min + CacheWrite1Hour;

    public static TokenCounts operator +(TokenCounts left, TokenCounts right) => new(
        left.Input + right.Input,
        left.Output + right.Output,
        left.CacheWrite5Min + right.CacheWrite5Min,
        left.CacheWrite1Hour + right.CacheWrite1Hour,
        left.CacheRead + right.CacheRead);
}
```

Create `src/Mcacc.Core/Stats/TokenBucket.cs`:

```csharp
namespace Mcacc.Core.Stats;

/// <summary>
/// The tokens of one UTC hour, one model, one project and one session. Hours rather than days let the query cut the
/// days in local time.
/// </summary>
/// <param name="Project">The working directory of the session; empty when the log does not say.</param>
public sealed record TokenBucket(
    DateTimeOffset Hour,
    string Model,
    string Project,
    string Session,
    TokenCounts Tokens,
    int Messages);
```

Create `src/Mcacc.Core/Stats/LogUsage.cs`:

```csharp
namespace Mcacc.Core.Stats;

/// <summary>The usage of one assistant response in a session log.</summary>
/// <param name="Id">
/// A hash of <c>message.id|requestId</c>. Claude Code logs a response once per content block, each time with the same
/// usage, so only the first line with an id counts.
/// </param>
public sealed record LogUsage(
    long Id,
    DateTimeOffset Timestamp,
    string Model,
    string Project,
    string Session,
    TokenCounts Tokens);
```

- [ ] **Step 5: Implement the parser**

Create `src/Mcacc.Core/Stats/LogLineParser.cs`:

```csharp
using System.Globalization;
using System.Text.Json;

namespace Mcacc.Core.Stats;

/// <summary>Reads one line of a Claude Code session log; only assistant lines with a usage block count.</summary>
public static class LogLineParser
{
    private static ReadOnlySpan<byte> UsageMarker => "\"usage\""u8;

    /// <param name="usage">The usage of the line, or null when it holds none.</param>
    /// <returns>False only for a line that mentions usage but is not valid json, e.g. one cut off mid-write.</returns>
    public static bool TryParse(ReadOnlyMemory<byte> line, out LogUsage? usage)
    {
        usage = null;

        // Most lines are user turns and tool results; they are skipped without parsing.
        if (line.Span.IndexOf(UsageMarker) < 0)
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            usage = Read(document.RootElement);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>FNV-1a over the UTF-16 code units: stable across runs, unlike <see cref="string.GetHashCode()"/>.</summary>
    public static long Hash(string text)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offsetBasis;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= prime;
        }

        return unchecked((long)hash);
    }

    private static LogUsage? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || Text(root, "type") != "assistant"
            || !root.TryGetProperty("message", out var message)
            || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("usage", out var usage)
            || usage.ValueKind != JsonValueKind.Object
            || Text(message, "id") is not { } messageId
            || Timestamp(root) is not { } timestamp)
        {
            return null;
        }

        // Synthetic messages (model "<synthetic>") carry zeros; they would only add a model without tokens.
        var tokens = Tokens(usage);
        if (tokens.Total == 0)
        {
            return null;
        }

        return new LogUsage(
            Hash($"{messageId}|{Text(root, "requestId")}"),
            timestamp,
            Text(message, "model") ?? "unknown",
            Text(root, "cwd") ?? "",
            Text(root, "sessionId") ?? "",
            tokens);
    }

    private static TokenCounts Tokens(JsonElement usage)
    {
        long cache5Min;
        long cache1Hour;
        if (usage.TryGetProperty("cache_creation", out var creation)
            && creation.ValueKind == JsonValueKind.Object
            && (creation.TryGetProperty("ephemeral_5m_input_tokens", out _)
                || creation.TryGetProperty("ephemeral_1h_input_tokens", out _)))
        {
            cache5Min = Number(creation, "ephemeral_5m_input_tokens");
            cache1Hour = Number(creation, "ephemeral_1h_input_tokens");
        }
        else
        {
            // Older logs have no split; five minutes was the only lifetime then.
            cache5Min = Number(usage, "cache_creation_input_tokens");
            cache1Hour = 0;
        }

        return new TokenCounts(
            Number(usage, "input_tokens"),
            Number(usage, "output_tokens"),
            cache5Min,
            cache1Hour,
            Number(usage, "cache_read_input_tokens"));
    }

    private static long Number(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
        && number > 0
            ? number
            : 0;

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static DateTimeOffset? Timestamp(JsonElement root) =>
        Text(root, "timestamp") is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~LogLineParserTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): parse the usage lines of the claude code session logs"
```

---

### Task 7: Stats index and the incremental log scanner

**Files:**
- Create: `src/Mcacc.Core/Stats/StatsIndex.cs`, `src/Mcacc.Core/Stats/StatsIndexStore.cs`, `src/Mcacc.Core/Stats/ClaudeLogScanner.cs`
- Modify: `src/Mcacc.Core/Config/McaccCompactJsonContext.cs` (+ `StatsIndex`)
- Test: `tests/Mcacc.Core.Tests/Stats/ClaudeLogScannerTests.cs`, `tests/Mcacc.Core.Tests/Stats/StatsIndexStoreTests.cs` (new)

**Interfaces:**
- Consumes:
  - `LogLineParser`, `LogUsage`, `TokenBucket`, `TokenCounts` (Task 6)
  - `JsonFile` and `McaccPaths.StatsFile(id)` (Task 1)
  - `McaccCompactJsonContext` (Task 4)
- Produces:
  - `StatsIndex(int Version, IReadOnlyList<IndexedLogFile> Files)` with `const int CurrentVersion = 1` and `static StatsIndex Empty`
  - `IndexedLogFile(string Path, long Length, DateTime LastWriteUtc, long Offset, IReadOnlyList<TokenBucket> Buckets, IReadOnlyList<long> Ids, int BrokenLines)`. `Path` is relative to `<claudeDir>/projects`.
  - `LogScanResult(StatsIndex Index, bool Changed)`
  - `ClaudeLogScanner.Update(string claudeDir, StatsIndex index, IProgress<double>? progress, CancellationToken ct) : LogScanResult`
  - `StatsIndexStore(McaccPaths paths)` with `Load(string accountId) : StatsIndex` (Empty when missing or of another version), `Save(string accountId, StatsIndex index)` and `Delete(string accountId)`

Rules the scanner follows:
- A file whose length and write time match its entry is not opened.
- A file that grew is read on from `Offset`. Only complete lines count: `Offset` always stands right after the last `\n`, so a line still being written is read on the next scan.
- A file that shrank is read again from the start.
- **A deleted file keeps its entry.** Claude Code deletes transcripts after `cleanupPeriodDays` (30 days by default), and the 90-day view still needs them. This refines the spec, which said "Datei verschwunden: Eintrag entfernt".
- A response counts once across all files. Claude Code logs it once per content block, and a resumed session copies earlier lines.
- A file that cannot be read keeps its old entry and is tried again on the next scan.

- [ ] **Step 1: Write the failing scanner tests**

Create `tests/Mcacc.Core.Tests/Stats/ClaudeLogScannerTests.cs`:

```csharp
using System.Text;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public sealed class ClaudeLogScannerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Claude => Path.Join(_temp.Path, ".claude");

    private string Log(string relative, params string[] lines)
    {
        var file = Path.Join(Claude, "projects", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Concat(lines.Select(line => line + "\n")));
        return file;
    }

    private LogScanResult Scan(StatsIndex? index = null, IProgress<double>? progress = null) =>
        ClaudeLogScanner.Update(Claude, index ?? StatsIndex.Empty, progress, Ct);

    private static IEnumerable<TokenBucket> Buckets(StatsIndex index) => index.Files.SelectMany(file => file.Buckets);

    private static TokenCounts Sum(StatsIndex index) =>
        Buckets(index).Aggregate(TokenCounts.Zero, (sum, bucket) => sum + bucket.Tokens);

    private static int Messages(StatsIndex index) => Buckets(index).Sum(bucket => bucket.Messages);

    [Fact]
    public void A_response_logged_once_per_content_block_counts_once()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"), LogLines.Assistant("msg_1"), LogLines.Assistant("msg_2"));

        var index = Scan().Index;

        Messages(index).ShouldBe(2);
        Sum(index).Input.ShouldBe(20);
    }

    [Fact]
    public void A_response_copied_into_another_session_counts_once()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        Log("p1/s2.jsonl", LogLines.Assistant("msg_1"));

        Messages(Scan().Index).ShouldBe(1);
    }

    [Fact]
    public void The_subagent_logs_count()
    {
        Log(Path.Join("p1", "s1", "subagents", "agent-a1.jsonl"), LogLines.Assistant("msg_1"));

        var index = Scan().Index;

        Messages(index).ShouldBe(1);
        index.Files.ShouldHaveSingleItem().Path.ShouldBe(Path.Join("p1", "s1", "subagents", "agent-a1.jsonl"));
    }

    [Fact]
    public void Broken_lines_are_skipped_and_counted()
    {
        Log(
            "p1/s1.jsonl",
            LogLines.Assistant("msg_1"),
            """{"type":"assistant","message":{"usage":{""",
            LogLines.User());

        var file = Scan().Index.Files.ShouldHaveSingleItem();

        file.Buckets.Sum(bucket => bucket.Messages).ShouldBe(1);
        file.BrokenLines.ShouldBe(1);
    }

    [Fact]
    public void A_line_still_being_written_is_read_on_the_next_scan()
    {
        var first = LogLines.Assistant("msg_1");
        var second = LogLines.Assistant("msg_2");
        var file = Log("p1/s1.jsonl", first);
        File.AppendAllText(file, second[..20]);

        var before = Scan().Index;
        before.Files.ShouldHaveSingleItem().Offset.ShouldBe(Encoding.UTF8.GetByteCount(first) + 1);
        Messages(before).ShouldBe(1);

        File.AppendAllText(file, second[20..] + "\n");

        Messages(Scan(before).Index).ShouldBe(2);
    }

    [Fact]
    public void A_grown_file_is_read_on_from_the_last_offset()
    {
        var file = Log("p1/s1.jsonl", LogLines.Assistant("msg_1", output: 20));
        var before = Scan().Index;

        // Same length, other digits: had the scanner started over, it would count 29 instead of 20.
        File.WriteAllText(
            file,
            LogLines.Assistant("msg_1", output: 29) + "\n" + LogLines.Assistant("msg_2", output: 5) + "\n");

        Sum(Scan(before).Index).Output.ShouldBe(25);
    }

    [Fact]
    public void Unchanged_logs_leave_the_index_as_it_is()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        var before = Scan().Index;

        var result = Scan(before);

        result.Changed.ShouldBeFalse();
        result.Index.ShouldBeSameAs(before);
    }

    [Fact]
    public void A_shrunk_file_is_read_from_the_start()
    {
        var file = Log("p1/s1.jsonl", LogLines.Assistant("msg_1"), LogLines.Assistant("msg_2"));
        var before = Scan().Index;

        File.WriteAllText(file, LogLines.Assistant("msg_3", input: 7) + "\n");
        var index = Scan(before).Index;

        Messages(index).ShouldBe(1);
        Sum(index).Input.ShouldBe(7);
    }

    [Fact]
    public void A_deleted_log_keeps_its_counts()
    {
        var file = Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        var before = Scan().Index;

        File.Delete(file);
        var result = Scan(before);

        result.Changed.ShouldBeFalse();
        Messages(result.Index).ShouldBe(1);
    }

    [Fact]
    public void Without_a_projects_folder_nothing_is_found()
    {
        var result = Scan();

        result.Changed.ShouldBeFalse();
        result.Index.Files.ShouldBeEmpty();
    }

    [Fact]
    public void Buckets_are_cut_per_utc_hour_model_project_and_session()
    {
        Log(
            "p1/s1.jsonl",
            LogLines.Assistant("msg_1", timestamp: "2026-09-20T10:15:00.000Z"),
            LogLines.Assistant("msg_2", timestamp: "2026-09-20T10:45:00.000Z"),
            LogLines.Assistant("msg_3", timestamp: "2026-09-20T11:05:00.000Z"),
            LogLines.Assistant("msg_4", timestamp: "2026-09-20T10:20:00.000Z", model: "claude-sonnet-4-5"),
            LogLines.Assistant("msg_5", timestamp: "2026-09-20T10:25:00.000Z", session: "s2"));

        var buckets = Scan().Index.Files.ShouldHaveSingleItem().Buckets;

        buckets.Count.ShouldBe(4);
        buckets[0].ShouldBe(new TokenBucket(
            new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
            "claude-opus-4-5",
            @"C:\Code\Mcacc",
            "s1",
            new TokenCounts(20, 40, 0, 0, 0),
            2));
    }

    [Fact]
    public void Progress_ends_at_one()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        var progress = new RecordingProgress();

        Scan(progress: progress);

        progress.Values.ShouldNotBeEmpty();
        progress.Values.ShouldAllBe(value => value >= 0 && value <= 1);
        progress.Values[^1].ShouldBe(1);
    }

    [Fact]
    public void An_index_of_another_version_is_rebuilt()
    {
        Log("p1/s1.jsonl", LogLines.Assistant("msg_1"));
        var stale = new StatsIndex(0, [new IndexedLogFile("ghost.jsonl", 1, DateTime.UnixEpoch, 1, [], [42], 0)]);

        var result = Scan(stale);

        result.Changed.ShouldBeTrue();
        result.Index.Version.ShouldBe(StatsIndex.CurrentVersion);
        result.Index.Files.ShouldHaveSingleItem().Path.ShouldBe(Path.Join("p1", "s1.jsonl"));
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }
}
```

- [ ] **Step 2: Write the failing store tests**

Create `tests/Mcacc.Core.Tests/Stats/StatsIndexStoreTests.cs`:

```csharp
using System.Text.Json;
using Mcacc.Core.Config;
using Mcacc.Core.Paths;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public sealed class StatsIndexStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private McaccPaths Paths => new(
        Path.Join(_temp.Path, ".mcacc"),
        OperatingSystem.IsWindows() ? PathStyle.Windows : PathStyle.Posix);

    private static readonly StatsIndex Sample = new(
        StatsIndex.CurrentVersion,
        [
            new IndexedLogFile(
                Path.Join("p1", "s1.jsonl"),
                1234,
                new DateTime(2026, 9, 20, 10, 15, 0, DateTimeKind.Utc),
                1200,
                [
                    new TokenBucket(
                        new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
                        "claude-opus-4-5",
                        @"C:\Code\Mcacc",
                        "s1",
                        new TokenCounts(1, 2, 3, 4, 5),
                        6),
                ],
                [42, -7],
                1),
        ]);

    private static string Json(StatsIndex index) =>
        JsonSerializer.Serialize(index, McaccCompactJsonContext.Default.StatsIndex);

    [Fact]
    public void A_saved_index_loads_back()
    {
        var store = new StatsIndexStore(Paths);

        store.Save("privat", Sample);

        Json(store.Load("privat")).ShouldBe(Json(Sample));
    }

    [Fact]
    public void Without_a_file_the_index_is_empty()
    {
        new StatsIndexStore(Paths).Load("privat").ShouldBeSameAs(StatsIndex.Empty);
    }

    [Fact]
    public void An_index_of_another_version_loads_as_empty()
    {
        var store = new StatsIndexStore(Paths);
        store.Save("privat", Sample with { Version = 0 });

        store.Load("privat").ShouldBeSameAs(StatsIndex.Empty);
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var store = new StatsIndexStore(Paths);
        store.Save("privat", Sample);

        store.Delete("privat");
        store.Delete("privat");

        File.Exists(Paths.StatsFile("privat")).ShouldBeFalse();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~Mcacc.Core.Tests.Stats"`
Expected: the build fails because `StatsIndex`, `IndexedLogFile`, `ClaudeLogScanner` and `StatsIndexStore` are unknown.

- [ ] **Step 4: Implement the index and its store**

Create `src/Mcacc.Core/Stats/StatsIndex.cs`:

```csharp
namespace Mcacc.Core.Stats;

/// <summary>What the scanner already knows about the session logs of one account, kept in <c>stats/&lt;id&gt;.json</c>.</summary>
/// <param name="Version">Raised whenever the meaning of a field changes; an index of another version is rebuilt.</param>
public sealed record StatsIndex(int Version, IReadOnlyList<IndexedLogFile> Files)
{
    public const int CurrentVersion = 1;

    public static readonly StatsIndex Empty = new(CurrentVersion, []);
}

/// <summary>One log file as the last scan left it.</summary>
/// <param name="Path">Relative to <c>&lt;claudeDir&gt;/projects</c>.</param>
/// <param name="Offset">The byte right after the last complete line; the next scan of a grown file starts here.</param>
/// <param name="Ids">The responses this file counted, so no other file counts them again.</param>
public sealed record IndexedLogFile(
    string Path,
    long Length,
    DateTime LastWriteUtc,
    long Offset,
    IReadOnlyList<TokenBucket> Buckets,
    IReadOnlyList<long> Ids,
    int BrokenLines);

public sealed record LogScanResult(StatsIndex Index, bool Changed);
```

Add the index to `src/Mcacc.Core/Config/McaccCompactJsonContext.cs`, below the existing attribute:

```csharp
[JsonSerializable(typeof(UsageHistoryPoint))]
[JsonSerializable(typeof(StatsIndex))]
public sealed partial class McaccCompactJsonContext : JsonSerializerContext;
```

Also add `using Mcacc.Core.Stats;` at the top of that file.

Create `src/Mcacc.Core/Stats/StatsIndexStore.cs`:

```csharp
using Mcacc.Core.Config;
using Mcacc.Core.FileSystem;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Stats;

public sealed class StatsIndexStore(McaccPaths paths)
{
    /// <summary>A missing, broken or outdated index loads as empty: the scanner then simply reads everything again.</summary>
    public StatsIndex Load(string accountId) =>
        JsonFile.Read(paths.StatsFile(accountId), McaccCompactJsonContext.Default.StatsIndex)
            is { Version: StatsIndex.CurrentVersion, Files: not null } index
            ? index
            : StatsIndex.Empty;

    public void Save(string accountId, StatsIndex index) =>
        JsonFile.Write(paths.StatsFile(accountId), index, McaccCompactJsonContext.Default.StatsIndex);

    public void Delete(string accountId)
    {
        var file = paths.StatsFile(accountId);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }
}
```

- [ ] **Step 5: Implement the scanner**

Create `src/Mcacc.Core/Stats/ClaudeLogScanner.cs`:

```csharp
using System.Buffers;

namespace Mcacc.Core.Stats;

/// <summary>
/// Brings a <see cref="StatsIndex"/> up to date with the session logs under <c>&lt;claudeDir&gt;/projects</c>. Only what
/// is new since the last scan is read, so after the first scan an update takes milliseconds.
/// </summary>
public static class ClaudeLogScanner
{
    private const int BufferSize = 64 * 1024;
    private const long ProgressStep = 8L * 1024 * 1024;

    private static readonly EnumerationOptions AllLogs = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    public static LogScanResult Update(
        string claudeDir,
        StatsIndex index,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var rebuild = index.Version != StatsIndex.CurrentVersion;

        // Entries of deleted files stay: Claude Code cleans up old transcripts, and their tokens still count.
        var entries = rebuild
            ? new Dictionary<string, IndexedLogFile>(StringComparer.Ordinal)
            : index.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);

        var root = Path.Join(claudeDir, "projects");
        var work = new List<WorkItem>();
        if (Directory.Exists(root))
        {
            foreach (var log in new DirectoryInfo(root).EnumerateFiles("*.jsonl", AllLogs)
                         .OrderBy(file => file.FullName, StringComparer.Ordinal))
            {
                var path = Path.GetRelativePath(root, log.FullName);
                var old = entries.GetValueOrDefault(path);
                if (old is not null && old.Length == log.Length && old.LastWriteUtc == log.LastWriteTimeUtc)
                {
                    continue;
                }

                work.Add(new WorkItem(log, path, old, Resume: old is not null && log.Length >= old.Length));
            }
        }

        if (work.Count == 0 && !rebuild)
        {
            progress?.Report(1);
            return new LogScanResult(index, Changed: false);
        }

        var seen = entries.Values.SelectMany(file => file.Ids).ToHashSet();
        var counter = new ProgressCounter(work.Sum(item => item.Info.Length - item.Start), progress);
        foreach (var item in work)
        {
            ct.ThrowIfCancellationRequested();
            if (Scan(item, seen, counter, ct) is { } scanned)
            {
                entries[item.Path] = scanned;
            }
        }

        progress?.Report(1);
        var files = entries.Values.OrderBy(file => file.Path, StringComparer.Ordinal).ToList();
        return new LogScanResult(new StatsIndex(StatsIndex.CurrentVersion, files), Changed: true);
    }

    /// <returns>The new entry, or null when the file could not be read; the old entry then stands.</returns>
    private static IndexedLogFile? Scan(WorkItem item, HashSet<long> seen, ProgressCounter counter, CancellationToken ct)
    {
        var resumed = item.Resume ? item.Old : null;

        // A file read from the start may count its own earlier responses again; they are in seen because of it.
        HashSet<long> own = item.Old is not null && !item.Resume ? [.. item.Old.Ids] : [];
        var sums = new Dictionary<BucketKey, (TokenCounts Tokens, int Messages)>();
        foreach (var bucket in resumed?.Buckets ?? [])
        {
            sums[new BucketKey(bucket.Hour, bucket.Model, bucket.Project, bucket.Session)] = (bucket.Tokens, bucket.Messages);
        }

        var ids = new List<long>(resumed?.Ids ?? []);
        var found = new HashSet<long>();
        var broken = resumed?.BrokenLines ?? 0;
        long offset;
        try
        {
            offset = ReadLines(item.Info.FullName, item.Start, counter, ct, line =>
            {
                if (!LogLineParser.TryParse(line, out var usage))
                {
                    broken++;
                    return;
                }

                if (usage is null || found.Contains(usage.Id) || (seen.Contains(usage.Id) && !own.Contains(usage.Id)))
                {
                    return;
                }

                found.Add(usage.Id);
                ids.Add(usage.Id);
                var key = new BucketKey(Hour(usage.Timestamp), usage.Model, usage.Project, usage.Session);
                var sum = sums.TryGetValue(key, out var known) ? known : (TokenCounts.Zero, 0);
                sums[key] = (sum.Tokens + usage.Tokens, sum.Messages + 1);
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Locked or deleted mid-read: the next scan tries again.
            return null;
        }

        seen.ExceptWith(own);
        seen.UnionWith(found);
        var buckets = sums
            .Select(pair => new TokenBucket(pair.Key.Hour, pair.Key.Model, pair.Key.Project, pair.Key.Session, pair.Value.Tokens, pair.Value.Messages))
            .OrderBy(bucket => bucket.Hour)
            .ThenBy(bucket => bucket.Model, StringComparer.Ordinal)
            .ThenBy(bucket => bucket.Project, StringComparer.Ordinal)
            .ThenBy(bucket => bucket.Session, StringComparer.Ordinal)
            .ToList();
        return new IndexedLogFile(item.Path, item.Info.Length, item.Info.LastWriteTimeUtc, offset, buckets, ids, broken);
    }

    /// <summary>Hands every complete line to <paramref name="onLine"/>; a line may be longer than the buffer.</summary>
    /// <returns>The offset right after the last complete line.</returns>
    private static long ReadLines(
        string file,
        long start,
        ProgressCounter counter,
        CancellationToken ct,
        Action<ReadOnlyMemory<byte>> onLine)
    {
        // Claude Code keeps writing the log of a running session; sharing write and delete lets it.
        using var stream = new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 0,
            FileOptions.SequentialScan);
        stream.Seek(start, SeekOrigin.Begin);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var carry = new ArrayBufferWriter<byte>();
        var position = start;
        var consumed = start;
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, BufferSize)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                var chunk = buffer.AsMemory(0, read);
                int newline;
                while ((newline = chunk.Span.IndexOf((byte)'\n')) >= 0)
                {
                    if (carry.WrittenCount == 0)
                    {
                        onLine(chunk[..newline]);
                    }
                    else
                    {
                        carry.Write(chunk.Span[..newline]);
                        onLine(carry.WrittenMemory);
                        carry.ResetWrittenCount();
                    }

                    chunk = chunk[(newline + 1)..];
                }

                carry.Write(chunk.Span);
                position += read;
                consumed = position - carry.WrittenCount;
                counter.Add(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return consumed;
    }

    private static DateTimeOffset Hour(DateTimeOffset at) =>
        new(at.UtcTicks - at.UtcTicks % TimeSpan.TicksPerHour, TimeSpan.Zero);

    private sealed record WorkItem(FileInfo Info, string Path, IndexedLogFile? Old, bool Resume)
    {
        public long Start => Resume ? Old!.Offset : 0;
    }

    private readonly record struct BucketKey(DateTimeOffset Hour, string Model, string Project, string Session);

    /// <summary>Reports every 8 MB, so a first scan of hundreds of megabytes moves the bar without flooding it.</summary>
    private sealed class ProgressCounter(long total, IProgress<double>? progress)
    {
        private long _read;
        private long _reported;

        public void Add(long bytes)
        {
            _read += bytes;
            if (progress is null || _read - _reported < ProgressStep)
            {
                return;
            }

            _reported = _read;
            progress.Report(total <= 0 ? 1 : Math.Min(1, (double)_read / total));
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~Mcacc.Core.Tests.Stats"`
Expected: PASS for `ClaudeLogScannerTests`, `StatsIndexStoreTests` and `LogLineParserTests`.

- [ ] **Step 7: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): scan the session logs incrementally into a stats index"
```

---

### Task 8: Price table and cost estimate

**Files:**
- Create: `src/Mcacc.Core/Stats/ModelPrice.cs`, `src/Mcacc.Core/Stats/ModelPricing.cs`, `src/Mcacc.Core/Stats/CostEstimator.cs`
- Test: `tests/Mcacc.Core.Tests/Stats/CostEstimatorTests.cs` (new)

**Interfaces:**
- Consumes: `TokenCounts` (Task 6).
- Produces:
  - `ModelPrice(decimal Input, decimal Output, decimal CacheWrite5Min, decimal CacheWrite1Hour, decimal CacheRead)`, in USD per million tokens, with `static ModelPrice Standard(decimal input, decimal output, decimal? cacheRead = null)`
  - `ModelPricing.Find(string model) : ModelPrice?` and `ModelPricing.Normalize(string model) : string`
  - `CostEstimator.Estimate(string model, TokenCounts tokens) : decimal?` (null for an unknown model) and `CostEstimator.Cost(ModelPrice price, TokenCounts tokens) : decimal`

Prices, in USD per million tokens (input / output):

| Model | Price | Note |
|---|---|---|
| Fable 5.1 | 10 / 50 | cache read 0.25 |
| Mythos 5.1 | 10 / 50 | cache read 0.25, assumed equal to Fable 5.1 |
| Fable 5, Mythos 5 | 10 / 50 | cache read 1 |
| Opus 5.5 | 4 / 20 | cache read 0.20 |
| Opus 5, Opus 4.8 / 4.7 / 4.6 / 4.5 | 5 / 25 | |
| Sonnet 5 | 2 / 10 | |
| Sonnet 4.6 / 4.5 / 4, Sonnet 3.7, Sonnet 3.5 | 3 / 15 | |
| Haiku 4.5 | 1 / 5 | |
| Opus 4.1 / 4, Opus 3 | 15 / 75 | |
| Haiku 3.5 | 0.80 / 4 | |
| Haiku 3 | 0.25 / 1.25 | |

Cache prices, unless the table says otherwise:
- cache write 5 min = 1.25 × input
- cache write 1 h = 2 × input
- cache read = 0.1 × input

- [ ] **Step 1: Write the failing tests**

Create `tests/Mcacc.Core.Tests/Stats/CostEstimatorTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~CostEstimatorTests"`
Expected: the build fails because `CostEstimator`, `ModelPricing` and `ModelPrice` are unknown.

- [ ] **Step 3: Implement**

Create `src/Mcacc.Core/Stats/ModelPrice.cs`:

```csharp
namespace Mcacc.Core.Stats;

/// <summary>API list prices in USD per million tokens.</summary>
public sealed record ModelPrice(
    decimal Input,
    decimal Output,
    decimal CacheWrite5Min,
    decimal CacheWrite1Hour,
    decimal CacheRead)
{
    /// <summary>
    /// Cache writes cost 1.25 times (5 minutes) and 2 times (1 hour) the input price, a cache read a tenth of it,
    /// unless the model has its own cache read price.
    /// </summary>
    public static ModelPrice Standard(decimal input, decimal output, decimal? cacheRead = null) =>
        new(input, output, input * 1.25m, input * 2m, cacheRead ?? input * 0.1m);
}
```

Create `src/Mcacc.Core/Stats/ModelPricing.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Mcacc.Core.Stats;

/// <summary>
/// The built-in price table. It is only an estimate of what the tokens would cost on the API: a subscription pays
/// none of it, and the long-context surcharge of some models is left out.
/// </summary>
public static partial class ModelPricing
{
    private static readonly Dictionary<string, ModelPrice> Prices = new(StringComparer.Ordinal)
    {
        ["claude-fable-5-1"] = ModelPrice.Standard(10m, 50m, cacheRead: 0.25m),

        // No published price of its own; taken to be the same as Fable 5.1.
        ["claude-mythos-5-1"] = ModelPrice.Standard(10m, 50m, cacheRead: 0.25m),
        ["claude-fable-5"] = ModelPrice.Standard(10m, 50m, cacheRead: 1m),
        ["claude-mythos-5"] = ModelPrice.Standard(10m, 50m, cacheRead: 1m),
        ["claude-opus-5-5"] = ModelPrice.Standard(4m, 20m, cacheRead: 0.20m),
        ["claude-opus-5"] = ModelPrice.Standard(5m, 25m),
        ["claude-opus-4-8"] = ModelPrice.Standard(5m, 25m),
        ["claude-opus-4-7"] = ModelPrice.Standard(5m, 25m),
        ["claude-opus-4-6"] = ModelPrice.Standard(5m, 25m),
        ["claude-opus-4-5"] = ModelPrice.Standard(5m, 25m),
        ["claude-sonnet-5"] = ModelPrice.Standard(2m, 10m),
        ["claude-sonnet-4-6"] = ModelPrice.Standard(3m, 15m),
        ["claude-sonnet-4-5"] = ModelPrice.Standard(3m, 15m),
        ["claude-sonnet-4"] = ModelPrice.Standard(3m, 15m),
        ["claude-3-7-sonnet"] = ModelPrice.Standard(3m, 15m),
        ["claude-3-5-sonnet"] = ModelPrice.Standard(3m, 15m),
        ["claude-haiku-4-5"] = ModelPrice.Standard(1m, 5m),

        // The retired models below are listed from memory, as the current price list no longer carries them.
        ["claude-opus-4-1"] = ModelPrice.Standard(15m, 75m),
        ["claude-opus-4"] = ModelPrice.Standard(15m, 75m),
        ["claude-3-opus"] = ModelPrice.Standard(15m, 75m),
        ["claude-3-5-haiku"] = ModelPrice.Standard(0.80m, 4m),
        ["claude-3-haiku"] = ModelPrice.Standard(0.25m, 1.25m),
    };

    /// <returns>Null for a model the table does not know; an unknown price is no price, not zero.</returns>
    public static ModelPrice? Find(string model) => Prices.GetValueOrDefault(Normalize(model));

    /// <summary>"claude-opus-4-5-20251101[1m]" becomes "claude-opus-4-5": neither the date nor the context changes the price.</summary>
    public static string Normalize(string model)
    {
        var name = model.Trim().ToLowerInvariant();
        var bracket = name.IndexOf('[', StringComparison.Ordinal);
        if (bracket >= 0)
        {
            name = name[..bracket];
        }

        return DateSuffix().Replace(name, "");
    }

    [GeneratedRegex(@"-\d{8}$")]
    private static partial Regex DateSuffix();
}
```

Create `src/Mcacc.Core/Stats/CostEstimator.cs`:

```csharp
namespace Mcacc.Core.Stats;

public static class CostEstimator
{
    /// <returns>USD, or null when the model has no price.</returns>
    public static decimal? Estimate(string model, TokenCounts tokens) =>
        ModelPricing.Find(model) is { } price ? Cost(price, tokens) : null;

    public static decimal Cost(ModelPrice price, TokenCounts tokens) =>
        (tokens.Input * price.Input
         + tokens.Output * price.Output
         + tokens.CacheWrite5Min * price.CacheWrite5Min
         + tokens.CacheWrite1Hour * price.CacheWrite1Hour
         + tokens.CacheRead * price.CacheRead) / 1_000_000m;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~CostEstimatorTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): estimate the api cost of the logged tokens"
```

---

### Task 9: Statistics query

**Files:**
- Create: `src/Mcacc.Core/Stats/AccountStats.cs`, `src/Mcacc.Core/Stats/AccountStatsQuery.cs`
- Test: `tests/Mcacc.Core.Tests/Stats/AccountStatsQueryTests.cs` (new)

**Interfaces:**
- Consumes: `TokenBucket`, `TokenCounts` (Task 6) and `CostEstimator` (Task 8).
- Produces:
  - `AccountStats(DateOnly From, DateOnly To, TokenCounts Tokens, decimal Cost, long UnpricedTokens, int Messages, int Sessions, int ActiveDays, IReadOnlyList<DailyUsage> Days, IReadOnlyList<ModelUsage> Models, IReadOnlyList<ProjectUsage> Projects)`
  - `DailyUsage(DateOnly Day, IReadOnlyDictionary<string, long> TokensByModel)` with `long Total`. Every day of the range is present, and a day without tokens has an empty dictionary.
  - `ModelUsage(string Model, TokenCounts Tokens, int Messages, decimal? Cost)`, sorted by tokens, largest first
  - `ProjectUsage(string Project, long Tokens, decimal Cost)`: the top N by tokens
  - `AccountStatsQuery.Compute(IEnumerable<TokenBucket> buckets, DateTimeOffset now, int days, TimeZoneInfo zone, int topProjects = 8) : AccountStats`

Rules:
- The range is the last `days` **local** days including today.
- A bucket belongs to the local day of its start hour.
- `Cost` counts only priced models. `UnpricedTokens` holds the tokens of the others.

- [ ] **Step 1: Write the failing tests**

Create `tests/Mcacc.Core.Tests/Stats/AccountStatsQueryTests.cs`:

```csharp
using System.Globalization;
using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public class AccountStatsQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo PlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static TokenBucket Bucket(
        string utcHour,
        string model = "claude-opus-4-5",
        string project = @"C:\A",
        string session = "s1",
        long input = 100,
        int messages = 1) =>
        new(
            DateTimeOffset.Parse(utcHour, CultureInfo.InvariantCulture),
            model,
            project,
            session,
            new TokenCounts(input, 0, 0, 0, 0),
            messages);

    private static AccountStats Compute(params TokenBucket[] buckets) =>
        AccountStatsQuery.Compute(buckets, Now, 7, PlusTwo);

    [Fact]
    public void The_range_is_the_last_n_local_days_including_today()
    {
        var stats = Compute();

        stats.From.ShouldBe(new DateOnly(2026, 9, 18));
        stats.To.ShouldBe(new DateOnly(2026, 9, 24));
        stats.Days.Select(day => day.Day).ShouldBe(Enumerable.Range(18, 7).Select(day => new DateOnly(2026, 9, day)));
        stats.Days.ShouldAllBe(day => day.Total == 0);
    }

    [Fact]
    public void A_late_utc_hour_belongs_to_the_next_local_day()
    {
        var stats = Compute(Bucket("2026-09-19T21:00Z", input: 1), Bucket("2026-09-19T22:00Z", input: 2));

        stats.Days.Single(day => day.Day == new DateOnly(2026, 9, 19)).Total.ShouldBe(1);
        stats.Days.Single(day => day.Day == new DateOnly(2026, 9, 20)).Total.ShouldBe(2);
    }

    [Fact]
    public void Buckets_outside_the_range_are_left_out()
    {
        var stats = Compute(Bucket("2026-09-17T21:00Z", input: 1), Bucket("2026-09-17T22:00Z", input: 2));

        stats.Tokens.Total.ShouldBe(2);
    }

    [Fact]
    public void Sums_tokens_messages_sessions_and_active_days()
    {
        var stats = Compute(
            Bucket("2026-09-20T08:00Z", session: "s1", messages: 2),
            Bucket("2026-09-20T09:00Z", session: "s2", messages: 3),
            Bucket("2026-09-22T09:00Z", session: "s2", messages: 1));

        stats.Tokens.ShouldBe(new TokenCounts(300, 0, 0, 0, 0));
        stats.Messages.ShouldBe(6);
        stats.Sessions.ShouldBe(2);
        stats.ActiveDays.ShouldBe(2);
    }

    [Fact]
    public void A_day_splits_its_tokens_by_model()
    {
        var stats = Compute(
            Bucket("2026-09-20T08:00Z", model: "claude-opus-4-5", input: 100),
            Bucket("2026-09-20T09:00Z", model: "claude-sonnet-4-5", input: 50));

        stats.Days.Single(day => day.Day == new DateOnly(2026, 9, 20)).TokensByModel
            .ShouldBe(new Dictionary<string, long> { ["claude-opus-4-5"] = 100, ["claude-sonnet-4-5"] = 50 }, ignoreOrder: true);
    }

    [Fact]
    public void Models_are_sorted_by_tokens_and_priced()
    {
        var stats = Compute(
            Bucket("2026-09-20T08:00Z", model: "claude-opus-4-5", input: 100),
            Bucket("2026-09-20T09:00Z", model: "claude-sonnet-4-5", input: 300, messages: 4));

        stats.Models.ShouldBe([
            new ModelUsage("claude-sonnet-4-5", new TokenCounts(300, 0, 0, 0, 0), 4, 0.0009m),
            new ModelUsage("claude-opus-4-5", new TokenCounts(100, 0, 0, 0, 0), 1, 0.0005m),
        ]);
        stats.Cost.ShouldBe(0.0014m);
        stats.UnpricedTokens.ShouldBe(0);
    }

    [Fact]
    public void An_unknown_model_adds_no_cost_but_is_counted_as_unpriced()
    {
        var stats = Compute(
            Bucket("2026-09-20T08:00Z", model: "claude-opus-4-5", input: 100),
            Bucket("2026-09-20T09:00Z", model: "claude-opus-9", input: 40));

        stats.Models.Single(model => model.Model == "claude-opus-9").Cost.ShouldBeNull();
        stats.Cost.ShouldBe(0.0005m);
        stats.UnpricedTokens.ShouldBe(40);
    }

    [Fact]
    public void Only_the_top_projects_are_listed()
    {
        var stats = AccountStatsQuery.Compute(
            [
                Bucket("2026-09-20T08:00Z", project: @"C:\A", input: 10),
                Bucket("2026-09-20T08:00Z", project: @"C:\B", input: 30),
                Bucket("2026-09-20T08:00Z", project: @"C:\C", input: 20),
            ],
            Now,
            7,
            PlusTwo,
            topProjects: 2);

        stats.Projects.ShouldBe([
            new ProjectUsage(@"C:\B", 30, 0.00015m),
            new ProjectUsage(@"C:\C", 20, 0.0001m),
        ]);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~AccountStatsQueryTests"`
Expected: the build fails because `AccountStatsQuery` and `AccountStats` are unknown.

- [ ] **Step 3: Implement**

Create `src/Mcacc.Core/Stats/AccountStats.cs`:

```csharp
namespace Mcacc.Core.Stats;

/// <summary>The Claude Code usage of one account over a range of local days.</summary>
/// <param name="Cost">USD for the priced models only; see <paramref name="UnpricedTokens"/> for the rest.</param>
public sealed record AccountStats(
    DateOnly From,
    DateOnly To,
    TokenCounts Tokens,
    decimal Cost,
    long UnpricedTokens,
    int Messages,
    int Sessions,
    int ActiveDays,
    IReadOnlyList<DailyUsage> Days,
    IReadOnlyList<ModelUsage> Models,
    IReadOnlyList<ProjectUsage> Projects);

public sealed record DailyUsage(DateOnly Day, IReadOnlyDictionary<string, long> TokensByModel)
{
    public long Total => TokensByModel.Values.Sum();
}

/// <param name="Cost">Null when the model has no price.</param>
public sealed record ModelUsage(string Model, TokenCounts Tokens, int Messages, decimal? Cost);

public sealed record ProjectUsage(string Project, long Tokens, decimal Cost);
```

Create `src/Mcacc.Core/Stats/AccountStatsQuery.cs`:

```csharp
namespace Mcacc.Core.Stats;

public static class AccountStatsQuery
{
    private static readonly IReadOnlyDictionary<string, long> NoTokens = new Dictionary<string, long>();

    /// <param name="days">How many local days, today included.</param>
    public static AccountStats Compute(
        IEnumerable<TokenBucket> buckets,
        DateTimeOffset now,
        int days,
        TimeZoneInfo zone,
        int topProjects = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        var to = LocalDay(now, zone);
        var from = to.AddDays(1 - days);
        var inRange = buckets
            .Select(bucket => (Bucket: bucket, Day: LocalDay(bucket.Hour, zone)))
            .Where(entry => entry.Day >= from && entry.Day <= to)
            .ToList();

        var models = inRange
            .GroupBy(entry => entry.Bucket.Model, StringComparer.Ordinal)
            .Select(group =>
            {
                var tokens = Sum(group.Select(entry => entry.Bucket.Tokens));
                return new ModelUsage(
                    group.Key,
                    tokens,
                    group.Sum(entry => entry.Bucket.Messages),
                    CostEstimator.Estimate(group.Key, tokens));
            })
            .OrderByDescending(model => model.Tokens.Total)
            .ThenBy(model => model.Model, StringComparer.Ordinal)
            .ToList();

        var perDay = inRange
            .GroupBy(entry => entry.Day)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, long>)group
                    .GroupBy(entry => entry.Bucket.Model, StringComparer.Ordinal)
                    .ToDictionary(
                        model => model.Key,
                        model => model.Sum(entry => entry.Bucket.Tokens.Total),
                        StringComparer.Ordinal));

        var projects = inRange
            .GroupBy(entry => entry.Bucket.Project, StringComparer.Ordinal)
            .Select(group => new ProjectUsage(
                group.Key,
                group.Sum(entry => entry.Bucket.Tokens.Total),
                group
                    .GroupBy(entry => entry.Bucket.Model, StringComparer.Ordinal)
                    .Sum(model => CostEstimator.Estimate(model.Key, Sum(model.Select(entry => entry.Bucket.Tokens))) ?? 0m)))
            .OrderByDescending(project => project.Tokens)
            .ThenBy(project => project.Project, StringComparer.Ordinal)
            .Take(topProjects)
            .ToList();

        return new AccountStats(
            from,
            to,
            Sum(models.Select(model => model.Tokens)),
            models.Sum(model => model.Cost ?? 0m),
            models.Where(model => model.Cost is null).Sum(model => model.Tokens.Total),
            models.Sum(model => model.Messages),
            inRange
                .Select(entry => entry.Bucket.Session)
                .Where(session => session.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            perDay.Count,
            Enumerable.Range(0, days)
                .Select(offset => from.AddDays(offset))
                .Select(day => new DailyUsage(day, perDay.GetValueOrDefault(day) ?? NoTokens))
                .ToList(),
            models,
            projects);
    }

    private static DateOnly LocalDay(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    private static TokenCounts Sum(IEnumerable<TokenCounts> all) =>
        all.Aggregate(TokenCounts.Zero, (sum, tokens) => sum + tokens);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~AccountStatsQueryTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): compute the token statistics of an account per local day"
```

---

### Task 10: Stats provider

**Files:**
- Create: `src/Mcacc.Core/Abstractions/IClaudeStatsProvider.cs`, `src/Mcacc.Core/Stats/ClaudeStatsProvider.cs`
- Test: `tests/Mcacc.Core.Tests/Stats/ClaudeStatsProviderTests.cs` (new)

**Interfaces:**
- Consumes:
  - `ClaudeLogScanner`, `StatsIndexStore` (Task 7)
  - `AccountPathResolver.Resolve(Account).EffectiveClaudeDir` (existing)
- Produces:
  - `IClaudeStatsProvider.LoadAsync(Account account, IProgress<double>? progress, CancellationToken ct) : Task<IReadOnlyList<TokenBucket>>`
  - `IClaudeStatsProvider.Forget(Account account)`
  - `ClaudeStatsProvider(StatsIndexStore store, AccountPathResolver resolver)`

Behaviour:
- The scan runs on the thread pool.
- Only one scan per account runs at a time. A second caller waits and then finds the index up to date.
- A scan that ends after `Forget` does not write the index back.

- [ ] **Step 1: Write the failing tests**

Create `tests/Mcacc.Core.Tests/Stats/ClaudeStatsProviderTests.cs`:

```csharp
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Stats;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Stats;

public sealed class ClaudeStatsProviderTests : IDisposable
{
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PlatformInfo Platform => new(
        OperatingSystem.IsWindows() ? OsKind.Windows : OsKind.Linux,
        _temp.Path,
        Path.Join(_temp.Path, "Roaming"),
        Path.Join(_temp.Path, "Local"));

    private McaccPaths Paths => new(Path.Join(_temp.Path, ".mcacc"), Platform.PathStyle);

    private AccountPathResolver Resolver => new(Paths, Platform);

    private StatsIndexStore Store => new(Paths);

    private ClaudeStatsProvider Provider() => new(Store, Resolver);

    private string Log(params string[] lines)
    {
        var file = Path.Join(Resolver.Resolve(Privat).EffectiveClaudeDir, "projects", "p1", "s1.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Concat(lines.Select(line => line + "\n")));
        return file;
    }

    [Fact]
    public async Task Loads_the_buckets_and_saves_the_index()
    {
        Log(LogLines.Assistant("msg_1"), LogLines.Assistant("msg_2"));

        var buckets = await Provider().LoadAsync(Privat, null, Ct);

        buckets.Sum(bucket => bucket.Messages).ShouldBe(2);
        Store.Load("privat").Files.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_index_outlives_a_deleted_log()
    {
        var file = Log(LogLines.Assistant("msg_1"));
        await Provider().LoadAsync(Privat, null, Ct);

        File.Delete(file);

        (await Provider().LoadAsync(Privat, null, Ct)).Sum(bucket => bucket.Messages).ShouldBe(1);
    }

    [Fact]
    public async Task Forget_deletes_the_index()
    {
        Log(LogLines.Assistant("msg_1"));
        var provider = Provider();
        await provider.LoadAsync(Privat, null, Ct);

        provider.Forget(Privat);

        File.Exists(Paths.StatsFile("privat")).ShouldBeFalse();
    }

    [Fact]
    public async Task Parallel_loads_count_each_response_once()
    {
        Log(LogLines.Assistant("msg_1"), LogLines.Assistant("msg_2"));
        var provider = Provider();

        var results = await Task.WhenAll(
            provider.LoadAsync(Privat, null, Ct),
            provider.LoadAsync(Privat, null, Ct));

        results.ShouldAllBe(buckets => buckets.Sum(bucket => bucket.Messages) == 2);
        Store.Load("privat").Files.ShouldHaveSingleItem().Ids.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_account_without_logs_has_no_buckets()
    {
        (await Provider().LoadAsync(Privat, null, Ct)).ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.Core.Tests --filter "FullyQualifiedName~ClaudeStatsProviderTests"`
Expected: the build fails because `ClaudeStatsProvider` is unknown.

- [ ] **Step 3: Implement**

Create `src/Mcacc.Core/Abstractions/IClaudeStatsProvider.cs`:

```csharp
using Mcacc.Core.Model;
using Mcacc.Core.Stats;

namespace Mcacc.Core.Abstractions;

public interface IClaudeStatsProvider
{
    /// <summary>Brings the index up to date with the account's session logs and returns all its buckets.</summary>
    /// <param name="progress">0 to 1 over the bytes still to read; a first scan can take a while.</param>
    Task<IReadOnlyList<TokenBucket>> LoadAsync(Account account, IProgress<double>? progress, CancellationToken ct);

    /// <summary>For a removed account: deletes its index.</summary>
    void Forget(Account account);
}
```

Create `src/Mcacc.Core/Stats/ClaudeStatsProvider.cs`:

```csharp
using System.Collections.Concurrent;
using Mcacc.Core.Abstractions;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Stats;

public sealed class ClaudeStatsProvider(StatsIndexStore store, AccountPathResolver resolver) : IClaudeStatsProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    /// <summary>One scan per account at a time; a second caller waits and then finds the index up to date.</summary>
    public async Task<IReadOnlyList<TokenBucket>> LoadAsync(
        Account account,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var gate = _gates.GetOrAdd(account.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var claudeDir = resolver.Resolve(account).EffectiveClaudeDir;
            return await Task.Run(() => Scan(account.Id, claudeDir, gate, progress, ct), ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Forget(Account account)
    {
        _gates.TryRemove(account.Id, out _);
        store.Delete(account.Id);
    }

    private IReadOnlyList<TokenBucket> Scan(
        string accountId,
        string claudeDir,
        SemaphoreSlim gate,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var result = ClaudeLogScanner.Update(claudeDir, store.Load(accountId), progress, ct);

        // A forgotten account has lost its gate; writing now would bring its index back.
        if (result.Changed && _gates.TryGetValue(accountId, out var current) && ReferenceEquals(current, gate))
        {
            try
            {
                store.Save(accountId, result.Index);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The numbers are still right; the next load just scans the same bytes again.
            }
        }

        return result.Index.Files.SelectMany(file => file.Buckets).ToList();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.Core.Tests`
Expected: all Core tests PASS.

- [ ] **Step 5: Commit**

```bash
git add -A src/Mcacc.Core tests/Mcacc.Core.Tests
git commit -F <message-file>   # "feat(core): load the token statistics of an account in the background"
```

---

### Task 11: Poller, page deactivation and the new service slots

**Files:**
- Create: `src/Mcacc.App/Services/UsagePoller.cs`
- Modify: `src/Mcacc.App/ViewModels/PageViewModel.cs`, `src/Mcacc.App/Services/Navigator.cs`, `src/Mcacc.App/Services/AppServices.cs`, `src/Mcacc.App/CompositionRoot.cs`, `src/Mcacc.App/ViewModels/AccountPickerViewModel.cs`
- Modify: `tests/Mcacc.App.Tests/TestSupport/Fakes.cs`, `tests/Mcacc.App.Tests/TestSupport/TestServices.cs`
- Test: `tests/Mcacc.App.Tests/Services/UsagePollerTests.cs` (new), `tests/Mcacc.App.Tests/Services/NavigatorTests.cs`, `tests/Mcacc.App.Tests/ViewModels/AccountPickerViewModelTests.cs`

**Interfaces:**
- Consumes:
  - `IUsageProvider.Forget` (Task 5)
  - `IClaudeStatsProvider`, `ClaudeStatsProvider`, `StatsIndexStore` (Tasks 7 and 10)
- Produces:
  - `UsagePoller(IAccountService accounts, IUsageProvider usage, ILog log) : IDisposable` with:
    - `static TimeSpan Interval` (5 min)
    - `event Action<Account, AccountUsage>? Updated`
    - `void Start()`
    - `Task TickAsync(CancellationToken ct)`
  - `PageViewModel.Deactivate()` (virtual, empty). `Navigator` calls it on the page it leaves.
  - `AppServices(…, IUsageProvider Usage, UsagePoller Poller, IClaudeStatsProvider Stats, ICredentialSource Credentials, ILoginInfoReader Login, …)`
  - Test fakes:
    - `FakeClaudeStatsProvider` with `Buckets`, `Loaded`, `Forgotten`, `Gate`, `ThrowOnLoad`
    - `FakeCredentialSource` with `Results`
    - `TestServices.Poller`, `.Stats`, `.Credentials`

- [ ] **Step 1: Write the failing tests**

Create `tests/Mcacc.App.Tests/Services/UsagePollerTests.cs`:

```csharp
using Mcacc.App.Services;
using Mcacc.App.Tests.TestSupport;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Services;

public class UsagePollerTests
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeAccountService _accounts = new(Standard, Privat);
    private readonly FakeUsageProvider _usage = new();

    private UsagePoller Poller() => new(_accounts, _usage, NullLog.Instance);

    [Fact]
    public async Task A_tick_refreshes_every_account_and_reports_each_result()
    {
        var fresh = new AccountUsage(UsageState.Ok, new UsageSnapshot(new UsageWindow(42, null), null, DateTimeOffset.Now));
        _usage.Fresh["privat"] = fresh;
        using var poller = Poller();
        var updates = new List<(string Id, AccountUsage Usage)>();
        poller.Updated += (account, usage) => updates.Add((account.Id, usage));

        await poller.TickAsync(Ct);

        _usage.Refreshed.ShouldBe(["standard", "privat"], ignoreOrder: true);
        updates.ShouldBe([("standard", _usage.GetCached(Standard)), ("privat", fresh)]);
    }

    [Fact]
    public async Task A_failing_refresh_reports_nothing_and_does_not_throw()
    {
        _usage.ThrowOnRefresh = new HttpRequestException("offline");
        using var poller = Poller();
        var updates = 0;
        poller.Updated += (_, _) => updates++;

        await poller.TickAsync(Ct);

        updates.ShouldBe(0);
    }

    [Fact]
    public void The_poller_asks_every_five_minutes()
    {
        UsagePoller.Interval.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void A_started_poller_stops_on_dispose()
    {
        var poller = Poller();

        poller.Start();

        Should.NotThrow(poller.Dispose);
    }
}
```

In `tests/Mcacc.App.Tests/Services/NavigatorTests.cs`, give the nested `Page` a counter and add three tests:

```csharp
    private sealed class Page(string name) : PageViewModel
    {
        public string Name { get; } = name;

        public int Deactivations { get; private set; }

        public override void Deactivate() => Deactivations++;
    }

    [Fact]
    public void Navigating_away_deactivates_the_page_that_is_left()
    {
        var navigator = new Navigator();
        var first = new Page("first");
        var second = new Page("second");
        navigator.Reset(first);

        navigator.NavigateTo(second);

        first.Deactivations.ShouldBe(1);
        second.Deactivations.ShouldBe(0);
    }

    [Fact]
    public void Going_back_deactivates_the_page_that_is_left()
    {
        var navigator = new Navigator();
        var first = new Page("first");
        var second = new Page("second");
        navigator.Reset(first);
        navigator.NavigateTo(second);

        navigator.Back();

        second.Deactivations.ShouldBe(1);
        first.Deactivations.ShouldBe(1);
    }

    [Fact]
    public void Showing_the_same_page_again_does_not_deactivate_it()
    {
        var navigator = new Navigator();
        var first = new Page("first");
        navigator.Reset(first);

        navigator.Reset(first);

        first.Deactivations.ShouldBe(0);
    }
```

Append to `tests/Mcacc.App.Tests/ViewModels/AccountPickerViewModelTests.cs`:

```csharp
    [Fact]
    public async Task A_poll_updates_the_tile_of_that_account()
    {
        var picker = Picker();
        await picker.ActivateAsync(Ct);
        var snapshot = new UsageSnapshot(new UsageWindow(77, null), null, DateTimeOffset.Now);
        _services.Usage.Fresh["privat"] = new AccountUsage(UsageState.Ok, snapshot);

        await _services.Poller.TickAsync(Ct);

        picker.Tiles.Single(tile => tile.Id == "privat").FiveHour.Value.ShouldBe(77);
    }

    [Fact]
    public async Task A_deactivated_picker_ignores_polls()
    {
        var picker = Picker();
        await picker.ActivateAsync(Ct);
        picker.Deactivate();
        _services.Usage.Fresh["privat"] =
            new AccountUsage(UsageState.Ok, new UsageSnapshot(new UsageWindow(77, null), null, DateTimeOffset.Now));

        await _services.Poller.TickAsync(Ct);

        picker.Tiles.Single(tile => tile.Id == "privat").FiveHour.Value.ShouldBeNull();
    }

    [Fact]
    public async Task Removing_an_account_forgets_its_limits_and_statistics()
    {
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: false));
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Usage.Forgotten.ShouldBe(["privat"]);
        _services.Stats.Forgotten.ShouldBe(["privat"]);
    }

    [Fact]
    public async Task A_failing_removal_forgets_nothing()
    {
        _services.Accounts.ThrowOnRemove = new IOException("the folder is in use");
        _services.Dialogs.Answer<RemoveAccountDialogViewModel>(_ => new RemoveAccountResult(DeleteData: false));
        var picker = Picker();

        await picker.RemoveCommand.ExecuteAsync(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Usage.Forgotten.ShouldBeEmpty();
        _services.Stats.Forgotten.ShouldBeEmpty();
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~UsagePollerTests|FullyQualifiedName~NavigatorTests|FullyQualifiedName~AccountPickerViewModelTests"`
Expected: the build fails because `UsagePoller`, `PageViewModel.Deactivate`, `TestServices.Poller` and `TestServices.Stats` do not exist.

- [ ] **Step 3: Add `Deactivate` and call it from the navigator**

In `src/Mcacc.App/ViewModels/PageViewModel.cs`, add below `ActivateAsync`:

```csharp
    /// <summary>Runs when another page takes over; stop background work and drop event subscriptions here.</summary>
    public virtual void Deactivate()
    {
    }
```

In `src/Mcacc.App/Services/Navigator.cs`, replace `Show` with:

```csharp
    private void Show(PageViewModel page)
    {
        var previous = CurrentPage;
        CurrentPage = page;
        if (previous is not null && !ReferenceEquals(previous, page))
        {
            previous.Deactivate();
        }

        OnPropertyChanged(nameof(CanGoBack));
        Navigated?.Invoke(page);
    }
```

- [ ] **Step 4: Create the poller**

Create `src/Mcacc.App/Services/UsagePoller.cs`:

```csharp
using Mcacc.Core.Abstractions;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Usage;

namespace Mcacc.App.Services;

/// <summary>
/// Refreshes the limits of every account while the app is open, so the history grows even when nobody clicks.
/// The one-minute guard in <see cref="IUsageProvider.RefreshAsync"/> still applies.
/// </summary>
public sealed class UsagePoller(IAccountService accounts, IUsageProvider usage, ILog log) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly CancellationTokenSource _stop = new();

    /// <summary>Raised per account after a tick, on the thread that called <see cref="Start"/>.</summary>
    public event Action<Account, AccountUsage>? Updated;

    /// <summary>Call on the UI thread: the loop keeps its context, so the pages get their updates there.</summary>
    public void Start() => _ = RunAsync(_stop.Token);

    public async Task TickAsync(CancellationToken ct)
    {
        var all = accounts.Config.Accounts;
        var results = await Task.WhenAll(all.Select(account => RefreshAsync(account, ct))).ConfigureAwait(true);
        for (var i = 0; i < all.Count; i++)
        {
            if (results[i] is { } result)
            {
                Updated?.Invoke(all[i], result);
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(true))
            {
                await TickAsync(ct).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    /// <returns>Null when the refresh failed; the failure is logged and the next tick tries again.</returns>
    private async Task<AccountUsage?> RefreshAsync(Account account, CancellationToken ct)
    {
        try
        {
            return await usage.RefreshAsync(account, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.Warn($"Polling the limits of '{account.Id}' failed.", e);
            return null;
        }
    }
}
```

- [ ] **Step 5: Add the service slots and wire them**

In `src/Mcacc.App/Services/AppServices.cs`, replace `IUsageProvider Usage,` with:

```csharp
    IUsageProvider Usage,
    UsagePoller Poller,
    IClaudeStatsProvider Stats,
    ICredentialSource Credentials,
```

In `src/Mcacc.App/CompositionRoot.cs`:

- Add `using Mcacc.Core.Stats;`.
- Before `var services = new AppServices(`, insert:

```csharp
        var usage = new UsageService(
            platform.Credentials,
            login,
            platform.UsageHttp,
            new UsageCache(platform.Paths),
            new ProfileCache(platform.Paths),
            new UsageHistoryStore(platform.Paths),
            resolver);
        var poller = new UsagePoller(accounts, usage, log);
```

- In the `AppServices` argument list, replace the eight-line `new UsageService(` … `resolver),` argument that Task 5 wrote with:

```csharp
            usage,
            poller,
            new ClaudeStatsProvider(new StatsIndexStore(platform.Paths), resolver),
            platform.Credentials,
```

- After `navigator.Reset(new AccountPickerViewModel(services));`, add `poller.Start();`.

- [ ] **Step 6: Update the picker**

In `src/Mcacc.App/ViewModels/AccountPickerViewModel.cs`:

Replace `ActivateAsync` with:

```csharp
    public override async Task ActivateAsync(CancellationToken ct)
    {
        _services.Poller.Updated -= OnPolled;
        _services.Poller.Updated += OnPolled;
        Reload();
        await Task.WhenAll(RefreshRunningAsync(ct), RefreshUsageAsync(ct)).ConfigureAwait(true);
    }

    public override void Deactivate() => _services.Poller.Updated -= OnPolled;
```

In `RemoveAsync`, replace the `try` block body with:

```csharp
            _services.Accounts.Remove(tile.Account.Id, result.DeleteData);
            _services.Usage.Forget(tile.Account);
            _services.Stats.Forget(tile.Account);
```

Add below `RefreshUsageAsync`:

```csharp
    private void OnPolled(Account account, AccountUsage usage) =>
        Tiles.FirstOrDefault(tile => tile.Id == account.Id)?.SetUsage(usage);
```

- [ ] **Step 7: Add the fakes and the test slots**

Append to `tests/Mcacc.App.Tests/TestSupport/Fakes.cs`. Also add `using Mcacc.Core.Stats;` at the top.

```csharp
internal sealed class FakeClaudeStatsProvider : IClaudeStatsProvider
{
    public List<TokenBucket> Buckets { get; } = [];

    public List<string> Loaded { get; } = [];

    public List<string> Forgotten { get; } = [];

    /// <summary>Holds a load until it is completed, to look at the page while it scans.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public Exception? ThrowOnLoad { get; set; }

    public async Task<IReadOnlyList<TokenBucket>> LoadAsync(
        Account account,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        Loaded.Add(account.Id);
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(ct).ConfigureAwait(true);
        }

        if (ThrowOnLoad is { } error)
        {
            throw error;
        }

        return Buckets.ToList();
    }

    public void Forget(Account account) => Forgotten.Add(account.Id);
}

internal sealed class FakeCredentialSource : ICredentialSource
{
    public Dictionary<string, CredentialReadResult> Results { get; } = [];

    public Task<CredentialReadResult> ReadAsync(Account account, AccountPaths paths, CancellationToken ct) =>
        Task.FromResult(Results.GetValueOrDefault(
            account.Id,
            new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null)));
}
```

In `tests/Mcacc.App.Tests/TestSupport/TestServices.cs`:

- In the constructor, before `Services = new AppServices(`, add `Poller = new UsagePoller(Accounts, Usage, NullLog.Instance);`.
- In the argument list, replace `Usage,` with:

```csharp
            Usage,
            Poller,
            Stats,
            Credentials,
```

- Add the properties below `Usage`:

```csharp
    public UsagePoller Poller { get; }

    public FakeClaudeStatsProvider Stats { get; } = new();

    public FakeCredentialSource Credentials { get; } = new();
```

- Change `public void Dispose() => _temp.Dispose();` to:

```csharp
    public void Dispose()
    {
        Poller.Dispose();
        _temp.Dispose();
    }
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet build MCACC.slnx` and then `dotnet test tests/Mcacc.App.Tests`
Expected: the build succeeds and all App tests PASS.

- [ ] **Step 9: Commit**

```bash
git add -A src/Mcacc.App tests/Mcacc.App.Tests
git commit -F <message-file>   # "feat(app): poll the limits every five minutes and forget removed accounts"
```

---

### Task 12: Chart colors, icon and chart basics

**Files:**
- Modify: `src/Mcacc.App/Theme/Tokens.axaml`, `src/Mcacc.App/Theme/ThemeKeys.cs`, `src/Mcacc.App/Controls/MIconKind.cs`, `src/Mcacc.App/Controls/MIconGlyphs.cs`
- Create: `src/Mcacc.App/Controls/ChartData.cs`, `src/Mcacc.App/Controls/ChartScale.cs`, `src/Mcacc.App/Controls/ChartControl.cs`
- Test: `tests/Mcacc.App.Tests/Theme/ThemeTests.cs`, `tests/Mcacc.App.Tests/Controls/ChartTests.cs` (new)

**Interfaces:**
- Produces:
  - `ThemeKeys.Series1..Series6`, `ThemeKeys.Series` (the six keys in order) and `ThemeKeys.UiFont` (`"Mcacc.Ui"`, not in `All`)
  - `MIconKind.Insights`
  - `ChartColor` enum: `Series1..Series6, Accent, Danger, Muted, Track`
  - Records:
    - `ChartPoint(double X, double Y, string? Tooltip = null)`
    - `LineSeries(string Name, ChartColor Color, IReadOnlyList<ChartPoint> Points)`
    - `BarSegment(double Value, ChartColor Color)`
    - `BarStack(string Label, IReadOnlyList<BarSegment> Segments, string? Tooltip = null)` with `double Total`
    - `ChartSlice(string Label, double Value, ChartColor Color, string? Tooltip = null)`
    - `BarListItem(string Label, double Value, string ValueText, ChartColor Color, string? Tooltip = null)`
  - `ChartScale`: `NiceMax(double)`, `Ticks(double max, int count = 4)`, `Map(double value, double min, double max, double from, double to)`, `BandAt(double x, double width, int count)`
  - `ChartControl : Control` (abstract):
    - `EmptyText`
    - `abstract bool HasData`, `abstract void RenderChart(DrawingContext, Size)`, `abstract string? TooltipAt(Point)`
    - `static string KeyOf(ChartColor)`
    - protected `Brush(ChartColor)`, `Brush(string key)`, `Label(...)`

- [ ] **Step 1: Write the failing tests**

In `tests/Mcacc.App.Tests/Theme/ThemeTests.cs`, add:

```csharp
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Every_chart_color_resolves_to_a_brush(string variantName)
    {
        var variant = Variants.Single(v => v.Key.ToString() == variantName);
        var application = Application.Current.ShouldNotBeNull();

        foreach (var color in Enum.GetValues<ChartColor>())
        {
            application.TryGetResource(ChartControl.KeyOf(color), variant, out var value).ShouldBeTrue(color.ToString());
            value.ShouldBeOfType<SolidColorBrush>();
        }
    }

    [AvaloniaFact]
    public void The_six_series_colors_differ_from_each_other()
    {
        var application = Application.Current.ShouldNotBeNull();

        foreach (var variant in Variants)
        {
            var colors = ThemeKeys.Series
                .Select(key => application.TryGetResource(key, variant, out var value) ? ((SolidColorBrush)value!).Color : default)
                .ToList();
            colors.Distinct().Count().ShouldBe(6);
        }
    }
```

Create `tests/Mcacc.App.Tests/Controls/ChartTests.cs`:

```csharp
using Mcacc.App.Controls;
using Mcacc.App.Theme;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Controls;

public class ChartTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(0.7, 1)]
    [InlineData(1, 1)]
    [InlineData(1.2, 2)]
    [InlineData(2.2, 2.5)]
    [InlineData(3, 5)]
    [InlineData(7, 10)]
    [InlineData(100, 100)]
    [InlineData(101, 200)]
    [InlineData(1_234_567, 2_000_000)]
    public void The_axis_ends_at_the_next_nice_number(double value, double expected)
    {
        ChartScale.NiceMax(value).ShouldBe(expected, tolerance: 1e-9);
    }

    [Fact]
    public void Ticks_split_the_axis_evenly()
    {
        ChartScale.Ticks(100).ShouldBe([0, 25, 50, 75, 100]);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 50)]
    [InlineData(100, 0)]
    [InlineData(150, 0)]
    [InlineData(-10, 100)]
    public void Map_turns_a_value_into_a_clamped_pixel(double value, double expected)
    {
        ChartScale.Map(value, 0, 100, 100, 0).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    [InlineData(100, 1)]
    [InlineData(399, 3)]
    [InlineData(400, -1)]
    [InlineData(-1, -1)]
    public void BandAt_finds_the_band_under_the_pointer(double x, int expected)
    {
        ChartScale.BandAt(x, 400, 4).ShouldBe(expected);
    }

    [Fact]
    public void A_stack_totals_its_segments()
    {
        new BarStack("a", [new BarSegment(2, ChartColor.Series1), new BarSegment(3, ChartColor.Series2)]).Total.ShouldBe(5);
    }

    [Theory]
    [InlineData(ChartColor.Series1, ThemeKeys.Series1)]
    [InlineData(ChartColor.Series6, ThemeKeys.Series6)]
    [InlineData(ChartColor.Accent, ThemeKeys.Accent)]
    [InlineData(ChartColor.Danger, ThemeKeys.Danger)]
    [InlineData(ChartColor.Muted, ThemeKeys.TextTertiary)]
    [InlineData(ChartColor.Track, ThemeKeys.Border)]
    public void Each_chart_color_names_a_theme_token(ChartColor color, string key)
    {
        ChartControl.KeyOf(color).ShouldBe(key);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~ChartTests|FullyQualifiedName~ThemeTests"`
Expected: the build fails because `ChartScale`, `ChartColor`, `ChartControl` and `ThemeKeys.Series` are unknown.

- [ ] **Step 3: Add the tokens**

In `src/Mcacc.App/Theme/Tokens.axaml`, add to the `Light` dictionary after `Mcacc.ScrimColor`:

```xml
      <Color x:Key="Mcacc.Series1Color">#D97757</Color>
      <Color x:Key="Mcacc.Series2Color">#6A9BCC</Color>
      <Color x:Key="Mcacc.Series3Color">#788C5D</Color>
      <Color x:Key="Mcacc.Series4Color">#C9A34E</Color>
      <Color x:Key="Mcacc.Series5Color">#A07CB8</Color>
      <Color x:Key="Mcacc.Series6Color">#87867F</Color>
```

and after the `Mcacc.Scrim` brush:

```xml
      <SolidColorBrush x:Key="Mcacc.Series1" Color="#D97757" />
      <SolidColorBrush x:Key="Mcacc.Series2" Color="#6A9BCC" />
      <SolidColorBrush x:Key="Mcacc.Series3" Color="#788C5D" />
      <SolidColorBrush x:Key="Mcacc.Series4" Color="#C9A34E" />
      <SolidColorBrush x:Key="Mcacc.Series5" Color="#A07CB8" />
      <SolidColorBrush x:Key="Mcacc.Series6" Color="#87867F" />
```

Add the same to the `Dark` dictionary with the dark values:

```xml
      <Color x:Key="Mcacc.Series1Color">#E08A6D</Color>
      <Color x:Key="Mcacc.Series2Color">#86B2DD</Color>
      <Color x:Key="Mcacc.Series3Color">#9DB27E</Color>
      <Color x:Key="Mcacc.Series4Color">#D9B660</Color>
      <Color x:Key="Mcacc.Series5Color">#B997CC</Color>
      <Color x:Key="Mcacc.Series6Color">#9C9A92</Color>
```

```xml
      <SolidColorBrush x:Key="Mcacc.Series1" Color="#E08A6D" />
      <SolidColorBrush x:Key="Mcacc.Series2" Color="#86B2DD" />
      <SolidColorBrush x:Key="Mcacc.Series3" Color="#9DB27E" />
      <SolidColorBrush x:Key="Mcacc.Series4" Color="#D9B660" />
      <SolidColorBrush x:Key="Mcacc.Series5" Color="#B997CC" />
      <SolidColorBrush x:Key="Mcacc.Series6" Color="#9C9A92" />
```

Replace `src/Mcacc.App/Theme/ThemeKeys.cs` with:

```csharp
namespace Mcacc.App.Theme;

/// <summary>The keys the theme files define, so code and tests do not spell them twice.</summary>
public static class ThemeKeys
{
    public const string Bg = "Mcacc.Bg";
    public const string Surface = "Mcacc.Surface";
    public const string SurfaceHover = "Mcacc.SurfaceHover";
    public const string Border = "Mcacc.Border";
    public const string BorderStrong = "Mcacc.BorderStrong";
    public const string Text = "Mcacc.Text";
    public const string TextSecondary = "Mcacc.TextSecondary";
    public const string TextTertiary = "Mcacc.TextTertiary";
    public const string Accent = "Mcacc.Accent";
    public const string AccentFill = "Mcacc.AccentFill";
    public const string AccentSubtle = "Mcacc.AccentSubtle";
    public const string Danger = "Mcacc.Danger";
    public const string Scrim = "Mcacc.Scrim";
    public const string Series1 = "Mcacc.Series1";
    public const string Series2 = "Mcacc.Series2";
    public const string Series3 = "Mcacc.Series3";
    public const string Series4 = "Mcacc.Series4";
    public const string Series5 = "Mcacc.Series5";
    public const string Series6 = "Mcacc.Series6";

    /// <summary>The font family of the UI text; a font, so it is not in <see cref="All"/>.</summary>
    public const string UiFont = "Mcacc.Ui";

    /// <summary>The chart colors in the order the series use them.</summary>
    public static IReadOnlyList<string> Series => [Series1, Series2, Series3, Series4, Series5, Series6];

    public static IReadOnlyList<string> All =>
    [
        Bg, Surface, SurfaceHover, Border, BorderStrong, Text, TextSecondary, TextTertiary,
        Accent, AccentFill, AccentSubtle, Danger, Scrim,
        Series1, Series2, Series3, Series4, Series5, Series6,
    ];
}
```

- [ ] **Step 4: Add the icon**

In `src/Mcacc.App/Controls/MIconKind.cs`, add `Insights,` after `Logout,`.

In `src/Mcacc.App/Controls/MIconGlyphs.cs`, add after the `Logout` entry:

```csharp
        [MIconKind.Insights] = "",
```

`insights` is U+F092 in Material Icons Round. The existing entries hold the raw character. The escape is used here so the plan stays readable; both compile to the same string.

- [ ] **Step 5: Create the chart data and the scale**

Create `src/Mcacc.App/Controls/ChartData.cs`:

```csharp
namespace Mcacc.App.Controls;

/// <summary>A color of the chart palette; the control looks up the brush in the current theme.</summary>
public enum ChartColor
{
    Series1,
    Series2,
    Series3,
    Series4,
    Series5,
    Series6,
    Accent,
    Danger,
    Muted,
    Track,
}

/// <param name="Tooltip">What the chart shows when the pointer rests on this point.</param>
public sealed record ChartPoint(double X, double Y, string? Tooltip = null);

public sealed record LineSeries(string Name, ChartColor Color, IReadOnlyList<ChartPoint> Points);

public sealed record BarSegment(double Value, ChartColor Color);

/// <summary>One bar of a stacked bar chart; the segments are drawn from the bottom up.</summary>
public sealed record BarStack(string Label, IReadOnlyList<BarSegment> Segments, string? Tooltip = null)
{
    public double Total => Segments.Sum(segment => segment.Value);
}

public sealed record ChartSlice(string Label, double Value, ChartColor Color, string? Tooltip = null);

public sealed record BarListItem(string Label, double Value, string ValueText, ChartColor Color, string? Tooltip = null);
```

Create `src/Mcacc.App/Controls/ChartScale.cs`:

```csharp
namespace Mcacc.App.Controls;

/// <summary>The arithmetic of the charts, kept apart from drawing so it can be tested without rendering.</summary>
public static class ChartScale
{
    private static readonly double[] NiceSteps = [1, 2, 2.5, 5, 10];

    /// <summary>The smallest of 1, 2, 2.5, 5 times a power of ten that is at least <paramref name="value"/>.</summary>
    public static double NiceMax(double value)
    {
        if (!(value > 0) || double.IsInfinity(value))
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in NiceSteps)
        {
            // The tolerance keeps 100 at 100 although Log10 and Pow round.
            if (value <= step * magnitude * (1 + 1e-9))
            {
                return step * magnitude;
            }
        }

        return 10 * magnitude;
    }

    /// <returns><paramref name="count"/> + 1 values from 0 to <paramref name="max"/>.</returns>
    public static IReadOnlyList<double> Ticks(double max, int count = 4) =>
        Enumerable.Range(0, count + 1).Select(i => max * i / count).ToList();

    /// <summary>Maps a value between min and max onto the pixels from..to; to may be smaller, as on a y axis.</summary>
    public static double Map(double value, double min, double max, double from, double to) =>
        max <= min ? from : from + (Math.Clamp(value, min, max) - min) / (max - min) * (to - from);

    /// <returns>The index of the band under x when the width is split into count equal bands, or -1.</returns>
    public static int BandAt(double x, double width, int count) =>
        count <= 0 || width <= 0 || x < 0 || x >= width ? -1 : Math.Min(count - 1, (int)(x / (width / count)));
}
```

- [ ] **Step 6: Create the chart base class**

Create `src/Mcacc.App/Controls/ChartControl.cs`:

```csharp
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>
/// The common part of the charts: theme colors, labels, the empty state and the tooltip that follows the pointer.
/// A chart draws itself in <see cref="RenderChart"/>; there is no template.
/// </summary>
public abstract class ChartControl : Control
{
    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<ChartControl, string?>(nameof(EmptyText));

    static ChartControl()
    {
        AffectsRender<ChartControl>(EmptyTextProperty);
    }

    protected ChartControl()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>Shown in the middle when there is nothing to draw.</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    protected abstract bool HasData { get; }

    public static string KeyOf(ChartColor color) => color switch
    {
        ChartColor.Series1 => ThemeKeys.Series1,
        ChartColor.Series2 => ThemeKeys.Series2,
        ChartColor.Series3 => ThemeKeys.Series3,
        ChartColor.Series4 => ThemeKeys.Series4,
        ChartColor.Series5 => ThemeKeys.Series5,
        ChartColor.Series6 => ThemeKeys.Series6,
        ChartColor.Accent => ThemeKeys.Accent,
        ChartColor.Danger => ThemeKeys.Danger,
        ChartColor.Muted => ThemeKeys.TextTertiary,
        ChartColor.Track => ThemeKeys.Border,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
    };

    /// <summary>The brush of a chart color in the element's current theme.</summary>
    public static IBrush BrushOf(StyledElement element, ChartColor color) => BrushOf(element, KeyOf(color));

    public static IBrush BrushOf(StyledElement element, string key) =>
        element.TryFindResource(key, element.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Brushes.Gray;

    /// <returns>What the tooltip says at this point of the control, or null for no tooltip.</returns>
    public abstract string? TooltipAt(Point point);

    public sealed override void Render(DrawingContext context)
    {
        // A transparent fill makes the whole area hit-testable, so the tooltip works between the bars too.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (HasData)
        {
            RenderChart(context, Bounds.Size);
        }
        else if (!string.IsNullOrEmpty(EmptyText))
        {
            var text = Label(EmptyText, 12, ThemeKeys.TextTertiary, Bounds.Width - 16);
            context.DrawText(text, new Point((Bounds.Width - text.Width) / 2, (Bounds.Height - text.Height) / 2));
        }
    }

    protected abstract void RenderChart(DrawingContext context, Size size);

    protected IBrush Brush(ChartColor color) => BrushOf(this, color);

    protected IBrush Brush(string key) => BrushOf(this, key);

    /// <param name="maxWidth">Cuts the text off with an ellipsis beyond this width.</param>
    protected FormattedText Label(
        string text,
        double size = 11,
        string key = ThemeKeys.TextTertiary,
        double maxWidth = double.PositiveInfinity,
        FontWeight weight = FontWeight.Normal)
    {
        var family = this.TryFindResource(ThemeKeys.UiFont, ActualThemeVariant, out var value) && value is FontFamily font
            ? font
            : FontFamily.Default;
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(family, FontStyle.Normal, weight),
            size,
            Brush(key));
        if (!double.IsPositiveInfinity(maxWidth))
        {
            formatted.MaxTextWidth = Math.Max(1, maxWidth);
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }

        return formatted;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var tip = HasData ? TooltipAt(e.GetPosition(this)) : null;
        ToolTip.SetTip(this, tip);
        ToolTip.SetIsOpen(this, tip is not null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetIsOpen(this, false);
        ToolTip.SetTip(this, null);
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~ChartTests|FullyQualifiedName~ThemeTests"`
Expected: PASS, including the existing icon tests (`Insights` has a PUA codepoint that no other icon uses).

- [ ] **Step 8: Commit**

```bash
git add -A src/Mcacc.App tests/Mcacc.App.Tests
git commit -F <message-file>   # "feat(app): add the chart colors, the insights icon and the chart basics"
```

---

### Task 13: Chart controls

**Files:**
- Create: `src/Mcacc.App/Controls/LineChart.cs`, `src/Mcacc.App/Controls/StackedBarChart.cs`, `src/Mcacc.App/Controls/DonutChart.cs`, `src/Mcacc.App/Controls/BarList.cs`, `src/Mcacc.App/Controls/ChartSwatch.cs`
- Test: `tests/Mcacc.App.Tests/Controls/ControlTests.cs`

**Interfaces:**
- Consumes: `ChartControl`, `ChartScale` and the chart records (Task 12).
- Produces:
  - `LineChart`:
    - `Series` (`IReadOnlyList<LineSeries>?`), `Minimum` (0), `Maximum` (100), `Threshold` (NaN = none), `XMin` (0), `XMax` (1), `MaxGap` (∞), `YSuffix` (" %"), `StartLabel`, `EndLabel`
    - internal consts `AxisWidth` (36) and `RightPadding` (6)
  - `StackedBarChart`: `Bars` (`IReadOnlyList<BarStack>?`), `AxisFormatter` (`Func<double, string>?`), internal const `AxisWidth` (48)
  - `DonutChart`: `Slices` (`IReadOnlyList<ChartSlice>?`), `CenterText`, `CenterCaption`, `Thickness` (14)
  - `BarList`: `Items` (`IReadOnlyList<BarListItem>?`), internal const `RowHeight` (28). Its height is the number of rows times `RowHeight`.
  - `ChartSwatch`: `Color` (`ChartColor`), a 10 × 10 rounded square

Every chart takes `EmptyText` from `ChartControl`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/Mcacc.App.Tests/Controls/ControlTests.cs`. The file already has `using Avalonia.Headless.XUnit;`; add `using Avalonia.Headless;` for `CaptureRenderedFrame` and `MouseMove`.

```csharp
    private static void Rendered(Window window)
    {
        window.UpdateLayout();
        using var frame = window.CaptureRenderedFrame();
        frame.ShouldNotBeNull();
    }

    private static LineChart TwoPointLine() => new()
    {
        Width = 360,
        Height = 160,
        XMin = 0,
        XMax = 1,
        Threshold = 90,
        StartLabel = "vor 24 h",
        EndLabel = "jetzt",
        Series =
        [
            new LineSeries("5h", ChartColor.Series1, [new ChartPoint(0, 20, "a"), new ChartPoint(1, 95, "b")]),
            new LineSeries("Woche", ChartColor.Series2, [new ChartPoint(0, 5, "a"), new ChartPoint(1, 7, "b")]),
        ],
    };

    [AvaloniaFact]
    public void Every_chart_renders_with_data_and_empty()
    {
        Rendered(Show(TwoPointLine()));
        Rendered(Show(new LineChart { Width = 360, Height = 160, EmptyText = "leer" }));
        Rendered(Show(new StackedBarChart
        {
            Width = 360,
            Height = 160,
            AxisFormatter = value => $"{value:0} k",
            Bars = [new BarStack("1.9.", [new BarSegment(3, ChartColor.Series1), new BarSegment(2, ChartColor.Series2)])],
        }));
        Rendered(Show(new StackedBarChart { Width = 360, Height = 160, EmptyText = "leer" }));
        Rendered(Show(new DonutChart
        {
            Width = 120,
            Height = 120,
            CenterText = "12 M",
            CenterCaption = "Tokens",
            Slices = [new ChartSlice("Input", 1, ChartColor.Series1), new ChartSlice("Output", 3, ChartColor.Series2)],
        }));
        Rendered(Show(new DonutChart { Width = 120, Height = 120, Slices = [new ChartSlice("Input", 5, ChartColor.Series1)] }));
        Rendered(Show(new DonutChart { Width = 120, Height = 120, EmptyText = "leer" }));
        Rendered(Show(new BarList { Width = 360, Items = [new BarListItem("Mcacc", 5, "5 k", ChartColor.Accent)] }));
        Rendered(Show(new ChartSwatch { Color = ChartColor.Series3 }));
    }

    [AvaloniaFact]
    public void A_line_chart_tells_the_point_nearest_to_the_pointer()
    {
        var chart = TwoPointLine();
        Show(chart);

        chart.TooltipAt(new Point(LineChart.AxisWidth + 2, 50)).ShouldBe("a");
        chart.TooltipAt(new Point(chart.Bounds.Width - LineChart.RightPadding - 2, 50)).ShouldBe("b");
        chart.TooltipAt(new Point(chart.Bounds.Width / 2, 50)).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Moving_the_pointer_over_a_chart_sets_its_tooltip()
    {
        var chart = TwoPointLine();
        var window = Show(chart);

        var point = chart.TranslatePoint(new Point(chart.Bounds.Width - LineChart.RightPadding - 2, 50), window)!.Value;
        window.MouseMove(point);

        ToolTip.GetTip(chart).ShouldBe("b");
    }

    [AvaloniaFact]
    public void A_bar_chart_tells_the_bar_under_the_pointer()
    {
        var chart = new StackedBarChart
        {
            Width = 360,
            Height = 160,
            Bars =
            [
                new BarStack("1.9.", [new BarSegment(1, ChartColor.Series1)], "erster"),
                new BarStack("2.9.", [new BarSegment(2, ChartColor.Series1)], "zweiter"),
            ],
        };
        Show(chart);

        chart.TooltipAt(new Point(StackedBarChart.AxisWidth + 1, 40)).ShouldBe("erster");
        chart.TooltipAt(new Point(chart.Bounds.Width - 1, 40)).ShouldBe("zweiter");
        chart.TooltipAt(new Point(1, 40)).ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_donut_tells_the_slice_under_the_pointer()
    {
        var chart = new DonutChart
        {
            Width = 120,
            Height = 120,
            Slices = [new ChartSlice("a", 1, ChartColor.Series1, "rechts"), new ChartSlice("b", 1, ChartColor.Series2, "links")],
        };
        Show(chart);

        // The first slice starts at the top and runs clockwise over the right half.
        chart.TooltipAt(new Point(61, 9)).ShouldBe("rechts");
        chart.TooltipAt(new Point(59, 9)).ShouldBe("links");
        chart.TooltipAt(new Point(60, 60)).ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_bar_list_is_one_row_per_item_high_and_tells_the_row_under_the_pointer()
    {
        var list = new BarList
        {
            Width = 360,
            Items =
            [
                new BarListItem("A", 3, "3", ChartColor.Accent, "eins"),
                new BarListItem("B", 2, "2", ChartColor.Accent, "zwei"),
                new BarListItem("C", 1, "1", ChartColor.Accent, "drei"),
            ],
        };
        var window = Show(new StackPanel { Children = { list } });
        window.UpdateLayout();

        list.Bounds.Height.ShouldBe(3 * BarList.RowHeight);
        list.TooltipAt(new Point(10, BarList.RowHeight + 5)).ShouldBe("zwei");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~ControlTests"`
Expected: the build fails because `LineChart`, `StackedBarChart`, `DonutChart`, `BarList` and `ChartSwatch` are unknown.

- [ ] **Step 3: Implement `LineChart`**

Create `src/Mcacc.App/Controls/LineChart.cs`:

```csharp
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>
/// Lines over a value axis from <see cref="Minimum"/> to <see cref="Maximum"/>, with an optional dashed threshold.
/// A line breaks where two points lie further apart than <see cref="MaxGap"/>, e.g. while the app was closed.
/// </summary>
public sealed class LineChart : ChartControl
{
    internal const double AxisWidth = 36;
    internal const double RightPadding = 6;
    private const double TopPadding = 6;
    private const double BottomLabels = 18;
    private const double HitDistance = 12;

    public static readonly StyledProperty<IReadOnlyList<LineSeries>?> SeriesProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<LineSeries>?>(nameof(Series));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(Threshold), double.NaN);

    public static readonly StyledProperty<double> XMinProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(XMin));

    public static readonly StyledProperty<double> XMaxProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(XMax), 1);

    public static readonly StyledProperty<double> MaxGapProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(MaxGap), double.PositiveInfinity);

    public static readonly StyledProperty<string> YSuffixProperty =
        AvaloniaProperty.Register<LineChart, string>(nameof(YSuffix), " %");

    public static readonly StyledProperty<string?> StartLabelProperty =
        AvaloniaProperty.Register<LineChart, string?>(nameof(StartLabel));

    public static readonly StyledProperty<string?> EndLabelProperty =
        AvaloniaProperty.Register<LineChart, string?>(nameof(EndLabel));

    static LineChart()
    {
        AffectsRender<LineChart>(
            SeriesProperty,
            MinimumProperty,
            MaximumProperty,
            ThresholdProperty,
            XMinProperty,
            XMaxProperty,
            MaxGapProperty,
            YSuffixProperty,
            StartLabelProperty,
            EndLabelProperty);
    }

    public IReadOnlyList<LineSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Drawn as a dashed line in the danger color; NaN draws none.</summary>
    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public double XMin
    {
        get => GetValue(XMinProperty);
        set => SetValue(XMinProperty, value);
    }

    public double XMax
    {
        get => GetValue(XMaxProperty);
        set => SetValue(XMaxProperty, value);
    }

    /// <summary>In x units: two points further apart are not connected.</summary>
    public double MaxGap
    {
        get => GetValue(MaxGapProperty);
        set => SetValue(MaxGapProperty, value);
    }

    public string YSuffix
    {
        get => GetValue(YSuffixProperty);
        set => SetValue(YSuffixProperty, value);
    }

    /// <summary>Written under the left end of the x axis.</summary>
    public string? StartLabel
    {
        get => GetValue(StartLabelProperty);
        set => SetValue(StartLabelProperty, value);
    }

    /// <summary>Written under the right end of the x axis.</summary>
    public string? EndLabel
    {
        get => GetValue(EndLabelProperty);
        set => SetValue(EndLabelProperty, value);
    }

    protected override bool HasData => Series?.Any(series => series.Points.Count > 0) == true;

    public override string? TooltipAt(Point point)
    {
        var plot = Plot(Bounds.Size);
        var nearest = (Series ?? [])
            .SelectMany(series => series.Points)
            .Select(p => (Point: p, Distance: Math.Abs(X(p.X, plot) - point.X)))
            .Where(entry => entry.Distance <= HitDistance)
            .OrderBy(entry => entry.Distance)
            .Select(entry => entry.Point)
            .FirstOrDefault();
        return nearest?.Tooltip;
    }

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var plot = Plot(size);
        var track = new Pen(Brush(ChartColor.Track), 1);
        foreach (var tick in ChartScale.Ticks(Maximum - Minimum))
        {
            var value = Minimum + tick;
            var y = Math.Round(Y(value, plot)) + 0.5;
            context.DrawLine(track, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = Label(string.Create(CultureInfo.CurrentCulture, $"{value:0}{YSuffix}"), 10);
            context.DrawText(label, new Point(plot.Left - 6 - label.Width, y - label.Height / 2));
        }

        if (!double.IsNaN(Threshold))
        {
            var y = Y(Threshold, plot);
            var dashed = new Pen(Brush(ChartColor.Danger), 1, new DashStyle([4, 3], 0));
            context.DrawLine(dashed, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        foreach (var series in Series ?? [])
        {
            DrawSeries(context, series, plot);
        }

        if (StartLabel is { } start)
        {
            context.DrawText(Label(start, 10), new Point(plot.Left, plot.Bottom + 4));
        }

        if (EndLabel is { } end)
        {
            var label = Label(end, 10);
            context.DrawText(label, new Point(plot.Right - label.Width, plot.Bottom + 4));
        }
    }

    private void DrawSeries(DrawingContext context, LineSeries series, Rect plot)
    {
        var brush = Brush(series.Color);
        var pen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var points = series.Points.OrderBy(p => p.X).ToList();
        var run = new List<Point>();
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0 && points[i].X - points[i - 1].X > MaxGap)
            {
                DrawRun(context, run, pen, brush);
                run.Clear();
            }

            run.Add(new Point(X(points[i].X, plot), Y(points[i].Y, plot)));
        }

        DrawRun(context, run, pen, brush);
    }

    /// <summary>A lone point has no line to show it, so it gets a dot.</summary>
    private static void DrawRun(DrawingContext context, List<Point> run, Pen pen, IBrush brush)
    {
        if (run.Count == 1)
        {
            context.DrawEllipse(brush, null, run[0], 2.5, 2.5);
            return;
        }

        if (run.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(run[0], isFilled: false);
            foreach (var point in run.Skip(1))
            {
                figure.LineTo(point);
            }

            figure.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static Rect Plot(Size size) => new(
        AxisWidth,
        TopPadding,
        Math.Max(1, size.Width - AxisWidth - RightPadding),
        Math.Max(1, size.Height - TopPadding - BottomLabels));

    private double X(double x, Rect plot) => ChartScale.Map(x, XMin, XMax, plot.Left, plot.Right);

    private double Y(double y, Rect plot) => ChartScale.Map(y, Minimum, Maximum, plot.Bottom, plot.Top);
}
```

- [ ] **Step 4: Implement `StackedBarChart`**

Create `src/Mcacc.App/Controls/StackedBarChart.cs`:

```csharp
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Mcacc.App.Controls;

/// <summary>One stacked bar per entry, e.g. per day; labels are thinned out so they do not overlap.</summary>
public sealed class StackedBarChart : ChartControl
{
    internal const double AxisWidth = 48;
    private const double RightPadding = 4;
    private const double TopPadding = 6;
    private const double BottomLabels = 18;
    private const double MaxBarWidth = 28;
    private const double LabelSpace = 36;

    public static readonly StyledProperty<IReadOnlyList<BarStack>?> BarsProperty =
        AvaloniaProperty.Register<StackedBarChart, IReadOnlyList<BarStack>?>(nameof(Bars));

    public static readonly StyledProperty<Func<double, string>?> AxisFormatterProperty =
        AvaloniaProperty.Register<StackedBarChart, Func<double, string>?>(nameof(AxisFormatter));

    static StackedBarChart()
    {
        AffectsRender<StackedBarChart>(BarsProperty, AxisFormatterProperty);
    }

    public IReadOnlyList<BarStack>? Bars
    {
        get => GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    /// <summary>Writes the values of the axis; without it they are plain numbers.</summary>
    public Func<double, string>? AxisFormatter
    {
        get => GetValue(AxisFormatterProperty);
        set => SetValue(AxisFormatterProperty, value);
    }

    protected override bool HasData => Bars?.Any(bar => bar.Total > 0) == true;

    public override string? TooltipAt(Point point)
    {
        var bars = Bars ?? [];
        var plot = Plot(Bounds.Size);
        var index = ChartScale.BandAt(point.X - plot.Left, plot.Width, bars.Count);
        return index < 0 ? null : bars[index].Tooltip;
    }

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var bars = Bars!;
        var plot = Plot(size);
        var max = ChartScale.NiceMax(bars.Max(bar => bar.Total));
        var track = new Pen(Brush(ChartColor.Track), 1);
        foreach (var tick in ChartScale.Ticks(max))
        {
            var y = Math.Round(ChartScale.Map(tick, 0, max, plot.Bottom, plot.Top)) + 0.5;
            context.DrawLine(track, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = AxisFormatter?.Invoke(tick) ?? tick.ToString("0", CultureInfo.CurrentCulture);
            var label = Label(text, 10);
            context.DrawText(label, new Point(plot.Left - 6 - label.Width, y - label.Height / 2));
        }

        var band = plot.Width / bars.Count;
        var width = Math.Min(MaxBarWidth, band * 0.7);
        var every = Math.Max(1, (int)Math.Ceiling(LabelSpace / band));
        for (var i = 0; i < bars.Count; i++)
        {
            var center = plot.Left + band * (i + 0.5);
            var sum = 0d;
            var bottom = plot.Bottom;
            foreach (var segment in bars[i].Segments.Where(segment => segment.Value > 0))
            {
                sum += segment.Value;
                var top = ChartScale.Map(sum, 0, max, plot.Bottom, plot.Top);
                context.FillRectangle(Brush(segment.Color), new Rect(center - width / 2, top, width, bottom - top));
                bottom = top;
            }

            if (i % every == 0)
            {
                var label = Label(bars[i].Label, 10, maxWidth: band * every);
                context.DrawText(label, new Point(center - label.Width / 2, plot.Bottom + 4));
            }
        }
    }

    private static Rect Plot(Size size) => new(
        AxisWidth,
        TopPadding,
        Math.Max(1, size.Width - AxisWidth - RightPadding),
        Math.Max(1, size.Height - TopPadding - BottomLabels));
}
```

The segments stack on a running sum, so each one starts where the one below it ended.

- [ ] **Step 5: Implement `DonutChart`**

Create `src/Mcacc.App/Controls/DonutChart.cs`:

```csharp
using Avalonia;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>A ring of slices, starting at the top and running clockwise, with a text in the middle.</summary>
public sealed class DonutChart : ChartControl
{
    private const double Margin = 2;

    public static readonly StyledProperty<IReadOnlyList<ChartSlice>?> SlicesProperty =
        AvaloniaProperty.Register<DonutChart, IReadOnlyList<ChartSlice>?>(nameof(Slices));

    public static readonly StyledProperty<string?> CenterTextProperty =
        AvaloniaProperty.Register<DonutChart, string?>(nameof(CenterText));

    public static readonly StyledProperty<string?> CenterCaptionProperty =
        AvaloniaProperty.Register<DonutChart, string?>(nameof(CenterCaption));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Thickness), 14);

    static DonutChart()
    {
        AffectsRender<DonutChart>(SlicesProperty, CenterTextProperty, CenterCaptionProperty, ThicknessProperty);
    }

    public IReadOnlyList<ChartSlice>? Slices
    {
        get => GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public string? CenterText
    {
        get => GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string? CenterCaption
    {
        get => GetValue(CenterCaptionProperty);
        set => SetValue(CenterCaptionProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    private double Total => Slices?.Where(slice => slice.Value > 0).Sum(slice => slice.Value) ?? 0;

    protected override bool HasData => Total > 0;

    public override string? TooltipAt(Point point)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = Radius(Bounds.Size);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < radius - Thickness - Margin || distance > radius + Margin)
        {
            return null;
        }

        // Degrees clockwise from the top, like the slices.
        var angle = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
        var start = 0d;
        foreach (var slice in (Slices ?? []).Where(slice => slice.Value > 0))
        {
            var sweep = slice.Value / Total * 360;
            if (angle < start + sweep)
            {
                return slice.Tooltip;
            }

            start += sweep;
        }

        return null;
    }

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var center = new Point(size.Width / 2, size.Height / 2);
        var ring = Radius(size) - Thickness / 2;
        var slices = Slices!.Where(slice => slice.Value > 0).ToList();
        if (slices.Count == 1)
        {
            context.DrawEllipse(null, new Pen(Brush(slices[0].Color), Thickness), center, ring, ring);
        }
        else
        {
            var start = 0d;
            foreach (var slice in slices)
            {
                var sweep = slice.Value / Total * 360;
                DrawArc(context, center, ring, start, sweep, new Pen(Brush(slice.Color), Thickness, lineCap: PenLineCap.Flat));
                start += sweep;
            }
        }

        var y = center.Y;
        if (CenterText is { } text)
        {
            var label = Label(text, 18, ThemeKeys.Text, ring * 1.6, FontWeight.SemiBold);
            y -= CenterCaption is null ? label.Height / 2 : label.Height * 0.75;
            context.DrawText(label, new Point(center.X - label.Width / 2, y));
            y += label.Height;
        }

        if (CenterCaption is { } caption)
        {
            var label = Label(caption, 11, maxWidth: ring * 1.6);
            context.DrawText(label, new Point(center.X - label.Width / 2, CenterText is null ? y - label.Height / 2 : y));
        }
    }

    private static void DrawArc(DrawingContext context, Point center, double radius, double start, double sweep, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(PointAt(center, radius, start), isFilled: false);
            figure.ArcTo(
                PointAt(center, radius, start + sweep),
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: sweep > 180,
                SweepDirection.Clockwise);
            figure.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    /// <param name="degrees">Clockwise from the top.</param>
    private static Point PointAt(Point center, double radius, double degrees)
    {
        var radians = (degrees - 90) * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private static double Radius(Size size) => Math.Max(1, Math.Min(size.Width, size.Height) / 2 - Margin);
}
```

- [ ] **Step 6: Implement `BarList` and `ChartSwatch`**

Create `src/Mcacc.App/Controls/BarList.cs`:

```csharp
using Avalonia;
using Avalonia.Media;
using Mcacc.App.Theme;

namespace Mcacc.App.Controls;

/// <summary>Horizontal bars, one row per item: label, bar relative to the largest value, value text.</summary>
public sealed class BarList : ChartControl
{
    internal const double RowHeight = 28;
    private const double BarHeight = 6;

    public static readonly StyledProperty<IReadOnlyList<BarListItem>?> ItemsProperty =
        AvaloniaProperty.Register<BarList, IReadOnlyList<BarListItem>?>(nameof(Items));

    static BarList()
    {
        AffectsRender<BarList>(ItemsProperty);
        AffectsMeasure<BarList>(ItemsProperty);
    }

    public IReadOnlyList<BarListItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override bool HasData => Items is { Count: > 0 };

    public override string? TooltipAt(Point point)
    {
        var items = Items ?? [];
        var row = (int)Math.Floor(point.Y / RowHeight);
        return row >= 0 && row < items.Count ? items[row].Tooltip : null;
    }

    /// <summary>Without items the list keeps one row, so the empty text has room.</summary>
    protected override Size MeasureOverride(Size availableSize) =>
        new(0, Math.Max(1, Items?.Count ?? 0) * RowHeight);

    protected override void RenderChart(DrawingContext context, Size size)
    {
        var items = Items!;
        var max = Math.Max(items.Max(item => item.Value), double.Epsilon);
        var labelWidth = size.Width * 0.38;
        var barLeft = labelWidth + 8;
        var barWidth = Math.Max(1, size.Width * 0.84 - barLeft);
        var track = Brush(ChartColor.Track);
        for (var i = 0; i < items.Count; i++)
        {
            var middle = i * RowHeight + RowHeight / 2;
            var label = Label(items[i].Label, 12, ThemeKeys.Text, labelWidth);
            context.DrawText(label, new Point(0, middle - label.Height / 2));

            var bar = new Rect(barLeft, middle - BarHeight / 2, barWidth, BarHeight);
            context.DrawRectangle(track, null, bar, BarHeight / 2, BarHeight / 2);
            var fill = bar.WithWidth(Math.Max(BarHeight, barWidth * items[i].Value / max));
            context.DrawRectangle(Brush(items[i].Color), null, fill, BarHeight / 2, BarHeight / 2);

            var value = Label(items[i].ValueText, 11, ThemeKeys.TextSecondary);
            context.DrawText(value, new Point(size.Width - value.Width, middle - value.Height / 2));
        }
    }
}
```

Create `src/Mcacc.App/Controls/ChartSwatch.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mcacc.App.Controls;

/// <summary>The small color square of a legend entry.</summary>
public sealed class ChartSwatch : Control
{
    private const double Side = 10;

    public static readonly StyledProperty<ChartColor> ColorProperty =
        AvaloniaProperty.Register<ChartSwatch, ChartColor>(nameof(Color));

    static ChartSwatch()
    {
        AffectsRender<ChartSwatch>(ColorProperty);
    }

    public ChartSwatch()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public ChartColor Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public override void Render(DrawingContext context) =>
        context.DrawRectangle(ChartControl.BrushOf(this, Color), null, new Rect(0, 0, Side, Side), 2, 2);

    protected override Size MeasureOverride(Size availableSize) => new(Side, Side);
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~ControlTests|FullyQualifiedName~ChartTests"`
Expected: PASS.

Two layout facts behind the expected values:
- The donut tooltip test uses a 120 × 120 chart. That gives a radius of 58 and a ring from 44 to 58 px from the center (60, 60), so `(61, 9)` lies on the ring just right of the top.
- The line chart test uses a width of 360. The plot runs from x = 36 to 354, so the middle (x = 180) is more than 12 px away from both points.

- [ ] **Step 8: Commit**

```bash
git add -A src/Mcacc.App tests/Mcacc.App.Tests
git commit -F <message-file>   # "feat(app): draw line, stacked bar, donut and bar list charts"
```

---

### Task 14: Texts of the info page

**Files:**
- Modify: `src/Mcacc.App/Localization/Texts.cs` (`static class` → `static partial class`)
- Create: `src/Mcacc.App/Localization/Texts.Info.cs`
- Test: `tests/Mcacc.App.Tests/Localization/TextsTests.cs` (new)

**Interfaces:**
- Consumes:
  - `ModelPricing.Normalize(string)` (Task 8)
  - `DailyUsage` (Task 9)
- Produces (all on `Texts`). The formatters use a fixed German culture, so the tests do not depend on the machine.
  - Constants:
    - Page and sections: `Info`, `InfoSubtitle`, `InfoAccount`, `InfoLimits`, `InfoUsage`
    - Field labels: `InfoEmail`, `InfoName`, `InfoPlan`, `InfoOrganization`, `InfoRole`, `InfoBilling`, `InfoRateLimitTier`, `InfoCreatedAt`, `InfoTokenExpires`, `InfoScopes`, `InfoStartups`, `InfoInstallMethod`, `InfoProjects`, `InfoDesktop`, `InfoDesktopNotRunning`, `InfoNoAccountData`, `InfoNoLimits`
    - History: `InfoHistory`, `InfoHistoryDay`, `InfoHistoryWeek`, `InfoNoHistory`, `InfoHistoryDayStart`, `InfoHistoryWeekStart`, `InfoNow`
    - KPIs: `InfoTokens`, `InfoCost`, `InfoCostNote`, `InfoSessions`, `InfoMessages`, `InfoActiveDays`
    - Charts: `InfoTokensPerDay`, `InfoTokenKinds`, `InfoTopProjects`, `InfoModels`
    - Token kinds: `InfoKindInput`, `InfoKindOutput`, `InfoKindCacheWrite`, `InfoKindCacheRead`
    - Model table: `InfoColumnModel`, `InfoColumnTokens`, `InfoColumnShare`, `InfoColumnCost`, `InfoOtherModels`
    - Status: `InfoNoSessions`, `InfoScanning`, `InfoScanFailed`, `InfoNoProject`, `InfoNoValue` ("—")
  - Methods:
    - Amounts: `TokenAmount(long)`, `Count(long)`, `Cost(decimal)`, `Percent(double)`, `Share(double fraction)`, `AmountTooltip(string label, long value, long total)`
    - Account fields: `Plan(bool? hasMax, bool? hasPro, string? subscriptionType)` (returns `string?`), `Humanize(string)`, `UsageExtraWindow(string key)`, `InfoDate(DateTimeOffset)`, `InfoDateTime(DateTimeOffset)`, `InfoSince(DateTimeOffset start, DateTimeOffset now)`
    - Names: `ProjectName(string cwd)`, `ModelName(string model)`
    - Charts and status: `DayLabel(DateOnly)`, `DayTooltip(DailyUsage)`, `HistoryTooltip(DateTimeOffset at, double? fiveHour, double? sevenDay)`, `ProjectTooltip(string project, long tokens, decimal cost)`, `InfoDays(int)`, `InfoActiveDaysOf(int active, int days)`, `InfoScanProgress(double)`, `InfoUnpriced(long)`

- [ ] **Step 1: Write the failing tests**

Create `tests/Mcacc.App.Tests/Localization/TextsTests.cs`:

```csharp
using System.Globalization;
using Mcacc.App.Localization;
using Mcacc.Core.Stats;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.Localization;

public class TextsTests
{
    private static DateTimeOffset Local(int month, int day, int hour, int minute) =>
        new(new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Local));

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1_234, "1,2 k")]
    [InlineData(12_345, "12,3 k")]
    [InlineData(123_456, "123 k")]
    [InlineData(1_234_567, "1,2 M")]
    [InlineData(2_500_000_000, "2,5 Mrd.")]
    public void Token_amounts_are_short(long tokens, string expected)
    {
        Texts.TokenAmount(tokens).ShouldBe(expected);
    }

    [Theory]
    [InlineData("0", "≈ 0,00 $")]
    [InlineData("0.004", "≈ < 0,01 $")]
    [InlineData("1234.567", "≈ 1.234,57 $")]
    public void Costs_are_marked_as_an_estimate(string cost, string expected)
    {
        Texts.Cost(decimal.Parse(cost, CultureInfo.InvariantCulture)).ShouldBe(expected);
    }

    [Fact]
    public void Percentages_and_shares_use_a_german_comma()
    {
        Texts.Percent(42.4).ShouldBe("42 %");
        Texts.Share(0.125).ShouldBe("12,5 %");
        Texts.Count(12_345).ShouldBe("12.345");
    }

    [Fact]
    public void Amount_tooltips_name_the_share_of_the_total()
    {
        Texts.AmountTooltip("Input", 250, 1_000).ShouldBe("Input: 250 · 25,0 %");
        Texts.AmountTooltip("Input", 0, 0).ShouldBe("Input: 0");
        Texts.ProjectTooltip("/home/ben/shop", 1_500, 0.5m).ShouldBe("/home/ben/shop\n1,5 k Tokens · ≈ 0,50 $");
        Texts.ProjectTooltip("", 1_500, 0.5m).ShouldStartWith(Texts.InfoNoProject);
    }

    [Theory]
    [InlineData(true, false, null, "Claude Max")]
    [InlineData(false, true, null, "Claude Pro")]
    [InlineData(null, null, "max", "Claude Max")]
    [InlineData(null, null, "team", "Claude Team")]
    [InlineData(null, null, "enterprise", "Claude Enterprise")]
    [InlineData(false, false, "free_trial", "Free trial")]
    [InlineData(null, null, null, null)]
    public void The_plan_comes_from_the_profile_or_the_credentials(
        bool? hasMax,
        bool? hasPro,
        string? subscriptionType,
        string? expected)
    {
        Texts.Plan(hasMax, hasPro, subscriptionType).ShouldBe(expected);
    }

    [Theory]
    [InlineData("seven_day_opus", "Woche Opus")]
    [InlineData("seven_day_oauth_apps", "Woche Oauth apps")]
    [InlineData("monthly", "Monthly")]
    public void Extra_windows_get_a_readable_name(string key, string expected)
    {
        Texts.UsageExtraWindow(key).ShouldBe(expected);
    }

    [Fact]
    public void Dates_are_german_and_local()
    {
        Texts.InfoDate(Local(9, 4, 8, 5)).ShouldBe("04.09.2026");
        Texts.InfoDateTime(Local(9, 4, 8, 5)).ShouldBe("04.09.2026 08:05");
    }

    [Fact]
    public void Since_names_the_day_only_when_it_is_not_today()
    {
        Texts.InfoSince(Local(9, 24, 8, 5), Local(9, 24, 12, 0)).ShouldBe("läuft seit 08:05");
        Texts.InfoSince(Local(9, 22, 8, 5), Local(9, 24, 12, 0)).ShouldBe("läuft seit 22.09. 08:05");
    }

    [Theory]
    [InlineData(@"C:\Users\ben\RiderProjects\MCACC", "MCACC")]
    [InlineData("/home/ben/code/shop/", "shop")]
    [InlineData("", Texts.InfoNoProject)]
    public void A_project_is_named_after_its_folder(string cwd, string expected)
    {
        Texts.ProjectName(cwd).ShouldBe(expected);
    }

    [Theory]
    [InlineData("claude-opus-4-5-20251101", "Opus 4.5")]
    [InlineData("claude-opus-4-6[1m]", "Opus 4.6")]
    [InlineData("claude-fable-5-1", "Fable 5.1")]
    [InlineData("claude-opus-5", "Opus 5")]
    [InlineData("claude-haiku-4-5-20251001", "Haiku 4.5")]
    [InlineData("claude-3-5-sonnet-20241022", "Sonnet 3.5")]
    [InlineData("claude-3-opus-20240229", "Opus 3")]
    [InlineData("<synthetic>", "<synthetic>")]
    public void Model_ids_become_readable_names(string model, string expected)
    {
        Texts.ModelName(model).ShouldBe(expected);
    }

    [Fact]
    public void A_day_tooltip_lists_the_models_largest_first()
    {
        var day = new DailyUsage(
            new DateOnly(2026, 9, 23),
            new Dictionary<string, long> { ["claude-haiku-4-5"] = 2_000, ["claude-opus-4-5"] = 1_500_000 });

        Texts.DayLabel(day.Day).ShouldBe("23.09.");
        Texts.DayTooltip(day).ShouldBe("Mittwoch, 23.09. · 1,5 M Tokens\nOpus 4.5: 1,5 M\nHaiku 4.5: 2 k");
    }

    [Fact]
    public void A_history_tooltip_shows_both_windows()
    {
        Texts.HistoryTooltip(Local(9, 23, 14, 5), 42, null).ShouldBe("23.09. 14:05\n5h: 42 %\nWoche: —");
    }

    [Fact]
    public void Counts_and_progress_read_naturally()
    {
        Texts.InfoDays(30).ShouldBe("30 Tage");
        Texts.InfoActiveDaysOf(12, 30).ShouldBe("12 von 30");
        Texts.InfoScanProgress(0.426).ShouldBe("Logs werden gelesen … 43 %");
        Texts.InfoUnpriced(1_234_567).ShouldBe("1,2 M Tokens ohne bekannten Preis");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~TextsTests"`
Expected: the build fails because `Texts.TokenAmount` and the other new members do not exist.

- [ ] **Step 3: Make `Texts` partial**

In `src/Mcacc.App/Localization/Texts.cs`, change `public static class Texts` to `public static partial class Texts`.

- [ ] **Step 4: Add the info texts**

Create `src/Mcacc.App/Localization/Texts.Info.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using Mcacc.Core.Stats;

namespace Mcacc.App.Localization;

public static partial class Texts
{
    /// <summary>The info page formats with a fixed culture: the app is German, whatever the machine says.</summary>
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    // Info page
    public const string Info = "Info & Statistik";
    public const string InfoSubtitle = "Alles, was sich über diesen Account auslesen lässt.";
    public const string InfoAccount = "Konto";
    public const string InfoLimits = "Limits";
    public const string InfoUsage = "Claude Code Nutzung";

    public const string InfoEmail = "E-Mail";
    public const string InfoName = "Name";
    public const string InfoPlan = "Plan";
    public const string InfoOrganization = "Organisation";
    public const string InfoRole = "Rolle";
    public const string InfoBilling = "Abrechnung";
    public const string InfoRateLimitTier = "Rate-Limit-Stufe";
    public const string InfoCreatedAt = "Konto erstellt";
    public const string InfoTokenExpires = "Token gültig bis";
    public const string InfoScopes = "Berechtigungen";
    public const string InfoStartups = "Claude-Code-Starts";
    public const string InfoInstallMethod = "Installation";
    public const string InfoProjects = "Projekte";
    public const string InfoDesktop = "Claude Desktop";
    public const string InfoDesktopNotRunning = "läuft nicht";
    public const string InfoNoAccountData = "Noch keine Kontodaten – einmal mit diesem Account anmelden.";
    public const string InfoNoLimits = "Keine Limits verfügbar.";

    public const string InfoHistory = "Verlauf";
    public const string InfoHistoryDay = "24 h";
    public const string InfoHistoryWeek = "7 Tage";
    public const string InfoNoHistory = "Noch kein Verlauf – er wächst, solange MCACC offen ist.";
    public const string InfoHistoryDayStart = "vor 24 h";
    public const string InfoHistoryWeekStart = "vor 7 Tagen";
    public const string InfoNow = "jetzt";

    public const string InfoTokens = "Tokens";
    public const string InfoCost = "Kosten";
    public const string InfoCostNote = "API-Äquivalent, Schätzung";
    public const string InfoSessions = "Sessions";
    public const string InfoMessages = "Nachrichten";
    public const string InfoActiveDays = "Aktive Tage";

    public const string InfoTokensPerDay = "Tokens pro Tag";
    public const string InfoTokenKinds = "Token-Arten";
    public const string InfoTopProjects = "Top-Projekte";
    public const string InfoModels = "Modelle";

    public const string InfoKindInput = "Input";
    public const string InfoKindOutput = "Output";
    public const string InfoKindCacheWrite = "Cache-Write";
    public const string InfoKindCacheRead = "Cache-Read";

    public const string InfoColumnModel = "Modell";
    public const string InfoColumnTokens = "Tokens";
    public const string InfoColumnShare = "Anteil";
    public const string InfoColumnCost = "≈ Kosten";
    public const string InfoOtherModels = "Andere";

    public const string InfoNoSessions = "Keine Claude-Code-Sessions in diesem Zeitraum.";
    public const string InfoScanning = "Logs werden gelesen …";
    public const string InfoScanFailed = "Die Claude-Code-Logs ließen sich nicht lesen.";
    public const string InfoNoProject = "(ohne Projekt)";
    public const string InfoNoValue = "—";

    private const string WeekPrefix = "seven_day_";

    public static string TokenAmount(long tokens) => tokens switch
    {
        < 1_000 => tokens.ToString("0", De),
        < 1_000_000 => Scaled(tokens / 1e3, "k"),
        < 1_000_000_000 => Scaled(tokens / 1e6, "M"),
        _ => Scaled(tokens / 1e9, "Mrd."),
    };

    public static string Count(long value) => value.ToString("N0", De);

    public static string Cost(decimal usd) => usd is > 0 and < 0.01m
        ? $"≈ < {0.01m.ToString("N2", De)} $"
        : $"≈ {usd.ToString("N2", De)} $";

    public static string Percent(double value) => $"{value.ToString("0", De)} %";

    /// <param name="fraction">0..1</param>
    public static string Share(double fraction) => $"{(fraction * 100).ToString("0.0", De)} %";

    /// <returns>Null when neither the profile nor the credentials name a plan.</returns>
    public static string? Plan(bool? hasMax, bool? hasPro, string? subscriptionType)
    {
        if (hasMax == true)
        {
            return "Claude Max";
        }

        if (hasPro == true)
        {
            return "Claude Pro";
        }

        return subscriptionType?.ToLowerInvariant() switch
        {
            null or "" => null,
            "max" => "Claude Max",
            "pro" => "Claude Pro",
            "team" => "Claude Team",
            "enterprise" => "Claude Enterprise",
            _ => Humanize(subscriptionType),
        };
    }

    /// <summary>"stripe_subscription" → "Stripe subscription": for API values without a German name.</summary>
    public static string Humanize(string value)
    {
        var text = value.Replace('_', ' ').Trim();
        return text.Length == 0 ? text : char.ToUpper(text[0], De) + text[1..];
    }

    /// <param name="key">The raw API name, e.g. <c>seven_day_opus</c>.</param>
    public static string UsageExtraWindow(string key) => key.StartsWith(WeekPrefix, StringComparison.Ordinal)
        ? $"{UsageSevenDay} {Humanize(key[WeekPrefix.Length..])}"
        : Humanize(key);

    public static string InfoDate(DateTimeOffset at) => at.ToLocalTime().ToString("dd.MM.yyyy", De);

    public static string InfoDateTime(DateTimeOffset at) => at.ToLocalTime().ToString("dd.MM.yyyy HH:mm", De);

    public static string InfoSince(DateTimeOffset start, DateTimeOffset now)
    {
        var local = start.ToLocalTime();
        return local.Date == now.ToLocalTime().Date
            ? $"läuft seit {local.ToString("HH:mm", De)}"
            : $"läuft seit {local.ToString("dd.MM. HH:mm", De)}";
    }

    /// <param name="cwd">The working directory of the session, as the log wrote it.</param>
    public static string ProjectName(string cwd)
    {
        var trimmed = cwd.TrimEnd('/', '\\');
        if (trimmed.Length == 0)
        {
            return InfoNoProject;
        }

        var slash = trimmed.LastIndexOfAny(['/', '\\']);
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    /// <summary>"claude-opus-4-5-20251101" → "Opus 4.5"; unknown shapes stay as they are.</summary>
    public static string ModelName(string model)
    {
        var name = ModelPricing.Normalize(model);
        var match = FamilyFirst().Match(name);
        if (!match.Success)
        {
            match = VersionFirst().Match(name);
        }

        if (!match.Success)
        {
            return model;
        }

        var family = Humanize(match.Groups["family"].Value);
        var minor = match.Groups["minor"];
        return minor.Success
            ? $"{family} {match.Groups["major"].Value}.{minor.Value}"
            : $"{family} {match.Groups["major"].Value}";
    }

    public static string DayLabel(DateOnly day) => day.ToString("dd.MM.", De);

    public static string DayTooltip(DailyUsage day)
    {
        var lines = day.TokensByModel
            .Where(entry => entry.Value > 0)
            .OrderByDescending(entry => entry.Value)
            .Select(entry => $"{ModelName(entry.Key)}: {TokenAmount(entry.Value)}");
        return string.Join('\n', [$"{day.Day.ToString("dddd, dd.MM.", De)} · {TokenAmount(day.Total)} {InfoTokens}", .. lines]);
    }

    public static string HistoryTooltip(DateTimeOffset at, double? fiveHour, double? sevenDay) =>
        $"{at.ToLocalTime().ToString("dd.MM. HH:mm", De)}\n"
        + $"{UsageFiveHour}: {PercentOrDash(fiveHour)}\n"
        + $"{UsageSevenDay}: {PercentOrDash(sevenDay)}";

    public static string AmountTooltip(string label, long value, long total) => total > 0
        ? $"{label}: {TokenAmount(value)} · {Share((double)value / total)}"
        : $"{label}: {TokenAmount(value)}";

    /// <param name="project">The full working directory; the bar list shows only its last folder.</param>
    public static string ProjectTooltip(string project, long tokens, decimal cost) =>
        $"{(project.Length > 0 ? project : InfoNoProject)}\n{TokenAmount(tokens)} {InfoTokens} · {Cost(cost)}";

    public static string InfoDays(int days) => $"{days} Tage";

    public static string InfoActiveDaysOf(int active, int days) => $"{active} von {days}";

    /// <param name="progress">0..1</param>
    public static string InfoScanProgress(double progress) => $"{InfoScanning} {Percent(progress * 100)}";

    public static string InfoUnpriced(long tokens) => $"{TokenAmount(tokens)} Tokens ohne bekannten Preis";

    private static string Scaled(double value, string unit) =>
        $"{value.ToString(value < 100 ? "0.#" : "0", De)} {unit}";

    private static string PercentOrDash(double? value) => value is { } percent ? Percent(percent) : InfoNoValue;

    [GeneratedRegex(@"^claude-(?<family>[a-z]+)-(?<major>\d+)(?:-(?<minor>\d+))?$")]
    private static partial Regex FamilyFirst();

    [GeneratedRegex(@"^claude-(?<major>\d+)(?:-(?<minor>\d+))?-(?<family>[a-z]+)$")]
    private static partial Regex VersionFirst();
}
```

With `"0.#"`, `12_345` becomes `"12,3 k"` and `2_000` becomes `"2 k"`, because the `#` drops a zero decimal. That is what the tooltip test expects. The tooltip writes the full weekday: the abbreviated German names differ between ICU ("Mi.") and NLS ("Mi").

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~TextsTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A src/Mcacc.App/Localization tests/Mcacc.App.Tests/Localization
git commit -F <message-file>   # "feat(app): add the texts and formatters of the info page"
```

---

### Task 15: The info page view model

**Files:**
- Create: `src/Mcacc.App/ViewModels/AccountInfoViewModel.cs`, `src/Mcacc.App/ViewModels/AccountInfoItems.cs`
- Modify: `src/Mcacc.App/ViewModels/AccountTileViewModel.cs` (`HintFor` becomes `internal static`)
- Test: `tests/Mcacc.App.Tests/ViewModels/AccountInfoViewModelTests.cs` (new)

**Interfaces:**
- Consumes:
  - `IUsageProvider.GetCached`, `RefreshAsync`, `GetProfile`, `GetHistory` (Task 5)
  - `UsagePoller.Updated` and the `AppServices` slots `Stats` and `Credentials` (Task 11)
  - `AccountStatsQuery.Compute` (Task 9)
  - the chart records (Task 12)
  - `UsageBar.WarningThreshold`
  - `Texts.*` (Task 14)
- Produces:
  - `AccountInfoViewModel(AppServices services, Account account) : PageViewModel`. The Info view (Task 16) binds these members:
    - Header: `Account`, `Title`, `Subtitle`, `AccountName`, `BackTooltip`, `RefreshTooltip`, `Initials`, `AvatarBrush`
    - Section and label texts: `AccountHeader`, `LimitsHeader`, `HistoryHeader`, `UsageHeader`, `TokensPerDayHeader`, `KindsHeader`, `ProjectsHeader`, `ModelsHeader`, `ColumnModel`, `ColumnTokens`, `ColumnShare`, `ColumnCost`, `CostNote`, `NoAccountDataText`, `NoLimitsText`, `NoHistoryText`, `NoSessionsText`, `HistoryDayText`, `HistoryWeekText`, `HistoryEndLabel`, `Days7Text`, `Days30Text`, `Days90Text`, `KindsCaption`
    - Account: `Fields`, `HasAccountData`
    - Limits: `Rings`, `HasRings`, `LimitsAsOf`, `LimitsHint`
    - History: `IsHistoryDay`, `IsHistoryWeek`, `HistorySeries`, `HistoryMax`, `HistoryGap`, `HistoryStartLabel`, `HistoryLegend`
    - Usage state: `UsageDays`, `IsDays7`, `IsDays30`, `IsDays90`, `IsScanning`, `ScanText`, `StatsError`, `HasSessions`
    - Usage charts: `Kpis`, `DailyBars`, `ModelLegend`, `KindSlices`, `KindLegend`, `KindCenterText`, `Projects`, `Models`, `UnpricedText`, `TokenAxis`
    - Commands: `BackCommand`, `RefreshCommand`, `ShowHistoryCommand` (parameter `"24h"` or `"7d"`), `ShowDaysCommand` (parameter `"7"`, `"30"` or `"90"`)
  - Item records in `AccountInfoItems.cs`:
    - `InfoFieldViewModel(Label, Value)`
    - `KpiViewModel(Label, Value, Caption?)`
    - `LegendItemViewModel(Label, Color, Value?)`
    - `ModelRowViewModel(Name, Tokens, Share, Cost, Color)`
    - `LimitRingViewModel(Label, ValueText, ResetText?, Slices)` with `static From(string label, UsageWindow window)`

Behaviour:
- **Constructor:** shows what is cached, without IO beyond the login file. That covers the limits (`GetCached`), the profile (`GetProfile`), the login (`.claude.json`) and the history of the last 24 h.
- **`ActivateAsync`:**
  - Subscribes to the poller.
  - Runs three steps in parallel on a lifetime token:
    - refresh the limits (then reload the profile and the history)
    - read the stats, with progress
    - read the credentials and the running desktop instance
- **`Deactivate`:** unsubscribes and cancels that token.
- **Failure handling:**
  - Each step logs its failures and never throws.
  - A cancelled step ends silently.
  - A failed log scan shows `Texts.InfoScanFailed`.
- **Credentials:** only `SubscriptionType`, `Scopes` and `ExpiresAt` are kept. The access token never leaves `LoadAccountAsync`.
- **Fields:**
  - The profile wins over the login.
  - Empty fields are left out.
  - The desktop field appears once detection ran.
  - `HasAccountData` ignores the desktop field.
- **Rings:** 5h, Woche, then the extra windows by key. A window without a value gets no ring. At ≥ `UsageBar.WarningThreshold` the ring turns Danger, otherwise it is Accent.
- **Models:**
  - The five largest get `Series1..5`, the rest share `Series6` and the legend entry `Texts.InfoOtherModels`.
  - The daily bars stack one segment per color, so all "other" models form one segment.
- **Keys:** F5 refreshes. Esc, Backspace and Alt+Left go back.

- [ ] **Step 1: Write the failing tests**

Create `tests/Mcacc.App.Tests/ViewModels/AccountInfoViewModelTests.cs`:

```csharp
using Avalonia.Input;
using Mcacc.App.Controls;
using Mcacc.App.Localization;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Stats;
using Mcacc.Core.Usage;
using Shouldly;
using Xunit;

namespace Mcacc.App.Tests.ViewModels;

public sealed class AccountInfoViewModelTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _services.Dispose();

    private AccountInfoViewModel Page() => new(_services.Services, Privat);

    private static string? Field(AccountInfoViewModel page, string label) =>
        page.Fields.FirstOrDefault(field => field.Label == label)?.Value;

    private static AccountUsage Usage(double? fiveHour, double? sevenDay, IReadOnlyDictionary<string, UsageWindow>? extra = null) =>
        new(UsageState.Ok, new UsageSnapshot(
            new UsageWindow(fiveHour, DateTimeOffset.Now.AddHours(2)),
            new UsageWindow(sevenDay, null),
            DateTimeOffset.Now,
            extra));

    private static UsageHistoryPoint Point(double hoursAgo, double? fiveHour, double? sevenDay) =>
        new(DateTimeOffset.Now.AddHours(-hoursAgo), fiveHour, null, sevenDay, null);

    private static TokenBucket Bucket(string model, long input, string session = "s1", string project = "/code/shop") =>
        new(DateTimeOffset.Now.AddHours(-1), model, project, session, new TokenCounts(input, 0, 0, 0, 0), Messages: 1);

    private static AccountProfile Profile(string email) => new(
        "uuid-1",
        email,
        "Ben Biber",
        "Ben",
        HasClaudeMax: true,
        HasClaudePro: false,
        new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero),
        "Bens Org",
        "claude_max",
        "stripe_subscription",
        "default_claude_max_20x");

    // Limits

    [Fact]
    public void The_page_shows_the_cached_limits_as_rings()
    {
        _services.Usage.Cached["privat"] = Usage(42, 94);

        var page = Page();

        page.Rings.Select(ring => ring.Label).ShouldBe([Texts.UsageFiveHour, Texts.UsageSevenDay]);
        page.Rings.Select(ring => ring.ValueText).ShouldBe(["42 %", "94 %"]);
        page.Rings[0].Slices[0].Color.ShouldBe(ChartColor.Accent);
        page.Rings[1].Slices[0].Color.ShouldBe(ChartColor.Danger);
        page.Rings[0].ResetText.ShouldNotBeNull();
        page.LimitsAsOf.ShouldNotBeNull();
        page.HasRings.ShouldBeTrue();
    }

    [Fact]
    public void Extra_windows_follow_the_two_main_rings_by_name()
    {
        _services.Usage.Cached["privat"] = Usage(1, 2, new Dictionary<string, UsageWindow>
        {
            ["seven_day_sonnet"] = new(3, null),
            ["seven_day_opus"] = new(4, null),
        });

        Page().Rings.Select(ring => ring.Label).ShouldBe(["5h", "Woche", "Woche Opus", "Woche Sonnet"]);
    }

    [Fact]
    public void A_window_without_a_value_gets_no_ring_and_no_snapshot_gets_none()
    {
        _services.Usage.Cached["privat"] = Usage(42, null);
        Page().Rings.Select(ring => ring.Label).ShouldBe([Texts.UsageFiveHour]);

        _services.Usage.Cached.Remove("privat");
        var empty = Page();
        empty.Rings.ShouldBeEmpty();
        empty.HasRings.ShouldBeFalse();
        empty.LimitsAsOf.ShouldBeNull();
    }

    [Fact]
    public async Task Activating_refreshes_the_limits()
    {
        _services.Usage.Cached["privat"] = Usage(42, 7);
        _services.Usage.Fresh["privat"] = Usage(55, 8);
        var page = Page();

        await page.ActivateAsync(Ct);

        page.Rings[0].ValueText.ShouldBe("55 %");
    }

    [Fact]
    public void A_token_of_another_account_shows_the_hint()
    {
        _services.Usage.Cached["privat"] = new AccountUsage(UsageState.WrongAccount, null, "other@example.com");

        Page().LimitsHint.ShouldBe(Texts.UsageWrongAccount("other@example.com"));
    }

    [Fact]
    public async Task A_poll_of_this_account_updates_the_rings()
    {
        _services.Usage.Cached["privat"] = Usage(42, 7);
        var page = Page();
        await page.ActivateAsync(Ct);
        _services.Usage.Fresh["privat"] = Usage(77, 7);

        await _services.Poller.TickAsync(Ct);

        page.Rings[0].ValueText.ShouldBe("77 %");
    }

    [Fact]
    public async Task A_left_page_ignores_polls()
    {
        _services.Usage.Cached["privat"] = Usage(42, 7);
        var page = Page();
        await page.ActivateAsync(Ct);
        page.Deactivate();
        _services.Usage.Fresh["privat"] = Usage(77, 7);

        await _services.Poller.TickAsync(Ct);

        page.Rings[0].ValueText.ShouldBe("42 %");
    }

    // History

    [Fact]
    public void The_history_shows_the_last_24_hours_first()
    {
        _services.Usage.History["privat"] = [Point(30, 80, 20), Point(2, 40, 10)];

        var page = Page();

        page.IsHistoryDay.ShouldBeTrue();
        page.HistoryMax.ShouldBe(24);
        page.HistoryGap.ShouldBe(AccountInfoViewModel.HistoryDayGap);
        page.HistoryStartLabel.ShouldBe(Texts.InfoHistoryDayStart);
        page.HistorySeries.Select(series => series.Name).ShouldBe([Texts.UsageFiveHour, Texts.UsageSevenDay]);
        var point = page.HistorySeries[0].Points.ShouldHaveSingleItem();
        point.X.ShouldBe(22, tolerance: 0.01);
        point.Y.ShouldBe(40);
        point.Tooltip.ShouldNotBeNull();
    }

    [Fact]
    public void The_week_view_shows_seven_days()
    {
        _services.Usage.History["privat"] = [Point(30, 80, 20), Point(2, 40, 10)];
        var page = Page();

        page.ShowHistoryCommand.Execute("7d");

        page.IsHistoryWeek.ShouldBeTrue();
        page.IsHistoryDay.ShouldBeFalse();
        page.HistoryMax.ShouldBe(168);
        page.HistoryGap.ShouldBe(AccountInfoViewModel.HistoryWeekGap);
        page.HistoryStartLabel.ShouldBe(Texts.InfoHistoryWeekStart);
        page.HistorySeries[0].Points.Select(p => p.Y).ShouldBe([80, 40]);
    }

    [Fact]
    public void A_point_without_a_value_is_left_out_of_that_line_only()
    {
        _services.Usage.History["privat"] = [Point(1, null, 5)];

        var page = Page();

        page.HistorySeries[0].Points.ShouldBeEmpty();
        page.HistorySeries[1].Points.ShouldHaveSingleItem();
    }

    [Fact]
    public void Without_history_there_is_no_series()
    {
        Page().HistorySeries.ShouldBeEmpty();
    }

    // Account

    [Fact]
    public void The_profile_wins_over_the_login_file()
    {
        _services.SetLogin(Privat, "login@example.com");
        _services.Usage.Profiles["privat"] = Profile("profile@example.com");

        var page = Page();

        Field(page, Texts.InfoEmail).ShouldBe("profile@example.com");
        Field(page, Texts.InfoName).ShouldBe("Ben Biber");
        Field(page, Texts.InfoPlan).ShouldBe("Claude Max");
        Field(page, Texts.InfoOrganization).ShouldBe("Bens Org");
        Field(page, Texts.InfoBilling).ShouldBe("Stripe subscription");
        Field(page, Texts.InfoRateLimitTier).ShouldBe("Default claude max 20x");
        Field(page, Texts.InfoCreatedAt).ShouldBe(Texts.InfoDate(new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero)));
        page.HasAccountData.ShouldBeTrue();
    }

    [Fact]
    public void Without_a_profile_the_login_file_fills_the_fields()
    {
        _services.Login.ByClaudeDir[_services.Services.Resolver.Resolve(Privat).EffectiveClaudeDir] =
            new ClaudeLoginInfo("login@example.com", "Firma", "Ben", "uuid-1", "admin", 12, "native", 3);

        var page = Page();

        Field(page, Texts.InfoEmail).ShouldBe("login@example.com");
        Field(page, Texts.InfoName).ShouldBe("Ben");
        Field(page, Texts.InfoOrganization).ShouldBe("Firma");
        Field(page, Texts.InfoRole).ShouldBe("Admin");
        Field(page, Texts.InfoStartups).ShouldBe("12");
        Field(page, Texts.InfoInstallMethod).ShouldBe("Native");
        Field(page, Texts.InfoProjects).ShouldBe("3");
        Field(page, Texts.InfoPlan).ShouldBeNull();
    }

    [Fact]
    public async Task The_credentials_add_plan_scopes_and_expiry_but_never_the_token()
    {
        var expires = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        _services.Credentials.Results["privat"] = new CredentialReadResult(
            CredentialReadStatus.Found,
            new OAuthCredentials("sk-ant-secret", expires, "max", ["user:inference", "user:profile"]));
        var page = Page();

        await page.ActivateAsync(Ct);

        Field(page, Texts.InfoPlan).ShouldBe("Claude Max");
        Field(page, Texts.InfoScopes).ShouldBe("user:inference, user:profile");
        Field(page, Texts.InfoTokenExpires).ShouldBe(Texts.InfoDateTime(expires));
        page.Fields.ShouldAllBe(field => !field.Value.Contains("sk-ant"));
    }

    [Fact]
    public async Task A_running_desktop_shows_since_when()
    {
        _services.Detector.Instances.Add(_services.RunningInstance(Privat) with { StartTime = DateTimeOffset.Now.AddMinutes(-5) });
        var page = Page();

        await page.ActivateAsync(Ct);

        Field(page, Texts.InfoDesktop).ShouldNotBeNull().ShouldStartWith("läuft seit");
    }

    [Fact]
    public async Task Without_any_data_the_page_says_so_and_names_only_the_desktop()
    {
        var page = Page();
        page.Fields.ShouldBeEmpty();

        await page.ActivateAsync(Ct);

        page.HasAccountData.ShouldBeFalse();
        page.Fields.ShouldHaveSingleItem().ShouldBe(new InfoFieldViewModel(Texts.InfoDesktop, Texts.InfoDesktopNotRunning));
    }

    // Claude Code usage

    [Fact]
    public async Task The_statistics_sum_up_the_logs()
    {
        _services.Stats.Buckets.AddRange([Bucket("claude-opus-4-5", 1_000_000), Bucket("claude-haiku-4-5", 2_000, "s2")]);
        var page = Page();

        await page.ActivateAsync(Ct);

        page.HasSessions.ShouldBeTrue();
        page.IsScanning.ShouldBeFalse();
        page.Kpis.Select(kpi => kpi.Label)
            .ShouldBe([Texts.InfoTokens, Texts.InfoCost, Texts.InfoSessions, Texts.InfoMessages, Texts.InfoActiveDays]);
        page.Kpis.Select(kpi => kpi.Value).ShouldBe(["1 M", "≈ 5,00 $", "2", "2", "1 von 30"]);
        page.Kpis[1].Caption.ShouldBe(Texts.InfoCostNote);
        page.DailyBars.Count.ShouldBe(30);
        page.Models.Select(model => model.Name).ShouldBe(["Opus 4.5", "Haiku 4.5"]);
        page.Models[0].Cost.ShouldBe("≈ 5,00 $");
        page.Projects.ShouldHaveSingleItem().Label.ShouldBe("shop");
        page.UnpricedText.ShouldBeNull();
    }

    [Fact]
    public async Task The_token_kinds_make_the_donut()
    {
        _services.Stats.Buckets.Add(new TokenBucket(
            DateTimeOffset.Now.AddHours(-1), "claude-opus-4-5", "/p", "s1", new TokenCounts(100, 200, 30, 20, 650), 1));
        var page = Page();

        await page.ActivateAsync(Ct);

        page.KindSlices.Select(slice => slice.Value).ShouldBe([100, 200, 50, 650]);
        page.KindSlices.Select(slice => slice.Label)
            .ShouldBe([Texts.InfoKindInput, Texts.InfoKindOutput, Texts.InfoKindCacheWrite, Texts.InfoKindCacheRead]);
        page.KindCenterText.ShouldBe("1 k");
        page.KindLegend.Select(item => item.Value).ShouldBe(["100", "200", "50", "650"]);
    }

    [Fact]
    public async Task The_period_switch_recomputes_the_statistics()
    {
        _services.Stats.Buckets.Add(Bucket("claude-opus-4-5", 1_000));
        var page = Page();
        await page.ActivateAsync(Ct);

        page.ShowDaysCommand.Execute("7");

        page.UsageDays.ShouldBe(7);
        page.IsDays7.ShouldBeTrue();
        page.IsDays30.ShouldBeFalse();
        page.DailyBars.Count.ShouldBe(7);
        page.Kpis[4].Value.ShouldBe("1 von 7");
    }

    [Fact]
    public async Task The_five_largest_models_get_their_own_color_and_the_rest_is_other()
    {
        _services.Stats.Buckets.AddRange(Enumerable.Range(1, 7).Select(i => Bucket($"claude-test-{i}", 1_000 * (8 - i))));
        var page = Page();

        await page.ActivateAsync(Ct);

        page.ModelLegend.Select(item => item.Color).ShouldBe(
            [ChartColor.Series1, ChartColor.Series2, ChartColor.Series3, ChartColor.Series4, ChartColor.Series5, ChartColor.Series6]);
        page.ModelLegend[^1].Label.ShouldBe(Texts.InfoOtherModels);
        page.Models.Count.ShouldBe(7);
        page.Models[^1].Color.ShouldBe(ChartColor.Series6);
        page.DailyBars.Single(bar => bar.Total > 0).Segments.Count.ShouldBe(6);
        page.UnpricedText.ShouldBe(Texts.InfoUnpriced(28_000));
    }

    [Fact]
    public async Task No_sessions_in_the_period_shows_the_empty_state()
    {
        var page = Page();

        await page.ActivateAsync(Ct);

        page.HasSessions.ShouldBeFalse();
        page.StatsError.ShouldBeNull();
    }

    [Fact]
    public async Task The_page_shows_that_it_is_scanning()
    {
        _services.Stats.Gate = new TaskCompletionSource();
        var page = Page();

        var activation = page.ActivateAsync(Ct);

        page.IsScanning.ShouldBeTrue();
        page.ScanText.ShouldBe(Texts.InfoScanning);
        _services.Stats.Gate.SetResult();
        await activation;
        page.IsScanning.ShouldBeFalse();
    }

    [Fact]
    public async Task A_failing_scan_shows_an_error()
    {
        _services.Stats.ThrowOnLoad = new IOException("locked");
        var page = Page();

        await page.ActivateAsync(Ct);

        page.StatsError.ShouldBe(Texts.InfoScanFailed);
        page.IsScanning.ShouldBeFalse();
    }

    [Fact]
    public async Task Leaving_the_page_cancels_the_scan()
    {
        _services.Stats.Gate = new TaskCompletionSource();
        var page = Page();
        var activation = page.ActivateAsync(Ct);

        page.Deactivate();
        await activation;

        page.IsScanning.ShouldBeFalse();
        page.StatsError.ShouldBeNull();
    }

    // Keys

    [Fact]
    public void F5_refreshes_the_limits()
    {
        var page = Page();

        page.OnKeyDown(Key.F5, KeyModifiers.None).ShouldBeTrue();

        _services.Usage.Refreshed.ShouldContain("privat");
    }

    [Theory]
    [InlineData(Key.Escape, KeyModifiers.None)]
    [InlineData(Key.Back, KeyModifiers.None)]
    [InlineData(Key.Left, KeyModifiers.Alt)]
    public void Escape_backspace_and_alt_left_go_back(Key key, KeyModifiers modifiers)
    {
        var picker = new AccountPickerViewModel(_services.Services);
        var page = Page();
        _services.Navigator.Reset(picker);
        _services.Navigator.NavigateTo(page);

        page.OnKeyDown(key, modifiers).ShouldBeTrue();

        _services.Navigator.CurrentPage.ShouldBe(picker);
    }
}
```

`ClaudeLoginInfo`, `CredentialReadResult`, `CredentialReadStatus` and `OAuthCredentials` live in `Mcacc.Core.ClaudeCode`; `AccountProfile`, `UsageHistoryPoint` and the usage records in `Mcacc.Core.Usage`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~AccountInfoViewModelTests"`
Expected: the build fails because `AccountInfoViewModel` does not exist.

- [ ] **Step 3: Open up the hint**

In `src/Mcacc.App/ViewModels/AccountTileViewModel.cs`, change `private static string? HintFor(AccountUsage usage)` to `internal static string? HintFor(AccountUsage usage)`.

- [ ] **Step 4: Create the item records**

Create `src/Mcacc.App/ViewModels/AccountInfoItems.cs`:

```csharp
using Mcacc.App.Controls;
using Mcacc.App.Localization;
using Mcacc.Core.Usage;

namespace Mcacc.App.ViewModels;

public sealed record InfoFieldViewModel(string Label, string Value);

/// <param name="Caption">A note under the value, e.g. that the cost is an estimate.</param>
public sealed record KpiViewModel(string Label, string Value, string? Caption = null);

public sealed record LegendItemViewModel(string Label, ChartColor Color, string? Value = null);

public sealed record ModelRowViewModel(string Name, string Tokens, string Share, string Cost, ChartColor Color);

/// <summary>One limit window as a ring: the used part in color, the rest as track.</summary>
public sealed record LimitRingViewModel(string Label, string ValueText, string? ResetText, IReadOnlyList<ChartSlice> Slices)
{
    public static LimitRingViewModel From(string label, UsageWindow window)
    {
        var value = Math.Clamp(window.Utilization ?? 0, 0, 100);
        var color = value >= UsageBar.WarningThreshold ? ChartColor.Danger : ChartColor.Accent;
        var text = Texts.Percent(value);
        return new LimitRingViewModel(
            label,
            text,
            window.ResetsAt is { } reset ? Texts.UsageReset(reset) : null,
            [new ChartSlice(label, value, color, $"{label}: {text}"), new ChartSlice(string.Empty, 100 - value, ChartColor.Track)]);
    }
}
```

- [ ] **Step 5: Create the view model**

Create `src/Mcacc.App/ViewModels/AccountInfoViewModel.cs`:

```csharp
using System.Globalization;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Controls;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Stats;
using Mcacc.Core.Usage;

namespace Mcacc.App.ViewModels;

/// <summary>Everything MCACC can read about one account: profile, limits with their history, and Claude Code usage.</summary>
public sealed partial class AccountInfoViewModel : PageViewModel
{
    /// <summary>In hours: two history points further apart are not connected, the app was closed in between.</summary>
    internal const double HistoryDayGap = 0.5;

    internal const double HistoryWeekGap = 2;

    private const int NamedModels = 5;

    private static readonly ChartColor[] ModelColors =
        [ChartColor.Series1, ChartColor.Series2, ChartColor.Series3, ChartColor.Series4, ChartColor.Series5];

    private readonly AppServices _services;
    private CancellationTokenSource? _life;
    private IReadOnlyList<TokenBucket> _buckets = [];
    private AccountProfile? _profile;
    private ClaudeLoginInfo? _login;
    private CredentialFacts? _credentials;
    private string? _desktop;

    public AccountInfoViewModel(AppServices services, Account account)
    {
        _services = services;
        Account = account;
        _profile = services.Usage.GetProfile(account);
        _login = services.Login.Read(services.Resolver.Resolve(account));
        ShowLimits(services.Usage.GetCached(account));
        LoadHistory();
        BuildFields();
        ShowStats();
    }

    public Account Account { get; }

    public string Title => Texts.Info;

    public string Subtitle => Texts.InfoSubtitle;

    public string AccountName => Account.Name;

    public string BackTooltip => Texts.Back;

    public string RefreshTooltip => Texts.RefreshUsage;

    public string Initials => AccountTileViewModel.InitialsOf(Account.Name);

    public IBrush AvatarBrush => new SolidColorBrush(Color.Parse(Account.Color));

    public string AccountHeader => Texts.InfoAccount;

    public string LimitsHeader => Texts.InfoLimits;

    public string HistoryHeader => Texts.InfoHistory;

    public string UsageHeader => Texts.InfoUsage;

    public string TokensPerDayHeader => Texts.InfoTokensPerDay;

    public string KindsHeader => Texts.InfoTokenKinds;

    public string ProjectsHeader => Texts.InfoTopProjects;

    public string ModelsHeader => Texts.InfoModels;

    public string ColumnModel => Texts.InfoColumnModel;

    public string ColumnTokens => Texts.InfoColumnTokens;

    public string ColumnShare => Texts.InfoColumnShare;

    public string ColumnCost => Texts.InfoColumnCost;

    public string CostNote => Texts.InfoCostNote;

    public string NoAccountDataText => Texts.InfoNoAccountData;

    public string NoLimitsText => Texts.InfoNoLimits;

    public string NoHistoryText => Texts.InfoNoHistory;

    public string NoSessionsText => Texts.InfoNoSessions;

    public string HistoryDayText => Texts.InfoHistoryDay;

    public string HistoryWeekText => Texts.InfoHistoryWeek;

    public string HistoryEndLabel => Texts.InfoNow;

    public string Days7Text => Texts.InfoDays(7);

    public string Days30Text => Texts.InfoDays(30);

    public string Days90Text => Texts.InfoDays(90);

    public string KindsCaption => Texts.InfoTokens;

    public IReadOnlyList<LegendItemViewModel> HistoryLegend { get; } =
    [
        new(Texts.UsageFiveHour, ChartColor.Series1),
        new(Texts.UsageSevenDay, ChartColor.Series2),
    ];

    public Func<double, string> TokenAxis { get; } = value => Texts.TokenAmount((long)Math.Round(value));

    // Account

    [ObservableProperty]
    public partial IReadOnlyList<InfoFieldViewModel> Fields { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasAccountData { get; private set; }

    // Limits

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRings))]
    public partial IReadOnlyList<LimitRingViewModel> Rings { get; private set; } = [];

    public bool HasRings => Rings.Count > 0;

    [ObservableProperty]
    public partial string? LimitsAsOf { get; private set; }

    [ObservableProperty]
    public partial string? LimitsHint { get; private set; }

    // History

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryDay))]
    public partial bool IsHistoryWeek { get; private set; }

    public bool IsHistoryDay => !IsHistoryWeek;

    [ObservableProperty]
    public partial IReadOnlyList<LineSeries> HistorySeries { get; private set; } = [];

    [ObservableProperty]
    public partial double HistoryMax { get; private set; } = 24;

    [ObservableProperty]
    public partial double HistoryGap { get; private set; } = HistoryDayGap;

    [ObservableProperty]
    public partial string HistoryStartLabel { get; private set; } = Texts.InfoHistoryDayStart;

    // Claude Code usage

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDays7), nameof(IsDays30), nameof(IsDays90))]
    public partial int UsageDays { get; private set; } = 30;

    public bool IsDays7 => UsageDays == 7;

    public bool IsDays30 => UsageDays == 30;

    public bool IsDays90 => UsageDays == 90;

    [ObservableProperty]
    public partial bool IsScanning { get; private set; }

    [ObservableProperty]
    public partial string ScanText { get; private set; } = Texts.InfoScanning;

    [ObservableProperty]
    public partial string? StatsError { get; private set; }

    [ObservableProperty]
    public partial bool HasSessions { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<KpiViewModel> Kpis { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<BarStack> DailyBars { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LegendItemViewModel> ModelLegend { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ChartSlice> KindSlices { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LegendItemViewModel> KindLegend { get; private set; } = [];

    [ObservableProperty]
    public partial string? KindCenterText { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<BarListItem> Projects { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ModelRowViewModel> Models { get; private set; } = [];

    [ObservableProperty]
    public partial string? UnpricedText { get; private set; }

    public override async Task ActivateAsync(CancellationToken ct)
    {
        _services.Poller.Updated -= OnPolled;
        _services.Poller.Updated += OnPolled;
        _life?.Cancel();
        _life?.Dispose();
        _life = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _life.Token);
        await Task.WhenAll(
                RefreshLimitsAsync(linked.Token),
                LoadStatsAsync(linked.Token),
                LoadAccountAsync(linked.Token))
            .ConfigureAwait(true);
    }

    public override void Deactivate()
    {
        _services.Poller.Updated -= OnPolled;
        _life?.Cancel();
    }

    [RelayCommand]
    public void Back() => _services.Navigator.Back();

    [RelayCommand]
    public Task RefreshAsync() => RefreshLimitsAsync(_life?.Token ?? CancellationToken.None);

    /// <param name="range">"24h" or "7d".</param>
    [RelayCommand]
    public void ShowHistory(string range)
    {
        IsHistoryWeek = range == "7d";
        LoadHistory();
    }

    /// <param name="days">"7", "30" or "90".</param>
    [RelayCommand]
    public void ShowDays(string days)
    {
        UsageDays = int.Parse(days, CultureInfo.InvariantCulture);
        ShowStats();
    }

    public override bool OnKeyDown(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.F5 when modifiers == KeyModifiers.None:
                RefreshCommand.Execute(null);
                return true;
            case Key.Escape or Key.Back:
            case Key.Left when modifiers.HasFlag(KeyModifiers.Alt):
                Back();
                return true;
            default:
                return false;
        }
    }

    private void OnPolled(Account account, AccountUsage usage)
    {
        if (account.Id == Account.Id)
        {
            ShowUsage(usage);
        }
    }

    private async Task RefreshLimitsAsync(CancellationToken ct)
    {
        try
        {
            ShowUsage(await _services.Usage.RefreshAsync(Account, ct).ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            // The page was left.
        }
        catch (Exception e)
        {
            _services.Log.Warn($"Reading the limits of '{Account.Id}' failed.", e);
        }
    }

    /// <summary>A fresh reply may bring a new profile and a new history point too.</summary>
    private void ShowUsage(AccountUsage usage)
    {
        ShowLimits(usage);
        LoadHistory();
        _profile = _services.Usage.GetProfile(Account);
        BuildFields();
    }

    private void ShowLimits(AccountUsage usage)
    {
        var rings = new List<LimitRingViewModel>();
        if (usage.Snapshot is { } snapshot)
        {
            AddRing(rings, Texts.UsageFiveHour, snapshot.FiveHour);
            AddRing(rings, Texts.UsageSevenDay, snapshot.SevenDay);
            foreach (var (key, window) in (snapshot.Extra ?? new Dictionary<string, UsageWindow>())
                         .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                AddRing(rings, Texts.UsageExtraWindow(key), window);
            }
        }

        Rings = rings;
        LimitsAsOf = usage.Snapshot is { } shown ? Texts.UsageAsOf(shown.FetchedAt) : null;
        LimitsHint = AccountTileViewModel.HintFor(usage);
    }

    private static void AddRing(List<LimitRingViewModel> rings, string label, UsageWindow? window)
    {
        if (window?.Utilization is not null)
        {
            rings.Add(LimitRingViewModel.From(label, window));
        }
    }

    private void LoadHistory()
    {
        var span = IsHistoryWeek ? TimeSpan.FromDays(7) : TimeSpan.FromHours(24);
        var start = DateTimeOffset.Now - span;
        var points = _services.Usage.GetHistory(Account, start);
        HistoryMax = span.TotalHours;
        HistoryGap = IsHistoryWeek ? HistoryWeekGap : HistoryDayGap;
        HistoryStartLabel = IsHistoryWeek ? Texts.InfoHistoryWeekStart : Texts.InfoHistoryDayStart;
        HistorySeries = points.Count == 0
            ? []
            :
            [
                Line(Texts.UsageFiveHour, ChartColor.Series1, points, start, point => point.FiveHour),
                Line(Texts.UsageSevenDay, ChartColor.Series2, points, start, point => point.SevenDay),
            ];
    }

    private static LineSeries Line(
        string name,
        ChartColor color,
        IReadOnlyList<UsageHistoryPoint> points,
        DateTimeOffset start,
        Func<UsageHistoryPoint, double?> value) =>
        new(name, color, points
            .Where(point => value(point) is not null)
            .Select(point => new ChartPoint(
                (point.At - start).TotalHours,
                value(point)!.Value,
                Texts.HistoryTooltip(point.At, point.FiveHour, point.SevenDay)))
            .ToList());

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        IsScanning = true;
        StatsError = null;
        ScanText = Texts.InfoScanning;
        var progress = new Progress<double>(value => ScanText = Texts.InfoScanProgress(value));
        try
        {
            _buckets = await _services.Stats.LoadAsync(Account, progress, ct).ConfigureAwait(true);
            ShowStats();
        }
        catch (OperationCanceledException)
        {
            // The page was left.
        }
        catch (Exception e)
        {
            _services.Log.Warn($"Reading the Claude Code logs of '{Account.Id}' failed.", e);
            StatsError = Texts.InfoScanFailed;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ShowStats()
    {
        var stats = AccountStatsQuery.Compute(_buckets, DateTimeOffset.Now, UsageDays, TimeZoneInfo.Local);
        var total = stats.Tokens.Total;
        HasSessions = total > 0;

        Kpis =
        [
            new(Texts.InfoTokens, Texts.TokenAmount(total)),
            new(Texts.InfoCost, Texts.Cost(stats.Cost), Texts.InfoCostNote),
            new(Texts.InfoSessions, Texts.Count(stats.Sessions)),
            new(Texts.InfoMessages, Texts.Count(stats.Messages)),
            new(Texts.InfoActiveDays, Texts.InfoActiveDaysOf(stats.ActiveDays, UsageDays)),
        ];

        var colors = stats.Models
            .Take(NamedModels)
            .Select((model, index) => (model.Model, Color: ModelColors[index]))
            .ToDictionary(entry => entry.Model, entry => entry.Color, StringComparer.Ordinal);
        ChartColor ColorOf(string model) => colors.GetValueOrDefault(model, ChartColor.Series6);

        DailyBars = stats.Days
            .Select(day => new BarStack(
                Texts.DayLabel(day.Day),
                day.TokensByModel
                    .GroupBy(entry => ColorOf(entry.Key))
                    .OrderBy(group => group.Key)
                    .Select(group => new BarSegment(group.Sum(entry => entry.Value), group.Key))
                    .ToList(),
                day.Total > 0 ? Texts.DayTooltip(day) : null))
            .ToList();

        var legend = stats.Models
            .Take(NamedModels)
            .Select(model => new LegendItemViewModel(Texts.ModelName(model.Model), ColorOf(model.Model)))
            .ToList();
        if (stats.Models.Count > NamedModels)
        {
            legend.Add(new LegendItemViewModel(Texts.InfoOtherModels, ChartColor.Series6));
        }

        ModelLegend = legend;

        var tokens = stats.Tokens;
        KindSlices =
        [
            Kind(Texts.InfoKindInput, tokens.Input, ChartColor.Series1, total),
            Kind(Texts.InfoKindOutput, tokens.Output, ChartColor.Series2, total),
            Kind(Texts.InfoKindCacheWrite, tokens.CacheWrite, ChartColor.Series3, total),
            Kind(Texts.InfoKindCacheRead, tokens.CacheRead, ChartColor.Series4, total),
        ];
        KindLegend = KindSlices
            .Select(slice => new LegendItemViewModel(slice.Label, slice.Color, Texts.TokenAmount((long)slice.Value)))
            .ToList();
        KindCenterText = Texts.TokenAmount(total);

        Projects = stats.Projects
            .Select(project => new BarListItem(
                Texts.ProjectName(project.Project),
                project.Tokens,
                Texts.TokenAmount(project.Tokens),
                ChartColor.Accent,
                Texts.ProjectTooltip(project.Project, project.Tokens, project.Cost)))
            .ToList();

        Models = stats.Models
            .Select(model => new ModelRowViewModel(
                Texts.ModelName(model.Model),
                Texts.TokenAmount(model.Tokens.Total),
                Texts.Share(total > 0 ? (double)model.Tokens.Total / total : 0),
                model.Cost is { } cost ? Texts.Cost(cost) : Texts.InfoNoValue,
                ColorOf(model.Model)))
            .ToList();

        UnpricedText = stats.UnpricedTokens > 0 ? Texts.InfoUnpriced(stats.UnpricedTokens) : null;
    }

    private static ChartSlice Kind(string label, long value, ChartColor color, long total) =>
        new(label, value, color, Texts.AmountTooltip(label, value, total));

    /// <summary>Reads the credentials and the running desktop; both need IO, so the constructor leaves them out.</summary>
    private async Task LoadAccountAsync(CancellationToken ct)
    {
        try
        {
            var result = await _services.Credentials
                .ReadAsync(Account, _services.Resolver.Resolve(Account), ct)
                .ConfigureAwait(true);

            // Only what the page shows; the access token stays in this method.
            _credentials = result.Credentials is { } credentials
                ? new CredentialFacts(credentials.SubscriptionType, credentials.Scopes, credentials.ExpiresAt)
                : null;

            var instances = await _services.Detector.DetectAsync(ct).ConfigureAwait(true);
            var own = instances
                .Where(instance => _services.Matcher.Match(instance, _services.Accounts.Config.Accounts)?.Id == Account.Id)
                .ToList();
            var since = own.Select(instance => instance.StartTime).Min();
            _desktop = own.Count == 0
                ? Texts.InfoDesktopNotRunning
                : since is { } start ? Texts.InfoSince(start, DateTimeOffset.Now) : Texts.RunningBadge;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            _services.Log.Warn($"Reading the account details of '{Account.Id}' failed.", e);
        }

        BuildFields();
    }

    private void BuildFields()
    {
        var profile = _profile;
        var login = _login;
        var credentials = _credentials;
        var fields = new List<InfoFieldViewModel>();
        AddField(fields, Texts.InfoEmail, profile?.Email ?? login?.Email);
        AddField(fields, Texts.InfoName, profile?.FullName ?? profile?.DisplayName ?? login?.DisplayName);
        AddField(fields, Texts.InfoPlan, Texts.Plan(profile?.HasClaudeMax, profile?.HasClaudePro, credentials?.SubscriptionType));
        AddField(fields, Texts.InfoOrganization, profile?.OrganizationName ?? login?.OrganizationName);
        AddField(fields, Texts.InfoRole, Humanized(login?.OrganizationRole));
        AddField(fields, Texts.InfoBilling, Humanized(profile?.BillingType));
        AddField(fields, Texts.InfoRateLimitTier, Humanized(profile?.RateLimitTier));
        AddField(fields, Texts.InfoCreatedAt, profile?.CreatedAt is { } created ? Texts.InfoDate(created) : null);
        AddField(fields, Texts.InfoTokenExpires, credentials?.ExpiresAt is { } expires ? Texts.InfoDateTime(expires) : null);
        AddField(fields, Texts.InfoScopes, credentials?.Scopes is { Count: > 0 } scopes ? string.Join(", ", scopes) : null);
        AddField(fields, Texts.InfoStartups, login?.NumStartups is { } startups ? Texts.Count(startups) : null);
        AddField(fields, Texts.InfoInstallMethod, Humanized(login?.InstallMethod));
        AddField(fields, Texts.InfoProjects, login?.ProjectCount is { } projects ? Texts.Count(projects) : null);
        HasAccountData = fields.Count > 0;
        AddField(fields, Texts.InfoDesktop, _desktop);
        Fields = fields;
    }

    private static string? Humanized(string? value) => string.IsNullOrWhiteSpace(value) ? null : Texts.Humanize(value);

    private static void AddField(List<InfoFieldViewModel> fields, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new InfoFieldViewModel(label, value));
        }
    }

    /// <summary>What the page shows of the credentials: never the token.</summary>
    private sealed record CredentialFacts(string? SubscriptionType, IReadOnlyList<string>? Scopes, DateTimeOffset? ExpiresAt);
}
```

Two notes on the expected values:
- `The_statistics_sum_up_the_logs` expects "1 M" for 1 002 000 tokens: `1.002` formatted with `"0.#"` is `"1"`. Its cost is 5 $ for one million Opus 4.5 input tokens plus 0.002 $ for Haiku.
- In `The_five_largest_models_get_their_own_color_and_the_rest_is_other`, all seven `claude-test-*` models are unpriced: 7+6+5+4+3+2+1 = 28 thousand tokens, hence `InfoUnpriced(28_000)`. All buckets lie in the same hour, so exactly one daily bar has tokens.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~AccountInfoViewModelTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A src/Mcacc.App/ViewModels tests/Mcacc.App.Tests/ViewModels
git commit -F <message-file>   # "feat(app): add the view model of the info page"
```

---

### Task 16: The info view and the menu entry

**Files:**
- Create: `src/Mcacc.App/Views/AccountInfoView.axaml`, `src/Mcacc.App/Views/AccountInfoView.axaml.cs`
- Modify:
  - `src/Mcacc.App/App.axaml` (DataTemplate)
  - `src/Mcacc.App/Theme/Controls.axaml` (`Button.segment`)
  - `src/Mcacc.App/Views/AccountPickerView.axaml` (menu item)
  - `src/Mcacc.App/ViewModels/AccountPickerViewModel.cs` (`InfoText`, `OpenInfoCommand`)
- Test: `tests/Mcacc.App.Tests/Views/PageRenderTests.cs`, `tests/Mcacc.App.Tests/Views/ScreenshotTests.cs`, `tests/Mcacc.App.Tests/ViewModels/AccountPickerViewModelTests.cs`

**Interfaces:**
- Consumes: `AccountInfoViewModel` (Task 15), the chart controls (Task 13), `MIconKind.Insights` (Task 12)
- Produces:
  - `AccountPickerViewModel.InfoText` (`Texts.Info`)
  - `AccountPickerViewModel.OpenInfoCommand` (parameter `AccountTileViewModel?`)
  - the style class `Button.segment` with `.selected`

- [ ] **Step 1: Write the failing tests**

Append to `tests/Mcacc.App.Tests/ViewModels/AccountPickerViewModelTests.cs`:

```csharp
    [Fact]
    public void The_info_entry_opens_the_info_page_of_that_account()
    {
        var picker = Picker();
        _services.Navigator.Reset(picker);

        picker.OpenInfoCommand.Execute(picker.Tiles.Single(t => t.Id == "privat"));

        _services.Navigator.CurrentPage.ShouldBeOfType<AccountInfoViewModel>().Account.Id.ShouldBe("privat");
        _services.Navigator.CanGoBack.ShouldBeTrue();
    }
```

In `tests/Mcacc.App.Tests/Views/PageRenderTests.cs`, change the expected menu headers in `The_tile_menu_shows_its_entries_and_runs_their_commands` to:

```csharp
        items.Select(item => item.Header)
            .ShouldBe([picker.InfoText, picker.EditText, picker.RemoveText, picker.OpenFolderText]);
```

and add, with `using Mcacc.Core.Stats;`, `using Mcacc.Core.Usage;` and `using Xunit;` (already there):

```csharp
    [AvaloniaFact]
    public async Task The_info_page_renders_its_rings_and_charts()
    {
        var now = DateTimeOffset.Now;
        _services.Usage.Cached["privat"] = new AccountUsage(
            UsageState.Ok,
            new UsageSnapshot(new UsageWindow(42, now.AddHours(2)), new UsageWindow(7, now.AddDays(3)), now));
        _services.Usage.History["privat"] =
            [new UsageHistoryPoint(now.AddHours(-2), 30, null, 5, null), new UsageHistoryPoint(now.AddHours(-1), 42, null, 7, null)];
        _services.Stats.Buckets.Add(new TokenBucket(
            now.AddHours(-1), "claude-opus-4-5", "/code/shop", "s1", new TokenCounts(100, 200, 0, 0, 700), 1));
        var page = new AccountInfoViewModel(_services.Services, Privat);
        var window = Show(page);

        await page.ActivateAsync(TestContext.Current.CancellationToken);
        window.UpdateLayout();

        var controls = window.GetVisualDescendants().ToList();
        controls.OfType<LineChart>().ShouldHaveSingleItem().Series.ShouldNotBeNull().Count.ShouldBe(2);
        controls.OfType<StackedBarChart>().ShouldHaveSingleItem().Bars.ShouldNotBeNull().Count.ShouldBe(30);
        controls.OfType<DonutChart>().Count().ShouldBe(3); // two limit rings and the token kinds
        controls.OfType<BarList>().ShouldHaveSingleItem().Items.ShouldNotBeNull().Count.ShouldBe(1);
        controls.OfType<TextBlock>().Select(text => text.Text).ShouldContain("Opus 4.5");
    }
```

In `tests/Mcacc.App.Tests/Views/ScreenshotTests.cs`:

- Give `Window` a `height` parameter:

```csharp
    private Window Window(PageViewModel page, ThemeVariant variant, double width = 760, double height = 540)
    {
        _services.Navigator.Reset(page);
        Avalonia.Application.Current!.RequestedThemeVariant = variant;
        var window = new MainWindow { DataContext = _services.Window, Width = width, Height = height };
        window.Show();
        return window;
    }
```

- Add `using Mcacc.Core.Stats;`.
- Add:

```csharp
    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Info_page(string theme)
    {
        Seed();
        var now = DateTimeOffset.Now;
        _services.Usage.Profiles["privat"] = new AccountProfile(
            "uuid", "privat@example.com", "Ben Biber", "Ben", true, false, now.AddYears(-1), "Privat", "claude_max",
            "stripe_subscription", "default_claude_max_20x");
        _services.Usage.History["privat"] = Enumerable.Range(0, 24 * 12)
            .Select(i => new UsageHistoryPoint(
                now.AddMinutes(-5 * i), Math.Max(0, 94 - i * 0.4), null, 30 + i * 0.02, null))
            .ToList();
        string[] models = ["claude-opus-4-5", "claude-sonnet-4-5", "claude-haiku-4-5"];
        string[] projects = ["/code/mcacc", "/code/shop", "/code/blog"];
        _services.Stats.Buckets.AddRange(Enumerable.Range(0, 30 * 3).Select(i => new TokenBucket(
            now.AddDays(-(i / 3)).AddHours(-1),
            models[i % 3],
            projects[(i / 7) % 3],
            $"s{i / 4}",
            new TokenCounts(20_000 * (i % 5 + 1), 8_000 * (i % 3 + 1), 30_000, 5_000, 400_000 * (i % 4 + 1)),
            Messages: 10 + i % 7)));
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var page = new AccountInfoViewModel(_services.Services, Privat);

        var window = Window(page, variant, height: 1500);
        await page.ActivateAsync(TestContext.Current.CancellationToken);
        Save(window, $"info-{theme}");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~PageRenderTests|FullyQualifiedName~AccountPickerViewModelTests"`
Expected: the build fails because `InfoText` and `OpenInfoCommand` do not exist.

- [ ] **Step 3: Add the menu entry**

In `src/Mcacc.App/ViewModels/AccountPickerViewModel.cs`, add next to `EditText`:

```csharp
    public string InfoText => Texts.Info;
```

and next to the `Edit` command:

```csharp
    [RelayCommand]
    public void OpenInfo(AccountTileViewModel? tile)
    {
        if (tile is not null)
        {
            _services.Navigator.NavigateTo(new AccountInfoViewModel(_services, tile.Account));
        }
    }
```

In `src/Mcacc.App/Views/AccountPickerView.axaml`, insert as the first child of the tile's `MenuFlyout`, before the Edit item:

```xml
                <MenuItem Header="{Binding #TileList.((vm:AccountPickerViewModel)DataContext).InfoText}"
                          Command="{Binding #TileList.((vm:AccountPickerViewModel)DataContext).OpenInfoCommand}"
                          CommandParameter="{Binding}">
                  <MenuItem.Icon>
                    <controls:MIcon Kind="Insights" />
                  </MenuItem.Icon>
                </MenuItem>
```

Keep the indentation of the neighbouring `MenuItem`s. If the file has no `xmlns:controls="using:Mcacc.App.Controls"` yet, add it to the root element.

- [ ] **Step 4: Add the segment style**

In `src/Mcacc.App/Theme/Controls.axaml`, add before `</Styles>`:

```xml
  <!-- Segmented switch: buttons inside a chip border, the selected one tinted in the accent. -->
  <Style Selector="Button.segment">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Foreground" Value="{DynamicResource Mcacc.TextSecondary}" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="CornerRadius" Value="12" />
    <Setter Property="Padding" Value="10,3" />
    <Setter Property="FontSize" Value="12" />
    <Setter Property="MinHeight" Value="0" />

    <Style Selector="^:pointerover /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="Background" Value="{DynamicResource Mcacc.SurfaceHover}" />
      <Setter Property="Foreground" Value="{DynamicResource Mcacc.Text}" />
    </Style>

    <Style Selector="^.selected /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="Background" Value="{DynamicResource Mcacc.AccentSubtle}" />
      <Setter Property="Foreground" Value="{DynamicResource Mcacc.Accent}" />
    </Style>

    <Style Selector="^.selected:pointerover /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="Background" Value="{DynamicResource Mcacc.AccentSubtle}" />
      <Setter Property="Foreground" Value="{DynamicResource Mcacc.Accent}" />
    </Style>
  </Style>
```

- [ ] **Step 5: Create the view**

Create `src/Mcacc.App/Views/AccountInfoView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Mcacc.App.ViewModels"
             xmlns:controls="using:Mcacc.App.Controls"
             x:Class="Mcacc.App.Views.AccountInfoView"
             x:DataType="vm:AccountInfoViewModel">

  <UserControl.DataTemplates>
    <DataTemplate DataType="vm:LegendItemViewModel">
      <StackPanel Orientation="Horizontal" Spacing="6" Margin="0,0,14,4">
        <controls:ChartSwatch Color="{Binding Color}" VerticalAlignment="Center" />
        <TextBlock Classes="caption" Text="{Binding Label}" VerticalAlignment="Center" />
        <TextBlock Classes="muted" Text="{Binding Value}" FontSize="12" VerticalAlignment="Center"
                   IsVisible="{Binding Value, Converter={x:Static ObjectConverters.IsNotNull}}" />
      </StackPanel>
    </DataTemplate>
  </UserControl.DataTemplates>

  <ScrollViewer>
    <StackPanel Margin="24,20" Spacing="16">

      <!-- Header, as on the target picker -->
      <Grid ColumnDefinitions="*,Auto">
        <StackPanel Orientation="Horizontal" Spacing="10">
          <Button Classes="icon" Command="{Binding BackCommand}" ToolTip.Tip="{Binding BackTooltip}">
            <controls:MIcon Kind="ArrowBack" />
          </Button>
          <Border Classes="chip">
            <StackPanel Orientation="Horizontal" Spacing="8">
              <Panel Width="20" Height="20">
                <Ellipse Fill="{Binding AvatarBrush}" />
                <TextBlock Text="{Binding Initials}"
                           Foreground="White"
                           FontSize="10"
                           FontWeight="SemiBold"
                           TextAlignment="Center"
                           VerticalAlignment="Center" />
              </Panel>
              <TextBlock Text="{Binding AccountName}" FontSize="12" VerticalAlignment="Center" />
            </StackPanel>
          </Border>
        </StackPanel>
        <Button Grid.Column="1" Classes="icon" Command="{Binding RefreshCommand}" ToolTip.Tip="{Binding RefreshTooltip}">
          <controls:MIcon Kind="Refresh" />
        </Button>
      </Grid>

      <StackPanel Spacing="4">
        <TextBlock Classes="h1" Text="{Binding Title}" />
        <TextBlock Classes="caption" Text="{Binding Subtitle}" />
      </StackPanel>

      <!-- Account -->
      <Border Classes="card">
        <StackPanel Spacing="10">
          <TextBlock Classes="h2" Text="{Binding AccountHeader}" />
          <TextBlock Classes="muted" Text="{Binding NoAccountDataText}" TextWrapping="Wrap"
                     IsVisible="{Binding !HasAccountData}" />
          <ItemsControl ItemsSource="{Binding Fields}">
            <ItemsControl.ItemTemplate>
              <DataTemplate DataType="vm:InfoFieldViewModel">
                <Grid ColumnDefinitions="170,*" Margin="0,2">
                  <TextBlock Classes="caption" Text="{Binding Label}" />
                  <TextBlock Grid.Column="1" Text="{Binding Value}" TextWrapping="Wrap" />
                </Grid>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
      </Border>

      <!-- Limits -->
      <Border Classes="card">
        <StackPanel Spacing="12">
          <Grid ColumnDefinitions="*,Auto">
            <TextBlock Classes="h2" Text="{Binding LimitsHeader}" />
            <TextBlock Grid.Column="1" Classes="muted" FontSize="12" Text="{Binding LimitsAsOf}"
                       VerticalAlignment="Center" />
          </Grid>
          <Border Classes="banner" IsVisible="{Binding LimitsHint, Converter={x:Static ObjectConverters.IsNotNull}}">
            <TextBlock Text="{Binding LimitsHint}" TextWrapping="Wrap" />
          </Border>
          <TextBlock Classes="muted" Text="{Binding NoLimitsText}" IsVisible="{Binding !HasRings}" />
          <ItemsControl ItemsSource="{Binding Rings}">
            <ItemsControl.ItemsPanel>
              <ItemsPanelTemplate>
                <WrapPanel />
              </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
              <DataTemplate DataType="vm:LimitRingViewModel">
                <StackPanel Width="120" Spacing="4" Margin="0,0,12,8">
                  <controls:DonutChart Width="96" Height="96" Thickness="10"
                                       Slices="{Binding Slices}"
                                       CenterText="{Binding ValueText}"
                                       CenterCaption="{Binding Label}" />
                  <TextBlock Classes="muted" FontSize="12" Text="{Binding ResetText}" TextAlignment="Center" />
                </StackPanel>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>

          <Grid ColumnDefinitions="*,Auto">
            <TextBlock Classes="caption" Text="{Binding HistoryHeader}" VerticalAlignment="Center" />
            <Border Grid.Column="1" Classes="chip" Padding="2">
              <StackPanel Orientation="Horizontal">
                <Button Classes="segment" Classes.selected="{Binding IsHistoryDay}"
                        Content="{Binding HistoryDayText}"
                        Command="{Binding ShowHistoryCommand}" CommandParameter="24h" />
                <Button Classes="segment" Classes.selected="{Binding IsHistoryWeek}"
                        Content="{Binding HistoryWeekText}"
                        Command="{Binding ShowHistoryCommand}" CommandParameter="7d" />
              </StackPanel>
            </Border>
          </Grid>
          <controls:LineChart Height="170"
                              Series="{Binding HistorySeries}"
                              XMax="{Binding HistoryMax}"
                              MaxGap="{Binding HistoryGap}"
                              Threshold="90"
                              StartLabel="{Binding HistoryStartLabel}"
                              EndLabel="{Binding HistoryEndLabel}"
                              EmptyText="{Binding NoHistoryText}" />
          <ItemsControl ItemsSource="{Binding HistoryLegend}">
            <ItemsControl.ItemsPanel>
              <ItemsPanelTemplate>
                <WrapPanel />
              </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
          </ItemsControl>
        </StackPanel>
      </Border>

      <!-- Claude Code usage -->
      <Border Classes="card">
        <StackPanel Spacing="14">
          <Grid ColumnDefinitions="*,Auto">
            <TextBlock Classes="h2" Text="{Binding UsageHeader}" />
            <Border Grid.Column="1" Classes="chip" Padding="2">
              <StackPanel Orientation="Horizontal">
                <Button Classes="segment" Classes.selected="{Binding IsDays7}" Content="{Binding Days7Text}"
                        Command="{Binding ShowDaysCommand}" CommandParameter="7" />
                <Button Classes="segment" Classes.selected="{Binding IsDays30}" Content="{Binding Days30Text}"
                        Command="{Binding ShowDaysCommand}" CommandParameter="30" />
                <Button Classes="segment" Classes.selected="{Binding IsDays90}" Content="{Binding Days90Text}"
                        Command="{Binding ShowDaysCommand}" CommandParameter="90" />
              </StackPanel>
            </Border>
          </Grid>

          <StackPanel Orientation="Horizontal" Spacing="8" IsVisible="{Binding IsScanning}">
            <ProgressBar IsIndeterminate="True" Width="120" VerticalAlignment="Center" />
            <TextBlock Classes="caption" Text="{Binding ScanText}" VerticalAlignment="Center" />
          </StackPanel>
          <Border Classes="banner" IsVisible="{Binding StatsError, Converter={x:Static ObjectConverters.IsNotNull}}">
            <TextBlock Text="{Binding StatsError}" TextWrapping="Wrap" />
          </Border>
          <TextBlock Classes="muted" Text="{Binding NoSessionsText}" TextWrapping="Wrap">
            <TextBlock.IsVisible>
              <MultiBinding Converter="{x:Static BoolConverters.And}">
                <Binding Path="!HasSessions" />
                <Binding Path="!IsScanning" />
              </MultiBinding>
            </TextBlock.IsVisible>
          </TextBlock>

          <StackPanel Spacing="14" IsVisible="{Binding HasSessions}">
            <ItemsControl ItemsSource="{Binding Kpis}">
              <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                  <UniformGrid Rows="1" />
                </ItemsPanelTemplate>
              </ItemsControl.ItemsPanel>
              <ItemsControl.ItemTemplate>
                <DataTemplate DataType="vm:KpiViewModel">
                  <StackPanel Spacing="2" Margin="0,0,12,0">
                    <TextBlock Classes="caption" Text="{Binding Label}" />
                    <TextBlock Text="{Binding Value}" FontSize="18" FontWeight="SemiBold" />
                    <TextBlock Classes="muted" FontSize="11" Text="{Binding Caption}" TextWrapping="Wrap"
                               IsVisible="{Binding Caption, Converter={x:Static ObjectConverters.IsNotNull}}" />
                  </StackPanel>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>

            <StackPanel Spacing="6">
              <TextBlock Classes="caption" Text="{Binding TokensPerDayHeader}" />
              <controls:StackedBarChart Height="180" Bars="{Binding DailyBars}" AxisFormatter="{Binding TokenAxis}" />
              <ItemsControl ItemsSource="{Binding ModelLegend}">
                <ItemsControl.ItemsPanel>
                  <ItemsPanelTemplate>
                    <WrapPanel />
                  </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
              </ItemsControl>
            </StackPanel>

            <Grid ColumnDefinitions="Auto,24,*">
              <StackPanel Spacing="8">
                <TextBlock Classes="caption" Text="{Binding KindsHeader}" />
                <controls:DonutChart Width="140" Height="140"
                                     Slices="{Binding KindSlices}"
                                     CenterText="{Binding KindCenterText}"
                                     CenterCaption="{Binding KindsCaption}" />
                <ItemsControl ItemsSource="{Binding KindLegend}" />
              </StackPanel>
              <StackPanel Grid.Column="2" Spacing="8">
                <TextBlock Classes="caption" Text="{Binding ProjectsHeader}" />
                <controls:BarList Items="{Binding Projects}" />
              </StackPanel>
            </Grid>

            <StackPanel Spacing="6">
              <TextBlock Classes="caption" Text="{Binding ModelsHeader}" />
              <Grid ColumnDefinitions="18,*,80,70,100">
                <TextBlock Grid.Column="1" Classes="muted" FontSize="12" Text="{Binding ColumnModel}" />
                <TextBlock Grid.Column="2" Classes="muted" FontSize="12" Text="{Binding ColumnTokens}" TextAlignment="Right" />
                <TextBlock Grid.Column="3" Classes="muted" FontSize="12" Text="{Binding ColumnShare}" TextAlignment="Right" />
                <TextBlock Grid.Column="4" Classes="muted" FontSize="12" Text="{Binding ColumnCost}" TextAlignment="Right" />
              </Grid>
              <ItemsControl ItemsSource="{Binding Models}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate DataType="vm:ModelRowViewModel">
                    <Grid ColumnDefinitions="18,*,80,70,100" Margin="0,3">
                      <controls:ChartSwatch Color="{Binding Color}" VerticalAlignment="Center" />
                      <TextBlock Grid.Column="1" Text="{Binding Name}" />
                      <TextBlock Grid.Column="2" Text="{Binding Tokens}" TextAlignment="Right" />
                      <TextBlock Grid.Column="3" Text="{Binding Share}" TextAlignment="Right" />
                      <TextBlock Grid.Column="4" Text="{Binding Cost}" TextAlignment="Right" />
                    </Grid>
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
              <TextBlock Classes="muted" FontSize="11" Text="{Binding CostNote}" />
              <TextBlock Classes="muted" FontSize="11" Text="{Binding UnpricedText}"
                         IsVisible="{Binding UnpricedText, Converter={x:Static ObjectConverters.IsNotNull}}" />
            </StackPanel>
          </StackPanel>
        </StackPanel>
      </Border>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

Create `src/Mcacc.App/Views/AccountInfoView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Mcacc.App.Views;

public partial class AccountInfoView : UserControl
{
    public AccountInfoView() => InitializeComponent();
}
```

In `src/Mcacc.App/App.axaml`, add next to the `TargetPickerViewModel` template:

```xml
    <DataTemplate DataType="vm:AccountInfoViewModel">
      <views:AccountInfoView />
    </DataTemplate>
```

Binding notes:
- `Classes.selected="{Binding …}"` toggles a single class from a bool.
- `ObjectConverters.IsNotNull` and `BoolConverters.And` are built into Avalonia.
- The legend `ItemsControl`s use the `LegendItemViewModel` template from `UserControl.DataTemplates`, so they need no own `ItemTemplate`.
- `!HasSessions` in a `MultiBinding` `Binding Path` is the compiled-binding negation. If the compiler rejects it there, add a `public bool ShowNoSessions => !HasSessions && !IsScanning;` property instead:
  - Raise it with `[NotifyPropertyChangedFor(nameof(ShowNoSessions))]` on both `HasSessions` and `IsScanning`.
  - Bind `IsVisible="{Binding ShowNoSessions}"` to it.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet build MCACC.slnx` and then `dotnet test tests/Mcacc.App.Tests`
Expected: the build succeeds (the compiled bindings check every path) and all App tests PASS, the screenshot tests skipped.

- [ ] **Step 7: Look at the screenshots**

Run (PowerShell): `$env:MCACC_SHOTS = "$env:TEMP\mcacc-shots"; dotnet test tests/Mcacc.App.Tests --filter "FullyQualifiedName~ScreenshotTests"; Remove-Item Env:MCACC_SHOTS`
Expected: `info-light.png` and `info-dark.png` are in `%TEMP%\mcacc-shots`. Open both with the Read tool and check:
- The header, the three cards, the rings, and the history lines with the dashed 90 % line.
- The bars of 30 days, the donut, the project bars and the model table.
- Readable text in both themes and nothing cut off.

Fix what looks wrong before committing.

- [ ] **Step 8: Commit**

```bash
git add -A src/Mcacc.App tests/Mcacc.App.Tests
git commit -F <message-file>   # "feat(app): show the info page from the tile menu"
```

---

### Task 17: Wrap-up

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Full build and test run**

Run: `dotnet build MCACC.slnx -c Release` and then `dotnet test MCACC.slnx`
Expected: no warnings (TreatWarningsAsErrors), and every test PASSES except the skipped screenshot tests.

- [ ] **Step 2: Update the README**

In `README.md`, add after the section on removing accounts (the `### Accounts entfernen` block):

```markdown
### Info & Statistik

Im ⋮-Menü einer Kachel öffnet **Info & Statistik** eine Seite mit allem, was sich über den Account auslesen lässt:

- **Konto:** E-Mail, Name, Plan, Organisation, Abrechnung, Rate-Limit-Stufe, Ablauf des Tokens, Claude-Code-Starts und seit wann Claude Desktop läuft.
- **Limits:** 5h, Woche und weitere Fenster als Ringe, dazu der Verlauf der letzten 24 Stunden oder 7 Tage. MCACC fragt die Limits alle 5 Minuten ab, solange es offen ist; der Verlauf wächst nur in dieser Zeit.
- **Claude Code Nutzung:** Tokens, Sessions, Nachrichten und aktive Tage aus den lokalen Session-Logs (`<claude-dir>/projects/**/*.jsonl`), als Tagesbalken nach Modell, nach Token-Art, nach Projekt und als Modelltabelle, für 7, 30 oder 90 Tage.

Die Kosten sind eine **Schätzung** zu API-Listenpreisen („≈“). Mit einem Abo zahlst du sie nicht; sie zeigen nur, was dieselbe Nutzung über die API gekostet hätte. Der erste Aufruf liest alle Logs und kann bei großen Verzeichnissen einige Sekunden dauern; danach liest MCACC nur, was neu hinzugekommen ist.
```

In the `## Datenablage` code block, add after the `usage-cache.json` line (same column alignment):

```
  profile-cache.json             Profil je Account (E-Mail, Plan, Organisation), ohne Tokens
  usage-history/<id>.jsonl       Verlauf der Limits, 90 Tage
  stats/<id>.json                Index der gelesenen Claude-Code-Logs
```

- [ ] **Step 3: Manual check**

Start the app (`dotnet run --project src/Mcacc.App`) and check:
1. ⋮ → **Info & Statistik** opens the page. The first scan shows its progress and ends with numbers.
2. Back, then open the page again: the numbers appear almost at once, because the index is reused.
3. Compare the message count of one session file with the page. PowerShell: `(Select-String -Path <file> -Pattern '"usage"').Count` gives an upper bound, since duplicates per content block are removed.
4. Leave the app open for 5 minutes: the history gets a new point.
5. Switch the theme in the system settings: the charts follow.

Note the result in the ticket comment. Anything that could not be checked means `blockiert` + `Human Interaction`.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -F <message-file>   # "docs: describe the info page"
```
