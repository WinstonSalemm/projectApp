using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ProductCreateViewModel : ObservableObject
{
    private readonly ICatalogService _catalog;
    private readonly IProductsService _products;

    [ObservableProperty] private string sku = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string unit = "шт";
    [ObservableProperty] private decimal price;

    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private string newCategoryName = string.Empty;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;

    public ObservableCollection<string> Categories { get; } = new();

    // filled after successful creation
    [ObservableProperty] private int? lastCreatedProductId;

    public ProductCreateViewModel(ICatalogService catalog, IProductsService products)
    {
        _catalog = catalog;
        _products = products;
        _ = LoadCategoriesAsync();
    }

    [RelayCommand]
    public async Task LoadCategoriesAsync()
    {
        try
        {
            IsBusy = true; StatusMessage = string.Empty;
            Categories.Clear();
            var cats = await _catalog.GetCategoriesAsync();
            foreach (var c in cats) Categories.Add(c);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task CreateCategoryAsync()
    {
        var n = (NewCategoryName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n)) { StatusMessage = "Введите название категории"; return; }
        try
        {
            IsBusy = true; StatusMessage = string.Empty;
            var ok = await _products.CreateCategoryAsync(n);
            if (ok)
            {
                if (!Categories.Contains(n)) Categories.Add(n);
                SelectedCategory = n;
                NewCategoryName = string.Empty;
                StatusMessage = "Категория создана";
            }
            else StatusMessage = "Не удалось создать категорию";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task CreateProductAsync()
    {
        if (string.IsNullOrWhiteSpace(Sku) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Unit))
        {
            StatusMessage = "Заполните SKU, Название и Ед."; return;
        }
        var cat = !string.IsNullOrWhiteSpace(NewCategoryName?.Trim()) ? NewCategoryName.Trim() : (SelectedCategory ?? string.Empty);
        try
        {
            IsBusy = true; StatusMessage = string.Empty; LastCreatedProductId = null;
            if (!string.IsNullOrWhiteSpace(NewCategoryName?.Trim()))
            {
                await _products.CreateCategoryAsync(NewCategoryName.Trim());
            }
            var id = await _products.CreateProductAsync(new ProductCreateDraft
            {
                Sku = Sku.Trim(),
                Name = Name.Trim(),
                Unit = Unit.Trim(),
                Price = Price,
                Category = cat
            });
            if (id.HasValue)
            {
                LastCreatedProductId = id.Value;
                StatusMessage = "Товар создан";
            }
            else StatusMessage = "Ошибка создания товара";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
