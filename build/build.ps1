#! /usr/bin/env pwsh
#Requires -Version 7.0
#Requires -PSEdition Core

param (
    [string] $Configuration = "Release",
    [string] $OutputPath = (Join-Path $PSScriptRoot "artifacts"),
    [switch] $SkipTests = $false
)

$installSdk = Join-Path $PSScriptRoot "install-sdk.ps1"
& $installSdk

$dotnet = Join-Path "$env:DOTNET_INSTALL_DIR" "dotnet"
$slnDirectory = Join-Path $PSScriptRoot ".."

$buildProjectPaths = @(
)

$testProjectPaths = @(
    "tests/HardenGitHubActions.Tests/HardenGitHubActions.Tests.csproj"
)

$packageProjectPaths = @(
    "src/HardenGitHubActions.Core/HardenGitHubActions.Core.csproj"
    "src/HardenGitHubActions.Cli/HardenGitHubActions.Cli.csproj"
)


& $dotnet workload restore

foreach ($buildProjectPath in $buildProjectPaths) {
    $fullBuildProjectPath = Join-Path $slnDirectory $buildProjectPath
    & $dotnet build $fullBuildProjectPath --configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

if (-not $SkipTests) {
    foreach ($testProjectPath in $testProjectPaths) {
        $fullTestProjectPath = Join-Path $slnDirectory $testProjectPath
        & $dotnet test $fullTestProjectPath --configuration $Configuration
    
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test failed with exit code $LASTEXITCODE"
        }
    }
}

foreach ($packageProjectPath in $packageProjectPaths) {
    $fullPackageProjectPath = Join-Path $slnDirectory $packageProjectPath
    & $dotnet pack $fullPackageProjectPath --configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed with exit code $LASTEXITCODE"
    }
}
