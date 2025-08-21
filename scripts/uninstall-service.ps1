param(
  [string]$ServiceName = "M'awuGab Agent"
)

Write-Host "Stopping and removing service $ServiceName" -ForegroundColor Yellow
try { Stop-Service "$ServiceName" -ErrorAction SilentlyContinue } catch {}
sc.exe delete "$ServiceName"
Write-Host "Service removed." -ForegroundColor Green
