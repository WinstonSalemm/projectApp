# Test stocks endpoint
$apiUrl = "https://tranquil-upliftment-production.up.railway.app"

Write-Host "Testing stocks endpoint..." -ForegroundColor Cyan

# Login
$body = @{ userName = "admin"; password = "140606tl" } | ConvertTo-Json
$auth = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$token = $auth.accessToken

# Call stocks endpoint
$headers = @{ Authorization = "Bearer $token" }
try {
    $result = Invoke-RestMethod -Uri "$apiUrl/api/stocks" -Headers $headers
    Write-Host "`nSUCCESS! Got $($result.Count) stock items" -ForegroundColor Green
    $result | Select-Object -First 5 | ConvertTo-Json
} catch {
    Write-Host "`nERROR!" -ForegroundColor Red
    Write-Host "Status: $($_.Exception.Response.StatusCode.value__)"
    Write-Host "Message: $($_.Exception.Message)"
    
    # Try to get error details
    $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
    $errorBody = $reader.ReadToEnd()
    Write-Host "`nError body:"
    $errorBody | ConvertFrom-Json | ConvertTo-Json -Depth 5
}
