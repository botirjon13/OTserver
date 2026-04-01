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
        private readonly UpdateService _updateService = new UpdateService();
        private readonly CurrencyRateProviderService _currencyRateProvider = new CurrencyRateProviderService();
        private readonly DatabaseHelper _dbHelper = new DatabaseHelper();
        private readonly Label _lblTitleOson = new Label();
        private readonly Label _lblHeaderAlerts = new Label();
        private readonly PictureBox _picRole = new PictureBox();
        private readonly Label _lblRole = new Label();
        private readonly Panel _header = new Panel();
        private readonly Panel _body = new Panel();
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
        private const int TileColumns = 3;
        private const int TileRowHeight = 186;
        private AppUpdateInfo? _pendingUpdate;
        private string? _lastUpdateCheckError;
        private bool _isUpdateDownloading;

        public MainForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            Icon = BrandingAssets.TryLoadAppIcon() ?? Icon;
            BuildTiles();
            ApplyThemeStyles();
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

            _header.Dock = DockStyle.Top;
            _header.Height = 98;
            _header.Padding = new Padding(28, 18, 28, 16);
            _header.BackColor = Color.FromArgb(20, 30, 44);
            _header.Paint += (s, e) =>
            {
                using Pen line = new Pen(Color.FromArgb(56, 78, 108));
                e.Graphics.DrawLine(line, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            };

            Image? logoImage = BrandingAssets.TryLoadLogo();
            Size headerLogoSize = logoImage != null ? new Size(220, 88) : new Size(56, 56);
            Control appIcon = BrandingAssets.CreateLogoControl(headerLogoSize, 14, "\uE700");
            appIcon.Left = -34;
            appIcon.Top = 6;
            _appLogoControl = appIcon;
            _appLogoBaseBounds = appIcon.Bounds;
            appIcon.MouseEnter += (s, e) => _logoHoverTarget = 1f;
            appIcon.MouseLeave += (s, e) => _logoHoverTarget = 0f;

            _lblTitleOson.Text = "OsonTrack SRM tizimi";
            _lblTitleOson.AutoSize = false;
            _lblTitleOson.Font = new Font("Bahnschrift SemiBold", 22, FontStyle.Bold);
            _lblTitleOson.Left = appIcon.Right + 12;
            _lblTitleOson.Top = 14;
            _lblTitleOson.Height = 40;
            _lblTitleOson.TextAlign = ContentAlignment.MiddleCenter;
            _lblTitleOson.AutoEllipsis = true;
            _lblTitleOson.BackColor = _header.BackColor;
            _lblTitleOson.Text = GetHeaderAddressText();

            _lblHeaderAlerts.AutoSize = false;
            _lblHeaderAlerts.Font = new Font("Bahnschrift SemiBold", 10.5f, FontStyle.Bold);
            _lblHeaderAlerts.TextAlign = ContentAlignment.MiddleCenter;
            _lblHeaderAlerts.AutoEllipsis = true;
            _lblHeaderAlerts.BackColor = _header.BackColor;
            _lblHeaderAlerts.ForeColor = Color.FromArgb(255, 196, 84);
            _lblHeaderAlerts.Text = GetCriticalHeaderWarningsText();
            _lblHeaderAlerts.Cursor = Cursors.Hand;
            _lblHeaderAlerts.Click += HeaderAlerts_Click;

            string? roleIconFile = GetRoleIconFileName(_currentUser.Role);
            Image? roleIconImage = string.IsNullOrWhiteSpace(roleIconFile) ? null : BrandingAssets.TryLoadAssetImage(roleIconFile);
            _picRole.Size = new Size(16, 16);
            _picRole.Left = appIcon.Right + 14;
            _picRole.Top = 51;
            _picRole.BackColor = Color.Transparent;
            _picRole.SizeMode = PictureBoxSizeMode.Zoom;
            _picRole.Image = roleIconImage;
            _picRole.Visible = roleIconImage != null;
            if (_picRole.Visible)
            {
                _picRole.Size = new Size(56, 56);
            }

            _lblRole.Text = GetRoleDisplayText(_currentUser.Role);
            _lblRole.AutoSize = true;
            _lblRole.Font = new Font("Bahnschrift SemiBold", 10.5f, FontStyle.Bold);
            _lblRole.Left = appIcon.Right + 14;
            _lblRole.Top = 53;
            _lblRole.BackColor = _header.BackColor;

            _header.Controls.Add(appIcon);
            _header.Controls.Add(_lblTitleOson);
            _header.Controls.Add(_lblHeaderAlerts);
            _header.Controls.Add(_picRole);
            _header.Controls.Add(_lblRole);
            LayoutRoleBadge();
            LayoutHeaderCenterText();
            _header.Resize += (s, e) => LayoutRoleBadge();
            _header.Resize += (s, e) => LayoutHeaderCenterText();

            _body.Dock = DockStyle.Fill;
            _body.Padding = new Padding(26, 20, 26, 12);
            _body.AutoScroll = true;

            _tilesGrid.Dock = DockStyle.Top;
            _tilesGrid.AutoSize = false;
            _tilesGrid.Height = 600;
            _tilesGrid.ColumnCount = TileColumns;
            _tilesGrid.RowCount = 1;
            _tilesGrid.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            _tilesGrid.Padding = new Padding(0, 2, 0, 2);
            _tilesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            _tilesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            _tilesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));

            _actionPanel.Dock = DockStyle.Top;
            _actionPanel.Height = 90;
            _actionPanel.Padding = new Padding(0, 10, 0, 8);

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
            _picUpdateAction.Visible = true;
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
                    new Font("Bahnschrift SemiBold", 11, FontStyle.Bold),
                    _picThemeAction.ClientRectangle,
                    Color.FromArgb(46, 65, 96),
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
                _actionsFlow.Left = Math.Max(0, (_actionPanel.ClientSize.Width - _actionsFlow.Width) / 2);
                _actionsFlow.Top = Math.Max(0, (_actionPanel.ClientSize.Height - _actionsFlow.Height) / 2);
            };

            _lblFooter.Text = "(c) 2026 Osontrack SRM tizimi - Barcha huquqlar himoyalangan";
            _lblFooter.Dock = DockStyle.Top;
            _lblFooter.Height = 44;
            _lblFooter.TextAlign = ContentAlignment.MiddleCenter;
            _lblFooter.Font = new Font("Bahnschrift", 10, FontStyle.Regular);

            _body.Controls.Add(_lblFooter);
            _body.Controls.Add(_actionPanel);
            _body.Controls.Add(_tilesGrid);

            Controls.Add(_body);
            Controls.Add(_header);

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
                _ = CheckForUpdatesAsync();
            };
            FormClosed += (s, e) => _headerWarningsTimer.Stop();

            Resize += (s, e) =>
            {
                LayoutRoleBadge();
                LayoutHeaderCenterText();
                int rows = Math.Max(1, _tilesGrid.RowCount);
                _tilesGrid.Height = (rows * TileRowHeight) + 12;
                UpdateBodyScrollArea();
            };
        }

        private void BuildTiles()
        {
            var defs = new List<TileDefinition>();

            bool isAdmin = string.Equals(_currentUser.Role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase);
            bool isSeller = string.Equals(_currentUser.Role, DatabaseHelper.RoleSeller, StringComparison.OrdinalIgnoreCase);

            if (isAdmin && AuthorizationService.CanViewProducts(_currentUser))
            {
                defs.Add(new TileDefinition("Tovarlar", "Ombor va narxlar", "\uECAA", Color.FromArgb(20, 143, 230), () => new ProductForm(_currentUser).ShowDialog(this), "tile-products.png"));
            }

            if (AuthorizationService.CanCreateSales(_currentUser))
            {
                defs.Add(new TileDefinition("Sotuv", "Yangi savdo operatsiyasi", "\uE7BF", Color.FromArgb(13, 178, 108), () => new SaleForm(_currentUser).ShowDialog(this), "tile-sales.png"));
            }

            if (AuthorizationService.CanManageExpenses(_currentUser))
            {
                defs.Add(new TileDefinition("Rasxod", "Xarajatlarni boshqarish", "\uE8C7", Color.FromArgb(245, 34, 77), () => new ExpenseForm(_currentUser).ShowDialog(this), "tile-expenses.png"));
            }

            if (AuthorizationService.CanManageDebts(_currentUser))
            {
                defs.Add(new TileDefinition("Mijozlar", "Mijozlar bazasi", "\uE77B", Color.FromArgb(237, 146, 0), () => new CustomersForm(_currentUser).ShowDialog(this), "tile-customers.png"));
            }

            if (isAdmin && AuthorizationService.CanViewReports(_currentUser))
            {
                defs.Add(new TileDefinition("Hisobot", "Tahlil va eksport", "\uE9D2", Color.FromArgb(67, 80, 231), () => new ReportForm(_currentUser).ShowDialog(this), "tile-reports.png"));
            }

            if (isAdmin)
            {
                defs.Add(new TileDefinition("Foydalanuvchilar", "Rol va akkauntlar", "\uE716", Color.FromArgb(75, 88, 114), () => new UserManagementForm(_currentUser).ShowDialog(this), "tile-users.png"));
                defs.Add(new TileDefinition("Backup / Restore", "Zaxira va tiklash", "\uE7F1", Color.FromArgb(75, 88, 114), () => new BackupRestoreForm(_currentUser).ShowDialog(this), "tile-backup.png"));
                defs.Add(new TileDefinition("Chek Rekvizitlari", "Chek sozlamalari", "\uE8A5", Color.FromArgb(32, 129, 214), () => new FiscalSettingsForm(_currentUser).ShowDialog(this), "tile-receipt.png"));
                defs.Add(new TileDefinition("Foiz va chegirmalar", "Chegirma sozlamalari", "%", Color.FromArgb(16, 161, 121), () => new PricingDiscountForm(_currentUser).ShowDialog(this), "tile-discount.png"));
            }

            if (isSeller)
            {
                // Seller uchun faqat ishchi kartalar ko'rinadi.
                defs.RemoveAll(x => x.Title != "Sotuv" && x.Title != "Rasxod" && x.Title != "Mijozlar");
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

        private TileUi CreateTile(TileDefinition def)
        {
            Panel card = new Panel
            {
                Margin = new Padding(10),
                Padding = new Padding(20, 18, 20, 16),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill
            };
            UpdateCardRegion(card);

            Image? tileImage = string.IsNullOrWhiteSpace(def.IconImageFile) ? null : BrandingAssets.TryLoadAssetImage(def.IconImageFile);
            bool useImageIcon = tileImage != null;
            Panel iconBox = new Panel
            {
                Size = useImageIcon ? new Size(68, 68) : new Size(54, 54),
                Left = 24,
                Top = 20,
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
                    Font = UiTheme.IconFont(22),
                    ForeColor = Color.White,
                    BackColor = Color.Transparent
                };
            }
            iconBox.Controls.Add(iconControl);

            Label title = new Label
            {
                Text = def.Title,
                AutoSize = false,
                Height = 34,
                Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            Label subtitle = new Label
            {
                Text = def.Subtitle,
                AutoSize = false,
                Height = 24,
                Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular),
                BackColor = Color.Transparent
            };
            UpdateTileLayout(card, iconBox, title, subtitle);

            EventHandler click = (s, e) => def.OnClick();
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
                UpdateTileLayout(card, iconBox, title, subtitle);
            };

            var tileUi = new TileUi(card, title, subtitle, iconBox, iconControl, useImageIcon);
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
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
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
            BackColor = isLight ? Color.FromArgb(230, 236, 245) : Color.FromArgb(12, 18, 29);
            _header.BackColor = Color.FromArgb(20, 30, 44);
            _actionPanel.BackColor = BackColor;
            _actionsFlow.BackColor = BackColor;
            _body.BackColor = BackColor;
            _tilesGrid.BackColor = BackColor;

            _lblTitleOson.BackColor = _header.BackColor;
            _lblHeaderAlerts.BackColor = _header.BackColor;
            _lblRole.BackColor = _header.BackColor;
            _lblTitleOson.ForeColor = Color.FromArgb(238, 245, 255);
            UpdateThemeActionImage();
            UpdateActionImages();
            _roleTextBaseColor = isLight ? Color.FromArgb(156, 201, 255) : Color.FromArgb(168, 212, 255);
            _roleTextPulseColor = isLight ? Color.FromArgb(235, 247, 255) : Color.FromArgb(212, 233, 255);
            UpdateRoleTextColor();
            _lblFooter.ForeColor = isLight ? Color.FromArgb(90, 112, 147) : Color.FromArgb(134, 156, 188);

            foreach (TileUi tile in _tiles)
            {
                tile.BaseColor = isLight ? Color.FromArgb(246, 249, 253) : Color.FromArgb(22, 33, 50);
                tile.HoverColor = isLight ? Color.FromArgb(236, 243, 252) : Color.FromArgb(30, 45, 66);
                tile.Card.BackColor = tile.BaseColor;
                tile.Title.ForeColor = isLight ? Color.FromArgb(37, 50, 74) : Color.FromArgb(238, 244, 252);
                tile.Subtitle.ForeColor = isLight ? Color.FromArgb(90, 110, 138) : Color.FromArgb(156, 179, 207);
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
                    new Font("Bahnschrift SemiBold", 11, FontStyle.Bold),
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

                tile.IconBox.Top = 20 - (int)(3 * tile.HoverProgress);
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
            int contentHeight = _tilesGrid.Height + _actionPanel.Height + _lblFooter.Height + _body.Padding.Top + _body.Padding.Bottom + 24;
            _body.AutoScrollMinSize = new Size(0, contentHeight);
        }

        private static void UpdateCardRegion(Panel card)
        {
            int width = Math.Max(card.Width, 1);
            int height = Math.Max(card.Height, 1);
            card.Region = new Region(RoundedRect(new Rectangle(0, 0, width, height), 16));
        }

        private static void UpdateTileLayout(Panel card, Panel iconBox, Label title, Label subtitle)
        {
            int iconTop = 20;
            iconBox.Left = (card.ClientSize.Width - iconBox.Width) / 2;
            iconBox.Top = iconTop;
            title.SetBounds(14, iconBox.Bottom + 16, Math.Max(1, card.ClientSize.Width - 28), title.Height);
            title.TextAlign = ContentAlignment.MiddleCenter;
            subtitle.SetBounds(14, title.Bottom + 2, Math.Max(1, card.ClientSize.Width - 28), subtitle.Height);
            subtitle.TextAlign = ContentAlignment.MiddleCenter;
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
                _roleLabelBaseTop = _lblRole.Top;
                _picRole.BringToFront();
                _lblRole.BringToFront();
                LayoutHeaderCenterText();
            }
            else
            {
                _lblRole.Left = rightLimit - _lblRole.Width;
                _lblRole.Top = Math.Min(53, _header.Height - _lblRole.Height - 4);
                _roleLabelBaseTop = _lblRole.Top;
                LayoutHeaderCenterText();
            }
        }

        private void LayoutHeaderCenterText()
        {
            int safeLeft = (_appLogoControl?.Right ?? 0) + 18;
            int safeRight = (_picRole.Visible ? _picRole.Left : _header.Width) - 18;
            int maxWidth = Math.Max(220, safeRight - safeLeft);

            int desiredWidth = Math.Min(maxWidth, 840);
            int centeredLeft = (_header.ClientSize.Width - desiredWidth) / 2;
            int left = Math.Max(safeLeft, Math.Min(centeredLeft, safeRight - desiredWidth));

            if (safeRight <= safeLeft + 40)
            {
                left = safeLeft;
                desiredWidth = Math.Max(120, _header.Width - safeLeft - 24);
            }

            _lblTitleOson.SetBounds(left, 8, desiredWidth, 40);
            _lblHeaderAlerts.SetBounds(left, 48, desiredWidth, 22);
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
            _lblHeaderAlerts.ForeColor = _lblHeaderAlerts.Text.StartsWith("OK:", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(117, 228, 169)
                : Color.FromArgb(255, 196, 84);
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
                Font = new Font("Bahnschrift SemiBold", 13, FontStyle.Bold),
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
                Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular),
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
                Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold),
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

            using var form = new DebtorsForm(_currentUser, "Overdue");
            form.ShowDialog(this);
            RefreshHeaderWarnings();
        }

        private void OpenLowStockProductsFromHeader()
        {
            if (!AuthorizationService.CanViewProducts(_currentUser))
            {
                return;
            }

            using var form = new ProductForm(_currentUser, "Kam qolgan");
            form.ShowDialog(this);
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
            lblTitle.Font = new Font("Bahnschrift SemiBold", 16, FontStyle.Bold);
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
                Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(72, 96, 128)
            };
            lblInfo.SetBounds(24, 56, 470, 24);

            Label lblStatus = new Label
            {
                Text = "Yangilash tugmasini bosing.",
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Bahnschrift", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(37, 60, 92)
            };
            lblStatus.SetBounds(24, 90, 470, 58);

            Label lblManual = new Label
            {
                Text = "Internet bo'lmasa qo'lda kiriting (UZS):",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(58, 82, 115)
            };
            lblManual.SetBounds(24, 144, 470, 24);

            TextBox txtManualRate = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 51, 78)
            };
            txtManualRate.SetBounds(24, 170, 220, 32);
            txtManualRate.PlaceholderText = "Masalan: 12650.00";

            Button btnUpdate = new Button
            {
                Text = "Kursni yangilash",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
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
                Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold),
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
                Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold),
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
                FiscalSettings settings = _fiscalSettingsService.Get(_currentUser);
                bool missingFiscal =
                    string.IsNullOrWhiteSpace(settings.BusinessName) ||
                    string.IsNullOrWhiteSpace(settings.TIN) ||
                    string.IsNullOrWhiteSpace(settings.StoreAddress) ||
                    string.IsNullOrWhiteSpace(settings.KkmNumber);
                if (missingFiscal)
                {
                    warnings.Add("Chek rekvizitlari to'liq emas");
                }
            }
            catch
            {
                // Fiskal sozlamalar o'qilmasa davom etamiz.
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






