using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Everywhere.Common;

/// <summary>
/// A simple wrapper class for binding purposes. Contains a single Value property.
/// Implements implicit conversion operators to and from T and JSON serialization support.
/// </summary>
/// <typeparam name="T"></typeparam>
[JsonConverter(typeof(BindingWrapperJsonConverterFactory))]
public partial class BindingWrapper<T> : ObservableObject
{
    [ObservableProperty]
    public required partial T Value { get; set; }

    public static implicit operator T(BindingWrapper<T> wrapper) => wrapper.Value;

    public static implicit operator BindingWrapper<T>(T value) => new() { Value = value };

    public override string? ToString() => Value?.ToString();

    public BindingWrapper() { }

    [JsonConstructor]
    [SetsRequiredMembers]
    public BindingWrapper(T value) => Value = value;
}

public sealed class BindingWrapperJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        return typeToConvert.GetGenericTypeDefinition() == typeof(BindingWrapper<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(BindingWrapperJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

public sealed class BindingWrapperJsonConverter<T> : JsonConverter<BindingWrapper<T>>
{
    public override BindingWrapper<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<T>(ref reader, options);
        return value == null ? null : new BindingWrapper<T> { Value = value };
    }

    public override void Write(Utf8JsonWriter writer, BindingWrapper<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }
}