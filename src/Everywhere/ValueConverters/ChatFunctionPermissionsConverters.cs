﻿using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Everywhere.Chat.Plugins;

namespace Everywhere.ValueConverters;

public static class ChatFunctionPermissionsConverters
{
    /// <summary>
    /// Splits ChatFunctionPermissions flags into a list of permissions.
    /// </summary>
    public static IValueConverter ToList { get; } = new ToListImpl();

    /// <summary>
    /// Converts ChatFunctionPermissions to a brush representing the highest level of permission.
    /// </summary>
    public static IValueConverter ToBrush { get; } = new ToBrushImpl();

    private class ToListImpl : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not ChatFunctionPermissions permissions) return null;

            var permissionList = new List<ChatFunctionPermissions>();
            if (permissions.HasFlag(ChatFunctionPermissions.NetworkAccess)) permissionList.Add(ChatFunctionPermissions.NetworkAccess);
            if (permissions.HasFlag(ChatFunctionPermissions.ClipboardAccess)) permissionList.Add(ChatFunctionPermissions.ClipboardAccess);
            else if (permissions.HasFlag(ChatFunctionPermissions.ClipboardRead)) permissionList.Add(ChatFunctionPermissions.ClipboardRead);
            if (permissions.HasFlag(ChatFunctionPermissions.ScreenAccess)) permissionList.Add(ChatFunctionPermissions.ScreenAccess);
            else if (permissions.HasFlag(ChatFunctionPermissions.ScreenRead)) permissionList.Add(ChatFunctionPermissions.ScreenRead);
            if (permissions.HasFlag(ChatFunctionPermissions.FileAccess)) permissionList.Add(ChatFunctionPermissions.FileAccess);
            else if (permissions.HasFlag(ChatFunctionPermissions.FileRead)) permissionList.Add(ChatFunctionPermissions.FileRead);
            if (permissions.HasFlag(ChatFunctionPermissions.ProcessAccess)) permissionList.Add(ChatFunctionPermissions.ProcessAccess);
            if (permissions.HasFlag(ChatFunctionPermissions.ShellExecute)) permissionList.Add(ChatFunctionPermissions.ShellExecute);
            return permissionList;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    private class ToBrushImpl : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not ChatFunctionPermissions permissions) return null;

            // Define brushes for different permission levels
            var brushNone = Brushes.Gray;
            var brushLow = Brushes.Green;
            var brushMedium = Brushes.DarkOrange;
            var brushHigh = Brushes.DarkRed;

            if (permissions == ChatFunctionPermissions.None)
                return brushNone;
            if (permissions.HasFlag(ChatFunctionPermissions.ProcessAccess) || permissions.HasFlag(ChatFunctionPermissions.ShellExecute))
                return brushHigh;
            if (permissions.HasFlag(ChatFunctionPermissions.FileAccess) || permissions.HasFlag(ChatFunctionPermissions.ClipboardAccess) ||
                permissions.HasFlag(ChatFunctionPermissions.ScreenAccess))
                return brushMedium;
            if (permissions.HasFlag(ChatFunctionPermissions.FileRead) || permissions.HasFlag(ChatFunctionPermissions.ClipboardRead) ||
                permissions.HasFlag(ChatFunctionPermissions.ScreenRead) || permissions.HasFlag(ChatFunctionPermissions.NetworkAccess))
                return brushLow;

            return brushNone; // Fallback to gray if no known permissions are set
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}