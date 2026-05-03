#! /usr/bin/env pwsh
#Requires -Version 7.0
#Requires -PSEdition Core

$configurEnv = Join-Path $PSScriptRoot "build" "configure-env.ps1"
& $configurEnv

code $PSScriptRoot
