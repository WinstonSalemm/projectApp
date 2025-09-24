# ProjectApp.Client.Maui

A minimal .NET MAUI client for quick sales with mock/API switch.

## Features
- QuickSalePage (MVVM):
  - Search by SKU/Name with 300ms debounce
  - Add items to cart, quantity Stepper, totals
  - PaymentType selection (CashWithReceipt, CashNoReceipt, CardWithReceipt, Click)
  - "Провести" button (mock success)
- Services with DI: ICatalogService/ISalesService
  - Mock* (default)
  - Api* (calls ProjectApp.Api: GET /api/products, POST /api/sales)
- Offline banner when using mocks (UseApi=false)

## Structure
- `Models/` — basic models (ProductModel, CartItemModel, PaymentType)
- `Services/` — interfaces and implementations (Mock*, Api*)
- `ViewModels/` — `QuickSaleViewModel`
- `Views/` — `QuickSalePage`

## Build & Run (Windows)
Prerequisites:
- .NET 9 SDK
- Visual Studio 2022 with .NET Multi-platform App UI (MAUI) workload

Steps:
1. Open terminal in repo and restore/build:
   ```powershell
   cd C:\projectApp\src
   dotnet restore
   dotnet build ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj
   ```
2. Run MAUI Windows target:
   ```powershell
   dotnet build -t:Run -f net9.0-windows10.0.19041.0 ProjectApp.Client.Maui/ProjectApp.Client.Maui.csproj
   ```
   Or start `ProjectApp.Client.Maui` from Visual Studio (F5) with Windows as target.

## API Mode (UseApi=true)
The client defaults to mock services. To switch to API:
1. Edit `ProjectApp.Client.Maui/appsettings.json`:
   ```json
   {
     "UseApi": true,
     "ApiBaseUrl": "http://localhost:5028"
   }
   ```
   - Recommended base URL uses `launchSettings.json` in `ProjectApp.Api`:
     - http: `http://localhost:5028`
     - https: `https://localhost:7289`
2. Start the API in another terminal:
   ```powershell
   cd C:\projectApp\src\ProjectApp.Api
   dotnet run --launch-profile http
   ```
3. Run the MAUI client (see above). The yellow banner disappears in API mode. Search pulls data from `/api/products`, and "Провести" posts to `/api/sales`.

## Verify mock → API
- Mock mode (`UseApi=false`):
  - Banner "Оффлайн режим (моки)"
  - Search returns 2–3 seeded mock products.
  - Провести always shows success.
- API mode (`UseApi=true` with Api running):
  - Banner hidden.
  - Search results come from server seed (`AppDbContext` `HasData`).
  - Провести posts sale and shows success if HTTP 201.

## Notes
- Enum `PaymentType` is sent as string to API (API uses `JsonStringEnumConverter`).
- Stepper binds to double `Qty` in `CartItemModel` for smoother UX; values are converted to decimal for API.
