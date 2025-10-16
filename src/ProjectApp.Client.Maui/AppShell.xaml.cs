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
    private readonly IDictionary<string, string> _routeDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = "Administrative overview",
        ["sales"] = "Active sales pipeline",
        ["inventory"] = "Stock availability and movement",
        ["clients"] = "Customer directory and ownership",
        ["finance"] = "Gross profit and cash-flow analytics",
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
            UpdateTitleBar();
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
        RegisterRoute<ContractsHistoryPage>("contracts/history");
        RegisterRoute<SuppliesPage>("supplies/list");
        RegisterRoute<SuppliesHistoryPage>("supplies/history");
        RegisterRoute<ReturnsPage>("returns/list");
        RegisterRoute<ReturnsHistoryPage>("returns/history");
        RegisterRoute<ClientsListPage>("clients/list");
        RegisterRoute<ClientDetailPage>("clients/detail");
        RegisterRoute<ClientCreatePage>("clients/create");
        RegisterRoute<ClientEditPage>("clients/edit");
        RegisterRoute<ClientPickerPage>("clients/picker");
        RegisterRoute<UnregisteredClientPage>("clients/unregistered");
        RegisterRoute<StocksPage>("stocks/list");
        RegisterRoute<HistoryTabsPage>("history/tabs");
        RegisterRoute<FinanceDashboardPage>("finance/dashboard");
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
        else if (ReferenceEquals(CurrentItem, InventoryTab))
            _currentRoute = "inventory";
        else if (ReferenceEquals(CurrentItem, ClientsTab))
            _currentRoute = "clients";
        else if (ReferenceEquals(CurrentItem, FinanceTab))
            _currentRoute = "finance";
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
            if (FinanceTab != null) FinanceTab.IsVisible = isAdmin;
            if (FinanceRailButton != null) FinanceRailButton.IsVisible = isAdmin;
            if (DashboardRailButton != null) DashboardRailButton.IsVisible = isAdmin;
            if (DashboardTab != null) DashboardTab.IsVisible = isAdmin;

            var defaultRoute = isAdmin ? "dashboard" : "sales";
            NavigateToRoute(defaultRoute);
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
                case "inventory":
                    if (InventoryTab != null) CurrentItem = InventoryTab;
                    break;
                case "clients":
                    if (ClientsTab != null) CurrentItem = ClientsTab;
                    break;
                case "finance":
                    if (FinanceTab?.IsVisible == true)
                        CurrentItem = FinanceTab;
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
        if (TitleBar is null)
            return;

        var title = CurrentPage?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = _routeDescriptions.TryGetValue(_currentRoute, out var name)
                ? name
                : "ProjectApp";
        }

        var subtitle = _routeDescriptions.TryGetValue(_currentRoute, out var description)
            ? description
            : "Project control centre";

        TitleBar.Title = title;
        TitleBar.Subtitle = subtitle ?? "ProjectApp";
    }

    private void OnLogoutClicked(object? sender, EventArgs e)
    {
        _auth.Logout();
        var userSelectPage = _services.GetRequiredService<UserSelectPage>();
        NavigationHelper.SetRoot(new NavigationPage(userSelectPage));
    }
}
