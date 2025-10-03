using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractEditViewModel : ObservableObject
{
    private readonly IContractsService _contracts;
    private int _id;

    [ObservableProperty] private string orgName = string.Empty;
    [ObservableProperty] private string? inn;
    [ObservableProperty] private string? phone;
    [ObservableProperty] private string status = "Signed";
    [ObservableProperty] private string? note;

    public List<string> Statuses { get; } = new() { "Signed", "Paid", "Closed" };

    // Item editor
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

    public ContractEditViewModel(IContractsService contracts)
    {
        _contracts = contracts;
    }

    public async Task LoadAsync(int id)
    {
        _id = id;
        IsBusy = true; StatusMessage = string.Empty; EditorMessage = string.Empty; Items.Clear();
        var dto = await _contracts.GetAsync(id);
        if (dto != null)
        {
            OrgName = dto.OrgName; Inn = dto.Inn; Phone = dto.Phone; Status = dto.Status; Note = dto.Note; 
            foreach (var it in dto.Items) Items.Add(it);
        }
        IsBusy = false;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (string.IsNullOrWhiteSpace(NewName) || NewQty <= 0 || NewUnitPrice <= 0)
        { EditorMessage = "Введите позицию (название, кол-во > 0 и цену > 0)"; return; }
        Items.Add(new ContractItemDraft
        {
            ProductId = NewProductId,
            Name = NewName.Trim(),
            Unit = string.IsNullOrWhiteSpace(NewUnit) ? "шт" : NewUnit.Trim(),
            Qty = NewQty,
            UnitPrice = NewUnitPrice
        });
        NewProductId = null; NewName = string.Empty; NewUnit = "шт"; NewQty = 1m; NewUnitPrice = 0m; EditorMessage = string.Empty;
        SelectedNd40Qty = 0; SelectedIm40Qty = 0; SelectedTotalQty = 0;
    }

    [RelayCommand]
    private void RemoveItem(ContractItemDraft? item)
    {
        if (item is null) return; Items.Remove(item);
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(OrgName)) { StatusMessage = "Укажите организацию"; return; }
        if (Items.Count == 0) { StatusMessage = "Добавьте позиции"; return; }
        if (Items.Any(i => i.UnitPrice <= 0 || i.Qty <= 0 || string.IsNullOrWhiteSpace(i.Name)))
        { StatusMessage = "Исправьте позиции: название, кол-во > 0, цена > 0"; return; }

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
            var ok = await _contracts.UpdateAsync(_id, draft);
            StatusMessage = ok ? "Сохранено" : "Ошибка сохранения";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }
}
