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
        private readonly PictureBox _picHeaderLocation = new PictureBox();
        private readonly Label _lblStoreName = new Label();
        private readonly PictureBox _picHeaderBell = new PictureBox();
        private readonly PictureBox _picHeaderChevron = new PictureBox();
        private readonly PictureBox _picRole = new PictureBox();
        private readonly Label _lblRole = new Label();
        private readonly Label _lblRoleSubtitle = new Label();
        private readonly Panel _workspace = new Panel();
        private readonly Panel _footerPanel = new Panel();
        private readonly Panel _sidebar = new Panel();
        private readonly Panel _sidebarBrand = new Panel();
        private readonly Button _btnSidebarToggle = new Button();
        private readonly Label _lblSidebarCaption = new Label();
        private readonly ToolTip _navigationToolTip = new ToolTip();
        private readonly Panel _header = new Panel();
        private readonly Panel _body = new Panel();
        private readonly Panel _homeContentPanel = new Panel();
        private readonly Panel _welcomePanel = new Panel();
        private readonly Panel _homeDashboardPanel = new Panel();
        private readonly Panel _homeDashboardChart = new Panel();
        private readonly Panel _homeDashboardSummary = new Panel();
        private readonly Panel _homeStatusPanel = new Panel();
        private readonly PictureBox _picHomeStatusCheck = new PictureBox();
        private readonly Label _lblHomeStatus = new Label();
        private readonly Label _lblHomeDate = new Label();
        private readonly Label _lblDashboardSection = new Label();
        private readonly Button _btnDashboardRefresh = new Button();
        private readonly Button _btnNewSale = new Button();
        private readonly FlowLayoutPanel _homeQuickActions = new FlowLayoutPanel();
        private readonly List<(Panel Card, Label Title, Label Value, Label Detail, PictureBox Icon)> _dashboardCards =
            new List<(Panel Card, Label Title, Label Value, Label Detail, PictureBox Icon)>();
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
        private readonly PictureBox _picFooterInfo = new PictureBox();
        private readonly Label _lblFooterCaption = new Label();
        private readonly PictureBox _picFooterStatus = new PictureBox();
        private readonly Label _lblFooterStatus = new Label();
        private readonly List<TileUi> _tiles = new List<TileUi>();
        private readonly System.Windows.Forms.Timer _revealTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _roleTextAnimTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _logoAnimTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _headerWarningsTimer = new System.Windows.Forms.Timer();
        private int _revealIndex;
        private float _roleTextPhase;
        private int _roleIntroStep;
        private bool _roleIntroActive;
        private float _logoAnimPhase;
        private float _logoHoverProgress;
        private float _logoHoverTarget;
        private Color _roleTextBaseColor = Color.FromArgb(132, 182, 255);
        private Color _roleTextPulseColor = Color.FromArgb(226, 243, 255);
        private int _roleLabelBaseTop;
        private Control? _appLogoControl;
        private Rectangle _appLogoBaseBounds;
        private Size _activePagePreferredSize;
        private const int TileColumns = 1;
        private const int TileRowHeight = 46;
        private static readonly Color DashboardSalesColor = Color.FromArgb(91, 91, 240);
        private AppUpdateInfo? _pendingUpdate;
        private Form? _activePage;
        private string? _lastUpdateCheckError;
        private bool _isUpdateDownloading;
        private bool _isSidebarCollapsed;
        private string _selectedNavigationTitle = "Bosh sahifa";
        private HomeDashboardSnapshot? _homeDashboardSnapshot;
        private string? _homeDashboardErrorMessage;

        public MainForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            Icon = BrandingAssets.TryLoadAppIcon() ?? Icon;
            BuildTiles();
            ApplySidebarLayout(_isSidebarCollapsed);
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
            _sidebar.Width = 232;
            _sidebar.Padding = new Padding(12, 10, 12, 12);
            _sidebar.AutoScroll = true;

            _btnSidebarToggle.Height = 36;
            _btnSidebarToggle.FlatStyle = FlatStyle.Flat;
            _btnSidebarToggle.FlatAppearance.BorderSize = 1;
            _btnSidebarToggle.Font = new Font("Segoe UI", 18, FontStyle.Regular);
            _btnSidebarToggle.Cursor = Cursors.Hand;
            _btnSidebarToggle.Click += (_, __) => ApplySidebarLayout(!_isSidebarCollapsed);
            _navigationToolTip.SetToolTip(_btnSidebarToggle, "Navigatsiyani yig'ish");

            _header.Dock = DockStyle.Top;
            _header.Height = 72;
            _header.Padding = new Padding(24, 14, 24, 14);
            _header.BackColor = UiTheme.Card;
            _header.Paint += (s, e) =>
            {
                if (_header.ClientSize.Width > 0 && _header.ClientSize.Height > 0)
                {
                    using SolidBrush background = new SolidBrush(UiTheme.Card);
                    e.Graphics.FillRectangle(background, _header.ClientRectangle);
                }
                using Pen line = new Pen(UiTheme.Border);
                e.Graphics.DrawLine(line, 0, _header.Height - 1, _header.Width, _header.Height - 1);
                int profileDividerX = _header.Width - 156;
                e.Graphics.DrawLine(line, profileDividerX, 14, profileDividerX, _header.Height - 14);
            };

            PictureBox appIcon = new PictureBox
            {
                Size = new Size(144, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = BrandingAssets.TryLoadAssetImage("pdf-sidebar-brand.png")
                    ?? BrandingAssets.TryLoadLogo()
            };
            appIcon.Left = 16;
            appIcon.Top = 22;
            _appLogoControl = appIcon;
            _appLogoBaseBounds = appIcon.Bounds;
            appIcon.MouseEnter += (s, e) => _logoHoverTarget = 1f;
            appIcon.MouseLeave += (s, e) => _logoHoverTarget = 0f;

            _sidebarBrand.Dock = DockStyle.Top;
            _sidebarBrand.Height = 108;
            _sidebarBrand.BackColor = Color.FromArgb(19, 35, 56);
            _sidebarBrand.Controls.Add(appIcon);
            _btnSidebarToggle.Parent = _sidebarBrand;
            _sidebarBrand.Cursor = Cursors.Hand;
            _sidebarBrand.Click += (_, __) => ShowHomePage();
            appIcon.Click += (_, __) => ShowHomePage();
            _lblSidebarCaption.Visible = false;

            _lblTitleOson.Text = "OsonTrack SRM tizimi";
            _lblTitleOson.AutoSize = false;
            _lblTitleOson.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 13, FontStyle.Regular);
            _lblTitleOson.Height = 26;
            _lblTitleOson.TextAlign = ContentAlignment.MiddleLeft;
            _lblTitleOson.AutoEllipsis = true;
            _lblTitleOson.BackColor = Color.Transparent;
            (string storeAddress, string businessName) = GetHeaderStoreDetails();
            _lblTitleOson.Text = storeAddress;

            _picHeaderLocation.Size = new Size(22, 24);
            _picHeaderLocation.SizeMode = PictureBoxSizeMode.Zoom;
            _picHeaderLocation.BackColor = Color.Transparent;
            _picHeaderLocation.Image = BrandingAssets.TryLoadAssetImage("pdf-header-location.png");

            _lblStoreName.Text = businessName;
            _lblStoreName.AutoSize = false;
            _lblStoreName.Height = 22;
            _lblStoreName.Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 9.5f, FontStyle.Regular);
            _lblStoreName.ForeColor = Color.FromArgb(101, 116, 138);
            _lblStoreName.BackColor = Color.Transparent;
            _lblStoreName.TextAlign = ContentAlignment.MiddleLeft;

            _lblHeaderAlerts.AutoSize = false;
            _lblHeaderAlerts.Visible = false;

            _picHeaderBell.Size = new Size(22, 24);
            _picHeaderBell.SizeMode = PictureBoxSizeMode.Zoom;
            _picHeaderBell.BackColor = Color.Transparent;
            _picHeaderBell.Image = BrandingAssets.TryLoadAssetImage("pdf-header-bell.png");
            _picHeaderBell.Cursor = Cursors.Hand;
            _picHeaderBell.Click += HeaderAlerts_Click;
            _navigationToolTip.SetToolTip(_picHeaderBell, "Ogohlantirishlar");

            _picHeaderChevron.Size = new Size(16, 16);
            _picHeaderChevron.SizeMode = PictureBoxSizeMode.Zoom;
            _picHeaderChevron.BackColor = Color.Transparent;
            _picHeaderChevron.Image = BrandingAssets.TryLoadAssetImage("pdf-header-chevron.png");

            string? roleIconFile = GetRoleIconFileName(_currentUser.Role);
            Image? roleIconImage = string.IsNullOrWhiteSpace(roleIconFile) ? null : BrandingAssets.TryLoadAssetImage(roleIconFile);
            _picRole.Visible = false;
            _picRole.Image = roleIconImage;

            _lblRole.Text = GetRoleDisplayText(_currentUser.Role);
            _lblRole.AutoSize = false;
            _lblRole.Size = new Size(92, 24);
            _lblRole.Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11, FontStyle.Bold);
            _lblRole.BackColor = Color.Transparent;
            _lblRole.ForeColor = Color.FromArgb(30, 43, 64);
            _lblRole.TextAlign = ContentAlignment.MiddleLeft;

            _lblRoleSubtitle.Text = string.Equals(_currentUser.Role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase)
                ? "Administrator"
                : "Sotuvchi";
            _lblRoleSubtitle.AutoSize = false;
            _lblRoleSubtitle.Size = new Size(92, 20);
            _lblRoleSubtitle.Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 9, FontStyle.Regular);
            _lblRoleSubtitle.ForeColor = Color.FromArgb(101, 116, 138);
            _lblRoleSubtitle.BackColor = Color.Transparent;
            _lblRoleSubtitle.TextAlign = ContentAlignment.MiddleLeft;

            _header.Controls.Add(_picHeaderLocation);
            _header.Controls.Add(_lblTitleOson);
            _header.Controls.Add(_lblStoreName);
            _header.Controls.Add(_lblHeaderAlerts);
            _header.Controls.Add(_picHeaderBell);
            _header.Controls.Add(_picRole);
            _header.Controls.Add(_lblRole);
            _header.Controls.Add(_lblRoleSubtitle);
            _header.Controls.Add(_picHeaderChevron);
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
            _sidebar.Controls.Add(_actionPanel);

            _body.Dock = DockStyle.Fill;
            _body.Padding = new Padding(28, 22, 28, 8);
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
            _lblWelcomeSubtitle.AutoEllipsis = true;
            _lblWelcomeSubtitle.SetBounds(2, 43, 600, 24);
            _lblWelcomeSubtitle.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10.5f, FontStyle.Regular);
            _lblWelcomeSubtitle.TextAlign = ContentAlignment.MiddleLeft;
            _lblWelcomeSubtitle.BackColor = Color.Transparent;
            _lblWelcomeSubtitle.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _welcomePanel.Controls.Add(_lblWelcomeTitle);
            _welcomePanel.Controls.Add(_lblWelcomeSubtitle);

            BuildHomeDashboard();

            _pageHost.Dock = DockStyle.Fill;
            _pageHost.BackColor = UiTheme.Background;
            _pageHost.AutoScroll = true;
            _pageHost.Visible = false;

            _actionPanel.Dock = DockStyle.Bottom;
            _actionPanel.Height = 48;
            _actionPanel.Padding = new Padding(6);

            _actionsFlow.FlowDirection = FlowDirection.LeftToRight;
            _actionsFlow.WrapContents = false;
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

            _picThemeAction.Size = new Size(28, 28);
            _picThemeAction.Margin = new Padding(2);
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
            _navigationToolTip.SetToolTip(_picLogoutAction, "Akkauntdan chiqish");
            _navigationToolTip.SetToolTip(_picExitAction, "Dasturdan chiqish");
            _navigationToolTip.SetToolTip(_picLicenseAction, "Dastur va litsenziya holati");
            _navigationToolTip.SetToolTip(_picUpdateAction, "Dastur yangilanishi");
            _navigationToolTip.SetToolTip(_picThemeAction, UiTheme.GetThemeToggleButtonText());

            _lblFooter.Text = "© 2026 OsonTrack SRM";
            _lblFooter.Dock = DockStyle.Right;
            _lblFooter.Width = 180;
            _lblFooter.TextAlign = ContentAlignment.MiddleRight;
            _lblFooter.Padding = new Padding(0, 0, 28, 0);
            _lblFooter.Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 9, FontStyle.Regular);
            _lblFooter.BackColor = Color.Transparent;

            _picFooterStatus.Image = BrandingAssets.TryLoadAssetImage("pdf-status-dot.png");
            _picFooterStatus.Size = new Size(8, 8);
            _picFooterStatus.SizeMode = PictureBoxSizeMode.Zoom;
            _picFooterStatus.BackColor = Color.Transparent;
            _picFooterStatus.Location = new Point(24, 16);
            _lblFooterStatus.Text = "Tizim ishlayapti";
            _lblFooterStatus.AutoSize = false;
            _lblFooterStatus.SetBounds(38, 6, 180, 28);
            _lblFooterStatus.Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 9, FontStyle.Regular);
            _lblFooterStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblFooterStatus.BackColor = Color.Transparent;
            _footerPanel.Dock = DockStyle.Bottom;
            _footerPanel.Height = 40;
            _footerPanel.Padding = Padding.Empty;
            _footerPanel.BackColor = Color.White;
            _footerPanel.Controls.Add(_picFooterStatus);
            _footerPanel.Controls.Add(_lblFooterStatus);
            _footerPanel.Controls.Add(_lblFooter);

            BuildHomeContent();
            _body.Controls.Add(_homeContentPanel);
            _body.Controls.Add(_pageHost);

            _workspace.Dock = DockStyle.Fill;
            _workspace.Controls.Add(_body);
            _workspace.Controls.Add(_footerPanel);
            _workspace.Controls.Add(_header);
            Controls.Add(_workspace);
            Controls.Add(_sidebar);

            _revealTimer.Interval = 70;
            _revealTimer.Tick += RevealTimer_Tick;
            _roleTextAnimTimer.Interval = 40;
            _roleTextAnimTimer.Tick += RoleTextAnimTimer_Tick;
            _logoAnimTimer.Interval = 33;
            _logoAnimTimer.Tick += LogoAnimTimer_Tick;
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
                StartRoleTextAnimation();
                StartLogoAnimation();
                StartRevealAnimation();
                LoadHomeDashboard();
                _ = CheckForUpdatesAsync();
            };
            FormClosed += (s, e) =>
            {
                _headerWarningsTimer.Stop();
                _revealTimer.Stop();
                _roleTextAnimTimer.Stop();
                _logoAnimTimer.Stop();
                _actionHoverTimer.Stop();
                CloseActivePage();
            };

            Resize += (s, e) =>
            {
                LayoutRoleBadge();
                LayoutHeaderCenterText();
                int rows = Math.Max(1, _tilesGrid.RowCount);
                _tilesGrid.Height = (rows * (_isSidebarCollapsed ? 56 : TileRowHeight)) + 10;
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
                    ShowHomePage,
                    "pdf-nav-home.png")
            };

            bool isAdmin = string.Equals(_currentUser.Role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase);
            bool isSeller = string.Equals(_currentUser.Role, DatabaseHelper.RoleSeller, StringComparison.OrdinalIgnoreCase);

            if (isAdmin && AuthorizationService.CanViewProducts(_currentUser))
            {
                defs.Add(new TileDefinition("Tovarlar", "Ombor va narxlar", "\uECAA", Color.FromArgb(20, 143, 230), () => OpenPage(() => new ProductForm(_currentUser)), "pdf-nav-products.png"));
            }

            if (AuthorizationService.CanCreateSales(_currentUser))
            {
                defs.Add(new TileDefinition("Sotuv", "Yangi savdo operatsiyasi", "\uE7BF", Color.FromArgb(13, 178, 108), () => OpenPage(() => new SaleForm(_currentUser)), "pdf-nav-sales.png"));
            }

            if (AuthorizationService.CanManageExpenses(_currentUser))
            {
                defs.Add(new TileDefinition("Rasxod", "Xarajatlarni boshqarish", "\uE8C7", Color.FromArgb(245, 34, 77), () => OpenPage(() => new ExpenseForm(_currentUser)), "pdf-nav-expenses.png"));
            }

            if (AuthorizationService.CanManageDebts(_currentUser))
            {
                defs.Add(new TileDefinition("Mijozlar", "Mijozlar bazasi", "\uE77B", Color.FromArgb(237, 146, 0), () => OpenPage(() => new CustomersForm(_currentUser)), "pdf-nav-customers.png"));
            }

            if (isAdmin && AuthorizationService.CanViewReports(_currentUser))
            {
                defs.Add(new TileDefinition("Hisobot", "Tahlil va eksport", "\uE9D2", Color.FromArgb(67, 80, 231), () => OpenPage(() => new ReportForm(_currentUser)), "pdf-nav-reports.png"));
            }

            if (isAdmin)
            {
                defs.Add(new TileDefinition("Foydalanuvchilar", "Rol va akkauntlar", "\uE716", Color.FromArgb(75, 88, 114), () => OpenPage(() => new UserManagementForm(_currentUser)), "pdf-nav-users.png"));
                defs.Add(new TileDefinition("Backup / Restore", "Zaxira va tiklash", "\uE7F1", Color.FromArgb(75, 88, 114), () => OpenPage(() => new BackupRestoreForm(_currentUser)), "pdf-nav-backup.png"));
                defs.Add(new TileDefinition("Chek Rekvizitlari", "Chek sozlamalari", "\uE8A5", Color.FromArgb(32, 129, 214), () => OpenPage(() => new FiscalSettingsForm(_currentUser)), "pdf-nav-receipt.png"));
                defs.Add(new TileDefinition("Foiz va chegirmalar", "Chegirma sozlamalari", "%", Color.FromArgb(16, 161, 121), () => OpenPage(() => new PricingDiscountForm(_currentUser)), "pdf-nav-discounts.png"));
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
                RowCount = 5,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

            _homeStatusPanel.Dock = DockStyle.Fill;
            _homeStatusPanel.Margin = new Padding(0, 0, 0, 8);
            _homeStatusPanel.Paint += HomeStatusPanel_Paint;
            _lblHomeStatus.SetBounds(42, 0, 600, 38);
            _lblHomeStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblHomeStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblHomeStatus.Font = UiTheme.BodyFont;
            _lblHomeStatus.AutoEllipsis = true;
            _lblHomeStatus.Cursor = Cursors.Hand;
            _lblHomeStatus.Click += HeaderAlerts_Click;
            _homeStatusPanel.Controls.Add(_lblHomeStatus);
            _picHomeStatusCheck.SetBounds(14, 9, 20, 20);
            _picHomeStatusCheck.SizeMode = PictureBoxSizeMode.Zoom;
            _picHomeStatusCheck.BackColor = Color.Transparent;
            _picHomeStatusCheck.Image = BrandingAssets.TryLoadAssetImage("pdf-status-check.png");
            _picHomeStatusCheck.Cursor = Cursors.Hand;
            _picHomeStatusCheck.Click += HeaderAlerts_Click;
            _homeStatusPanel.Controls.Add(_picHomeStatusCheck);
            _lblHomeDate.SetBounds(0, 0, 100, 38);
            _lblHomeDate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblHomeDate.TextAlign = ContentAlignment.MiddleRight;
            _lblHomeDate.Font = UiTheme.BodyFont;
            _lblHomeDate.ForeColor = Color.FromArgb(22, 107, 75);
            _lblHomeDate.Text = DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            _homeStatusPanel.Controls.Add(_lblHomeDate);
            _homeStatusPanel.Resize += (_, __) =>
            {
                _lblHomeStatus.Width = Math.Max(100, _homeStatusPanel.ClientSize.Width - 174);
                _lblHomeDate.Left = Math.Max(0, _homeStatusPanel.ClientSize.Width - 126);
            };
            _homeStatusPanel.Cursor = Cursors.Hand;
            _homeStatusPanel.Click += HeaderAlerts_Click;
            layout.Controls.Add(_homeStatusPanel, 0, 0);

            var cards = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 10),
                Padding = Padding.Empty
            };
            string[] metricIconFiles = AuthorizationService.IsAdmin(_currentUser)
                ? new[]
                {
                    "pdf-metric-sales.png",
                    "pdf-metric-profit.png",
                    "pdf-metric-expense.png",
                    "pdf-metric-debt.png"
                }
                : new[]
                {
                    "pdf-nav-customers.png",
                    "pdf-summary-debts.png",
                    "pdf-summary-debts.png",
                    "pdf-metric-debt.png"
                };
            for (int i = 0; i < 4; i++)
            {
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
                var metricCard = CreateDashboardMetricCard(metricIconFiles[i]);
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
            _homeQuickActions.Resize += (_, __) =>
            {
                int count = _homeQuickActions.Controls.Count;
                if (count == 0)
                {
                    return;
                }

                int width = Math.Max(150, (_homeQuickActions.ClientSize.Width - (count * 12)) / count);
                foreach (Control control in _homeQuickActions.Controls)
                {
                    control.Width = width;
                }
            };
            layout.Controls.Add(quickActionsSection, 0, 3);

            var note = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 1, 0, 0)
            };
            var noteIcon = new PictureBox
            {
                Image = BrandingAssets.TryLoadAssetImage("pdf-footer-info.png"),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(0, 4, 16, 16)
            };
            var noteText = new Label
            {
                Text = $"Ko'rsatkichlar {DateTime.Today:dd.MM.yyyy} sanasi uchun. Summalar O'zbekiston so'mida (UZS).",
                Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 8.5f, FontStyle.Regular),
                ForeColor = UiTheme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Bounds = new Rectangle(24, 0, 700, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            note.Controls.Add(noteIcon);
            note.Controls.Add(noteText);
            layout.Controls.Add(note, 0, 4);

            _btnDashboardRefresh.Size = new Size(122, 42);
            _btnDashboardRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            UiTheme.StyleNeutralButton(_btnDashboardRefresh);
            _btnDashboardRefresh.Text = "Yangilash";
            _btnDashboardRefresh.Image = BrandingAssets.TryLoadAssetImage("pdf-button-refresh.png");
            _btnDashboardRefresh.ImageAlign = ContentAlignment.MiddleLeft;
            _btnDashboardRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;
            _btnDashboardRefresh.Padding = new Padding(18, 0, 14, 0);
            _btnDashboardRefresh.BackColor = Color.White;
            _btnDashboardRefresh.ForeColor = Color.FromArgb(30, 43, 64);
            _btnDashboardRefresh.FlatAppearance.BorderSize = 1;
            _btnDashboardRefresh.FlatAppearance.BorderColor = Color.FromArgb(222, 229, 238);
            _btnDashboardRefresh.Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 10.5f, FontStyle.Regular);
            if (_btnDashboardRefresh.Image != null)
            {
                _btnDashboardRefresh.Image = new Bitmap(_btnDashboardRefresh.Image, new Size(18, 18));
            }
            _btnDashboardRefresh.Click += (_, __) => LoadHomeDashboard();
            _welcomePanel.Controls.Add(_btnDashboardRefresh);

            _btnNewSale.Text = "Yangi sotuv";
            _btnNewSale.Size = new Size(160, 42);
            _btnNewSale.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            UiTheme.StylePrimaryButton(_btnNewSale);
            _btnNewSale.Text = "Yangi sotuv";
            _btnNewSale.Image = BrandingAssets.TryLoadAssetImage("pdf-button-plus.png");
            _btnNewSale.ImageAlign = ContentAlignment.MiddleLeft;
            _btnNewSale.TextImageRelation = TextImageRelation.ImageBeforeText;
            _btnNewSale.Padding = new Padding(18, 0, 16, 0);
            _btnNewSale.BackColor = Color.FromArgb(94, 91, 240);
            _btnNewSale.Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 10.5f, FontStyle.Regular);
            if (_btnNewSale.Image != null)
            {
                _btnNewSale.Image = new Bitmap(_btnNewSale.Image, new Size(18, 18));
            }
            _btnNewSale.Visible = AuthorizationService.CanCreateSales(_currentUser);
            _btnNewSale.Click += (_, __) =>
            {
                UpdateNavigationSelection("Sotuv");
                OpenPage(() => new SaleForm(_currentUser));
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
            var actions = new List<(string Title, string Detail, string IconFile, Action Open)>
            {
                ("Tovar qo'shish", "Omborga yangi tovar", "pdf-quick-products.png", () =>
                {
                    UpdateNavigationSelection("Tovarlar");
                    OpenPage(() => new ProductForm(_currentUser));
                }),
                ("Mijoz qo'shish", "Mijozlar bazasiga yozish", "pdf-quick-customers.png", () =>
                {
                    UpdateNavigationSelection("Mijozlar");
                    OpenPage(() => new CustomersForm(_currentUser));
                }),
                ("Xarajat kiritish", "Chiqimni qayd etish", "pdf-quick-expenses.png", () =>
                {
                    UpdateNavigationSelection("Rasxod");
                    OpenPage(() => new ExpenseForm(_currentUser));
                })
            };

            if (!AuthorizationService.CanViewProducts(_currentUser))
            {
                actions.RemoveAt(0);
            }
            if (!AuthorizationService.CanManageDebts(_currentUser))
            {
                actions.RemoveAt(actions.FindIndex(action => action.Title == "Mijoz qo'shish"));
            }
            if (!AuthorizationService.CanManageExpenses(_currentUser))
            {
                actions.RemoveAt(actions.FindIndex(action => action.Title == "Xarajat kiritish"));
            }

            for (int i = 0; i < actions.Count; i++)
            {
                (string title, string detail, string iconFile, Action open) = actions[i];
                Panel card = CreateHomeQuickAction(title, detail, iconFile, open);
                card.Margin = new Padding(6, 0, 6, 0);
                card.Width = Math.Max(180, (_homeContentPanel.ClientSize.Width - 36) / Math.Max(1, actions.Count));
                _homeQuickActions.Controls.Add(card);
            }
        }

        private Panel CreateHomeQuickAction(string title, string detail, string iconFile, Action open)
        {
            var card = new Panel
            {
                Height = 72,
                BackColor = UiTheme.Card,
                Cursor = Cursors.Hand,
                Tag = "home-quick-action"
            };
            var icon = new PictureBox
            {
                Image = BrandingAssets.TryLoadAssetImage(iconFile),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(16, 16, 40, 40)
            };
            var titleLabel = new Label
            {
                Text = title,
                Font = UiTheme.ButtonFont,
                ForeColor = UiTheme.Text,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Bounds = new Rectangle(64, 13, 190, 22)
            };
            var detailLabel = new Label
            {
                Text = detail,
                Font = UiTheme.BodyFont,
                ForeColor = UiTheme.Muted,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Bounds = new Rectangle(64, 37, 190, 20)
            };
            var arrow = new PictureBox
            {
                Image = BrandingAssets.TryLoadAssetImage("pdf-quick-arrow.png"),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Size = new Size(16, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            card.Controls.Add(icon);
            card.Controls.Add(titleLabel);
            card.Controls.Add(detailLabel);
            card.Controls.Add(arrow);
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
                arrow.Location = new Point(Math.Max(0, card.ClientSize.Width - 32), (card.ClientSize.Height - arrow.Height) / 2);
                int textWidth = Math.Max(1, arrow.Left - titleLabel.Left - 12);
                titleLabel.Width = textWidth;
                detailLabel.Width = textWidth;
            };
            card.Click += (_, __) => open();
            icon.Click += (_, __) => open();
            titleLabel.Click += (_, __) => open();
            detailLabel.Click += (_, __) => open();
            arrow.Click += (_, __) => open();
            return card;
        }

        private static (Panel Card, Label Title, Label Value, Label Detail, PictureBox Icon) CreateDashboardMetricCard(string iconFile)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Card,
                Padding = new Padding(12, 9, 10, 8),
                Tag = "dashboard-card"
            };
            var title = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Padding = new Padding(0, 0, 30, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.BodyFont,
                ForeColor = UiTheme.Muted,
                BackColor = Color.Transparent
            };
            var value = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 18, FontStyle.Bold),
                ForeColor = UiTheme.Text,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            var detail = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 8.5f, FontStyle.Regular),
                ForeColor = UiTheme.Muted,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            var icon = new PictureBox
            {
                Image = BrandingAssets.TryLoadAssetImage(iconFile),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Size = new Size(20, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            card.Controls.Add(icon);
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
            card.Resize += (_, __) => icon.Location = new Point(Math.Max(0, card.ClientSize.Width - icon.Width - 14), 12);
            return (card, title, value, detail, icon);
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
            bool showHealthyState = _homeDashboardErrorMessage == null && isHealthy;
            _lblHomeStatus.Text = _homeDashboardErrorMessage
                ?? (isHealthy
                    ? "Muhim ogohlantirishlar yo'q. Barcha ko'rsatkichlar me'yorda."
                    : warnings);
            _lblHomeStatus.ForeColor = _homeDashboardErrorMessage != null
                ? (isLight ? Color.FromArgb(165, 49, 45) : Color.FromArgb(255, 151, 145))
                : isHealthy
                    ? (isLight ? Color.FromArgb(22, 126, 78) : Color.FromArgb(117, 228, 169))
                    : (isLight ? Color.FromArgb(157, 91, 0) : Color.FromArgb(255, 196, 84));
            _picHomeStatusCheck.Visible = showHealthyState;
            _lblHomeDate.Text = DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            _lblHomeDate.ForeColor = isLight ? Color.FromArgb(22, 107, 75) : UiTheme.Muted;
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
                    ? (isLight ? Color.FromArgb(233, 244, 239) : Color.FromArgb(30, 52, 45))
                    : (isLight ? Color.FromArgb(250, 243, 224) : Color.FromArgb(54, 46, 31));
            using SolidBrush fill = new SolidBrush(fillColor);
            e.Graphics.FillPath(fill, path);
        }

        private void HomeDashboardSummary_Paint(object? sender, PaintEventArgs e)
        {
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

            using Font titleFont = new Font(UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 12, FontStyle.Bold);
            using Font labelFont = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 9, FontStyle.Regular);
            using Font valueFont = new Font(UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 17, FontStyle.Bold);
            HomeDashboardSnapshot? snapshot = _homeDashboardSnapshot;
            TextRenderer.DrawText(
                graphics,
                "Do'kon holati",
                titleFont,
                new Rectangle(16, 10, panel.Width - 32, 24),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (snapshot == null)
            {
                TextRenderer.DrawText(
                    graphics,
                    "Ma'lumot mavjud emas",
                    labelFont,
                    new Rectangle(16, 46, panel.Width - 32, 28),
                    UiTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            if (snapshot.IsAdministrator)
            {
                DrawHomeSummarySection(
                    graphics,
                    panel.ClientSize,
                    44,
                    "pdf-summary-products.png",
                    "Ombordagi tovarlar",
                    $"{snapshot.ProductCount:N0} ta",
                    "Jami tovar turlari",
                    snapshot.LowStockCount > 0 ? UiTheme.Danger : UiTheme.Accent,
                    labelFont,
                    valueFont,
                    $"{snapshot.LowStockCount:N0} ta kam qolgan");
                DrawHomeSummaryDivider(graphics, panel.Width, 112);
                DrawHomeSummarySection(
                    graphics,
                    panel.ClientSize,
                    124,
                    "pdf-summary-debts.png",
                    "Qarzlar",
                    $"{snapshot.OpenDebts:N0} ta faol",
                    snapshot.OverdueDebts > 0
                        ? $"{snapshot.OverdueDebts:N0} ta muddati o'tgan · {snapshot.OverdueUZS:N0} UZS"
                        : "Muddati o'tgan qarzlar yo'q",
                    snapshot.OverdueDebts > 0 ? UiTheme.Danger : UiTheme.Accent,
                    labelFont,
                    valueFont);
            }
            else
            {
                DrawHomeSummarySection(
                    graphics,
                    panel.ClientSize,
                    48,
                    "pdf-nav-customers.png",
                    "Mijozlar bazasi",
                    $"{snapshot.CustomerCount:N0} ta",
                    "Jami mijozlar",
                    UiTheme.Primary,
                    labelFont,
                    valueFont);
                DrawHomeSummaryDivider(graphics, panel.Width, 116);
                DrawHomeSummarySection(
                    graphics,
                    panel.ClientSize,
                    128,
                    "pdf-summary-debts.png",
                    "Qarzlar",
                    $"{snapshot.OpenDebts:N0} ta faol",
                    snapshot.OverdueDebts > 0
                        ? $"{snapshot.OverdueDebts:N0} ta muddati o'tgan · {snapshot.OverdueUZS:N0} UZS"
                        : "Muddati o'tgan qarzlar yo'q",
                    snapshot.OverdueDebts > 0 ? UiTheme.Danger : UiTheme.Accent,
                    labelFont,
                    valueFont);
            }
        }

        private static void DrawHomeSummarySection(
            Graphics graphics,
            Size bounds,
            int y,
            string iconFile,
            string title,
            string value,
            string detail,
            Color detailColor,
            Font labelFont,
            Font valueFont,
            string? badgeText = null)
        {
            int textWidth = Math.Max(1, bounds.Width - 32);
            Image? icon = BrandingAssets.TryLoadAssetImage(iconFile);
            if (icon != null)
            {
                graphics.DrawImage(icon, new Rectangle(16, y + 1, 20, 20));
            }
            TextRenderer.DrawText(
                graphics,
                title,
                labelFont,
                new Rectangle(icon == null ? 16 : 44, y, textWidth - (icon == null ? 0 : 28), 20),
                UiTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            int badgeWidth = 0;
            if (!string.IsNullOrWhiteSpace(badgeText))
            {
                badgeWidth = Math.Min(140, TextRenderer.MeasureText(badgeText, labelFont).Width + 18);
                int badgeX = bounds.Width - badgeWidth - 16;
                Rectangle badge = new Rectangle(badgeX, y + 23, badgeWidth, 22);
                bool isWarning = detailColor == UiTheme.Danger;
                using GraphicsPath badgePath = RoundedRect(badge, 5);
                using SolidBrush badgeFill = new SolidBrush(isWarning
                    ? Color.FromArgb(253, 235, 234)
                    : Color.FromArgb(233, 244, 239));
                graphics.FillPath(badgeFill, badgePath);
                TextRenderer.DrawText(
                    graphics,
                    badgeText,
                    labelFont,
                    badge,
                    isWarning ? Color.FromArgb(176, 55, 50) : Color.FromArgb(22, 107, 75),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            TextRenderer.DrawText(
                graphics,
                value,
                valueFont,
                new Rectangle(16, y + 20, Math.Max(1, textWidth - badgeWidth), 30),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(
                graphics,
                detail,
                labelFont,
                new Rectangle(16, y + 49, textWidth, 20),
                detailColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawHomeSummaryDivider(Graphics graphics, int width, int y)
        {
            using Pen divider = new Pen(UiTheme.Border, 1);
            graphics.DrawLine(divider, 16, y, width - 16, y);
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
                    $"UZS  Bugun yakunlangan sotuvlar",
                    $"UZS  Foyda: {snapshot.TodayProfitUZS:N0}",
                    $"UZS  Bugun qayd etilgan xarajatlar",
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
                : "Qarzlar holati";
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
                    new Rectangle(16, 54, chart.Width - 32, 30),
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
            int bottom = bounds.Height - 30;
            int plotHeight = bottom - top;
            int plotWidth = right - left;
            if (plotHeight < 24 || plotWidth < 20 || itemCount <= 0)
            {
                return;
            }

            maxValue = maxValue <= 0 ? 1 : maxValue * 1.15;
            Color gridColor = UiTheme.Border;
            using (Pen gridPen = new Pen(gridColor, 1))
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
                    float center = left + (slot * i) + (slot / 2f);
                    float firstHeight = (float)Math.Max(0, firstValue(i) / maxValue * plotHeight);
                    float secondHeight = (float)Math.Max(0, secondValue(i) / maxValue * plotHeight);
                    graphics.FillRectangle(firstBrush, center - barWidth - 1, bottom - firstHeight, barWidth, firstHeight);
                    graphics.FillRectangle(secondBrush, center + 1, bottom - secondHeight, barWidth, secondHeight);

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

            _activePagePreferredSize = page.Size;
            page.TopLevel = false;
            page.FormBorderStyle = FormBorderStyle.None;
            page.StartPosition = FormStartPosition.Manual;
            page.BackColor = UiTheme.BackgroundSecondary;
            page.Font = UiTheme.BodyFont;
            page.Location = Point.Empty;

            _activePage = page;
            _body.AutoScroll = false;
            _homeContentPanel.Visible = false;
            _lblFooter.Visible = false;
            _pageHost.Visible = true;
            _pageHost.Controls.Add(page);
            _pageHost.Resize += PageHost_Resize;
            LayoutActivePage();
            page.Show();
            page.BringToFront();
            _body.PerformLayout();
        }

        private void UpdateNavigationSelection(string title)
        {
            _selectedNavigationTitle = title;
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            foreach (TileUi tile in _tiles)
            {
                bool selected = string.Equals(tile.Title.Text, title, StringComparison.Ordinal);
                tile.BaseColor = selected
                    ? Color.FromArgb(91, 91, 240)
                    : _sidebar.BackColor;
                tile.HoverColor = selected
                    ? Color.FromArgb(77, 77, 220)
                    : isLight
                        ? Color.FromArgb(30, 50, 75)
                        : Color.FromArgb(29, 45, 65);
                tile.Card.BackColor = tile.HoverProgress > 0
                    ? LerpColor(tile.BaseColor, tile.HoverColor, tile.HoverProgress)
                    : tile.BaseColor;
                if (tile.IconControl is PictureBox icon)
                {
                    icon.Image = selected ? tile.SelectedIconImage : tile.IconImage;
                }
                tile.Title.ForeColor = selected ? Color.White : Color.FromArgb(188, 200, 216);
                tile.Card.Invalidate();
            }
        }

        private void ShowHomePage()
        {
            CloseActivePage();
            UpdateNavigationSelection("Bosh sahifa");
            LoadHomeDashboard();
            _pageHost.Visible = false;
            _pageHost.AutoScrollMinSize = Size.Empty;
            _lblFooter.Visible = true;
            _homeContentPanel.Visible = true;
            _body.AutoScroll = true;
            UpdateBodyScrollArea();
            _body.PerformLayout();
        }

        private void ApplySidebarLayout(bool collapsed)
        {
            _isSidebarCollapsed = collapsed;
            _sidebar.SuspendLayout();
            _tilesGrid.SuspendLayout();

            _sidebar.Width = collapsed ? 72 : 232;
            _sidebar.Padding = collapsed
                ? new Padding(6, 8, 6, 10)
                : new Padding(12, 10, 12, 12);
            _actionsFlow.FlowDirection = collapsed ? FlowDirection.TopDown : FlowDirection.LeftToRight;
            _actionsFlow.WrapContents = false;
            _actionPanel.Height = collapsed
                ? (_actionsFlow.Controls.Cast<Control>().Count(control => control.Visible) * 32) + 12
                : 48;
            _sidebarBrand.Visible = true;

            _btnSidebarToggle.Dock = DockStyle.None;
            _btnSidebarToggle.SetBounds(collapsed ? 20 : 188, collapsed ? 70 : 28, 28, 28);
            _btnSidebarToggle.Width = collapsed ? 32 : 28;
            _btnSidebarToggle.Height = collapsed ? 32 : 28;
            _btnSidebarToggle.Margin = new Padding(0, 0, 0, 6);
            _btnSidebarToggle.Text = collapsed ? "\u203A" : "\u2039";
            _btnSidebarToggle.AccessibleName = collapsed
                ? "Navigatsiyani kengaytirish"
                : "Navigatsiyani yig'ish";
            _navigationToolTip.SetToolTip(
                _btnSidebarToggle,
                collapsed ? "Navigatsiyani kengaytirish" : "Navigatsiyani yig'ish");
            _btnSidebarToggle.ForeColor = Color.FromArgb(224, 238, 255);
            _btnSidebarToggle.BackColor = Color.FromArgb(34, 53, 78);
            _btnSidebarToggle.FlatAppearance.BorderColor = Color.FromArgb(71, 116, 177);
            _btnSidebarToggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 68, 125);

            _tilesGrid.Padding = collapsed
                ? new Padding(8, 8, 8, 2)
                : new Padding(8, 8, 8, 2);
            _appLogoControl?.SetBounds(collapsed ? 18 : 16, 22, collapsed ? 36 : 144, collapsed ? 26 : 48);
            if (_appLogoControl is PictureBox logo)
            {
                logo.Image = BrandingAssets.TryLoadAssetImage(collapsed ? "pdf-sidebar-mark.png" : "pdf-sidebar-brand.png")
                    ?? BrandingAssets.TryLoadLogo();
            }
            _sidebarBrand.Height = 108;
            foreach (TileUi tile in _tiles)
            {
                tile.Title.Visible = !collapsed;
                tile.Subtitle.Visible = !collapsed;
                tile.Card.Padding = collapsed
                    ? new Padding(4, 2, 4, 2)
                    : new Padding(8, 2, 8, 2);
                tile.Card.Margin = new Padding(0, 1, 0, 1);
                UpdateTileLayout(tile.Card, tile.IconBox, tile.Title, tile.Subtitle, collapsed);

                string tooltip = $"{tile.Title.Text}: {tile.Subtitle.Text}";
                _navigationToolTip.SetToolTip(tile.Card, tooltip);
                _navigationToolTip.SetToolTip(tile.IconBox, tooltip);
                _navigationToolTip.SetToolTip(tile.IconControl, tooltip);
                tile.Card.AccessibleName = tile.Title.Text;
                tile.Card.AccessibleRole = AccessibleRole.PushButton;
            }

            int rowHeight = TileRowHeight;
            for (int i = 0; i < _tilesGrid.RowStyles.Count; i++)
            {
                _tilesGrid.RowStyles[i].Height = rowHeight;
            }
            _tilesGrid.Height = (_tilesGrid.RowCount * rowHeight) + 10;
            _tilesGrid.ResumeLayout(true);
            _sidebar.ResumeLayout(true);
            LayoutActivePage();
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
            int width = Math.Max(hostSize.Width, Math.Max(_activePagePreferredSize.Width, minimumSize.Width));
            int height = Math.Max(hostSize.Height, Math.Max(_activePagePreferredSize.Height, minimumSize.Height));

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

            Image? tileImage = string.IsNullOrWhiteSpace(def.IconImageFile) ? null : BrandingAssets.TryLoadAssetImage(def.IconImageFile);
            Image? selectedTileImage = string.IsNullOrWhiteSpace(def.IconImageFile)
                ? null
                : BrandingAssets.TryLoadAssetImage(
                    def.IconImageFile.Replace(".png", "-active.png", StringComparison.OrdinalIgnoreCase));
            bool useImageIcon = tileImage != null;
            Panel iconBox = new Panel
            {
                Size = useImageIcon ? new Size(22, 22) : new Size(38, 38),
                Left = 12,
                Top = 10,
                BackColor = Color.Transparent
            };
            iconBox.Paint += (s, e) =>
            {
                if (useImageIcon)
                {
                    return;
                }

                using var brush = new LinearGradientBrush(iconBox.ClientRectangle, def.IconColor, ControlPaint.Light(def.IconColor), 45f);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = RoundedRect(iconBox.ClientRectangle, 14);
                e.Graphics.FillPath(brush, path);
            };

            Control iconControl;
            if (tileImage != null)
            {
                PictureBox imageIcon = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Image = tileImage
                };
                iconControl = imageIcon;
                SetImageIconBounds(imageIcon, iconBox, 0f);
            }
            else
            {
                iconControl = new Label
                {
                    Text = def.Icon,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = UiTheme.IconFont(18),
                    ForeColor = Color.White,
                    BackColor = Color.Transparent
                };
            }
            iconBox.Controls.Add(iconControl);

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
            UpdateTileLayout(card, iconBox, title, subtitle, _isSidebarCollapsed);

            EventHandler click = (s, e) =>
            {
                UpdateNavigationSelection(def.Title);
                def.OnClick();
            };
            card.Click += click;
            iconBox.Click += click;
            iconControl.Click += click;
            title.Click += click;
            subtitle.Click += click;

            card.Controls.Add(iconBox);
            card.Controls.Add(title);
            card.Controls.Add(subtitle);
            card.Resize += (s, e) =>
            {
                UpdateCardRegion(card);
                UpdateTileLayout(card, iconBox, title, subtitle, _isSidebarCollapsed);
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle borderBounds = new Rectangle(1, 1, Math.Max(1, card.Width - 3), Math.Max(1, card.Height - 3));
                using GraphicsPath borderPath = RoundedRect(borderBounds, 16);
                using Pen border = new Pen(UiTheme.Border, 1f);
                e.Graphics.DrawPath(border, borderPath);
            };

            var tileUi = new TileUi(card, title, subtitle, iconBox, iconControl, useImageIcon, tileImage, selectedTileImage);
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
            _header.BackColor = UiTheme.Card;
            _footerPanel.BackColor = UiTheme.Card;
            _actionPanel.BackColor = _sidebar.BackColor;
            _actionsFlow.BackColor = _sidebar.BackColor;
            _body.BackColor = BackColor;
            _tilesGrid.BackColor = BackColor;
            _pageHost.BackColor = BackColor;
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
            _lblStoreName.ForeColor = UiTheme.Muted;
            _lblRole.ForeColor = UiTheme.Text;
            _lblRoleSubtitle.ForeColor = UiTheme.Muted;
            _lblHomeDate.ForeColor = isLight ? Color.FromArgb(22, 107, 75) : UiTheme.Muted;
            UpdateThemeActionImage();
            UpdateActionImages();
            _roleTextBaseColor = isLight ? Color.FromArgb(44, 104, 186) : Color.FromArgb(168, 212, 255);
            _roleTextPulseColor = isLight ? Color.FromArgb(25, 62, 115) : Color.FromArgb(212, 233, 255);
            UpdateRoleTextColor();
            _lblFooter.ForeColor = isLight ? Color.FromArgb(101, 116, 138) : Color.FromArgb(134, 156, 188);
            _lblFooterStatus.ForeColor = isLight ? Color.FromArgb(101, 116, 138) : Color.FromArgb(134, 156, 188);

            foreach (TileUi tile in _tiles)
            {
                tile.BaseColor = isLight ? Color.FromArgb(19, 35, 56) : Color.FromArgb(13, 24, 41);
                tile.HoverColor = isLight ? Color.FromArgb(30, 50, 75) : Color.FromArgb(29, 45, 65);
                tile.Card.BackColor = tile.BaseColor;
                tile.Title.ForeColor = Color.FromArgb(188, 200, 216);
                tile.Subtitle.ForeColor = isLight ? Color.FromArgb(188, 200, 216) : Color.FromArgb(156, 179, 207);
                tile.Card.Invalidate();
            }

            _sidebar.BackColor = isLight ? Color.FromArgb(19, 35, 56) : Color.FromArgb(13, 24, 41);
            _btnSidebarToggle.BackColor = _sidebar.BackColor;
            _btnSidebarToggle.ForeColor = isLight ? Color.FromArgb(224, 238, 255) : Color.FromArgb(198, 218, 241);
            _btnSidebarToggle.FlatAppearance.BorderColor = isLight
                ? Color.FromArgb(71, 116, 177)
                : Color.FromArgb(65, 87, 116);
            _btnSidebarToggle.FlatAppearance.MouseOverBackColor = isLight
                ? Color.FromArgb(25, 68, 125)
                : Color.FromArgb(31, 47, 68);
            _sidebarBrand.BackColor = _sidebar.BackColor;
            _sidebarBrand.Invalidate();
            _lblSidebarCaption.ForeColor = isLight ? Color.FromArgb(177, 205, 244) : Color.FromArgb(144, 169, 202);
            _tilesGrid.BackColor = _sidebar.BackColor;
            _actionPanel.BackColor = _sidebar.BackColor;
            _actionsFlow.BackColor = _sidebar.BackColor;
            _sidebarBrand.BackColor = _sidebar.BackColor;
            _footerPanel.BackColor = UiTheme.Card;
            _welcomePanel.BackColor = Color.Transparent;
            _lblWelcomeTitle.ForeColor = UiTheme.Text;
            _lblWelcomeSubtitle.ForeColor = UiTheme.Muted;
            _lblDashboardSection.ForeColor = UiTheme.Text;
            foreach (Control control in _homeQuickActions.Controls)
            {
                if (control is not Panel actionCard)
                {
                    continue;
                }

                if (actionCard.Controls.Count >= 3)
                {
                    actionCard.Controls[1].ForeColor = UiTheme.Text;
                    actionCard.Controls[2].ForeColor = UiTheme.Muted;
                }
                actionCard.Invalidate();
            }
            UpdateNavigationSelection(_selectedNavigationTitle);
            if (_activePage != null)
            {
                _activePage.BackColor = UiTheme.BackgroundSecondary;
                _activePage.ForeColor = UiTheme.Text;
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
            _picThemeAction.Image = BrandingAssets.TryLoadAssetImage("pdf-nav-theme.png");
            _picThemeAction.BorderStyle = BorderStyle.None;
            _picThemeAction.AccessibleName = UiTheme.GetThemeToggleButtonText();
            _navigationToolTip.SetToolTip(_picThemeAction, UiTheme.GetThemeToggleButtonText());
        }

        private void SetupActionImageBox(PictureBox box, string text, Color c1, Color c2)
        {
            box.Size = new Size(28, 28);
            box.Margin = new Padding(2);
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
            _picLogoutAction.Image = BrandingAssets.TryLoadAssetImage("pdf-nav-logout.png");
            _picExitAction.Image = BrandingAssets.TryLoadAssetImage("pdf-nav-exit.png");
            _picLicenseAction.Image = BrandingAssets.TryLoadAssetImage("pdf-nav-about.png");
            _picUpdateAction.Image = BrandingAssets.TryLoadAssetImage("pdf-button-refresh.png");

            _picLogoutAction.BorderStyle = BorderStyle.None;
            _picExitAction.BorderStyle = BorderStyle.None;
            _picLicenseAction.BorderStyle = BorderStyle.None;
            _picUpdateAction.BorderStyle = BorderStyle.None;
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
                ApplySidebarLayout(_isSidebarCollapsed);
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
                if (tile.HoverProgress <= 0.01f)
                {
                    return;
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, tile.Card.Width - 3), Math.Max(1, tile.Card.Height - 3));
                using GraphicsPath path = RoundedRect(rect, 16);

                int lineAlpha = (int)Math.Round(255 * tile.HoverProgress);
                float segmentLength = 24f;

                using Pen snake = new Pen(Color.FromArgb(lineAlpha, 24, 106, 58), 3f)
                {
                    DashStyle = DashStyle.Custom,
                    DashPattern = new float[] { segmentLength, 10000f },
                    DashOffset = -tile.NeonOffset,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                e.Graphics.DrawPath(snake, path);
            };

            tile.HoverTimer.Interval = 16;
            tile.HoverTimer.Tick += (s, e) =>
            {
                float step = 0.2f;
                if (tile.HoverTarget > tile.HoverProgress)
                {
                    tile.HoverProgress = Math.Min(1f, tile.HoverProgress + step);
                }
                else
                {
                    tile.HoverProgress = Math.Max(0f, tile.HoverProgress - step);
                }

                tile.IconBox.Top = Math.Max(0, (tile.Card.ClientSize.Height - tile.IconBox.Height) / 2) - (int)(3 * tile.HoverProgress);
                tile.Card.BackColor = LerpColor(tile.BaseColor, tile.HoverColor, tile.HoverProgress);
                if (tile.HoverProgress > 0.01f)
                {
                    tile.NeonOffset = (tile.NeonOffset + 1.9f) % 10000f;
                    tile.Card.Invalidate();
                }
                if (tile.IsImageIcon && tile.IconControl is PictureBox imageIcon)
                {
                    SetImageIconBounds(imageIcon, tile.IconBox, tile.HoverProgress);
                }

                if (Math.Abs(tile.HoverProgress - tile.HoverTarget) < 0.01f)
                {
                    // Hover ustida turganda ilon animatsiyasi doimiy aylanishi kerak.
                    if (tile.HoverTarget <= 0.01f)
                    {
                        tile.NeonOffset = 0f;
                        tile.HoverTimer.Stop();
                    }
                }
            };

            void EnterHover(object? _, EventArgs __)
            {
                if (tile.HoverTarget <= 0.01f)
                {
                    tile.HoverProgress = 0f;
                    tile.NeonOffset = 0f;
                }
                tile.HoverTarget = 1f;
                tile.HoverTimer.Start();
            }

            void LeaveHover(object? _, EventArgs __)
            {
                tile.HoverTarget = 0f;
                tile.HoverTimer.Start();
            }

            tile.Card.MouseEnter += EnterHover;
            tile.Card.MouseLeave += LeaveHover;
            tile.Title.MouseEnter += EnterHover;
            tile.Title.MouseLeave += LeaveHover;
            tile.Subtitle.MouseEnter += EnterHover;
            tile.Subtitle.MouseLeave += LeaveHover;
            tile.IconBox.MouseEnter += EnterHover;
            tile.IconBox.MouseLeave += LeaveHover;
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

        private void UpdateBodyScrollArea()
        {
            int homeHeight = _homeContentPanel.Visible ? _homeContentPanel.Height : 0;
            int contentHeight = homeHeight + _body.Padding.Top + _body.Padding.Bottom + 12;
            _body.AutoScrollMinSize = new Size(0, contentHeight);
        }

        private static void UpdateCardRegion(Panel card)
        {
            int width = Math.Max(card.Width, 1);
            int height = Math.Max(card.Height, 1);
            card.Region = new Region(RoundedRect(new Rectangle(0, 0, width, height), 16));
        }

        private static void UpdateTileLayout(Panel card, Panel iconBox, Label title, Label subtitle, bool collapsed)
        {
            int iconLeft = collapsed
                ? Math.Max(0, (card.ClientSize.Width - iconBox.Width) / 2)
                : 12;
            int iconTop = Math.Max(0, (card.ClientSize.Height - iconBox.Height) / 2);
            int textLeft = iconLeft + iconBox.Width + 12;
            int textWidth = Math.Max(1, card.ClientSize.Width - textLeft - 8);
            iconBox.SetBounds(iconLeft, iconTop, iconBox.Width, iconBox.Height);
            title.SetBounds(textLeft, 7, textWidth, 24);
            title.TextAlign = ContentAlignment.MiddleLeft;
            subtitle.SetBounds(textLeft, 32, textWidth, 20);
            subtitle.TextAlign = ContentAlignment.MiddleLeft;
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
            int profileLeft = Math.Max(180, _header.Width - 148);
            if (_picRole.Visible)
            {
                _picRole.SetBounds(profileLeft, 18, 30, 30);
                _lblRole.Left = profileLeft + _picRole.Width + 2;
                _lblRole.Top = 13;
                _lblRole.Width = Math.Max(40, _header.Width - _lblRole.Left - 34);
                _lblRoleSubtitle.SetBounds(_lblRole.Left, 36, _lblRole.Width, 18);
                _roleLabelBaseTop = _lblRole.Top;
            }
            else
            {
                _lblRole.SetBounds(profileLeft, 11, Math.Max(40, _header.Width - profileLeft - 34), 25);
                _lblRoleSubtitle.SetBounds(profileLeft, 35, _lblRole.Width, 19);
                _roleLabelBaseTop = _lblRole.Top;
            }

            _picHeaderChevron.SetBounds(Math.Max(profileLeft, _header.Width - 25), 27, 14, 14);
            _picHeaderChevron.BringToFront();
            _picRole.BringToFront();
            _lblRole.BringToFront();
            _lblRoleSubtitle.BringToFront();
            LayoutHeaderCenterText();
        }

        private void LayoutHeaderCenterText()
        {
            int safeLeft = 56;
            int profileDividerX = _header.Width - 156;
            _picHeaderLocation.SetBounds(24, 24, 20, 24);
            _picHeaderBell.SetBounds(Math.Max(safeLeft + 100, profileDividerX - 42), 24, 20, 24);
            int addressWidth = Math.Max(80, _picHeaderBell.Left - safeLeft - 16);
            _lblTitleOson.SetBounds(safeLeft, 9, addressWidth, 27);
            _lblStoreName.SetBounds(safeLeft, 35, addressWidth, 22);
            _lblHeaderAlerts.Visible = false;
        }

        private (string Address, string BusinessName) GetHeaderStoreDetails()
        {
            try
            {
                FiscalSettings settings = _fiscalSettingsService.Get(_currentUser);
                string address = string.IsNullOrWhiteSpace(settings.StoreAddress)
                    ? "Manzil kiritilmagan"
                    : settings.StoreAddress.Trim();
                string businessName = string.IsNullOrWhiteSpace(settings.BusinessName)
                    ? "Asosiy do'kon"
                    : settings.BusinessName.Trim();
                return (address, businessName);
            }
            catch
            {
                // Sozlamalarni o'qib bo'lmasa sarlavhada standart qiymatlar ko'rsatiladi.
            }

            return ("Manzil kiritilmagan", "Asosiy do'kon");
        }

        private void RefreshHeaderWarnings()
        {
            _lblHeaderAlerts.Text = GetCriticalHeaderWarningsText();
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
        }

        private void OpenLowStockProductsFromHeader()
        {
            if (!AuthorizationService.CanViewProducts(_currentUser))
            {
                return;
            }

            OpenPage(() => new ProductForm(_currentUser, "Kam qolgan"));
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

        private void StartRoleTextAnimation()
        {
            _roleTextPhase = 0f;
            _roleIntroStep = 0;
            _roleIntroActive = true;
            if (!_roleTextAnimTimer.Enabled)
            {
                _roleTextAnimTimer.Start();
            }
            UpdateRoleTextColor();
        }

        private void StartLogoAnimation()
        {
            if (_appLogoControl == null)
            {
                return;
            }

            _appLogoBaseBounds = _appLogoControl.Bounds;
            _logoAnimPhase = 0f;
            _logoHoverProgress = 0f;
            _logoHoverTarget = 0f;
            if (!_logoAnimTimer.Enabled)
            {
                _logoAnimTimer.Start();
            }
        }

        private void LogoAnimTimer_Tick(object? sender, EventArgs e)
        {
            if (_appLogoControl == null || _appLogoControl.IsDisposed)
            {
                _logoAnimTimer.Stop();
                return;
            }

            _logoAnimPhase += 0.09f;
            if (_logoAnimPhase > (Math.PI * 2))
            {
                _logoAnimPhase = 0f;
            }

            float hoverStep = 0.08f;
            if (_logoHoverTarget > _logoHoverProgress)
            {
                _logoHoverProgress = Math.Min(1f, _logoHoverProgress + hoverStep);
            }
            else if (_logoHoverTarget < _logoHoverProgress)
            {
                _logoHoverProgress = Math.Max(0f, _logoHoverProgress - hoverStep);
            }

            float breathing = (float)Math.Sin(_logoAnimPhase * 0.8f) * 0.008f;
            int driftY = (int)Math.Round(Math.Sin(_logoAnimPhase * 0.6f) * 1.0);

            int baseW = _appLogoBaseBounds.Width;
            int baseH = _appLogoBaseBounds.Height;
            int width = Math.Max(12, (int)Math.Round(baseW * (1f + breathing + (_logoHoverProgress * 0.04f))));
            int height = Math.Max(12, (int)Math.Round(baseH * (1f + breathing + (_logoHoverProgress * 0.04f))));
            int left = _appLogoBaseBounds.Left - ((width - baseW) / 2);
            int top = _appLogoBaseBounds.Top - ((height - baseH) / 2) + driftY;

            _appLogoControl.SetBounds(left, top, width, height);
        }

        private void RoleTextAnimTimer_Tick(object? sender, EventArgs e)
        {
            _roleTextPhase += 0.12f;
            if (_roleTextPhase > (Math.PI * 2))
            {
                _roleTextPhase = 0f;
            }

            if (_roleIntroActive)
            {
                _roleIntroStep++;
                if (_roleIntroStep >= 12)
                {
                    _roleIntroActive = false;
                }
            }

            UpdateRoleTextColor();
            int yOffset = (int)Math.Round(Math.Sin(_roleTextPhase * 1.2f) * 1.0);
            _lblRole.Top = _roleLabelBaseTop + yOffset;
        }

        private void UpdateRoleTextColor()
        {
            float pulse = (float)((Math.Sin(_roleTextPhase) + 1.0) * 0.5);
            Color pulsed = LerpColor(_roleTextBaseColor, _roleTextPulseColor, pulse * 0.8f);

            if (_roleIntroActive)
            {
                float introT = Math.Max(0f, Math.Min(1f, _roleIntroStep / 16f));
                _lblRole.ForeColor = LerpColor(_header.BackColor, pulsed, introT);
            }
            else
            {
                _lblRole.ForeColor = pulsed;
            }
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
            public TileUi(
                Panel card,
                Label title,
                Label subtitle,
                Panel iconBox,
                Control iconControl,
                bool isImageIcon,
                Image? iconImage,
                Image? selectedIconImage)
            {
                Card = card;
                Title = title;
                Subtitle = subtitle;
                IconBox = iconBox;
                IconControl = iconControl;
                IsImageIcon = isImageIcon;
                IconImage = iconImage;
                SelectedIconImage = selectedIconImage ?? iconImage;
            }

            public Panel Card { get; }
            public Label Title { get; }
            public Label Subtitle { get; }
            public Panel IconBox { get; }
            public Control IconControl { get; }
            public bool IsImageIcon { get; }
            public Image? IconImage { get; }
            public Image? SelectedIconImage { get; }
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
