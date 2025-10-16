# Kill all running instances
Write-Host "Closing app..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.ProcessName -like "*ProjectApp*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Clean and rebuild
Write-Host "Rebuilding..." -ForegroundColor Yellow
cd "C:\projectApp"
dotnet build "src\ProjectApp.Client.Maui\ProjectApp.Client.Maui.csproj" -c Debug -f net9.0-windows10.0.19041.0

# Run
Write-Host "Starting app..." -ForegroundColor Green
$exePath = "C:\projectApp\src\ProjectApp.Client.Maui\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\ProjectApp.Client.Maui.exe"
if (Test-Path $exePath) {
    Start-Process -FilePath $exePath
    Write-Host "App started!" -ForegroundColor Green
} else {
    Write-Host "ERROR: EXE not found at $exePath" -ForegroundColor Red
}
