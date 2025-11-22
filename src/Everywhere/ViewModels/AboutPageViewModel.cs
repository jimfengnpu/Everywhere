using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Everywhere.Views;
using Everywhere.Views.Pages;

namespace Everywhere.ViewModels;

public partial class AboutPageViewModel : ReactiveViewModelBase
{
    public static string Version => typeof(AboutPage).Assembly.GetName().Version?.ToString() ?? "Unknown Version";

    [RelayCommand]
    private void OpenWelcomeDialog()
    {
        DialogManager
            .CreateCustomDialog(ServiceLocator.Resolve<WelcomeView>())
            .ShowAsync();
    }

    [RelayCommand]
    private void OpenChangeLogDialog()
    {
        DialogManager
            .CreateCustomDialog(ServiceLocator.Resolve<ChangeLogView>())
            .Dismissible()
            .ShowAsync();
    }
}