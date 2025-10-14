# Скрипт для тестирования API
$apiUrl = "https://tranquil-upliftment-production.up.railway.app"

Write-Host "=== Тест API ===" -ForegroundColor Green
Write-Host "URL: $apiUrl" -ForegroundColor Cyan

# 1. Проверка здоровья API
Write-Host "`n1. Проверка /health..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$apiUrl/health" -Method Get
    Write-Host "✓ API работает" -ForegroundColor Green
} catch {
    Write-Host "✗ API недоступен: $_" -ForegroundColor Red
    exit 1
}

# 2. Попытка получить категории без авторизации
Write-Host "`n2. Проверка /api/products/categories (без авторизации)..." -ForegroundColor Yellow
try {
    $categories = Invoke-RestMethod -Uri "$apiUrl/api/products/categories" -Method Get
    Write-Host "✓ Получено категорий: $($categories.Count)" -ForegroundColor Green
    $categories | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
} catch {
    Write-Host "✗ Ошибка: $($_.Exception.Response.StatusCode) - $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Попытка получить товары без авторизации
Write-Host "`n3. Проверка /api/products (без авторизации)..." -ForegroundColor Yellow
try {
    $products = Invoke-RestMethod -Uri "$apiUrl/api/products?page=1&size=10" -Method Get
    Write-Host "✓ Получено товаров: $($products.Items.Count) из $($products.Total)" -ForegroundColor Green
    $products.Items | Select-Object -First 5 | ForEach-Object { 
        Write-Host "  - [$($_.Sku)] $($_.Name) - $($_.UnitPrice) сум" -ForegroundColor Gray 
    }
} catch {
    Write-Host "✗ Ошибка: $($_.Exception.Response.StatusCode) - $($_.Exception.Message)" -ForegroundColor Red
}

# 4. Авторизация
Write-Host "`n4. Авторизация..." -ForegroundColor Yellow
$username = Read-Host "Введите имя пользователя (или Enter для 'admin')"
if ([string]::IsNullOrWhiteSpace($username)) { $username = "admin" }

$body = @{
    userName = $username
    password = $null
} | ConvertTo-Json

try {
    $auth = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    Write-Host "✓ Авторизация успешна" -ForegroundColor Green
    Write-Host "  Роль: $($auth.role)" -ForegroundColor Gray
    Write-Host "  Токен: $($auth.accessToken.Substring(0, 20))..." -ForegroundColor Gray
    $token = $auth.accessToken
} catch {
    Write-Host "✗ Ошибка авторизации: $_" -ForegroundColor Red
    exit 1
}

# 5. Получение товаров с авторизацией
Write-Host "`n5. Проверка /api/products (с авторизацией)..." -ForegroundColor Yellow
try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    $products = Invoke-RestMethod -Uri "$apiUrl/api/products?page=1&size=10" -Method Get -Headers $headers
    Write-Host "✓ Получено товаров: $($products.Items.Count) из $($products.Total)" -ForegroundColor Green
    $products.Items | Select-Object -First 5 | ForEach-Object { 
        Write-Host "  - [$($_.Sku)] $($_.Name) - $($_.UnitPrice) сум (Категория: $($_.Category))" -ForegroundColor Gray 
    }
} catch {
    Write-Host "✗ Ошибка: $_" -ForegroundColor Red
}

# 6. Получение остатков с авторизацией
Write-Host "`n6. Проверка /api/stocks (с авторизацией)..." -ForegroundColor Yellow
try {
    $headers = @{
        Authorization = "Bearer $token"
    }
    $stocks = Invoke-RestMethod -Uri "$apiUrl/api/stocks" -Method Get -Headers $headers
    Write-Host "✓ Получено остатков: $($stocks.Count)" -ForegroundColor Green
    $stocks | Select-Object -First 5 | ForEach-Object { 
        Write-Host "  - [$($_.Sku)] $($_.Name): ND40=$($_.Nd40Qty), IM40=$($_.Im40Qty), Total=$($_.TotalQty)" -ForegroundColor Gray 
    }
} catch {
    Write-Host "✗ Ошибка: $_" -ForegroundColor Red
}

Write-Host "`n=== Тест завершен ===" -ForegroundColor Green
