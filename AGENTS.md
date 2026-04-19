# AGENTS.md

Operating manual for AI coding agents working in this repository.

> Project: **HardenGitHubActions** — a .NET CLI (`harden-actions`) that rewrites
> `uses:` references in `.github/workflows/*.{yml,yaml}` files from mutable
> tags/branches to immutable commit SHAs, using the GitHub REST API.

---

## Commands (use these — do not invent new ones)

All commands run from the repository root.

| Task                                  | Command                                                                 |
| ------------------------------------- | ----------------------------------------------------------------------- |
| Restore + build everything            | `dotnet build HardenGitHubActions.slnx --configuration Release`         |
| Run all tests                         | `dotnet test --configuration Release`                                   |
| Run a single test project             | `dotnet test tests/HardenGitHubActions.Tests/HardenGitHubActions.Tests.csproj` |
| Run a single test by name             | `dotnet test --filter "FullyQualifiedName~WorkflowRewriterTests"`       |
| Pack NuGet packages                   | `dotnet pack src/HardenGitHubActions.Cli/HardenGitHubActions.Cli.csproj --configuration Release` |
| Full CI-equivalent build (test+pack)  | `pwsh ./build.ps1`                                                      |
| CI-equivalent build, skip tests       | `pwsh ./build.ps1 -SkipTests`                                           |
| Run the CLI from source               | `dotnet run --project src/HardenGitHubActions.Cli -- [args]`            |
| Run CLI against this repo (dry-run)   | `dotnet run --project src/HardenGitHubActions.Cli -- . --dry-run`       |

CLI surface (see `src/HardenGitHubActions.Cli/HardenCommand.cs:17`):

```
harden-actions [repository-root]
  --comment-mode  None | ExactTag | MostSpecificTag   (default: None)
  --token         <github-pat>                         (optional; raises rate limits)
  --dry-run                                            (no files written)
  -v|--verbose                                         (Debug logging)
  -q|--quiet                                           (Warnings+ only)
```

`TreatWarningsAsErrors=true` is enabled repo-wide (`Directory.Build.props:12`).
A warning **is** a build break — fix it, do not suppress it without justification.

---

## Tech stack (exact versions)

- **.NET SDK:** `11.0.100-preview.3.26207.106` (pinned in `global.json`)
- **TFM:** `net11.0` (set via `DefaultClassLibFrameworks` in `Directory.Build.props`)
- **Language:** C# `latest`, `Nullable=enable`, `ImplicitUsings=enable`
- **CLI framework:** `Spectre.Console.Cli` 0.55.0
- **DI / logging:** `Microsoft.Extensions.{DependencyInjection,Logging}` 11.0 preview
- **Test framework:** `TUnit` 1.31.0 (Microsoft.Testing.Platform runner — see `global.json`)
- **Central package management** is on (`ManagePackageVersionsCentrally=true`).
  Add new package versions to `Directory.Packages.props`, **never** to a `.csproj`.

---

## Project structure

```
HardenGitHubActions.slnx                 Solution (slnx format)
Directory.Build.props                    Repo-wide MSBuild settings (TFM, analysers, warnings-as-errors)
Directory.Packages.props                 Central package versions
global.json                              SDK pin + test runner
build.ps1                                CI-equivalent local build script
.github/workflows/build.yml              CI: test, pack, multi-RID publish, NuGet release
src/HardenGitHubActions.Core/            Library — pure logic, no I/O concerns at the edges
  ActionReference.cs                     Parses `owner/repo@ref` from `uses:` lines
  HardeningOptions.cs                    Options record + TagCommentMode enum
  WorkflowScanner.cs                     Discovers workflow files under .github/workflows
  WorkflowRewriter.cs                    Rewrites `uses:` lines (regex over text — preserves formatting)
  WorkflowHardener.cs                    Orchestrates scan → rewrite → write, with logging + dry-run
  GitHub/                                IGitHubApiClient + HTTP impl + caching decorator + DTOs
src/HardenGitHubActions.Cli/             Spectre.Console.Cli host (executable: harden-actions)
  Program.cs                             Composition root (ServiceCollection + CommandApp)
  HardenCommand.cs                       The single command + Settings
  Infrastructure/                        Spectre/MEDI bridge + Spectre logger provider
tests/HardenGitHubActions.Tests/         TUnit tests; mirrors src layout
artifacts/                               Build output (UseArtifactsOutput=true) — ignored, do not commit
```

The library/CLI split is deliberate: **all rewriting logic lives in
`HardenGitHubActions.Core` and must be testable without spinning up the CLI**.
`HardenGitHubActions.Cli` should stay thin — composition, argument parsing,
console output.

---

## Code style

Enforced by `.editorconfig` and `<AnalysisMode>All</AnalysisMode>`. The
analyser set is strict; treat its output as the source of truth.

- 4-space indent for `.cs`, 2-space for `.csproj`/`.props`/`.json`/`.yml`.
- `insert_final_newline = false` for C# files (per `.editorconfig`).
- File-scoped namespaces. One public type per file. `sealed` by default.
- Prefer `record`/`sealed record` for value-like types (see `HardeningOptions`,
  `ActionReference`, `HardeningSummary`).
- Use primary constructors where the type is a thin DI consumer (see
  `HardenCommand`).
- Logging: use `[LoggerMessage]` source generators, **not** `_logger.LogInformation("...")`.
  See `WorkflowHardener.cs:96` for the established pattern.
- Always `.ConfigureAwait(false)` on awaited tasks in library code.
- `ArgumentNullException.ThrowIfNull(x)` for public-API argument validation.
- Regex: prefer `[GeneratedRegex]` (see `WorkflowRewriter.cs:10`); for static
  fallbacks use `RegexOptions.Compiled | RegexOptions.CultureInvariant`.
- String comparisons: pass an explicit `StringComparison` / `StringComparer`
  (CA rules will fail the build otherwise).

### Example — adding a new logger message

```csharp
// Good — source-generated, structured, allocation-free
public sealed partial class Foo
{
    private readonly ILogger<Foo> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed {Count} item(s) for {Owner}")]
    private static partial void LogProcessed(ILogger logger, int count, string owner);
}

// Bad — string interpolation, untyped, allocates
_logger.LogInformation($"Processed {count} items for {owner}");
```

### Example — a TUnit test

Tests live next to their subject under `tests/HardenGitHubActions.Tests/` and
use TUnit attributes (`[Test]`, `[Arguments(...)]`). Fakes (e.g.
`FakeGitHubApiClient` in `WorkflowRewriterTests.cs:9`) are preferred over
mocking libraries — none are referenced and none should be added without a
strong reason.

```csharp
[Test]
public async Task Rewrites_tag_to_pinned_sha()
{
    var client = CreateClient(tag: "v4");
    var result = await WorkflowRewriter.RewriteAsync(
        "      - uses: actions/checkout@v4\n",
        new HardeningOptions { CommentMode = TagCommentMode.None },
        client);

    await Assert.That(result).Contains("@aabbccdd"); // pinned SHA prefix
}
```

---

## Test-Driven Development (Red → Green → Refactor)

New functionality **must** be implemented test-first using the Red-Green-Refactor
loop, with a human-in-the-loop checkpoint between Red and Green:

1. **Red.** Write the failing TUnit test(s) that describe the new behaviour.
   Run `dotnet test --configuration Release` and confirm the new test(s) fail
   for the *right* reason (assertion failure or missing API — not a compile
   error in unrelated code, not a typo).
2. **Human checkpoint.** **Stop and surface the failing test(s) to the user
   for review before writing any production code.** Show:
   - the test source,
   - the exact failure output,
   - a one-line summary of what behaviour the test pins down.
   Wait for explicit approval (or requested edits) before proceeding. Do not
   implement and test in the same turn.
3. **Green.** Implement the *minimum* production code in
   `HardenGitHubActions.Core` (or the relevant project) to make the approved
   test(s) pass. Re-run `dotnet test --configuration Release`; all tests must
   be green.
4. **Refactor.** Clean up — naming, duplication, allocations, analyser
   warnings — with the suite green the whole way. Re-run tests after each
   non-trivial refactor.

Exceptions (no human checkpoint required, but still test-covered):
- Pure refactors with no behaviour change.
- Trivial typo / comment / formatting fixes.
- Changes confined to `src/HardenGitHubActions.Cli` plumbing that is already
  exercised by existing CLI tests.

If a bug is being fixed, the Red step is a regression test that reproduces the
bug; the same human checkpoint applies.

---

## Testing expectations

- **Every** behaviour change in `HardenGitHubActions.Core` needs a TUnit test.
- Network is **never** hit from tests — use `FakeGitHubApiClient` or write a
  similar fake against `IGitHubApiClient`.
- Run `dotnet test --configuration Release` before declaring work done.
  `Release` matters: warnings-as-errors and the analyser pass differ subtly
  from `Debug`.
- New CLI behaviour belongs in `tests/HardenGitHubActions.Tests/Cli/` using
  `Spectre.Console.Cli.Testing` (already referenced).

---

## Git and CI workflow

- Default branch: `main`. CI (`.github/workflows/build.yml`) runs on every
  push and PR: test → pack → multi-RID publish → (on tag `v*.*.*`) NuGet
  push + GitHub Release upload.
- Releases are tag-driven: `vMAJOR.MINOR.PATCH`. Non-tag builds are versioned
  `0.0.0-ci.<run>+<sha>`.
- This project's whole purpose is supply-chain hardening — **any new GitHub
  Action added to `.github/workflows/` must already be pinned to a 40-char
  commit SHA**. A floating tag in our own workflows is a bug. If you add an
  action, run the tool against the repo (`dotnet run --project
  src/HardenGitHubActions.Cli -- .`) before committing.
- Conventional, imperative commit subjects (look at `git log` for tone). Keep
  the diff focused — no drive-by reformatting in feature commits.
- Do not push tags or create releases unless explicitly asked.

---

## Boundaries

### Always
- Follow Red → Green → Refactor for new functionality, and pause for human
  approval of the failing test(s) before writing production code (see the
  TDD section above). The human checkpoint may be skipped **only** if the
  user explicitly says so for that task (e.g. "skip the TDD checkpoint",
  "go straight through"); a general "go ahead" does not count.
- Add new package versions to `Directory.Packages.props` (central package management).
- Keep new logic in `HardenGitHubActions.Core` and cover it with TUnit tests.
- Use `[LoggerMessage]` source generators for logging.
- Pass `CancellationToken` through every async path; respect it.
- Run `dotnet test --configuration Release` before claiming a task is done.
- Pin any new third-party action in `.github/workflows/` to a full commit SHA.

### Ask first
- Before changing the public API surface of `HardenGitHubActions.Core` (the
  package is published to NuGet — breaking changes need an intentional bump).
- Before changing the CLI command/option names or default values in
  `HardenCommand.Settings` — these are user-facing.
- Before bumping the .NET SDK in `global.json` or the TFM in `Directory.Build.props`.
- Before adding a new NuGet dependency, especially anything that pulls in
  reflection-heavy frameworks (the CLI is published as a single self-contained
  file across multiple RIDs — see the `publish-binaries` job).
- Before editing `.github/workflows/build.yml` (release plumbing — easy to break).
- Before suppressing an analyser warning (`#pragma warning disable` /
  `[SuppressMessage]`). Only `CA1515` is globally suppressed; new suppressions
  need a justification comment, scoped as narrowly as possible.

### Never
- **Never** commit secrets, PATs, or NuGet API keys. The release pipeline uses
  OIDC / Trusted Publishing — there are no long-lived secrets in this repo.
- **Never** edit files under `artifacts/` — it is build output.
- **Never** weaken `TreatWarningsAsErrors`, `Nullable`, `AnalysisMode=All`, or
  `Deterministic` in `Directory.Build.props` to make code compile.
- **Never** add a mocking library (Moq, NSubstitute, etc.) — fakes against
  `IGitHubApiClient` are the pattern.
- **Never** introduce direct `HttpClient` instantiation outside the GitHub
  client composition in `Program.cs:31` — go through `IGitHubApiClient`.
- **Never** add a floating-tag `uses:` reference in our own workflows — pin to
  a commit SHA. (Yes, this tool exists to do that for you.)
- **Never** rewrite history on `main` or force-push.
