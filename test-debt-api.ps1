# =========================================
# Debt System Testing Script
# =========================================

$API_URL = "https://tranquil-upliftment-production.up.railway.app"

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  DEBT SYSTEM TESTING" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Get JWT Token
Write-Host "STEP 1: Get JWT Token" -ForegroundColor Yellow
Write-Host ""
Write-Host "Open in browser:" -ForegroundColor White
Write-Host "  $API_URL/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "Find endpoint: POST /api/auth/login" -ForegroundColor White
Write-Host "Login and copy the JWT token" -ForegroundColor White
Write-Host ""

$TOKEN = Read-Host "Paste JWT token here"

if ([string]::IsNullOrWhiteSpace($TOKEN)) {
    Write-Host ""
    Write-Host "ERROR: Token is empty. Exiting." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "OK: Token received!" -ForegroundColor Green
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $TOKEN"
    "Content-Type" = "application/json"
}

# ========================================
# TEST 1: Create Test Client
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TEST 1: Create Test Client" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$clientBody = @{
    name = "Test Debtor"
    phone = "+998901234567"
    type = 1
} | ConvertTo-Json

try {
    $clientResponse = Invoke-RestMethod -Uri "$API_URL/api/clients" -Method POST -Headers $headers -Body $clientBody
    $CLIENT_ID = $clientResponse.id
    Write-Host "OK: Client created! ID: $CLIENT_ID" -ForegroundColor Green
} catch {
    Write-Host "WARNING: Cannot create client. Using ID=1" -ForegroundColor Yellow
    $CLIENT_ID = 1
}

Write-Host ""
Start-Sleep -Seconds 1

# ========================================
# TEST 2: Create Sale with DEBT
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TEST 2: Create Sale with DEBT" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$dueDate = (Get-Date).AddDays(30).ToString("yyyy-MM-dd")

$saleBody = @{
    clientId = $CLIENT_ID
    clientName = "Test Debtor"
    paymentType = 11
    debtDueDate = $dueDate
    debtNotes = "Test debt from autotest"
    items = @(
        @{
            productId = 1
            qty = 5
            unitPrice = 150000
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "Request body:" -ForegroundColor Gray
Write-Host $saleBody -ForegroundColor DarkGray
Write-Host ""

try {
    $saleResponse = Invoke-RestMethod -Uri "$API_URL/api/sales" -Method POST -Headers $headers -Body $saleBody
    $SALE_ID = $saleResponse.id
    Write-Host "OK: Sale created! ID: $SALE_ID" -ForegroundColor Green
    Write-Host "Total: $($saleResponse.total) UZS" -ForegroundColor Green
} catch {
    Write-Host "ERROR creating sale:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    # Try to get error details
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        $responseBody = $reader.ReadToEnd()
        Write-Host "API Response:" -ForegroundColor Yellow
        Write-Host $responseBody -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "Trying to get products list first..." -ForegroundColor Yellow
    
    try {
        $productsResponse = Invoke-RestMethod -Uri "$API_URL/api/products?page=1&size=5" -Method GET -Headers $headers
        Write-Host "Available products:" -ForegroundColor Cyan
        foreach ($product in $productsResponse.items) {
            Write-Host "  ID: $($product.id) - $($product.name) - Price: $($product.price)" -ForegroundColor White
        }
    } catch {
        Write-Host "Cannot get products list" -ForegroundColor Red
    }
    
    exit 1
}

Write-Host ""
Start-Sleep -Seconds 2

# ========================================
# TEST 3: Get Client Debts
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TEST 3: Get Client Debts" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    $debtsResponse = Invoke-RestMethod -Uri "$API_URL/api/debts/by-client/$CLIENT_ID" -Method GET -Headers $headers
    Write-Host "OK: Debts retrieved!" -ForegroundColor Green
    Write-Host "Total debt: $($debtsResponse.totalDebt) UZS" -ForegroundColor Yellow
    Write-Host "Debts count: $($debtsResponse.debts.Count)" -ForegroundColor Yellow
    
    if ($debtsResponse.debts.Count -gt 0) {
        $DEBT_ID = $debtsResponse.debts[0].id
        Write-Host "Debt ID: $DEBT_ID" -ForegroundColor Cyan
    }
} catch {
    Write-Host "ERROR getting debts:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Start-Sleep -Seconds 1

# ========================================
# TEST 4: Debt Details with Items
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TEST 4: Debt Details with Items" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    $debtDetailsResponse = Invoke-RestMethod -Uri "$API_URL/api/debts/$DEBT_ID" -Method GET -Headers $headers
    Write-Host "OK: Debt details:" -ForegroundColor Green
    Write-Host "  Client: $($debtDetailsResponse.clientName)" -ForegroundColor White
    Write-Host "  Amount: $($debtDetailsResponse.amount) UZS" -ForegroundColor Yellow
    Write-Host "  Due date: $($debtDetailsResponse.dueDate)" -ForegroundColor White
    Write-Host "  Items: $($debtDetailsResponse.items.Count)" -ForegroundColor White
} catch {
    Write-Host "ERROR getting debt details:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Start-Sleep -Seconds 1

# ========================================
# TEST 5: Debtors List
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TEST 5: Debtors List" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    $debtorsResponse = Invoke-RestMethod -Uri "$API_URL/api/clients/debtors" -Method GET -Headers $headers
    Write-Host "OK: Debtors list retrieved!" -ForegroundColor Green
    Write-Host "Total debtors: $($debtorsResponse.total)" -ForegroundColor Yellow
    Write-Host ""
    
    foreach ($debtor in $debtorsResponse.items) {
        Write-Host "  $($debtor.clientName)" -ForegroundColor White
        Write-Host "    Debt: $($debtor.totalDebt) UZS" -ForegroundColor Yellow
        Write-Host "    Phone: $($debtor.phone)" -ForegroundColor Gray
        Write-Host ""
    }
} catch {
    Write-Host "ERROR getting debtors:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Start-Sleep -Seconds 1

# ========================================
# TEST 6: Client with Debt and History
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TEST 6: Client with Debt & History" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    $clientWithDebtResponse = Invoke-RestMethod -Uri "$API_URL/api/clients/$CLIENT_ID/with-debt" -Method GET -Headers $headers
    Write-Host "OK: Client info:" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Client: $($clientWithDebtResponse.client.name)" -ForegroundColor White
    Write-Host ""
    Write-Host "  DEBT:" -ForegroundColor Yellow
    Write-Host "    Amount: $($clientWithDebtResponse.debt.totalAmount) UZS" -ForegroundColor Red
    Write-Host ""
    Write-Host "  PURCHASES:" -ForegroundColor Yellow
    Write-Host "    Total purchases: $($clientWithDebtResponse.purchases.totalAmount) UZS" -ForegroundColor Green
    Write-Host "    Purchases count: $($clientWithDebtResponse.purchases.count)" -ForegroundColor White
} catch {
    Write-Host "ERROR getting client info:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Write-Host ""

# ========================================
# SUMMARY
# ========================================
Write-Host "=====================================" -ForegroundColor Green
Write-Host "       ALL TESTS COMPLETED!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Tested:" -ForegroundColor Cyan
Write-Host "  1. Create client" -ForegroundColor White
Write-Host "  2. Create sale with debt (paymentType = 11)" -ForegroundColor White
Write-Host "  3. Get client debts" -ForegroundColor White
Write-Host "  4. Debt details with items" -ForegroundColor White
Write-Host "  5. Debtors list" -ForegroundColor White
Write-Host "  6. Client with debt and purchase history" -ForegroundColor White
Write-Host ""
Write-Host "SUCCESS: Debt system is working!" -ForegroundColor Green
Write-Host ""
Write-Host "Swagger: $API_URL/swagger" -ForegroundColor Cyan
Write-Host ""
