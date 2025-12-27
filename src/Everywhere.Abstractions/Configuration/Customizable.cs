using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Everywhere.Configuration;

/// <summary>
/// This class is used to wrap a customizable property.
/// </summary>
/// <typeparam name="T"></typeparam>
public partial class Customizable<T> : ObservableObject where T : notnull
{
    public required T DefaultValue
    {
        get;
        set
        {
            if (_isDefaultValueReadonly) return;
            if (!SetProperty(ref field, value)) return;

            OnPropertyChanged(nameof(ActualValue));
            OnPropertyChanged(nameof(BindableValue));
        }
    }

    /// <summary>
    /// If T is a value type, T? will not be a nullable type.
    /// So we can only use object? to allow null values.
    /// </summary>
    [IgnoreDataMember]
    public object? CustomValue
    {
        get;
        set
        {
            if (value is not null && value is not T)
            {
                try
                {
                    // When setting from JSON deserialization, the value may be of a different type.
                    // Try to convert it to the correct type.
                    if (typeof(T).IsEnum)
                    {
                        // Enum is serialized as string (int or name)
                        if (value is string enumString)
                        {
                            if (int.TryParse(enumString, out var enumValue))
                            {
                                value = Enum.ToObject(typeof(T), enumValue);
                            }
                            else
                            {
                                value = Enum.Parse(typeof(T), enumString, true);
                            }
                        }
                        else
                        {
                            value = Enum.ToObject(typeof(T), Convert.ToInt32(value));
                        }
                    }
                    else if (value is JsonElement element)
                    {
                        value = element.Deserialize<T>();
                    }
                    else
                    {
                        value = (T)Convert.ChangeType(value, typeof(T));
                    }
                }
                catch
                {
                    value = default(T);
                }
            }

            if (!SetProperty(ref field, value)) return;

            OnPropertyChanged(nameof(ActualValue));
            OnPropertyChanged(nameof(BindableValue));
        }
    }

    [JsonIgnore]
    public bool IsCustomValueSet => CustomValue is not null && !EqualityComparer<T>.Default.Equals((T)CustomValue, DefaultValue);

    [JsonIgnore]
    public T ActualValue => CustomValue is T value ? value : DefaultValue;

    [JsonIgnore]
    public T? BindableValue
    {
        get => CustomValue is null ? typeof(T).IsClass ? default : DefaultValue : (T?)CustomValue;
        set
        {
            if (value is string { Length: 0 }) value = default; // Treat empty string as null for string types

            if (EqualityComparer<T>.Default.Equals(ActualValue, value)) return;

            CustomValue = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Indicates whether the default value is read-only. Which means the CustomValue cannot be set after construction.
    /// </summary>
    private readonly bool _isDefaultValueReadonly;

    [JsonConstructor]
    public Customizable() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Customizable{T}"/> class.
    /// </summary>
    /// <param name="defaultValue"></param>
    /// <param name="customValue"></param>
    /// <param name="isDefaultValueReadonly"></param>
    [SetsRequiredMembers]
    public Customizable(T defaultValue, T? customValue = default, bool isDefaultValueReadonly = false) : this()
    {
        DefaultValue = defaultValue;
        CustomValue = customValue;
        _isDefaultValueReadonly = isDefaultValueReadonly;
    }

    [RelayCommand]
    [property: JsonIgnore]
    [property: IgnoreDataMember]
    private void Reset()
    {
        CustomValue = null;
    }

    public static implicit operator Customizable<T>(T value) => new() { DefaultValue = value };

    public static implicit operator T(Customizable<T> customizable) => customizable.ActualValue;

    public override string? ToString() => ActualValue.ToString();
}