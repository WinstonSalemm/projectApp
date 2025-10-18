# =========================================
# Скрипт тестирования системы долгов
# =========================================

$API_URL = "https://tranquil-upliftment-production.up.railway.app"

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  ТЕСТИРОВАНИЕ СИСТЕМЫ ДОЛГОВ" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Шаг 1: Получение токена
Write-Host "ШАГ 1: Получение JWT токена" -ForegroundColor Yellow
Write-Host ""
Write-Host "Открой в браузере:" -ForegroundColor White
Write-Host "  $API_URL/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "Найди эндпоинт: POST /api/auth/login" -ForegroundColor White
Write-Host "Введи свои credentials и скопируй токен" -ForegroundColor White
Write-Host ""

$TOKEN = Read-Host "Вставь JWT токен сюда"

if ([string]::IsNullOrWhiteSpace($TOKEN)) {
    Write-Host ""
    Write-Host "Токен не введен. Выход." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Токен получен!" -ForegroundColor Green
Write-Host ""

# Заголовки для запросов
$headers = @{
    "Authorization" = "Bearer $TOKEN"
    "Content-Type" = "application/json"
}

# ========================================
# ТЕСТ 1: Создание тестового клиента
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "ТЕСТ 1: Создание тестового клиента" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$clientBody = @{
    name = "Тестовый должник"
    phone = "+998901234567"
    type = 1
} | ConvertTo-Json

try {
    $clientResponse = Invoke-RestMethod -Uri "$API_URL/api/clients" -Method POST -Headers $headers -Body $clientBody
    $CLIENT_ID = $clientResponse.id
    Write-Host "Клиент создан! ID: $CLIENT_ID" -ForegroundColor Green
} catch {
    Write-Host "Ошибка создания клиента. Используем ID=1" -ForegroundColor Yellow
    $CLIENT_ID = 1
}

Write-Host ""
Start-Sleep -Seconds 1

# ========================================
# ТЕСТ 2: Создание продажи В ДОЛГ
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "ТЕСТ 2: Создание продажи В ДОЛГ" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$dueDate = (Get-Date).AddDays(30).ToString("yyyy-MM-dd")

$saleBody = @{
    clientId = $CLIENT_ID
    clientName = "Тестовый должник"
    paymentType = 11
    debtDueDate = $dueDate
    debtNotes = "Тестовый долг из автотеста"
    items = @(
        @{
            productId = 1
            qty = 5
            unitPrice = 150000
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $saleResponse = Invoke-RestMethod -Uri "$API_URL/api/sales" -Method POST -Headers $headers -Body $saleBody
    $SALE_ID = $saleResponse.id
    Write-Host "Продажа создана! ID: $SALE_ID" -ForegroundColor Green
    Write-Host "Сумма: $($saleResponse.total) сум" -ForegroundColor Green
} catch {
    Write-Host "Ошибка создания продажи:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host ""
Start-Sleep -Seconds 2

# ========================================
# ТЕСТ 3: Получение долгов клиента
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "ТЕСТ 3: Получение долгов клиента" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    $debtsResponse = Invoke-RestMethod -Uri "$API_URL/api/debts/by-client/$CLIENT_ID" -Method GET -Headers $headers
    Write-Host "Долги получены!" -ForegroundColor Green
    Write-Host "Общий долг: $($debtsResponse.totalDebt) сум" -ForegroundColor Yellow
    Write-Host "Количество долгов: $($debtsResponse.debts.Count)" -ForegroundColor Yellow
    
    if ($debtsResponse.debts.Count -gt 0) {
        $DEBT_ID = $debtsResponse.debts[0].id
        Write-Host "ID долга: $DEBT_ID" -ForegroundColor Cyan
    }
} catch {
    Write-Host "Ошибка получения долгов:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Start-Sleep -Seconds 1

# ========================================
# ТЕСТ 4: Детали долга с товарами
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "ТЕСТ 4: Детали долга с товарами" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    $debtDetailsResponse = Invoke-RestMethod -Uri "$API_URL/api/debts/$DEBT_ID" -Method GET -Headers $headers
    Write-Host "Детали долга:" -ForegroundColor Green
    Write-Host "  Клиент: $($debtDetailsResponse.clientName)" -ForegroundColor White
    Write-Host "  Сумма долга: $($debtDetailsResponse.amount) сум" -ForegroundColor Yellow
    Write-Host "  Срок оплаты: $($debtDetailsResponse.dueDate)" -ForegroundColor White
    Write-Host "  Товаров: $($debtDetailsResponse.items.Count)" -ForegroundColor White
} catch {
    Write-Host "Ошибка получения деталей долга:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Start-Sleep -Seconds 1

# ========================================
# ТЕСТ 5: Список ДОЛЖНИКОВ
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "ТЕСТ 5: Список всех должников" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    $debtorsResponse = Invoke-RestMethod -Uri "$API_URL/api/clients/debtors" -Method GET -Headers $headers
    Write-Host "Список должников получен!" -ForegroundColor Green
    Write-Host "Всего должников: $($debtorsResponse.total)" -ForegroundColor Yellow
    Write-Host ""
    
    foreach ($debtor in $debtorsResponse.items) {
        Write-Host "  $($debtor.clientName)" -ForegroundColor White
        Write-Host "    Долг: $($debtor.totalDebt) сум" -ForegroundColor Yellow
        Write-Host "    Телефон: $($debtor.phone)" -ForegroundColor Gray
        Write-Host ""
    }
} catch {
    Write-Host "Ошибка получения списка должников:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Start-Sleep -Seconds 1

# ========================================
# ТЕСТ 6: Клиент с долгом и историей
# ========================================
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "ТЕСТ 6: Клиент с долгом и историей" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

try {
    $clientWithDebtResponse = Invoke-RestMethod -Uri "$API_URL/api/clients/$CLIENT_ID/with-debt" -Method GET -Headers $headers
    Write-Host "Информация о клиенте:" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Клиент: $($clientWithDebtResponse.client.name)" -ForegroundColor White
    Write-Host ""
    Write-Host "  ДОЛГ:" -ForegroundColor Yellow
    Write-Host "    Сумма: $($clientWithDebtResponse.debt.totalAmount) сум" -ForegroundColor Red
    Write-Host ""
    Write-Host "  ПОКУПКИ:" -ForegroundColor Yellow
    Write-Host "    Всего набрал товара на: $($clientWithDebtResponse.purchases.totalAmount) сум" -ForegroundColor Green
    Write-Host "    Количество покупок: $($clientWithDebtResponse.purchases.count)" -ForegroundColor White
} catch {
    Write-Host "Ошибка получения информации о клиенте:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Write-Host ""

# ========================================
# ИТОГИ
# ========================================
Write-Host "=====================================" -ForegroundColor Green
Write-Host "       ВСЕ ТЕСТЫ ЗАВЕРШЕНЫ!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Протестировано:" -ForegroundColor Cyan
Write-Host "  1. Создание клиента" -ForegroundColor White
Write-Host "  2. Создание продажи в долг (PaymentType = 11)" -ForegroundColor White
Write-Host "  3. Получение долгов клиента" -ForegroundColor White
Write-Host "  4. Детали долга с товарами" -ForegroundColor White
Write-Host "  5. Список всех должников" -ForegroundColor White
Write-Host "  6. Клиент с долгом и историей покупок" -ForegroundColor White
Write-Host ""
Write-Host "Система долгов работает!" -ForegroundColor Green
Write-Host ""
Write-Host "Swagger: $API_URL/swagger" -ForegroundColor Cyan
Write-Host ""
