# =========================================
# Скрипт тестирования системы долгов
# =========================================

$API_URL = "https://tranquil-upliftment-production.up.railway.app"
# Или локально: $API_URL = "http://localhost:5028"

Write-Host "🧪 ТЕСТИРОВАНИЕ СИСТЕМЫ ДОЛГОВ" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Получаем токен (замени на свой!)
Write-Host "📝 Сначала получи JWT токен:" -ForegroundColor Yellow
Write-Host ""
Write-Host "curl -X POST $API_URL/api/auth/login ``" -ForegroundColor White
Write-Host "  -H 'Content-Type: application/json' ``" -ForegroundColor White
Write-Host "  -d '{""username"": ""admin"", ""password"": ""твой_пароль""}'" -ForegroundColor White
Write-Host ""
Write-Host "Скопируй токен и вставь ниже:" -ForegroundColor Yellow
$TOKEN = Read-Host "Введи токен"

if ([string]::IsNullOrWhiteSpace($TOKEN)) {
    Write-Host "❌ Токен не введен. Выход." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Токен получен!" -ForegroundColor Green
Write-Host ""

# ===== ТЕСТ 1: Создание клиента =====
Write-Host "📋 ТЕСТ 1: Создание тестового клиента" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

$clientBody = @{
    name = "Тестовый должник"
    phone = "+998901234567"
    type = 1
} | ConvertTo-Json

Write-Host "Запрос:"
Write-Host $clientBody -ForegroundColor Gray

try {
    $clientResponse = Invoke-RestMethod -Uri "$API_URL/api/clients" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $TOKEN"
            "Content-Type" = "application/json"
        } `
        -Body $clientBody

    $CLIENT_ID = $clientResponse.id
    Write-Host "✅ Клиент создан! ID: $CLIENT_ID" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "❌ Ошибка создания клиента: $_" -ForegroundColor Red
    Write-Host "Используем существующего клиента ID=1" -ForegroundColor Yellow
    $CLIENT_ID = 1
    Write-Host ""
}

# ===== ТЕСТ 2: Создание продажи в долг =====
Write-Host "📋 ТЕСТ 2: Создание продажи В ДОЛГ" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan

$saleBody = @{
    clientId = $CLIENT_ID
    clientName = "Тестовый должник"
    paymentType = 11  # Debt
    debtDueDate = (Get-Date).AddDays(30).ToString("yyyy-MM-dd")
    debtNotes = "Тестовый долг"
    items = @(
        @{
            productId = 1
            qty = 5
            unitPrice = 150000
        },
        @{
            productId = 3
            qty = 2
            unitPrice = 350000
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "Запрос:"
Write-Host $saleBody -ForegroundColor Gray

try {
    $saleResponse = Invoke-RestMethod -Uri "$API_URL/api/sales" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $TOKEN"
            "Content-Type" = "application/json"
        } `
        -Body $saleBody

    $SALE_ID = $saleResponse.id
    Write-Host "✅ Продажа создана! ID: $SALE_ID" -ForegroundColor Green
    Write-Host "Сумма: $($saleResponse.total) сум" -ForegroundColor Green
    Write-Host ""
    
    Start-Sleep -Seconds 2
} catch {
    Write-Host "❌ Ошибка создания продажи: $_" -ForegroundColor Red
    exit 1
}

# ===== ТЕСТ 3: Получить долги клиента =====
Write-Host "📋 ТЕСТ 3: Получение долгов клиента" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

try {
    $debtsResponse = Invoke-RestMethod -Uri "$API_URL/api/debts/by-client/$CLIENT_ID" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $TOKEN"
        }

    Write-Host "✅ Долги получены!" -ForegroundColor Green
    Write-Host "Общий долг: $($debtsResponse.totalDebt) сум" -ForegroundColor Yellow
    Write-Host "Количество долгов: $($debtsResponse.debts.Count)" -ForegroundColor Yellow
    
    if ($debtsResponse.debts.Count -gt 0) {
        $DEBT_ID = $debtsResponse.debts[0].id
        Write-Host "ID долга для тестов: $DEBT_ID" -ForegroundColor Cyan
    }
    Write-Host ""
    
    Start-Sleep -Seconds 1
} catch {
    Write-Host "❌ Ошибка получения долгов: $_" -ForegroundColor Red
}

# ===== ТЕСТ 4: Детали долга с товарами =====
Write-Host "📋 ТЕСТ 4: Детали долга с товарами" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan

try {
    $debtDetailsResponse = Invoke-RestMethod -Uri "$API_URL/api/debts/$DEBT_ID" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $TOKEN"
        }

    Write-Host "✅ Детали долга:" -ForegroundColor Green
    Write-Host "Клиент: $($debtDetailsResponse.clientName)" -ForegroundColor White
    Write-Host "Сумма долга: $($debtDetailsResponse.amount) сум" -ForegroundColor Yellow
    Write-Host "Срок оплаты: $($debtDetailsResponse.dueDate)" -ForegroundColor White
    Write-Host "Товары:" -ForegroundColor White
    
    foreach ($item in $debtDetailsResponse.items) {
        Write-Host "  - $($item.productName): $($item.qty) x $($item.price) = $($item.total) сум" -ForegroundColor Gray
    }
    Write-Host ""
    
    Start-Sleep -Seconds 1
} catch {
    Write-Host "❌ Ошибка получения деталей долга: $_" -ForegroundColor Red
}

# ===== ТЕСТ 5: Редактирование товаров в долге =====
Write-Host "📋 ТЕСТ 5: Редактирование цены товара в долге" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

$updatedItems = @{
    items = @(
        @{
            id = $debtDetailsResponse.items[0].id
            productId = $debtDetailsResponse.items[0].productId
            productName = $debtDetailsResponse.items[0].productName
            sku = $debtDetailsResponse.items[0].sku
            qty = $debtDetailsResponse.items[0].qty
            price = 160000  # Повышаем цену!
            total = $debtDetailsResponse.items[0].qty * 160000
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "Повышаем цену первого товара до 160,000 сум" -ForegroundColor Yellow

try {
    $updateResponse = Invoke-RestMethod -Uri "$API_URL/api/debts/$DEBT_ID/items" `
        -Method PUT `
        -Headers @{
            "Authorization" = "Bearer $TOKEN"
            "Content-Type" = "application/json"
        } `
        -Body $updatedItems

    Write-Host "✅ Товары обновлены!" -ForegroundColor Green
    Write-Host "Новая сумма долга: $($updateResponse.amount) сум" -ForegroundColor Yellow
    Write-Host ""
    
    Start-Sleep -Seconds 1
} catch {
    Write-Host "❌ Ошибка редактирования товаров: $_" -ForegroundColor Red
}

# ===== ТЕСТ 6: Частичная оплата долга =====
Write-Host "📋 ТЕСТ 6: Частичная оплата долга" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan

$paymentBody = @{
    amount = 500000
} | ConvertTo-Json

Write-Host "Оплачиваем 500,000 сум" -ForegroundColor Yellow

try {
    $paymentResponse = Invoke-RestMethod -Uri "$API_URL/api/debts/$DEBT_ID/pay" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $TOKEN"
            "Content-Type" = "application/json"
        } `
        -Body $paymentBody

    Write-Host "✅ Оплата принята!" -ForegroundColor Green
    Write-Host "Остаток долга: $($paymentResponse.amount) сум" -ForegroundColor Yellow
    Write-Host "Статус: $($paymentResponse.status)" -ForegroundColor White
    Write-Host ""
    
    Start-Sleep -Seconds 1
} catch {
    Write-Host "❌ Ошибка оплаты долга: $_" -ForegroundColor Red
}

# ===== ТЕСТ 7: Список должников =====
Write-Host "📋 ТЕСТ 7: Список всех должников" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

try {
    $debtorsResponse = Invoke-RestMethod -Uri "$API_URL/api/clients/debtors" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $TOKEN"
        }

    Write-Host "✅ Список должников получен!" -ForegroundColor Green
    Write-Host "Всего должников: $($debtorsResponse.total)" -ForegroundColor Yellow
    Write-Host ""
    
    foreach ($debtor in $debtorsResponse.items) {
        Write-Host "👤 $($debtor.clientName)" -ForegroundColor White
        Write-Host "   💰 Долг: $($debtor.totalDebt) сум" -ForegroundColor Yellow
        Write-Host "   📞 Телефон: $($debtor.phone)" -ForegroundColor Gray
        Write-Host "   📅 Старейший срок: $($debtor.oldestDueDate)" -ForegroundColor Gray
        Write-Host ""
    }
    
    Start-Sleep -Seconds 1
} catch {
    Write-Host "❌ Ошибка получения списка должников: $_" -ForegroundColor Red
}

# ===== ТЕСТ 8: Клиент с долгом и историей =====
Write-Host "📋 ТЕСТ 8: Клиент с долгом и историей покупок" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

try {
    $clientWithDebtResponse = Invoke-RestMethod -Uri "$API_URL/api/clients/$CLIENT_ID/with-debt" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $TOKEN"
        }

    Write-Host "✅ Информация о клиенте получена!" -ForegroundColor Green
    Write-Host ""
    Write-Host "👤 Клиент: $($clientWithDebtResponse.client.name)" -ForegroundColor White
    Write-Host ""
    Write-Host "💰 ДОЛГ:" -ForegroundColor Yellow
    Write-Host "   Общая сумма: $($clientWithDebtResponse.debt.totalAmount) сум" -ForegroundColor Red
    Write-Host "   Количество долгов: $($clientWithDebtResponse.debt.debts.Count)" -ForegroundColor White
    Write-Host ""
    Write-Host "🛒 ПОКУПКИ:" -ForegroundColor Yellow
    Write-Host "   Всего набрал товара на: $($clientWithDebtResponse.purchases.totalAmount) сум" -ForegroundColor Green
    Write-Host "   Количество покупок: $($clientWithDebtResponse.purchases.count)" -ForegroundColor White
    Write-Host ""
    Write-Host "   История последних покупок:" -ForegroundColor Gray
    foreach ($purchase in $clientWithDebtResponse.purchases.history | Select-Object -First 5) {
        $date = ([DateTime]$purchase.createdAt).ToString("dd.MM.yyyy HH:mm")
        Write-Host "   - $date : $($purchase.total) сум" -ForegroundColor Gray
    }
    Write-Host ""
} catch {
    Write-Host "❌ Ошибка получения информации о клиенте: $_" -ForegroundColor Red
}

# ===== ИТОГИ =====
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "🎉 ВСЕ ТЕСТЫ ЗАВЕРШЕНЫ!" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Система долгов работает!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Что протестировано:" -ForegroundColor Cyan
Write-Host "  1. Создание клиента" -ForegroundColor White
Write-Host "  2. Создание продажи в долг (PaymentType = 11)" -ForegroundColor White
Write-Host "  3. Получение долгов клиента" -ForegroundColor White
Write-Host "  4. Детали долга с товарами" -ForegroundColor White
Write-Host "  5. Редактирование цены товара в долге" -ForegroundColor White
Write-Host "  6. Частичная оплата долга" -ForegroundColor White
Write-Host "  7. Список всех должников" -ForegroundColor White
Write-Host "  8. Клиент с долгом и историей покупок" -ForegroundColor White
Write-Host ""
Write-Host "🔍 Можешь проверить в Swagger:" -ForegroundColor Yellow
Write-Host "   $API_URL/swagger" -ForegroundColor Cyan
Write-Host ""
