using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Chat.Permissions;
using ShadUI;

namespace Everywhere.Views;

public class ConsentDecisionEventArgs(ConsentDecision decision) : RoutedEventArgs
{
    public ConsentDecision Decision { get; } = decision;
}

public partial class ConsentDecisionCard : Card
{
    /// <summary>
    /// Defines the <see cref="CanRemember"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> CanRememberProperty =
        AvaloniaProperty.Register<ConsentDecisionCard, bool>(nameof(CanRemember), true);

    /// <summary>
    /// Gets or sets a value indicating whether the user can choose to remember their decision.
    /// </summary>
    public bool CanRemember
    {
        get => GetValue(CanRememberProperty);
        set => SetValue(CanRememberProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ConsentSelected"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<ConsentDecisionEventArgs> ConsentSelectedEvent =
        RoutedEvent.Register<ConsentDecisionCard, ConsentDecisionEventArgs>(nameof(ConsentSelected), RoutingStrategies.Bubble);

    /// <summary>
    /// Occurs when the user selects a consent decision.
    /// </summary>
    public event EventHandler<ConsentDecisionEventArgs>? ConsentSelected
    {
        add => AddHandler(ConsentSelectedEvent, value);
        remove => RemoveHandler(ConsentSelectedEvent, value);
    }

    [RelayCommand]
    private void SelectConsent(ConsentDecision decision)
    {
        RaiseEvent(new ConsentDecisionEventArgs(decision) { RoutedEvent = ConsentSelectedEvent });
    }
}