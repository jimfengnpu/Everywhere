using Microsoft.CodeAnalysis;

namespace Everywhere.Configuration.SourceGenerator;

internal static class Diagnostics
{
    private const string Category = $"{nameof(Everywhere)}.{nameof(Configuration)}.{nameof(SourceGenerator)}";

    public static readonly DiagnosticDescriptor MustBePartial = new(
        id: "STG001",
        title: "Target class must be partial",
        messageFormat: "Class '{0}' is marked with [GeneratedSettingsItems] but is not partial",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyHeaderKey = new(
        id: "STG002",
        title: "Empty header key",
        messageFormat: "Property '{0}' has no [DynamicResourceKey] or an empty header key in [DynamicResourceKey]",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NullableSettingsControl = new(
        id: "STG003",
        title: "Nullable SettingsControl",
        messageFormat: "Property '{0}' is of type SettingsControl<T>? but must be non-nullable",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingItemsSourceBindingPath = new(
        id: "STG004",
        title: "Missing ItemsSource Binding Path",
        messageFormat: "Property '{0}' is a collection but has no ItemsSourceBindingPath specified in [SettingsItemsSource]",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidItemsSourceBindingPath = new(
        id: "STG005",
        title: "Invalid ItemsSource Binding Path",
        messageFormat: "Property '{0}' has an invalid ItemsSourceBindingPath '{1}' specified in [SettingsItemsSource]",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}