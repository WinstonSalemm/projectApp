# Test Railway API with detailed error
$apiUrl = "https://tranquil-upliftment-production.up.railway.app"

Write-Host "Testing Railway API..." -ForegroundColor Cyan

# Login
$body = @{ userName = "admin"; password = "140606tl" } | ConvertTo-Json
try {
    $auth = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    Write-Host "Login: OK" -ForegroundColor Green
    $token = $auth.accessToken
} catch {
    Write-Host "Login: FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message
    exit 1
}

# Try to get products with detailed error
Write-Host "`nTrying to get products..." -ForegroundColor Yellow
try {
    $headers = @{ Authorization = "Bearer $token" }
    $response = Invoke-WebRequest -Uri "$apiUrl/api/products?page=1&size=5" -Method Get -Headers $headers
    Write-Host "Products: OK" -ForegroundColor Green
    Write-Host $response.Content
} catch {
    Write-Host "Products: FAILED" -ForegroundColor Red
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "Status Code: $statusCode"
    
    try {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "`nError Response:"
        Write-Host $errorBody
        
        # Try to parse as JSON
        try {
            $errorJson = $errorBody | ConvertFrom-Json
            Write-Host "`nParsed Error:"
            Write-Host "Type: $($errorJson.type)"
            Write-Host "Title: $($errorJson.title)"
            Write-Host "Status: $($errorJson.status)"
            Write-Host "Detail: $($errorJson.detail)"
            Write-Host "CorrelationId: $($errorJson.correlationId)"
        } catch {
            Write-Host "Could not parse error as JSON"
        }
    } catch {
        Write-Host "Could not read error response"
    }
}

Write-Host "`nNow check Railway logs with this correlationId!"
