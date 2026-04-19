# Harden GitHub Actions

[![NuGet](https://img.shields.io/nuget/v/harden-actions.svg?logo=nuget)](https://www.nuget.org/packages/harden-actions)

## Introduction

Harden GitHub Actions is a command-line tool that performs one-touch migration of GitHub Actions workflows from mutable references (branches and tags) to immutable commit-SHA references. Pinning actions to a specific commit hash protects your workflows from supply-chain attacks where a tag is moved or a branch is force-pushed to malicious code.

The tool scans every workflow file under `.github/workflows` in a repository, resolves each `uses:` reference against the GitHub API, and rewrites the file in place with the resolved commit SHA. Optionally, the original tag can be preserved as a trailing comment so the human-readable version is still visible.

### Installation

The CLI is distributed as a .NET global tool on NuGet under the package id `harden-actions`:

```sh
dotnet tool install --global harden-actions
```

Once installed, the `harden-actions` command will be available on your `PATH`.

Self-contained binaries (no .NET runtime required) are also published for each release on the [GitHub Releases page](https://github.com/hwoodiwiss/HardenGitHubActions/releases). Download the archive for your platform, extract it, and place the `harden-actions` executable somewhere on your `PATH`.

## Usage

Run `harden-actions` from the root of a repository, or pass the repository path as an argument:

```sh
# Harden workflows in the current directory
harden-actions

# Harden workflows in a specific repository
harden-actions ./path/to/repo

# Preview changes without writing files
harden-actions --dry-run

# Pin SHAs and append the original tag as a comment
harden-actions --comment-mode ExactTag

# Use an authenticated token to avoid GitHub API rate limits
harden-actions --token "$GITHUB_TOKEN"
```

### Arguments and options

| Flag | Description | Default |
| --- | --- | --- |
| `[repository-root]` | Positional argument: path to the repository root to scan. | `.` |
| `--comment-mode` | Append a tag comment after each pinned SHA. One of `None`, `ExactTag`, `MostSpecificTag`. | `None` |
| `--token` | GitHub personal access token used for authenticated API requests. | _(unset)_ |
| `-v`, `--verbose` | Enable verbose (Debug-level) logging. | `false` |
| `-q`, `--quiet` | Suppress informational output (warnings and above only). | `false` |
| `--dry-run` | Show what would change without writing any files. | `false` |
| `-h`, `--help` | Show help and usage information. | — |
| `--version` | Show the tool version. | — |

## Building and Testing

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) matching the version pinned in `global.json` (currently `11.0.100-preview.3.26207.106`).
- [PowerShell 7+ (PowerShell Core)](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) to run `build.ps1`.

### Build, test, and pack

The repository ships with a single entry-point script that builds the projects, runs the test suite, and produces NuGet packages under `./artifacts`:

```powershell
# Full build + tests + pack (Release configuration)
./build.ps1

# Build with a different configuration
./build.ps1 -Configuration Debug

# Skip the test run
./build.ps1 -SkipTests

# Override the pack output location
./build.ps1 -OutputPath ./out
```

#### Script parameters

| Parameter | Description | Default |
| --- | --- | --- |
| `-Configuration` | MSBuild configuration to use for `build`, `test`, and `pack`. | `Release` |
| `-OutputPath` | Directory for build artifacts. | `./artifacts` |
| `-SkipTests` | Skip running the test projects. | `false` |

You can also work with the solution directly using the standard .NET CLI commands (`dotnet build`, `dotnet test`, `dotnet pack`) against `HardenGitHubActions.slnx`.
