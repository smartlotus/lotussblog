[CmdletBinding()]
param(
    [string]$RepoUrl = "https://github.com/smartlotus/lotussblog.git",
    [string]$Branch = "main",
    [string]$CommitMessage = "",
    [switch]$UpdateOriginUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Require-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "未检测到 git。请先安装 Git for Windows 并重试。"
    }
}

function Ensure-Repo {
    & git rev-parse --is-inside-work-tree *> $null
    if ($LASTEXITCODE -eq 0) {
        return
    }

    Write-Host "当前目录不是 Git 仓库，正在初始化..." -ForegroundColor Yellow
    & git init | Out-Host
}

function Ensure-Origin {
    param(
        [string]$TargetRepoUrl,
        [bool]$ShouldUpdateUrl
    )

    $originUrl = (& git remote get-url origin 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originUrl)) {
        & git remote add origin $TargetRepoUrl | Out-Null
        Write-Host "已添加远程仓库 origin -> $TargetRepoUrl" -ForegroundColor Green
        return
    }

    if ($ShouldUpdateUrl -and $originUrl -ne $TargetRepoUrl) {
        & git remote set-url origin $TargetRepoUrl | Out-Null
        Write-Host "已更新 origin 地址 -> $TargetRepoUrl" -ForegroundColor Green
        return
    }

    Write-Host "当前 origin：$originUrl" -ForegroundColor DarkGray
}

function Checkout-TargetBranch {
    param([string]$TargetBranch)

    $currentBranch = (& git branch --show-current).Trim()
    if ($currentBranch -eq $TargetBranch) {
        return
    }

    & git show-ref --verify --quiet "refs/heads/$TargetBranch"
    if ($LASTEXITCODE -eq 0) {
        & git checkout $TargetBranch | Out-Host
        return
    }

    & git checkout -b $TargetBranch | Out-Host
}

function Commit-IfNeeded {
    param([string]$Message)

    & git add -A
    & git diff --cached --quiet
    $hasChanges = ($LASTEXITCODE -ne 0)

    if (-not $hasChanges) {
        Write-Host "没有检测到新的改动，跳过 commit，直接 push。" -ForegroundColor Yellow
        return $false
    }

    $finalMessage = $Message
    if ([string]::IsNullOrWhiteSpace($finalMessage)) {
        $finalMessage = "site sync $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    }

    & git commit -m $finalMessage | Out-Host
    return $true
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $projectRoot

try {
    Require-Git
    Ensure-Repo
    Ensure-Origin -TargetRepoUrl $RepoUrl -ShouldUpdateUrl ([bool]$UpdateOriginUrl)
    Checkout-TargetBranch -TargetBranch $Branch
    [void](Commit-IfNeeded -Message $CommitMessage)

    Write-Host "正在推送到 GitHub：origin/$Branch ..." -ForegroundColor Cyan
    & git push -u origin $Branch | Out-Host

    Write-Host ""
    Write-Host "上传完成。" -ForegroundColor Green
    Write-Host "仓库地址：$RepoUrl" -ForegroundColor Green
    Write-Host "分支：$Branch" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "上传失败：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host "请确认：GitHub 登录状态 / 远程仓库权限 / 网络可用后重试。" -ForegroundColor Yellow
    exit 1
}
finally {
    Pop-Location
}
