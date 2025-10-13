using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using System.ComponentModel;

namespace ProjectApp.Client.Maui.Views;

public partial class ProductCreatePage : ContentPage
{
    public event EventHandler<(int Id, string Sku, string Name, string Category)>? ProductCreated;

    private readonly ProductCreateViewModel _vm;

    public ProductCreatePage(ProductCreateViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += VmOnPropertyChanged;
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductCreateViewModel.LastCreatedProductId))
        {
            if (_vm.LastCreatedProductId is int id && id > 0)
            {
                var cat = string.IsNullOrWhiteSpace(_vm.NewCategoryName) ? (_vm.SelectedCategory ?? string.Empty) : _vm.NewCategoryName;
                ProductCreated?.Invoke(this, (id, _vm.Sku, _vm.Name, cat ?? string.Empty));
            }
        }
    }
}

