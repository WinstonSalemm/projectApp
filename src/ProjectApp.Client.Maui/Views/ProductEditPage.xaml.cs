using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using System.ComponentModel;

namespace ProjectApp.Client.Maui.Views;

public partial class ProductEditPage : ContentPage
{
    public event EventHandler<(int Id, string Sku, string Name, string Category)>? ProductUpdated;

    private readonly ProductEditViewModel _vm;

    public ProductEditPage(ProductEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += VmOnPropertyChanged;
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductEditViewModel.UpdatedProductId))
        {
            if (_vm.UpdatedProductId is int id && id > 0)
            {
                var cat = string.IsNullOrWhiteSpace(_vm.NewCategoryName)
                    ? (_vm.SelectedCategory ?? string.Empty)
                    : _vm.NewCategoryName;
                ProductUpdated?.Invoke(this, (id, _vm.Sku, _vm.Name, cat ?? string.Empty));
            }
        }
    }

    private async void OnReloadCategoriesClicked(object? sender, EventArgs e)
    {
        if (BindingContext is ProductEditViewModel vm)
        {
            await vm.LoadCategoriesAsync();
        }
    }

    private async void OnCreateCategoryClicked(object? sender, EventArgs e)
    {
        if (BindingContext is ProductEditViewModel vm)
        {
            await vm.CreateCategoryAsync();
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (BindingContext is ProductEditViewModel vm)
        {
            await vm.SaveAsync();
        }
    }
}
