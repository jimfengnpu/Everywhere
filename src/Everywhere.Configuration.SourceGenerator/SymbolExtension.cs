using Microsoft.CodeAnalysis;

namespace Everywhere.Configuration.SourceGenerator;

internal static class SymbolExtension
{
    /// <summary>
    /// Safe way to read modifiers across C# versions
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static IEnumerable<SyntaxToken> Modifiers(this SyntaxNode node)
    {
        var prop = node.GetType().GetProperty("Modifiers");
        if (prop?.GetValue(node) is SyntaxTokenList list) return list;
        return [];
    }

    extension(ISymbol symbol)
    {
        public bool HasAttribute(string fullMetadataName)
            => symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == fullMetadataName);

        public AttributeData? GetAttribute(string fullMetadataName)
            => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == fullMetadataName);

        public bool IsHiddenItem() => symbol.HasAttribute(KnownAttributes.HiddenSettingsItem);
    }

    extension(INamedTypeSymbol type)
    {
        public string GetNamespace()
        {
            var ns = type.ContainingNamespace;
            return ns == null || ns.IsGlobalNamespace ? string.Empty : ns.ToDisplayString();
        }

        public bool IsSettingsCategory()
            => type.BaseType?.ToDisplayString() == "Everywhere.Configuration.SettingsCategory";

        public bool IsPartial() =>
            type.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .Any(syntax => syntax?.Modifiers().Any(m => m.Text == "partial") == true);
    }

    public static TypedConstant? GetNamedArgument(this AttributeData attribute, string key)
    {
        var arg = attribute.NamedArguments.FirstOrDefault(na => na.Key == key);
        return arg.Key is null ? null : arg.Value;
    }
}
