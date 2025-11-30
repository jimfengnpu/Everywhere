using FlaUI.Core;
using FlaUI.Core.Patterns.Infrastructure;

namespace Everywhere.Windows.Interop;

public static class AutomationExtension
{
    /// <summary>
    /// Sometimes pattern.TryGetPattern() will throw an exception!?
    /// </summary>
    /// <param name="pattern"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T? TryGetPattern<T>(this IAutomationPattern<T> pattern) where T : class, IPattern
    {
        try
        {
            return pattern.PatternOrDefault;
        }
        catch
        {
            return null;
        }
    }
}