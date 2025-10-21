using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Services;
using ProjectApp.Client.Maui.Views;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;
    private readonly AuthService _auth;
    private readonly double _compactBreakpoint;
    private readonly double _mediumBreakpoint;
    
    public string UserDisplayName => _auth?.DisplayName ?? _auth?.UserName ?? "Пользователь";
    private readonly IDictionary<string, string> _routeDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = "Administrative overview",
        ["sales"] = "Active sales pipeline",
        ["clients"] = "Customer directory and debts",
        ["inventory"] = "Stock availability and movement",
        ["finances"] = "Cashboxes and expenses",
        ["analytics"] = "Tax and commercial analytics",
        ["settings"] = "Application preferences"
    };
    private string _currentRoute = "sales";

    public AppShell(IServiceProvider services, AuthService auth)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[AppShell] Constructor started");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[AppShell] InitializeComponent completed");
            
            _services = services;
            _auth = auth;
            _compactBreakpoint = GetResourceDouble("Breakpoint.Compact", 800);
            _mediumBreakpoint = GetResourceDouble("Breakpoint.Medium", 1200);

            System.Diagnostics.Debug.WriteLine("[AppShell] RegisterRoutes starting");
            RegisterRoutes();
            System.Diagnostics.Debug.WriteLine("[AppShell] RefreshRoleState starting");
            RefreshRoleState();
            System.Diagnostics.Debug.WriteLine("[AppShell] Event handlers starting");

            Loaded += OnShellLoaded;
            SizeChanged += OnShellSizeChanged;
            BindingContext = this;
            UpdateTitleBar();
            
            // Start checking online status
            _ = CheckOnlineStatusPeriodically();
            
            System.Diagnostics.Debug.WriteLine("[AppShell] Constructor completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Constructor FAILED: {ex}");
            throw;
        }
    }

    private void RegisterRoutes()
    {
        RegisterRoute<QuickSalePage>("quick-sale/detail");
        RegisterRoute<PaymentSelectPage>("sales/payment");
        RegisterRoute<UserSelectPage>("auth/users");
        RegisterRoute<LoginPage>("auth/login");
        RegisterRoute<ContractsListPage>("contracts/list");
        RegisterRoute<ContractCreatePage>("contracts/create");
        RegisterRoute<ContractEditPage>("contracts/edit");
        RegisterRoute<ContractDetailsPage>("contracts/details");
        RegisterRoute<ContractsHistoryPage>("contracts/history");
        RegisterRoute<SuppliesPage>("supplies/list");
        RegisterRoute<SuppliesHistoryPage>("supplies/history");
        RegisterRoute<SalePickerForReturnPage>("returns/picker");
        RegisterRoute<ReturnForSalePage>("returns/create");
        RegisterRoute<ReturnsPage>("returns/list");
        RegisterRoute<ReturnsHistoryPage>("returns/history");
        RegisterRoute<ClientsListPage>("clients/list");
        RegisterRoute<ClientDetailPage>("clients/detail");
        RegisterRoute<ClientCreatePage>("clients/create");
        RegisterRoute<ClientEditPage>("clients/edit");
        RegisterRoute<ClientPickerPage>("clients/picker");
        RegisterRoute<UnregisteredClientPage>("clients/unregistered");
        RegisterRoute<DebtorsListPage>("clients/debtors");
        RegisterRoute<DebtDetailPage>("debts/detail");
        RegisterRoute<StocksPage>("stocks/list");
        RegisterRoute<HistoryTabsPage>("history/tabs");
        RegisterRoute<FinancesMenuPage>("finances/menu");
        RegisterRoute<CashboxesPage>("finances/cashboxes");
        RegisterRoute<ExpensesPage>("finances/expenses");
        RegisterRoute<FinanceAnalyticsPage>("analytics/finance");
        RegisterRoute<ManagerAnalyticsPage>("analytics/managers");
        RegisterRoute<TaxAnalyticsPage>("analytics/tax");
        RegisterRoute<ManagerKpiPage>("analytics/kpi");
        RegisterRoute<CommissionAgentsPage>("analytics/commission");
        RegisterRoute<ProductCostsPage>("analytics/products");
        RegisterRoute<SettingsPage>("settings");
    }

    private void RegisterRoute<TPage>(string route)
        where TPage : Page
    {
        try
        {
            Routing.RegisterRoute(route, typeof(TPage));
        }
        catch (ArgumentException)
        {
            // Route already registered; ignore duplicates when Shell is recreated.
        }
    }

    private void OnShellLoaded(object? sender, EventArgs e) => UpdateVisualState(Width);

    private void OnShellSizeChanged(object? sender, EventArgs e) => UpdateVisualState(Width);

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        if (ReferenceEquals(CurrentItem, DashboardTab))
            _currentRoute = "dashboard";
        else if (ReferenceEquals(CurrentItem, SalesTab))
            _currentRoute = "sales";
        else if (ReferenceEquals(CurrentItem, ClientsTab))
            _currentRoute = "clients";
        else if (ReferenceEquals(CurrentItem, InventoryTab))
            _currentRoute = "inventory";
        else if (ReferenceEquals(CurrentItem, FinancesTab))
            _currentRoute = "finances";
        else if (ReferenceEquals(CurrentItem, AnalyticsTab))
            _currentRoute = "analytics";
        else if (ReferenceEquals(CurrentItem, SettingsTab))
            _currentRoute = "settings";

        UpdateTitleBar();
    }

    private void UpdateVisualState(double width)
    {
        if (width <= 0)
            return;

        string state;
        if (width < _compactBreakpoint)
        {
            state = "Compact";
            FlyoutBehavior = FlyoutBehavior.Disabled;
            SetTabBarVisibility(true);
            FlyoutIsPresented = false;
        }
        else if (width < _mediumBreakpoint)
        {
            state = "Medium";
            FlyoutBehavior = FlyoutBehavior.Disabled;
            SetTabBarVisibility(true);
            FlyoutIsPresented = false;
        }
        else
        {
            state = "Expanded";
            FlyoutBehavior = FlyoutBehavior.Locked;
            SetTabBarVisibility(false);
            FlyoutIsPresented = true;
        }

        if (TitleBar != null)
            TitleBar.IsVisible = state != "Expanded";

        VisualStateManager.GoToState(this, state);
    }

    private void SetTabBarVisibility(bool isVisible)
    {
        SetValue(TabBarIsVisibleProperty, isVisible);
        MainTabBar.IsVisible = isVisible;
    }

    public void RefreshRoleState()
    {
        try
        {
            bool isAdmin = string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            
            // Admin-only tabs and buttons
            if (DashboardTab != null) DashboardTab.IsVisible = isAdmin;
            if (DashboardRailButton != null) DashboardRailButton.IsVisible = isAdmin;
            if (FinancesTab != null) FinancesTab.IsVisible = isAdmin;
            if (FinancesRailButton != null) FinancesRailButton.IsVisible = isAdmin;
            if (AnalyticsTab != null) AnalyticsTab.IsVisible = isAdmin;
            if (AnalyticsRailButton != null) AnalyticsRailButton.IsVisible = isAdmin;

            var defaultRoute = isAdmin ? "dashboard" : "sales";
            NavigateToRoute(defaultRoute);
            
            System.Diagnostics.Debug.WriteLine($"[AppShell] Role-based UI updated. IsAdmin={isAdmin}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] RefreshRoleState failed: {ex}");
        }
    }

    private void OnNavRailClicked(object? sender, EventArgs e)
    {
        if (sender is Button button &&
            button.CommandParameter is string route)
        {
            NavigateToRoute(route);
            FlyoutIsPresented = false;
        }
    }

    public async Task EnsureRouteAsync(string route)
    {
        try
        {
            NavigateToRoute(route);
            await GoToAsync($"//{route}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] EnsureRouteAsync failed for '{route}': {ex}");
            // Fallback to a safe tab
            try { CurrentItem = SalesTab ?? CurrentItem; } catch { }
        }
    }

    private static double GetResourceDouble(string key, double fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true &&
            value is double d)
        {
            return d;
        }
        return fallback;
    }

    private void NavigateToRoute(string route)
    {
        try
        {
            _currentRoute = route;
            switch (route)
            {
                case "dashboard":
                    if (DashboardTab?.IsVisible == true)
                        CurrentItem = DashboardTab;
                    break;
                case "sales":
                    if (SalesTab != null) CurrentItem = SalesTab;
                    break;
                case "clients":
                    if (ClientsTab != null) CurrentItem = ClientsTab;
                    break;
                case "inventory":
                    if (InventoryTab != null) CurrentItem = InventoryTab;
                    break;
                case "finances":
                    if (FinancesTab?.IsVisible == true)
                        CurrentItem = FinancesTab;
                    break;
                case "analytics":
                    if (AnalyticsTab?.IsVisible == true)
                        CurrentItem = AnalyticsTab;
                    break;
                case "settings":
                    if (SettingsTab != null) CurrentItem = SettingsTab;
                    break;
            }

            UpdateTitleBar();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] NavigateToRoute failed: {ex}");
        }
    }

    private void UpdateTitleBar()
    {
        // TitleBar is now a static Grid with POJ PRO branding
        // No dynamic updates needed - logo is always visible
        if (TitleBar is null)
            return;

        // Title and subtitle are now fixed in the POJ PRO branding
        // Keep this method for future enhancements if needed
    }

    private void OnLogoutClicked(object? sender, EventArgs e)
    {
        _auth.Logout();
        var userSelectPage = _services.GetRequiredService<UserSelectPage>();
        NavigationHelper.SetRoot(new NavigationPage(userSelectPage));
    }

    private async Task CheckOnlineStatusPeriodically()
    {
        while (true)
        {
            try
            {
                await Task.Delay(5000); // Check every 5 seconds
                
                var isOnline = Connectivity.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.Internet;
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (StatusIndicator != null)
                        {
                            StatusIndicator.BackgroundColor = isOnline ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336");
                            
                            if (StatusIndicator.Content is HorizontalStackLayout stack && 
                                stack.Children.Count > 1 && 
                                stack.Children[1] is Label label)
                            {
                                label.Text = isOnline ? "Онлайн" : "Офлайн";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppShell] Status update error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] CheckOnlineStatus error: {ex}");
            }
        }
    }
}
