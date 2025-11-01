using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using ProjectApp.Client.Maui.Services;
using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Views;
using CommunityToolkit.Maui;
using Microsoft.Maui.Storage;
using Plugin.Maui.Audio;

namespace ProjectApp.Client.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
            });

#if WINDOWS
        // Hand cursor on hover for ALL buttons
        Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("HoverCursor", (handler, view) =>
        {
            if (handler.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
            {
                fe.PointerEntered += (s, e) => ApplyHandCursor();
                fe.PointerMoved  += (s, e) => ApplyHandCursor();
                fe.PointerExited  += (s, e) => ApplyArrowCursor();
            }
        });
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Load appsettings.json from output directory
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        builder.Configuration.AddConfiguration(config);

        // Bind settings
        var settings = new AppSettings();
        config.Bind(settings);
        // Override with saved Preferences if present
        if (Preferences.ContainsKey("UseApi"))
            settings.UseApi = Preferences.Get("UseApi", settings.UseApi);
        if (Preferences.ContainsKey("ApiBaseUrl"))
            settings.ApiBaseUrl = Preferences.Get("ApiBaseUrl", settings.ApiBaseUrl ?? "http://localhost:5028");
        builder.Services.AddSingleton(settings);

        // Register concrete API and Mock services
        builder.Services.AddTransient<AuthHeaderHandler>();
        builder.Services.AddHttpClient(HttpClientNames.Api, (sp, client) =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            var baseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl) 
                ? "http://localhost:5028" 
                : settings.ApiBaseUrl;
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
            .AddHttpMessageHandler<AuthHeaderHandler>(); // ensure auth header injection
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<SaleSession>();
        builder.Services.AddSingleton<IAudioManager>(sp => AudioManager.Current);
        builder.Services.AddSingleton<ApiCatalogService>();
        builder.Services.AddSingleton<ApiClientsService>();
        builder.Services.AddSingleton<ApiSalesService>();
        builder.Services.AddSingleton<ApiSuppliesService>();
        builder.Services.AddSingleton<ApiCostingService>();
        builder.Services.AddSingleton<ApiStocksService>();
        builder.Services.AddSingleton<MockCatalogService>();
        builder.Services.AddSingleton<MockSalesService>();
        
        // Offline support - ОТКЛЮЧЕНО для избежания зависаний
        // builder.Services.AddSingleton<LocalDatabase>();
        // builder.Services.AddSingleton<OfflineSyncService>();
        // builder.Services.AddSingleton<OfflineSalesService>();
        builder.Services.AddSingleton<ApiHealthChecker>();
        
        // Routed services decide at runtime based on current AppSettings
        builder.Services.AddSingleton<ICatalogService, RoutedCatalogService>();
        builder.Services.AddSingleton<ISalesService, RoutedSalesService>(); // Используем обычный сервис без офлайна
        builder.Services.AddSingleton<IStocksService>(sp => sp.GetRequiredService<ApiStocksService>());
        builder.Services.AddSingleton<ApiReturnsService>();
        builder.Services.AddSingleton<IReturnsService>(sp => sp.GetRequiredService<ApiReturnsService>());
        builder.Services.AddSingleton<ApiReservationsService>();
        builder.Services.AddSingleton<IReservationsService>(sp => sp.GetRequiredService<ApiReservationsService>());
        builder.Services.AddSingleton<ApiContractsService>();
        builder.Services.AddSingleton<IContractsService>(sp => sp.GetRequiredService<ApiContractsService>());
        // Products (create)
        builder.Services.AddSingleton<ApiProductsService>();
        builder.Services.AddSingleton<IProductsService>(sp => sp.GetRequiredService<ApiProductsService>());
        builder.Services.AddSingleton<ISuppliesService>(sp => sp.GetRequiredService<ApiSuppliesService>());
        builder.Services.AddSingleton<ICostingService>(sp => sp.GetRequiredService<ApiCostingService>());
        builder.Services.AddSingleton<IReturnsService>(sp => sp.GetRequiredService<ApiReturnsService>());
        // Finance
        builder.Services.AddSingleton<ApiFinanceService>();
        builder.Services.AddSingleton<IFinanceService>(sp => sp.GetRequiredService<ApiFinanceService>());
        
        // New API Services (with HttpClient)
        builder.Services.AddHttpClient<ApiService>(client =>
        {
            var settings = builder.Services.BuildServiceProvider().GetRequiredService<AppSettings>();
            client.BaseAddress = new Uri(settings.ApiBaseUrl ?? "https://projectapp-production.up.railway.app");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<DebtorsApiService>();
        builder.Services.AddSingleton<FinancesApiService>();
        builder.Services.AddSingleton<TaxApiService>();
        builder.Services.AddSingleton<AnalyticsApiService>();
        builder.Services.AddSingleton<CashCollectionApiService>();
        
        // Defectives and Refills - need HttpClient with factory
        builder.Services.AddSingleton<DefectivesApiService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpClientNames.Api);
            var authService = sp.GetRequiredService<AuthService>();
            return new DefectivesApiService(httpClient, authService);
        });
        builder.Services.AddSingleton<RefillsApiService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpClientNames.Api);
            var authService = sp.GetRequiredService<AuthService>();
            return new RefillsApiService(httpClient, authService);
        });
        builder.Services.AddSingleton<IBatchCostService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpClientNames.Api);
            var logger = sp.GetService<ILogger<BatchCostApiService>>();
            return new BatchCostApiService(httpClient, logger);
        });
        
        // Camera Service (platform-specific)
#if ANDROID
        builder.Services.AddSingleton<ICameraService, ProjectApp.Client.Maui.Platforms.Android.Services.AndroidCameraService>();
#else
        builder.Services.AddSingleton<ICameraService, DefaultCameraService>();
#endif
        builder.Services.AddSingleton<SalePhotoService>();

        // VM and Views
        builder.Services.AddTransient<QuickSaleViewModel>();
        builder.Services.AddTransient<QuickSalePage>();
        builder.Services.AddTransient<SaleStartViewModel>();
        builder.Services.AddTransient<SaleStartPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<PaymentSelectViewModel>();
        builder.Services.AddTransient<PaymentSelectPage>();
        builder.Services.AddTransient<UserSelectViewModel>();
        builder.Services.AddTransient<UserSelectPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SuppliesViewModel>();
        builder.Services.AddTransient<SuppliesPage>();
        builder.Services.AddTransient<SupplyEditViewModel>();
        builder.Services.AddTransient<SupplyEditPage>();
        builder.Services.AddTransient<CostingViewModel>();
        builder.Services.AddTransient<CostingPage>();
        builder.Services.AddTransient<ReturnsViewModel>();
        builder.Services.AddTransient<ReturnsPage>();
        builder.Services.AddTransient<StocksViewModel>();
        builder.Services.AddTransient<StocksPage>();
        builder.Services.AddTransient<ContractsListViewModel>();
        builder.Services.AddTransient<ContractsListPage>();
        builder.Services.AddTransient<ContractCreateViewModel>();
        builder.Services.AddTransient<ContractCreatePage>();
        builder.Services.AddTransient<ContractEditViewModel>();
        builder.Services.AddTransient<ContractEditPage>();
        builder.Services.AddTransient<ProductSelectViewModel>();
        builder.Services.AddTransient<ProductSelectPage>();
        builder.Services.AddTransient<ClientSelectViewModel>();
        builder.Services.AddTransient<ClientSelectPage>();
        builder.Services.AddTransient<AnalyticsViewModel>();
        builder.Services.AddTransient<AnalyticsMenuPage>();
        builder.Services.AddTransient<FinanceAnalyticsPage>();
        builder.Services.AddTransient<AdminDashboardViewModel>();
        builder.Services.AddTransient<AdminDashboardPage>();
        builder.Services.AddTransient<SalesHistoryViewModel>();
        builder.Services.AddTransient<SalesHistoryPage>();
        builder.Services.AddTransient<ReturnSourceSelectorViewModel>();
        builder.Services.AddTransient<ReturnSourceSelectorPage>();
        builder.Services.AddTransient<ReturnForSaleViewModel>();
        builder.Services.AddTransient<ReturnForSalePage>();
        builder.Services.AddTransient<ContractsViewModel>();
        builder.Services.AddTransient<ContractsPage>();
        builder.Services.AddTransient<SalePickerForReturnViewModel>();
        builder.Services.AddTransient<SalePickerForReturnPage>();
        builder.Services.AddTransient<ReturnsHistoryViewModel>();
        builder.Services.AddTransient<ReturnsHistoryPage>();
        builder.Services.AddTransient<ContractsHistoryViewModel>();
        builder.Services.AddTransient<ContractsHistoryPage>();
        builder.Services.AddTransient<ProductCreateViewModel>();
        builder.Services.AddTransient<ProductCreatePage>();
        builder.Services.AddTransient<HistoryTabsPage>();
        builder.Services.AddTransient<FinanceDashboardViewModel>();
        builder.Services.AddTransient<FinanceDashboardPage>();
        builder.Services.AddTransient<ContractDetailsViewModel>();
        builder.Services.AddTransient<ContractDetailsPage>();
        builder.Services.AddTransient<ClientsListViewModel>();
        builder.Services.AddTransient<ClientsListPage>();
        builder.Services.AddTransient<ClientCreateViewModel>();
        builder.Services.AddTransient<ClientCreatePage>();
        builder.Services.AddTransient<ClientDetailViewModel>();
        builder.Services.AddTransient<ClientDetailPage>();
        builder.Services.AddTransient<ClientPickerViewModel>();
        builder.Services.AddTransient<ClientPickerPage>();
        builder.Services.AddTransient<ClientEditViewModel>();
        builder.Services.AddTransient<ClientEditPage>();
        builder.Services.AddTransient<UnregisteredClientViewModel>();
        builder.Services.AddTransient<UnregisteredClientPage>();
        builder.Services.AddTransient<ConfirmAccountPage>();
        builder.Services.AddTransient<SimpleAdminPage>();
        
        // New pages and ViewModels
        builder.Services.AddTransient<DebtorsListViewModel>();
        builder.Services.AddTransient<DebtorsListPage>();
        builder.Services.AddTransient<DebtDetailViewModel>();
        builder.Services.AddTransient<DebtDetailPage>();
        builder.Services.AddTransient<FinancesMenuPage>();
        
        builder.Services.AddTransient<CashboxesViewModel>();
        builder.Services.AddTransient<CashboxesPage>();
        
        builder.Services.AddTransient<ExpensesViewModel>();
        builder.Services.AddTransient<ExpensesPage>();
        
        builder.Services.AddTransient<TaxAnalyticsViewModel>();
        builder.Services.AddTransient<TaxAnalyticsPage>();
        
        builder.Services.AddTransient<ManagerKpiViewModel>();
        builder.Services.AddTransient<ManagerKpiPage>();
        
        builder.Services.AddTransient<CommissionAgentsViewModel>();
        builder.Services.AddTransient<CommissionAgentsPage>();
        
        builder.Services.AddTransient<CashCollectionViewModel>();
        builder.Services.AddTransient<CashCollectionPage>();
        
        builder.Services.AddTransient<DefectivesViewModel>();
        builder.Services.AddTransient<DefectivesPage>();
        
        builder.Services.AddTransient<RefillsViewModel>();
        builder.Services.AddTransient<RefillsPage>();
        
        builder.Services.AddTransient<BatchCostCalculationViewModel>();
        builder.Services.AddTransient<BatchCostCalculationPage>();
        
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }

#if WINDOWS
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr SetCursor(IntPtr hCursor);
    private const int IDC_ARROW = 32512;
    private const int IDC_HAND  = 32649;
    private static void ApplyHandCursor() { try { SetCursor(LoadCursor(IntPtr.Zero, IDC_HAND)); } catch { } }
    private static void ApplyArrowCursor() { try { SetCursor(LoadCursor(IntPtr.Zero, IDC_ARROW)); } catch { } }
#endif
}

