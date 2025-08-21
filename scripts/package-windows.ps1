param(
  [string]$Configuration = "Release",
  [string]$Framework = "net8.0-windows",
  [string]$Runtime = "win-x64",
  [switch]$SelfContained,
  [string]$SshNetVersion = ""  # optional explicit version override
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


# Helper: set Renci.SshNet version in csproj (net8.0-windows ItemGroup)
function Set-SshNetVersionInCsproj([string]$csprojPath, [string]$version) {
  [xml]$xml = Get-Content $csprojPath
  $node = $xml.Project.ItemGroup | Where-Object { $_.Condition -eq "'$(TargetFramework)'=='net8.0-windows'" } |
          ForEach-Object { $_.PackageReference } | Where-Object { $_.Include -eq "Renci.SshNet" }
  if (-not $node) { throw "Renci.SshNet PackageReference not found in $csprojPath" }
  $node.Version = $version
  $xml.Save($csprojPath)
}

$originalXml = Get-Content $proj -Raw
$candidates = @()
if ([string]::IsNullOrWhiteSpace($SshNetVersion)) {
  # Try from newest to older commonly available versions
  $candidates = @( "2023.0.0", "2020.0.1", "2016.1.0", "2013.4.7", "2013.4.6" )
} else {
  $candidates = @($SshNetVersion)
}

$nugetConfig = Join-Path $repoRoot "NuGet.config"

$published = $false
foreach ($ver in $candidates) {
  try {
    Write-Host "Trying Renci.SshNet version $ver" -ForegroundColor Yellow
    Set-SshNetVersionInCsproj -csprojPath $proj -version $ver

    # Restore first to fail fast
    $restoreArgs = @( "restore", $proj )
    if (Test-Path $nugetConfig) { $restoreArgs += @("--configfile", $nugetConfig) }
    Write-Host "Restoring: dotnet $($restoreArgs -join ' ')" -ForegroundColor Cyan
    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw "restore failed for $ver" }

    $publishArgs = @(
      "publish", $proj,
      "-c", $Configuration,
      "-f", $Framework,
      "-r", $Runtime,
      "--self-contained", $scValue
    )
    if (Test-Path $nugetConfig) { $publishArgs += @("--configfile", $nugetConfig) }
    Write-Host "Publishing: dotnet $($publishArgs -join ' ')" -ForegroundColor Cyan
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $ver" }

    $published = $true
    break
  }
  catch {
    Write-Warning $_
    continue
  }
}

# restore original csproj content on disk (we only needed a temporary change)
Set-Content -Path $proj -Value $originalXml -Encoding UTF8

if (-not $published) { throw "Failed to publish after trying versions: $($candidates -join ', ')" }

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
