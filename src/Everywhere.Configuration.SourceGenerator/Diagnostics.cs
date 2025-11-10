using Microsoft.CodeAnalysis;

namespace Everywhere.Configuration.SourceGenerator;

internal static class Diagnostics
{
    private const string Category = $"{nameof(Everywhere)}.{nameof(Configuration)}.{nameof(SourceGenerator)}";

    public static readonly DiagnosticDescriptor MustBePartial = new(
        id: "STG001",
        title: "Target class must be partial",
        messageFormat: "Class '{0}' is marked with [GeneratedSettingsItems] but is not partial.",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyHeaderKey = new(
        id: "STG002",
        title: "Empty header key",
        messageFormat: "Property '{0}' has no [DynamicResourceKey] or an empty header key in [DynamicResourceKey].",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedType = new(
        id: "STG003",
        title: "Unsupported settings type",
        messageFormat: "Type '{0}' is not recognized; generator will emit runtime template discovery (SettingsTypedItem.TryCreate).",
        category: Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}