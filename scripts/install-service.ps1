param(
  [string]$ServiceName = "M'awuGab Agent",
  [string]$DisplayName = "M'awuGab Agent",
  [string]$Description = "Collecte, compresse et transmet des journaux .jrn via SFTP, avec mise à jour automatique et monitoring.",
  [string]$ExePath = "$(Resolve-Path ..\src\MawuGab\bin\Release\net8.0-windows\MawuGab.exe)"
)

Write-Host "Installing service $ServiceName from $ExePath" -ForegroundColor Cyan

New-Item -ItemType Directory -Path "C:\ProgramData\MawuGab\logs" -Force | Out-Null
New-Item -ItemType Directory -Path "C:\ProgramData\MawuGab\queue" -Force | Out-Null

sc.exe create "$ServiceName" binPath= '"' + $ExePath + '"' start= auto DisplayName= '"' + $DisplayName + '"'
sc.exe description "$ServiceName" "$Description"

# Recovery: restart on crash
sc.exe failure "$ServiceName" reset= 86400 actions= restart/60000/restart/60000/restart/60000

Start-Service "$ServiceName"
Write-Host "Service installed and started." -ForegroundColor Green
