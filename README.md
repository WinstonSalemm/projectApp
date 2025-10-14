# ProjectApp
## Docker

Р—Р°РїСѓСЃРє API РІ Docker С‡РµСЂРµР· compose:

```powershell
cd C:\projectApp
docker compose up -d --build
```

API РїРѕРґРЅРёРјРµС‚СЃСЏ РЅР° `http://localhost:5028`.

Р”Р°РЅРЅС‹Рµ SQLite С…СЂР°РЅСЏС‚СЃСЏ РІ Р»РѕРєР°Р»СЊРЅРѕР№ РїР°РїРєРµ `./data` (РјРѕРЅС‚РёСЂСѓРµС‚СЃСЏ РІ РєРѕРЅС‚РµР№РЅРµСЂ `/app/data`).

РџРµСЂРµРѕРїСЂРµРґРµР»РµРЅРёРµ РєРѕРЅС„РёРіСѓСЂР°С†РёРё С‡РµСЂРµР· РїРµСЂРµРјРµРЅРЅС‹Рµ РѕРєСЂСѓР¶РµРЅРёСЏ (РїРѕРґС…РІР°С‚С‹РІР°СЋС‚СЃСЏ СЃ РїСЂРµС„РёРєСЃРѕРј `PROJECTAPP__`):

```powershellРІРІРІРІ
# Р—Р°РґР°С‚СЊ API РєР»СЋС‡ (РґР»СЏ РјСѓС‚Р°С†РёР№)
$env:PROJECTAPP__Security__ApiKey = "prod-secret"

# Р Р°Р·СЂРµС€РёС‚СЊ РёСЃС‚РѕС‡РЅРёРєРё CORS
$env:PROJECTAPP__Cors__Origins__0 = "https://app.example.com"
$env:PROJECTAPP__Cors__Origins__1 = "https://admin.example.com"

# РџРµСЂРµРѕРїСЂРµРґРµР»РёС‚СЊ СЃС‚СЂРѕРєСѓ РїРѕРґРєР»СЋС‡РµРЅРёСЏ Рє SQLite
$env:PROJECTAPP__ConnectionStrings__DefaultConnection = "Data Source=/app/data/projectapp.db"

docker compose up -d

```

## Recent Fixes
- MAUI font registration now relies on font aliases in `Resources/Fonts`, preventing startup crashes and font URI warnings.
- Auth tokens persist between requests and a delegating handler injects the `Authorization` header for every API call.
- Category-driven screens show a retryable empty state when the API returns errors instead of crashing the UI.
dwdw
Readiness: РїСЂРѕРІРµСЂСЊС‚Рµ `http://localhost:5028/ready` вЂ” 200 РµСЃР»Рё РјРёРіСЂР°С†РёРё РїСЂРёРјРµРЅРµРЅС‹ Рё Р‘Р” РґРѕСЃС‚СѓРїРЅР°. Liveness: `http://localhost:5028/health` вЂ” РІСЃРµРіРґР° 200 РїСЂРё Р¶РёРІРѕРј РїСЂРѕС†РµСЃСЃРµ.
dwdwd
## CORS РєРѕРЅС„РёРіСѓСЂР°С†РёСЏ (CORS Configuration)

Р’ API CORS СѓРїСЂР°РІР»СЏРµС‚СЃСЏ С‡РµСЂРµР· СЃРµРєС†РёСЋ `Cors` РІ РєРѕРЅС„РёРіСѓСЂР°С†РёРё.

- РљРѕРЅС„РёРіРё РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ:
  - `src/ProjectApp.Api/appsettings.json` вЂ” СЃРѕРґРµСЂР¶РёС‚ СЃРїРёСЃРѕРє Origins РґР»СЏ Р»РѕРєР°Р»СЊРЅРѕР№ СЂР°Р·СЂР°Р±РѕС‚РєРё (`http://localhost:5028`, `http://localhost:5000`).
  - `src/ProjectApp.Api/appsettings.Development.json` вЂ” `Origins=["*"]` (СЂР°Р·СЂРµС€Р°РµС‚ РІСЃРµС… РґР»СЏ РґРµРІ-СЂРµР¶РёРјР°).
  - `src/ProjectApp.Api/appsettings.Production.json` вЂ” `Origins=[]` (РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РїСѓСЃС‚Рѕ, РЅСѓР¶РЅРѕ СЏРІРЅРѕ Р·Р°РґР°С‚СЊ).

Р’С‹ РјРѕР¶РµС‚Рµ РїРµСЂРµРѕРїСЂРµРґРµР»СЏС‚СЊ Origins С‡РµСЂРµР· РїРµСЂРµРјРµРЅРЅС‹Рµ РѕРєСЂСѓР¶РµРЅРёСЏ СЃ РїСЂРµС„РёРєСЃРѕРј `PROJECTAPP__` (РѕРЅРё РґРѕР±Р°РІР»СЏСЋС‚СЃСЏ РІ РєРѕРЅС„РёРіСѓСЂР°С†РёСЋ РЅР° СЃС‚Р°СЂС‚Рµ РІ `Program.cs`). РРЅРґРµРєСЃР°С†РёСЏ СЌР»РµРјРµРЅС‚РѕРІ РјР°СЃСЃРёРІР° вЂ” СЃ 0.

РџСЂРёРјРµСЂС‹ (Windows PowerShell):

```powershell
# Р Р°Р·СЂРµС€РёС‚СЊ РґРІР° РёСЃС‚РѕС‡РЅРёРєР°
$env:PROJECTAPP__Cors__Origins__0 = "https://app.example.com"
$env:PROJECTAPP__Cors__Origins__1 = "https://admin.example.com"

# Р—Р°РїСѓСЃРє API РїРѕСЃР»Рµ СѓСЃС‚Р°РЅРѕРІРєРё РїРµСЂРµРјРµРЅРЅС‹С…
cd C:\projectApp\src\ProjectApp.Api
dotnet run --launch-profile http
```

Р’ Production РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РїСЂРёРЅРёРјР°СЋС‚СЃСЏ С‚РѕР»СЊРєРѕ СЏРІРЅРѕ Р·Р°РґР°РЅРЅС‹Рµ Origins. Р’ Development СЂР°Р·СЂРµС€РµРЅС‹ РІСЃРµ РёСЃС‚РѕС‡РЅРёРєРё, С‡С‚РѕР±С‹ СѓРїСЂРѕСЃС‚РёС‚СЊ Р»РѕРєР°Р»СЊРЅСѓСЋ СЂР°Р·СЂР°Р±РѕС‚РєСѓ.

## Р‘Р°Р·Р° РґР°РЅРЅС‹С… (SQLite), РјРёРіСЂР°С†РёРё Рё СЃРёРґРёСЂРѕРІР°РЅРёРµ

- Р’ СЃСЂРµРґРµ Development API РїСЂРё СЃС‚Р°СЂС‚Рµ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё РїСЂРёРјРµРЅСЏРµС‚ РјРёРіСЂР°С†РёРё (`db.Database.Migrate()`), Р° Р·Р°С‚РµРј РІС‹РїРѕР»РЅСЏРµС‚ СЃРёРґРёСЂРѕРІР°РЅРёРµ С‚РѕР»СЊРєРѕ РїСЂРё РїСѓСЃС‚РѕР№ С‚Р°Р±Р»РёС†Рµ `Products`.
- Р¤Р°Р№Р» Р‘Р” `projectapp.db` СЂР°Р·РјРµС‰Р°РµС‚СЃСЏ РІ РєР°С‚Р°Р»РѕРіРµ ContentRoot РїСЂРёР»РѕР¶РµРЅРёСЏ (`src/ProjectApp.Api/` РїСЂРё Р·Р°РїСѓСЃРєРµ РёР· РїСЂРѕРµРєС‚Р°).

Р СѓС‡РЅРѕРµ РїСЂРёРјРµРЅРµРЅРёРµ РјРёРіСЂР°С†РёР№ (РµСЃР»Рё Р·Р°РїСѓСЃРєР°РµС‚Рµ Р±РµР· Р°РІС‚РѕРїСЂРёРјРµРЅРµРЅРёСЏ РёР»Рё РЅР° CI):

```powershell
cd C:\projectApp\src\ProjectApp.Api
dotnet tool restore # РµСЃР»Рё С‚СЂРµР±СѓРµС‚СЃСЏ
dotnet ef database update
```

РћС‚РєР»СЋС‡РµРЅРёРµ СЃРёРґРёСЂРѕРІР°РЅРёСЏ РІ Production:

- РџРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РІ `src/ProjectApp.Api/appsettings.Production.json` СЃРёРґРёСЂРѕРІР°РЅРёРµ РІС‹РєР»СЋС‡РµРЅРѕ:

```json
{
  "Seed": { "Enabled": false }
}
```

- РџРµСЂРµРѕРїСЂРµРґРµР»РёС‚СЊ РјРѕР¶РЅРѕ РїРµСЂРµРјРµРЅРЅРѕР№ РѕРєСЂСѓР¶РµРЅРёСЏ:

```powershell
$env:PROJECTAPP__Seed__Enabled = "false"
```

Р’ Development СЃРёРґРёСЂРѕРІР°РЅРёРµ РІРєР»СЋС‡РµРЅРѕ РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ Рё РїСЂРѕРёР·РѕР№РґС‘С‚ С‚РѕР»СЊРєРѕ РѕРґРёРЅ СЂР°Р· РїСЂРё РїСѓСЃС‚РѕР№ С‚Р°Р±Р»РёС†Рµ.

## РђСѓС‚РµРЅС‚РёС„РёРєР°С†РёСЏ API (ApiKey)

- РЎС…РµРјР° Р°СѓС‚РµРЅС‚РёС„РёРєР°С†РёРё: `ApiKey` С‡РµСЂРµР· Р·Р°РіРѕР»РѕРІРѕРє `X-API-KEY`.
- Р—РЅР°С‡РµРЅРёРµ С‡РёС‚Р°РµС‚СЃСЏ РёР· РєРѕРЅС„РёРіСѓСЂР°С†РёРё `Security:ApiKey`.
- Р’ Dev РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РІ `appsettings.Development.json` СѓСЃС‚Р°РЅРѕРІР»РµРЅРѕ: `"Security": { "ApiKey": "dev-key" }`.

РџРµСЂРµРјРµРЅРЅР°СЏ РѕРєСЂСѓР¶РµРЅРёСЏ РґР»СЏ Prod (Рё РЅРµ С‚РѕР»СЊРєРѕ):

```powershell
$env:PROJECTAPP__Security__ApiKey = "<prod-secret>"
```

РўСЂРµР±СѓРµС‚СЃСЏ РґР»СЏ РјСѓС‚Р°С†РёР№:

- `POST /api/sales`
- `POST /api/returns`

РђРЅРѕРЅРёРјРЅРѕ РґРѕСЃС‚СѓРїРЅС‹:

- `GET /api/products`
- `GET /health`
- Swagger UI

Р’ Swagger РЅР°Р¶РјРёС‚Рµ Authorize Рё РІРІРµРґРёС‚Рµ Р·РЅР°С‡РµРЅРёРµ API Key (Р·Р°РіРѕР»РѕРІРѕРє `X-API-KEY`).

## Health checks

- Liveness: `GET /health` вЂ” РІСЃРµРіРґР° 200, РµСЃР»Рё РїСЂРѕС†РµСЃСЃ Р¶РёРІ.
- Readiness: `GET /ready` вЂ” 200 С‚РѕР»СЊРєРѕ РµСЃР»Рё Р‘Р” РґРѕСЃС‚СѓРїРЅР° (РїСЂРѕРІРµСЂРєР° `db`).

РџСЂРѕРІРµСЂРєР° С‡РµСЂРµР· curl (PowerShell):

```powershell
curl http://localhost:5028/health -v
curl http://localhost:5028/ready -v
```


Р СѓСЃ/Eng quick guide for the repository at `C:\projectApp`.

## РћР±Р·РѕСЂ (Overview)
- Р‘СЌРєРµРЅРґ: `src/ProjectApp.Api` вЂ” ASP.NET Core (net9.0), EF Core + SQLite, Swagger.
- РўРµСЃС‚С‹ API: `src/tests/ProjectApp.Api.Tests` вЂ” xUnit + FluentAssertions, InMemory SQLite.
- РљР»РёРµРЅС‚: `src/ProjectApp.Client.Maui` вЂ” .NET MAUI (Windows), СЌРєСЂР°РЅ Р±С‹СЃС‚СЂРѕР№ РїСЂРѕРґР°Р¶Рё (QuickSale) СЃ РјРѕРєРѕРј/СЂРµР¶РёРјРѕРј API.

Back-end API (ASP.NET Core + EF Core SQLite), tests (xUnit), and a .NET MAUI client for Quick Sale with mock/API switch.

## Р‘С‹СЃС‚СЂС‹Р№ СЃС‚Р°СЂС‚ (Quick Start)

### 1) РЎР±РѕСЂРєР° (Build)
```powershell
cd C:\projectApp\src
dotnet restore
dotnet build
```

### 2) Р—Р°РїСѓСЃРє API (Run API)
```powershell
cd C:\projectApp\src\ProjectApp.Api
dotnet run --launch-profile http
```
Swagger UI: http://localhost:5028/swagger

Profiles/ports (СЃРј. `src/ProjectApp.Api/Properties/launchSettings.json`):
- http: `http://localhost:5028`
- https: `https://localhost:7289`

### 3) РњРёРіСЂР°С†РёРё Р‘Р” (EF Core Migrations)
РџСЂРѕРµРєС‚ РёСЃРїРѕР»СЊР·СѓРµС‚ SQLite. РџРµСЂРµРґ Р·Р°РїСѓСЃРєРѕРј API РІ РїРµСЂРІС‹Р№ СЂР°Р· РїСЂРёРјРµРЅРёС‚Рµ РјРёРіСЂР°С†РёРё:
```powershell
cd C:\projectApp\src\ProjectApp.Api
dotnet tool restore # РµСЃР»Рё РЅСѓР¶РЅРѕ
dotnet ef database update
```
РўРµСЃС‚С‹ РїСЂРёРјРµРЅСЏСЋС‚ РјРёРіСЂР°С†РёРё Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё (СЃРј. С‚РµСЃС‚РѕРІСѓСЋ С„РёРєСЃС‚СѓСЂСѓ), РЅРѕ СЃР°РјРѕ API вЂ” РЅРµС‚.

## MAUI РєР»РёРµРЅС‚ (Windows) вЂ” Р·Р°РїСѓСЃРє Рё СЂРµР¶РёРј API

### Р—Р°РїСѓСЃРє РєР»РёРµРЅС‚Р°
```powershell
cd C:\projectApp\src
dotnet build -t:Run -f net9.0-windows10.0.19041.0 ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj
```
Р›РёР±Рѕ РёР· Visual Studio (F5), РІС‹Р±СЂР°РІ Windows РєР°Рє С†РµР»РµРІСѓСЋ РїР»Р°С‚С„РѕСЂРјСѓ.

### Р’РєР»СЋС‡РµРЅРёРµ API-СЂРµР¶РёРјР°
Р¤Р°Р№Р» РЅР°СЃС‚СЂРѕРµРє РєР»РёРµРЅС‚Р°: `src/ProjectApp.Client.Maui/appsettings.json`
```json
{
  "UseApi": true,
  "ApiBaseUrl": "http://localhost:5028"
}
```
- `UseApi=false` вЂ” РѕС„С„Р»Р°Р№РЅ (РјРѕРєРё), Р±Р°РЅРЅРµСЂ В«РћС„С„Р»Р°Р№РЅ СЂРµР¶РёРј (РјРѕРєРё)В».
- `UseApi=true` вЂ” РѕРЅР»Р°Р№РЅ С‡РµСЂРµР· API (`/api/products`, `/api/sales`). РЈР±РµРґРёС‚РµСЃСЊ, С‡С‚Рѕ API Р·Р°РїСѓС‰РµРЅРѕ РЅР° РїРѕСЂС‚Сѓ РёР· `launchSettings.json`.

## РљРѕРјР°РЅРґС‹ (Commands)

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

## РЎС‚СЂСѓРєС‚СѓСЂР° РєР°С‚Р°Р»РѕРіРѕРІ (Directory Structure)

```
C:\projectApp\
в””в”Ђ src\
   в”њв”Ђ ProjectApp.sln                   # Solution: API, tests, MAUI client
   в”њв”Ђ ProjectApp.Api\                 # ASP.NET Core Web API (net9.0)
   в”‚  в”њв”Ђ Controllers\                 # ProductsController, SalesController, ReturnsController
   в”‚  в”њв”Ђ Data\                        # AppDbContext, EF Core setup, seed data
   в”‚  в”њв”Ђ Migrations\                  # EF Core migrations for SQLite
   в”‚  в”њв”Ђ Models\                      # Product, Sale, SaleItem, Stock, Return, PaymentType, ...
   в”‚  в”њв”Ђ Repositories\                # EfProductRepository, EfSaleRepository, interfaces
   в”‚  в”њв”Ђ Services\                    # SaleCalculator (+ ISaleCalculator)
   в”‚  в””в”Ђ Program.cs                   # DI, Swagger, JSON enum as strings, CORS
   в”‚
   в”њв”Ђ tests\
   в”‚  в””в”Ђ ProjectApp.Api.Tests\        # xUnit + FluentAssertions
   в”‚     в”њв”Ђ SqliteDbFixture.cs        # InMemory SQLite with db.Database.Migrate()
   в”‚     в”њв”Ђ SaleCalculatorTests.cs    # РџСЂРѕРІРµСЂРєР° Total РїРѕ РЅРµСЃРєРѕР»СЊРєРёРј РїРѕР·РёС†РёСЏРј
   в”‚     в”њв”Ђ StockRegisterSelectionTests.cs  # РЎРїРёСЃР°РЅРёРµ IM40/ND40 РїРѕ PaymentType
   в”‚     в””в”Ђ ReturnsControllerTests.cs # Р’РѕР·РІСЂР°С‚ РІРѕР·РІСЂР°С‰Р°РµС‚ РѕСЃС‚Р°С‚РєРё РІ РЅСѓР¶РЅС‹Р№ СЂРµРіРёСЃС‚СЂ
   в”‚
   в””в”Ђ ProjectApp.Client.Maui\         # .NET MAUI client (Windows)
      в”њв”Ђ appsettings.json             # UseApi flag, ApiBaseUrl
      в”њв”Ђ App.xaml / App.xaml.cs       # Startup (QuickSalePage)
      в”њв”Ђ MauiProgram.cs               # DI: Mock*/Api* services, HttpClient
      в”њв”Ђ Models\                      # ProductModel, CartItemModel (Observable), PaymentType
      в”њв”Ђ Services\                    # ICatalogService/ISalesService; Mock*/Api* impls
      в”њв”Ђ ViewModels\                  # QuickSaleViewModel (РїРѕРёСЃРє СЃ debounce, РѕС„С„Р»Р°Р№РЅ Р±Р°РЅРЅРµСЂ, РёС‚РѕРіРё)
      в””в”Ђ Views\                       # QuickSalePage (UI)
