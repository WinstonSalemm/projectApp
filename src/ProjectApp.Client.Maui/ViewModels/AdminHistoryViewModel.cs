using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class AdminHistoryViewModel : ObservableObject
{
    public string PageTitle { get; } = "История";

    public string PlaceholderDescription { get; } =
        "Раздел подготовки. Здесь появится полная история операций после уточнения требований.";
}
