# Test debug endpoint
$apiUrl = "https://tranquil-upliftment-production.up.railway.app"

Write-Host "Testing debug endpoint..." -ForegroundColor Cyan

# Login
$body = @{ userName = "admin"; password = "140606tl" } | ConvertTo-Json
$auth = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$token = $auth.accessToken

# Call debug endpoint
$headers = @{ Authorization = "Bearer $token" }
$result = Invoke-RestMethod -Uri "$apiUrl/api/products/debug" -Headers $headers

Write-Host "`nDebug result:" -ForegroundColor Yellow
$result | ConvertTo-Json -Depth 5

if ($result.success) {
    Write-Host "`nSUCCESS! Products table is readable" -ForegroundColor Green
    Write-Host "Sample row:" -ForegroundColor Cyan
    $result.rows[0] | ConvertTo-Json
} else {
    Write-Host "`nERROR!" -ForegroundColor Red
    Write-Host "Error: $($result.error)"
    Write-Host "`nStack trace:"
    Write-Host $result.stackTrace
}
