using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Models;
using HealthHelper.Navigation;

namespace HealthHelper.ViewModels;

public partial class AdviceViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty] private string _metadata = string.Empty;
    [ObservableProperty] private string _narrativeSummary = string.Empty;

    public AdviceViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Load(DailySnapshot snapshot, AdviceBundle bundle)
    {
        NarrativeSummary = bundle.Narrative;
        Metadata = $"记录日期：{snapshot.Date:yyyy年M月d日}";
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }
}


