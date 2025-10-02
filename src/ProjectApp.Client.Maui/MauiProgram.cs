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

namespace ProjectApp.Client.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                // Add fonts if needed
            });

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
        builder.Services.AddHttpClient(); // generic factory
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ApiCatalogService>();
        builder.Services.AddSingleton<ApiSalesService>();
        builder.Services.AddSingleton<ApiSuppliesService>();
        builder.Services.AddSingleton<ApiReturnsService>();
        builder.Services.AddSingleton<MockCatalogService>();
        builder.Services.AddSingleton<MockSalesService>();
        // Routed services decide at runtime based on current AppSettings
        builder.Services.AddSingleton<ICatalogService, RoutedCatalogService>();
        builder.Services.AddSingleton<ISalesService, RoutedSalesService>();
        builder.Services.AddSingleton<ApiStocksService>();
        builder.Services.AddSingleton<IStocksService>(sp => sp.GetRequiredService<ApiStocksService>());
        builder.Services.AddSingleton<ISuppliesService>(sp => sp.GetRequiredService<ApiSuppliesService>());
        builder.Services.AddSingleton<IReturnsService>(sp => sp.GetRequiredService<ApiReturnsService>());

        // VM and Views
        builder.Services.AddTransient<QuickSaleViewModel>();
        builder.Services.AddTransient<QuickSalePage>();
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
        builder.Services.AddTransient<ReturnsViewModel>();
        builder.Services.AddTransient<ReturnsPage>();
        builder.Services.AddTransient<StocksViewModel>();
        builder.Services.AddTransient<StocksPage>();

        return builder.Build();
    }
}
