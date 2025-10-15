# Railway Debugging Guide

## Что сделано

Добавлено детальное логирование в `ProductsController`:
- Логируются все шаги выполнения
- Логируются все ошибки с полным стектрейсом
- Теперь в Railway logs будет видно где именно падает

## Как проверить

### 1. Дождитесь деплоя (5-10 минут)

Зайдите на Railway → Deployments и дождитесь статуса **ACTIVE**.

### 2. Проверьте API

```powershell
cd c:\projectApp
powershell -ExecutionPolicy Bypass -File test-api-with-password.ps1
```

### 3. Посмотрите логи

1. Зайдите на Railway → Logs
2. Найдите строки с `[ProductsController]`
3. Если есть ошибка - будет `LogError` с полным стектрейсом

### 4. Что искать в логах

**Успешный запрос выглядит так:**
```
[ProductsController] GetCategories started
[ProductsController] Repository categories: 4
[ProductsController] Directory categories: 0
[ProductsController] Returning 4 categories
```

**Ошибка выглядит так:**
```
[ProductsController] GetCategories started
[ProductsController] GetCategories failed: <текст ошибки>
System.Exception: <детали>
   at <стектрейс>
```

## Возможные проблемы и решения

### Проблема 1: Кодировка UTF-8

**Симптом:** Ошибка при чтении данных с кириллицей

**Решение:** Проверить что MySQL на Railway использует `utf8mb4`:
```sql
SHOW VARIABLES LIKE 'character_set%';
```

### Проблема 2: Таблица Categories не существует

**Симптом:** `Table 'railway.Categories' doesn't exist`

**Решение:** Миграции не применились. Нужно:
1. Зайти на Railway → Settings → Variables
2. Проверить `ConnectionStrings__DefaultConnection`
3. Вручную применить миграции или пересоздать БД

### Проблема 3: Нет данных

**Симптом:** Запросы работают, но возвращают пустые массивы

**Решение:** Запустить seed:
```powershell
curl -X POST https://tranquil-upliftment-production.up.railway.app/api/products/seed-standard -H "Authorization: Bearer <token>"
```

## После исправления

Когда Railway заработает:

1. Измените `appsettings.json` в клиенте обратно на Railway URL
2. Пересоберите приложение
3. Запустите и проверьте что данные загружаются
4. Telegram отчеты должны работать

## Если ничего не помогает

Последний вариант - **пересоздать БД на Railway**:

1. Railway → MySQL service → Settings → Delete
2. Создать новый MySQL service
3. Подключить к API
4. Применить миграции
5. Запустить seed

Но это крайняя мера - сначала посмотрим логи!
