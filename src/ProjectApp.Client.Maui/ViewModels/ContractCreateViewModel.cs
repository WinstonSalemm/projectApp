using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractCreateViewModel : ObservableObject
{
    private readonly IContractsService _contracts;
    public event EventHandler<bool>? Created; // true on success, false on failure

    [ObservableProperty] private string orgName = string.Empty;
    [ObservableProperty] private string? inn;
    [ObservableProperty] private string? phone;
    [ObservableProperty] private string status = "Signed"; // Signed | Paid | Closed
    [ObservableProperty] private string? note;

    public List<string> Statuses { get; } = new() { "Signed", "Paid", "Closed" };

    // New item editor
    [ObservableProperty] private int? newProductId;
    [ObservableProperty] private string newName = string.Empty;
    [ObservableProperty] private string newUnit = "шт";
    [ObservableProperty] private decimal newQty = 1m;
    [ObservableProperty] private decimal newUnitPrice = 0m;

    // For UI display of stocks of currently selected product
    [ObservableProperty] private decimal selectedNd40Qty;
    [ObservableProperty] private decimal selectedIm40Qty;
    [ObservableProperty] private decimal selectedTotalQty;

    public ObservableCollection<ContractItemDraft> Items { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string editorMessage = string.Empty;

    public ContractCreateViewModel(IContractsService contracts)
    {
        _contracts = contracts;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (string.IsNullOrWhiteSpace(NewName) || NewQty <= 0 || NewUnitPrice <= 0)
        {
            EditorMessage = "Введите позицию (название, кол-во > 0 и цену > 0)";
            return;
        }
        Items.Add(new ContractItemDraft
        {
            ProductId = NewProductId,
            Name = NewName.Trim(),
            Unit = string.IsNullOrWhiteSpace(NewUnit) ? "шт" : NewUnit.Trim(),
            Qty = NewQty,
            UnitPrice = NewUnitPrice
        });
        NewProductId = null; NewName = string.Empty; NewUnit = "шт"; NewQty = 1m; NewUnitPrice = 0m;
        SelectedNd40Qty = 0; SelectedIm40Qty = 0; SelectedTotalQty = 0;
        EditorMessage = string.Empty;
    }

    [RelayCommand]
    private void RemoveItem(ContractItemDraft? item)
    {
        if (item is null) return;
        Items.Remove(item);
    }

    [RelayCommand]
    public async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(OrgName)) { StatusMessage = "Укажите организацию"; return; }
        if (Items.Count == 0) { StatusMessage = "Добавьте позиции"; return; }
        if (Items.Any(i => i.UnitPrice <= 0 || i.Qty <= 0 || string.IsNullOrWhiteSpace(i.Name)))
        {
            StatusMessage = "Исправьте позиции: название, кол-во > 0, цена > 0";
            return;
        }
        try
        {
            IsBusy = true; StatusMessage = string.Empty; EditorMessage = string.Empty;
            var draft = new ContractCreateDraft
            {
                OrgName = OrgName.Trim(),
                Inn = string.IsNullOrWhiteSpace(Inn) ? null : Inn!.Trim(),
                Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone!.Trim(),
                Status = Status,
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note,
                Items = Items.ToList()
            };
            var ok = await _contracts.CreateAsync(draft);
            StatusMessage = ok ? "Договор создан" : "Ошибка создания";
            if (ok)
            {
                OrgName = string.Empty; Inn = null; Phone = null; Note = null; Status = "Signed"; Items.Clear();
                Created?.Invoke(this, true);
                // Navigate to account selection
                try
                {
                    var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                    NavigationHelper.SetRoot(new NavigationPage(select));
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Created?.Invoke(this, false);
        }
        finally { IsBusy = false; }
    }
}


