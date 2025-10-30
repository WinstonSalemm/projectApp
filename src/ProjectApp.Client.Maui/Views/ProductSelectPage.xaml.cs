using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class ProductSelectPage : ContentPage
{
    public event EventHandler<ProductSelectViewModel.ProductRow>? ProductPicked;
    
    private readonly ProductSelectViewModel _vm;
    private readonly IServiceProvider _services;

    public static readonly BindableProperty IsPickerProperty = BindableProperty.Create(
        nameof(IsPicker), typeof(bool), typeof(ProductSelectPage), false, propertyChanged: OnIsPickerChanged);

    public bool IsPicker
    {
        get => (bool)GetValue(IsPickerProperty);
        set => SetValue(IsPickerProperty, value);
    }

    public ProductSelectPage(ProductSelectViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
        _services = services;
    }

    private static void OnIsPickerChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ProductSelectPage page)
        {
            try
            {
                if (newValue is bool b)
                {
                    page.Title = b ? "Выбор товара" : "POJ PRO - Оформление продажи";
                }
            }
            catch { }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Results.Count == 0)
        {
            _vm.SearchAsyncCommand.Execute(null);
        }
    }

    private void OnAllCategoriesClicked(object? sender, EventArgs e)
    {
        _vm.SelectedCategory = null;
        _vm.SearchAsyncCommand.Execute(null);
    }

    private async void OnCategoryChanged(object? sender, EventArgs e)
    {
        try
        {
            // Когда выбрана категория в Picker, автоматически запускаем поиск
            if (_vm?.SearchAsyncCommand != null && _vm.SearchAsyncCommand.CanExecute(null))
            {
                // Небольшая задержка чтобы дать UI время на обновление
                await Task.Delay(50);
                await _vm.SearchAsyncCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProductSelectPage] OnCategoryChanged error: {ex}");
        }
    }

    private void OnAddToCart(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.CommandParameter is not ProductSelectViewModel.ProductRow product) return;
        _vm.AddToCart(product);
    }

    private void OnRemoveFromCart(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.CommandParameter is not ProductSelectViewModel.CartItem item) return;
        _vm.RemoveFromCart(item);
    }

    private async void OnSelectClientClicked(object? sender, EventArgs e)
    {
        var clientPage = _services.GetRequiredService<ClientSelectPage>();
        
        var tcs = new TaskCompletionSource<(int? Id, string Name)>();
        void Handler(object? s, (int? Id, string Name) client) => tcs.TrySetResult(client);
        clientPage.ClientSelected += Handler;
        
        await Navigation.PushAsync(clientPage);
        var selected = await tcs.Task;
        clientPage.ClientSelected -= Handler;
        await Navigation.PopAsync();
        
        _vm.SetClient(selected.Id, selected.Name);
    }

    private void OnPickClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ProductSelectViewModel.ProductRow p)
        {
            ProductPicked?.Invoke(this, p);
        }
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not ProductSelectViewModel vm) return;
        var page = _services.GetService<ProductCreatePage>();
        if (page == null) return;

        var tcs = new TaskCompletionSource<(int Id, string Sku, string Name, string Category)>();
        void Handler(object? s, (int Id, string Sku, string Name, string Category) p) => tcs.TrySetResult(p);
        page.ProductCreated += Handler;
        await Navigation.PushAsync(page);
        var created = await tcs.Task;
        page.ProductCreated -= Handler;
        await Navigation.PopAsync();

        if (created.Id > 0)
        {
            // Select created category and refresh list
            vm.SelectedCategory = string.IsNullOrWhiteSpace(created.Category) ? vm.SelectedCategory : created.Category;
            await vm.LoadCategoriesAsync();
            await vm.SearchAsync();
        }
    }
}

