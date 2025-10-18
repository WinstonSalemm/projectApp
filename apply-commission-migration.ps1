# =========================================
# Скрипт применения миграции партнерской программы
# =========================================

Write-Host "🚀 Применение миграции партнерской программы..." -ForegroundColor Cyan

# Проверяем наличие Railway CLI
$railwayInstalled = Get-Command railway -ErrorAction SilentlyContinue

if ($railwayInstalled) {
    Write-Host "✅ Railway CLI найден" -ForegroundColor Green
    Write-Host "📝 Применяем миграцию через Railway..." -ForegroundColor Yellow
    
    # Применяем миграцию
    Get-Content "add-commission-sales.sql" | railway run mysql -u root -p
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Миграция успешно применена!" -ForegroundColor Green
    } else {
        Write-Host "❌ Ошибка применения миграции" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "⚠️ Railway CLI не установлен" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "📋 Для применения миграции вручную:" -ForegroundColor Cyan
    Write-Host "1. Установи Railway CLI: npm install -g @railway/cli" -ForegroundColor White
    Write-Host "2. Залогинься: railway login" -ForegroundColor White
    Write-Host "3. Привяжи проект: railway link" -ForegroundColor White
    Write-Host "4. Запусти этот скрипт снова" -ForegroundColor White
    Write-Host ""
    Write-Host "Или примени миграцию вручную через phpMyAdmin/MySQL Workbench" -ForegroundColor White
    Write-Host "Файл миграции: add-commission-sales.sql" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✨ Готово!" -ForegroundColor Green
