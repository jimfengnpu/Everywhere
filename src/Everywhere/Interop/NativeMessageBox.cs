using Avalonia.Controls;
using Everywhere.Common;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Everywhere.Interop;

public static partial class NativeMessageBox
{
    /// <summary>
    /// Gets the global exception handler which shows a message box when an exception occurs.
    /// </summary>
    public static IExceptionHandler ExceptionHandler { get; } = new ExceptionHandlerImpl();

    private class ExceptionHandlerImpl : IExceptionHandler
    {
        public void HandleException(Exception exception, string? message = null, object? source = null, int lineNumber = 0)
        {
            ShowAsync(
                $"Error at [{source}:{lineNumber}]",
                $"{message ?? "An error occurred."}\n\n{exception.GetFriendlyMessage()}",
                ButtonEnum.Ok,
                Icon.Error);
        }
    }

    public static Task<ButtonResult> ShowAsync(
        string title,
        string message,
        ButtonEnum buttons = ButtonEnum.Ok,
        Icon icon = Icon.None,
        WindowStartupLocation startupLocation = WindowStartupLocation.CenterScreen) =>
        MessageBoxManager.GetMessageBoxStandard(title, message, buttons, icon, startupLocation).ShowAsync();
}