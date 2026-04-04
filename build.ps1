#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"
Set-Location -LiteralPath $PSScriptRoot

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

# ==========================================
# 1. Restore
# ==========================================
Write-Host "Restoring packages..." -ForegroundColor Cyan
dotnet restore "./AquaMai.slnx"

# ==========================================
# 2. PreBuild: Generate BuildInfo.g.cs
# ==========================================
Write-Host "Generating BuildInfo..." -ForegroundColor Cyan
try {
    $gitDescribe = git describe --tags
    # remove 'v' if exists
    if ($gitDescribe.StartsWith("v")) {
        $gitDescribe = $gitDescribe.Substring(1)
    }

    # Parse git describe: "1.8.0" or "1.8.0-3-gabcdef"
    # Merge commit count into the patch version: "1.8.0-3-gabcdef" → "1.8.3-gabcdef"
    $describeParts = $gitDescribe.Split('-')
    $tagVersion = $describeParts[0]

    if ($describeParts.Length -ge 3) {
        $commitCount = $describeParts[1]
        $hash = $describeParts[2]
        $verParts = $tagVersion.Split('.')
        $verParts[2] = $commitCount
        $shortVer = $verParts -join '.'
        $gitDescribe = "$shortVer-$hash"
    } else {
        $shortVer = $tagVersion
    }

    $branch = git rev-parse --abbrev-ref HEAD
    if ($branch -ne "main") {
        $gitDescribe = "$gitDescribe-$branch"
    }

    # Skip dirty check in CI environment
    if (-not $env:CI -and -not $env:GITHUB_ACTIONS) {
        $isDirty = git status --porcelain
        if ($isDirty) {
            $gitDescribe = "$gitDescribe-dirty"
        }
    }

    $buildDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    
    $versionContent = @"
    // Auto-generated file. Do not modify manually.
    namespace AquaMai;

    public static partial class BuildInfo
    {
        public const string Version = "$shortVer";
        public const string GitVersion = "$gitDescribe";
        public const string BuildDate = "$buildDate";
    }
"@
    Set-Content "./AquaMai/BuildInfo.g.cs" $versionContent -Encoding UTF8
} catch {
    Write-Warning "Failed to generate BuildInfo.g.cs (Git describe failed?): $_"
    # Fallback if needed, or just continue
}

# ==========================================
# 3. Build
# ==========================================
Write-Host "Building Solution..." -ForegroundColor Cyan
$Configuration = "Release"
if ($args.Count -gt 0 -and $args[0] -eq "-Configuration") {
    $Configuration = $args[1]
}

dotnet build "./AquaMai.slnx" -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
