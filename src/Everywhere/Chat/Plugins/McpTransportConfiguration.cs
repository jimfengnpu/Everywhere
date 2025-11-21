using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;

namespace Everywhere.Chat.Plugins;

[JsonPolymorphic]
[JsonDerivedType(typeof(StdioMcpTransportConfiguration), "stdio")]
[JsonDerivedType(typeof(HttpMcpTransportConfiguration), "sse")]
public abstract partial class McpTransportConfiguration : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(McpTransportConfiguration), nameof(ValidateName))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Description { get; set; }

    /// <summary>
    /// Validates the entire configuration. returns true if valid.
    /// </summary>
    /// <returns></returns>
    public bool Validate()
    {
        ValidateAllProperties();
        return !HasErrors;
    }

    /// <summary>
    /// Validates the Name property. Used for CustomValidation attribute.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static ValidationResult? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ValidationResult(LocaleResolver.ValidationErrorMessage_Required);
        }

        if (name.Length > 50)
        {
            return new ValidationResult(string.Format(LocaleResolver.ValidationErrorMessage_MaxLength, 50));
        }

        return ValidationResult.Success;
    }
}

public sealed partial class StdioMcpTransportConfiguration : McpTransportConfiguration
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessageResourceType = typeof(LocaleResolver), ErrorMessageResourceName = LocaleKey.ValidationErrorMessage_Required)]
    public partial string Command { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Arguments { get; set; }

    [ObservableProperty]
    public partial string? WorkingDirectory { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(StdioMcpTransportConfiguration), nameof(ValidateEnvironmentVariables))]
    public partial ObservableCollection<ObservableKeyValuePair<string, string?>>? EnvironmentVariables { get; set; } = [];

    [RelayCommand]
    private void AddEmptyEnvironmentVariable() => EnvironmentVariables?.Add(new ObservableKeyValuePair<string, string?>(string.Empty, null));

    [RelayCommand]
    private void RemoveEnvironmentVariable(ObservableKeyValuePair<string, string?> item) => EnvironmentVariables?.Remove(item);

    public static ValidationResult? ValidateEnvironmentVariables(ObservableCollection<ObservableKeyValuePair<string, string?>>? envVars)
    {
        if (envVars is null) return ValidationResult.Success;

        var keys = new HashSet<string?>();
        foreach (var kvp in envVars)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                return new ValidationResult(LocaleResolver.ValidationErrorMessage_NullKey);
            }

            if (!keys.Add(kvp.Key))
            {
                return new ValidationResult(LocaleResolver.ValidationErrorMessage_DuplicateKey);
            }
        }

        return ValidationResult.Success;
    }
}

public sealed partial class HttpMcpTransportConfiguration : McpTransportConfiguration
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Url(ErrorMessageResourceType = typeof(LocaleResolver), ErrorMessageResourceName = LocaleKey.ValidationErrorMessage_Url)]
    [Required(ErrorMessageResourceType = typeof(LocaleResolver), ErrorMessageResourceName = LocaleKey.ValidationErrorMessage_Required)]
    public partial string Endpoint { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(HttpMcpTransportConfiguration), nameof(ValidateHeaders))]
    public partial ObservableCollection<ObservableKeyValuePair<string, string>>? Headers { get; set; } = [];

    [RelayCommand]
    private void AddEmptyHeader() => Headers?.Add(new ObservableKeyValuePair<string, string>(string.Empty, string.Empty));

    [RelayCommand]
    private void RemoveHeader(ObservableKeyValuePair<string, string> item) => Headers?.Remove(item);

    public static ValidationResult? ValidateHeaders(ObservableCollection<ObservableKeyValuePair<string, string>>? headers)
    {
        if (headers is null) return ValidationResult.Success;

        var keys = new HashSet<string?>();
        foreach (var kvp in headers)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                return new ValidationResult(LocaleResolver.ValidationErrorMessage_NullKey);
            }

            if (string.IsNullOrWhiteSpace(kvp.Value))
            {
                return new ValidationResult(LocaleResolver.ValidationErrorMessage_NullValue);
            }

            if (!keys.Add(kvp.Key))
            {
                return new ValidationResult(LocaleResolver.ValidationErrorMessage_DuplicateKey);
            }
        }

        return ValidationResult.Success;
    }
}