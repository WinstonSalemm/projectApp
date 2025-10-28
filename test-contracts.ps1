#!/usr/bin/env pwsh
# Test Contracts API after migration

$baseUrl = "https://tranquil-upliftment-production.up.railway.app"
# $baseUrl = "http://localhost:5000"  # Uncomment for local testing

Write-Host "🧪 Testing Contracts API..." -ForegroundColor Cyan
Write-Host "Base URL: $baseUrl" -ForegroundColor Gray
Write-Host ""

# 1. Test: Get all contracts
Write-Host "1️⃣ Testing GET /api/contracts" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/contracts" -Method Get
    Write-Host "   ✅ Success! Found $($response.Count) contracts" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 2. Test: Create a new CLOSED contract
Write-Host "2️⃣ Testing POST /api/contracts (Closed Contract)" -ForegroundColor Yellow

$closedContract = @{
    Type = 0  # Closed
    ContractNumber = "DOG-001-TEST"
    ClientId = 1
    OrgName = "Test Company LLC"
    Inn = "1234567890"
    Description = "Товар: iPhone 15 Pro - придёт через неделю. Пока нет в НД."
    Note = "Тестовый закрытый договор"
    Items = @(
        @{
            ProductId = $null  # Товара нет в каталоге
            Name = "iPhone 15 Pro 256GB"
            Description = "Ожидается поставка 05.11.2025"
            Unit = "шт"
            Qty = 5
            UnitPrice = 15000000
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/contracts" -Method Post -Body $closedContract -ContentType "application/json"
    Write-Host "   ✅ Created contract ID: $($response.id)" -ForegroundColor Green
    $closedId = $response.id
} catch {
    Write-Host "   ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 3. Test: Create a new OPEN contract
Write-Host "3️⃣ Testing POST /api/contracts (Open Contract)" -ForegroundColor Yellow

$openContract = @{
    Type = 1  # Open
    ContractNumber = "DOG-002-TEST"
    ClientId = 2
    OrgName = "Another Company Ltd"
    TotalAmount = 100000000  # Лимит договора
    Note = "Тестовый открытый договор на 100 млн"
    Items = @()  # Пустой - будем добавлять из каталога
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/contracts" -Method Post -Body $openContract -ContentType "application/json"
    Write-Host "   ✅ Created contract ID: $($response.id)" -ForegroundColor Green
    $openId = $response.id
} catch {
    Write-Host "   ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 4. Test: Add item to open contract
if ($openId) {
    Write-Host "4️⃣ Testing POST /api/contracts/$openId/items (Add from catalog)" -ForegroundColor Yellow
    
    $newItem = @{
        ProductId = 1  # OP-1 from catalog
        Qty = 10
        UnitPrice = 150000
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/contracts/$openId/items" -Method Post -Body $newItem -ContentType "application/json"
        Write-Host "   ✅ Added item ID: $($response.id)" -ForegroundColor Green
    } catch {
        Write-Host "   ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""

# 5. Test: Get contract details
if ($closedId) {
    Write-Host "5️⃣ Testing GET /api/contracts/$closedId" -ForegroundColor Yellow
    try {
        $contract = Invoke-RestMethod -Uri "$baseUrl/api/contracts/$closedId" -Method Get
        Write-Host "   ✅ Success!" -ForegroundColor Green
        Write-Host "   Contract: $($contract.ContractNumber)" -ForegroundColor Gray
        Write-Host "   Type: $($contract.Type)" -ForegroundColor Gray
        Write-Host "   Items: $($contract.Items.Count)" -ForegroundColor Gray
        Write-Host "   Description: $($contract.Description)" -ForegroundColor Gray
    } catch {
        Write-Host "   ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "✅ Testing complete!" -ForegroundColor Green
Write-Host ""
Write-Host "🔍 Next steps:" -ForegroundColor Cyan
Write-Host "   1. Check Railway Dashboard to verify data" -ForegroundColor Gray
Write-Host "   2. Test reservation logic" -ForegroundColor Gray
Write-Host "   3. Implement MAUI UI" -ForegroundColor Gray
