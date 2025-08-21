param(
  [string]$Configuration = "Release",
  [string]$Framework = "net8.0-windows",
  [string]$Runtime = "win-x64",
  [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

# Resolve paths
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $repoRoot  # go up from scripts/
$proj = Join-Path $repoRoot "src/MawuGab/MawuGab.csproj"

# Determine version
[xml]$csproj = Get-Content $proj
$versionNode = $csproj.Project.PropertyGroup.Version
if (-not $versionNode) { $version = "1.0.0" } else { $version = $versionNode }
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"

# Publish
$sc = $false
if ($SelfContained.IsPresent) { $sc = $true }
$scValue = if ($sc) { "true" } else { "false" }

$publishArgs = @(
  "publish", $proj,
  "-c", $Configuration,
  "-f", $Framework,
  "-r", $Runtime,
  "--self-contained", $scValue
)

Write-Host "Publishing: dotnet $($publishArgs -join ' ')" -ForegroundColor Cyan
& dotnet @publishArgs

$pubDir = Join-Path $repoRoot "src/MawuGab/bin/$Configuration/$Framework/$Runtime/publish"
if (!(Test-Path $pubDir)) { throw "Publish folder not found: $pubDir" }

# Create artifacts folder
$artifacts = Join-Path $repoRoot "artifacts"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# Zip name
$zipName = "MawuGab-$Framework-$Runtime-$version-$stamp.zip"
$zipPath = Join-Path $artifacts $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# Create zip
Write-Host "Zipping to $zipPath" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $pubDir '*') -DestinationPath $zipPath -Force

Write-Host "Done." -ForegroundColor Green
Write-Host "Publish folder: $pubDir"
Write-Host "Zip created:    $zipPath"
