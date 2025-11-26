using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ProductEditViewModel : ObservableObject
{
    private readonly ICatalogService _catalog;
    private readonly IProductsService _products;

    [ObservableProperty] private int productId;
    [ObservableProperty] private string sku = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string unit = "шт";

    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private string newCategoryName = string.Empty;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;

    [ObservableProperty] private int? updatedProductId;

    public ObservableCollection<string> Categories { get; } = new();

    public ProductEditViewModel(ICatalogService catalog, IProductsService products)
    {
        _catalog = catalog;
        _products = products;
    }

    public void LoadFromParameters(int id, string sku, string name, string category, string unit)
    {
        ProductId = id;
        Sku = sku ?? string.Empty;
        Name = name ?? string.Empty;
        Unit = string.IsNullOrWhiteSpace(unit) ? "шт" : unit;
        SelectedCategory = string.IsNullOrWhiteSpace(category) ? null : category;
    }

    [RelayCommand]
    public async Task LoadCategoriesAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            Categories.Clear();
            var cats = await _catalog.GetCategoriesAsync();
            foreach (var c in cats) Categories.Add(c);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CreateCategoryAsync()
    {
        var n = (NewCategoryName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n))
        {
            StatusMessage = "Введите название категории";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            var ok = await _products.CreateCategoryAsync(n);
            if (ok)
            {
                NewCategoryName = string.Empty;
                StatusMessage = "Категория создана";
                await LoadCategoriesAsync();
                SelectedCategory = n;
            }
            else
            {
                StatusMessage = "Не удалось создать категорию";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (ProductId <= 0)
        {
            StatusMessage = "Не задан Id товара";
            return;
        }

        if (string.IsNullOrWhiteSpace(Sku) || string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Заполните SKU и Название";
            return;
        }

        var category = !string.IsNullOrWhiteSpace(NewCategoryName?.Trim())
            ? NewCategoryName.Trim()
            : (SelectedCategory ?? string.Empty);

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            UpdatedProductId = null;

            if (!string.IsNullOrWhiteSpace(NewCategoryName?.Trim()))
            {
                await _products.CreateCategoryAsync(NewCategoryName.Trim());
            }

            var ok = await _products.UpdateProductAsync(ProductId, Sku.Trim(), Name.Trim(), category);
            if (ok)
            {
                UpdatedProductId = ProductId;
                StatusMessage = "Товар обновлён";
            }
            else
            {
                StatusMessage = "Ошибка сохранения товара";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
