# =========================================
# Скрипт применения миграции системы долгов
# =========================================

Write-Host "🚀 Применение миграции системы долгов..." -ForegroundColor Cyan

# Проверяем наличие Railway CLI
$railwayInstalled = Get-Command railway -ErrorAction SilentlyContinue

if ($railwayInstalled) {
    Write-Host "✅ Railway CLI найден" -ForegroundColor Green
    Write-Host "📝 Применяем миграцию через Railway..." -ForegroundColor Yellow
    
    # Применяем миграцию
    Get-Content "add-debt-system-enhanced.sql" | railway run mysql -u root -p
    
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
    Write-Host "1. Открой Railway Dashboard" -ForegroundColor White
    Write-Host "2. Перейди: Твой проект → MySQL → Query" -ForegroundColor White
    Write-Host "3. Скопируй содержимое файла add-debt-system-enhanced.sql" -ForegroundColor White
    Write-Host "4. Вставь и выполни" -ForegroundColor White
}

Write-Host ""
Write-Host "✨ Готово!" -ForegroundColor Green
