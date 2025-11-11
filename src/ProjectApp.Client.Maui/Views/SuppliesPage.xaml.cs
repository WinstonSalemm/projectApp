using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class SuppliesPage : ContentPage
{
    private readonly ISuppliesService _suppliesService;
    
    public SuppliesPage(SuppliesViewModel viewModel, ISuppliesService suppliesService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _suppliesService = suppliesService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is SuppliesViewModel vm)
        {
            await vm.LoadSuppliesCommand.ExecuteAsync(null);
        }
    }
    
    // ✅ TOOLBAR: Создать поставку
    private async void OnCreateSupplyClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("=== OnCreateSupplyClicked FIRED (TOOLBAR) ===");
        await CreateSupplyDirect();
    }
    
    // ✅ ОБЩИЙ МЕТОД: Создание поставки
    private async Task CreateSupplyDirect()
    {
        try
        {
            var code = await DisplayPromptAsync(
                "Новая поставка",
                "Введите № ГТД:",
                "Создать",
                "Отмена",
                placeholder: "ГТД-123");
            
            if (string.IsNullOrWhiteSpace(code))
                return;

            var newSupply = await _suppliesService.CreateSupplyAsync(code);
            
            if (newSupply == null)
            {
                await DisplayAlert("❌ ОШИБКА", "API вернул NULL - поставка не создана!", "ОК");
                return;
            }
            
            // Добавляем в нужную коллекцию через ViewModel
            if (BindingContext is SuppliesViewModel vm)
            {
                if (newSupply.RegisterType == "ND40")
                {
                    vm.Nd40Supplies.Insert(0, newSupply);
                }
                else
                {
                    vm.Im40Supplies.Insert(0, newSupply);
                }
            }
            
            await DisplayAlert("✅ УСПЕХ", $"Поставка {code} создана в {newSupply.RegisterType}\n\nНажмите кнопку ✏️ чтобы добавить товары", "ОК");
          
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateSupplyDirect error: {ex}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            await DisplayAlert("❌ ОШИБКА", $"Ошибка: {ex.Message}\n\n{ex.StackTrace}", "ОК");
        }
    }
    
    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("=== OnRefreshClicked FIRED (TOOLBAR) ===");
        
        if (BindingContext is SuppliesViewModel vm)
        {
            await vm.LoadSuppliesCommand.ExecuteAsync(null);
        }
    }
    
    private async void OnEditSupplyClicked(object sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== OnEditSupplyClicked START");
            System.Diagnostics.Debug.WriteLine($"sender is null: {sender == null}");
            
            if (sender == null)
            {
                await DisplayAlert("Ошибка", "sender is NULL!", "ОК");
                return;
            }
            
            if (sender is Microsoft.Maui.Controls.Button button)
            {
                System.Diagnostics.Debug.WriteLine($"button.BindingContext is null: {button.BindingContext == null}");
                System.Diagnostics.Debug.WriteLine($"button.BindingContext type: {button.BindingContext?.GetType()}");
                
                if (button.BindingContext == null)
                {
                    await DisplayAlert("Ошибка", "BindingContext is NULL!", "ОК");
                    return;
                }
                
                if (button.BindingContext is SupplyDto supply)
                {
                    System.Diagnostics.Debug.WriteLine($"Supply: Id={supply.Id}, Code={supply.Code}, RegisterType={supply.RegisterType}");
                    
                    if (supply.RegisterType == "IM40")
                    {
                        await DisplayAlert("Внимание", "Поставки в IM-40 нельзя редактировать", "ОК");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Opening editor for supply {supply.Id}");
                    
                    var editPage = App.Current.Handler.MauiContext.Services.GetService<SupplyEditPage>();
                    
                    if (editPage != null)
                    {
                        if (editPage.BindingContext is SupplyEditViewModel vm)
                        {
                            var queryParams = new Dictionary<string, object>
                            {
                                ["supplyId"] = supply.Id.ToString()
                            };
                            vm.ApplyQueryAttributes(queryParams);
                        }
                        
                        await Navigation.PushAsync(editPage);
                        System.Diagnostics.Debug.WriteLine("Navigation completed");
                    }
                    else
                    {
                        await DisplayAlert("Ошибка", "Не удалось создать страницу редактирования", "ОК");
                    }
                }
                else
                {
                    await DisplayAlert("Ошибка", $"BindingContext is not SupplyDto! Type: {button.BindingContext.GetType()}", "ОК");
                }
            }
            else
            {
                await DisplayAlert("Ошибка", $"sender is not Button! Type: {sender.GetType()}", "ОК");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnEditSupplyClicked error: {ex}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            await DisplayAlert("Ошибка", $"Не удалось открыть редактор:\n\n{ex.Message}\n\n{ex.StackTrace}", "ОК");
        }
    }
    
    private async void OnDeleteSupplyClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Microsoft.Maui.Controls.Button button && button.BindingContext is SupplyDto supply)
            {
                System.Diagnostics.Debug.WriteLine($"=== OnDeleteSupplyClicked: {supply.Code}");
                
                var confirm = await DisplayAlert(
                    "Подтверждение",
                    $"Удалить поставку {supply.Code}?",
                    "Да",
                    "Нет");
                
                if (!confirm) return;
                
                await _suppliesService.DeleteSupplyAsync(supply.Id);
                
                if (BindingContext is SuppliesViewModel vm)
                {
                    if (supply.RegisterType == "ND40")
                    {
                        vm.Nd40Supplies.Remove(supply);
                    }
                    else
                    {
                        vm.Im40Supplies.Remove(supply);
                    }
                }
                
                await DisplayAlert("✅ Удалено", $"Поставка {supply.Code} удалена", "ОК");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnDeleteSupplyClicked error: {ex}");
            await DisplayAlert("Ошибка", $"Не удалось удалить: {ex.Message}", "ОК");
        }
    }
    
    private async void OnTransferToIm40Clicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Microsoft.Maui.Controls.Button button && button.BindingContext is SupplyDto supply)
            {
                System.Diagnostics.Debug.WriteLine($"=== OnTransferToIm40Clicked: {supply.Code}");
                
                var confirm = await DisplayAlert(
                    "Перевести в IM-40?",
                    $"Перевести поставку {supply.Code} в регистр IM-40?",
                    "Да",
                    "Нет");
                
                if (!confirm) return;
                
                await _suppliesService.TransferToIm40Async(supply.Id);
                
                if (BindingContext is SuppliesViewModel vm)
                {
                    vm.Nd40Supplies.Remove(supply);
                    supply.RegisterType = "IM40";
                    vm.Im40Supplies.Insert(0, supply);
                }
                
                await DisplayAlert("✅ Переведено", $"Поставка {supply.Code} переведена в IM-40", "ОК");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnTransferToIm40Clicked error: {ex}");
            await DisplayAlert("Ошибка", $"Не удалось перевести: {ex.Message}", "ОК");
        }
    }
    
    private async void OnOpenCostingClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Microsoft.Maui.Controls.Button button && button.BindingContext is SupplyDto supply)
            {
                System.Diagnostics.Debug.WriteLine($"=== OnOpenCostingClicked: {supply.Code}");
                await Shell.Current.GoToAsync($"costing?supplyId={supply.Id}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnOpenCostingClicked error: {ex}");
            await DisplayAlert("Ошибка", $"Не удалось открыть: {ex.Message}", "ОК");
        }
    }
}
