# 🚀 Деплой ProjectApp на Railway

## Быстрый старт

### 1. Создай проект на Railway
1. Перейди на https://railway.app
2. Нажми **"Start a New Project"**
3. Выбери **"Deploy from GitHub repo"**
4. Выбери репозиторий `WinstonSalemm/projectApp`

### 2. Добавь MySQL базу данных
1. В проекте нажми **"+ New"**
2. Выбери **"Database" → "Add MySQL"**
3. Railway создаст базу и предоставит credentials

### 3. Настрой переменные окружения

В проекте API перейди в **Settings → Variables** и добавь:

```bash
# Database Connection
PROJECTAPP__ConnectionStrings__DefaultConnection=Server=${{MySQL.MYSQLHOST}};Port=${{MySQL.MYSQLPORT}};Database=${{MySQL.MYSQLDATABASE}};User=${{MySQL.MYSQLUSER}};Password=${{MySQL.MYSQLPASSWORD}};

# CORS (добавь домен твоего фронтенда)
PROJECTAPP__Cors__Origins__0=https://твой-фронтенд.railway.app

# API Key (сгенерируй случайный ключ)
PROJECTAPP__Auth__ApiKey=твой-секретный-api-ключ-12345

# Environment
ASPNETCORE_ENVIRONMENT=Production

# Telegram (опционально)
PROJECTAPP__Telegram__BotToken=твой-токен-бота
PROJECTAPP__Telegram__DefaultChatId=твой-chat-id
```

### 4. Применение миграций

После деплоя подключись к базе и выполни миграции:

```bash
# Через Railway CLI
railway connect MySQL

# Или через MySQL Workbench/DBeaver используя credentials из Railway
```

Выполни миграции в следующем порядке:

1. `add-reservation-batches.sql`
2. `add-manager-bonuses.sql`
3. `add-client-classification.sql`
4. `add-commercial-analytics.sql`

### 5. Проверь деплой

Railway автоматически предоставит публичный URL:

```
https://projectapp-production-xxxx.up.railway.app
```

Проверь:
- Swagger UI: `https://твой-домен.railway.app/swagger`
- Health check: `https://твой-домен.railway.app/health`

## 📊 Новые API эндпоинты

### Коммерческая аналитика:
```bash
GET /api/commercial-analytics/abc
GET /api/commercial-analytics/forecast
GET /api/commercial-analytics/forecast/critical
POST /api/commercial-analytics/promotions/auto-generate
POST /api/commercial-analytics/discounts/validate
```

### Бонусы менеджеров:
```bash
POST /api/manager-bonuses/calculate?year=2025&month=10
GET /api/manager-bonuses?year=2025&month=10
POST /api/manager-bonuses/{id}/mark-paid
```

## 🔧 Troubleshooting

### Ошибка подключения к базе
- Проверь что MySQL сервис запущен
- Проверь переменные подключения
- Убедись что миграции применены

### Приложение не стартует
- Проверь логи в Railway Dashboard
- Убедись что переменная `PORT` не переопределена
- Проверь что все required переменные окружения установлены

### CORS ошибки
- Добавь домен фронтенда в `PROJECTAPP__Cors__Origins__0`
- Для нескольких доменов добавь `Origins__1`, `Origins__2` и т.д.

## 📚 Дополнительно

### Мониторинг
Railway предоставляет встроенный мониторинг:
- CPU/Memory usage
- Request logs
- Deployment history

### Автодеплой
Railway автоматически деплоит при push в `main` ветку.

Для отключения:
Settings → Triggers → Снять галку "Enable automatic deployments"

### Масштабирование
Railway автоматически масштабирует приложение на основе нагрузки.

Для ручного масштабирования:
Settings → Resources → Adjust replicas

---

🎉 **Готово! Твой ProjectApp теперь в облаке!**
