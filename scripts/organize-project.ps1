[CmdletBinding()]
param(
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $projectRoot

function Write-Step {
    param([string]$Message)
    Write-Host "[organize] $Message"
}

function Ensure-Directory {
    param([string]$RelativePath)
    $full = Join-Path $projectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $full)) {
        Write-Step "Create directory: $RelativePath"
        if (-not $DryRun) {
            New-Item -ItemType Directory -Path $full | Out-Null
        }
    }
}

function Move-IfExists {
    param(
        [string]$FromRelative,
        [string]$ToRelative
    )

    $from = Join-Path $projectRoot $FromRelative
    $to = Join-Path $projectRoot $ToRelative

    if (-not (Test-Path -LiteralPath $from)) {
        return
    }

    $targetDir = Split-Path -Parent $to
    if (-not (Test-Path -LiteralPath $targetDir)) {
        Write-Step "Create directory: $targetDir"
        if (-not $DryRun) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
    }

    if (Test-Path -LiteralPath $to) {
        Write-Step "Skip move, target exists: $ToRelative"
        return
    }

    Write-Step "Move: $FromRelative -> $ToRelative"
    if (-not $DryRun) {
        Move-Item -LiteralPath $from -Destination $to -Force
    }
}

function Replace-InFile {
    param(
        [string]$FileRelative,
        [hashtable]$Replacements
    )

    $file = Join-Path $projectRoot $FileRelative
    if (-not (Test-Path -LiteralPath $file)) {
        return
    }

    $text = Get-Content -Raw -Encoding utf8 $file
    $updated = $text
    foreach ($key in $Replacements.Keys) {
        $pattern = [Regex]::Escape($key)
        $updated = $updated -replace $pattern, $Replacements[$key]
    }

    if ($updated -ne $text) {
        Write-Step "Update references: $FileRelative"
        if (-not $DryRun) {
            Set-Content -Encoding utf8 $file $updated
        }
    }
}

Write-Step "Start project organization at: $projectRoot"

$directories = @(
    "content",
    "pages",
    "pages/core",
    "pages/content",
    "pages/experiments",
    "asset/image/cards",
    "asset/image/backgrounds",
    "asset/image/gallery",
    "asset/image/pages",
    "asset/image/archive",
    "scripts"
)

foreach ($dir in $directories) {
    Ensure-Directory -RelativePath $dir
}

$moves = @(
    @{ From = "author.html"; To = "pages/core/author.html" },
    @{ From = "link1.html"; To = "pages/core/link1.html" },
    @{ From = "link2.html"; To = "pages/core/link2.html" },
    @{ From = "link3.html"; To = "pages/core/link3.html" },
    @{ From = "t1.html"; To = "pages/experiments/t1.html" },
    @{ From = "t2.html"; To = "pages/experiments/t2.html" },
    @{ From = "t3.html"; To = "pages/experiments/t3.html" },
    @{ From = "task01.html"; To = "pages/experiments/task01.html" },
    @{ From = "task02.html"; To = "pages/experiments/task02.html" },
    @{ From = "asset/image/1.png"; To = "asset/image/cards/1.png" },
    @{ From = "asset/image/2.png"; To = "asset/image/cards/2.png" },
    @{ From = "asset/image/3.png"; To = "asset/image/cards/3.png" },
    @{ From = "asset/image/4.png"; To = "asset/image/cards/4.png" },
    @{ From = "asset/image/5.png"; To = "asset/image/cards/5.png" },
    @{ From = "asset/image/6.png"; To = "asset/image/cards/6.png" },
    @{ From = "asset/image/grid.svg"; To = "asset/image/backgrounds/grid.svg" },
    @{ From = "asset/image/COVER.png"; To = "asset/image/gallery/COVER.png" },
    @{ From = "asset/image/Cover_image-100.jpg"; To = "asset/image/gallery/Cover_image-100.jpg" },
    @{ From = "asset/image/feedback.png"; To = "asset/image/gallery/feedback.png" },
    @{ From = "asset/image/Finteck-min.png"; To = "asset/image/gallery/Finteck-min.png" },
    @{ From = "asset/image/Flutterwave-app-min.jpeg"; To = "asset/image/gallery/Flutterwave-app-min.jpeg" },
    @{ From = "asset/image/header-image.png"; To = "asset/image/gallery/header-image.png" },
    @{ From = "asset/image/轮播图学习.html"; To = "asset/image/archive/轮播图学习.html" }
)

foreach ($move in $moves) {
    Move-IfExists -FromRelative $move.From -ToRelative $move.To
}

$fileReplacements = @{
    "style.css" = @{
        "./asset/image/grid.svg" = "./asset/image/backgrounds/grid.svg"
    }
    "style.scss" = @{
        "./asset/image/grid.svg" = "./asset/image/backgrounds/grid.svg"
    }
    "pages/core/author.html" = @{
        "href=""index.html""" = "href=""../../index.html"""
    }
    "pages/core/link1.html" = @{
        "href=""index.html""" = "href=""../../index.html"""
    }
    "pages/core/link2.html" = @{
        "href=""index.html""" = "href=""../../index.html"""
    }
    "pages/core/link3.html" = @{
        "href=""index.html""" = "href=""../../index.html"""
    }
}

foreach ($file in $fileReplacements.Keys) {
    Replace-InFile -FileRelative $file -Replacements $fileReplacements[$file]
}

$required = @(
    "index.html",
    "config.js",
    "content/site-content.json",
    "style.css",
    "asset/image/backgrounds/grid.svg",
    "pages/content/content-page.css",
    "pages/core/author.html",
    "pages/core/link1.html",
    "pages/core/link2.html",
    "pages/core/link3.html",
    "asset/image/cards/1.png",
    "asset/image/cards/2.png",
    "asset/image/cards/3.png",
    "asset/image/cards/4.png"
)

$missing = @()
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $path))) {
        $missing += $path
    }
}

if ($missing.Count -gt 0) {
    Write-Step "Missing required files:"
    $missing | ForEach-Object { Write-Host "  - $_" }
    throw "Organization completed with missing required files."
}

if (-not $DryRun) {
    $configRaw = Get-Content -Raw -Encoding utf8 (Join-Path $projectRoot "config.js")
    $imageMatches = [Regex]::Matches($configRaw, 'image:\s*"([^"]+)"')
    $linkMatches = [Regex]::Matches($configRaw, 'link:\s*"([^"]+)"')

    foreach ($m in $imageMatches) {
        $path = $m.Groups[1].Value
        $full = Join-Path $projectRoot $path
        if (-not (Test-Path -LiteralPath $full)) {
            throw "config.js image path does not exist: $path"
        }
    }

    foreach ($m in $linkMatches) {
        $path = $m.Groups[1].Value
        $full = Join-Path $projectRoot $path
        if (-not (Test-Path -LiteralPath $full)) {
            throw "config.js link path does not exist: $path"
        }
    }
}

Write-Step "Done. Project is organized for long-term maintenance."
