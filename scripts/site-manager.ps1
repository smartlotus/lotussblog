[CmdletBinding()]
param(
    [ValidateSet("Menu", "Build")]
    [string]$Mode = "Menu"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$paths = @{
    DataFile      = Join-Path $projectRoot "content\site-content.json"
    IndexFile     = Join-Path $projectRoot "index.html"
    ConfigFile    = Join-Path $projectRoot "config.js"
    PagesDir      = Join-Path $projectRoot "pages\content"
    PageCss       = Join-Path $projectRoot "pages\content\content-page.css"
    CardsDir      = Join-Path $projectRoot "asset\image\cards"
    PageImagesDir = Join-Path $projectRoot "asset\image\pages"
    BackupRoot    = Join-Path $projectRoot "backups"
}

function Ensure-ProjectLayout {
    $dirs = @(
        (Split-Path -Parent $paths.DataFile),
        $paths.PagesDir,
        $paths.CardsDir,
        $paths.PageImagesDir,
        $paths.BackupRoot
    )

    foreach ($dir in $dirs) {
        if (-not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir | Out-Null
        }
    }
}

function Escape-Html {
    param([string]$Text)
    if ($null -eq $Text) { return "" }
    return [System.Net.WebUtility]::HtmlEncode([string]$Text)
}

function To-JsString {
    param([string]$Text)
    if ($null -eq $Text) { $Text = "" }
    return ($Text | ConvertTo-Json -Compress)
}

function Normalize-RelativePath {
    param([string]$PathText)
    if ([string]::IsNullOrWhiteSpace($PathText)) { return "" }

    $pathValue = $PathText.Trim().Trim('"')
    if ($pathValue -match '^https?://') { return $pathValue }

    $pathValue = $pathValue -replace '\\', '/'
    if ($pathValue.StartsWith("./")) { return $pathValue }
    if ($pathValue.StartsWith("/")) { return ".${pathValue}" }
    return "./$pathValue"
}

function To-PageAssetPath {
    param([string]$PathText)
    if ([string]::IsNullOrWhiteSpace($PathText)) { return "" }

    if ($PathText -match '^https?://') { return $PathText }

    $normalized = $PathText -replace '\\', '/'
    $normalized = $normalized -replace '^\./', ''
    if ($normalized.StartsWith("../")) { return $normalized }
    return "../../$normalized"
}

function New-Backup {
    param([string]$Reason = "manual")

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $safeReason = ($Reason -replace '[^a-zA-Z0-9\-]', '-').ToLower()
    $folder = Join-Path $paths.BackupRoot "$stamp-$safeReason"
    New-Item -ItemType Directory -Path $folder | Out-Null

    $backupFiles = @($paths.DataFile, $paths.IndexFile, $paths.ConfigFile)
    foreach ($file in $backupFiles) {
        if (Test-Path -LiteralPath $file) {
            Copy-Item -LiteralPath $file -Destination (Join-Path $folder ([IO.Path]::GetFileName($file))) -Force
        }
    }

    if (Test-Path -LiteralPath $paths.PagesDir) {
        Copy-Item -LiteralPath $paths.PagesDir -Destination (Join-Path $folder "pages-content") -Recurse -Force
    }
}

function Read-SiteData {
    if (-not (Test-Path -LiteralPath $paths.DataFile)) {
        throw "找不到内容数据库：$($paths.DataFile)"
    }
    $raw = Get-Content -Raw -Encoding utf8 $paths.DataFile
    $data = $raw | ConvertFrom-Json
    $siteProp = $data.PSObject.Properties["site"]
    if ($null -eq $siteProp) {
        $data | Add-Member -MemberType NoteProperty -Name site -Value ([pscustomobject]@{})
    }
    $itemsProp = $data.PSObject.Properties["items"]
    if ($null -eq $itemsProp) {
        $data | Add-Member -MemberType NoteProperty -Name items -Value @()
    }
    $sectionsProp = $data.PSObject.Properties["sections"]
    if ($null -eq $sectionsProp) {
        $data | Add-Member -MemberType NoteProperty -Name sections -Value @()
    }
    return $data
}

function Save-SiteData {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Data,
        [string]$Reason = "update"
    )

    New-Backup -Reason $Reason
    $json = $Data | ConvertTo-Json -Depth 16
    Set-Content -Encoding utf8 -LiteralPath $paths.DataFile -Value $json
}

function Normalize-ItemsOrder {
    param([object]$Data)
    $orderedSections = @($Data.sections | Sort-Object order, title)
    $sectionIds = @{}
    foreach ($section in $orderedSections) {
        $sectionIds[[string]$section.id] = $true
    }

    $normalizedItems = @()
    foreach ($section in $orderedSections) {
        $sectionItems = @($Data.items | Where-Object { [string]$_.sectionId -eq [string]$section.id } | Sort-Object order, cardTitle)
        $counter = 1
        foreach ($item in $sectionItems) {
            $item.order = $counter
            $counter++
        }
        $normalizedItems += $sectionItems
    }

    $ungroupedItems = @($Data.items | Where-Object {
            [string]::IsNullOrWhiteSpace([string]$_.sectionId) -or -not $sectionIds.ContainsKey([string]$_.sectionId)
        } | Sort-Object order, cardTitle)
    $ungroupedCounter = 1
    foreach ($item in $ungroupedItems) {
        $item.order = $ungroupedCounter
        $ungroupedCounter++
    }
    $normalizedItems += $ungroupedItems
    $Data.items = $normalizedItems
}

function Normalize-Sections {
    param([object]$Data)

    if ($null -eq $Data.sections) {
        $Data | Add-Member -MemberType NoteProperty -Name sections -Value @()
    }

    $normalized = @()
    $existingIds = @{}

    foreach ($section in @($Data.sections)) {
        $title = ""
        $titleProp = $section.PSObject.Properties["title"]
        if ($null -ne $titleProp) {
            $title = [string]$titleProp.Value
        }
        if ([string]::IsNullOrWhiteSpace($title)) {
            $title = "未命名分类"
        }
        else {
            $title = $title.Trim()
        }

        $rawId = ""
        $idProp = $section.PSObject.Properties["id"]
        if ($null -ne $idProp) {
            $rawId = [string]$idProp.Value
        }
        if ([string]::IsNullOrWhiteSpace($rawId)) {
            $rawId = $title
        }

        $id = Get-SafeSectionId -InputText $rawId
        $baseId = $id
        $suffix = 2
        while ($existingIds.ContainsKey($id)) {
            $id = "$baseId-$suffix"
            $suffix++
        }
        $existingIds[$id] = $true

        $enabled = $true
        $enabledProp = $section.PSObject.Properties["enabled"]
        if ($null -ne $enabledProp) {
            $enabled = [bool]$enabledProp.Value
        }

        $order = 0
        $orderProp = $section.PSObject.Properties["order"]
        if ($null -ne $orderProp) {
            $order = [int]$orderProp.Value
        }
        if ($order -le 0) {
            $order = ($normalized.Count + 1)
        }

        $description = ""
        $descriptionProp = $section.PSObject.Properties["description"]
        if ($null -ne $descriptionProp) {
            $description = [string]$descriptionProp.Value
        }

        $cover = "./asset/image/cards/1.png"
        $coverProp = $section.PSObject.Properties["cover"]
        if ($null -ne $coverProp -and -not [string]::IsNullOrWhiteSpace([string]$coverProp.Value)) {
            $cover = Normalize-RelativePath -PathText ([string]$coverProp.Value)
        }

        $normalized += [pscustomobject]@{
            id          = $id
            enabled     = $enabled
            order       = $order
            title       = $title
            description = $description
            cover       = $cover
        }
    }

    if ($normalized.Count -eq 0) {
        $normalized += [pscustomobject]@{
            id          = "projects"
            enabled     = $true
            order       = 1
            title       = "项目板块"
            description = "进入项目合集并跳转到不同站点"
            cover       = "./asset/image/cards/pickup-card.jpg"
        }
        $normalized += [pscustomobject]@{
            id          = "author"
            enabled     = $true
            order       = 2
            title       = "关于作者板块"
            description = "进入作者相关页面与内容索引"
            cover       = "./asset/image/cards/author-card.png"
        }
    }

    $sortedSections = @($normalized | Sort-Object order, title)
    $counter = 1
    foreach ($section in $sortedSections) {
        $section.order = $counter
        $counter++
    }
    $Data.sections = $sortedSections

    $defaultSectionId = [string]$Data.sections[0].id
    $validIds = @{}
    foreach ($section in @($Data.sections)) {
        $validIds[[string]$section.id] = $true
    }

    foreach ($item in @($Data.items)) {
        $sectionIdProp = $item.PSObject.Properties["sectionId"]
        if ($null -eq $sectionIdProp) {
            $item | Add-Member -MemberType NoteProperty -Name sectionId -Value $defaultSectionId
            continue
        }

        $candidate = Get-SafeSectionId -InputText ([string]$sectionIdProp.Value)
        if ([string]::IsNullOrWhiteSpace($candidate) -or -not $validIds.ContainsKey($candidate)) {
            $item.sectionId = $defaultSectionId
        }
        else {
            $item.sectionId = $candidate
        }
    }
}

function Get-DefaultSectionId {
    param([object]$Data)

    Normalize-Sections -Data $Data
    return [string]$Data.sections[0].id
}

function Get-SectionTitle {
    param(
        [object]$Data,
        [string]$SectionId
    )

    foreach ($section in @($Data.sections)) {
        if ([string]$section.id -eq $SectionId) {
            return [string]$section.title
        }
    }
    return "未分组"
}

function Get-NextItemOrderForSection {
    param(
        [object]$Data,
        [string]$SectionId
    )

    $maxOrder = 0
    foreach ($item in @($Data.items | Where-Object { [string]$_.sectionId -eq $SectionId })) {
        if ([int]$item.order -gt $maxOrder) {
            $maxOrder = [int]$item.order
        }
    }
    return ($maxOrder + 1)
}

function Get-LayoutLabel {
    param([string]$Layout)
    switch ($Layout) {
        "image-left" { return "图片在左" }
        "image-right" { return "图片在右" }
        "text-only" { return "纯文字" }
        default { return "图片在左" }
    }
}

function Get-LayoutValue {
    param([string]$Choice, [string]$Current = "image-left")
    switch ($Choice) {
        "1" { return "image-left" }
        "2" { return "image-right" }
        "3" { return "text-only" }
        default { return $Current }
    }
}

function Resolve-FileInput {
    param([string]$InputPath)
    if ([string]::IsNullOrWhiteSpace($InputPath)) { return $null }

    $value = $InputPath.Trim().Trim('"')
    if ($value -match '^https?://') {
        return [pscustomobject]@{ kind = "url"; path = $value }
    }

    if ([IO.Path]::IsPathRooted($value)) {
        if (Test-Path -LiteralPath $value -PathType Leaf) {
            return [pscustomobject]@{ kind = "file"; path = (Resolve-Path $value).Path }
        }
        return $null
    }

    $candidate = Join-Path $projectRoot $value
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return [pscustomobject]@{ kind = "file"; path = (Resolve-Path $candidate).Path }
    }
    return $null
}

function Get-SafeSlug {
    param([string]$InputText)
    $candidate = ($InputText.Trim().ToLower() -replace '[^a-z0-9\-]', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = "item-" + (Get-Date -Format "yyyyMMddHHmmss")
    }
    return $candidate
}

function Get-SafeSectionId {
    param([string]$InputText)

    if ([string]::IsNullOrWhiteSpace($InputText)) {
        return "section-" + (Get-Date -Format "yyyyMMddHHmmss")
    }

    $candidate = ($InputText.Trim().ToLower() -replace '[^\p{L}\p{Nd}\-_]', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = "section-" + (Get-Date -Format "yyyyMMddHHmmss")
    }
    return $candidate
}

function Get-UniqueSlug {
    param(
        [string]$Candidate,
        [object]$Data
    )

    $slug = $Candidate
    $index = 2
    $existing = @($Data.items | ForEach-Object { $_.pageSlug })
    while ($existing -contains $slug) {
        $slug = "$Candidate-$index"
        $index++
    }
    return $slug
}

function Get-ItemPageMode {
    param([object]$Item)
    $modeProp = $Item.PSObject.Properties["pageMode"]
    $mode = ""
    if ($null -ne $modeProp) {
        $mode = [string]$modeProp.Value
    }
    if ([string]::IsNullOrWhiteSpace($mode)) {
        return "generated"
    }
    return $mode.ToLower()
}

function Get-ItemPageRelativePath {
    param([object]$Item)

    $pageFileProp = $Item.PSObject.Properties["pageFile"]
    $pageFile = ""
    if ($null -ne $pageFileProp) {
        $pageFile = [string]$pageFileProp.Value
    }
    if ([string]::IsNullOrWhiteSpace($pageFile)) {
        $slug = [string]$Item.pageSlug
        if ([string]::IsNullOrWhiteSpace($slug)) {
            return ""
        }
        return "pages/content/$slug.html"
    }

    $normalized = $pageFile -replace '\\', '/'
    $normalized = $normalized -replace '^\./', ''
    return $normalized
}

function Import-Image {
    param(
        [string]$InputValue,
        [string]$SubFolder,
        [string]$Slug,
        [string]$Suffix,
        [string]$Fallback
    )

    if ([string]::IsNullOrWhiteSpace($InputValue)) {
        return $Fallback
    }

    $resolved = Resolve-FileInput -InputPath $InputValue
    if ($null -eq $resolved) {
        Write-Host "未找到图片：$InputValue，保留原值。" -ForegroundColor Yellow
        return $Fallback
    }

    if ($resolved.kind -eq "url") {
        return $resolved.path
    }

    $ext = [IO.Path]::GetExtension($resolved.path)
    if ([string]::IsNullOrWhiteSpace($ext)) {
        $ext = ".png"
    }
    $safeSlug = Get-SafeSlug -InputText $Slug
    $relative = "asset/image/$SubFolder/$safeSlug-$Suffix$ext".ToLower()
    $target = Join-Path $projectRoot $relative
    $targetDir = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir | Out-Null
    }

    $sourceFull = [IO.Path]::GetFullPath($resolved.path)
    $targetFull = [IO.Path]::GetFullPath($target)
    if ($sourceFull -ne $targetFull) {
        Copy-Item -LiteralPath $resolved.path -Destination $target -Force
    }

    return "./$($relative -replace '\\', '/')"
}

function Update-IndexTitles {
    param([object]$Data)
    $indexRaw = Get-Content -Raw -Encoding utf8 $paths.IndexFile

    $browserTitle = [string]$Data.site.browserTitle
    $bannerTitle = [string]$Data.site.bannerTitle

    $titleRegex = [System.Text.RegularExpressions.Regex]::new('<title>.*?</title>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $bannerRegex = [System.Text.RegularExpressions.Regex]::new('(<div class="title">\s*<p>).*?(</p>)', [System.Text.RegularExpressions.RegexOptions]::Singleline)

    $indexRaw = $titleRegex.Replace($indexRaw, [System.Text.RegularExpressions.MatchEvaluator]{
            param($m)
            return "<title>$browserTitle</title>"
        })

    $indexRaw = $bannerRegex.Replace($indexRaw, [System.Text.RegularExpressions.MatchEvaluator]{
            param($m)
            return "$($m.Groups[1].Value)$bannerTitle$($m.Groups[2].Value)"
        })

    Set-Content -Encoding utf8 -LiteralPath $paths.IndexFile -Value $indexRaw
}

function New-PageHtml {
    param([object]$Item)

    $layout = [string]$Item.pageLayout
    if ($layout -notin @("image-left", "image-right", "text-only")) {
        $layout = "image-left"
    }

    $title = Escape-Html ([string]$Item.pageTitle)
    $summary = Escape-Html ([string]$Item.pageSummary)
    $alt = Escape-Html ([string]$Item.cardAlt)
    if ([string]::IsNullOrWhiteSpace($alt)) {
        $alt = $title
    }

    $rawImage = [string]$Item.pageImage
    if ([string]::IsNullOrWhiteSpace($rawImage)) {
        $rawImage = [string]$Item.cardImage
    }
    $heroSrc = To-PageAssetPath -PathText $rawImage

    $paragraphLines = @()
    foreach ($line in @($Item.pageBody)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$line)) {
            $paragraphLines += "      <p>$(Escape-Html([string]$line))</p>"
        }
    }
    if ($paragraphLines.Count -eq 0) {
        $paragraphLines += "      <p>这里还没有内容，请回到管理器继续补充。</p>"
    }
    $paragraphHtml = $paragraphLines -join "`r`n"

    return @"
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>$title</title>
  <link rel="stylesheet" href="./content-page.css" />
</head>
<body class="layout-$layout">
  <div class="container">
    <div class="topbar">
      <a class="home-link" href="../../index.html">返回首页</a>
    </div>

    <section class="hero">
      <div class="hero-text">
        <h1>$title</h1>
        <p class="summary">$summary</p>
      </div>
      <div class="hero-media">
        <img src="$heroSrc" alt="$alt" />
      </div>
    </section>

    <section class="content">
$paragraphHtml
    </section>
  </div>
</body>
</html>
"@
}

function Build-Site {
    param([object]$Data)

    Normalize-Sections -Data $Data
    Normalize-ItemsOrder -Data $Data
    $enabledSections = @($Data.sections | Where-Object { $_.enabled } | Sort-Object order)
    if ($enabledSections.Count -eq 0) {
        $enabledSections = @($Data.sections | Sort-Object order)
    }

    $enabledItems = @()
    foreach ($section in $enabledSections) {
        $sectionEnabledItems = @(
            $Data.items |
            Where-Object { $_.enabled -and [string]$_.sectionId -eq [string]$section.id } |
            Sort-Object order, cardTitle
        )
        $enabledItems += $sectionEnabledItems
    }

    $cardLines = @()
    foreach ($item in $enabledItems) {
        $link = Get-ItemPageRelativePath -Item $item
        $cardLines += "  { title: $(To-JsString([string]$item.cardTitle)), image: $(To-JsString([string]$item.cardImage)), link: $(To-JsString($link)), alt: $(To-JsString([string]$item.cardAlt)) }"
    }

    $sectionLines = @()
    foreach ($section in $enabledSections) {
        $sectionItems = @($enabledItems | Where-Object { [string]$_.sectionId -eq [string]$section.id } | Sort-Object order)
        $sectionCardLines = @()
        foreach ($item in $sectionItems) {
            $link = Get-ItemPageRelativePath -Item $item
            $sectionCardLines += "      { title: $(To-JsString([string]$item.cardTitle)), image: $(To-JsString([string]$item.cardImage)), link: $(To-JsString($link)), alt: $(To-JsString([string]$item.cardAlt)) }"
        }

        $sectionCardsJs = if ($sectionCardLines.Count -eq 0) {
            "[]"
        }
        else {
            "[`r`n" + ($sectionCardLines -join ",`r`n") + "`r`n    ]"
        }

        $sectionLines += "  { id: $(To-JsString([string]$section.id)), title: $(To-JsString([string]$section.title)), description: $(To-JsString([string]$section.description)), cover: $(To-JsString([string]$section.cover)), cards: $sectionCardsJs }"
    }

    $configRaw = "const CARDS = [`r`n" + ($cardLines -join ",`r`n") + "`r`n];`r`n`r`nconst HOME_SECTIONS = [`r`n" + ($sectionLines -join ",`r`n") + "`r`n];`r`n"
    Set-Content -Encoding utf8 -LiteralPath $paths.ConfigFile -Value $configRaw

    Update-IndexTitles -Data $Data

    $expectedFiles = @()
    foreach ($item in @($Data.items | Sort-Object order)) {
        $relativePage = Get-ItemPageRelativePath -Item $item
        if ([string]::IsNullOrWhiteSpace($relativePage)) { continue }
        if ($relativePage -match '^https?://') { continue }

        $relativePage = $relativePage -replace '\\', '/'
        $isManagedPage = $relativePage.ToLower().StartsWith("pages/content/") -and $relativePage.ToLower().EndsWith(".html")
        $fileName = Split-Path -Leaf $relativePage

        if ($isManagedPage) {
            $expectedFiles += $fileName
            $target = Join-Path $paths.PagesDir $fileName
        }
        else {
            $target = Join-Path $projectRoot $relativePage
        }

        $pageMode = Get-ItemPageMode -Item $item
        if ($pageMode -eq "external") {
            if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
                Write-Host "警告：导入页面不存在 -> $relativePage" -ForegroundColor Yellow
            }
            continue
        }

        $html = New-PageHtml -Item $item
        Set-Content -Encoding utf8 -LiteralPath $target -Value $html
    }

    $existingGenerated = Get-ChildItem -LiteralPath $paths.PagesDir -Filter "*.html" -File -ErrorAction SilentlyContinue
    foreach ($file in $existingGenerated) {
        if ($expectedFiles -notcontains $file.Name) {
            Remove-Item -LiteralPath $file.FullName -Force
        }
    }

    Write-Host "已完成一键发布：共生成 $($enabledSections.Count) 个分类板块，$($enabledItems.Count) 个轮播卡片，$($expectedFiles.Count) 个内容页面。" -ForegroundColor Green
}

function Save-And-Build {
    param(
        [object]$Data,
        [string]$Reason
    )
    Normalize-Sections -Data $Data
    Normalize-ItemsOrder -Data $Data
    Save-SiteData -Data $Data -Reason $Reason
    Build-Site -Data $Data
}

function Prompt-WithDefault {
    param(
        [string]$Label,
        [string]$CurrentValue
    )
    $inputValue = Read-Host "$Label [$CurrentValue]"
    if ([string]::IsNullOrWhiteSpace($inputValue)) {
        return $CurrentValue
    }
    return $inputValue.Trim()
}

function Show-Items {
    param([object]$Data)
    Normalize-Sections -Data $Data
    Normalize-ItemsOrder -Data $Data
    $items = @($Data.items)
    if ($items.Count -eq 0) {
        Write-Host "当前没有任何内容。" -ForegroundColor Yellow
        return
    }
    Write-Host ""
    Write-Host "当前内容列表：" -ForegroundColor Cyan
    $index = 1
    foreach ($item in $items) {
        $stateText = if ($item.enabled) { "显示中" } else { "已隐藏" }
        $layoutText = Get-LayoutLabel -Layout ([string]$item.pageLayout)
        $sectionTitle = Get-SectionTitle -Data $Data -SectionId ([string]$item.sectionId)
        Write-Host "[$index] $($item.cardTitle) | 分类=$sectionTitle | slug=$($item.pageSlug) | 排版=$layoutText | 状态=$stateText"
        $index++
    }
    Write-Host ""
}

function Select-Item {
    param([object]$Data)
    Normalize-Sections -Data $Data
    Normalize-ItemsOrder -Data $Data
    $items = @($Data.items)
    if ($items.Count -eq 0) {
        Write-Host "当前没有可操作内容。" -ForegroundColor Yellow
        return $null
    }

    Show-Items -Data $Data
    $pick = Read-Host "请输入要操作的序号（回车取消）"
    if ([string]::IsNullOrWhiteSpace($pick)) { return $null }

    $number = 0
    if (-not [int]::TryParse($pick, [ref]$number)) {
        Write-Host "输入不是数字，已取消。" -ForegroundColor Yellow
        return $null
    }
    if ($number -lt 1 -or $number -gt $items.Count) {
        Write-Host "序号超出范围，已取消。" -ForegroundColor Yellow
        return $null
    }
    return $items[$number - 1]
}

function Prompt-LayoutChoice {
    param([string]$Current = "image-left")

    Write-Host ""
    Write-Host "请选择页面排版：" -ForegroundColor Cyan
    Write-Host "1. 图片在左，文字在右"
    Write-Host "2. 图片在右，文字在左"
    Write-Host "3. 纯文字（无顶部图片）"
    Write-Host "回车 = 保持当前（$(Get-LayoutLabel -Layout $Current)）"
    $layoutChoice = Read-Host "输入 1/2/3"
    return Get-LayoutValue -Choice $layoutChoice -Current $Current
}

function Split-BodyText {
    param([string]$RawBodyText)

    $parts = @()
    foreach ($piece in ($RawBodyText -split '\|')) {
        $clean = $piece.Trim()
        if (-not [string]::IsNullOrWhiteSpace($clean)) {
            $parts += $clean
        }
    }
    return @($parts)
}

function Add-ItemFlow {
    param([object]$Data)

    Write-Host ""
    Write-Host "=== 新增内容向导 ===" -ForegroundColor Cyan
    Write-Host "按提示填写即可。不会写代码也没关系。"
    Write-Host ""

    $cardTitle = Read-Host "1/8 请输入卡片标题（例如：我的摄影作品）"
    if ([string]::IsNullOrWhiteSpace($cardTitle)) {
        Write-Host "卡片标题不能为空，已取消。" -ForegroundColor Yellow
        return
    }

    $slugInput = Read-Host "2/8 请输入页面英文标识 slug（例如：photo-work，回车自动生成）"
    $baseSlug = if ([string]::IsNullOrWhiteSpace($slugInput)) { Get-SafeSlug -InputText "" } else { Get-SafeSlug -InputText $slugInput }
    $slug = Get-UniqueSlug -Candidate $baseSlug -Data $Data

    $pageTitle = Read-Host "3/8 请输入页面主标题（回车默认同卡片标题）"
    if ([string]::IsNullOrWhiteSpace($pageTitle)) { $pageTitle = $cardTitle }

    $summary = Read-Host "4/8 请输入页面简介（显示在标题下方）"
    if ([string]::IsNullOrWhiteSpace($summary)) { $summary = "这是一个新页面，你可以稍后继续完善内容。" }

    Write-Host "5/8 请输入正文。多段内容请用 | 分隔。"
    Write-Host "示例：第一段介绍 | 第二段详细说明 | 第三段联系方式"
    $bodyRaw = Read-Host "正文"
    $bodyLines = @(Split-BodyText -RawBodyText $bodyRaw)
    if ($bodyLines.Count -eq 0) {
        $bodyLines = @("这里是正文第一段。", "你可以在管理器中再次编辑这部分内容。")
    }

    $layout = Prompt-LayoutChoice -Current "image-left"

    Write-Host "6/8 请输入轮播卡片图片路径（可拖拽图片到窗口，或填网络图片 URL，回车用默认图）"
    $cardImageInput = Read-Host "卡片图片"
    $cardImage = Import-Image -InputValue $cardImageInput -SubFolder "cards" -Slug $slug -Suffix "card" -Fallback "./asset/image/cards/1.png"

    Write-Host "7/8 请输入页面顶部图片路径（回车默认使用卡片图）"
    $pageImageInput = Read-Host "页面图片"
    $pageImage = Import-Image -InputValue $pageImageInput -SubFolder "pages" -Slug $slug -Suffix "page" -Fallback $cardImage

    $cardAlt = Read-Host "8/8 请输入图片说明 alt（回车默认同卡片标题）"
    if ([string]::IsNullOrWhiteSpace($cardAlt)) { $cardAlt = $cardTitle }

    $defaultSectionId = Get-DefaultSectionId -Data $Data

    $newItem = [pscustomobject]@{
        id          = $slug
        sectionId   = $defaultSectionId
        enabled     = $true
        order       = (Get-NextItemOrderForSection -Data $Data -SectionId $defaultSectionId)
        cardTitle   = $cardTitle
        cardImage   = (Normalize-RelativePath -PathText $cardImage)
        cardAlt     = $cardAlt
        pageSlug    = $slug
        pageTitle   = $pageTitle
        pageSummary = $summary
        pageLayout  = $layout
        pageImage   = (Normalize-RelativePath -PathText $pageImage)
        pageBody    = @($bodyLines)
    }

    $Data.items = @($Data.items + $newItem)
    Save-And-Build -Data $Data -Reason "add-item"
    Write-Host "新增完成：$cardTitle（slug=$slug）" -ForegroundColor Green
}

function Add-LocalHtmlFlow {
    param([object]$Data)

    Write-Host ""
    Write-Host "=== 导入本地 HTML 向导 ===" -ForegroundColor Cyan
    Write-Host "这个功能会把你本地写好的 html 文件，直接添加为新页面。"
    Write-Host ""

    $htmlInput = Read-Host "1/6 请输入本地 HTML 文件路径（可拖拽文件到窗口）"
    if ([string]::IsNullOrWhiteSpace($htmlInput)) {
        Write-Host "未输入路径，已取消。" -ForegroundColor Yellow
        return
    }

    $resolved = Resolve-FileInput -InputPath $htmlInput
    if ($null -eq $resolved -or $resolved.kind -ne "file") {
        Write-Host "未找到本地文件，已取消。" -ForegroundColor Yellow
        return
    }

    $ext = [IO.Path]::GetExtension($resolved.path).ToLower()
    if ($ext -ne ".html" -and $ext -ne ".htm") {
        Write-Host "只支持导入 .html/.htm 文件，已取消。" -ForegroundColor Yellow
        return
    }

    $baseName = [IO.Path]::GetFileNameWithoutExtension($resolved.path)
    $defaultSlug = Get-SafeSlug -InputText $baseName

    $slugInput = Read-Host "2/6 请输入页面 slug（回车默认：$defaultSlug）"
    $baseSlug = if ([string]::IsNullOrWhiteSpace($slugInput)) { $defaultSlug } else { Get-SafeSlug -InputText $slugInput }
    $slug = Get-UniqueSlug -Candidate $baseSlug -Data $Data

    $cardTitle = Read-Host "3/6 请输入卡片标题（回车默认：$baseName）"
    if ([string]::IsNullOrWhiteSpace($cardTitle)) { $cardTitle = $baseName }

    $cardImageInput = Read-Host "4/6 请输入卡片图片路径（回车用默认图）"
    $cardImage = Import-Image -InputValue $cardImageInput -SubFolder "cards" -Slug $slug -Suffix "card" -Fallback "./asset/image/cards/1.png"

    $cardAlt = Read-Host "5/6 请输入图片说明 alt（回车默认同卡片标题）"
    if ([string]::IsNullOrWhiteSpace($cardAlt)) { $cardAlt = $cardTitle }

    $summary = Read-Host "6/6 请输入页面简介（可回车留空）"
    if ([string]::IsNullOrWhiteSpace($summary)) { $summary = "本页面来自本地导入 HTML 文件。" }

    $targetFileName = "$slug.html"
    $targetRelative = "pages/content/$targetFileName"
    $targetFull = Join-Path $paths.PagesDir $targetFileName
    $sourceFull = [IO.Path]::GetFullPath($resolved.path)
    $destFull = [IO.Path]::GetFullPath($targetFull)
    if ($sourceFull -ne $destFull) {
        Copy-Item -LiteralPath $resolved.path -Destination $targetFull -Force
    }

    $defaultSectionId = Get-DefaultSectionId -Data $Data

    $newItem = [pscustomobject]@{
        id          = $slug
        sectionId   = $defaultSectionId
        enabled     = $true
        order       = (Get-NextItemOrderForSection -Data $Data -SectionId $defaultSectionId)
        cardTitle   = $cardTitle
        cardImage   = (Normalize-RelativePath -PathText $cardImage)
        cardAlt     = $cardAlt
        pageSlug    = $slug
        pageFile    = $targetRelative
        pageMode    = "external"
        pageTitle   = $cardTitle
        pageSummary = $summary
        pageLayout  = "text-only"
        pageImage   = (Normalize-RelativePath -PathText $cardImage)
        pageBody    = @("该页面由本地 HTML 文件导入。")
    }

    $Data.items = @($Data.items + $newItem)
    Save-And-Build -Data $Data -Reason "add-local-html"
    Write-Host "导入完成：$cardTitle -> $targetRelative" -ForegroundColor Green
}

function Add-ExternalLinkFlow {
    param([object]$Data)

    Write-Host ""
    Write-Host "=== 添加外部网页链接向导 ===" -ForegroundColor Cyan
    Write-Host "这个功能会在轮播中新增一个卡片，点击后跳转到外部网址。"
    Write-Host ""

    $url = Read-Host "1/5 请输入外部网页链接（必须以 http:// 或 https:// 开头）"
    if ([string]::IsNullOrWhiteSpace($url)) {
        Write-Host "未输入链接，已取消。" -ForegroundColor Yellow
        return
    }
    $url = $url.Trim()
    if ($url -notmatch '^https?://') {
        Write-Host "链接格式不正确，已取消。" -ForegroundColor Yellow
        return
    }

    $cardTitle = Read-Host "2/5 请输入卡片标题（例如：我的博客）"
    if ([string]::IsNullOrWhiteSpace($cardTitle)) {
        Write-Host "卡片标题不能为空，已取消。" -ForegroundColor Yellow
        return
    }

    $slugInput = Read-Host "3/5 请输入标识 slug（回车自动生成）"
    $baseSlug = if ([string]::IsNullOrWhiteSpace($slugInput)) { Get-SafeSlug -InputText "" } else { Get-SafeSlug -InputText $slugInput }
    $slug = Get-UniqueSlug -Candidate $baseSlug -Data $Data

    $cardImageInput = Read-Host "4/5 请输入卡片图片路径（回车用默认图）"
    $cardImage = Import-Image -InputValue $cardImageInput -SubFolder "cards" -Slug $slug -Suffix "card" -Fallback "./asset/image/cards/1.png"

    $cardAlt = Read-Host "5/5 请输入图片说明 alt（回车默认同卡片标题）"
    if ([string]::IsNullOrWhiteSpace($cardAlt)) { $cardAlt = $cardTitle }

    $defaultSectionId = Get-DefaultSectionId -Data $Data

    $newItem = [pscustomobject]@{
        id          = $slug
        sectionId   = $defaultSectionId
        enabled     = $true
        order       = (Get-NextItemOrderForSection -Data $Data -SectionId $defaultSectionId)
        cardTitle   = $cardTitle
        cardImage   = (Normalize-RelativePath -PathText $cardImage)
        cardAlt     = $cardAlt
        pageSlug    = $slug
        pageFile    = $url
        pageMode    = "external-link"
        pageTitle   = $cardTitle
        pageSummary = "外部链接页面"
        pageLayout  = "text-only"
        pageImage   = (Normalize-RelativePath -PathText $cardImage)
        pageBody    = @("该卡片跳转到外部网页链接。")
    }

    $Data.items = @($Data.items + $newItem)
    Save-And-Build -Data $Data -Reason "add-external-link"
    Write-Host "添加完成：$cardTitle -> $url" -ForegroundColor Green
}

function Edit-ItemFlow {
    param([object]$Data)

    Write-Host ""
    Write-Host "=== 编辑内容向导 ===" -ForegroundColor Cyan
    $target = Select-Item -Data $Data
    if ($null -eq $target) { return }

    Write-Host ""
    Write-Host "回车 = 保持原值。"

    $target.cardTitle = Prompt-WithDefault -Label "卡片标题" -CurrentValue ([string]$target.cardTitle)
    $target.pageTitle = Prompt-WithDefault -Label "页面主标题" -CurrentValue ([string]$target.pageTitle)
    $target.pageSummary = Prompt-WithDefault -Label "页面简介" -CurrentValue ([string]$target.pageSummary)
    $target.cardAlt = Prompt-WithDefault -Label "图片说明 alt" -CurrentValue ([string]$target.cardAlt)

    $bodyCurrent = (@($target.pageBody) -join " | ")
    $bodyInput = Read-Host "正文（多段用 | 分隔） [$bodyCurrent]"
    if (-not [string]::IsNullOrWhiteSpace($bodyInput)) {
        $newBody = @(Split-BodyText -RawBodyText $bodyInput)
        if ($newBody.Count -gt 0) {
            $target.pageBody = @($newBody)
        }
    }

    $target.pageLayout = Prompt-LayoutChoice -Current ([string]$target.pageLayout)

    Write-Host "如果要换卡片图片，请输入新路径；回车保持不变。"
    $newCardImage = Read-Host "卡片图片路径"
    if (-not [string]::IsNullOrWhiteSpace($newCardImage)) {
        $target.cardImage = Import-Image -InputValue $newCardImage -SubFolder "cards" -Slug ([string]$target.pageSlug) -Suffix "card" -Fallback ([string]$target.cardImage)
    }

    Write-Host "如果要换页面图片，请输入新路径；回车保持不变。"
    $newPageImage = Read-Host "页面图片路径"
    if (-not [string]::IsNullOrWhiteSpace($newPageImage)) {
        $target.pageImage = Import-Image -InputValue $newPageImage -SubFolder "pages" -Slug ([string]$target.pageSlug) -Suffix "page" -Fallback ([string]$target.pageImage)
    }

    $stateInput = Read-Host "是否在首页显示？(Y/N，回车保持当前)"
    if ($stateInput -match '^[Yy]$') { $target.enabled = $true }
    if ($stateInput -match '^[Nn]$') { $target.enabled = $false }

    $orderInput = Read-Host "显示顺序（数字越小越靠前） [$($target.order)]"
    if (-not [string]::IsNullOrWhiteSpace($orderInput)) {
        $parsed = 0
        if ([int]::TryParse($orderInput, [ref]$parsed) -and $parsed -gt 0) {
            $target.order = $parsed
        }
    }

    $target.cardImage = Normalize-RelativePath -PathText ([string]$target.cardImage)
    $target.pageImage = Normalize-RelativePath -PathText ([string]$target.pageImage)

    Save-And-Build -Data $Data -Reason "edit-item"
    Write-Host "编辑完成：$($target.cardTitle)" -ForegroundColor Green
}

function Remove-IfUnusedImage {
    param(
        [string]$ImagePath,
        [object]$Data
    )

    if ([string]::IsNullOrWhiteSpace($ImagePath)) { return }
    if ($ImagePath -notmatch '^\.\/asset/image/') { return }

    foreach ($item in @($Data.items)) {
        if ($item.cardImage -eq $ImagePath -or $item.pageImage -eq $ImagePath) {
            return
        }
    }

    $relative = $ImagePath -replace '^\./', ''
    $full = Join-Path $projectRoot $relative
    if (Test-Path -LiteralPath $full -PathType Leaf) {
        Remove-Item -LiteralPath $full -Force
    }
}

function Delete-ItemFlow {
    param([object]$Data)

    Write-Host ""
    Write-Host "=== 删除内容向导 ===" -ForegroundColor Cyan
    $target = Select-Item -Data $Data
    if ($null -eq $target) { return }

    $confirm = Read-Host "确认删除 [$($target.cardTitle)] 吗？输入 DELETE 确认"
    if ($confirm -ne "DELETE") {
        Write-Host "已取消删除。" -ForegroundColor Yellow
        return
    }

    $oldCardImage = [string]$target.cardImage
    $oldPageImage = [string]$target.pageImage
    $slug = [string]$target.pageSlug

    $remaining = @($Data.items | Where-Object { $_.id -ne $target.id })
    $Data.items = $remaining

    Save-And-Build -Data $Data -Reason "delete-item"

    $removeImageChoice = Read-Host "是否顺便删除该内容关联的图片文件？(Y/N)"
    if ($removeImageChoice -match '^[Yy]$') {
        Remove-IfUnusedImage -ImagePath $oldCardImage -Data $Data
        Remove-IfUnusedImage -ImagePath $oldPageImage -Data $Data
        Write-Host "图片清理已完成（仅删除未被其他内容使用的文件）。" -ForegroundColor Green
    }

    Write-Host "删除完成：$slug" -ForegroundColor Green
}

function Edit-SiteInfoFlow {
    param([object]$Data)

    Write-Host ""
    Write-Host "=== 编辑首页标题 ===" -ForegroundColor Cyan
    $browserTitle = Prompt-WithDefault -Label "浏览器标题（<title>）" -CurrentValue ([string]$Data.site.browserTitle)
    $bannerTitle = Prompt-WithDefault -Label "首页横幅主文案" -CurrentValue ([string]$Data.site.bannerTitle)

    $Data.site.browserTitle = $browserTitle
    $Data.site.bannerTitle = $bannerTitle
    Save-And-Build -Data $Data -Reason "edit-site-title"
    Write-Host "首页标题已更新。" -ForegroundColor Green
}

function Show-Menu {
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor DarkGray
    Write-Host " 一键网页内容管理器（不需要改代码）" -ForegroundColor Cyan
    Write-Host "==========================================" -ForegroundColor DarkGray
    Write-Host "1. 一键发布（同步全部内容到网页）"
    Write-Host "2. 新增内容（新增卡片 + 新页面）"
    Write-Host "3. 删除内容（删除卡片 + 页面）"
    Write-Host "4. 编辑现有内容（文字 / 图片 / 排版）"
    Write-Host "5. 编辑首页标题"
    Write-Host "6. 查看内容列表"
    Write-Host "7. 导入本地HTML（直接添加你写好的网页）"
    Write-Host "8. 添加外部网页链接"
    Write-Host "0. 退出"
    Write-Host ""
}

try {
    Ensure-ProjectLayout
    $data = Read-SiteData

    if ($Mode -eq "Build") {
        Build-Site -Data $data
        exit 0
    }

    $shouldExit = $false
    while (-not $shouldExit) {
        Show-Menu
        $choice = Read-Host "请输入菜单编号"
        switch ($choice) {
            "1" {
                Save-And-Build -Data $data -Reason "manual-build"
                Read-Host "按回车继续"
            }
            "2" {
                Add-ItemFlow -Data $data
                Read-Host "按回车继续"
            }
            "3" {
                Delete-ItemFlow -Data $data
                Read-Host "按回车继续"
            }
            "4" {
                Edit-ItemFlow -Data $data
                Read-Host "按回车继续"
            }
            "5" {
                Edit-SiteInfoFlow -Data $data
                Read-Host "按回车继续"
            }
            "6" {
                Show-Items -Data $data
                Read-Host "按回车继续"
            }
            "7" {
                Add-LocalHtmlFlow -Data $data
                Read-Host "按回车继续"
            }
            "8" {
                Add-ExternalLinkFlow -Data $data
                Read-Host "按回车继续"
            }
            "0" {
                $shouldExit = $true
            }
            default {
                Write-Host "无效输入，请输入 0-8 之间的数字。" -ForegroundColor Yellow
                Read-Host "按回车继续"
            }
        }
    }
}
catch {
    Write-Host ""
    Write-Host "发生错误：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host "请把这段报错发给我，我会继续帮你修复。" -ForegroundColor Yellow
    exit 1
}

