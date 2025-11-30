using System.Reflection;
using FlaUI.Core;
using FlaUI.Core.Identifiers;
using FlaUI.Core.Patterns;
using FlaUI.UIA3.Patterns;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;

namespace Everywhere.Windows.Interop;

public static class AutomationExtension
{
    private delegate object InternalGetPatternDelegate(FrameworkAutomationElementBase element, int patternId, bool cached);

    private readonly static InternalGetPatternDelegate InternalGetPatternMethod =
        typeof(FrameworkAutomationElementBase)
            .GetMethod("InternalGetPattern", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<InternalGetPatternDelegate>();

    extension(AutomationElement element)
    {
        public T? TryGetPattern<T>(PatternId pattern) where T : class
        {
            try
            {
                return InternalGetPatternMethod(element.FrameworkAutomationElement, pattern.Id, false) as T;
            }
            catch
            {
                return null;
            }
        }

        public IValuePattern? TryGetValuePattern() =>
            TryGetPattern<IValuePattern>(element, ValuePattern.Pattern);

        public ITextPattern? TryGetTextPattern() =>
            TryGetPattern<ITextPattern>(element, TextPattern.Pattern);

        public IInvokePattern? TryGetInvokePattern() =>
            TryGetPattern<IInvokePattern>(element, InvokePattern.Pattern);

        public ITogglePattern? TryGetTogglePattern() =>
            TryGetPattern<ITogglePattern>(element, TogglePattern.Pattern);

        public IExpandCollapsePattern? TryGetExpandCollapsePattern() =>
            TryGetPattern<IExpandCollapsePattern>(element, ExpandCollapsePattern.Pattern);

        public ISelectionItemPattern? TryGetSelectionItemPattern() =>
            TryGetPattern<ISelectionItemPattern>(element, SelectionItemPattern.Pattern);

        public ILegacyIAccessiblePattern? TryGetLegacyIAccessiblePattern() =>
            TryGetPattern<ILegacyIAccessiblePattern>(element, LegacyIAccessiblePattern.Pattern);
    }
}