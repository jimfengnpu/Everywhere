using System.Diagnostics;
using System.Security.Principal;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Everywhere.Common;
using Everywhere.Extensions;
using Everywhere.Interop;
using Microsoft.Win32;
using Vector = Avalonia.Vector;

namespace Everywhere.Windows.Interop;

public class NativeHelper : INativeHelper
{
    private const string AppName = nameof(Everywhere);
    private const string RegistryInstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{D66EA41B-8DEB-4E5A-9D32-AB4F8305F664}}_is1";
    private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static string ProcessPathWithArgument => $"\"{Environment.ProcessPath}\" --autorun";

    public bool IsInstalled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryInstallKey);
            return key?.GetValue("InstallLocation")?.ToString() is not null;
        }
    }

    public bool IsAdministrator
    {
        get
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool IsUserStartupEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                // If the registry key cannot be accessed, assume it is not enabled.
                return false;
            }
        }
        set
        {
            if (value)
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                key?.SetValue(AppName, ProcessPathWithArgument);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                key?.DeleteValue(AppName, false);
            }
        }
    }

    public bool IsAdministratorStartupEnabled
    {
        get
        {
            try
            {
                return TaskSchedulerHelper.IsTaskScheduled(AppName);
            }
            catch
            {
                return false;
            }
        }
        set
        {
            if (!IsAdministrator) throw new UnauthorizedAccessException("The current user is not an administrator.");

            if (value)
            {
                TaskSchedulerHelper.CreateScheduledTask(AppName, ProcessPathWithArgument);
            }
            else
            {
                TaskSchedulerHelper.DeleteScheduledTask(AppName);
            }
        }
    }

    public void RestartAsAdministrator()
    {
        if (IsAdministrator)
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath.NotNull(),
            Arguments = "--ui",
            UseShellExecute = true,
            Verb = "runas" // This will prompt for elevation
        };

        Entrance.ReleaseMutex();
        Process.Start(startInfo);
        Environment.Exit(0); // Exit the current process
    }

    public void ShowDesktopNotification(string message, string? title)
    {
        var registryKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId");
        const string ModelId = "{D66EA41B-8DEB-4E5A-9D32-AB4F8305F664}/Everywhere";
        var tempFilePath = Path.Combine(Path.GetTempPath(), "D66EA41B-8DEB-4E5A-9D32-AB4F8305F664-Everywhere.ico");

        using (var subKey = registryKey.CreateSubKey(ModelId))
        {
            subKey.SetValue("DisplayName", "Everywhere");

            var iconResource = AssetLoader.Open(new Uri("avares://Everywhere/Assets/Everywhere.ico"));
            using (var fs = File.Create(tempFilePath))
            {
                iconResource.CopyTo(fs);
            }

            subKey.SetValue("IconUri", tempFilePath);
        }

        var xml =
            $"""
             <toast launch='conversationId=9813'>
                 <visual>
                     <binding template='ToastGeneric'>
                         {(string.IsNullOrEmpty(title) ? "" : $"<text>{title}</text>")}
                         <text>{message}</text>
                     </binding>
                 </visual>
             </toast>
             """;
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(xml);

        var toast = new ToastNotification(xmlDocument);
        ToastNotificationManager.CreateToastNotifier(ModelId).Show(toast);

        toast.Dismissed += delegate
        {
            try
            {
                registryKey.DeleteSubKey(ModelId);
                registryKey.Dispose();
                File.Delete(tempFilePath);
            }
            catch
            {
                // ignore
            }
        };
    }

    public void OpenFileLocation(string fullPath)
    {
        if (fullPath.IsNullOrWhiteSpace()) return;
        var args = $"/e,/select,\"{fullPath}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
    }
}