using System.ComponentModel;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Chat;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Utilities;
using LiveMarkdown.Avalonia;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Everywhere.Views;

public partial class ChatWindow : ReactiveShadWindow<ChatWindowViewModel>, IReactiveHost
{
    public DialogHost DialogHost => PART_DialogHost;

    public ToastHost ToastHost => PART_ToastHost;

    public static readonly DirectProperty<ChatWindow, bool> IsOpenedProperty =
        AvaloniaProperty.RegisterDirect<ChatWindow, bool>(nameof(IsOpened), o => o.IsOpened);

    public bool IsOpened
    {
        get;
        private set => SetAndRaise(IsOpenedProperty, ref field, value);
    }

    public static readonly StyledProperty<bool> IsWindowPinnedProperty =
        AvaloniaProperty.Register<ChatWindow, bool>(nameof(IsWindowPinned));

    public bool IsWindowPinned
    {
        get => GetValue(IsWindowPinnedProperty);
        set => SetValue(IsWindowPinnedProperty, value);
    }

    private static Size DefaultSize => new(400d, 600d);

    private readonly ILauncher _launcher;
    private readonly IWindowHelper _windowHelper;
    private readonly Settings _settings;

    public ChatWindow(
        ILauncher launcher,
        IWindowHelper windowHelper,
        Settings settings)
    {
        _launcher = launcher;
        _windowHelper = windowHelper;
        _settings = settings;

        InitializeComponent();
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel, true);

        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        ChatInputBox.TextChanged += HandleChatInputBoxTextChanged;
        ChatInputBox.PastingFromClipboard += HandleChatInputBoxPastingFromClipboard;
        
        SetupDragDropHandlers();
    }
    
    private void SetupDragDropHandlers()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, HandleDragEnter);
        AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        AddHandler(DragDrop.DragLeaveEvent, HandleDragLeave);
        AddHandler(DragDrop.DropEvent, HandleDrop);
    }

    /// <summary>
    /// Initializes the chat window.
    /// </summary>
    public void Initialize()
    {
        EnsureInitialized();
        ApplyStyling();
        ApplyTemplate();
        _windowHelper.SetCloaked(this, true);
        ShowActivated = true;
        Topmost = true;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e)
        {
            case { Key: Key.Escape }:
            {
                IsOpened = false;
                e.Handled = true;
                break;
            }
            case { Key: Key.D, KeyModifiers: KeyModifiers.Control }:
            {
                IsWindowPinned = !IsWindowPinned;
                e.Handled = true;
                break;
            }
            case { Key: Key.N, KeyModifiers: KeyModifiers.Control }:
            {
                ViewModel.ChatContextManager.CreateNewCommand.Execute(null);
                e.Handled = true;
                break;
            }
            case { Key: Key.T, KeyModifiers: KeyModifiers.Control } when
                _settings.Model.SelectedCustomAssistant?.IsFunctionCallingSupported.ActualValue is true:
            {
                _settings.Internal.IsToolCallEnabled = !_settings.Internal.IsToolCallEnabled;
                e.Handled = true;
                break;
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            IsOpened = change.NewValue is true;
        }
        else if (change.Property == IsWindowPinnedProperty)
        {
            var value = change.NewValue is true;
            _settings.Internal.IsChatWindowPinned = value;
            ShowInTaskbar = value;
            _windowHelper.SetCloaked(this, false); // Uncloak when pinned state changes to ensure visibility
        }
    }

    /// <summary>
    /// Indicates whether the window has been resized by the user.
    /// </summary>
    private bool _isUserResized;

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);

        if (e.Reason == WindowResizeReason.User)
        {
            if (e.ClientSize.NearlyEquals(new Size(MinWidth, MinHeight)))
            {
                _isUserResized = false;
                SizeToContent = SizeToContent.WidthAndHeight;
            }
            else
            {
                _isUserResized = true;
            }
        }
        else if (!_isUserResized)
        {
            ClampToScreen();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_isUserResized)
        {
            availableSize = DefaultSize;
        }

        double width = 0;
        double height = 0;

        {
            var visualCount = VisualChildren.Count;
            for (var i = 0; i < visualCount; i++)
            {
                var visual = VisualChildren[i];
                if (visual is not Layoutable layoutable) continue;

                layoutable.Measure(availableSize);
                var childSize = layoutable.DesiredSize;
                if (childSize.Width > width) width = childSize.Width;
                if (childSize.Height > height) height = childSize.Height;
            }
        }

        if (_isUserResized)
        {
            var clientSize = ClientSize;

            if (!double.IsInfinity(availableSize.Width))
            {
                width = availableSize.Width;
            }
            else
            {
                width = clientSize.Width;
            }

            if (!double.IsInfinity(availableSize.Height))
            {
                height = availableSize.Height;
            }
            else
            {
                height = clientSize.Height;
            }
        }

        return new Size(width, height);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        if (!ViewModel.IsPickingFiles && !IsActive && !IsWindowPinned && !_windowHelper.AnyModelDialogOpened(this))
        {
            IsOpened = false;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new NoneAutomationPeer(this); // Disable automation peer to avoid being detected by self
    }

    private void ClampToScreen()
    {
        var position = Position;
        var actualSize = Bounds.Size.To(s => new PixelSize((int)(s.Width * DesktopScaling), (int)(s.Height * DesktopScaling)));
        var screenBounds = Screens.ScreenFromPoint(position)?.Bounds ?? Screens.Primary?.Bounds ?? Screens.All[0].Bounds;
        Position = ClampToArea(position, actualSize, screenBounds);
    }

    private static PixelPoint ClampToArea(PixelPoint pos, PixelSize size, PixelRect area)
    {
        var x = Math.Max(area.X, Math.Min(pos.X, area.X + area.Width - size.Width));
        var y = Math.Max(area.Y, Math.Min(pos.Y, area.Y + area.Height - size.Height));
        return new PixelPoint(x, y);
    }
    
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (TitleBarBorder.Bounds.Contains(e.GetCurrentPoint(this).Position))
            BeginMoveDrag(e);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ViewModel.IsOpened)) return;

        IsOpened = ViewModel.IsOpened;
        if (IsOpened)
        {
            switch (_settings.ChatWindow.WindowPinMode)
            {
                case ChatWindowPinMode.RememberLast:
                {
                    IsWindowPinned = _settings.Internal.IsChatWindowPinned;
                    break;
                }
                case ChatWindowPinMode.AlwaysPinned:
                {
                    IsWindowPinned = true;
                    break;
                }
                case ChatWindowPinMode.AlwaysUnpinned:
                case ChatWindowPinMode.PinOnInput:
                {
                    IsWindowPinned = false;
                    break;
                }
            }

            ShowInTaskbar = IsWindowPinned;
            _windowHelper.SetCloaked(this, false);
            ChatInputBox.Focus();
        }
        else
        {
            ShowInTaskbar = false;
            _windowHelper.SetCloaked(this, true);
        }
    }

    private void HandleChatInputBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_settings.ChatWindow.WindowPinMode == ChatWindowPinMode.PinOnInput)
        {
            IsWindowPinned = true;
        }
    }

    /// <summary>
    /// TODO: Avalonia says they will support this in 12.0
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleChatInputBoxPastingFromClipboard(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.AddClipboardCommand.CanExecute(null)) return;

        ViewModel.AddClipboardCommand.Execute(null);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // allow closing only on application or OS shutdown
        // otherwise, Windows will say "Everywhere is preventing shutdown"
        if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            base.OnClosing(e);
            return;
        }

        // do not allow closing, just hide the window
        e.Cancel = true;
        IsOpened = false;

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        ServiceLocator.Resolve<ILogger<ChatWindow>>().LogError("Chat window was closed unexpectedly. This should not happen.");
    }

    [RelayCommand]
    private Task LaunchInlineHyperlink(InlineHyperlinkClickedEventArgs e)
    {
        // currently we only support http(s) links for safety reasons
        return e.HRef is not { Scheme: "http" or "https" } uri ? Task.CompletedTask : _launcher.LaunchUriAsync(uri);
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        UpdateDragVisuals(e);
        e.Handled = true;
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        UpdateDragVisuals(e);
        e.Handled = true;
    }

    private void UpdateDragVisuals(DragEventArgs e)
    {
        if (ViewModel.IsBusy)
        {
            e.DragEffects = DragDropEffects.None;
            DragDropOverlay.IsVisible = false;
            return;
        }

        var hasFiles = e.DataTransfer.Contains(DataFormat.File);
        var hasText = e.DataTransfer.Contains(DataFormat.Text);

        if (!hasFiles && !hasText)
        {
            e.DragEffects = DragDropEffects.None;
            DragDropOverlay.IsVisible = false;
            return;
        }

        // Check file support
        if (hasFiles)
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null)
            {
                var hasSupportedFile = false;
                var hasUnsupportedFile = false;
                string? firstMimeType = null;

                foreach (var item in files)
                {
                    if (IsSupportedFile(item, out _, out var mimeType))
                    {
                        hasSupportedFile = true;
                        firstMimeType ??= mimeType;
                    }
                    else
                    {
                        hasUnsupportedFile = true;
                    }
                }

                if (hasUnsupportedFile)
                {
                    e.DragEffects = DragDropEffects.None;
                    DragDropIcon.Kind = LucideIconKind.FileX;
                    DragDropText.Text = LocaleKey.ChatWindow_DragDrop_Overlay_Unsupported.I18N();
                    DragDropOverlay.IsVisible = true;
                    return;
                }

                if (hasSupportedFile)
                {
                    if (ViewModel.ChatAttachments.Count >= _settings.Internal.MaxChatAttachmentCount)
                    {
                        e.DragEffects = DragDropEffects.None;
                        DragDropOverlay.IsVisible = false;
                        return;
                    }

                    e.DragEffects = DragDropEffects.Copy;
                    DragDropIcon.Kind = firstMimeType != null && FileUtilities.IsOfCategory(firstMimeType, FileTypeCategory.Image)
                        ? LucideIconKind.Image
                        : LucideIconKind.FileUp;
                    DragDropText.Text = LocaleKey.ChatWindow_DragDrop_Overlay_DropFilesHere.I18N();
                    DragDropOverlay.IsVisible = true;
                    return;
                }
            }
        }

        // Text only
        if (hasText)
        {
            e.DragEffects = DragDropEffects.Copy;
            DragDropIcon.Kind = LucideIconKind.TextCursorInput;
            DragDropText.Text = LocaleKey.ChatWindow_DragDrop_Overlay_DropTextHere.I18N();
            DragDropOverlay.IsVisible = true;
            return;
        }

        e.DragEffects = DragDropEffects.None;
        DragDropOverlay.IsVisible = false;
    }

    private void HandleDragLeave(object? sender, DragEventArgs e)
    {
        DragDropOverlay.IsVisible = false;
        e.Handled = true;
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        DragDropOverlay.IsVisible = false;
        e.Handled = true;

        if (ViewModel.IsBusy)
            return;

        HandleDropAsync().Detach(ToastHost.Manager.ToExceptionHandler());

        async Task HandleDropAsync()
        {
            // Handle file drops
            if (e.DataTransfer.Contains(DataFormat.File))
            {
                var files = e.DataTransfer.TryGetFiles();
                if (files is null) return;

                foreach (var item in files)
                {
                    if (!IsSupportedFile(item, out var localPath, out _))
                        continue;

                    try
                    {
                        await ViewModel.AddFileFromDragDropAsync(localPath!);
                    }
                    catch (Exception ex)
                    {
                        ServiceLocator.Resolve<ILogger<ChatWindow>>()
                            .LogError(ex, "Failed to add dropped file: {FilePath}", localPath);
                    }

                    if (ViewModel.ChatAttachments.Count >= _settings.Internal.MaxChatAttachmentCount) break;
                }
            }


            // Handle text drops
            if (e.DataTransfer.Contains(DataFormat.Text))
            {
                var text = e.DataTransfer.TryGetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                var currentText = _settings.Internal.ChatInputBoxText ?? string.Empty;
                var caretIndex = ChatInputBox.CaretIndex;
                _settings.Internal.ChatInputBoxText = currentText.Insert(caretIndex, text);
                ChatInputBox.CaretIndex = caretIndex + text.Length;
            }
        }
    }

    private static bool IsSupportedFile(IStorageItem storageItem, out string? localPath, out string? mimeType)
    {
        localPath = null;
        mimeType = null;

        if (!storageItem.Path.IsFile || storageItem.TryGetLocalPath() is not { } path)
            return false;

        localPath = path;
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (FileUtilities.KnownMimeTypes.TryGetValue(extension, out var mime) &&
            FileUtilities.KnownFileTypes.TryGetValue(mime, out var fileType) &&
            fileType is FileTypeCategory.Image or FileTypeCategory.Audio or FileTypeCategory.Document or FileTypeCategory.Script)
        {
            mimeType = mime;
            return true;
        }

        return false;
    }

}
