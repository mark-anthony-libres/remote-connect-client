using System.Drawing.Drawing2D;
using RemoteDesktopClient.Core.Device;
using RemoteDesktopClient.UI.WinForms.Controls;

namespace RemoteDesktopClient.UI.WinForms;

partial class MainForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "RemoteConnect";
        MinimumSize = new Size(860, 640);
        ClientSize = new Size(1180, 840);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        Font = Theme.FontBody;

        var header = BuildHeaderBar();
        var footer = BuildFooterBar();
        var content = BuildContentArea();

        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(header);

        // Enter-triggers-Connect is handled in MainForm.ProcessCmdKey instead
        // of via Form.AcceptButton: AcceptButton marks the button as the Win32
        // "default push button" (IsDefault = true), which makes Windows paint
        // an extra dark default-button border around it — a native chrome
        // effect that a fully owner-drawn ModernButton.OnPaint cannot suppress.

        ResumeLayout(false);

        // Several controls above position themselves manually from their
        // parent's ClientSize (not Dock/Anchor) the moment they're
        // constructed, when the form doesn't have its real, final size yet.
        // Forcing one more full layout pass right before the form is shown
        // re-runs that positioning against the true size.
        Load += (_, _) => PerformLayout();
    }

    #endregion

    // -------------------------------------------------------------------
    // UI construction helpers — hand-written (not drag-and-drop generated),
    // but called from InitializeComponent() above so the Designer can still
    // render a live preview. Grouped here with the rest of the UI setup.
    // -------------------------------------------------------------------

    private static Panel BuildHeaderBar()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 84,
            BackColor = Theme.CardBackground,
        };

        var logoMark = new Panel
        {
            Size = new Size(40, 40),
            Location = new Point(24, 22),
        };
        logoMark.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, logoMark.Width - 1, logoMark.Height - 1);
            using var path = RoundedRect.CreatePath(rect, 11);
            using var brush = new SolidBrush(Theme.Accent);
            e.Graphics.FillPath(brush, path);
            using var dotBrush = new SolidBrush(Color.White);
            e.Graphics.FillEllipse(dotBrush, rect.Width / 2f - 5f, rect.Height / 2f - 5f, 10, 10);
        };

        var title = new Label
        {
            Text = "RemoteConnect",
            Font = Theme.FontTitle,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(24 + logoMark.Width + 12, 12),
        };

        var subtitle = new Label
        {
            Text = "Securely connect to your devices, anytime.",
            Font = Theme.FontSubtitle,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Location = new Point(24 + logoMark.Width + 12, 50),
        };

        var settingsButton = new ModernButton
        {
            Text = "Settings",
            Icon = IconKind.Gear,
            Variant = ButtonVariant.Subtle,
            Size = new Size(112, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        var avatar = new Panel
        {
            Size = new Size(56, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        avatar.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var circleBrush = new SolidBrush(Theme.AccentSoft);
            e.Graphics.FillEllipse(circleBrush, 0, 2, 36, 36);
            TextRenderer.DrawText(
                e.Graphics, "M", Theme.FontBodyBold, new Rectangle(0, 2, 36, 36), Theme.AccentSoftText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            var chevronBounds = new RectangleF(42, 15, 12, 10);
            Icons.Draw(e.Graphics, IconKind.Chevron, chevronBounds, Theme.TextSecondary, 1.5f);
        };

        void PositionHeaderRightControls()
        {
            settingsButton.Location = new Point(header.ClientSize.Width - settingsButton.Width - 24, 22);
            avatar.Location = new Point(settingsButton.Left - avatar.Width - 20, 22);
        }
        header.Resize += (_, _) => PositionHeaderRightControls();
        PositionHeaderRightControls();

        header.Controls.Add(logoMark);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(settingsButton);
        header.Controls.Add(avatar);

        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.CardBorder);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        return header;
    }

    private static Panel BuildFooterBar()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = Theme.CardBackground,
        };

        var statusLabel = new Label
        {
            Text = "Ready to connect   ·   Secure   ·   Reliable   ·   Fast",
            Font = Theme.FontSmall,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Location = new Point(38, 12),
        };

        var versionLabel = new Label
        {
            Text = "RemoteConnect v1.0.0",
            Font = Theme.FontSmall,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        void PositionVersionLabel() =>
            versionLabel.Location = new Point(footer.ClientSize.Width - versionLabel.Width - 24, 12);
        footer.Resize += (_, _) => PositionVersionLabel();
        PositionVersionLabel();

        footer.Controls.Add(statusLabel);
        footer.Controls.Add(versionLabel);
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.CardBorder);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);

            using var dotBrush = new SolidBrush(Theme.Online);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillEllipse(dotBrush, 24, footer.Height / 2f - 3.5f, 7, 7);
        };

        return footer;
    }

    // Content: This Device (hero, fixed) + Remote Connection (fixed) + Recent
    // Connections (fills whatever vertical space remains, and scrolls
    // internally past that) — a TableLayoutPanel drives all three, so
    // resizing the window reflows the page instead of just clipping it.
    private Panel BuildContentArea()
    {
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
        };

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 3,
        };
        // Each row is the card's intended content height PLUS its own
        // bottom margin (200+20, 178+20) — not just the content height.
        // Dock.Fill + Margin inside a fixed Absolute row doesn't add space
        // around a full-size card; the margin is carved out of the row,
        // shrinking the card by exactly the margin amount. Sizing the row
        // for content+margin keeps the card at its full intended height
        // and turns the margin into genuine extra space after it.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 198f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var thisDeviceCard = BuildThisDeviceCard();
        thisDeviceCard.Dock = DockStyle.Fill;
        thisDeviceCard.Margin = new Padding(0, 0, 0, 20);

        var remoteConnectionCard = BuildRemoteConnectionCard();
        remoteConnectionCard.Dock = DockStyle.Fill;
        remoteConnectionCard.Margin = new Padding(0, 0, 0, 20);

        var recentConnectionsCard = BuildRecentConnectionsCard(CreateMockRecentConnections());
        recentConnectionsCard.Dock = DockStyle.Fill;
        recentConnectionsCard.Margin = new Padding(0);

        layout.Controls.Add(thisDeviceCard, 0, 0);
        layout.Controls.Add(remoteConnectionCard, 0, 1);
        layout.Controls.Add(recentConnectionsCard, 0, 2);

        // The three cards are capped at a comfortable reading width and
        // centered, rather than stretching edge-to-edge on a wide monitor —
        // recalculated on every resize so it stays centered as the window
        // grows or shrinks.
        const int maxContentWidth = 1400;
        const int sidePadding = 28;
        const int topPadding = 20;
        const int bottomPadding = 16;

        void Reflow()
        {
            int available = Math.Max(0, content.ClientSize.Width - sidePadding * 2);
            int width = Math.Min(available, maxContentWidth);
            int x = sidePadding + (available - width) / 2;
            int height = Math.Max(0, content.ClientSize.Height - topPadding - bottomPadding);
            layout.Bounds = new Rectangle(x, topPadding, width, height);
        }
        content.Resize += (_, _) => Reflow();
        content.Controls.Add(layout);
        Reflow();

        return content;
    }

    private static RoundedPanel BuildThisDeviceCard()
    {
        var card = new RoundedPanel { CardColor = Theme.HeroBackground, BorderColor = Theme.HeroBackground };

        // Two halves (identity | ready-to-connect), each built the same way:
        // a fixed-width icon column next to a column that fills the rest of
        // the half and holds a top-to-bottom stack of labels. Everything is
        // positioned by the TableLayoutPanels themselves (Dock/Margin), not
        // by hand-computed pixel coordinates.
        var halves = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.HeroBackground,
            ColumnCount = 2,
            RowCount = 1,
        };
        halves.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
        halves.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));

        halves.Controls.Add(BuildIdentityHalf(), 0, 0);
        halves.Controls.Add(BuildReadyToConnectHalf(), 1, 0);

        card.Controls.Add(halves);
        return card;
    }

    private static TableLayoutPanel BuildIdentityHalf()
    {
        var half = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.HeroBackground,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 6, 0, 6),
        };
        half.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
        half.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var iconBadge = new IconBadge
        {
            Icon = IconKind.Monitor,
            TintBackground = Theme.CardBackground,
            TintForeground = Theme.Accent,
            Size = new Size(72, 72),
            CornerRadius = 16,
            Anchor = AnchorStyles.None,
        };

        var textStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.HeroBackground,
            ColumnCount = 1,
            RowCount = 4,
        };
        for (int i = 0; i < 4; i++)
        {
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var thisDeviceLabel = new Label
        {
            Text = "This Device",
            Font = Theme.FontBody,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 4),
        };

        var nameLabel = new Label
        {
            Text = "DESKTOP-7XQ2KD1",
            Font = Theme.FontTitle,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 12),
        };

        var idChip = new IdChip
        {
            DeviceId = "482 913 607",
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 12),
        };

        var statusBadge = new StatusBadge
        {
            IsOnline = true, // placeholder — this app instance is "online" once signaling exists
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0),
        };

        textStack.Controls.Add(thisDeviceLabel, 0, 0);
        textStack.Controls.Add(nameLabel, 0, 1);
        textStack.Controls.Add(idChip, 0, 2);
        textStack.Controls.Add(statusBadge, 0, 3);

        half.Controls.Add(iconBadge, 0, 0);
        half.Controls.Add(textStack, 1, 0);

        return half;
    }

    private static TableLayoutPanel BuildReadyToConnectHalf()
    {
        var half = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.HeroBackground,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 6, 0, 6),
        };
        half.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
        half.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var illustration = new HeroIllustration { Anchor = AnchorStyles.None };

        var textStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.HeroBackground,
            ColumnCount = 1,
            RowCount = 2,
        };
        textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        textStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));

        var readyTitle = new Label
        {
            Text = "Ready to connect",
            Font = Theme.FontSectionHeader,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 8),
        };

        var readyDescription = new Label
        {
            Text = "Share this ID with someone to allow them to connect to this device.",
            Font = Theme.FontBody,
            ForeColor = Theme.TextSecondary,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };

        textStack.Controls.Add(readyTitle, 0, 0);
        textStack.Controls.Add(readyDescription, 0, 1);

        half.Controls.Add(illustration, 0, 0);
        half.Controls.Add(textStack, 1, 0);

        return half;
    }

    private RoundedPanel BuildRemoteConnectionCard()
    {
        var card = new RoundedPanel();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.CardBackground,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // Header: icon | title+subtitle stack — same pattern as This Device,
        // laid out by the TableLayoutPanel itself, not by hand-placed points.
        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.CardBackground,
            ColumnCount = 2,
            RowCount = 1,
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48f));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var iconBadge = new IconBadge
        {
            Icon = IconKind.Link,
            TintBackground = Theme.IconBlueBg,
            TintForeground = Theme.IconBlueFg,
            Size = new Size(36, 36),
            CornerRadius = 10,
            Anchor = AnchorStyles.None,
        };

        var headerTextStack = new TableLayoutPanel
        {
            // AutoSize (not Dock.Fill) so this stack sizes to its two lines of
            // text instead of stretching to headerRow's full cell height.
            // Anchor.Left with no vertical bits centers it in the cell,
            // matching iconBadge's Anchor.None.
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left,
            BackColor = Theme.CardBackground,
            ColumnCount = 1,
            RowCount = 2,
        };
        headerTextStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerTextStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = "Connect to a Remote Device",
            Font = Theme.FontSectionHeader,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 4),
        };
        var subtitleLabel = new Label
        {
            Text = "Enter the remote device's ID to establish a connection.",
            Font = Theme.FontBody,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0),
        };
        headerTextStack.Controls.Add(titleLabel, 0, 0);
        headerTextStack.Controls.Add(subtitleLabel, 0, 1);

        headerRow.Controls.Add(iconBadge, 0, 0);
        headerRow.Controls.Add(headerTextStack, 1, 0);

        // Input row: the ID box fills all remaining width, the Connect
        // button keeps a comfortable fixed width, with a clear gap between
        // them via the input's own right Margin — all handled by the
        // TableLayoutPanel's column split, not manual pixel math.
        var inputRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.CardBackground,
            ColumnCount = 2,
            RowCount = 1,
        };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160f));

        _remoteIdInput = new IconTextBox
        {
            Icon = IconKind.Monitor,
            PlaceholderText = "Enter device ID (e.g. 123 456 789)",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 24, 4),
        };

        _remoteConnectButton = new ModernButton
        {
            Text = "Connect",
            Icon = IconKind.Send,
            Variant = ButtonVariant.Primary,
            Size = new Size(160, 52),
            Enabled = false,
            Anchor = AnchorStyles.None,
        };
        _remoteIdInput.TextChanged += (_, _) =>
            _remoteConnectButton.Enabled = !string.IsNullOrWhiteSpace(_remoteIdInput.Text);

        inputRow.Controls.Add(_remoteIdInput, 0, 0);
        inputRow.Controls.Add(_remoteConnectButton, 1, 0);

        layout.Controls.Add(headerRow, 0, 0);
        layout.Controls.Add(inputRow, 0, 1);

        card.Controls.Add(layout);
        return card;
    }

    private static RoundedPanel BuildRecentConnectionsCard(IReadOnlyList<DeviceConnection> devices)
    {
        var card = new RoundedPanel();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.CardBackground,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var headerRow = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
        var iconBadge = new IconBadge
        {
            Icon = IconKind.Clock,
            TintBackground = Theme.IconBlueBg,
            TintForeground = Theme.IconBlueFg,
            Size = new Size(36, 36),
            Location = new Point(0, 0),
            CornerRadius = 10,
        };
        var titleLabel = new Label
        {
            Text = "Recent Connections",
            Font = Theme.FontSectionHeader,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(48, 0),
        };
        var subtitleLabel = new Label
        {
            Text = "Quickly connect to your frequently used devices.",
            Font = Theme.FontBody,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Location = new Point(48, 24),
        };
        var clearAllButton = new ModernButton
        {
            Text = "Clear All",
            Icon = IconKind.Trash,
            Variant = ButtonVariant.Subtle,
            Size = new Size(112, 38),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        void PositionClearAllButton() =>
            clearAllButton.Location = new Point(headerRow.ClientSize.Width - clearAllButton.Width, 2);
        headerRow.Resize += (_, _) => PositionClearAllButton();
        PositionClearAllButton();

        headerRow.Controls.Add(iconBadge);
        headerRow.Controls.Add(titleLabel);
        headerRow.Controls.Add(subtitleLabel);
        headerRow.Controls.Add(clearAllButton);

        var gridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.CardBackground,
            AutoScroll = true,
        };

        var cardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            BackColor = Theme.CardBackground,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        foreach (var device in devices)
        {
            var (icon, background, foreground) = GetIconStyle(device.Type);
            cardsFlow.Controls.Add(new RecentDeviceCard(device, icon, background, foreground));
        }
        gridContainer.Controls.Add(cardsFlow);

        layout.Controls.Add(headerRow, 0, 0);
        layout.Controls.Add(gridContainer, 0, 1);

        card.Controls.Add(layout);
        return card;
    }
}
