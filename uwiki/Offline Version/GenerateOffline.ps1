# GenerateOffline.ps1
# Automates the creation of the Single-File Offline SPA for the Boriel Basic Portal

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$baseRawUrl = "https://raw.githubusercontent.com/boriel-basic/zxbasic/main/"
$treeUrl = "https://api.github.com/repos/boriel-basic/zxbasic/git/trees/main?recursive=1"
$commitUrl = "https://api.github.com/repos/boriel-basic/zxbasic/commits?path=docs&per_page=1"
$htmlPath = "index.html"
$outPath = "ZXBasic_Offline_Manual.html"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  ZX Basic Offline Manual Generator" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Fetch File Tree
Write-Host "`n[1/4] Fetching Git Repository Tree..." -ForegroundColor Yellow
$treeResponse = Invoke-RestMethod -Uri $treeUrl -Method Get
$mdFiles = $treeResponse.tree | Where-Object { $_.path -like "docs/*.md" }

$total = $mdFiles.Count
Write-Host "      Found $total Markdown files." -ForegroundColor Green

# 2. Fetch Latest Commit Date
Write-Host "`n[2/4] Fetching Last Update Date..." -ForegroundColor Yellow
$commitResponse = Invoke-RestMethod -Uri $commitUrl -Method Get
$commitDate = [datetime]$commitResponse[0].commit.committer.date
$formattedDate = $commitDate.ToString("MMM d, yyyy", [cultureinfo]::InvariantCulture)
Write-Host "      Latest commit: $formattedDate" -ForegroundColor Green

# 3. Download Markdown Content into Cache Object
Write-Host "`n[3/4] Downloading Markdown Content..." -ForegroundColor Yellow
$cache = @{}
$count = 1

foreach ($file in $mdFiles) {
    Write-Host "      --> [$count/$total] Downloading: $($file.path)"
    $rawUrl = $baseRawUrl + $file.path
    $content = Invoke-RestMethod -Uri $rawUrl -Method Get
    $cache[$file.path] = $content
    $count++
}

# 4. Inject Data into HTML
Write-Host "`n[4/4] Assembling Offline HTML Bundle..." -ForegroundColor Yellow
if (!(Test-Path $htmlPath)) {
    Write-Host "ERROR: Cannot find $htmlPath in the current directory." -ForegroundColor Red
    exit
}

$htmlContent = Get-Content -Path $htmlPath -Raw

# Remove the offline button so it doesn't appear in the exported file
$htmlContent = $htmlContent -replace '(?s)<button id="btn-offline".*?</button>', ''

# Create the master payload including the fetched date
$payload = @{
    tree = $treeResponse.tree
    cache = $cache
    date = $formattedDate
}

# Convert objects to JSON securely
$payloadJson = $payload | ConvertTo-Json -Depth 10 -Compress

# CRITICAL FIX: Escape brackets so HTML syntax inside markdown doesn't break the script tag!
$payloadJson = $payloadJson.Replace("<", "\u003c").Replace(">", "\u003e")

# We look for the exact literal string we put in index.html to avoid ALL Regex parsing bugs
$pattern = '<script id="offline-data-island" type="application/json">{}</script>'
$replacement = "<script id=`"offline-data-island`" type=`"application/json`">$payloadJson</script>"

if ($htmlContent.Contains($pattern)) {
    $htmlContent = $htmlContent.Replace($pattern, $replacement)
} else {
    Write-Host "ERROR: Could not find the offline-data-island string in index.html!" -ForegroundColor Red
    Write-Host "Make sure you copied the latest index.html exactly as provided." -ForegroundColor Red
    exit
}

# Write the final file 
[IO.File]::WriteAllText((Join-Path (Get-Location) $outPath), $htmlContent, [System.Text.Encoding]::UTF8)

Write-Host "`nSUCCESS! Offline Manual generated as:" -ForegroundColor Green
Write-Host "=> $outPath" -ForegroundColor White