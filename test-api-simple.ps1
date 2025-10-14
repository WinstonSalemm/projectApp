# API Test Script
$apiUrl = "https://tranquil-upliftment-production.up.railway.app"

Write-Host "Testing API: $apiUrl"

# Test 1: Health check
Write-Host "`nTest 1: Health check..."
try {
    $health = Invoke-RestMethod -Uri "$apiUrl/health" -Method Get
    Write-Host "SUCCESS: API is alive"
} catch {
    Write-Host "ERROR: API is down - $_"
    exit 1
}

# Test 2: Get categories (no auth)
Write-Host "`nTest 2: Get categories (no auth)..."
try {
    $categories = Invoke-RestMethod -Uri "$apiUrl/api/products/categories" -Method Get
    Write-Host "SUCCESS: Got $($categories.Count) categories"
    $categories | ForEach-Object { Write-Host "  - $_" }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}

# Test 3: Get products (no auth)
Write-Host "`nTest 3: Get products (no auth)..."
try {
    $products = Invoke-RestMethod -Uri "$apiUrl/api/products?page=1&size=10" -Method Get
    Write-Host "SUCCESS: Got $($products.Items.Count) products out of $($products.Total)"
    $products.Items | Select-Object -First 5 | ForEach-Object { 
        Write-Host "  - [$($_.Sku)] $($_.Name) - $($_.UnitPrice)"
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}

# Test 4: Login
Write-Host "`nTest 4: Login as admin..."
$body = @{
    userName = "admin"
    password = $null
} | ConvertTo-Json

try {
    $auth = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    Write-Host "SUCCESS: Logged in as $($auth.role)"
    $token = $auth.accessToken
} catch {
    Write-Host "ERROR: Login failed - $_"
    exit 1
}

# Test 5: Get stocks (with auth)
Write-Host "`nTest 5: Get stocks (with auth)..."
try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    $stocks = Invoke-RestMethod -Uri "$apiUrl/api/stocks" -Method Get -Headers $headers
    Write-Host "SUCCESS: Got $($stocks.Count) stocks"
    $stocks | Select-Object -First 5 | ForEach-Object { 
        Write-Host "  - [$($_.Sku)] $($_.Name): ND40=$($_.Nd40Qty), IM40=$($_.Im40Qty)"
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}

Write-Host "`nTest completed!"
