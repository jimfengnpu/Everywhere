using Avalonia.Threading;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Utilities;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Everywhere.Initialization;

/// <summary>
/// Applies the configured network proxy to global HTTP handlers and keeps it in sync with user settings.
/// </summary>
public sealed class ProxySettingsInitializer : IAsyncInitializer
{
    public AsyncInitializerPriority Priority => AsyncInitializerPriority.AfterSettings;

    private readonly ProxySettings _proxySettings;
    private readonly ILogger<ProxySettingsInitializer> _logger;
    private readonly DebounceExecutor<ProxySettingsInitializer, ThreadingTimerImpl> _applyProxyDebounceExecutor;

    public ProxySettingsInitializer(Settings settings, ILogger<ProxySettingsInitializer> logger)
    {
        _proxySettings = settings.Common.Proxy;
        _logger = logger;
        _applyProxyDebounceExecutor = new DebounceExecutor<ProxySettingsInitializer, ThreadingTimerImpl>(
            () => this,
            static that => that.ApplyProxySettings(true),
            TimeSpan.FromSeconds(0.5));
    }

    public Task InitializeAsync()
    {
        ApplyProxySettings(false);

        // Use ObjectObserver to observe property changes (including nested properties, e.g. Customizable).
        new ObjectObserver(HandleProxySettingsChanged).Observe(_proxySettings);

        return Task.CompletedTask;
    }

    private void HandleProxySettingsChanged(in ObjectObserverChangedEventArgs e)
    {
        _applyProxyDebounceExecutor.Trigger();
    }

    private void ApplyProxySettings(bool notifyOnError)
    {
        try
        {
            NetworkProxyManager.ApplyProxySettings(_proxySettings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to apply proxy settings: {Message}", ex);

            if (notifyOnError)
            {
                Dispatcher.UIThread.InvokeOnDemand(() =>
                {
                    ServiceLocator
                        .Resolve<ToastManager>()
                        .CreateToast(LocaleKey.Common_Error.I18N())
                        .WithContent(ex.GetFriendlyMessage().ToTextBlock())
                        .DismissOnClick()
                        .OnBottomRight()
                        .ShowError();
                });
            }
        }
    }
}