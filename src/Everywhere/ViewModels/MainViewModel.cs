using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reflection;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Views;
using Lucide.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ShadUI;

namespace Everywhere.ViewModels;

public sealed partial class MainViewModel : ReactiveViewModelBase, IDisposable
{
    public ReadOnlyObservableCollection<SidebarItem> Pages { get; }

    [ObservableProperty] public partial SidebarItem? SelectedPage { get; set; }

    public Settings Settings { get; }

    private readonly SourceList<SidebarItem> _pagesSource = new();
    private readonly CompositeDisposable _disposables = new(2);

    private readonly IServiceProvider _serviceProvider;

    public MainViewModel(IServiceProvider serviceProvider, Settings settings)
    {
        _serviceProvider = serviceProvider;
        Settings = settings;

        Pages = _pagesSource
            .Connect()
            .ObserveOnDispatcher()
            .BindEx(_disposables);

        _disposables.Add(_pagesSource);
    }

    protected internal override Task ViewLoaded(CancellationToken cancellationToken)
    {
        if (_pagesSource.Count > 0) return base.ViewLoaded(cancellationToken);

        _pagesSource.AddRange(
            _serviceProvider
                .GetServices<IMainViewPageFactory>()
                .SelectMany(f => f.CreatePages())
                .Concat(_serviceProvider.GetServices<IMainViewPage>())
                .OrderBy(p => p.Index)
                .Select(p => new SidebarItem
                {
                    [ContentControl.ContentProperty] = new TextBlock
                    {
                        [!TextBlock.TextProperty] = p.Title.ToBinding()
                    },
                    [SidebarItem.RouteProperty] = p,
                    Icon = new LucideIcon { Kind = p.Icon, Size = 20 }
                }));
        SelectedPage = _pagesSource.Items.FirstOrDefault();

        ShowOobeDialogOnDemand();

        return base.ViewLoaded(cancellationToken);
    }

    /// <summary>
    /// Shows the OOBE dialog if the application is launched for the first time or after an update.
    /// </summary>
    private void ShowOobeDialogOnDemand()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (!Version.TryParse(Settings.Internal.PreviousLaunchVersion, out var previousLaunchVersion)) previousLaunchVersion = null;
        if (Settings.Model.CustomAssistants.Count == 0)
        {
            DialogManager
                .CreateCustomDialog(ServiceLocator.Resolve<WelcomeView>())
                .ShowAsync();
        }
        else if (previousLaunchVersion != version)
        {
            DialogManager
                .CreateCustomDialog(ServiceLocator.Resolve<ChangeLogView>())
                .Dismissible()
                .ShowAsync();
        }

        Settings.Internal.PreviousLaunchVersion = version?.ToString();
    }

    protected internal override Task ViewUnloaded()
    {
        ShowHideToTrayNotificationOnDemand();

        return base.ViewUnloaded();
    }

    private void ShowHideToTrayNotificationOnDemand()
    {
        if (!Settings.Internal.IsFirstTimeHideToTrayIcon) return;

        ServiceLocator.Resolve<INativeHelper>().ShowDesktopNotificationAsync(LocaleResolver.MainView_EverywhereHasMinimizedToTray);
        Settings.Internal.IsFirstTimeHideToTrayIcon = false;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}