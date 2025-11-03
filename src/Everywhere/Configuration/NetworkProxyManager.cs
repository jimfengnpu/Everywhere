using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using Everywhere.Common;

namespace Everywhere.Configuration;

/// <summary>
/// Message indicating that the network proxy has changed.
/// </summary>
/// <param name="Proxy"></param>
public record NetworkProxyChangedMessage(IWebProxy Proxy);

public static class NetworkProxyManager
{
    private static readonly Lock SyncRoot = new();
    private static readonly IWebProxy SystemHttpProxy = HttpClient.DefaultProxy;
    private static readonly IWebProxy? SystemWebRequestProxy = WebRequest.DefaultWebProxy;

    public static IWebProxy CurrentProxy
    {
        get;
        private set
        {
            field = value;
            WeakReferenceMessenger.Default.Send(new NetworkProxyChangedMessage(CurrentProxy));
        }
    } = HttpClient.DefaultProxy;

    private static readonly string[] BypassSeparators =
    [
        "\r\n",
        "\n",
        "\r",
        ";",
        ","
    ];

    /// <summary>
    /// Applies the given proxy settings to global HTTP handlers.
    /// </summary>
    /// <param name="settings"></param>
    /// <exception cref="HandledException"></exception>
    public static void ApplyProxySettings(ProxySettings settings)
    {
        lock (SyncRoot)
        {
            if (!settings.IsEnabled)
            {
                HttpClient.DefaultProxy = SystemHttpProxy;
                WebRequest.DefaultWebProxy = SystemWebRequestProxy;
                CurrentProxy = SystemHttpProxy;
                return ;
            }

            var addressToUse = settings.Endpoint.ActualValue.Trim();
            if (string.IsNullOrWhiteSpace(addressToUse))
            {
                throw new HandledException(
                    new InvalidOperationException("Proxy server address is required."),
                    new DirectResourceKey("Proxy server address is required.")); // TODO: I18N
            }

            var proxy = CreateProxy(settings, addressToUse);
            HttpClient.DefaultProxy = proxy;
            WebRequest.DefaultWebProxy = proxy;
            CurrentProxy = proxy;
        }
    }

    /// <summary>
    /// Creates a <see cref="WebProxy"/> instance from the given settings and address. Throws an exception if the settings are invalid.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="address"></param>
    /// <returns></returns>
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

        if (!settings.UseAuthentication)
        {
            proxy.Credentials = null;
            return proxy;
        }

        if (string.IsNullOrWhiteSpace(settings.Username))
        {
            throw new HandledException(
                new InvalidOperationException("Proxy username is required when authentication is enabled."),
                new DirectResourceKey("Proxy username is required when authentication is enabled.")); // TODO: I18N
        }

        proxy.Credentials = new NetworkCredential(settings.Username.Trim(), settings.Password ?? string.Empty);
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