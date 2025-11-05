using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using Everywhere.Common;

namespace Everywhere.Configuration;

/// <summary>
/// Message indicating that the network proxy has changed.
/// </summary>
/// <param name="Proxy"></param>
public record NetworkProxyChangedMessage(IWebProxy Proxy);

/// <summary>
/// An IWebProxy implementation that can be updated at runtime.
/// It should be registered as a singleton in the dependency injection container.
/// This class dynamically delegates proxy requests to an internal proxy instance,
/// which can be changed by calling ApplyProxySettings.
/// </summary>
public sealed class DynamicWebProxy : IWebProxy
{
    private readonly IWebProxy _systemHttpProxy = HttpClient.DefaultProxy;

    private static readonly string[] BypassSeparators =
    [
        "\r\n", "\n", "\r", ";", ","
    ];

    private IWebProxy _currentProxy;

    public DynamicWebProxy()
    {
        _currentProxy = _systemHttpProxy;
    }

    public ICredentials? Credentials
    {
        get => _currentProxy.Credentials;
        set
        {
            if (_currentProxy.Credentials != value)
            {
                _currentProxy.Credentials = value;
            }
        }
    }

    public Uri? GetProxy(Uri destination) => _currentProxy.GetProxy(destination);

    public bool IsBypassed(Uri host) => _currentProxy.IsBypassed(host);

    /// <summary>
    /// Applies the given proxy settings.
    /// </summary>
    /// <param name="settings">The proxy settings to apply.</param>
    /// <exception cref="HandledException"></exception>
    public void ApplyProxySettings(ProxySettings settings)
    {
        IWebProxy newProxy;
        if (!settings.IsEnabled)
        {
            newProxy = _systemHttpProxy;
        }
        else
        {
            var addressToUse = settings.Endpoint.ActualValue.Trim();
            if (string.IsNullOrWhiteSpace(addressToUse))
            {
                throw new HandledException(
                    new InvalidOperationException("Proxy server address is required."),
                    new DirectResourceKey("Proxy server address is required.")); // TODO: I18N
            }

            newProxy = CreateProxy(settings, addressToUse);
        }

        _currentProxy = newProxy;
        WeakReferenceMessenger.Default.Send(new NetworkProxyChangedMessage(_currentProxy));
    }

    private static WebProxy CreateProxy(ProxySettings settings, string address)
    {
        var normalizedAddress = NormalizeAddress(address);
        if (!Uri.TryCreate(normalizedAddress, UriKind.Absolute, out var proxyUri))
        {
            throw new HandledException(
                new InvalidOperationException("Proxy server address is invalid."),
                new DirectResourceKey("Proxy server address is invalid.")); // TODO: I18N
        }

        if (string.IsNullOrWhiteSpace(proxyUri.Host))
        {
            throw new HandledException(
                new InvalidOperationException("Proxy host is required."),
                new DirectResourceKey("Proxy host is required.")); // TODO: I18N
        }

        if (proxyUri.Scheme is not "http" and not "https" and not "socks5")
        {
            throw new HandledException(
                new NotSupportedException($"Proxy scheme '{proxyUri.Scheme}' is not supported."),
                new DirectResourceKey($"Proxy scheme '{proxyUri.Scheme}' is not supported.")); // TODO: I18N
        }

        var proxy = new WebProxy(proxyUri)
        {
            BypassProxyOnLocal = settings.BypassOnLocal,
            UseDefaultCredentials = false,
            BypassList = ParseBypassList(settings.BypassList),
        };

        if (settings.UseAuthentication && !string.IsNullOrWhiteSpace(settings.Username))
        {
            proxy.Credentials = new NetworkCredential(settings.Username.Trim(), settings.Password ?? string.Empty);
        }
        else
        {
            proxy.Credentials = null;
        }

        return proxy;
    }

    private static string NormalizeAddress(string address)
    {
        address = address.Trim();

        if (!address.Contains("://", StringComparison.Ordinal))
        {
            address = $"http://{address}";
        }

        return address;
    }

    private static string[] ParseBypassList(string? bypassList)
    {
        if (string.IsNullOrWhiteSpace(bypassList)) return [];

        return bypassList
            .Split(BypassSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}