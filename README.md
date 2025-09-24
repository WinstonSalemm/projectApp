# ProjectApp
## Docker

Запуск API в Docker через compose:

```powershell
cd C:\projectApp
docker compose up -d --build
```

API поднимется на `http://localhost:5028`.

Данные SQLite хранятся в локальной папке `./data` (монтируется в контейнер `/app/data`).

Переопределение конфигурации через переменные окружения (подхватываются с префиксом `PROJECTAPP__`):

```powershell
# Задать API ключ (для мутаций)
$env:PROJECTAPP__Security__ApiKey = "prod-secret"

# Разрешить источники CORS
$env:PROJECTAPP__Cors__Origins__0 = "https://app.example.com"
$env:PROJECTAPP__Cors__Origins__1 = "https://admin.example.com"

# Переопределить строку подключения к SQLite
$env:PROJECTAPP__ConnectionStrings__DefaultConnection = "Data Source=/app/data/projectapp.db"

docker compose up -d
```

Readiness: проверьте `http://localhost:5028/ready` — 200 если миграции применены и БД доступна. Liveness: `http://localhost:5028/health` — всегда 200 при живом процессе.

## CORS конфигурация (CORS Configuration)

В API CORS управляется через секцию `Cors` в конфигурации.

- Конфиги по умолчанию:
  - `src/ProjectApp.Api/appsettings.json` — содержит список Origins для локальной разработки (`http://localhost:5028`, `http://localhost:5000`).
  - `src/ProjectApp.Api/appsettings.Development.json` — `Origins=["*"]` (разрешает всех для дев-режима).
  - `src/ProjectApp.Api/appsettings.Production.json` — `Origins=[]` (по умолчанию пусто, нужно явно задать).

Вы можете переопределять Origins через переменные окружения с префиксом `PROJECTAPP__` (они добавляются в конфигурацию на старте в `Program.cs`). Индексация элементов массива — с 0.

Примеры (Windows PowerShell):

```powershell
# Разрешить два источника
$env:PROJECTAPP__Cors__Origins__0 = "https://app.example.com"
$env:PROJECTAPP__Cors__Origins__1 = "https://admin.example.com"

# Запуск API после установки переменных
cd C:\projectApp\src\ProjectApp.Api
dotnet run --launch-profile http
```

В Production по умолчанию принимаются только явно заданные Origins. В Development разрешены все источники, чтобы упростить локальную разработку.

## База данных (SQLite), миграции и сидирование

- В среде Development API при старте автоматически применяет миграции (`db.Database.Migrate()`), а затем выполняет сидирование только при пустой таблице `Products`.
- Файл БД `projectapp.db` размещается в каталоге ContentRoot приложения (`src/ProjectApp.Api/` при запуске из проекта).

Ручное применение миграций (если запускаете без автоприменения или на CI):

```powershell
cd C:\projectApp\src\ProjectApp.Api
dotnet tool restore # если требуется
dotnet ef database update
```

Отключение сидирования в Production:

- По умолчанию в `src/ProjectApp.Api/appsettings.Production.json` сидирование выключено:

```json
{
  "Seed": { "Enabled": false }
}
```

- Переопределить можно переменной окружения:

```powershell
$env:PROJECTAPP__Seed__Enabled = "false"
```

В Development сидирование включено по умолчанию и произойдёт только один раз при пустой таблице.

## Аутентификация API (ApiKey)

- Схема аутентификации: `ApiKey` через заголовок `X-API-KEY`.
- Значение читается из конфигурации `Security:ApiKey`.
- В Dev по умолчанию в `appsettings.Development.json` установлено: `"Security": { "ApiKey": "dev-key" }`.

Переменная окружения для Prod (и не только):

```powershell
$env:PROJECTAPP__Security__ApiKey = "<prod-secret>"
```

Требуется для мутаций:

- `POST /api/sales`
- `POST /api/returns`

Анонимно доступны:

- `GET /api/products`
- `GET /health`
- Swagger UI

В Swagger нажмите Authorize и введите значение API Key (заголовок `X-API-KEY`).

## Health checks

- Liveness: `GET /health` — всегда 200, если процесс жив.
- Readiness: `GET /ready` — 200 только если БД доступна (проверка `db`).

Проверка через curl (PowerShell):

```powershell
curl http://localhost:5028/health -v
curl http://localhost:5028/ready -v
```


Рус/Eng quick guide for the repository at `C:\projectApp`.

## Обзор (Overview)
- Бэкенд: `src/ProjectApp.Api` — ASP.NET Core (net9.0), EF Core + SQLite, Swagger.
- Тесты API: `src/tests/ProjectApp.Api.Tests` — xUnit + FluentAssertions, InMemory SQLite.
- Клиент: `src/ProjectApp.Client.Maui` — .NET MAUI (Windows), экран быстрой продажи (QuickSale) с моком/режимом API.

Back-end API (ASP.NET Core + EF Core SQLite), tests (xUnit), and a .NET MAUI client for Quick Sale with mock/API switch.

## Быстрый старт (Quick Start)

### 1) Сборка (Build)
```powershell
cd C:\projectApp\src
dotnet restore
dotnet build
```

### 2) Запуск API (Run API)
```powershell
cd C:\projectApp\src\ProjectApp.Api
dotnet run --launch-profile http
```
Swagger UI: http://localhost:5028/swagger

Profiles/ports (см. `src/ProjectApp.Api/Properties/launchSettings.json`):
- http: `http://localhost:5028`
- https: `https://localhost:7289`

### 3) Миграции БД (EF Core Migrations)
Проект использует SQLite. Перед запуском API в первый раз примените миграции:
```powershell
cd C:\projectApp\src\ProjectApp.Api
dotnet tool restore # если нужно
dotnet ef database update
```
Тесты применяют миграции автоматически (см. тестовую фикстуру), но само API — нет.

## MAUI клиент (Windows) — запуск и режим API

### Запуск клиента
```powershell
cd C:\projectApp\src
dotnet build -t:Run -f net9.0-windows10.0.19041.0 ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj
```
Либо из Visual Studio (F5), выбрав Windows как целевую платформу.

### Включение API-режима
Файл настроек клиента: `src/ProjectApp.Client.Maui/appsettings.json`
```json
{
  "UseApi": true,
  "ApiBaseUrl": "http://localhost:5028"
}
```
- `UseApi=false` — оффлайн (моки), баннер «Оффлайн режим (моки)».
- `UseApi=true` — онлайн через API (`/api/products`, `/api/sales`). Убедитесь, что API запущено на порту из `launchSettings.json`.

## Команды (Commands)

### Build
```powershell
cd C:\projectApp\src
dotnet build
```

### Test
```powershell
cd C:\projectApp\src
dotnet test
```

### Run API
```powershell
cd C:\projectApp\src\ProjectApp.Api
dotnet run --launch-profile http
```

### Run MAUI (Windows)
```powershell
cd C:\projectApp\src
dotnet build -t:Run -f net9.0-windows10.0.19041.0 ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj
```

## Структура каталогов (Directory Structure)

```
C:\projectApp\
└─ src\
   ├─ ProjectApp.sln                   # Solution: API, tests, MAUI client
   ├─ ProjectApp.Api\                 # ASP.NET Core Web API (net9.0)
   │  ├─ Controllers\                 # ProductsController, SalesController, ReturnsController
   │  ├─ Data\                        # AppDbContext, EF Core setup, seed data
   │  ├─ Migrations\                  # EF Core migrations for SQLite
   │  ├─ Models\                      # Product, Sale, SaleItem, Stock, Return, PaymentType, ...
   │  ├─ Repositories\                # EfProductRepository, EfSaleRepository, interfaces
   │  ├─ Services\                    # SaleCalculator (+ ISaleCalculator)
   │  └─ Program.cs                   # DI, Swagger, JSON enum as strings, CORS
   │
   ├─ tests\
   │  └─ ProjectApp.Api.Tests\        # xUnit + FluentAssertions
   │     ├─ SqliteDbFixture.cs        # InMemory SQLite with db.Database.Migrate()
   │     ├─ SaleCalculatorTests.cs    # Проверка Total по нескольким позициям
   │     ├─ StockRegisterSelectionTests.cs  # Списание IM40/ND40 по PaymentType
   │     └─ ReturnsControllerTests.cs # Возврат возвращает остатки в нужный регистр
   │
   └─ ProjectApp.Client.Maui\         # .NET MAUI client (Windows)
      ├─ appsettings.json             # UseApi flag, ApiBaseUrl
      ├─ App.xaml / App.xaml.cs       # Startup (QuickSalePage)
      ├─ MauiProgram.cs               # DI: Mock*/Api* services, HttpClient
      ├─ Models\                      # ProductModel, CartItemModel (Observable), PaymentType
      ├─ Services\                    # ICatalogService/ISalesService; Mock*/Api* impls
      ├─ ViewModels\                  # QuickSaleViewModel (поиск с debounce, оффлайн баннер, итоги)
      └─ Views\                       # QuickSalePage (UI)
