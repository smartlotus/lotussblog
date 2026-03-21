[CmdletBinding()]
param(
    [string]$TargetRepoUrl = "https://github.com/smartlotus/lotussblog.git",
    [string]$Branch = "main",
    [string]$CommitMessage = "",
    [string]$ExportDir = "",
    [switch]$SkipPush
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-CommandExists {
    param([string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Assert-GitIdentity {
    $gitUserName = (git config user.name 2>$null)
    if ([string]::IsNullOrWhiteSpace($gitUserName)) {
        $gitUserName = (git config --global user.name 2>$null)
    }

    $gitUserEmail = (git config user.email 2>$null)
    if ([string]::IsNullOrWhiteSpace($gitUserEmail)) {
        $gitUserEmail = (git config --global user.email 2>$null)
    }

    if ([string]::IsNullOrWhiteSpace($gitUserName) -or [string]::IsNullOrWhiteSpace($gitUserEmail)) {
        throw @"
Git user.name or user.email is not configured yet.

Run these commands once, then rerun this script:
git config --global user.name "YourName"
git config --global user.email "you@example.com"
"@
    }
}

function Remove-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string[]]$ExcludeNames = @()
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    Get-ChildItem -LiteralPath $Path -Force | ForEach-Object {
        if ($ExcludeNames -contains $_.Name) {
            return
        }

        if ($_.PSIsContainer) {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force
        }
        else {
            Remove-Item -LiteralPath $_.FullName -Force
        }
    }
}

function Copy-ProjectItem {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,
        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot,
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $sourcePath = Join-Path $SourceRoot $RelativePath
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        return
    }

    $destinationPath = Join-Path $DestinationRoot $RelativePath
    $destinationParent = Split-Path -Parent $destinationPath
    if (-not (Test-Path -LiteralPath $destinationParent)) {
        New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
    }

    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Recurse -Force
}

function Ensure-GitIgnore {
    param([string]$DestinationRoot)

    $gitIgnore = @"
# Windows
Thumbs.db
Desktop.ini

# Local caches and build output
.appdata/
.dotnet-cli/
.localappdata/
.nuget/
.vscode/
backups/
dist/
tools/
"@

    Set-Content -LiteralPath (Join-Path $DestinationRoot ".gitignore") -Value $gitIgnore -Encoding UTF8
}

if (-not (Test-CommandExists "git")) {
    throw "Git was not found. Please install Git for Windows first, then run this script again."
}

Assert-GitIdentity

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($ExportDir)) {
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    $ExportDir = Join-Path $desktopPath "lotussblog-github-upload"
}

$exportRoot = [System.IO.Path]::GetFullPath($ExportDir)
$defaultCommitMessage = "Update site $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

$itemsToCopy = @(
    "asset",
    "pages",
    "content",
    "scripts",
    "index.html",
    "style.css",
    "style.css.map",
    "style.scss",
    "config.js",
    "BEGINNER_GUIDE.md",
    "MAINTENANCE.md",
    "LICENSE",
    "run-card-assistant-new.bat"
)

Write-Step "Preparing export folder"
if (-not (Test-Path -LiteralPath $exportRoot)) {
    New-Item -ItemType Directory -Path $exportRoot -Force | Out-Null
}

$preserveNames = @()
if (Test-Path -LiteralPath (Join-Path $exportRoot ".git")) {
    $preserveNames += ".git"
}

Remove-DirectoryContents -Path $exportRoot -ExcludeNames $preserveNames

Write-Step "Copying site files into a standalone upload folder"
foreach ($item in $itemsToCopy) {
    Copy-ProjectItem -SourceRoot $projectRoot -DestinationRoot $exportRoot -RelativePath $item
}

Ensure-GitIgnore -DestinationRoot $exportRoot

Write-Step "Initializing or updating the standalone Git repository"
Push-Location $exportRoot
try {
    if (-not (Test-Path -LiteralPath ".git")) {
        git init | Out-Null
    }

    git checkout -B $Branch | Out-Null

    $originExists = $false
    $remoteNames = @(git remote)
    if ($remoteNames -contains "origin") {
        $originExists = $true
    }

    if ($originExists) {
        $currentOrigin = (git remote get-url origin).Trim()
        if ($currentOrigin -ne $TargetRepoUrl) {
            git remote remove origin
            git remote add origin $TargetRepoUrl
        }
    }
    else {
        git remote add origin $TargetRepoUrl
    }

    git add --all

    $pendingChanges = (git status --porcelain)
    if ($pendingChanges) {
        if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
            $CommitMessage = $defaultCommitMessage
        }
        git commit -m $CommitMessage | Out-Null
        Write-Host "Created commit: $CommitMessage" -ForegroundColor Green
    }
    else {
        Write-Host "No new file changes were detected, so no new commit was created." -ForegroundColor Yellow
    }

    if ($SkipPush) {
        Write-Host ""
        Write-Host "Push was skipped. You can run the following commands later:" -ForegroundColor Yellow
        Write-Host "cd `"$exportRoot`"" -ForegroundColor White
        Write-Host "git push -u origin $Branch" -ForegroundColor White
        return
    }

    Write-Step "Pushing to GitHub"
    git push -u origin $Branch

    Write-Host ""
    Write-Host "Done. The current site has been pushed to GitHub from the standalone local folder." -ForegroundColor Green
    Write-Host "Standalone upload folder: $exportRoot" -ForegroundColor Green
}
finally {
    Pop-Location
}
