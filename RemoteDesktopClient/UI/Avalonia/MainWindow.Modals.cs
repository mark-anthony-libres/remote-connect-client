using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia;

public sealed partial class MainWindow
{
    private void ShowModal(Control content)
    {
        var backdrop = new Border { Background = new SolidColorBrush(Color.FromArgb(153, 0, 0, 0)) };
        var layer = new Grid();
        layer.Children.Add(backdrop);
        layer.Children.Add(content);

        _modalHost.Children.Clear();
        _modalHost.Children.Add(layer);
        _modalHost.IsHitTestVisible = true;
    }

    private void HideModal()
    {
        _modalHost.Children.Clear();
        _modalHost.IsHitTestVisible = false;
    }

    private static void AttachCountdown(Control root, TextBlock countdownBlock, DateTimeOffset expiresAt)
    {
        void Tick()
        {
            var secondsLeft = Math.Max(0, (int)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalSeconds));
            countdownBlock.Text = $"{secondsLeft} second{(secondsLeft == 1 ? "" : "s")} left";
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick();
        root.AttachedToVisualTree += (_, _) => { Tick(); timer.Start(); };
        root.DetachedFromVisualTree += (_, _) => timer.Stop();
    }

    private void ShowMessageModal(string title, string message, string buttonText = "OK", Action? onDismissed = null)
    {
        var card = Card(AppTheme.CardBackground, AppTheme.CardBorder);
        card.Width = 380;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(SharedBuilders.TextBlockOf(title, AppTheme.FontSectionHeader, AppTheme.TextPrimary));
        var messageBlock = SharedBuilders.TextBlockOf(message, AppTheme.FontBody, AppTheme.TextSecondary);
        messageBlock.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(messageBlock);

        var dismissButton = new ModernButton
        {
            Text = buttonText,
            Variant = ButtonVariant.Primary,
            Width = 100,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        dismissButton.Click += (_, _) =>
        {
            HideModal();
            onDismissed?.Invoke();
        };
        stack.Children.Add(dismissButton);

        card.Child = stack;
        ShowModal(card);
    }

    private void ShowWaitingForAcceptModal(string requestId, string targetComputerName, DateTimeOffset expiresAt)
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 16,
        };
        stack.Children.Add(new Components.Shared.Spinner { Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Center });

        var titleBlock = SharedBuilders.TextBlockOf("Connection Request", AppTheme.FontSectionHeader, Colors.White);
        titleBlock.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(titleBlock);

        var targetLabel = string.IsNullOrWhiteSpace(targetComputerName) ? "the target computer" : targetComputerName;
        var messageBlock = SharedBuilders.TextBlockOf($"Waiting for {targetLabel} to accept the connection...", AppTheme.FontBody, Colors.White);
        messageBlock.HorizontalAlignment = HorizontalAlignment.Center;
        messageBlock.TextWrapping = TextWrapping.Wrap;
        messageBlock.TextAlignment = TextAlignment.Center;
        messageBlock.MaxWidth = 320;
        stack.Children.Add(messageBlock);

        var countdownBlock = SharedBuilders.TextBlockOf(string.Empty, AppTheme.FontSmall, Colors.White);
        countdownBlock.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(countdownBlock);
        AttachCountdown(stack, countdownBlock, expiresAt);

        var cancelButton = new ModernButton
        {
            Text = "Cancel",
            Variant = ButtonVariant.Subtle,
            Width = 110,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };
        cancelButton.Click += (_, _) =>
        {
            cancelButton.IsEnabled = false;
            _ = _deviceIdentityClient.CancelConnectionRequestAsync(requestId);
        };
        stack.Children.Add(cancelButton);

        ShowModal(stack);
    }

    private void ShowIncomingConnectionModal(string requestId, string requestingComputerName, string requestingDeviceId, DateTimeOffset expiresAt)
    {
        var card = Card(AppTheme.CardBackground, AppTheme.CardBorder);
        card.Width = 400;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(SharedBuilders.TextBlockOf("Incoming Connection", AppTheme.FontSectionHeader, AppTheme.TextPrimary, new Thickness(0, 0, 0, 8)));
        var introBlock = SharedBuilders.TextBlockOf("A computer wants to connect to this computer.", AppTheme.FontBody, AppTheme.TextSecondary);
        introBlock.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(introBlock);

        stack.Children.Add(SharedBuilders.TextBlockOf("Computer", AppTheme.FontSmall, AppTheme.TextSecondary, new Thickness(0, 16, 0, 0)));
        stack.Children.Add(SharedBuilders.TextBlockOf(
            string.IsNullOrWhiteSpace(requestingComputerName) ? "Unknown computer" : requestingComputerName,
            AppTheme.FontBodyBold, AppTheme.TextPrimary));

        stack.Children.Add(SharedBuilders.TextBlockOf("Device ID", AppTheme.FontSmall, AppTheme.TextSecondary, new Thickness(0, 12, 0, 0)));
        stack.Children.Add(SharedBuilders.TextBlockOf(FormatDeviceId(requestingDeviceId), AppTheme.FontMonoId, AppTheme.Accent));

        var countdownBlock = SharedBuilders.TextBlockOf(string.Empty, AppTheme.FontSmall, AppTheme.TextSecondary, new Thickness(0, 12, 0, 0));
        stack.Children.Add(countdownBlock);
        AttachCountdown(card, countdownBlock, expiresAt);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12,
            Margin = new Thickness(0, 20, 0, 0),
        };
        var declineButton = new ModernButton { Text = "Decline", Variant = ButtonVariant.Subtle, Width = 110, Height = 40 };
        var acceptButton = new ModernButton { Text = "Accept", Variant = ButtonVariant.Primary, Width = 110, Height = 40 };
        declineButton.Click += (_, _) =>
        {
            declineButton.IsEnabled = false;
            acceptButton.IsEnabled = false;
            _ = _deviceIdentityClient.DeclineConnectionRequestAsync(requestId);
        };
        acceptButton.Click += (_, _) =>
        {
            declineButton.IsEnabled = false;
            acceptButton.IsEnabled = false;
            _ = _deviceIdentityClient.AcceptConnectionRequestAsync(requestId);
        };
        buttonRow.Children.Add(declineButton);
        buttonRow.Children.Add(acceptButton);
        stack.Children.Add(buttonRow);

        card.Child = stack;
        ShowModal(card);
    }

    private void ShowLoadingModal(string title, string message)
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 16,
        };
        stack.Children.Add(new Components.Shared.Spinner { Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Center });

        var titleBlock = SharedBuilders.TextBlockOf(title, AppTheme.FontSectionHeader, Colors.White);
        titleBlock.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(titleBlock);

        var messageBlock = SharedBuilders.TextBlockOf(message, AppTheme.FontBody, Colors.White);
        messageBlock.HorizontalAlignment = HorizontalAlignment.Center;
        messageBlock.TextWrapping = TextWrapping.Wrap;
        messageBlock.TextAlignment = TextAlignment.Center;
        messageBlock.MaxWidth = 320;
        stack.Children.Add(messageBlock);

        ShowModal(stack);
    }

    private void ShowUpdateAvailableModal(string newVersion, Action onUpdateClicked)
    {
        var card = Card(AppTheme.CardBackground, AppTheme.CardBorder);
        card.Width = 380;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(SharedBuilders.TextBlockOf("Update available", AppTheme.FontSectionHeader, AppTheme.TextPrimary));
        var messageBlock = SharedBuilders.TextBlockOf($"A new version of RemoteConnect (v{newVersion}) is available.", AppTheme.FontBody, AppTheme.TextSecondary);
        messageBlock.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(messageBlock);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var laterButton = new ModernButton { Text = "Later", Variant = ButtonVariant.Subtle, Width = 100, Height = 40 };
        var updateButton = new ModernButton { Text = "Update", Variant = ButtonVariant.Primary, Width = 100, Height = 40 };
        laterButton.Click += (_, _) => HideModal();
        updateButton.Click += (_, _) =>
        {
            HideModal();
            onUpdateClicked();
        };
        buttonRow.Children.Add(laterButton);
        buttonRow.Children.Add(updateButton);
        stack.Children.Add(buttonRow);

        card.Child = stack;
        ShowModal(card);
    }

    private static Border Card(Color background, Color border) => new()
    {
        Background = new SolidColorBrush(background),
        BorderBrush = new SolidColorBrush(border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(24),
    };
}
