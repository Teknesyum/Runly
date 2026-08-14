param(
    [string]$Configuration = "Release",
    [string]$Output = "dist",
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Stable repository-root entry point. Keep the implementation under scripts/ while making the
# README/common `./build.ps1` command actually perform a build and propagate its exit code.
& "$PSScriptRoot\scripts\build.ps1" @PSBoundParameters
exit $LASTEXITCODE
