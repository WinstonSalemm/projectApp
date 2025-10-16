using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;

namespace ProjectApp.Client.Maui.Views;

public partial class ProductSelectPage : ContentPage
{
    public event EventHandler<ProductSelectViewModel.ProductRow>? ProductPicked;
    
    private readonly ProductSelectViewModel _vm;
    private readonly IServiceProvider _services;

    public ProductSelectPage(ProductSelectViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
        _services = services;
        
        // Subscribe to categories changes
        _vm.Categories.CollectionChanged += OnCategoriesChanged;
        
        // Initial load
        LoadCategories();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Results.Count == 0)
        {
            _vm.SearchAsyncCommand.Execute(null);
        }
    }

    private void OnCategoriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LoadCategories();
    }

    private void LoadCategories()
    {
        CategoriesStack.Clear();
        
        // "Все" button
        var allButton = new Button
        {
            Text = "Все",
            HeightRequest = 36,
            Padding = new Thickness(16, 0),
            BackgroundColor = _vm.SelectedCategory == null ? Colors.Blue : Colors.Transparent,
            TextColor = _vm.SelectedCategory == null ? Colors.White : Colors.Gray,
            BorderColor = Colors.Gray,
            BorderWidth = 1,
            CornerRadius = 18
        };
        allButton.Clicked += (s, e) =>
        {
            _vm.SelectedCategory = null;
            _vm.SearchAsyncCommand.Execute(null);
            LoadCategories();
        };
        CategoriesStack.Add(allButton);

        // Category buttons
        foreach (var category in _vm.Categories)
        {
            var isSelected = _vm.SelectedCategory == category;
            var btn = new Button
            {
                Text = category.ToString(),
                HeightRequest = 36,
                Padding = new Thickness(16, 0),
                BackgroundColor = isSelected ? Colors.Blue : Colors.Transparent,
                TextColor = isSelected ? Colors.White : Colors.Gray,
                BorderColor = Colors.Gray,
                BorderWidth = 1,
                CornerRadius = 18
            };
            var cat = category;
            btn.Clicked += (s, e) =>
            {
                _vm.SelectedCategory = cat;
                _vm.SearchAsyncCommand.Execute(null);
                LoadCategories();
            };
            CategoriesStack.Add(btn);
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // no-op; we use button click to confirm
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

