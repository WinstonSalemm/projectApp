# Kill app
Write-Host "Closing app..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.ProcessName -like "*ProjectApp*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Clear ALL app data (Preferences are stored here)
Write-Host "Clearing app data..." -ForegroundColor Yellow
$appDataPaths = @(
    "$env:LOCALAPPDATA\Packages\com.projectapp.client*",
    "$env:LOCALAPPDATA\Microsoft\Maui\*ProjectApp*",
    "$env:APPDATA\ProjectApp*"
)

foreach ($path in $appDataPaths) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Removed: $path" -ForegroundColor Gray
    }
}

# Rebuild
Write-Host "Rebuilding..." -ForegroundColor Yellow
Set-Location "C:\projectApp"
dotnet build "src\ProjectApp.Client.Maui\ProjectApp.Client.Maui.csproj" -c Debug -f net9.0-windows10.0.19041.0 | Out-Null

# Run
Write-Host "Starting fresh app..." -ForegroundColor Green
$exePath = "C:\projectApp\src\ProjectApp.Client.Maui\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\ProjectApp.Client.Maui.exe"
Start-Process -FilePath $exePath
Write-Host "`nApp should now show USER SELECT screen!" -ForegroundColor Green
Write-Host "Choose a manager -> Payment selection (no tabs)" -ForegroundColor Cyan
Write-Host "Choose Administrator -> Enter password -> Admin panel (with tabs)" -ForegroundColor Cyan
