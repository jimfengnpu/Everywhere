using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Everywhere.AttachedProperties;

/// <summary>
/// <see cref="Control.DataTemplates"/> says they are 'Gets or sets'.
/// But maybe I'm blind, I cannot see the set method.
/// So I can only use this AttachedProperty to set it.
/// </summary>
public class BindingAssist : AvaloniaObject
{
    public static readonly AttachedProperty<IEnumerable<IDataTemplate>> DataTemplatesProperty =
        AvaloniaProperty.RegisterAttached<BindingAssist, Control, IEnumerable<IDataTemplate>>("DataTemplates");

    public static void SetDataTemplates(Control obj, IEnumerable<IDataTemplate> value) => obj.SetValue(DataTemplatesProperty, value);

    public static IEnumerable<IDataTemplate> GetDataTemplates(Control obj) => obj.GetValue(DataTemplatesProperty);

    public static readonly AttachedProperty<IEnumerable<string>> ClassesProperty =
        AvaloniaProperty.RegisterAttached<BindingAssist, Control, IEnumerable<string>>("Classes");

    public static void SetClasses(Control obj, IEnumerable<string> value) => obj.SetValue(ClassesProperty, value);

    public static IEnumerable<string> GetClasses(Control obj) => obj.GetValue(ClassesProperty);

    static BindingAssist()
    {
        DataTemplatesProperty.Changed.AddClassHandler<Control>(HandleDataTemplatesChanged);
        ClassesProperty.Changed.AddClassHandler<Control>(HandleClassesChanged);
    }

    private static void HandleDataTemplatesChanged(Control sender, AvaloniaPropertyChangedEventArgs args)
    {
        sender.DataTemplates.Clear();
        if (args.NewValue is IEnumerable<IDataTemplate> dataTemplates) sender.DataTemplates.AddRange(dataTemplates);
    }

    private static void HandleClassesChanged(Control sender, AvaloniaPropertyChangedEventArgs args)
    {
        sender.Classes.Clear();
        if (args.NewValue is IEnumerable<string> classes) sender.Classes.AddRange(classes);
    }
}