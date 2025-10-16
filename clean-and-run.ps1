# Kill app
Write-Host "Closing app..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.ProcessName -like "*ProjectApp*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Clear saved auth data
Write-Host "Clearing auth data..." -ForegroundColor Yellow
Remove-Item -Path "$env:LOCALAPPDATA\Microsoft\Maui\*" -Recurse -Force -ErrorAction SilentlyContinue

# Rebuild
Write-Host "Rebuilding..." -ForegroundColor Yellow
Set-Location "C:\projectApp"
dotnet build "src\ProjectApp.Client.Maui\ProjectApp.Client.Maui.csproj" -c Debug -f net9.0-windows10.0.19041.0

# Run
Write-Host "Starting app..." -ForegroundColor Green
$exePath = "C:\projectApp\src\ProjectApp.Client.Maui\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\ProjectApp.Client.Maui.exe"
Start-Process -FilePath $exePath
Write-Host "Done!" -ForegroundColor Green
