[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$paths = @{
    DataFile    = Join-Path $projectRoot "content\site-content.json"
    BuildScript = Join-Path $projectRoot "scripts\site-manager.ps1"
    PagesDir    = Join-Path $projectRoot "pages\content"
    CardsDir    = Join-Path $projectRoot "asset\image\cards"
    BackupRoot  = Join-Path $projectRoot "backups\card-assistant-new"
}

function Ensure-Layout {
    foreach ($dir in @((Split-Path -Parent $paths.DataFile), $paths.PagesDir, $paths.CardsDir, $paths.BackupRoot)) {
        if (-not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir | Out-Null
        }
    }
}

function Is-HttpUrl {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $false
    }

    $uri = $null
    if (-not [Uri]::TryCreate($Text.Trim(), [UriKind]::Absolute, [ref]$uri)) {
        return $false
    }

    return $uri.Scheme -eq [Uri]::UriSchemeHttp -or $uri.Scheme -eq [Uri]::UriSchemeHttps
}

function Normalize-ExternalUrl {
    param([string]$RawValue)

    if ([string]::IsNullOrWhiteSpace($RawValue)) {
        return ""
    }

    $value = $RawValue.Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($value)) {
        return ""
    }

    if ($value.StartsWith("//")) {
        $value = "https:$value"
    }

    if (-not ($value -match '^[a-zA-Z][a-zA-Z0-9+\-.]*://')) {
        # Allow domain-like input without scheme, e.g. pickup30.netlify.app
        if ($value -match '^[^/\s]+\.[^/\s]+' -or $value.StartsWith("www.", [System.StringComparison]::OrdinalIgnoreCase)) {
            $value = "https://$value"
        }
    }

    return $value
}

function Get-SafeSlug {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return "item-" + (Get-Date -Format "yyyyMMddHHmmss")
    }

    $candidate = ($Text.Trim().ToLower() -replace '[^a-z0-9\-]', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        return "item-" + (Get-Date -Format "yyyyMMddHHmmss")
    }

    return $candidate
}

function Normalize-Mode {
    param([string]$Mode)

    $safe = ""
    if ($null -ne $Mode) {
        $safe = $Mode.Trim().ToLower()
    }

    switch ($safe) {
        "external-link" { return "external-link" }
        "external" { return "external" }
        default { return "generated" }
    }
}

function Normalize-SitePath {
    param([string]$PathText)

    if ([string]::IsNullOrWhiteSpace($PathText)) {
        return ""
    }

    $value = $PathText.Trim().Trim('"')
    if (Is-HttpUrl $value) {
        return $value
    }

    $value = $value -replace '\\', '/'
    if ($value.StartsWith("./")) {
        return $value
    }
    if ($value.StartsWith("/")) {
        return ".${value}"
    }
    if ($value.Length -ge 3 -and [char]::IsLetter($value[0]) -and $value[1] -eq ':' -and ($value[2] -eq '/' -or $value[2] -eq '\')) {
        return ""
    }
    return "./$value"
}

function Resolve-InputFile {
    param([string]$RawPath)

    if ([string]::IsNullOrWhiteSpace($RawPath)) {
        return $null
    }

    $value = $RawPath.Trim().Trim('"')
    if ([IO.Path]::IsPathRooted($value)) {
        if (Test-Path -LiteralPath $value -PathType Leaf) {
            return (Resolve-Path -LiteralPath $value).Path
        }
        return $null
    }

    $candidate = Join-Path $projectRoot $value
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }
    return $null
}

function Import-CardImage {
    param(
        [string]$RawInput,
        [string]$Slug,
        [string]$Fallback
    )

    $fallbackPath = Normalize-SitePath -PathText $Fallback
    $input = ""
    if ($null -ne $RawInput) {
        $input = $RawInput.Trim().Trim('"')
    }

    if ([string]::IsNullOrWhiteSpace($input)) {
        return $fallbackPath
    }
    if (Is-HttpUrl $input) {
        return $input
    }

    $normalized = Normalize-SitePath -PathText $input
    if (-not [string]::IsNullOrWhiteSpace($normalized)) {
        $full = Join-Path $projectRoot (($normalized -replace '^\./', '') -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            return $normalized
        }
    }

    $source = Resolve-InputFile -RawPath $input
    if ($null -eq $source) {
        return $fallbackPath
    }

    $ext = [IO.Path]::GetExtension($source)
    if ([string]::IsNullOrWhiteSpace($ext)) {
        $ext = ".png"
    }

    $safeSlug = Get-SafeSlug -Text $Slug
    $targetRel = ("asset/image/cards/{0}-card{1}" -f $safeSlug, $ext).ToLower()
    $targetFull = Join-Path $projectRoot ($targetRel -replace '/', [IO.Path]::DirectorySeparatorChar)
    $targetDir = Split-Path -Parent $targetFull
    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir | Out-Null
    }

    if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($targetFull)) {
        Copy-Item -LiteralPath $source -Destination $targetFull -Force
    }

    return "./$($targetRel -replace '\\', '/')"
}

function Import-LocalHtml {
    param(
        [string]$RawInput,
        [string]$Slug
    )

    $source = Resolve-InputFile -RawPath $RawInput
    if ($null -eq $source) {
        throw "Local HTML file not found: $RawInput"
    }

    $ext = [IO.Path]::GetExtension($source).ToLower()
    if ($ext -ne ".html" -and $ext -ne ".htm") {
        throw "Only .html/.htm files are supported."
    }

    $safeSlug = Get-SafeSlug -Text $Slug
    $targetRel = "pages/content/$safeSlug.html"
    $targetFull = Join-Path $projectRoot ($targetRel -replace '/', [IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($targetFull)) {
        Copy-Item -LiteralPath $source -Destination $targetFull -Force
    }

    return $targetRel
}

function Get-UniqueSlug {
    param(
        [string]$Candidate,
        [object]$Data,
        [string]$ExcludeId = ""
    )

    $slug = $Candidate
    $i = 2
    $existing = @($Data.items | Where-Object { [string]$_.id -ne $ExcludeId } | ForEach-Object { [string]$_.pageSlug })
    while ($existing -contains $slug) {
        $slug = "$Candidate-$i"
        $i++
    }

    return $slug
}

function Normalize-Data {
    param([object]$Data)

    if ($null -eq $Data.site) {
        $Data | Add-Member -MemberType NoteProperty -Name site -Value ([pscustomobject]@{
                browserTitle = "Card Site"
                bannerTitle  = "Welcome"
            }) -Force
    }

    if ($null -eq $Data.items) {
        $Data | Add-Member -MemberType NoteProperty -Name items -Value @() -Force
    }

    $sorted = @($Data.items | Sort-Object order, cardTitle)
    $output = @()
    $order = 1
    foreach ($item in $sorted) {
        $slug = Get-SafeSlug -Text ([string]$item.pageSlug)
        if ([string]::IsNullOrWhiteSpace($slug)) {
            $slug = Get-SafeSlug -Text ([string]$item.id)
        }

        $title = [string]$item.cardTitle
        if ([string]::IsNullOrWhiteSpace($title)) {
            $title = [string]$item.pageTitle
        }
        if ([string]::IsNullOrWhiteSpace($title)) {
            $title = $slug
        }

        $mode = Normalize-Mode -Mode ([string]$item.pageMode)
        $pageFile = [string]$item.pageFile
        if ([string]::IsNullOrWhiteSpace($pageFile) -and $mode -eq "generated") {
            $pageFile = "pages/content/$slug.html"
        }

        $cardImage = Normalize-SitePath -PathText ([string]$item.cardImage)
        if ([string]::IsNullOrWhiteSpace($cardImage)) {
            $cardImage = "./asset/image/cards/1.png"
        }

        $cardAlt = [string]$item.cardAlt
        if ([string]::IsNullOrWhiteSpace($cardAlt)) {
            $cardAlt = $title
        }

        $pageTitle = [string]$item.pageTitle
        if ([string]::IsNullOrWhiteSpace($pageTitle)) {
            $pageTitle = $title
        }

        $pageSummary = [string]$item.pageSummary
        if ([string]::IsNullOrWhiteSpace($pageSummary)) {
            $pageSummary = "This is a page."
        }

        $pageLayout = [string]$item.pageLayout
        if ([string]::IsNullOrWhiteSpace($pageLayout)) {
            $pageLayout = "text-only"
        }

        $pageImage = Normalize-SitePath -PathText ([string]$item.pageImage)
        if ([string]::IsNullOrWhiteSpace($pageImage)) {
            $pageImage = $cardImage
        }

        $body = @([string[]]$item.pageBody | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($body.Count -eq 0) {
            $body = @("No body yet.")
        }

        $id = [string]$item.id
        if ([string]::IsNullOrWhiteSpace($id)) {
            $id = $slug
        }

        $enabled = [bool]$item.enabled

        $output += [pscustomobject]@{
            id          = $id
            enabled     = $enabled
            order       = $order
            cardTitle   = $title
            cardImage   = $cardImage
            cardAlt     = $cardAlt
            pageSlug    = $slug
            pageFile    = $pageFile
            pageMode    = $mode
            pageTitle   = $pageTitle
            pageSummary = $pageSummary
            pageLayout  = $pageLayout
            pageImage   = $pageImage
            pageBody    = @($body)
        }
        $order++
    }

    $Data.items = @($output)
}

function New-DefaultData {
    return [pscustomobject]@{
        site  = [pscustomobject]@{
            browserTitle = "Card Site"
            bannerTitle  = "Welcome"
        }
        items = @()
    }
}

function Repair-CommonJsonIssues {
    param([string]$RawText)

    $fixed = $RawText

    # Repair pattern like: "cardAlt": ""C:\path\file.jpg""
    $fixed = [regex]::Replace(
        $fixed,
        '"(?<key>[^"\r\n]+)"\s*:\s*""(?<val>[^"\r\n]*)""',
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($m)
            $key = $m.Groups["key"].Value
            $val = $m.Groups["val"].Value
            $val = $val -replace '\\', '/'
            $val = $val -replace '"', '\"'
            return '"' + $key + '": "' + $val + '"'
        }
    )

    # Repair unescaped Windows paths in JSON string values.
    $fixed = [regex]::Replace(
        $fixed,
        '"(?<key>[^"\r\n]+)"\s*:\s*"(?<val>[A-Za-z]:\\[^"\r\n]*)"',
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($m)
            $key = $m.Groups["key"].Value
            $val = $m.Groups["val"].Value
            $val = $val -replace '\\', '/'
            $val = $val -replace '"', '\"'
            return '"' + $key + '": "' + $val + '"'
        }
    )

    return $fixed
}

function Read-SiteData {
    if (-not (Test-Path -LiteralPath $paths.DataFile)) {
        return (New-DefaultData)
    }

    $raw = Get-Content -LiteralPath $paths.DataFile -Raw -Encoding utf8

    try {
        $data = $raw | ConvertFrom-Json
        Normalize-Data -Data $data
        return $data
    }
    catch {
        $repairedRaw = Repair-CommonJsonIssues -RawText $raw
        if ($repairedRaw -eq $raw) {
            throw "site-content.json is invalid JSON and could not be auto-repaired. File: $($paths.DataFile)`nError: $($_.Exception.Message)"
        }

        try {
            $data = $repairedRaw | ConvertFrom-Json
            Normalize-Data -Data $data

            $repairDir = Join-Path $paths.BackupRoot "invalid-json"
            if (-not (Test-Path -LiteralPath $repairDir)) {
                New-Item -ItemType Directory -Path $repairDir | Out-Null
            }

            $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $invalidBackup = Join-Path $repairDir "site-content-invalid-$stamp.json"
            Copy-Item -LiteralPath $paths.DataFile -Destination $invalidBackup -Force

            $json = $data | ConvertTo-Json -Depth 16
            [IO.File]::WriteAllText($paths.DataFile, $json, [Text.UTF8Encoding]::new($false))

            return $data
        }
        catch {
            throw "site-content.json is invalid and auto-repair failed. File: $($paths.DataFile)`nError: $($_.Exception.Message)"
        }
    }
}

function New-Backup {
    param([string]$Reason)

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $safeReason = "manual"
    if (-not [string]::IsNullOrWhiteSpace($Reason)) {
        $safeReason = ($Reason -replace '[^a-zA-Z0-9\-]', '-').ToLower()
    }

    $folder = Join-Path $paths.BackupRoot "$stamp-$safeReason"
    New-Item -ItemType Directory -Path $folder | Out-Null

    foreach ($file in @($paths.DataFile, (Join-Path $projectRoot "config.js"), (Join-Path $projectRoot "index.html"))) {
        if (Test-Path -LiteralPath $file -PathType Leaf) {
            Copy-Item -LiteralPath $file -Destination (Join-Path $folder ([IO.Path]::GetFileName($file))) -Force
        }
    }
}

function Save-DataFile {
    param(
        [object]$Data,
        [string]$Reason
    )

    Normalize-Data -Data $Data
    New-Backup -Reason $Reason
    $json = $Data | ConvertTo-Json -Depth 16
    [IO.File]::WriteAllText($paths.DataFile, $json, [Text.UTF8Encoding]::new($false))
}

function Invoke-Build {
    if (-not (Test-Path -LiteralPath $paths.BuildScript -PathType Leaf)) {
        return [pscustomobject]@{ ExitCode = 1; Output = "Build script not found: $($paths.BuildScript)" }
    }

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = "powershell.exe"
    $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$($paths.BuildScript)`" -Mode Build"
    $psi.WorkingDirectory = $projectRoot
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $p = [System.Diagnostics.Process]::Start($psi)
    if ($null -eq $p) {
        return [pscustomobject]@{ ExitCode = 1; Output = "Failed to start build process." }
    }

    $output = $p.StandardOutput.ReadToEnd() + [Environment]::NewLine + $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    return [pscustomobject]@{ ExitCode = $p.ExitCode; Output = $output.Trim() }
}

function Save-And-Build {
    param(
        [object]$Data,
        [string]$Reason
    )

    try {
        Apply-SiteFieldsToData
        Save-DataFile -Data $Data -Reason $Reason
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show("Save failed: $($_.Exception.Message)", "Error") | Out-Null
        return $false
    }

    $result = Invoke-Build
    if ($result.ExitCode -ne 0) {
        [System.Windows.Forms.MessageBox]::Show("Build sync failed:`r`n`r`n$($result.Output)", "Error") | Out-Null
        return $false
    }
    return $true
}

function Remove-PageFileIfUnused {
    param(
        [object]$Item,
        [object]$Data
    )

    $pageFile = [string]$Item.pageFile
    if ([string]::IsNullOrWhiteSpace($pageFile) -or (Is-HttpUrl $pageFile)) {
        return
    }

    $normalized = ($pageFile -replace '\\', '/') -replace '^\./', ''
    if (-not $normalized.ToLower().StartsWith("pages/content/")) {
        return
    }
    if (-not $normalized.ToLower().EndsWith(".html")) {
        return
    }

    foreach ($other in @($Data.items | Where-Object { [string]$_.id -ne [string]$Item.id })) {
        $otherPage = ([string]$other.pageFile -replace '\\', '/') -replace '^\./', ''
        if ($otherPage -eq $normalized) {
            return
        }
    }

    $full = Join-Path $projectRoot ($normalized -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (Test-Path -LiteralPath $full -PathType Leaf) {
        Remove-Item -LiteralPath $full -Force
    }
}

function Mode-Label {
    param([string]$Mode)

    switch (Normalize-Mode -Mode $Mode) {
        "external-link" { return "External URL" }
        "external" { return "Local HTML" }
        default { return "Generated" }
    }
}

Ensure-Layout
$script:data = Read-SiteData

$form = New-Object System.Windows.Forms.Form
$form.Text = "New Card Assistant (Standalone)"
$form.StartPosition = "CenterScreen"
$form.Width = 1320
$form.Height = 860
$form.MinimumSize = New-Object System.Drawing.Size(1160, 740)

$root = New-Object System.Windows.Forms.TableLayoutPanel
$root.Dock = "Fill"
$root.ColumnCount = 1
$root.RowCount = 2
$root.Padding = New-Object System.Windows.Forms.Padding(10)
[void]$root.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent, 100)))
[void]$root.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 34)))
[void]$form.Controls.Add($root)

$split = New-Object System.Windows.Forms.SplitContainer
$split.Dock = "Fill"
$split.FixedPanel = [System.Windows.Forms.FixedPanel]::Panel2
[void]$root.Controls.Add($split, 0, 0)

$leftGroup = New-Object System.Windows.Forms.GroupBox
$leftGroup.Text = "Cards (delete syncs immediately)"
$leftGroup.Dock = "Fill"
$leftGroup.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
[void]$split.Panel1.Controls.Add($leftGroup)

$leftLayout = New-Object System.Windows.Forms.TableLayoutPanel
$leftLayout.Dock = "Fill"
$leftLayout.ColumnCount = 1
$leftLayout.RowCount = 2
$leftLayout.Padding = New-Object System.Windows.Forms.Padding(10)
[void]$leftLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent, 100)))
[void]$leftLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 44)))
[void]$leftGroup.Controls.Add($leftLayout)

$script:listView = New-Object System.Windows.Forms.ListView
$script:listView.Dock = "Fill"
$script:listView.View = "Details"
$script:listView.FullRowSelect = $true
$script:listView.MultiSelect = $false
$script:listView.GridLines = $true
$script:listView.HideSelection = $false
[void]$script:listView.Columns.Add("Order", 50)
[void]$script:listView.Columns.Add("On", 50)
[void]$script:listView.Columns.Add("Title", 180)
[void]$script:listView.Columns.Add("Type", 100)
[void]$script:listView.Columns.Add("Link/Page", 240)
[void]$leftLayout.Controls.Add($script:listView, 0, 0)

$leftBtns = New-Object System.Windows.Forms.TableLayoutPanel
$leftBtns.Dock = "Fill"
$leftBtns.ColumnCount = 3
$leftBtns.RowCount = 1
[void]$leftBtns.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 33)))
[void]$leftBtns.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 34)))
[void]$leftBtns.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 33)))
[void]$leftLayout.Controls.Add($leftBtns, 0, 1)

$btnReload = New-Object System.Windows.Forms.Button
$btnReload.Text = "Reload"
$btnReload.Dock = "Fill"
[void]$leftBtns.Controls.Add($btnReload, 0, 0)

$btnDelete = New-Object System.Windows.Forms.Button
$btnDelete.Text = "Delete + Sync"
$btnDelete.Dock = "Fill"
$btnDelete.BackColor = [System.Drawing.Color]::MistyRose
[void]$leftBtns.Controls.Add($btnDelete, 1, 0)

$btnBuild = New-Object System.Windows.Forms.Button
$btnBuild.Text = "Build Only"
$btnBuild.Dock = "Fill"
[void]$leftBtns.Controls.Add($btnBuild, 2, 0)

$tabs = New-Object System.Windows.Forms.TabControl
$tabs.Dock = "Fill"
[void]$split.Panel2.Controls.Add($tabs)

$tabAdd = New-Object System.Windows.Forms.TabPage
$tabAdd.Text = "Add Card"
$tabs.TabPages.Add($tabAdd) | Out-Null

$tabEdit = New-Object System.Windows.Forms.TabPage
$tabEdit.Text = "Edit Selected"
$tabs.TabPages.Add($tabEdit) | Out-Null

$tabSite = New-Object System.Windows.Forms.TabPage
$tabSite.Text = "Site"
$tabs.TabPages.Add($tabSite) | Out-Null

function New-FieldLabel {
    param([string]$Text)
    return New-Object System.Windows.Forms.Label -Property @{ Text = $Text; Dock = "Fill"; TextAlign = "MiddleLeft" }
}

$siteLayout = New-Object System.Windows.Forms.TableLayoutPanel
$siteLayout.Dock = "Top"
$siteLayout.AutoSize = $true
$siteLayout.ColumnCount = 2
$siteLayout.Padding = New-Object System.Windows.Forms.Padding(14, 12, 14, 12)
[void]$siteLayout.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 150)))
[void]$siteLayout.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))
[void]$tabSite.Controls.Add($siteLayout)

$siteLayout.RowCount = 1
[void]$siteLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$siteLayout.Controls.Add((New-FieldLabel "Browser Title"), 0, 0)
$script:txtSiteBrowserTitle = New-Object System.Windows.Forms.TextBox
$script:txtSiteBrowserTitle.Dock = "Fill"
[void]$siteLayout.Controls.Add($script:txtSiteBrowserTitle, 1, 0)

$siteLayout.RowCount += 1
[void]$siteLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$siteLayout.Controls.Add((New-FieldLabel "Banner Title"), 0, 1)
$script:txtSiteBannerTitle = New-Object System.Windows.Forms.TextBox
$script:txtSiteBannerTitle.Dock = "Fill"
[void]$siteLayout.Controls.Add($script:txtSiteBannerTitle, 1, 1)

$siteLayout.RowCount += 1
[void]$siteLayout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 48)))
$script:btnSaveSite = New-Object System.Windows.Forms.Button
$script:btnSaveSite.Text = "Save Site Name + Sync"
$script:btnSaveSite.Dock = "Fill"
$script:btnSaveSite.BackColor = [System.Drawing.Color]::Honeydew
[void]$siteLayout.Controls.Add((New-Object System.Windows.Forms.Label), 0, 2)
[void]$siteLayout.Controls.Add($script:btnSaveSite, 1, 2)

$add = New-Object System.Windows.Forms.TableLayoutPanel
$add.Dock = "Top"
$add.AutoSize = $true
$add.ColumnCount = 3
$add.Padding = New-Object System.Windows.Forms.Padding(14, 12, 14, 12)
[void]$add.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 120)))
[void]$add.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))
[void]$add.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 100)))
[void]$tabAdd.Controls.Add($add)

$add.RowCount = 1
[void]$add.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$add.Controls.Add((New-FieldLabel "Mode"), 0, 0)
$script:cmbAddMode = New-Object System.Windows.Forms.ComboBox
$script:cmbAddMode.Dock = "Fill"
$script:cmbAddMode.DropDownStyle = "DropDownList"
[void]$script:cmbAddMode.Items.Add("External URL")
[void]$script:cmbAddMode.Items.Add("Local HTML")
$script:cmbAddMode.SelectedIndex = 0
[void]$add.Controls.Add($script:cmbAddMode, 1, 0)
[void]$add.Controls.Add((New-Object System.Windows.Forms.Label), 2, 0)

$add.RowCount += 1
[void]$add.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$add.Controls.Add((New-FieldLabel "Title"), 0, 1)
$script:txtAddTitle = New-Object System.Windows.Forms.TextBox
$script:txtAddTitle.Dock = "Fill"
[void]$add.Controls.Add($script:txtAddTitle, 1, 1)
[void]$add.Controls.Add((New-Object System.Windows.Forms.Label), 2, 1)

$add.RowCount += 1
[void]$add.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$add.Controls.Add((New-FieldLabel "URL/HTML"), 0, 2)
$script:txtAddTarget = New-Object System.Windows.Forms.TextBox
$script:txtAddTarget.Dock = "Fill"
[void]$add.Controls.Add($script:txtAddTarget, 1, 2)
$script:btnAddTargetBrowse = New-Object System.Windows.Forms.Button
$script:btnAddTargetBrowse.Text = "Browse"
$script:btnAddTargetBrowse.Dock = "Fill"
[void]$add.Controls.Add($script:btnAddTargetBrowse, 2, 2)

$add.RowCount += 1
[void]$add.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$add.Controls.Add((New-FieldLabel "Card Image"), 0, 3)
$script:txtAddImage = New-Object System.Windows.Forms.TextBox
$script:txtAddImage.Dock = "Fill"
[void]$add.Controls.Add($script:txtAddImage, 1, 3)
$script:btnAddImageBrowse = New-Object System.Windows.Forms.Button
$script:btnAddImageBrowse.Text = "Browse"
$script:btnAddImageBrowse.Dock = "Fill"
[void]$add.Controls.Add($script:btnAddImageBrowse, 2, 3)

$add.RowCount += 1
[void]$add.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$add.Controls.Add((New-FieldLabel "Image ALT"), 0, 4)
$script:txtAddAlt = New-Object System.Windows.Forms.TextBox
$script:txtAddAlt.Dock = "Fill"
[void]$add.Controls.Add($script:txtAddAlt, 1, 4)
[void]$add.Controls.Add((New-Object System.Windows.Forms.Label), 2, 4)

$add.RowCount += 1
[void]$add.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 48)))
$script:btnAddCard = New-Object System.Windows.Forms.Button
$script:btnAddCard.Text = "Add + Sync"
$script:btnAddCard.Dock = "Fill"
$script:btnAddCard.BackColor = [System.Drawing.Color]::Honeydew
[void]$add.Controls.Add((New-Object System.Windows.Forms.Label), 0, 5)
[void]$add.Controls.Add($script:btnAddCard, 1, 5)
[void]$add.Controls.Add((New-Object System.Windows.Forms.Label), 2, 5)

$edit = New-Object System.Windows.Forms.TableLayoutPanel
$edit.Dock = "Top"
$edit.AutoSize = $true
$edit.ColumnCount = 3
$edit.Padding = New-Object System.Windows.Forms.Padding(14, 12, 14, 12)
[void]$edit.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 120)))
[void]$edit.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))
[void]$edit.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 100)))
[void]$tabEdit.Controls.Add($edit)

$r = 0
$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$edit.Controls.Add((New-FieldLabel "Slug"), 0, $r)
$script:txtEditSlug = New-Object System.Windows.Forms.TextBox
$script:txtEditSlug.Dock = "Fill"
$script:txtEditSlug.ReadOnly = $true
[void]$edit.Controls.Add($script:txtEditSlug, 1, $r)
[void]$edit.Controls.Add((New-Object System.Windows.Forms.Label), 2, $r)
$r++

$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$edit.Controls.Add((New-FieldLabel "Enabled"), 0, $r)
$script:chkEditEnabled = New-Object System.Windows.Forms.CheckBox
$script:chkEditEnabled.Text = "Enabled"
$script:chkEditEnabled.Dock = "Left"
[void]$edit.Controls.Add($script:chkEditEnabled, 1, $r)
[void]$edit.Controls.Add((New-Object System.Windows.Forms.Label), 2, $r)
$r++

$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$edit.Controls.Add((New-FieldLabel "Order"), 0, $r)
$script:numEditOrder = New-Object System.Windows.Forms.NumericUpDown
$script:numEditOrder.Dock = "Left"
$script:numEditOrder.Width = 120
$script:numEditOrder.Minimum = 1
$script:numEditOrder.Maximum = 99999
[void]$edit.Controls.Add($script:numEditOrder, 1, $r)
[void]$edit.Controls.Add((New-Object System.Windows.Forms.Label), 2, $r)
$r++

$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$edit.Controls.Add((New-FieldLabel "Mode"), 0, $r)
$script:cmbEditMode = New-Object System.Windows.Forms.ComboBox
$script:cmbEditMode.Dock = "Fill"
$script:cmbEditMode.DropDownStyle = "DropDownList"
[void]$script:cmbEditMode.Items.Add("external-link")
[void]$script:cmbEditMode.Items.Add("external")
[void]$script:cmbEditMode.Items.Add("generated")
$script:cmbEditMode.SelectedIndex = 0
[void]$edit.Controls.Add($script:cmbEditMode, 1, $r)
[void]$edit.Controls.Add((New-Object System.Windows.Forms.Label), 2, $r)
$r++

$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$edit.Controls.Add((New-FieldLabel "URL/Page"), 0, $r)
$script:txtEditTarget = New-Object System.Windows.Forms.TextBox
$script:txtEditTarget.Dock = "Fill"
[void]$edit.Controls.Add($script:txtEditTarget, 1, $r)
$script:btnEditTargetBrowse = New-Object System.Windows.Forms.Button
$script:btnEditTargetBrowse.Text = "Browse"
$script:btnEditTargetBrowse.Dock = "Fill"
[void]$edit.Controls.Add($script:btnEditTargetBrowse, 2, $r)
$r++

$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$edit.Controls.Add((New-FieldLabel "Title"), 0, $r)
$script:txtEditTitle = New-Object System.Windows.Forms.TextBox
$script:txtEditTitle.Dock = "Fill"
[void]$edit.Controls.Add($script:txtEditTitle, 1, $r)
[void]$edit.Controls.Add((New-Object System.Windows.Forms.Label), 2, $r)
$r++

$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$edit.Controls.Add((New-FieldLabel "Card Image"), 0, $r)
$script:txtEditImage = New-Object System.Windows.Forms.TextBox
$script:txtEditImage.Dock = "Fill"
[void]$edit.Controls.Add($script:txtEditImage, 1, $r)
$script:btnEditImageBrowse = New-Object System.Windows.Forms.Button
$script:btnEditImageBrowse.Text = "Browse"
$script:btnEditImageBrowse.Dock = "Fill"
[void]$edit.Controls.Add($script:btnEditImageBrowse, 2, $r)
$r++

$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38)))
[void]$edit.Controls.Add((New-FieldLabel "Image ALT"), 0, $r)
$script:txtEditAlt = New-Object System.Windows.Forms.TextBox
$script:txtEditAlt.Dock = "Fill"
[void]$edit.Controls.Add($script:txtEditAlt, 1, $r)
[void]$edit.Controls.Add((New-Object System.Windows.Forms.Label), 2, $r)
$r++

$edit.RowCount += 1
[void]$edit.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 48)))
$script:btnSaveEdit = New-Object System.Windows.Forms.Button
$script:btnSaveEdit.Text = "Save + Sync"
$script:btnSaveEdit.Dock = "Fill"
$script:btnSaveEdit.BackColor = [System.Drawing.Color]::Honeydew
[void]$edit.Controls.Add((New-Object System.Windows.Forms.Label), 0, $r)
[void]$edit.Controls.Add($script:btnSaveEdit, 1, $r)
[void]$edit.Controls.Add((New-Object System.Windows.Forms.Label), 2, $r)

$script:lblStatus = New-Object System.Windows.Forms.Label
$script:lblStatus.Dock = "Fill"
$script:lblStatus.TextAlign = "MiddleLeft"
$script:lblStatus.ForeColor = [System.Drawing.Color]::DimGray
[void]$root.Controls.Add($script:lblStatus, 0, 1)

function Set-Status {
    param([string]$Text)
    $script:lblStatus.Text = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $Text
}

function Apply-SiteFieldsToData {
    if ($null -eq $script:data.site) {
        $script:data | Add-Member -MemberType NoteProperty -Name site -Value ([pscustomobject]@{}) -Force
    }

    $browserTitle = ""
    $bannerTitle = ""
    if ($null -ne $script:txtSiteBrowserTitle) {
        $browserTitle = $script:txtSiteBrowserTitle.Text.Trim()
    }
    if ($null -ne $script:txtSiteBannerTitle) {
        $bannerTitle = $script:txtSiteBannerTitle.Text.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($browserTitle)) {
        $browserTitle = [string]$script:data.site.browserTitle
    }
    if ([string]::IsNullOrWhiteSpace($bannerTitle)) {
        $bannerTitle = [string]$script:data.site.bannerTitle
    }
    if ([string]::IsNullOrWhiteSpace($browserTitle)) {
        $browserTitle = "Card Site"
    }
    if ([string]::IsNullOrWhiteSpace($bannerTitle)) {
        $bannerTitle = "Welcome"
    }

    $script:data.site.browserTitle = $browserTitle
    $script:data.site.bannerTitle = $bannerTitle
}

function Load-SiteFieldsFromData {
    if ($null -eq $script:data.site) {
        $script:data | Add-Member -MemberType NoteProperty -Name site -Value ([pscustomobject]@{}) -Force
    }

    $browserTitle = [string]$script:data.site.browserTitle
    $bannerTitle = [string]$script:data.site.bannerTitle
    if ([string]::IsNullOrWhiteSpace($browserTitle)) {
        $browserTitle = "Card Site"
    }
    if ([string]::IsNullOrWhiteSpace($bannerTitle)) {
        $bannerTitle = "Welcome"
    }

    if ($null -ne $script:txtSiteBrowserTitle) {
        $script:txtSiteBrowserTitle.Text = $browserTitle
    }
    if ($null -ne $script:txtSiteBannerTitle) {
        $script:txtSiteBannerTitle.Text = $bannerTitle
    }
}

function Refresh-List {
    param([string]$PreferredId = "")

    $selectedId = $PreferredId
    if ([string]::IsNullOrWhiteSpace($selectedId) -and $script:listView.SelectedItems.Count -gt 0) {
        $selectedId = [string]$script:listView.SelectedItems[0].Tag
    }

    $script:listView.BeginUpdate()
    try {
        $script:listView.Items.Clear()
        foreach ($item in @($script:data.items | Sort-Object order)) {
            $row = New-Object System.Windows.Forms.ListViewItem(([string]$item.order))
            $row.Tag = [string]$item.id
            $enabledText = "N"
            if ([bool]$item.enabled) {
                $enabledText = "Y"
            }
            [void]$row.SubItems.Add($enabledText)
            [void]$row.SubItems.Add([string]$item.cardTitle)
            [void]$row.SubItems.Add((Mode-Label -Mode ([string]$item.pageMode)))
            [void]$row.SubItems.Add([string]$item.pageFile)
            [void]$script:listView.Items.Add($row)
        }

        if ($script:listView.Items.Count -eq 0) {
            $script:txtEditSlug.Text = ""
            $script:txtEditTitle.Text = ""
            $script:txtEditTarget.Text = ""
            $script:txtEditImage.Text = ""
            $script:txtEditAlt.Text = ""
            return
        }

        $picked = $false
        if (-not [string]::IsNullOrWhiteSpace($selectedId)) {
            foreach ($lvItem in $script:listView.Items) {
                if ([string]$lvItem.Tag -eq $selectedId) {
                    $lvItem.Selected = $true
                    $lvItem.EnsureVisible()
                    $picked = $true
                    break
                }
            }
        }

        if (-not $picked) {
            $script:listView.Items[0].Selected = $true
            $script:listView.Items[0].EnsureVisible()
        }
    }
    finally {
        $script:listView.EndUpdate()
    }
}

function Get-SelectedItem {
    if ($script:listView.SelectedItems.Count -eq 0) {
        return $null
    }
    $id = [string]$script:listView.SelectedItems[0].Tag
    if ([string]::IsNullOrWhiteSpace($id)) {
        return $null
    }
    return @($script:data.items | Where-Object { [string]$_.id -eq $id })[0]
}

function Load-EditorFromSelected {
    $item = Get-SelectedItem
    if ($null -eq $item) {
        $script:txtEditSlug.Text = ""
        $script:txtEditTitle.Text = ""
        $script:txtEditTarget.Text = ""
        $script:txtEditImage.Text = ""
        $script:txtEditAlt.Text = ""
        return
    }

    $script:txtEditSlug.Text = [string]$item.pageSlug
    $script:chkEditEnabled.Checked = [bool]$item.enabled
    $script:numEditOrder.Value = [Math]::Max(1, [int]$item.order)
    $script:cmbEditMode.SelectedItem = Normalize-Mode -Mode ([string]$item.pageMode)
    $script:txtEditTarget.Text = [string]$item.pageFile
    $script:txtEditTitle.Text = [string]$item.cardTitle
    $script:txtEditImage.Text = [string]$item.cardImage
    $script:txtEditAlt.Text = [string]$item.cardAlt
}

function Pick-ImageFile {
    $ofd = New-Object System.Windows.Forms.OpenFileDialog
    $ofd.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.gif|All Files (*.*)|*.*"
    $ofd.Title = "Select image"
    if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        return $ofd.FileName
    }
    return ""
}

function Pick-HtmlFile {
    $ofd = New-Object System.Windows.Forms.OpenFileDialog
    $ofd.Filter = "HTML Files (*.html;*.htm)|*.html;*.htm|All Files (*.*)|*.*"
    $ofd.Title = "Select HTML file"
    if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        return $ofd.FileName
    }
    return ""
}

$script:listView.Add_SelectedIndexChanged({ Load-EditorFromSelected })

$btnReload.Add_Click({
        try {
            $script:data = Read-SiteData
            Load-SiteFieldsFromData
            Refresh-List
            Set-Status "Data reloaded"
        }
        catch {
            [System.Windows.Forms.MessageBox]::Show("Reload failed: $($_.Exception.Message)", "Error") | Out-Null
            Set-Status "Reload failed"
        }
    })

$btnBuild.Add_Click({
        $result = Invoke-Build
        if ($result.ExitCode -eq 0) {
            [System.Windows.Forms.MessageBox]::Show("Build done.", "Done") | Out-Null
            Set-Status "Build done"
        }
        else {
            [System.Windows.Forms.MessageBox]::Show("Build failed:`r`n`r`n$($result.Output)", "Error") | Out-Null
            Set-Status "Build failed"
        }
    })

$btnDelete.Add_Click({
        $item = Get-SelectedItem
        if ($null -eq $item) {
            return
        }

        $confirm = [System.Windows.Forms.MessageBox]::Show(
            "Delete card [$($item.cardTitle)] and sync now?",
            "Confirm",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
        if ($confirm -ne [System.Windows.Forms.DialogResult]::Yes) {
            return
        }

        Remove-PageFileIfUnused -Item $item -Data $script:data
        $script:data.items = @($script:data.items | Where-Object { [string]$_.id -ne [string]$item.id })
        if (Save-And-Build -Data $script:data -Reason "delete-card") {
            Refresh-List
            Set-Status "Deleted and synced: $($item.cardTitle)"
        }
    })

$script:btnAddTargetBrowse.Add_Click({
        if ($script:cmbAddMode.SelectedIndex -eq 1) {
            $picked = Pick-HtmlFile
            if (-not [string]::IsNullOrWhiteSpace($picked)) {
                $script:txtAddTarget.Text = $picked
            }
        }
        else {
            [System.Windows.Forms.MessageBox]::Show("External URL mode does not require local HTML selection.", "Info") | Out-Null
        }
    })

$script:btnAddImageBrowse.Add_Click({
        $picked = Pick-ImageFile
        if (-not [string]::IsNullOrWhiteSpace($picked)) {
            $script:txtAddImage.Text = $picked
        }
    })

$script:btnEditTargetBrowse.Add_Click({
        $mode = Normalize-Mode -Mode ([string]$script:cmbEditMode.SelectedItem)
        if ($mode -eq "external") {
            $picked = Pick-HtmlFile
            if (-not [string]::IsNullOrWhiteSpace($picked)) {
                $script:txtEditTarget.Text = $picked
            }
        }
        else {
            [System.Windows.Forms.MessageBox]::Show("Current mode is not local HTML.", "Info") | Out-Null
        }
    })

$script:btnEditImageBrowse.Add_Click({
        $picked = Pick-ImageFile
        if (-not [string]::IsNullOrWhiteSpace($picked)) {
            $script:txtEditImage.Text = $picked
        }
    })

$script:btnAddCard.Add_Click({
        try {
            $title = $script:txtAddTitle.Text.Trim()
            if ([string]::IsNullOrWhiteSpace($title)) {
                throw "Card title is required."
            }

            $mode = if ($script:cmbAddMode.SelectedIndex -eq 1) { "external" } else { "external-link" }
            $targetInputRaw = $script:txtAddTarget.Text.Trim().Trim('"')
            $targetInput = $targetInputRaw
            $slug = Get-UniqueSlug -Candidate (Get-SafeSlug -Text $title) -Data $script:data
            $pageFile = ""

            if ($mode -eq "external-link") {
                $targetInput = Normalize-ExternalUrl -RawValue $targetInputRaw
                if (-not (Is-HttpUrl $targetInput)) {
                    throw "External URL mode requires a valid URL. Example: https://example.com or example.com"
                }
                $pageFile = $targetInput
                $script:txtAddTarget.Text = $targetInput
            }
            else {
                if ([string]::IsNullOrWhiteSpace($targetInput)) {
                    throw "Local HTML mode requires a local HTML file."
                }
                $pageFile = Import-LocalHtml -RawInput $targetInput -Slug $slug
            }

            $cardImage = Import-CardImage -RawInput $script:txtAddImage.Text -Slug $slug -Fallback "./asset/image/cards/1.png"
            $cardAlt = $script:txtAddAlt.Text.Trim()
            if ([string]::IsNullOrWhiteSpace($cardAlt)) {
                $cardAlt = $title
            }

            $maxOrder = 0
            foreach ($it in @($script:data.items)) {
                if ([int]$it.order -gt $maxOrder) {
                    $maxOrder = [int]$it.order
                }
            }

            $pageSummary = "External link page"
            $pageBodyLine = "This card links to an external URL."
            if ($mode -eq "external") {
                $pageSummary = "Local HTML imported page"
                $pageBodyLine = "This card uses an imported local HTML file."
            }

            $newItem = [pscustomobject]@{
                id          = $slug
                enabled     = $true
                order       = ($maxOrder + 1)
                cardTitle   = $title
                cardImage   = $cardImage
                cardAlt     = $cardAlt
                pageSlug    = $slug
                pageFile    = $pageFile
                pageMode    = $mode
                pageTitle   = $title
                pageSummary = $pageSummary
                pageLayout  = "text-only"
                pageImage   = $cardImage
                pageBody    = @($pageBodyLine)
            }

            $script:data.items = @($script:data.items + $newItem)
            if (Save-And-Build -Data $script:data -Reason "add-card") {
                $script:txtAddTitle.Text = ""
                $script:txtAddTarget.Text = ""
                $script:txtAddImage.Text = ""
                $script:txtAddAlt.Text = ""
                Refresh-List -PreferredId $slug
                Set-Status "Added and synced: $title"
            }
        }
        catch {
            [System.Windows.Forms.MessageBox]::Show("Add failed: $($_.Exception.Message)", "Warning") | Out-Null
        }
    })

$script:btnSaveEdit.Add_Click({
        try {
            $item = Get-SelectedItem
            if ($null -eq $item) {
                throw "Select a card first."
            }

            $title = $script:txtEditTitle.Text.Trim()
            if ([string]::IsNullOrWhiteSpace($title)) {
                throw "Title is required."
            }

            $mode = Normalize-Mode -Mode ([string]$script:cmbEditMode.SelectedItem)
            $targetInputRaw = $script:txtEditTarget.Text.Trim().Trim('"')
            $targetInput = $targetInputRaw
            $pageFile = $targetInput
            if ($mode -eq "external-link") {
                $targetInput = Normalize-ExternalUrl -RawValue $targetInputRaw
                if (-not (Is-HttpUrl $targetInput)) {
                    throw "external-link mode requires a valid URL. Example: https://example.com or example.com"
                }
                $pageFile = $targetInput
                $script:txtEditTarget.Text = $targetInput
            }
            elseif ($mode -eq "external") {
                if ([string]::IsNullOrWhiteSpace($targetInput)) {
                    throw "external mode requires a local HTML file."
                }
                if (Is-HttpUrl $targetInput) {
                    throw "external mode does not support URL."
                }

                $resolved = Resolve-InputFile -RawPath $targetInput
                if ($null -ne $resolved) {
                    $pageFile = Import-LocalHtml -RawInput $resolved -Slug ([string]$item.pageSlug)
                }
                else {
                    $normalizedTarget = ($targetInput -replace '\\', '/') -replace '^\./', ''
                    $candidate = Join-Path $projectRoot ($normalizedTarget -replace '/', [IO.Path]::DirectorySeparatorChar)
                    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                        $pageFile = $normalizedTarget
                    }
                    else {
                        throw "Local HTML file was not found."
                    }
                }
            }
            else {
                if ([string]::IsNullOrWhiteSpace($pageFile)) {
                    $pageFile = "pages/content/$($item.pageSlug).html"
                }
            }

            $fallbackImage = [string]$item.cardImage
            if ([string]::IsNullOrWhiteSpace($fallbackImage)) {
                $fallbackImage = "./asset/image/cards/1.png"
            }

            $cardImage = Import-CardImage -RawInput $script:txtEditImage.Text -Slug ([string]$item.pageSlug) -Fallback $fallbackImage
            $cardAlt = $script:txtEditAlt.Text.Trim()
            if ([string]::IsNullOrWhiteSpace($cardAlt)) {
                $cardAlt = $title
            }

            $item.order = [int]$script:numEditOrder.Value
            $item.enabled = [bool]$script:chkEditEnabled.Checked
            $item.cardTitle = $title
            $item.cardImage = $cardImage
            $item.cardAlt = $cardAlt
            $item.pageMode = $mode
            $item.pageFile = ($pageFile -replace '\\', '/')
            $item.pageTitle = $title
            $item.pageImage = $cardImage
            if ($mode -eq "external-link") {
                $item.pageSummary = "External link page"
            }
            elseif ($mode -eq "external") {
                $item.pageSummary = "Local HTML imported page"
            }

            if (Save-And-Build -Data $script:data -Reason "edit-card") {
                Refresh-List -PreferredId ([string]$item.id)
                Set-Status "Edited and synced: $title"
            }
        }
        catch {
            [System.Windows.Forms.MessageBox]::Show("Save failed: $($_.Exception.Message)", "Warning") | Out-Null
        }
    })

$script:btnSaveSite.Add_Click({
        try {
            Apply-SiteFieldsToData
            if (Save-And-Build -Data $script:data -Reason "edit-site-title") {
                Load-SiteFieldsFromData
                Set-Status "Site name updated and synced"
            }
        }
        catch {
            [System.Windows.Forms.MessageBox]::Show("Site name update failed: $($_.Exception.Message)", "Error") | Out-Null
        }
    })

$form.Add_Shown({
        $desiredRightWidth = 520
        $minLeftWidth = 520

        $panel2Min = 460
        if ($split.Width - 120 -lt $panel2Min) {
            $panel2Min = [Math]::Max(260, $split.Width - 120)
        }
        $split.Panel2MinSize = $panel2Min

        $maxDistance = $split.Width - $split.Panel2MinSize
        $distance = $split.Width - $desiredRightWidth

        if ($distance -lt $minLeftWidth) {
            $distance = $minLeftWidth
        }
        if ($distance -gt $maxDistance) {
            $distance = $maxDistance
        }
        if ($distance -gt 0) {
            $split.SplitterDistance = $distance
        }
    })

Set-Status "Ready"
Load-SiteFieldsFromData
Refresh-List
[void]$form.ShowDialog()

