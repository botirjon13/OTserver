using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class MainForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly FiscalSettingsService _fiscalSettingsService = new FiscalSettingsService();
        private readonly DebtService _debtService = new DebtService();
        private readonly ProductService _productService = new ProductService();
        private readonly BackupService _backupService = new BackupService();
        private readonly ActivationService _activationService = new ActivationService();
        private readonly CloudBackupCryptoService _cloudBackupCrypto = new CloudBackupCryptoService();
        private readonly UpdateService _updateService = new UpdateService();
        private readonly CurrencyRateProviderService _currencyRateProvider = new CurrencyRateProviderService();
        private readonly DatabaseHelper _dbHelper = new DatabaseHelper();
        private readonly HomeDashboardService _homeDashboardService = new HomeDashboardService();
        private readonly Label _lblTitleOson = new Label();
        private readonly Label _lblHeaderAlerts = new Label();
        private readonly PictureBox _picRole = new PictureBox();
        private readonly Label _lblRole = new Label();
        private readonly Panel _workspace = new Panel();
        private readonly Panel _sidebar = new Panel();
        private readonly Panel _sidebarBrand = new Panel();
        private readonly Label _lblSidebarCaption = new Label();
        private readonly Button _sidebarToggleButton = new Button();
        private readonly ToolTip _navigationToolTip = new ToolTip();
        private readonly Panel _header = new Panel();
        private readonly Panel _body = new Panel();
        private readonly Panel _homeContentPanel = new Panel();
        private readonly Panel _welcomePanel = new Panel();
        private readonly Panel _homeDashboardPanel = new Panel();
        private readonly Panel _homeDashboardChart = new Panel();
        private readonly Panel _homeDashboardSummary = new Panel();
        private readonly Panel _homeStatusPanel = new Panel();
        private readonly Label _lblHomeStatus = new Label();
        private readonly Label _lblDashboardSection = new Label();
        private readonly Button _btnDashboardRefresh = new Button();
        private readonly Button _btnNewSale = new Button();
        private readonly FlowLayoutPanel _homeQuickActions = new FlowLayoutPanel();
        private readonly List<(Panel Card, Label Title, Label Value, Label Detail)> _dashboardCards =
            new List<(Panel Card, Label Title, Label Value, Label Detail)>();
        private readonly Panel _pageHost = new Panel();
        private readonly Label _lblWelcomeTitle = new Label();
        private readonly Label _lblWelcomeSubtitle = new Label();
        private readonly TableLayoutPanel _tilesGrid = new TableLayoutPanel();
        private readonly Panel _actionPanel = new Panel();
        private readonly FlowLayoutPanel _actionsFlow = new FlowLayoutPanel();
        private readonly PictureBox _picLogoutAction = new PictureBox();
        private readonly PictureBox _picExitAction = new PictureBox();
        private readonly PictureBox _picLicenseAction = new PictureBox();
        private readonly PictureBox _picThemeAction = new PictureBox();
        private readonly PictureBox _picUpdateAction = new PictureBox();
        private readonly System.Windows.Forms.Timer _actionHoverTimer = new System.Windows.Forms.Timer();
        private readonly Dictionary<PictureBox, ActionHoverState> _actionHoverStates = new Dictionary<PictureBox, ActionHoverState>();
        private readonly Label _lblFooter = new Label();
        private readonly List<TileUi> _tiles = new List<TileUi>();
        private readonly System.Windows.Forms.Timer _revealTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _headerWarningsTimer = new System.Windows.Forms.Timer();
        private int _revealIndex;
        private Color _roleTextBaseColor = Color.FromArgb(132, 182, 255);
        private const int TileColumns = 1;
        private const int TileRowHeight = 68;
        private AppUpdateInfo? _pendingUpdate;
        private Size _activePagePreferredSize;
        private Form? _activePage;
        private bool _sidebarCollapsed;
        private string? _lastUpdateCheckError;
        private bool _isUpdateDownloading;
        private HomeDashboardSnapshot? _homeDashboardSnapshot;
        private string? _homeDashboardErrorMessage;
        private string _selectedNavigationTitle = "Bosh sahifa";
        private static readonly Color DashboardSalesColor = Color.FromArgb(91, 91, 240);

        public MainForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            Icon = BrandingAssets.TryLoadAppIcon() ?? Icon;
            BuildTiles();
            ApplyThemeStyles();
            Shown += MainForm_Shown;
        }

        private async void MainForm_Shown(object? sender, EventArgs e)
        {
            if (!string.Equals(_currentUser.Role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase) ||
                !_cloudBackupCrypto.IsEnabled() ||
                !_activationService.TryGetValidLocalActivation(out LocalActivationRecord? activation, out _) ||
                activation == null)
            {
                return;
            }

            bool uploaded = await new CloudSyncService().UploadMonthlyBackupIfEnabledAsync(
                activation,
                Application.ProductVersion);
            if (!uploaded && !IsDisposed)
            {
                MessageBox.Show(
                    "Oylik Drive zaxirasi bajarilmadi. Internet va serverdagi Drive ulanishini tekshirib, Backup bo'limidan qayta urinib ko'ring.",
                    "Bulut zaxirasi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void InitializeComponent()
        {
            Text = "Osontrack SRM tizimi";
            Width = 1380;
            Height = 860;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(900, 620);
            BackColor = Color.FromArgb(231, 237, 246);
            Font = UiTheme.BodyFont;
            DoubleBuffered = true;

            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = 248;
            _sidebar.Padding = new Padding(12, 10, 12, 12);
            _sidebar.AutoScroll = true;

            _header.Dock = DockStyle.Top;
            _header.Height = 80;
            _header.Padding = new Padding(24, 14, 24, 14);
            _header.BackColor = UiTheme.Card;
            _header.Paint += (s, e) =>
            {
                if (_header.ClientSize.Width > 0 && _header.ClientSize.Height > 0)
                {
                    using SolidBrush brush = new SolidBrush(UiTheme.Card);
                    e.Graphics.FillRectangle(brush, _header.ClientRectangle);
                }
                using Pen line = new Pen(UiTheme.Border);
                e.Graphics.DrawLine(line, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            };

            _sidebarBrand.Dock = DockStyle.Top;
            _sidebarBrand.Height = 58;
            _sidebarBrand.BackColor = Color.FromArgb(12, 43, 91);
            _lblSidebarCaption.Text = "BO'LIMLAR";
            _lblSidebarCaption.SetBounds(48, 0, _sidebarBrand.Width - 48, _sidebarBrand.Height);
            _lblSidebarCaption.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _lblSidebarCaption.TextAlign = ContentAlignment.MiddleCenter;
            _lblSidebarCaption.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 9, FontStyle.Bold);
            _lblSidebarCaption.ForeColor = Color.FromArgb(177, 205, 244);
            _lblSidebarCaption.BackColor = Color.Transparent;
            _sidebarBrand.Controls.Add(_lblSidebarCaption);
            _sidebarBrand.Resize += (_, __) =>
            {
                _lblSidebarCaption.SetBounds(48, 0, Math.Max(0, _sidebarBrand.ClientSize.Width - 48), _sidebarBrand.ClientSize.Height);
            };
            _sidebarToggleButton.SetBounds(4, 8, 40, 40);
            _sidebarToggleButton.Text = "\u2039";
            _sidebarToggleButton.AccessibleName = "Yon menyuni yig'ish";
            _sidebarToggleButton.FlatStyle = FlatStyle.Flat;
            _sidebarToggleButton.FlatAppearance.BorderSize = 0;
            _sidebarToggleButton.BackColor = Color.Transparent;
            _sidebarToggleButton.ForeColor = Color.White;
            _sidebarToggleButton.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 18, FontStyle.Regular);
            _sidebarToggleButton.Cursor = Cursors.Hand;
            _sidebarToggleButton.TabStop = true;
            _sidebarToggleButton.Click += (_, __) => ToggleSidebar();
            _navigationToolTip.SetToolTip(_sidebarToggleButton, "Yon menyuni yig'ish");
            _sidebarBrand.Controls.Add(_sidebarToggleButton);
            _sidebarToggleButton.BringToFront();

            _lblTitleOson.Text = "OsonTrack SRM tizimi";
            _lblTitleOson.AutoSize = false;
            _lblTitleOson.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 12, FontStyle.Bold);
            _lblTitleOson.Left = 24;
            _lblTitleOson.Top = 14;
            _lblTitleOson.Height = 28;
            _lblTitleOson.TextAlign = ContentAlignment.MiddleLeft;
            _lblTitleOson.AutoEllipsis = true;
            _lblTitleOson.BackColor = Color.Transparent;
            _lblTitleOson.Text = GetHeaderAddressText();

            _lblHeaderAlerts.AutoSize = false;
            _lblHeaderAlerts.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 9, FontStyle.Regular);
            _lblHeaderAlerts.TextAlign = ContentAlignment.MiddleLeft;
            _lblHeaderAlerts.AutoEllipsis = true;
            _lblHeaderAlerts.BackColor = Color.Transparent;
            _lblHeaderAlerts.ForeColor = Color.FromArgb(255, 196, 84);
            _lblHeaderAlerts.Text = GetCriticalHeaderWarningsText();
            _lblHeaderAlerts.Cursor = Cursors.Hand;
            _lblHeaderAlerts.Click += HeaderAlerts_Click;

            string? roleIconFile = GetRoleIconFileName(_currentUser.Role);
            Image? roleIconImage = string.IsNullOrWhiteSpace(roleIconFile) ? null : BrandingAssets.TryLoadAssetImage(roleIconFile);
            _picRole.Size = new Size(20, 20);
            _picRole.Left = 24;
            _picRole.Top = 51;
            _picRole.BackColor = Color.Transparent;
            _picRole.SizeMode = PictureBoxSizeMode.Zoom;
            _picRole.Image = roleIconImage;
            _picRole.Visible = roleIconImage != null;
            if (_picRole.Visible)
            {
                _picRole.Size = new Size(24, 24);
            }

            _lblRole.Text = GetRoleDisplayText(_currentUser.Role);
            _lblRole.AutoSize = true;
            _lblRole.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 10.5f, FontStyle.Bold);
            _lblRole.Left = 24;
            _lblRole.Top = 53;
            _lblRole.BackColor = Color.Transparent;

            _header.Controls.Add(_lblTitleOson);
            _header.Controls.Add(_lblHeaderAlerts);
            _header.Controls.Add(_picRole);
            _header.Controls.Add(_lblRole);
            LayoutRoleBadge();
            LayoutHeaderCenterText();
            _header.Resize += (s, e) => LayoutRoleBadge();
            _header.Resize += (s, e) => LayoutHeaderCenterText();

            _tilesGrid.Dock = DockStyle.Top;
            _tilesGrid.AutoSize = false;
            _tilesGrid.Height = 600;
            _tilesGrid.ColumnCount = TileColumns;
            _tilesGrid.RowCount = 1;
            _tilesGrid.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            _tilesGrid.Padding = new Padding(0, 8, 0, 2);
            _tilesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _sidebar.Controls.Add(_tilesGrid);
            _sidebar.Controls.Add(_sidebarBrand);

            _body.Dock = DockStyle.Fill;
            _body.Padding = new Padding(24, 22, 24, 16);
            _body.AutoScroll = true;

            _welcomePanel.Dock = DockStyle.Fill;
            _welcomePanel.Height = 76;
            _welcomePanel.BackColor = Color.Transparent;

            _lblWelcomeTitle.Text = "Bosh sahifa";
            _lblWelcomeTitle.AutoSize = false;
            _lblWelcomeTitle.SetBounds(0, 4, 560, 38);
            _lblWelcomeTitle.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 24, FontStyle.Bold);
            _lblWelcomeTitle.TextAlign = ContentAlignment.MiddleLeft;
            _lblWelcomeTitle.BackColor = Color.Transparent;
            _lblWelcomeTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            _lblWelcomeSubtitle.Text = "Savdo, ombor va moliyaviy holat — bir joyda.";
            _lblWelcomeSubtitle.AutoSize = false;
            _lblWelcomeSubtitle.SetBounds(2, 43, 600, 24);
            _lblWelcomeSubtitle.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10.5f, FontStyle.Regular);
            _lblWelcomeSubtitle.TextAlign = ContentAlignment.MiddleLeft;
            _lblWelcomeSubtitle.BackColor = Color.Transparent;
            _lblWelcomeSubtitle.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _lblWelcomeSubtitle.AutoEllipsis = true;
            _welcomePanel.Controls.Add(_lblWelcomeTitle);
            _welcomePanel.Controls.Add(_lblWelcomeSubtitle);

            _pageHost.Dock = DockStyle.Fill;
            _pageHost.BackColor = UiTheme.Background;
            _pageHost.AutoScroll = true;
            _pageHost.Visible = false;

            _actionPanel.Dock = DockStyle.Bottom;
            _actionPanel.Height = 96;
            _actionPanel.Padding = new Padding(0, 4, 0, 4);

            _actionsFlow.FlowDirection = FlowDirection.LeftToRight;
            _actionsFlow.WrapContents = true;
            _actionsFlow.AutoSize = true;
            _actionsFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _actionsFlow.BackColor = Color.Transparent;

            SetupActionImageBox(_picLogoutAction, "Akkauntdan chiqish", Color.FromArgb(17, 139, 201), Color.FromArgb(14, 118, 178));
            _picLogoutAction.Click += (s, e) =>
            {
                if (MessageBox.Show("Akkauntdan chiqilsinmi?", "Tasdiq", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    DialogResult = DialogResult.Retry;
                    Close();
                }
            };

            SetupActionImageBox(_picExitAction, "Dasturdan chiqish", Color.FromArgb(232, 71, 71), Color.FromArgb(214, 53, 53));
            _picExitAction.Click += (s, e) =>
            {
                if (MessageBox.Show("Dastur yopilsinmi?", "Tasdiq", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            SetupActionImageBox(_picLicenseAction, "License holati", Color.FromArgb(64, 120, 216), Color.FromArgb(52, 104, 194));
            _picLicenseAction.Click += (s, e) => new LicenseStatusForm().ShowDialog(this);

            SetupActionImageBox(_picUpdateAction, "Dastur yangilash", Color.FromArgb(230, 150, 12), Color.FromArgb(204, 120, 0));
            _picUpdateAction.Visible = false;
            _picUpdateAction.Click += UpdateAction_Click;

            _picThemeAction.Size = new Size(220, 44);
            _picThemeAction.Margin = new Padding(6, 0, 6, 0);
            _picThemeAction.SizeMode = PictureBoxSizeMode.Zoom;
            _picThemeAction.Cursor = Cursors.Hand;
            _picThemeAction.BackColor = Color.Transparent;
            _picThemeAction.Paint += (s, e) =>
            {
                if (_picThemeAction.Image != null)
                {
                    return;
                }

                string fallbackText = UiTheme.GetThemeToggleButtonText();
                TextRenderer.DrawText(
                    e.Graphics,
                    fallbackText,
                    new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11, FontStyle.Bold),
                    _picThemeAction.ClientRectangle,
                    UiTheme.CurrentMode == UiTheme.ThemeMode.Light ? Color.FromArgb(46, 65, 96) : Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _picThemeAction.Click += (s, e) =>
            {
                UiTheme.ToggleTheme();
                ApplyThemeStyles();
            };
            AttachActionHoverMagnify(_picThemeAction);

            _actionsFlow.Controls.Add(_picLogoutAction);
            _actionsFlow.Controls.Add(_picExitAction);
            if (AuthorizationService.IsAdmin(_currentUser))
            {
                _actionsFlow.Controls.Add(_picLicenseAction);
            }
            _actionsFlow.Controls.Add(_picUpdateAction);
            _actionsFlow.Controls.Add(_picThemeAction);
            _actionPanel.Controls.Add(_actionsFlow);
            _actionPanel.Resize += (s, e) =>
            {
                _actionsFlow.MaximumSize = new Size(Math.Max(1, _actionPanel.ClientSize.Width), 0);
                _actionsFlow.Left = Math.Max(0, (_actionPanel.ClientSize.Width - _actionsFlow.Width) / 2);
                _actionsFlow.Top = Math.Max(0, (_actionPanel.ClientSize.Height - _actionsFlow.Height) / 2);
            };

            _lblFooter.Text = "(c) 2026 Osontrack SRM tizimi - Barcha huquqlar himoyalangan";
            _lblFooter.Dock = DockStyle.Bottom;
            _lblFooter.Height = 34;
            _lblFooter.TextAlign = ContentAlignment.MiddleCenter;
            _lblFooter.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10, FontStyle.Regular);

            BuildHomeDashboard();
            BuildHomeContent();
            _body.Controls.Add(_homeContentPanel);
            _body.Controls.Add(_actionPanel);
            _body.Controls.Add(_lblFooter);
            _body.Controls.Add(_pageHost);

            _workspace.Dock = DockStyle.Fill;
            _workspace.Controls.Add(_body);
            _workspace.Controls.Add(_header);
            Controls.Add(_workspace);
            Controls.Add(_sidebar);

            _revealTimer.Interval = 70;
            _revealTimer.Tick += RevealTimer_Tick;
            _headerWarningsTimer.Interval = 30000;
            _headerWarningsTimer.Tick += (s, e) => RefreshHeaderWarnings();
            _actionHoverTimer.Interval = 16;
            _actionHoverTimer.Tick += ActionHoverTimer_Tick;
            Shown += (s, e) =>
            {
                LayoutRoleBadge();
                LayoutHeaderCenterText();
                RefreshHeaderWarnings();
                _headerWarningsTimer.Start();
                StartRevealAnimation();
                LoadHomeDashboard();
                _ = CheckForUpdatesAsync();
            };
            FormClosed += (s, e) =>
            {
                _headerWarningsTimer.Stop();
                _revealTimer.Stop();
                _actionHoverTimer.Stop();
                CloseActivePage();
            };

            Resize += (s, e) =>
            {
                LayoutRoleBadge();
                LayoutHeaderCenterText();
                LayoutSidebarTiles();
                UpdateBodyScrollArea();
            };
        }

        private void BuildTiles()
        {
            var defs = new List<TileDefinition>
            {
                new TileDefinition(
                    "Bosh sahifa",
                    "Asosiy sahifaga qaytish",
                    "\uE80F",
                    Color.FromArgb(64, 120, 216),
                    ShowHomePage)
            };

            bool isAdmin = string.Equals(_currentUser.Role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase);
            bool isSeller = string.Equals(_currentUser.Role, DatabaseHelper.RoleSeller, StringComparison.OrdinalIgnoreCase);

            if (isAdmin && AuthorizationService.CanViewProducts(_currentUser))
            {
                defs.Add(new TileDefinition("Tovarlar", "Ombor va narxlar", "\uECAA", Color.FromArgb(20, 143, 230), () => OpenPage(() => new ProductForm(_currentUser)), "tile-products.png"));
            }

            if (AuthorizationService.CanCreateSales(_currentUser))
            {
                defs.Add(new TileDefinition("Sotuv", "Yangi savdo operatsiyasi", "\uE7BF", Color.FromArgb(13, 178, 108), () => OpenPage(() => new SaleForm(_currentUser)), "tile-sales.png"));
            }

            if (AuthorizationService.CanManageExpenses(_currentUser))
            {
                defs.Add(new TileDefinition("Rasxod", "Xarajatlarni boshqarish", "\uE8C7", Color.FromArgb(245, 34, 77), () => OpenPage(() => new ExpenseForm(_currentUser)), "tile-expenses.png"));
            }

            if (AuthorizationService.CanManageDebts(_currentUser))
            {
                defs.Add(new TileDefinition("Mijozlar", "Mijozlar bazasi", "\uE77B", Color.FromArgb(237, 146, 0), () => OpenPage(() => new CustomersForm(_currentUser)), "tile-customers.png"));
            }

            if (isAdmin && AuthorizationService.CanViewReports(_currentUser))
            {
                defs.Add(new TileDefinition("Hisobot", "Tahlil va eksport", "\uE9D2", Color.FromArgb(67, 80, 231), () => OpenPage(() => new ReportForm(_currentUser)), "tile-reports.png"));
            }

            if (isAdmin)
            {
                defs.Add(new TileDefinition("Foydalanuvchilar", "Rol va akkauntlar", "\uE716", Color.FromArgb(75, 88, 114), () => OpenPage(() => new UserManagementForm(_currentUser)), "tile-users.png"));
                defs.Add(new TileDefinition("Backup / Restore", "Zaxira va tiklash", "\uE7F1", Color.FromArgb(75, 88, 114), () => OpenPage(() => new BackupRestoreForm(_currentUser)), "tile-backup.png"));
                defs.Add(new TileDefinition("Chek Rekvizitlari", "Chek sozlamalari", "\uE8A5", Color.FromArgb(32, 129, 214), () => OpenPage(() => new FiscalSettingsForm(_currentUser)), "tile-receipt.png"));
                defs.Add(new TileDefinition("Foiz va chegirmalar", "Chegirma sozlamalari", "%", Color.FromArgb(16, 161, 121), () => OpenPage(() => new PricingDiscountForm(_currentUser)), "tile-discount.png"));
            }

            if (isSeller)
            {
                // Seller uchun faqat ishchi kartalar ko'rinadi.
                defs.RemoveAll(x =>
                    x.Title != "Bosh sahifa" &&
                    x.Title != "Sotuv" &&
                    x.Title != "Rasxod" &&
                    x.Title != "Mijozlar");
            }

            int rows = (defs.Count + (TileColumns - 1)) / TileColumns;
            _tilesGrid.Controls.Clear();
            _tilesGrid.RowStyles.Clear();
            for (int i = 0; i < rows; i++)
            {
                _tilesGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, TileRowHeight));
            }
            _tilesGrid.RowCount = rows;
            _tilesGrid.Height = (rows * TileRowHeight) + 12;
            UpdateBodyScrollArea();

            _tiles.Clear();
            for (int i = 0; i < defs.Count; i++)
            {
                TileUi tile = CreateTile(defs[i]);
                tile.Card.Visible = false;
                _tiles.Add(tile);
                _tilesGrid.Controls.Add(tile.Card, i % TileColumns, i / TileColumns);
            }
            LayoutSidebarTiles();
        }

        private void BuildHomeDashboard()
        {
            _homeDashboardPanel.Dock = DockStyle.Fill;
            _homeDashboardPanel.Padding = new Padding(0, 2, 0, 0);
            _homeDashboardPanel.BackColor = Color.Transparent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));

            _homeStatusPanel.Dock = DockStyle.Fill;
            _homeStatusPanel.Margin = new Padding(0, 0, 0, 8);
            _homeStatusPanel.Paint += HomeStatusPanel_Paint;
            _lblHomeStatus.Dock = DockStyle.Fill;
            _lblHomeStatus.Padding = new Padding(14, 0, 12, 0);
            _lblHomeStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblHomeStatus.Font = UiTheme.BodyFont;
            _lblHomeStatus.AutoEllipsis = true;
            _lblHomeStatus.Cursor = Cursors.Hand;
            _lblHomeStatus.Click += HeaderAlerts_Click;
            _homeStatusPanel.Controls.Add(_lblHomeStatus);
            _homeStatusPanel.Cursor = Cursors.Hand;
            _homeStatusPanel.Click += HeaderAlerts_Click;
            layout.Controls.Add(_homeStatusPanel, 0, 0);

            var cards = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 8),
                Padding = Padding.Empty
            };
            for (int i = 0; i < 4; i++)
            {
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
                var metricCard = CreateDashboardMetricCard();
                metricCard.Card.Margin = new Padding(i == 0 ? 0 : 6, 0, i == 3 ? 0 : 6, 0);
                cards.Controls.Add(metricCard.Card, i, 0);
                _dashboardCards.Add(metricCard);
            }
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(cards, 0, 1);

            var visualRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 8),
                Padding = Padding.Empty
            };
            visualRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));
            visualRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            visualRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _homeDashboardChart.Dock = DockStyle.Fill;
            _homeDashboardChart.Margin = new Padding(0, 0, 6, 0);
            _homeDashboardChart.BackColor = UiTheme.Card;
            _homeDashboardChart.Paint += HomeDashboardChart_Paint;
            visualRow.Controls.Add(_homeDashboardChart, 0, 0);

            _homeDashboardSummary.Dock = DockStyle.Fill;
            _homeDashboardSummary.Margin = new Padding(6, 0, 0, 0);
            _homeDashboardSummary.BackColor = UiTheme.Card;
            _homeDashboardSummary.Paint += HomeDashboardSummary_Paint;
            visualRow.Controls.Add(_homeDashboardSummary, 1, 0);

            layout.Controls.Add(visualRow, 0, 2);

            var quickActionsSection = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            quickActionsSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            quickActionsSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            quickActionsSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _lblDashboardSection.Text = "Tezkor amallar";
            _lblDashboardSection.Dock = DockStyle.Fill;
            _lblDashboardSection.Font = UiTheme.ButtonFont;
            _lblDashboardSection.ForeColor = UiTheme.Text;
            _lblDashboardSection.TextAlign = ContentAlignment.MiddleLeft;
            quickActionsSection.Controls.Add(_lblDashboardSection, 0, 0);

            _homeQuickActions.Dock = DockStyle.Fill;
            _homeQuickActions.WrapContents = false;
            _homeQuickActions.FlowDirection = FlowDirection.LeftToRight;
            _homeQuickActions.Margin = new Padding(-6, 2, -6, 0);
            AddHomeQuickActions();
            quickActionsSection.Controls.Add(_homeQuickActions, 0, 1);
            _homeQuickActions.Resize += (_, __) => LayoutHomeQuickActions();
            layout.Controls.Add(quickActionsSection, 0, 3);

            _btnDashboardRefresh.Text = "Yangilash";
            _btnDashboardRefresh.Size = new Size(108, 36);
            _btnDashboardRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            UiTheme.StyleNeutralButton(_btnDashboardRefresh);
            _btnDashboardRefresh.Click += (_, __) => LoadHomeDashboard();
            _welcomePanel.Controls.Add(_btnDashboardRefresh);

            _btnNewSale.Text = "Yangi sotuv";
            _btnNewSale.Size = new Size(132, 36);
            _btnNewSale.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            UiTheme.StylePrimaryButton(_btnNewSale);
            _btnNewSale.Visible = AuthorizationService.CanCreateSales(_currentUser);
            _btnNewSale.Click += (_, __) =>
            {
                OpenPage(() => new SaleForm(_currentUser));
                UpdateNavigationSelection("Sotuv");
            };
            _welcomePanel.Controls.Add(_btnNewSale);
            _welcomePanel.Resize += (_, __) => LayoutHomeHeaderControls();
            LayoutHomeHeaderControls();
            _homeDashboardPanel.Controls.Add(layout);
        }

        private void BuildHomeContent()
        {
            _homeContentPanel.Dock = DockStyle.Top;
            _homeContentPanel.Height = 610;
            _homeContentPanel.BackColor = Color.Transparent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(_welcomePanel, 0, 0);
            layout.Controls.Add(_homeDashboardPanel, 0, 1);
            _homeContentPanel.Controls.Add(layout);
        }

        private void LayoutHomeHeaderControls()
        {
            const int gap = 8;
            int right = Math.Max(0, _welcomePanel.ClientSize.Width - 2);
            _btnNewSale.Location = new Point(right - _btnNewSale.Width, 8);
            _btnDashboardRefresh.Location = new Point(
                _btnNewSale.Visible
                    ? _btnNewSale.Left - gap - _btnDashboardRefresh.Width
                    : right - _btnDashboardRefresh.Width,
                8);
            int textWidth = Math.Max(120, _btnDashboardRefresh.Left - 16);
            _lblWelcomeTitle.Width = textWidth;
            _lblWelcomeSubtitle.Width = textWidth;
        }

        private void AddHomeQuickActions()
        {
            var actions = new List<(string Title, string Detail, string Icon, Action Open)>
            {
                ("Tovar qo'shish", "Omborga yangi tovar", "\uECAA", () =>
                {
                    OpenPage(() => new ProductForm(_currentUser));
                    UpdateNavigationSelection("Tovarlar");
                }),
                ("Mijoz qo'shish", "Mijozlar bazasiga yozish", "\uE77B", () =>
                {
                    OpenPage(() => new CustomersForm(_currentUser));
                    UpdateNavigationSelection("Mijozlar");
                }),
                ("Xarajat kiritish", "Chiqimni qayd etish", "\uE8C7", () =>
                {
                    OpenPage(() => new ExpenseForm(_currentUser));
                    UpdateNavigationSelection("Rasxod");
                })
            };

            actions.RemoveAll(action =>
                (action.Title == "Tovar qo'shish" && !AuthorizationService.CanViewProducts(_currentUser)) ||
                (action.Title == "Mijoz qo'shish" && !AuthorizationService.CanManageDebts(_currentUser)) ||
                (action.Title == "Xarajat kiritish" && !AuthorizationService.CanManageExpenses(_currentUser)));

            foreach ((string title, string detail, string icon, Action open) in actions)
            {
                _homeQuickActions.Controls.Add(CreateHomeQuickAction(title, detail, icon, open));
            }
        }

        private void LayoutHomeQuickActions()
        {
            int count = _homeQuickActions.Controls.Count;
            if (count == 0)
            {
                return;
            }

            int width = Math.Max(150, (_homeQuickActions.ClientSize.Width - count * 12) / count);
            foreach (Control control in _homeQuickActions.Controls)
            {
                control.Width = width;
            }
        }

        private Panel CreateHomeQuickAction(string title, string detail, string icon, Action open)
        {
            var card = new Panel
            {
                Width = 260,
                Height = 68,
                BackColor = UiTheme.Card,
                Cursor = Cursors.Hand,
                Tag = "home-quick-action"
            };
            var iconLabel = new Label
            {
                Text = icon,
                Font = UiTheme.IconFont(18),
                ForeColor = UiTheme.Primary,
                BackColor = UiTheme.Background,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(12, 14, 40, 40)
            };
            var titleLabel = new Label
            {
                Text = title,
                Font = UiTheme.ButtonFont,
                ForeColor = UiTheme.Text,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Bounds = new Rectangle(62, 12, 190, 22)
            };
            var detailLabel = new Label
            {
                Text = detail,
                Font = UiTheme.BodyFont,
                ForeColor = UiTheme.Muted,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Bounds = new Rectangle(62, 36, 190, 20)
            };
            card.Controls.Add(iconLabel);
            card.Controls.Add(titleLabel);
            card.Controls.Add(detailLabel);
            card.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using GraphicsPath path = RoundedRect(bounds, 9);
                using SolidBrush fill = new SolidBrush(UiTheme.Card);
                using Pen border = new Pen(UiTheme.Border, 1);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            card.Resize += (_, __) =>
            {
                int textWidth = Math.Max(1, card.ClientSize.Width - 76);
                titleLabel.Width = textWidth;
                detailLabel.Width = textWidth;
            };
            card.Click += (_, __) => open();
            iconLabel.Click += (_, __) => open();
            titleLabel.Click += (_, __) => open();
            detailLabel.Click += (_, __) => open();
            return card;
        }

        private static (Panel Card, Label Title, Label Value, Label Detail) CreateDashboardMetricCard()
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Card,
                Padding = new Padding(12, 7, 10, 6)
            };
            var title = new Label
            {
                Dock = DockStyle.Top,
                Height = 19,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.BodyFont,
                ForeColor = UiTheme.Muted,
                BackColor = Color.Transparent
            };
            var value = new Label
            {
                Dock = DockStyle.Top,
                Height = 29,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 16, FontStyle.Bold),
                ForeColor = UiTheme.Text,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            var detail = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 8f, FontStyle.Regular),
                ForeColor = UiTheme.Muted,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            card.Controls.Add(detail);
            card.Controls.Add(value);
            card.Controls.Add(title);
            card.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using GraphicsPath path = RoundedRect(bounds, 10);
                using SolidBrush fill = new SolidBrush(UiTheme.Card);
                using Pen border = new Pen(UiTheme.Border, 1);
                using Pen accent = new Pen(UiTheme.Primary, 3);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                e.Graphics.DrawLine(accent, 10, 12, 10, card.Height - 12);
            };
            return (card, title, value, detail);
        }

        private void LoadHomeDashboard()
        {
            try
            {
                _homeDashboardSnapshot = _homeDashboardService.GetSnapshot(_currentUser, DateTime.Today);
                _homeDashboardErrorMessage = null;
                UpdateHomeStatus();
                _btnDashboardRefresh.Enabled = true;
                UpdateHomeDashboardCards(_homeDashboardSnapshot);
                _homeDashboardChart.Invalidate();
                _homeDashboardSummary.Invalidate();
            }
            catch (Exception ex)
            {
                _homeDashboardSnapshot = null;
                _homeDashboardErrorMessage = $"Ko'rsatkichlarni yuklab bo'lmadi: {ex.Message}";
                UpdateHomeStatus();
                _btnDashboardRefresh.Enabled = true;
                foreach (var card in _dashboardCards)
                {
                    card.Value.Text = "—";
                    card.Detail.Text = string.Empty;
                }
                _homeDashboardChart.Invalidate();
                _homeDashboardSummary.Invalidate();
            }
        }

        private void UpdateHomeStatus()
        {
            string warnings = GetCriticalHeaderWarningsText();
            bool isHealthy = warnings.StartsWith("OK:", StringComparison.OrdinalIgnoreCase);
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            _lblHomeStatus.Text = _homeDashboardErrorMessage
                ?? (isHealthy
                    ? "Muhim ogohlantirishlar yo'q. Barcha ko'rsatkichlar me'yorda."
                    : warnings);
            _lblHomeStatus.ForeColor = _homeDashboardErrorMessage != null
                ? (isLight ? Color.FromArgb(165, 49, 45) : Color.FromArgb(255, 151, 145))
                : isHealthy
                    ? (isLight ? Color.FromArgb(22, 126, 78) : Color.FromArgb(117, 228, 169))
                    : (isLight ? Color.FromArgb(157, 91, 0) : Color.FromArgb(255, 196, 84));
            _homeStatusPanel.Invalidate();
        }

        private void HomeStatusPanel_Paint(object? sender, PaintEventArgs e)
        {
            Panel panel = _homeStatusPanel;
            if (panel.ClientSize.Width <= 0 || panel.ClientSize.Height <= 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using GraphicsPath path = RoundedRect(bounds, 8);
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            Color fillColor = _homeDashboardErrorMessage != null
                ? (isLight ? Color.FromArgb(253, 235, 234) : Color.FromArgb(57, 34, 34))
                : _lblHomeStatus.Text.StartsWith("Muhim ogohlantirishlar", StringComparison.Ordinal)
                    ? (isLight ? Color.FromArgb(231, 244, 238) : Color.FromArgb(30, 52, 45))
                    : (isLight ? Color.FromArgb(250, 243, 224) : Color.FromArgb(54, 46, 31));
            using SolidBrush fill = new SolidBrush(fillColor);
            e.Graphics.FillPath(fill, path);
        }

        private void UpdateHomeDashboardCards(HomeDashboardSnapshot snapshot)
        {
            string[] titles = snapshot.IsAdministrator
                ? new[] { "Bugungi sotuv", "Sof foyda", "Bugungi xarajat", "Qarz qoldig'i" }
                : new[] { "Jami mijozlar", "Faol qarzlar", "Muddati o'tgan", "Qarz qoldig'i" };
            string[] values = snapshot.IsAdministrator
                ? new[]
                {
                    $"{snapshot.TodaySalesUZS:N0}",
                    $"{snapshot.TodayNetUZS:N0}",
                    $"{snapshot.TodayExpensesUZS:N0}",
                    $"{snapshot.OutstandingUZS:N0}"
                }
                : new[]
                {
                    snapshot.CustomerCount.ToString("N0"),
                    snapshot.OpenDebts.ToString("N0"),
                    snapshot.OverdueDebts.ToString("N0"),
                    $"{snapshot.OutstandingUZS:N0}"
                };
            string[] details = snapshot.IsAdministrator
                ? new[]
                {
                    "UZS  Bugun yakunlangan sotuvlar",
                    $"UZS  Foyda: {snapshot.TodayProfitUZS:N0}",
                    "UZS  Bugun qayd etilgan xarajatlar",
                    $"UZS  Muddati o'tgan: {snapshot.OverdueUZS:N0}"
                }
                : new[]
                {
                    "Mijozlar bazasidagi jami",
                    "Ochiq qarzlar soni",
                    "To'lov muddati o'tgan qarzlar",
                    $"UZS  Muddati o'tgan: {snapshot.OverdueUZS:N0}"
                };

            for (int i = 0; i < _dashboardCards.Count; i++)
            {
                _dashboardCards[i].Title.Text = titles[i];
                _dashboardCards[i].Value.Text = values[i];
                _dashboardCards[i].Detail.Text = details[i];
                _dashboardCards[i].Card.Invalidate();
            }

            if (snapshot.IsAdministrator)
            {
                _dashboardCards[0].Value.ForeColor = DashboardSalesColor;
                _dashboardCards[1].Value.ForeColor = snapshot.TodayNetUZS < 0 ? UiTheme.Danger : UiTheme.Accent;
                _dashboardCards[3].Value.ForeColor = snapshot.OutstandingUZS > 0 ? UiTheme.Primary : UiTheme.Text;
            }
            else
            {
                _dashboardCards[2].Value.ForeColor = snapshot.OverdueDebts > 0 ? UiTheme.Danger : UiTheme.Text;
                _dashboardCards[3].Value.ForeColor = snapshot.OutstandingUZS > 0 ? UiTheme.Primary : UiTheme.Text;
            }
        }

        private void HomeDashboardChart_Paint(object? sender, PaintEventArgs e)
        {
            HomeDashboardSnapshot? snapshot = _homeDashboardSnapshot;
            Panel chart = _homeDashboardChart;
            if (chart.ClientSize.Width < 120 || chart.ClientSize.Height < 100)
            {
                return;
            }

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, chart.Width - 1, chart.Height - 1);
            using (GraphicsPath path = RoundedRect(bounds, 10))
            using (SolidBrush fill = new SolidBrush(UiTheme.Card))
            using (Pen border = new Pen(UiTheme.Border, 1))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }

            string chartTitle = snapshot?.IsAdministrator == true
                ? "Oxirgi 7 kun"
                : "Qarzlar soni";
            TextRenderer.DrawText(
                graphics,
                chartTitle,
                UiTheme.ButtonFont,
                new Rectangle(16, 8, 190, 24),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (snapshot?.IsAdministrator == true)
            {
                TextRenderer.DrawText(
                    graphics,
                    "Sotuv va xarajatlar dinamikasi",
                    UiTheme.BodyFont,
                    new Rectangle(16, 31, 240, 19),
                    UiTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if (snapshot == null)
            {
                TextRenderer.DrawText(
                    graphics,
                    "Ma'lumot mavjud emas. Qayta yuklash uchun yuqoridagi tugmani bosing.",
                    UiTheme.BodyFont,
                    new Rectangle(16, 42, chart.Width - 32, 30),
                    UiTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            DrawDashboardLegend(graphics, chart.Width, snapshot.IsAdministrator);
            if (snapshot.IsAdministrator)
            {
                DrawAdminDashboardChart(graphics, chart.ClientRectangle, snapshot);
            }
            else
            {
                DrawDebtDashboardChart(graphics, chart.ClientRectangle, snapshot);
            }
        }

        private void HomeDashboardSummary_Paint(object? sender, PaintEventArgs e)
        {
            HomeDashboardSnapshot? snapshot = _homeDashboardSnapshot;
            Panel panel = _homeDashboardSummary;
            if (panel.ClientSize.Width < 150 || panel.ClientSize.Height < 100)
            {
                return;
            }

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using (GraphicsPath path = RoundedRect(bounds, 10))
            using (SolidBrush fill = new SolidBrush(UiTheme.Card))
            using (Pen border = new Pen(UiTheme.Border, 1))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }

            TextRenderer.DrawText(
                graphics,
                "Do'kon holati",
                UiTheme.ButtonFont,
                new Rectangle(14, 8, panel.Width - 28, 22),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (snapshot == null)
            {
                return;
            }

            if (snapshot.IsAdministrator)
            {
                DrawDashboardStatusBar(
                    graphics,
                    panel.ClientSize,
                    y: 44,
                    "Ombordagi tovarlar",
                    $"{snapshot.ProductCount:N0} ta jami  |  {snapshot.LowStockCount:N0} ta kam",
                    Math.Max(0, snapshot.ProductCount - snapshot.LowStockCount),
                    snapshot.LowStockCount,
                    Color.FromArgb(48, 148, 112),
                    Color.FromArgb(222, 144, 55));
                DrawDashboardStatusBar(
                    graphics,
                    panel.ClientSize,
                    y: 112,
                    "Qarzlar",
                    $"{snapshot.OpenDebts:N0} ta faol  |  {snapshot.OverdueDebts:N0} ta muddati o'tgan",
                    snapshot.OpenDebts,
                    snapshot.OverdueDebts,
                    UiTheme.Primary,
                    Color.FromArgb(222, 112, 78));
            }
            else
            {
                TextRenderer.DrawText(
                    graphics,
                    "Mijozlar bazasi",
                    UiTheme.BodyFont,
                    new Rectangle(14, 48, panel.Width - 28, 20),
                    UiTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                using Font customerValueFont = new Font(UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 20, FontStyle.Bold);
                TextRenderer.DrawText(
                    graphics,
                    snapshot.CustomerCount.ToString("N0"),
                    customerValueFont,
                    new Rectangle(14, 68, panel.Width - 28, 30),
                    UiTheme.Primary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                DrawDashboardStatusBar(
                    graphics,
                    panel.ClientSize,
                    y: 118,
                    "Qarzlar",
                    $"{snapshot.OpenDebts:N0} ta faol  |  {snapshot.OverdueDebts:N0} ta muddati o'tgan",
                    snapshot.OpenDebts,
                    snapshot.OverdueDebts,
                    UiTheme.Primary,
                    Color.FromArgb(222, 112, 78));
            }
        }

        private static void DrawDashboardStatusBar(
            Graphics graphics,
            Size bounds,
            int y,
            string title,
            string valueText,
            double normalValue,
            double warningValue,
            Color normalColor,
            Color warningColor)
        {
            int contentWidth = Math.Max(1, bounds.Width - 28);
            using Font labelFont = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 8.5f, FontStyle.Regular);
            using Font valueFont = new Font(UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 8f, FontStyle.Bold);
            TextRenderer.DrawText(
                graphics,
                title,
                labelFont,
                new Rectangle(14, y, contentWidth, 18),
                UiTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(
                graphics,
                valueText,
                valueFont,
                new Rectangle(14, y + 18, contentWidth, 18),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            int barY = y + 42;
            int barHeight = 10;
            using GraphicsPath barPath = RoundedRect(new Rectangle(14, barY, contentWidth, barHeight), 5);
            using SolidBrush trackBrush = new SolidBrush(UiTheme.Border);
            graphics.FillPath(trackBrush, barPath);

            double total = normalValue + warningValue;
            if (total <= 0)
            {
                return;
            }

            int normalWidth = (int)Math.Round(contentWidth * normalValue / total);
            int warningWidth = contentWidth - normalWidth;
            GraphicsState state = graphics.Save();
            graphics.SetClip(barPath);
            if (normalWidth > 0)
            {
                using SolidBrush brush = new SolidBrush(normalColor);
                graphics.FillRectangle(brush, 14, barY, normalWidth, barHeight);
            }

            if (warningWidth > 0)
            {
                using SolidBrush brush = new SolidBrush(warningColor);
                graphics.FillRectangle(brush, 14 + normalWidth, barY, warningWidth, barHeight);
            }
            graphics.Restore(state);
        }

        private static void DrawDashboardLegend(Graphics graphics, int width, bool isAdministrator)
        {
            Color firstColor = isAdministrator ? DashboardSalesColor : UiTheme.Primary;
            Color secondColor = isAdministrator ? UiTheme.Accent : Color.FromArgb(224, 120, 55);
            string firstLabel = isAdministrator ? "Sotuv" : "Faol";
            string secondLabel = isAdministrator ? "Xarajat" : "Muddati o'tgan";
            Font font = UiTheme.BodyFont;
            int y = 16;
            int right = width - 16;

            Size secondSize = TextRenderer.MeasureText(secondLabel, font);
            right -= secondSize.Width;
            TextRenderer.DrawText(graphics, secondLabel, font, new Point(right, y), UiTheme.Muted);
            right -= 16;
            using (SolidBrush brush = new SolidBrush(secondColor))
            {
                graphics.FillRectangle(brush, right, y + 5, 8, 8);
            }
            right -= 10;

            Size firstSize = TextRenderer.MeasureText(firstLabel, font);
            right -= firstSize.Width;
            TextRenderer.DrawText(graphics, firstLabel, font, new Point(right, y), UiTheme.Muted);
            right -= 16;
            using SolidBrush firstBrush = new SolidBrush(firstColor);
            graphics.FillRectangle(firstBrush, right, y + 5, 8, 8);
        }

        private static void DrawAdminDashboardChart(Graphics graphics, Rectangle bounds, HomeDashboardSnapshot snapshot)
        {
            if (snapshot.Week.Count == 0)
            {
                return;
            }

            double max = Math.Max(0, snapshot.Week.Max(day => Math.Max(day.SalesUZS, day.ExpensesUZS)));
            DrawDashboardBars(
                graphics,
                bounds,
                snapshot.Week.Count,
                max,
                index => snapshot.Week[index].SalesUZS,
                index => snapshot.Week[index].ExpensesUZS,
                index => snapshot.Week[index].Date.ToString("dd.MM", CultureInfo.InvariantCulture),
                isCurrency: true);
        }

        private static void DrawDebtDashboardChart(Graphics graphics, Rectangle bounds, HomeDashboardSnapshot snapshot)
        {
            int max = Math.Max(1, Math.Max(snapshot.OpenDebts, snapshot.OverdueDebts));
            DrawDashboardBars(
                graphics,
                bounds,
                1,
                max,
                _ => snapshot.OpenDebts,
                _ => snapshot.OverdueDebts,
                _ => "Qarzlar",
                isCurrency: false,
                secondSeriesColor: Color.FromArgb(224, 120, 55));
        }

        private static void DrawDashboardBars(
            Graphics graphics,
            Rectangle bounds,
            int itemCount,
            double maxValue,
            Func<int, double> firstValue,
            Func<int, double> secondValue,
            Func<int, string> itemLabel,
            bool isCurrency,
            Color? secondSeriesColor = null)
        {
            int left = 54;
            int right = bounds.Width - 20;
            int top = 54;
            int bottom = bounds.Height - 32;
            int plotHeight = bottom - top;
            int plotWidth = right - left;
            if (plotHeight < 24 || plotWidth < 20 || itemCount <= 0)
            {
                return;
            }

            maxValue = maxValue <= 0 ? 1 : maxValue * 1.15;
            using (Pen gridPen = new Pen(UiTheme.Border, 1))
            using (SolidBrush firstBrush = new SolidBrush(isCurrency ? DashboardSalesColor : UiTheme.Primary))
            using (SolidBrush secondBrush = new SolidBrush(secondSeriesColor ?? UiTheme.Accent))
            using (Font axisFont = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 8f))
            {
                for (int line = 0; line <= 3; line++)
                {
                    int y = top + (plotHeight * line / 3);
                    graphics.DrawLine(gridPen, left, y, right, y);
                    double labelValue = maxValue * (3 - line) / 3;
                    string label = isCurrency
                        ? FormatCompactCurrency(labelValue)
                        : Math.Round(labelValue).ToString("N0", CultureInfo.InvariantCulture);
                    TextRenderer.DrawText(
                        graphics,
                        label,
                        axisFont,
                        new Rectangle(4, y - 8, left - 10, 16),
                        UiTheme.Muted,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                float slot = (float)plotWidth / itemCount;
                float barWidth = Math.Min(22f, Math.Max(5f, slot * (itemCount == 1 ? 0.22f : 0.27f)));
                for (int i = 0; i < itemCount; i++)
                {
                    double firstMetric = Math.Max(0, firstValue(i));
                    double secondMetric = Math.Max(0, secondValue(i));
                    float center = left + (slot * i) + (slot / 2f);
                    float firstHeight = (float)(firstMetric / maxValue * plotHeight);
                    float secondHeight = (float)(secondMetric / maxValue * plotHeight);
                    graphics.FillRectangle(firstBrush, center - barWidth - 1, bottom - firstHeight, barWidth, firstHeight);
                    graphics.FillRectangle(secondBrush, center + 1, bottom - secondHeight, barWidth, secondHeight);
                    if (firstMetric > 0)
                    {
                        TextRenderer.DrawText(
                            graphics,
                            isCurrency ? FormatCompactCurrency(firstMetric) : firstMetric.ToString("N0", CultureInfo.InvariantCulture),
                            axisFont,
                            new Rectangle((int)(center - slot / 2f), Math.Max(top - 1, bottom - (int)firstHeight - 18), (int)slot, 16),
                            UiTheme.Text,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }
                    TextRenderer.DrawText(
                        graphics,
                        itemLabel(i),
                        axisFont,
                        new Rectangle((int)(center - slot / 2f), bottom + 5, (int)slot, 18),
                        UiTheme.Muted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private static string FormatCompactCurrency(double value)
        {
            if (value >= 1_000_000_000)
            {
                return $"{value / 1_000_000_000:0.#} mlrd";
            }

            if (value >= 1_000_000)
            {
                return $"{value / 1_000_000:0.#} mln";
            }

            if (value >= 1_000)
            {
                return $"{value / 1_000:0.#} ming";
            }

            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void OpenPage(Func<Form> createPage)
        {
            ArgumentNullException.ThrowIfNull(createPage);

            Form page = createPage();
            CloseActivePage();

            _activePagePreferredSize = page is ProductForm
                ? new Size(Math.Max(900, _pageHost.ClientSize.Width), Math.Max(620, _pageHost.ClientSize.Height))
                : page.Size;
            page.TopLevel = false;
            page.FormBorderStyle = FormBorderStyle.None;
            page.StartPosition = FormStartPosition.Manual;
            page.BackColor = UiTheme.BackgroundSecondary;
            page.Font = UiTheme.BodyFont;
            page.Location = Point.Empty;
            if (page is ProductForm)
            {
                page.MinimumSize = new Size(900, 620);
                page.Size = _activePagePreferredSize;
            }

            _activePage = page;
            _body.AutoScroll = false;
            _homeContentPanel.Visible = false;
            _actionPanel.Visible = false;
            _lblFooter.Visible = false;
            _pageHost.Visible = true;
            _pageHost.Controls.Add(page);
            _pageHost.Resize += PageHost_Resize;
            _header.Height = 68;
            LayoutHeaderCenterText();
            page.FormClosed += (_, __) =>
            {
                if (!ReferenceEquals(_activePage, page))
                {
                    return;
                }

                _activePage = null;
                _pageHost.Resize -= PageHost_Resize;
                _pageHost.Controls.Remove(page);
                _pageHost.Visible = false;
                _pageHost.AutoScrollMinSize = Size.Empty;
                _actionPanel.Visible = true;
                _lblFooter.Visible = true;
                _homeContentPanel.Visible = true;
                _body.AutoScroll = true;
                _header.Height = 80;
                LayoutHeaderCenterText();
                UpdateNavigationSelection("Bosh sahifa");
                UpdateBodyScrollArea();
                LoadHomeDashboard();
            };
            LayoutActivePage();
            page.Show();
            page.BringToFront();
            _body.PerformLayout();
        }

        private void ShowHomePage()
        {
            CloseActivePage();
            UpdateNavigationSelection("Bosh sahifa");
            _pageHost.Visible = false;
            _pageHost.AutoScrollMinSize = Size.Empty;
            _actionPanel.Visible = true;
            _lblFooter.Visible = true;
            _homeContentPanel.Visible = true;
            _body.AutoScroll = true;
            _header.Height = 80;
            LayoutHeaderCenterText();
            UpdateBodyScrollArea();
            _body.PerformLayout();
            LoadHomeDashboard();
        }

        private void UpdateNavigationSelection(string title)
        {
            _selectedNavigationTitle = title;
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            foreach (TileUi tile in _tiles)
            {
                bool selected = string.Equals(tile.Title.Text, title, StringComparison.Ordinal);
                tile.BaseColor = selected
                    ? DashboardSalesColor
                    : isLight
                        ? Color.FromArgb(20, 39, 62)
                        : Color.FromArgb(19, 34, 55);
                tile.HoverColor = selected
                    ? Color.FromArgb(77, 77, 220)
                    : isLight
                        ? Color.FromArgb(34, 57, 86)
                        : Color.FromArgb(29, 55, 87);
                tile.Card.BackColor = tile.HoverProgress > 0
                    ? LerpColor(tile.BaseColor, tile.HoverColor, tile.HoverProgress)
                    : tile.BaseColor;
                tile.Card.Invalidate();
            }
        }

        private void ToggleSidebar()
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            _sidebar.SuspendLayout();
            _sidebar.Width = _sidebarCollapsed ? 64 : 248;
            _sidebar.Padding = _sidebarCollapsed
                ? new Padding(6, 8, 6, 8)
                : new Padding(12, 10, 12, 12);
            _sidebarBrand.Height = 58;
            _lblSidebarCaption.Visible = !_sidebarCollapsed;
            _tilesGrid.Visible = true;
            _sidebarToggleButton.SetBounds(_sidebarCollapsed ? 6 : 4, 8, 40, 40);
            _sidebarToggleButton.Text = _sidebarCollapsed ? "\u203A" : "\u2039";
            _sidebarToggleButton.AccessibleName = _sidebarCollapsed
                ? "Yon menyuni ochish"
                : "Yon menyuni yig'ish";
            _navigationToolTip.SetToolTip(_sidebarToggleButton, _sidebarToggleButton.AccessibleName);
            _sidebarToggleButton.BringToFront();
            LayoutSidebarTiles();
            _sidebar.ResumeLayout(true);

            if (_activePage != null)
            {
                LayoutActivePage();
            }
        }

        private void CloseActivePage()
        {
            if (_activePage == null)
            {
                return;
            }

            Form page = _activePage;
            _activePage = null;
            _pageHost.Resize -= PageHost_Resize;
            _pageHost.Controls.Remove(page);
            if (!page.IsDisposed)
            {
                page.Close();
                page.Dispose();
            }
        }

        private void PageHost_Resize(object? sender, EventArgs e)
        {
            LayoutActivePage();
        }

        private void LayoutActivePage()
        {
            if (_activePage == null || _activePage.IsDisposed)
            {
                return;
            }

            Size hostSize = _pageHost.ClientSize;
            Size minimumSize = _activePage.MinimumSize;
            int width;
            int height;
            if (_activePage is ProductForm)
            {
                width = Math.Max(hostSize.Width, minimumSize.Width);
                height = Math.Max(hostSize.Height, minimumSize.Height);
            }
            else
            {
                width = Math.Max(hostSize.Width, Math.Max(_activePagePreferredSize.Width, minimumSize.Width));
                height = Math.Max(hostSize.Height, Math.Max(_activePagePreferredSize.Height, minimumSize.Height));
            }

            _activePage.Size = new Size(width, height);
            _pageHost.AutoScrollMinSize = _activePage.Size;
        }

        private TileUi CreateTile(TileDefinition def)
        {
            Panel card = new Panel
            {
                Margin = new Padding(3),
                Padding = new Padding(8, 6, 8, 6),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill
            };
            UpdateCardRegion(card);

            Panel iconBox = new Panel
            {
                Size = Size.Empty,
                Visible = false
            };
            Label iconControl = new Label
            {
                Text = def.Icon,
                Font = UiTheme.IconFont(18),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = _sidebarCollapsed,
                Cursor = Cursors.Hand
            };

            Label title = new Label
            {
                Text = def.Title,
                AutoSize = false,
                Height = 24,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11.5f, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            Label subtitle = new Label
            {
                Text = def.Subtitle,
                AutoSize = false,
                Height = 20,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 9, FontStyle.Regular),
                BackColor = Color.Transparent
            };
            UpdateTileLayout(card, iconControl, title, subtitle, _sidebarCollapsed);

            EventHandler click = (s, e) =>
            {
                def.OnClick();
                UpdateNavigationSelection(def.Title);
            };
            card.Click += click;
            iconBox.Click += click;
            iconControl.Click += click;
            title.Click += click;
            subtitle.Click += click;

            card.Controls.Add(iconBox);
            card.Controls.Add(iconControl);
            card.Controls.Add(title);
            card.Controls.Add(subtitle);
            card.Resize += (s, e) =>
            {
                UpdateCardRegion(card);
                UpdateTileLayout(card, iconControl, title, subtitle, _sidebarCollapsed);
            };
            _navigationToolTip.SetToolTip(card, def.Title);
            _navigationToolTip.SetToolTip(iconControl, def.Title);
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle borderBounds = new Rectangle(1, 1, Math.Max(1, card.Width - 3), Math.Max(1, card.Height - 3));
                using GraphicsPath borderPath = RoundedRect(borderBounds, 16);
                using Pen border = new Pen(UiTheme.Border, 1f);
                e.Graphics.DrawPath(border, borderPath);
            };

            var tileUi = new TileUi(card, title, subtitle, iconBox, iconControl, false);
            AttachTileHoverAnimation(tileUi);
            return tileUi;
        }

        private Button CreateActionButton(string text, string style)
        {
            Button button = new Button
            {
                Text = text,
                Width = 220,
                Height = 44,
                Margin = new Padding(6, 0, 6, 0),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            if (style == "danger")
            {
                UiTheme.StyleDangerButton(button);
            }
            else if (style == "neutral")
            {
                UiTheme.StyleNeutralButton(button);
            }
            else
            {
                UiTheme.StylePrimaryButton(button);
            }
            button.FlatAppearance.BorderSize = 2;
            ApplyActionButtonBorder(button, style);

            AttachPressAnimation(button);
            button.Resize += (s, e) =>
            {
                int radius = Math.Max(10, button.Height / 2);
                button.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, button.Width), Math.Max(1, button.Height)), radius));
            };
            button.Region = new Region(RoundedRect(new Rectangle(0, 0, button.Width, button.Height), Math.Max(10, button.Height / 2)));
            return button;
        }

        private void ApplyThemeStyles()
        {
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            BackColor = isLight ? Color.FromArgb(236, 243, 253) : Color.FromArgb(12, 18, 29);
            _header.BackColor = isLight ? Color.FromArgb(222, 234, 252) : Color.FromArgb(22, 38, 63);
            _actionPanel.BackColor = BackColor;
            _actionsFlow.BackColor = BackColor;
            _body.BackColor = BackColor;
            _pageHost.BackColor = UiTheme.Background;
            _tilesGrid.BackColor = BackColor;
            _homeDashboardPanel.BackColor = BackColor;
            _homeDashboardChart.BackColor = UiTheme.Card;
            _homeDashboardSummary.BackColor = UiTheme.Card;
            _homeStatusPanel.BackColor = BackColor;
            _btnDashboardRefresh.BackColor = isLight ? Color.FromArgb(87, 102, 122) : Color.FromArgb(58, 79, 103);
            _btnDashboardRefresh.ForeColor = Color.White;
            _btnDashboardRefresh.FlatAppearance.MouseOverBackColor = isLight
                ? Color.FromArgb(70, 85, 106)
                : Color.FromArgb(71, 94, 121);
            UiTheme.StyleNeutralButton(_btnDashboardRefresh);
            if (_btnNewSale.Visible)
            {
                UiTheme.StylePrimaryButton(_btnNewSale);
            }
            foreach (var card in _dashboardCards)
            {
                card.Card.Invalidate();
                card.Title.ForeColor = UiTheme.Muted;
                card.Detail.ForeColor = UiTheme.Muted;
                card.Value.ForeColor = UiTheme.Text;
            }
            if (_homeDashboardSnapshot != null)
            {
                UpdateHomeDashboardCards(_homeDashboardSnapshot);
            }
            _homeDashboardChart.Invalidate();
            _homeDashboardSummary.Invalidate();
            UpdateHomeStatus();

            _lblTitleOson.BackColor = Color.Transparent;
            _lblHeaderAlerts.BackColor = Color.Transparent;
            _lblRole.BackColor = Color.Transparent;
            _lblTitleOson.ForeColor = isLight ? Color.FromArgb(26, 48, 82) : Color.FromArgb(238, 245, 255);
            _lblHeaderAlerts.ForeColor = isLight ? Color.FromArgb(157, 91, 0) : Color.FromArgb(255, 196, 84);
            UpdateThemeActionImage();
            UpdateActionImages();
            _roleTextBaseColor = isLight ? Color.FromArgb(44, 104, 186) : Color.FromArgb(168, 212, 255);
            UpdateRoleTextColor();
            _lblFooter.ForeColor = isLight ? Color.FromArgb(90, 112, 147) : Color.FromArgb(134, 156, 188);

            foreach (TileUi tile in _tiles)
            {
                tile.BaseColor = isLight ? Color.FromArgb(20, 39, 62) : Color.FromArgb(19, 34, 55);
                tile.HoverColor = isLight ? Color.FromArgb(34, 57, 86) : Color.FromArgb(29, 55, 87);
                tile.Card.BackColor = tile.BaseColor;
                tile.Title.ForeColor = Color.FromArgb(245, 249, 255);
                tile.Subtitle.ForeColor = isLight ? Color.FromArgb(193, 207, 225) : Color.FromArgb(165, 180, 199);
                tile.Card.Invalidate();
            }
            UpdateNavigationSelection(_selectedNavigationTitle);

            _sidebar.BackColor = isLight ? Color.FromArgb(20, 37, 59) : Color.FromArgb(13, 24, 41);
            _sidebarBrand.BackColor = _sidebar.BackColor;
            _sidebarToggleButton.BackColor = _sidebar.BackColor;
            _sidebarBrand.Invalidate();
            _lblSidebarCaption.ForeColor = isLight ? Color.FromArgb(177, 205, 244) : Color.FromArgb(144, 169, 202);
            _tilesGrid.BackColor = _sidebar.BackColor;
            _welcomePanel.BackColor = Color.Transparent;
            _lblWelcomeTitle.ForeColor = UiTheme.Text;
            _lblWelcomeSubtitle.ForeColor = UiTheme.Muted;
            _lblDashboardSection.ForeColor = UiTheme.Text;
            foreach (Control control in _homeQuickActions.Controls)
            {
                if (control is not Panel actionCard || actionCard.Controls.Count < 3)
                {
                    continue;
                }

                actionCard.Controls[0].ForeColor = UiTheme.Primary;
                actionCard.Controls[0].BackColor = UiTheme.Background;
                actionCard.Controls[1].ForeColor = UiTheme.Text;
                actionCard.Controls[2].ForeColor = UiTheme.Muted;
                actionCard.Invalidate();
            }

            foreach (Control panelControl in _actionPanel.Controls)
            {
                if (panelControl is not FlowLayoutPanel flow)
                {
                    continue;
                }

                foreach (Control control in flow.Controls)
                {
                    if (control is not Button actionButton)
                    {
                        continue;
                    }

                    string style = actionButton.Tag?.ToString() switch
                    {
                        "btn:danger" => "danger",
                        "btn:neutral" => "neutral",
                        _ => "primary"
                    };
                    ApplyActionButtonBorder(actionButton, style);
                }
            }

            Invalidate(true);
        }

        private void UpdateThemeActionImage()
        {
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            string fileName = isLight ? "theme-dark.png" : "theme-light.png";
            Image? image = BrandingAssets.TryLoadAssetImage(fileName);

            _picThemeAction.Image = image;
            _picThemeAction.BorderStyle = image == null ? BorderStyle.FixedSingle : BorderStyle.None;
        }

        private void SetupActionImageBox(PictureBox box, string text, Color c1, Color c2)
        {
            box.Size = new Size(220, 44);
            box.Margin = new Padding(6, 0, 6, 0);
            box.SizeMode = PictureBoxSizeMode.Zoom;
            box.Cursor = Cursors.Hand;
            box.BackColor = Color.Transparent;
            box.Tag = Tuple.Create(text, c1, c2);
            box.Paint += (s, e) =>
            {
                if (box.Image != null)
                {
                    return;
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, Math.Max(1, box.Width - 1), Math.Max(1, box.Height - 1));
                using GraphicsPath path = RoundedRect(rect, Math.Max(10, box.Height / 2));
                using LinearGradientBrush lg = new LinearGradientBrush(rect, c1, c2, 0f);
                e.Graphics.FillPath(lg, path);
                using Pen edge = new Pen(Color.FromArgb(130, 255, 255, 255), 1f);
                e.Graphics.DrawPath(edge, path);

                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11, FontStyle.Bold),
                    rect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            AttachActionHoverMagnify(box);
        }

        private void AttachActionHoverMagnify(PictureBox box)
        {
            if (_actionHoverStates.ContainsKey(box))
            {
                return;
            }

            _actionHoverStates[box] = new ActionHoverState();

            box.MouseEnter += (s, e) =>
            {
                if (_actionHoverStates.TryGetValue(box, out ActionHoverState? st))
                {
                    st.Target = 1f;
                    _actionHoverStates[box] = st;
                }

                if (!_actionHoverTimer.Enabled)
                {
                    _actionHoverTimer.Start();
                }
            };

            box.MouseLeave += (s, e) =>
            {
                if (_actionHoverStates.TryGetValue(box, out ActionHoverState? st))
                {
                    st.Target = 0f;
                    _actionHoverStates[box] = st;
                }

                if (!_actionHoverTimer.Enabled)
                {
                    _actionHoverTimer.Start();
                }
            };

            box.Paint += (s, e) =>
            {
                if (box.Image == null)
                {
                    return;
                }

                if (!_actionHoverStates.TryGetValue(box, out ActionHoverState? st) || st == null || st.Progress <= 0.001f)
                {
                    return;
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Color bg = box.Parent?.BackColor ?? BackColor;
                using SolidBrush bgBrush = new SolidBrush(bg);
                e.Graphics.FillRectangle(bgBrush, box.ClientRectangle);

                Rectangle baseRect = GetZoomFitRect(box.ClientRectangle, box.Image.Size);
                float scale = 1f + (0.12f * st.Progress);
                int w = (int)Math.Round(baseRect.Width * scale);
                int h = (int)Math.Round(baseRect.Height * scale);
                Rectangle drawRect = new Rectangle(
                    baseRect.Left - ((w - baseRect.Width) / 2),
                    baseRect.Top - ((h - baseRect.Height) / 2),
                    w,
                    h);
                e.Graphics.DrawImage(box.Image, drawRect);
            };
        }

        private void ActionHoverTimer_Tick(object? sender, EventArgs e)
        {
            bool anyActive = false;

            foreach (var key in _actionHoverStates.Keys.ToList())
            {
                ActionHoverState st = _actionHoverStates[key];
                float step = 0.18f;

                if (st.Target > st.Progress)
                {
                    st.Progress = Math.Min(1f, st.Progress + step);
                }
                else if (st.Target < st.Progress)
                {
                    st.Progress = Math.Max(0f, st.Progress - step);
                }

                _actionHoverStates[key] = st;
                key.Invalidate();

                if (st.Progress > 0.001f || st.Target > 0.001f)
                {
                    anyActive = true;
                }
            }

            if (!anyActive)
            {
                _actionHoverTimer.Stop();
            }
        }

        private static Rectangle GetZoomFitRect(Rectangle bounds, Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            float scale = Math.Min((float)bounds.Width / imageSize.Width, (float)bounds.Height / imageSize.Height);
            int drawW = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
            int drawH = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
            int left = bounds.Left + ((bounds.Width - drawW) / 2);
            int top = bounds.Top + ((bounds.Height - drawH) / 2);
            return new Rectangle(left, top, drawW, drawH);
        }

        private void UpdateActionImages()
        {
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;

            _picLogoutAction.Image = GetThemeAwareImage(
                isLight ? "action-logout-light.png" : "action-logout-dark.png",
                "action-logout.png");
            _picExitAction.Image = GetThemeAwareImage(
                isLight ? "action-exit-light.png" : "action-exit-dark.png",
                "action-exit.png");
            _picLicenseAction.Image = GetThemeAwareImage(
                isLight ? "action-license-light.png" : "action-license-dark.png",
                "action-license.png");
            _picUpdateAction.Image =
                BrandingAssets.TryLoadAssetImage("action-update-custom.png")
                ?? GetThemeAwareImage(
                    isLight ? "action-update-light.png" : "action-update-dark.png",
                    "action-update.png");

            _picLogoutAction.BorderStyle = _picLogoutAction.Image == null ? BorderStyle.None : BorderStyle.None;
            _picExitAction.BorderStyle = _picExitAction.Image == null ? BorderStyle.None : BorderStyle.None;
            _picLicenseAction.BorderStyle = _picLicenseAction.Image == null ? BorderStyle.None : BorderStyle.None;
            _picUpdateAction.BorderStyle = _picUpdateAction.Image == null ? BorderStyle.None : BorderStyle.None;
            _picLogoutAction.Invalidate();
            _picExitAction.Invalidate();
            _picLicenseAction.Invalidate();
            _picUpdateAction.Invalidate();
        }

        private static Image? GetThemeAwareImage(string themedFileName, string fallbackFileName)
        {
            Image? themed = BrandingAssets.TryLoadAssetImage(themedFileName);
            if (themed != null)
            {
                return themed;
            }

            return BrandingAssets.TryLoadAssetImage(fallbackFileName);
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                _lastUpdateCheckError = null;
                _pendingUpdate = null;
                _picUpdateAction.Visible = false;

                if (!_activationService.TryGetValidLocalActivation(out LocalActivationRecord? activation, out _)
                    || activation == null
                    || string.IsNullOrWhiteSpace(activation.ServerUrl))
                {
                    return;
                }

                UpdateCheckResult result = await _updateService.CheckAsync(activation.ServerUrl, Application.ProductVersion);
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    _lastUpdateCheckError = result.ErrorMessage;
                    return;
                }

                if (!result.HasUpdate || result.Info == null)
                {
                    return;
                }

                _pendingUpdate = result.Info;
                string caption = result.Info.Mandatory
                    ? $"Majburiy yangilash ({result.Info.Version})"
                    : $"Yangilash ({result.Info.Version})";

                _picUpdateAction.Tag = Tuple.Create(
                    caption,
                    result.Info.Mandatory ? Color.FromArgb(217, 76, 76) : Color.FromArgb(230, 150, 12),
                    result.Info.Mandatory ? Color.FromArgb(186, 54, 54) : Color.FromArgb(204, 120, 0));
                _picUpdateAction.Visible = true;
                _picUpdateAction.Invalidate();
                _actionsFlow.PerformLayout();
            }
            catch
            {
                // Update tekshiruvda xato bo'lsa, UI normal ishlashda davom etadi.
            }
        }

        private async void UpdateAction_Click(object? sender, EventArgs e)
        {
            if (_pendingUpdate == null)
            {
                string text = string.IsNullOrWhiteSpace(_lastUpdateCheckError)
                    ? "Hozircha yangi update topilmadi."
                    : $"Update tekshiruvda xato:\n{_lastUpdateCheckError}";
                MessageBox.Show(text, "Ma'lumot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string message =
                $"Yangi versiya: {_pendingUpdate.Version}\n\n" +
                $"{(string.IsNullOrWhiteSpace(_pendingUpdate.Note) ? "Izoh berilmagan." : _pendingUpdate.Note)}\n\n" +
                "Update yuklab olinib o'rnatilsinmi?\nDastur yopiladi.";

            if (MessageBox.Show(message, "Dastur yangilash", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
            {
                return;
            }

            if (_isUpdateDownloading)
            {
                MessageBox.Show("Update allaqachon yuklanmoqda, kuting.", "Ma'lumot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _isUpdateDownloading = true;
                _picUpdateAction.Enabled = false;
                UseWaitCursor = true;
                Cursor = Cursors.WaitCursor;

                await _updateService.ApplyAndRestartAsync(_pendingUpdate);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update yuklash/o'rnatishda xatolik: {ex.Message}", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isUpdateDownloading = false;
                _picUpdateAction.Enabled = true;
                UseWaitCursor = false;
                Cursor = Cursors.Default;
            }
        }

        private void StartRevealAnimation()
        {
            _revealIndex = 0;
            foreach (TileUi tile in _tiles)
            {
                tile.Card.Visible = false;
            }

            _revealTimer.Start();
        }

        private void RevealTimer_Tick(object? sender, EventArgs e)
        {
            if (_revealIndex >= _tiles.Count)
            {
                _revealTimer.Stop();
                return;
            }

            _tiles[_revealIndex].Card.Visible = true;
            _revealIndex++;
        }

        private static void AttachPressAnimation(Button button)
        {
            Point original = Point.Empty;
            button.MouseDown += (s, e) =>
            {
                original = button.Location;
                button.Location = new Point(original.X, original.Y + 1);
            };
            button.MouseUp += (s, e) => button.Location = original;
            button.MouseLeave += (s, e) => button.Location = original;
        }

        private static void AttachTileHoverAnimation(TileUi tile)
        {
            tile.Card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, tile.Card.Width - 3), Math.Max(1, tile.Card.Height - 3));
                using GraphicsPath path = RoundedRect(rect, 8);
                using Pen border = new Pen(Color.FromArgb(65, 255, 255, 255), 1f);
                e.Graphics.DrawPath(border, path);
            };

            void EnterHover(object? _, EventArgs __)
            {
                tile.Card.BackColor = tile.HoverColor;
            }

            void LeaveHover(object? _, EventArgs __)
            {
                tile.Card.BackColor = tile.BaseColor;
            }

            tile.Card.MouseEnter += EnterHover;
            tile.Card.MouseLeave += LeaveHover;
            tile.Title.MouseEnter += EnterHover;
            tile.Title.MouseLeave += LeaveHover;
            tile.Subtitle.MouseEnter += EnterHover;
            tile.Subtitle.MouseLeave += LeaveHover;
            tile.IconControl.MouseEnter += EnterHover;
            tile.IconControl.MouseLeave += LeaveHover;
        }

        private static Color LerpColor(Color from, Color to, float t)
        {
            int r = from.R + (int)((to.R - from.R) * t);
            int g = from.G + (int)((to.G - from.G) * t);
            int b = from.B + (int)((to.B - from.B) * t);
            return Color.FromArgb(r, g, b);
        }

        private void LayoutSidebarTiles()
        {
            int rowCount = _tiles.Count;
            int rowHeight = _sidebarCollapsed ? 48 : TileRowHeight;
            _tilesGrid.SuspendLayout();
            _tilesGrid.RowStyles.Clear();
            for (int i = 0; i < rowCount; i++)
            {
                _tilesGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            }
            _tilesGrid.Height = (rowCount * rowHeight) + 12;

            foreach (TileUi tile in _tiles)
            {
                tile.Title.Visible = !_sidebarCollapsed;
                tile.Subtitle.Visible = !_sidebarCollapsed;
                tile.IconControl.Visible = _sidebarCollapsed;
                tile.Card.Margin = new Padding(_sidebarCollapsed ? 2 : 3);
                UpdateTileLayout(tile.Card, tile.IconControl, tile.Title, tile.Subtitle, _sidebarCollapsed);
                tile.Card.Invalidate();
            }

            _tilesGrid.ResumeLayout(true);
        }

        private void UpdateBodyScrollArea()
        {
            int homeHeight = _homeContentPanel.Visible ? _homeContentPanel.Height : 0;
            int contentHeight = homeHeight + _actionPanel.Height + _lblFooter.Height + _body.Padding.Top + _body.Padding.Bottom + 24;
            _body.AutoScrollMinSize = new Size(0, contentHeight);
        }

        private static void UpdateCardRegion(Panel card)
        {
            int width = Math.Max(card.Width, 1);
            int height = Math.Max(card.Height, 1);
            card.Region = new Region(RoundedRect(new Rectangle(0, 0, width, height), 16));
        }

        private static void UpdateTileLayout(Panel card, Control icon, Label title, Label subtitle, bool collapsed)
        {
            if (collapsed)
            {
                const int iconSize = 34;
                icon.SetBounds((card.ClientSize.Width - iconSize) / 2, 0, iconSize, card.ClientSize.Height);
                title.Visible = false;
                subtitle.Visible = false;
                return;
            }

            icon.SetBounds(0, 0, 0, 0);
            int textLeft = 14;
            int textWidth = Math.Max(1, card.ClientSize.Width - textLeft - 12);
            title.SetBounds(textLeft, 7, textWidth, 24);
            title.TextAlign = ContentAlignment.MiddleLeft;
            subtitle.SetBounds(textLeft, 33, textWidth, 20);
            subtitle.TextAlign = ContentAlignment.MiddleLeft;
            title.Visible = true;
            subtitle.Visible = true;
        }

        private static void SetImageIconBounds(PictureBox imageIcon, Panel iconBox, float hoverProgress)
        {
            float ratio = 0.84f + (0.12f * hoverProgress);
            int size = Math.Max(16, (int)(Math.Min(iconBox.ClientSize.Width, iconBox.ClientSize.Height) * ratio));
            int left = (iconBox.ClientSize.Width - size) / 2;
            int top = (iconBox.ClientSize.Height - size) / 2;
            imageIcon.SetBounds(left, top, size, size);
        }

        private static void ApplyActionButtonBorder(Button button, string style)
        {
            // Borderni asosiy tugma rangiga yaqin ushlab, "to'rtburchak kontur" effektini yo'qotamiz.
            button.FlatAppearance.BorderColor = ControlPaint.Light(button.BackColor, 0.24f);
        }

        private void LayoutRoleBadge()
        {
            int rightLimit = _header.Width - 28;
            int badgeWidth = Math.Max(_lblRole.Width, _picRole.Visible ? _picRole.Width : 0);
            int left = Math.Max(24, rightLimit - badgeWidth);

            if (_picRole.Visible)
            {
                int spacing = 2;
                int minTop = 2;
                int desiredTop = 8;
                int maxTop = Math.Max(minTop, _header.Height - (_picRole.Height + spacing + _lblRole.Height + 2));
                int iconTop = Math.Max(minTop, Math.Min(desiredTop, maxTop));

                _picRole.Left = left + (badgeWidth - _picRole.Width) / 2;
                _picRole.Top = iconTop;
                _lblRole.Left = left + (badgeWidth - _lblRole.Width) / 2;
                _lblRole.Top = _picRole.Bottom + spacing;
                _picRole.BringToFront();
                _lblRole.BringToFront();
                LayoutHeaderCenterText();
            }
            else
            {
                _lblRole.Left = rightLimit - _lblRole.Width;
                _lblRole.Top = Math.Min(53, _header.Height - _lblRole.Height - 4);
                LayoutHeaderCenterText();
            }
        }

        private void LayoutHeaderCenterText()
        {
            int safeLeft = 24;
            int safeRight = (_picRole.Visible ? _picRole.Left : _header.Width) - 18;
            int desiredWidth = Math.Max(120, safeRight - safeLeft);
            int titleTop = _header.Height < 80 ? 2 : 8;
            _lblTitleOson.SetBounds(safeLeft, titleTop, desiredWidth, 28);
            _lblHeaderAlerts.SetBounds(safeLeft, titleTop + 31, desiredWidth, 22);
            _lblHeaderAlerts.BringToFront();
        }

        private string GetHeaderAddressText()
        {
            try
            {
                FiscalSettings settings = _fiscalSettingsService.Get(_currentUser);
                if (!string.IsNullOrWhiteSpace(settings.StoreAddress))
                {
                    return settings.StoreAddress.Trim();
                }
            }
            catch
            {
                // Manzilni o'qib bo'lmasa fallback matn qaytaramiz.
            }

            return "Manzil kiritilmagan";
        }

        private void RefreshHeaderWarnings()
        {
            _lblHeaderAlerts.Text = GetCriticalHeaderWarningsText();
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            _lblHeaderAlerts.ForeColor = _lblHeaderAlerts.Text.StartsWith("OK:", StringComparison.OrdinalIgnoreCase)
                ? (isLight ? Color.FromArgb(22, 126, 78) : Color.FromArgb(117, 228, 169))
                : (isLight ? Color.FromArgb(157, 91, 0) : Color.FromArgb(255, 196, 84));
            UpdateHomeStatus();
        }

        private void HeaderAlerts_Click(object? sender, EventArgs e)
        {
            string alertText = _lblHeaderAlerts.Text ?? string.Empty;
            if (alertText.StartsWith("OK:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool hasOverdue = HasOverdueWarning(alertText);
            bool hasLowStock = HasLowStockWarning(alertText);
            bool hasRate = HasRateWarning(alertText);

            ShowHeaderAlertsPicker(hasOverdue, hasLowStock, hasRate);
        }

        private void ShowHeaderAlertsPicker(bool hasOverdue, bool hasLowStock, bool hasRate)
        {
            var items = new List<(string Title, bool Enabled, Action OnClick)>();

            if (hasOverdue)
            {
                items.Add(("Qarz muddati o'tgan", AuthorizationService.CanManageDebts(_currentUser), OpenDebtorsFromHeader));
            }

            if (hasLowStock)
            {
                items.Add(("Kam qoldiq", AuthorizationService.CanViewProducts(_currentUser), OpenLowStockProductsFromHeader));
            }

            if (hasRate)
            {
                items.Add(("Kurs yangilanmadi", true, ShowRateUpdateDialog));
            }

            if (items.Count == 0)
            {
                return;
            }

            int formHeight = 126 + (items.Count * 54);
            using Form dlg = new Form
            {
                Text = "Ogohlantirishlar",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(420, formHeight),
                BackColor = Color.FromArgb(238, 243, 251),
                Font = UiTheme.BodyFont
            };

            Panel header = new Panel
            {
                Left = 14,
                Top = 12,
                Width = 392,
                Height = 62
            };
            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new LinearGradientBrush(header.ClientRectangle, Color.FromArgb(47, 110, 220), Color.FromArgb(72, 92, 204), 0f);
                using GraphicsPath path = RoundedRect(new Rectangle(0, 0, Math.Max(1, header.Width - 1), Math.Max(1, header.Height - 1)), 12);
                e.Graphics.FillPath(brush, path);
            };

            Label lblTitle = new Label
            {
                Text = "Qaysi ogohlantirishni ochamiz?",
                Left = 16,
                Top = 8,
                Width = 360,
                Height = 24,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 13, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            Label lblSub = new Label
            {
                Text = "Birini tanlang:",
                Left = 16,
                Top = 32,
                Width = 360,
                Height = 20,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(224, 236, 255),
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            Panel list = new Panel
            {
                Left = 14,
                Top = 82,
                Width = 392,
                Height = formHeight - 96,
                BackColor = Color.FromArgb(238, 243, 251)
            };
            list.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, list.Width - 3), Math.Max(1, list.Height - 3));
                using SolidBrush b = new SolidBrush(Color.White);
                using Pen p = new Pen(Color.FromArgb(190, 205, 228), 1.4f);
                using GraphicsPath path = RoundedRect(bounds, 12);
                e.Graphics.FillPath(b, path);
                e.Graphics.DrawPath(p, path);
            };

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Button btn = CreatePickerButton(item.Title, item.Enabled);
                btn.SetBounds(14, 12 + (i * 46), 364, 38);
                if (item.Enabled)
                {
                    btn.Click += (s, e) =>
                    {
                        dlg.Close();
                        item.OnClick();
                    };
                }

                list.Controls.Add(btn);
            }

            dlg.Controls.Add(header);
            dlg.Controls.Add(list);
            dlg.ShowDialog(this);
        }

        private static Button CreatePickerButton(string text, bool enabled)
        {
            Button btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0),
                BackColor = enabled ? Color.FromArgb(241, 246, 255) : Color.FromArgb(242, 242, 242),
                ForeColor = enabled ? Color.FromArgb(45, 63, 92) : Color.FromArgb(136, 142, 152),
                Cursor = enabled ? Cursors.Hand : Cursors.Default,
                Enabled = enabled
            };
            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(0, 0, Math.Max(1, btn.Width - 1), Math.Max(1, btn.Height - 1));
                using SolidBrush b = new SolidBrush(btn.BackColor);
                using Pen p = new Pen(enabled ? Color.FromArgb(170, 194, 232) : Color.FromArgb(216, 216, 216), 1.2f);
                using GraphicsPath path = RoundedRect(bounds, 9);
                e.Graphics.FillPath(b, path);
                e.Graphics.DrawPath(p, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    btn.Text,
                    btn.Font,
                    btn.ClientRectangle,
                    btn.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.LeftAndRightPadding);
            };
            btn.Resize += (s, e) =>
            {
                btn.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btn.Width), Math.Max(1, btn.Height)), 9));
            };
            btn.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btn.Width), Math.Max(1, btn.Height)), 9));
            return btn;
        }

        private void OpenDebtorsFromHeader()
        {
            if (!AuthorizationService.CanManageDebts(_currentUser))
            {
                return;
            }

            OpenPage(() => new DebtorsForm(_currentUser, "Overdue"));
            RefreshHeaderWarnings();
        }

        private void OpenLowStockProductsFromHeader()
        {
            if (!AuthorizationService.CanViewProducts(_currentUser))
            {
                return;
            }

            OpenPage(() => new ProductForm(_currentUser, "Kam qolgan"));
            RefreshHeaderWarnings();
        }

        private static bool HasRateWarning(string text)
        {
            return text.IndexOf("Kurs yangilanmagan", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Dollar kursi kiritilmagan", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasOverdueWarning(string text)
        {
            return text.IndexOf("Qarz muddati o'tgan", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasLowStockWarning(string text)
        {
            return text.IndexOf("Kam qoldiq", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ShowRateUpdateDialog()
        {
            using Form dlg = new Form
            {
                Text = "Dollar kursini yangilash",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(520, 290),
                BackColor = Color.FromArgb(242, 246, 252),
                Font = UiTheme.BodyFont
            };

            Label lblTitle = new Label
            {
                Text = "Kurs ogohlantirishi aniqlandi",
                AutoSize = false
            };
            lblTitle.SetBounds(24, 16, 470, 30);
            lblTitle.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 16, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(28, 45, 73);

            CurrencyRate? lastRate = _dbHelper.GetLastCurrencyRate();
            string lastText = lastRate == null || lastRate.Date == DateTime.MinValue
                ? "Oxirgi kurs: topilmadi"
                : $"Oxirgi kurs: {lastRate.Rate.ToString("N2", CultureInfo.InvariantCulture)} UZS ({lastRate.Date:dd.MM.yyyy HH:mm})";

            Label lblInfo = new Label
            {
                Text = lastText,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(72, 96, 128)
            };
            lblInfo.SetBounds(24, 56, 470, 24);

            Label lblStatus = new Label
            {
                Text = "Yangilash tugmasini bosing.",
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(37, 60, 92)
            };
            lblStatus.SetBounds(24, 90, 470, 58);

            Label lblManual = new Label
            {
                Text = "Internet bo'lmasa qo'lda kiriting (UZS):",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(58, 82, 115)
            };
            lblManual.SetBounds(24, 144, 470, 24);

            TextBox txtManualRate = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 51, 78)
            };
            txtManualRate.SetBounds(24, 170, 220, 32);
            txtManualRate.PlaceholderText = "Masalan: 12650.00";

            Button btnUpdate = new Button
            {
                Text = "Kursni yangilash",
                FlatStyle = FlatStyle.Flat,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 12, FontStyle.Bold),
                BackColor = Color.FromArgb(35, 117, 233),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.SetBounds(24, 222, 220, 42);

            Button btnSaveManual = new Button
            {
                Text = "Qo'lda saqlash",
                FlatStyle = FlatStyle.Flat,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 154, 104),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSaveManual.FlatAppearance.BorderSize = 0;
            btnSaveManual.SetBounds(256, 170, 132, 32);

            Button btnClose = new Button
            {
                Text = "Yopish",
                FlatStyle = FlatStyle.Flat,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11, FontStyle.Bold),
                BackColor = Color.FromArgb(118, 132, 154),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.SetBounds(256, 222, 132, 42);
            btnClose.Click += (s, e) => dlg.Close();

            btnUpdate.Click += async (s, e) => await UpdateRateFromDialogAsync(btnUpdate, btnSaveManual, txtManualRate, lblStatus, lblInfo);
            btnSaveManual.Click += (s, e) => SaveManualRateFromDialog(btnUpdate, btnSaveManual, txtManualRate, lblStatus, lblInfo);

            dlg.Controls.Add(lblTitle);
            dlg.Controls.Add(lblInfo);
            dlg.Controls.Add(lblStatus);
            dlg.Controls.Add(lblManual);
            dlg.Controls.Add(txtManualRate);
            dlg.Controls.Add(btnUpdate);
            dlg.Controls.Add(btnSaveManual);
            dlg.Controls.Add(btnClose);
            dlg.ShowDialog(this);
        }

        private async Task UpdateRateFromDialogAsync(Button btnUpdate, Button btnSaveManual, TextBox txtManualRate, Label lblStatus, Label lblInfo)
        {
            try
            {
                btnUpdate.Enabled = false;
                btnSaveManual.Enabled = false;
                lblStatus.ForeColor = Color.FromArgb(37, 60, 92);
                lblStatus.Text = "Yangilanmoqda...";

                (double rate, string source) = await _currencyRateProvider.FetchUsdToUzsRateAsync();
                SaveRate(rate);

                lblStatus.ForeColor = Color.FromArgb(17, 125, 72);
                lblStatus.Text = $"Muvaffaqiyatli yangilandi. Manba: {source}";
                lblInfo.Text = $"Oxirgi kurs: {rate.ToString("N2", CultureInfo.InvariantCulture)} UZS ({DateTime.Now:dd.MM.yyyy HH:mm})";
                txtManualRate.Text = rate.ToString("N2", CultureInfo.InvariantCulture);
                RefreshHeaderWarnings();
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.FromArgb(196, 53, 53);
                lblStatus.Text = $"Yangilanmadi: {ex.Message}";
            }
            finally
            {
                btnUpdate.Enabled = true;
                btnSaveManual.Enabled = true;
            }
        }

        private void SaveManualRateFromDialog(Button btnUpdate, Button btnSaveManual, TextBox txtManualRate, Label lblStatus, Label lblInfo)
        {
            string raw = txtManualRate.Text?.Trim() ?? string.Empty;
            string normalized = raw.Replace(" ", string.Empty).Replace(",", ".");
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double manualRate))
            {
                lblStatus.ForeColor = Color.FromArgb(196, 53, 53);
                lblStatus.Text = "Qo'lda saqlash xatosi: kurs son bo'lishi kerak.";
                return;
            }

            if (manualRate < 1000 || manualRate > 100000)
            {
                lblStatus.ForeColor = Color.FromArgb(196, 53, 53);
                lblStatus.Text = "Qo'lda saqlash xatosi: kurs 1000 va 100000 oralig'ida bo'lishi kerak.";
                return;
            }

            try
            {
                btnUpdate.Enabled = false;
                btnSaveManual.Enabled = false;
                SaveRate(manualRate);
                lblStatus.ForeColor = Color.FromArgb(17, 125, 72);
                lblStatus.Text = "Qo'lda kurs saqlandi.";
                lblInfo.Text = $"Oxirgi kurs: {manualRate.ToString("N2", CultureInfo.InvariantCulture)} UZS ({DateTime.Now:dd.MM.yyyy HH:mm})";
                RefreshHeaderWarnings();
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.FromArgb(196, 53, 53);
                lblStatus.Text = $"Qo'lda saqlashda xatolik: {ex.Message}";
            }
            finally
            {
                btnUpdate.Enabled = true;
                btnSaveManual.Enabled = true;
            }
        }

        private void SaveRate(double rate)
        {
            _dbHelper.SaveCurrencyRate(new CurrencyRate
            {
                Rate = rate,
                Date = DateTime.Now
            });
        }

        private string GetCriticalHeaderWarningsText()
        {
            var warnings = new List<string>();

            try
            {
                DebtSummary summary = _debtService.GetSummary(_currentUser);
                if (summary.OverdueDebts > 0 || summary.OverdueUZS > 0)
                {
                    warnings.Add($"Qarz muddati o'tgan: {summary.OverdueDebts} ta ({summary.OverdueUZS.ToString("N0", CultureInfo.InvariantCulture)} so'm)");
                }
            }
            catch
            {
                // Ruxsat yo'q yoki xizmat xatosi bo'lsa ogohlantirish satrini to'xtatmaymiz.
            }

            try
            {
                int lowStockCount = _productService.GetAll().Count(p => p.QuantityUSD <= 5);
                if (lowStockCount > 0)
                {
                    warnings.Add($"Kam qoldiq: {lowStockCount} ta mahsulot");
                }
            }
            catch
            {
                // Ombor ma'lumoti o'qilmasa davom etamiz.
            }

            try
            {
                CurrencyRate? rate = _dbHelper.GetLastCurrencyRate();
                if (rate == null || rate.Date == DateTime.MinValue)
                {
                    warnings.Add("Dollar kursi kiritilmagan");
                }
                else
                {
                    int daysOld = (DateTime.Today - rate.Date.Date).Days;
                    if (daysOld >= 1)
                    {
                        warnings.Add($"Kurs yangilanmagan: {daysOld} kun");
                    }
                }
            }
            catch
            {
                // Kurs o'qilmasa davom etamiz.
            }

            try
            {
                List<string> backups = _backupService.ListBackups();
                bool hasTodayBackup = backups.Any(path =>
                {
                    string file = System.IO.Path.GetFileName(path);
                    string prefix = $"database_{DateTime.Now:yyyyMMdd}_";
                    return file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                });

                if (!hasTodayBackup)
                {
                    warnings.Add("Bugungi backup topilmadi");
                }
            }
            catch
            {
                // Backup katalogi o'qilmasa davom etamiz.
            }

            if (warnings.Count == 0)
            {
                return "OK: Muhim ogohlantirishlar yo'q";
            }

            if (warnings.Count > 5)
            {
                warnings = warnings.Take(5).ToList();
            }

            return string.Join("  |  ", warnings);
        }

        private void UpdateRoleTextColor()
        {
            _lblRole.ForeColor = _roleTextBaseColor;
        }

        private static string? GetRoleIconFileName(string role)
        {
            if (string.Equals(role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return "role-admin.png";
            }

            if (string.Equals(role, DatabaseHelper.RoleSeller, StringComparison.OrdinalIgnoreCase))
            {
                return "role-seller.png";
            }

            return null;
        }

        private static string GetRoleDisplayText(string role)
        {
            if (string.Equals(role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return "Admin";
            }

            if (string.Equals(role, DatabaseHelper.RoleSeller, StringComparison.OrdinalIgnoreCase))
            {
                return "Sotuvchi";
            }

            return role;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class TileDefinition
        {
            public TileDefinition(string title, string subtitle, string icon, Color iconColor, Action onClick, string? iconImageFile = null)
            {
                Title = title;
                Subtitle = subtitle;
                Icon = icon;
                IconColor = iconColor;
                OnClick = onClick;
                IconImageFile = iconImageFile;
            }

            public string Title { get; }
            public string Subtitle { get; }
            public string Icon { get; }
            public Color IconColor { get; }
            public Action OnClick { get; }
            public string? IconImageFile { get; }
        }

        private sealed class TileUi
        {
            public TileUi(Panel card, Label title, Label subtitle, Panel iconBox, Control iconControl, bool isImageIcon)
            {
                Card = card;
                Title = title;
                Subtitle = subtitle;
                IconBox = iconBox;
                IconControl = iconControl;
                IsImageIcon = isImageIcon;
            }

            public Panel Card { get; }
            public Label Title { get; }
            public Label Subtitle { get; }
            public Panel IconBox { get; }
            public Control IconControl { get; }
            public bool IsImageIcon { get; }
            public System.Windows.Forms.Timer HoverTimer { get; } = new System.Windows.Forms.Timer();
            public float HoverProgress { get; set; }
            public float HoverTarget { get; set; }
            public float NeonOffset { get; set; }
            public Color BaseColor { get; set; }
            public Color HoverColor { get; set; }
        }

        private sealed class ActionHoverState
        {
            public float Progress { get; set; }
            public float Target { get; set; }
        }

    }
}
