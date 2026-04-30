#! /usr/bin/env pwsh
#Requires -Version 7.0
#Requires -PSEdition Core

$installSdk = Join-Path $PSScriptRoot "install-sdk.ps1"
& $installSdk

$env:DOTNET_ROOT = $env:DOTNET_INSTALL_DIR

$currPath = $env:Path
$env:Path = "$env:DOTNET_ROOT;$currPath"
