# Railway Setup Instructions

## Проблема
API возвращает ошибки 500 при попытке получить данные из БД.

## Причина
Скорее всего, не настроена переменная окружения `ConnectionStrings__DefaultConnection` на Railway.

## Решение

### 1. Проверьте переменные окружения на Railway

Зайдите в проект на Railway и проверьте, что установлены следующие переменные:

```
ConnectionStrings__DefaultConnection=Server=<MYSQL_HOST>;Port=<MYSQL_PORT>;Database=<MYSQL_DATABASE>;User=<MYSQL_USER>;Password=<MYSQL_PASSWORD>;
```

### 2. Формат строки подключения MySQL

Для MySQL (Pomelo) используйте формат:
```
Server=mysql.railway.internal;Port=3306;Database=railway;User=root;Password=YOUR_PASSWORD;
```

### 3. Альтернатива: используйте переменную DATABASE_URL

Если Railway предоставляет `DATABASE_URL`, добавьте код в Program.cs для её парсинга:

```csharp
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    // Parse DATABASE_URL format: mysql://user:password@host:port/database
    var uri = new Uri(databaseUrl);
    var connectionString = $"Server={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};User={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};";
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
}
```

### 4. Проверка логов Railway

Зайдите в Deployments → Logs и найдите ошибки:
- Ошибки подключения к БД
- Ошибки миграций
- Ошибки авторизации

### 5. Проверка БД

Убедитесь, что:
- MySQL сервис запущен на Railway
- База данных создана
- Пользователь имеет права доступа
- Таблицы созданы (миграции применены)

### 6. Тестирование локально

Для локального тестирования с Railway MySQL:

1. Получите строку подключения из Railway
2. Добавьте в `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_RAILWAY_MYSQL_HOST;Port=3306;Database=railway;User=root;Password=YOUR_PASSWORD;"
  }
}
```
3. Запустите API локально

### 7. Проверка миграций

После настройки БД, убедитесь что миграции применены:
```bash
dotnet ef database update --project src/ProjectApp.Api
```

Или API должен автоматически применить миграции при старте (см. Program.cs, строка ~350).

## Дополнительная диагностика

### Проверка через Railway CLI:
```bash
railway logs
railway variables
```

### Проверка через API:
```bash
curl https://tranquil-upliftment-production.up.railway.app/health
curl https://tranquil-upliftment-production.up.railway.app/ready
```

Endpoint `/ready` проверяет подключение к БД.
