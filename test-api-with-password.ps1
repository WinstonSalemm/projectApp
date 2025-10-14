# API Test with Password
$apiUrl = "https://tranquil-upliftment-production.up.railway.app"

Write-Host "Testing API: $apiUrl"

# Login with password
Write-Host "`nLogin as admin with password..."
$body = @{
    userName = "admin"
    password = "140606tl"
} | ConvertTo-Json

try {
    $auth = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    Write-Host "SUCCESS: Logged in as $($auth.role)"
    $token = $auth.accessToken
} catch {
    Write-Host "ERROR: Login failed - $_"
    exit 1
}

# Get products
Write-Host "`nGet products..."
try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    $products = Invoke-RestMethod -Uri "$apiUrl/api/products?page=1&size=10" -Method Get -Headers $headers
    Write-Host "SUCCESS: Got $($products.Items.Count) products out of $($products.Total)"
    if ($products.Total -eq 0) {
        Write-Host "WARNING: No products in database!"
    } else {
        $products.Items | Select-Object -First 5 | ForEach-Object { 
            Write-Host "  - [$($_.Sku)] $($_.Name) - $($_.UnitPrice) (Category: $($_.Category))"
        }
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}

# Get categories
Write-Host "`nGet categories..."
try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    $categories = Invoke-RestMethod -Uri "$apiUrl/api/products/categories" -Method Get -Headers $headers
    Write-Host "SUCCESS: Got $($categories.Count) categories"
    if ($categories.Count -eq 0) {
        Write-Host "WARNING: No categories in database!"
    } else {
        $categories | ForEach-Object { Write-Host "  - $_" }
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}

# Get stocks
Write-Host "`nGet stocks..."
try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    $stocks = Invoke-RestMethod -Uri "$apiUrl/api/stocks" -Method Get -Headers $headers
    Write-Host "SUCCESS: Got $($stocks.Count) stocks"
    if ($stocks.Count -eq 0) {
        Write-Host "WARNING: No stocks in database!"
    } else {
        $stocks | Select-Object -First 5 | ForEach-Object { 
            Write-Host "  - [$($_.Sku)] $($_.Name): ND40=$($_.Nd40Qty), IM40=$($_.Im40Qty)"
        }
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}

# Seed standard products
Write-Host "`nSeed standard products..."
try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    $result = Invoke-RestMethod -Uri "$apiUrl/api/products/seed-standard" -Method Post -Headers $headers
    Write-Host "SUCCESS: Added $($result.added) products (total: $($result.total))"
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}

Write-Host "`nTest completed!"
