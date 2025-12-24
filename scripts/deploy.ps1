$ErrorActionPreference = "Stop"

# Configuration
$RepoRoot = Resolve-Path "$PSScriptRoot\.."
$ProjectFile = "$RepoRoot\plugins\ParentGuard\src\ParentGuard\ParentGuard.csproj"
$DistDir = "$RepoRoot\dist"
$RepoJsonFile = "$RepoRoot\plugins\ParentGuard\repository.json"
$ManifestFile = "$RepoRoot\plugins\ParentGuard\src\ParentGuard\manifest.json"
$IconUrl = "https://raw.githubusercontent.com/burrellka/JellyFin-PC-KB/main/plugins/ParentGuard/icon.png"
$RawUrlBase = "https://raw.githubusercontent.com/burrellka/JellyFin-PC-KB/main/dist/ParentGuard.zip"

Write-Host "=== ParentGuard Deployment Script ===" -ForegroundColor Cyan

# 1. Build
Write-Host "1. Building Release..." -ForegroundColor Yellow
dotnet build -c Release $ProjectFile
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 2. Package
Write-Host "2. Packaging..." -ForegroundColor Yellow
if (Test-Path $DistDir) { Remove-Item $DistDir -Recurse -Force }
New-Item -ItemType Directory -Path $DistDir | Out-Null

$BinDir = "$RepoRoot\plugins\ParentGuard\src\ParentGuard\bin\Release\net8.0"
Copy-Item "$BinDir\Jellyfin.Plugin.ParentGuard.dll" $DistDir
Copy-Item $ManifestFile $DistDir

$ZipPath = "$DistDir\ParentGuard.zip"
Compress-Archive -Path "$DistDir\Jellyfin.Plugin.ParentGuard.dll", "$DistDir\manifest.json" -DestinationPath $ZipPath -Force

# 3. Checksum
Write-Host "3. Calculating Checksum..." -ForegroundColor Yellow
$MD5 = (Get-FileHash $ZipPath -Algorithm MD5).Hash.ToLower()
Write-Host "   MD5: $MD5" -ForegroundColor Gray

# 4. Update repository.json
Write-Host "4. Updating repository.json..." -ForegroundColor Yellow
$Manifest = Get-Content $ManifestFile | ConvertFrom-Json
$Version = $Manifest.version
$TargetAbi = $Manifest.targetAbi

$RepoJson = @(Get-Content $RepoJsonFile -Raw | ConvertFrom-Json)
$OldPluginEntry = $RepoJson[0]

# Convert existing versions to ArrayList of HashTables to allow modification
$VersionsList = New-Object System.Collections.Generic.List[Object]
foreach ($v in $OldPluginEntry.versions) {
    if ($v.version -ne $Version) {
        $VersionsList.Add($v)
    }
}

# Add new version at top
$NewVersion = @{
    version   = $Version
    targetAbi = $TargetAbi
    sourceUrl = $RawUrlBase
    checksum  = $MD5
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}
$VersionsList.Insert(0, $NewVersion)

# Reconstruct the plugin entry
$NewPluginEntry = @{
    name        = $OldPluginEntry.name
    imageUrl    = $OldPluginEntry.imageUrl
    overview    = $OldPluginEntry.overview
    description = $OldPluginEntry.description
    owner       = $OldPluginEntry.owner
    category    = $OldPluginEntry.category
    guid        = $OldPluginEntry.guid
    versions    = $VersionsList
}

# Reconstruct the repo array
$NewRepoArray = @($NewPluginEntry)

# Handle single object vs array issue with ConvertTo-Json
$JsonOutput = $NewRepoArray | ConvertTo-Json -Depth 10
if ($NewRepoArray.Count -eq 1 -and !$JsonOutput.StartsWith("[")) {
    $JsonOutput = "[$JsonOutput]"
}
$JsonOutput | Set-Content $RepoJsonFile

Write-Host "=== Deployment Prep Complete ===" -ForegroundColor Green
Write-Host "Now run:" -ForegroundColor White

Write-Host "git add ." -ForegroundColor White
Write-Host "git commit -m 'Release v$Version'" -ForegroundColor White
Write-Host "git push" -ForegroundColor White
