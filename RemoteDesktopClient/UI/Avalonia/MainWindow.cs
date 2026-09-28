using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RemoteDesktopClient.Core;
using RemoteDesktopClient.Core.Configuration;
using RemoteDesktopClient.Core.Control;
using RemoteDesktopClient.Core.Device;
using RemoteDesktopClient.Native.Windows;
using RemoteDesktopClient.Services;
using RemoteDesktopClient.Signaling;
using RemoteDesktopClient.UI.Avalonia.Components;
using RemoteDesktopClient.UI.Avalonia.Components.ConnectToRemoteDevicePanel;
using RemoteDesktopClient.UI.Avalonia.Components.FooterBar;
using RemoteDesktopClient.UI.Avalonia.Components.HeaderBar;
using RemoteDesktopClient.UI.Avalonia.Components.RecentConnectionsPanel;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;
using RemoteDesktopClient.UI.Avalonia.Components.ThisDevicePanel;
using RemoteDesktopClient.UI.Avalonia.Components.TitleBar;
using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.UI.Avalonia;

public sealed partial class MainWindow : Window
{
    private IconTextBox _remoteIdInput = null!;
    private ModernButton _remoteConnectButton = null!;
    private IdChip _idChip = null!;
    private StatusBadge _statusBadge = null!;
    private RecentConnectionsCard _recentConnectionsCard = null!;
    private Panel _modalHost = null!;

    private string? _pendingOutgoingRequestId;
    private string? _incomingRequestId;
    private string? _incomingRequesterComputerName;
    private string? _incomingRequesterDeviceId;

    private string? _localDeviceId;

    private ActiveSessionToolbar? _activeSessionToolbar;

    private RemoteSessionWindow? _remoteSessionWindow;

    private bool _closingRemoteSessionWindowProgrammatically;

    private string? _requesterConnectingRequestId;

    private string? _activeToolbarRequestId;

    private WebRtcSessionManager? _webRtcSession;
    private string? _webRtcRequestId;
    private WebRtcRole? _webRtcRole;
    private string? _webRtcPeerLabel;
    private string? _webRtcPeerDeviceId;
    private CancellationTokenSource? _webRtcEstablishingCts;

    private bool _webRtcSessionConfirmedActive;
    private bool _webRtcTestMessageSent;

    private static readonly TimeSpan WebRtcEstablishmentTimeout = TimeSpan.FromSeconds(20);

    private readonly AppSettings _settings;
    private readonly DeviceIdentityClient _deviceIdentityClient;
    private readonly SignalingClient _signalingClient;
    private readonly CancellationTokenSource _deviceIdentityCts = new();

    private readonly IRemoteInputInjector _inputInjector = OperatingSystem.IsWindows()
        ? new Win32InputInjector()
        : new NullInputInjector();

    private readonly IRemoteClipboard _clipboard = OperatingSystem.IsWindows()
        ? new Win32Clipboard()
        : new NullRemoteClipboard();
    private readonly IClipboardWatcher _clipboardWatcher = OperatingSystem.IsWindows()
        ? new Win32ClipboardWatcher()
        : new NullClipboardWatcher();
    private string? _lastSyncedClipboardText;
    private byte[]? _lastSyncedClipboardImage;
    private IReadOnlyList<string>? _lastSyncedClipboardFiles;

    private ClipboardFileTransfer? _fileTransfer;

    private DebugSessionLogTransfer? _debugLogTransfer;

    public MainWindow(AppSettings settings, bool screenShareAvailable = true)
    {
        _settings = settings;
        _deviceIdentityClient = new DeviceIdentityClient(settings.DeviceWebSocketUrl);
        _signalingClient = new SignalingClient(_deviceIdentityClient);
        Title = "RemoteConnect";
        MinWidth = 860;
        MinHeight = 640;
        Width = 1180;
        Height = 840;
        Background = new SolidColorBrush(AppTheme.Background);
        CanResize = true;
        WindowDecorations = WindowDecorations.None;
        ExtendClientAreaToDecorationsHint = true;

        var root = new DockPanel();
        var titleBar = new TitleBar();
        titleBar.DragRequested += (_, e) => BeginMoveDrag(e);
        titleBar.ToggleMaximizeRequested += (_, _) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        titleBar.MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        titleBar.MaximizeButton.Click += (_, _) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
                titleBar.MaximizeButton.Icon = WindowState == WindowState.Maximized ? IconKind.WindowRestore : IconKind.WindowMaximize;
        };
        titleBar.CloseButton.Click += (_, _) => Close();
        root.Children.Add(titleBar);
        root.Children.Add(new HeaderBar());
        root.Children.Add(new FooterBar());
        var contentArea = new ContentArea(_settings.EnableSessionThumbnails);
        _idChip = contentArea.ThisDeviceCard.IdChip;
        _statusBadge = contentArea.ThisDeviceCard.StatusBadge;
        _remoteIdInput = contentArea.RemoteConnectionCard.RemoteIdInput;
        _remoteConnectButton = contentArea.RemoteConnectionCard.ConnectButton;
        _recentConnectionsCard = contentArea.RecentConnectionsCard;
        _recentConnectionsCard.ConnectClicked += OnRecentConnectionsCardConnectClicked;
        _recentConnectionsCard.RetryClicked += OnRecentConnectionsRetryClicked;
        _remoteIdInput.TextChanged += (_, _) => FormatRemoteIdAsDigitsAreTyped();
        _remoteIdInput.TextChanged += (_, _) =>
            _remoteConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(_remoteIdInput.Text);
        root.Children.Add(contentArea);

        var mainContent = new Border
        {
            BorderBrush = new SolidColorBrush(AppTheme.CardBorder),
            BorderThickness = new Thickness(1),
            Child = root,
        };

        _modalHost = new Panel { IsHitTestVisible = false };

        var rootContainer = new Panel();
        rootContainer.Children.Add(mainContent);
        rootContainer.Children.Add(_modalHost);
        Content = rootContainer;

        WindowStartupLocation = WindowStartupLocation.Manual;
        {
            var screens = Screens.All;
            var screen = Screens.Primary ?? (screens.Count > 0 ? screens[0] : null);
            if (screen is not null)
            {
                var area = screen.WorkingArea;
                var scale = screen.Scaling;
                int x = area.X + (int)((area.Width - Width * scale) / 2);
                int y = area.Y + (int)((area.Height - Height * scale) / 2);
                Position = new PixelPoint(x, Math.Max(area.Y, y));
            }
        }

        if (!screenShareAvailable)
        {
            ShowMessageModal(
                "Screen Sharing Unavailable",
                "This app couldn't load its bundled video component, so screen sharing won't be available this session. You can still connect and use the remote session's data channel normally.");
        }

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _remoteConnectButton.IsEnabled)
                _remoteConnectButton.PerformClick();
        };

        _deviceIdentityClient.DeviceIdResolved += OnDeviceIdResolved;
        _deviceIdentityClient.ConnectionStateChanged += OnConnectionStateChanged;
        _deviceIdentityClient.ConnectionRequestPending += OnConnectionRequestPending;
        _deviceIdentityClient.ConnectionRequestFailed += OnConnectionRequestFailed;
        _deviceIdentityClient.ConnectionRequestDeclined += OnConnectionRequestDeclined;
        _deviceIdentityClient.ConnectionRequestAccepted += OnConnectionRequestAccepted;
        _deviceIdentityClient.IncomingConnectionRequestReceived += OnIncomingConnectionRequestReceived;
        _deviceIdentityClient.IncomingConnectionRequestCancelled += OnIncomingConnectionRequestCancelled;
        _deviceIdentityClient.ConnectionActionAcknowledged += OnConnectionActionAcknowledged;
        _deviceIdentityClient.SessionInterrupted += OnSessionInterrupted;
        _deviceIdentityClient.SessionEnded += OnSessionEnded;
        _deviceIdentityClient.SessionActive += OnSessionActive;
        _deviceIdentityClient.RecentConnectionsReceived += OnRecentConnectionsReceived;
        _deviceIdentityClient.RecentConnectionsFailed += OnRecentConnectionsFailed;
        _deviceIdentityClient.DeviceStatusChanged += OnDeviceStatusChanged;

        _remoteConnectButton.Click += OnRemoteConnectButtonClick;

        _ = _deviceIdentityClient.KeepConnectionAliveAsync(_deviceIdentityCts.Token);

        Closed += (_, _) => _deviceIdentityCts.Cancel();

        Opened += (_, _) =>
        {
            if (TryGetPlatformHandle() is { } platformHandle)
                _clipboardWatcher.Attach(platformHandle.Handle);
        };
        _clipboardWatcher.ClipboardChanged += OnLocalClipboardChanged;

        SingleInstance.ListenForActivation(() => Dispatcher.UIThread.Post(ActivateForRelaunch));

        _ = CheckForUpdateAsync();
    }

}
