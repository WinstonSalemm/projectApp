# Test different Railway endpoints
$apiUrl = "https://tranquil-upliftment-production.up.railway.app"

Write-Host "Testing Railway endpoints..." -ForegroundColor Cyan

# Test 1: Health
Write-Host "`n1. /health"
try {
    $response = Invoke-RestMethod -Uri "$apiUrl/health"
    Write-Host "   OK: $response" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Ready (DB check)
Write-Host "`n2. /ready"
try {
    $response = Invoke-RestMethod -Uri "$apiUrl/ready"
    Write-Host "   OK: $response" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Login
Write-Host "`n3. /api/auth/login"
$body = @{ userName = "admin"; password = "140606tl" } | ConvertTo-Json
try {
    $auth = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    Write-Host "   OK: Logged in as $($auth.role)" -ForegroundColor Green
    $token = $auth.accessToken
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 4: Categories (no auth)
Write-Host "`n4. /api/products/categories (no auth)"
try {
    $categories = Invoke-RestMethod -Uri "$apiUrl/api/products/categories"
    Write-Host "   OK: Got $($categories.Count) categories" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

# Test 5: Categories (with auth)
Write-Host "`n5. /api/products/categories (with auth)"
try {
    $headers = @{ Authorization = "Bearer $token" }
    $categories = Invoke-RestMethod -Uri "$apiUrl/api/products/categories" -Headers $headers
    Write-Host "   OK: Got $($categories.Count) categories" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

# Test 6: Products (with auth)
Write-Host "`n6. /api/products (with auth)"
try {
    $headers = @{ Authorization = "Bearer $token" }
    $products = Invoke-RestMethod -Uri "$apiUrl/api/products?page=1&size=5" -Headers $headers
    Write-Host "   OK: Got $($products.Items.Count) products" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

# Test 7: Seed endpoint
Write-Host "`n7. /api/products/seed-standard"
try {
    $headers = @{ Authorization = "Bearer $token" }
    $result = Invoke-RestMethod -Uri "$apiUrl/api/products/seed-standard" -Method Post -Headers $headers
    Write-Host "   OK: Added $($result.added), Total: $($result.total)" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

Write-Host "`nDone!"
