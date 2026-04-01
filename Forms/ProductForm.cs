using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class ProductForm : Form
    {
        private readonly ProductService _service = new ProductService();
        private readonly DatabaseHelper _dbHelper = new DatabaseHelper();
        private readonly AppUser _currentUser;
        private readonly bool _canManageProducts;
        private readonly string? _initialQuickFilter;

        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _txtSearch = new TextBox();
        private readonly ComboBox _cmbQuickFilter = new ComboBox();
        private readonly Panel _filterWrap = new Panel();
        private readonly Panel _quickFilterArrowMask = new Panel();
        private readonly TextBox _txtName = new TextBox();
        private readonly TextBox _txtPriceUsd = new TextBox();
        private readonly ComboBox _cmbPriceCurrency = new ComboBox();
        private readonly Button _btnCurrencyToggle = new Button();
        private readonly TextBox _txtQty = new TextBox();
        private readonly TextBox _txtPriceUzs = new TextBox();
        private readonly Panel _nameBox = new Panel();
        private readonly Panel _currencyBox = new Panel();
        private readonly Panel _priceUsdBox = new Panel();
        private readonly Panel _qtyBox = new Panel();
        private readonly Panel _priceUzsBox = new Panel();
        private readonly Panel _imageBox = new Panel();
        private readonly Label _lblRate = new Label();
        private readonly Label _lblStatProducts = new Label();
        private readonly Label _lblStatQty = new Label();
        private readonly Label _lblStatValue = new Label();
        private readonly Label _lblFooter = new Label();
        private readonly Panel _rateBadge = new Panel();
        private readonly PictureBox _picNewProductImage = new PictureBox();
        private readonly Button _btnSelectImage = new Button();
        private readonly Button _btnClearImage = new Button();

        private readonly Panel _cardProducts = new Panel();
        private readonly Panel _cardQty = new Panel();
        private readonly Panel _cardValue = new Panel();
        private readonly List<Panel> _statCards = new List<Panel>();
        private readonly BindingSource _gridSource = new BindingSource();
        private readonly System.Windows.Forms.Timer _revealTimer = new System.Windows.Forms.Timer();
        private int _revealIndex;

        private List<Product> _allProducts = new List<Product>();
        private double _usdRate;
        private bool _priceSyncInProgress;
        private bool _initialQuickFilterApplied;
        private string _pendingNewProductImagePath = string.Empty;
        private const string EditColumnName = "EditAction";
        private const string RemoveColumnName = "RemoveAction";
        private string _sortColumn = "MahsulotNomi";
        private bool _sortAscending = true;

        public ProductForm(AppUser currentUser, string? initialQuickFilter = null)
        {
            _currentUser = currentUser;
            _initialQuickFilter = initialQuickFilter;
            AuthorizationService.Require(
                AuthorizationService.CanViewProducts(_currentUser),
                "Sizda ombor bo'limini ko'rish huquqi yo'q.");

            _canManageProducts = AuthorizationService.CanManageProducts(_currentUser);
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            LoadData();
            StartRevealAnimation();
        }

        private void InitializeComponent()
        {
            Text = "Omborxonadagi Tovarlar";
            Size = new Size(1360, 820);
            MinimumSize = new Size(1020, 680);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(233, 239, 247);
            DoubleBuffered = true;
            Font = UiTheme.BodyFont;

            Panel topArea = new Panel
            {
                Dock = DockStyle.Top,
                Height = 376,
                Padding = new Padding(24, 20, 24, 14)
            };

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68
            };

            Panel icon = new Panel
            {
                Left = 0,
                Top = 4,
                Width = 54,
                Height = 54
            };
            Image? productHeaderIcon = BrandingAssets.TryLoadAssetImage("tile-products.png");
            icon.Paint += (s, e) =>
            {
                if (productHeaderIcon != null)
                {
                    return;
                }

                using var brush = new LinearGradientBrush(icon.ClientRectangle, Color.FromArgb(48, 115, 250), Color.FromArgb(70, 61, 231), 45f);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = RoundedRect(icon.ClientRectangle, 16);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, "\uECAA", UiTheme.IconFont(26), icon.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            if (productHeaderIcon != null)
            {
                icon.BackColor = Color.Transparent;
                icon.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = productHeaderIcon
                });
            }

            Label lblTitle = new Label
            {
                Text = "Omborxonadagi Tovarlar",
                AutoSize = true,
                Left = 66,
                Top = 2,
                Font = new Font("Bahnschrift SemiBold", 23, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 45, 68)
            };

            Label lblSub = new Label
            {
                Text = "Mahsulotlar ro'yxati va inventarizatsiya",
                AutoSize = true,
                Left = 68,
                Top = 40,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(82, 103, 133)
            };

            _rateBadge.Width = 270;
            _rateBadge.Height = 42;
            _rateBadge.Top = 10;
            _rateBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _rateBadge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, _rateBadge.Width - 3), Math.Max(1, _rateBadge.Height - 3));
                using SolidBrush brush = new SolidBrush(Color.FromArgb(232, 252, 242));
                using Pen border = new Pen(Color.FromArgb(14, 156, 90), 3f);
                using GraphicsPath path = RoundedRect(bounds, 14);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };

            Image? rateIconImage = BrandingAssets.TryLoadAssetImage("tile-products-rate.png");
            Control lblDollar;
            if (rateIconImage != null)
            {
                lblDollar = new PictureBox
                {
                    Width = 24,
                    Height = 24,
                    Left = 18,
                    Top = 9,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = rateIconImage
                };
            }
            else
            {
                lblDollar = new Label
                {
                    Text = "$",
                    Width = 24,
                    Height = 24,
                    Left = 18,
                    Top = 10,
                    Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold),
                    ForeColor = Color.FromArgb(9, 149, 94),
                    BackColor = Color.Transparent
                };
            }
            _lblRate.AutoSize = true;
            _lblRate.Left = 46;
            _lblRate.Top = 12;
            _lblRate.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
            _lblRate.ForeColor = Color.FromArgb(41, 63, 91);
            _lblRate.BackColor = Color.Transparent;
            _rateBadge.Controls.Add(lblDollar);
            _rateBadge.Controls.Add(_lblRate);

            header.Controls.Add(icon);
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);
            header.Controls.Add(_rateBadge);

            Panel stats = new Panel
            {
                Dock = DockStyle.Top,
                Height = 96,
                Top = 76
            };

            BuildStatCard(_cardProducts, "Jami Tovarlar", _lblStatProducts, Color.FromArgb(57, 123, 226), Color.FromArgb(48, 105, 200), "\uECAA", "tile-products-stat-items.png");
            BuildStatCard(_cardQty, "Jami Miqdor", _lblStatQty, Color.FromArgb(121, 87, 218), Color.FromArgb(103, 70, 196), "\uE9D2", "tile-products-stat-qty.png");
            BuildStatCard(_cardValue, "Umumiy Qiymat", _lblStatValue, Color.FromArgb(24, 156, 109), Color.FromArgb(18, 138, 96), "$", "tile-products-stat-value.png");

            _statCards.Add(_cardProducts);
            _statCards.Add(_cardQty);
            _statCards.Add(_cardValue);
            foreach (var card in _statCards)
            {
                stats.Controls.Add(card);
                AttachCardHover(card);
            }

            Panel inputs = new Panel
            {
                Dock = DockStyle.Top,
                Height = 118,
                Padding = new Padding(0, 14, 0, 0)
            };

            PrepareInputContainer(_nameBox, _txtName, "Nomi");
            PrepareInputContainer(_currencyBox, _cmbPriceCurrency);
            PrepareInputContainer(_priceUsdBox, _txtPriceUsd, "Narx USD");
            PrepareInputContainer(_qtyBox, _txtQty, "Soni");
            PrepareInputContainer(_priceUzsBox, _txtPriceUzs, "Narx UZS");
            _imageBox.BackColor = Color.Transparent;

            _cmbPriceCurrency.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPriceCurrency.FlatStyle = FlatStyle.Flat;
            _cmbPriceCurrency.Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            _cmbPriceCurrency.BackColor = Color.FromArgb(247, 250, 255);
            _cmbPriceCurrency.ForeColor = Color.FromArgb(55, 74, 102);
            _cmbPriceCurrency.Items.AddRange(new object[] { "USD", "UZS" });
            _cmbPriceCurrency.SelectedIndex = 0;
            _cmbPriceCurrency.Visible = false;
            _btnCurrencyToggle.FlatStyle = FlatStyle.Flat;
            _btnCurrencyToggle.FlatAppearance.BorderSize = 0;
            _btnCurrencyToggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(247, 250, 255);
            _btnCurrencyToggle.FlatAppearance.MouseDownBackColor = Color.FromArgb(247, 250, 255);
            _btnCurrencyToggle.UseVisualStyleBackColor = false;
            _btnCurrencyToggle.Font = new Font("Bahnschrift SemiBold", 10.5f, FontStyle.Bold);
            _btnCurrencyToggle.Cursor = Cursors.Hand;
            _btnCurrencyToggle.Text = "USD";
            _btnCurrencyToggle.TextAlign = ContentAlignment.MiddleCenter;
            _btnCurrencyToggle.Click += (s, e) =>
            {
                string current = _cmbPriceCurrency.SelectedItem?.ToString() ?? "USD";
                _cmbPriceCurrency.SelectedItem = current == "USD" ? "UZS" : "USD";
            };
            _currencyBox.Controls.Add(_btnCurrencyToggle);

            _picNewProductImage.BackColor = Color.FromArgb(247, 250, 255);
            _picNewProductImage.BorderStyle = BorderStyle.FixedSingle;
            _picNewProductImage.SizeMode = PictureBoxSizeMode.Zoom;
            _picNewProductImage.SetBounds(0, 0, 70, 70);
            SetNewProductImagePreview(null);

            _btnSelectImage.Text = "Rasm yuklash";
            _btnSelectImage.Width = 122;
            _btnSelectImage.Height = 32;
            _btnSelectImage.Enabled = _canManageProducts;
            _btnSelectImage.FlatStyle = FlatStyle.Flat;
            _btnSelectImage.FlatAppearance.BorderSize = 0;
            _btnSelectImage.BackColor = Color.FromArgb(56, 104, 216);
            _btnSelectImage.ForeColor = Color.White;
            _btnSelectImage.Font = new Font("Bahnschrift SemiBold", 10f, FontStyle.Bold);
            _btnSelectImage.Click += (_, __) => SelectNewProductImage();

            _btnClearImage.Text = "Olib tashlash";
            _btnClearImage.Width = 122;
            _btnClearImage.Height = 32;
            _btnClearImage.Enabled = _canManageProducts;
            _btnClearImage.FlatStyle = FlatStyle.Flat;
            _btnClearImage.FlatAppearance.BorderSize = 0;
            _btnClearImage.BackColor = Color.FromArgb(133, 149, 173);
            _btnClearImage.ForeColor = Color.White;
            _btnClearImage.Font = new Font("Bahnschrift SemiBold", 10f, FontStyle.Bold);
            _btnClearImage.Click += (_, __) =>
            {
                _pendingNewProductImagePath = string.Empty;
                SetNewProductImagePreview(null);
            };

            _imageBox.Controls.Add(_picNewProductImage);
            _imageBox.Controls.Add(_btnSelectImage);
            _imageBox.Controls.Add(_btnClearImage);

            inputs.Controls.Add(_nameBox);
            inputs.Controls.Add(_currencyBox);
            inputs.Controls.Add(_priceUsdBox);
            inputs.Controls.Add(_qtyBox);
            inputs.Controls.Add(_priceUzsBox);
            inputs.Controls.Add(_imageBox);

            Panel searchRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                Padding = new Padding(0, 10, 0, 0)
            };

            Panel searchWrap = new Panel
            {
                Height = 48
            };
            searchWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, searchWrap.Width - 3), Math.Max(1, searchWrap.Height - 3));
                using SolidBrush brush = new SolidBrush(Color.FromArgb(247, 250, 255));
                using Pen border = new Pen(Color.FromArgb(101, 138, 198), 2f);
                using GraphicsPath path = RoundedRect(bounds, 14);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            searchWrap.Resize += (s, e) =>
            {
                searchWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, searchWrap.Width), Math.Max(1, searchWrap.Height)), 14));
            };

            Control searchIcon = new Label
            {
                Text = "\uE721",
                Font = UiTheme.IconFont(16),
                Width = 26,
                Height = 26,
                Left = 12,
                Top = 11,
                ForeColor = Color.FromArgb(126, 147, 174),
                BackColor = Color.Transparent
            };

            _txtSearch.BorderStyle = BorderStyle.None;
            _txtSearch.Left = 44;
            _txtSearch.Top = 14;
            _txtSearch.Width = 600;
            _txtSearch.Font = new Font("Bahnschrift", 14, FontStyle.Regular);
            _txtSearch.PlaceholderText = "Mahsulot nomini yozing...";
            _txtSearch.BackColor = Color.FromArgb(247, 250, 255);
            _txtSearch.ForeColor = Color.FromArgb(47, 64, 90);

            Button btnAdd = CreateActionButton("+  Qo'shish", Color.FromArgb(18, 165, 95), Color.FromArgb(14, 145, 82));
            btnAdd.Enabled = _canManageProducts;
            btnAdd.Click += AddProduct_Click;

            Button btnExport = CreateActionButton("Excel eksport", Color.FromArgb(56, 104, 216), Color.FromArgb(72, 90, 204));
            btnExport.Enabled = _canManageProducts;
            btnExport.Click += Export_Click;

            _filterWrap.Height = 48;
            _filterWrap.BackColor = Color.FromArgb(247, 250, 255);
            _filterWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, _filterWrap.Width - 3), Math.Max(1, _filterWrap.Height - 3));
                using SolidBrush brush = new SolidBrush(Color.FromArgb(247, 250, 255));
                using Pen border = new Pen(Color.FromArgb(101, 138, 198), 2f);
                using GraphicsPath path = RoundedRect(bounds, 12);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            _filterWrap.Resize += (s, e) =>
            {
                _filterWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _filterWrap.Width), Math.Max(1, _filterWrap.Height)), 12));
            };

            _cmbQuickFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbQuickFilter.FlatStyle = FlatStyle.Flat;
            _cmbQuickFilter.Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            _cmbQuickFilter.BackColor = Color.FromArgb(247, 250, 255);
            _cmbQuickFilter.ForeColor = Color.FromArgb(45, 60, 87);
            _cmbQuickFilter.DrawMode = DrawMode.OwnerDrawFixed;
            _cmbQuickFilter.ItemHeight = 24;
            _cmbQuickFilter.DrawItem += QuickFilter_DrawItem;
            _cmbQuickFilter.Items.AddRange(new object[]
            {
                "Hammasi",
                "Kam qolgan",
                "Suratsizlar",
                "Narxi yuqori",
                "Narxi past"
            });
            _cmbQuickFilter.SelectedIndex = 0;
            _cmbQuickFilter.SelectedIndexChanged += (s, e) => ApplySearchAndBind();
            _quickFilterArrowMask.BackColor = Color.FromArgb(247, 250, 255);
            _quickFilterArrowMask.Cursor = Cursors.Hand;
            _quickFilterArrowMask.Click += (s, e) =>
            {
                _cmbQuickFilter.Focus();
                _cmbQuickFilter.DroppedDown = true;
            };
            _filterWrap.Controls.Add(_cmbQuickFilter);
            _filterWrap.Controls.Add(_quickFilterArrowMask);

            searchWrap.Controls.Add(searchIcon);
            searchWrap.Controls.Add(_txtSearch);
            searchRow.Controls.Add(searchWrap);
            searchRow.Controls.Add(_filterWrap);
            searchRow.Controls.Add(btnAdd);
            searchRow.Controls.Add(btnExport);

            Panel gridCard = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 10, 24, 8)
            };

            Panel gridHolder = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(2),
                BackColor = Color.White
            };
            gridHolder.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, gridHolder.Width - 3), Math.Max(1, gridHolder.Height - 3));
                using GraphicsPath path = RoundedRect(bounds, 16);
                using Pen border = new Pen(Color.FromArgb(74, 96, 132), 1.6f);
                gridHolder.Region = new Region(path);
                e.Graphics.DrawPath(border, path);
            };

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoGenerateColumns = false;
            _grid.EnableHeadersVisualStyles = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.None;
            _grid.ScrollBars = ScrollBars.None;
            _grid.GridColor = Color.FromArgb(188, 202, 224);
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(44, 62, 92);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Bahnschrift SemiBold", 13, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(44, 62, 92);
            _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.ColumnHeadersHeight = 54;
            _grid.DefaultCellStyle.BackColor = Color.White;
            _grid.DefaultCellStyle.ForeColor = Color.FromArgb(45, 60, 87);
            _grid.DefaultCellStyle.Font = new Font("Bahnschrift", 13, FontStyle.Regular);
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 243, 252);
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(24, 37, 58);
            _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);
            _grid.RowTemplate.Height = 52;
            _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.MouseWheel += Grid_MouseWheel;
            ConfigureGridColumns();

            _lblFooter.Dock = DockStyle.Bottom;
            _lblFooter.Height = 38;
            _lblFooter.TextAlign = ContentAlignment.MiddleCenter;
            _lblFooter.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            _lblFooter.ForeColor = Color.FromArgb(75, 98, 131);

            gridHolder.Controls.Add(_grid);
            gridCard.Controls.Add(gridHolder);
            gridCard.Controls.Add(_lblFooter);

            topArea.Controls.Add(searchRow);
            topArea.Controls.Add(inputs);
            topArea.Controls.Add(stats);
            topArea.Controls.Add(header);

            Controls.Add(gridCard);
            Controls.Add(topArea);

            Resize += (s, e) =>
            {
                _rateBadge.Left = Math.Max(0, header.ClientSize.Width - _rateBadge.Width);
                LayoutTopBlocks();
            };

            _txtPriceUsd.TextChanged += (s, e) =>
            {
                if (_priceSyncInProgress)
                {
                    return;
                }

                if (string.Equals(_cmbPriceCurrency.SelectedItem?.ToString(), "USD", StringComparison.Ordinal))
                {
                    SyncPriceFieldsFromUsd();
                }
            };

            _txtPriceUzs.TextChanged += (s, e) =>
            {
                if (_priceSyncInProgress)
                {
                    return;
                }

                if (string.Equals(_cmbPriceCurrency.SelectedItem?.ToString(), "UZS", StringComparison.Ordinal))
                {
                    SyncPriceFieldsFromUzs();
                }
            };
            _cmbPriceCurrency.SelectedIndexChanged += (s, e) => ApplyCurrencyInputMode();

            _txtSearch.TextChanged += (s, e) => ApplySearchAndBind();
            _revealTimer.Interval = 85;
            _revealTimer.Tick += RevealTimer_Tick;

            ApplyCurrencyInputMode();
            LayoutTopBlocks();
        }

        private void BuildStatCard(Panel panel, string caption, Label valueLabel, Color c1, Color c2, string glyph, string? iconImageFile = null)
        {
            Image? cardIconImage = string.IsNullOrWhiteSpace(iconImageFile) ? null : BrandingAssets.TryLoadAssetImage(iconImageFile);
            panel.Width = 380;
            panel.Height = 82;
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new LinearGradientBrush(panel.ClientRectangle, c1, c2, 20f);
                using GraphicsPath path = RoundedRect(panel.ClientRectangle, 14);
                e.Graphics.FillPath(brush, path);
                if (cardIconImage != null)
                {
                    Rectangle imageRect = new Rectangle(panel.Width - 68, 14, 50, 50);
                    e.Graphics.DrawImage(cardIconImage, imageRect);
                }
                else
                {
                    Font iconFont = glyph == "$"
                        ? new Font("Bahnschrift SemiBold", 30, FontStyle.Bold)
                        : UiTheme.IconFont(28);
                    TextRenderer.DrawText(
                        e.Graphics,
                        glyph,
                        iconFont,
                        new Rectangle(panel.Width - 72, 14, 52, 52),
                        Color.FromArgb(135, 255, 255, 255),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };

            Label lblCap = new Label
            {
                Text = caption,
                AutoSize = true,
                Left = 16,
                Top = 14,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(230, 245, 255),
                BackColor = Color.Transparent
            };

            valueLabel.AutoSize = true;
            valueLabel.Left = 16;
            valueLabel.Top = 40;
            valueLabel.Font = new Font("Bahnschrift SemiBold", 24, FontStyle.Bold);
            valueLabel.ForeColor = Color.White;
            valueLabel.BackColor = Color.Transparent;

            panel.Controls.Add(lblCap);
            panel.Controls.Add(valueLabel);
        }

        private static void PrepareInputContainer(Panel container, TextBox box, string placeholder)
        {
            container.Height = 40;
            container.Padding = new Padding(12, 8, 12, 8);
            container.BackColor = Color.FromArgb(247, 250, 255);
            container.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, container.Width - 3), Math.Max(1, container.Height - 3));
                using SolidBrush brush = new SolidBrush(Color.FromArgb(247, 250, 255));
                using Pen border = new Pen(Color.FromArgb(101, 138, 198), 2f);
                using GraphicsPath path = RoundedRect(bounds, 11);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            container.Resize += (s, e) =>
            {
                container.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, container.Width), Math.Max(1, container.Height)), 11));
            };

            box.BorderStyle = BorderStyle.None;
            box.Dock = DockStyle.Fill;
            box.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            box.PlaceholderText = placeholder;
            box.BackColor = Color.FromArgb(247, 250, 255);
            box.ForeColor = Color.FromArgb(55, 74, 102);
            container.Controls.Add(box);
        }

        private static void PrepareInputContainer(Panel container, ComboBox box)
        {
            container.Height = 40;
            container.Padding = new Padding(8, 6, 8, 6);
            container.BackColor = Color.FromArgb(247, 250, 255);
            container.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, container.Width - 3), Math.Max(1, container.Height - 3));
                using SolidBrush brush = new SolidBrush(Color.FromArgb(247, 250, 255));
                using Pen border = new Pen(Color.FromArgb(101, 138, 198), 2f);
                using GraphicsPath path = RoundedRect(bounds, 11);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            container.Resize += (s, e) =>
            {
                container.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, container.Width), Math.Max(1, container.Height)), 11));
            };

            box.Dock = DockStyle.Fill;
            box.FlatStyle = FlatStyle.Flat;
            box.BackColor = Color.FromArgb(247, 250, 255);
            box.ForeColor = Color.FromArgb(55, 74, 102);
            container.Controls.Add(box);
        }

        private void ApplyCurrencyInputMode()
        {
            bool isUsdInput = !string.Equals(_cmbPriceCurrency.SelectedItem?.ToString(), "UZS", StringComparison.Ordinal);
            UpdateCurrencySelectorVisual(isUsdInput);
            _txtPriceUsd.ReadOnly = !isUsdInput;
            _txtPriceUzs.ReadOnly = isUsdInput;

            _txtPriceUsd.BackColor = isUsdInput ? Color.FromArgb(247, 250, 255) : Color.FromArgb(241, 245, 251);
            _txtPriceUzs.BackColor = isUsdInput ? Color.FromArgb(241, 245, 251) : Color.FromArgb(247, 250, 255);

            if (isUsdInput)
            {
                SyncPriceFieldsFromUsd();
            }
            else
            {
                SyncPriceFieldsFromUzs();
            }
        }

        private void UpdateCurrencySelectorVisual(bool isUsdInput)
        {
            _btnCurrencyToggle.Text = isUsdInput ? "USD" : "UZS";
            _btnCurrencyToggle.BackColor = Color.FromArgb(247, 250, 255);
            _btnCurrencyToggle.ForeColor = Color.FromArgb(55, 74, 102);
        }

        private void SyncPriceFieldsFromUsd()
        {
            _priceSyncInProgress = true;
            if (double.TryParse(_txtPriceUsd.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double usd) && usd > 0)
            {
                _txtPriceUzs.Text = (usd * _usdRate).ToString("N0", CultureInfo.CurrentCulture);
            }
            else
            {
                _txtPriceUzs.Clear();
            }
            _priceSyncInProgress = false;
        }

        private void SyncPriceFieldsFromUzs()
        {
            _priceSyncInProgress = true;
            if (double.TryParse(_txtPriceUzs.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double uzs) && uzs > 0)
            {
                double safeRate = _usdRate > 0 ? _usdRate : 12500;
                _txtPriceUsd.Text = (uzs / safeRate).ToString("N4", CultureInfo.CurrentCulture);
            }
            else
            {
                _txtPriceUsd.Clear();
            }
            _priceSyncInProgress = false;
        }

        private Button CreateActionButton(string text, Color c1, Color c2)
        {
            Button btn = new Button
            {
                Text = text,
                Height = 48,
                Width = 176,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                ForeColor = Color.White,
                Font = new Font("Bahnschrift SemiBold", 11.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new LinearGradientBrush(btn.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(btn.ClientRectangle, 14);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, btn.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btn.Resize += (s, e) =>
            {
                btn.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btn.Width), Math.Max(1, btn.Height)), 14));
            };
            btn.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btn.Width), Math.Max(1, btn.Height)), 14));

            btn.MouseDown += (s, e) => btn.Top += 1;
            btn.MouseUp += (s, e) => btn.Top -= 1;
            btn.MouseLeave += (s, e) => { };
            return btn;
        }

        private void LayoutTopBlocks()
        {
            Panel? topArea = Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Top);
            if (topArea == null)
            {
                return;
            }

            Panel header = topArea.Controls[3] as Panel ?? new Panel();
            Panel stats = topArea.Controls[2] as Panel ?? new Panel();
            Panel inputs = topArea.Controls[1] as Panel ?? new Panel();
            Panel searchRow = topArea.Controls[0] as Panel ?? new Panel();
            _rateBadge.Left = Math.Max(0, header.ClientSize.Width - _rateBadge.Width);

            int fullWidth = topArea.ClientSize.Width;
            int gap = 16;
            int cardWidth = (fullWidth - gap * 2) / 3;
            cardWidth = Math.Max(320, Math.Min(cardWidth, 390));
            int cardsBlockWidth = (cardWidth * 3) + (gap * 2);
            int cardsLeft = Math.Max(0, (fullWidth - cardsBlockWidth) / 2);

            _cardProducts.SetBounds(cardsLeft, 10, cardWidth, 82);
            _cardQty.SetBounds(cardsLeft + cardWidth + gap, 10, cardWidth, 82);
            _cardValue.SetBounds(cardsLeft + (cardWidth + gap) * 2, 10, cardWidth, 82);

            _nameBox.SetBounds(0, 10, 210, 40);
            _currencyBox.SetBounds(220, 10, 80, 40);
            _priceUsdBox.SetBounds(310, 10, 130, 40);
            _qtyBox.SetBounds(450, 10, 120, 40);
            _priceUzsBox.SetBounds(580, 10, 150, 40);
            _imageBox.SetBounds(0, 58, Math.Max(420, fullWidth - 20), 48);
            _picNewProductImage.SetBounds(0, 0, 46, 46);
            _btnSelectImage.SetBounds(58, 7, 132, 32);
            _btnClearImage.SetBounds(198, 7, 132, 32);
            _btnCurrencyToggle.SetBounds(7, 6, 66, 28);

            Panel searchWrap = searchRow.Controls.OfType<Panel>().FirstOrDefault() ?? new Panel();
            Button[] actionBtns = searchRow.Controls.OfType<Button>().ToArray();
            int rowWidth = searchRow.ClientSize.Width;

            if (actionBtns.Length >= 2)
            {
                int gapSearchToBtn = 12;
                int gapFilterToBtn = 10;
                int gapBetweenBtns = 10;
                int minSearchWidth = 260;
                int filterWidth = 190;

                int btnAddWidth = actionBtns[0].Width;
                int btnExportWidth = actionBtns[1].Width;

                int requiredWidth = minSearchWidth + gapSearchToBtn + filterWidth + gapFilterToBtn + gapBetweenBtns + btnAddWidth + btnExportWidth;
                if (rowWidth < requiredWidth)
                {
                    int availableButtons = Math.Max(240, rowWidth - minSearchWidth - gapSearchToBtn - filterWidth - gapFilterToBtn - gapBetweenBtns);
                    btnAddWidth = Math.Max(110, availableButtons / 2);
                    btnExportWidth = Math.Max(120, availableButtons - btnAddWidth);
                    filterWidth = Math.Max(145, rowWidth - btnAddWidth - btnExportWidth - gapBetweenBtns - gapFilterToBtn - gapSearchToBtn - 180);
                }

                actionBtns[0].Width = btnAddWidth;
                actionBtns[1].Width = btnExportWidth;

                actionBtns[1].SetBounds(rowWidth - actionBtns[1].Width, 10, actionBtns[1].Width, 48);
                actionBtns[0].SetBounds(actionBtns[1].Left - gapBetweenBtns - actionBtns[0].Width, 10, actionBtns[0].Width, 48);
                _filterWrap.SetBounds(actionBtns[0].Left - gapFilterToBtn - filterWidth, 10, filterWidth, 48);
                _cmbQuickFilter.SetBounds(10, 10, Math.Max(90, _filterWrap.Width - 20), 28);
                _quickFilterArrowMask.SetBounds(Math.Max(0, _filterWrap.Width - 30), 6, 20, Math.Max(1, _filterWrap.Height - 12));
                _quickFilterArrowMask.BringToFront();

                int searchWidth = Math.Max(180, _filterWrap.Left - gapSearchToBtn);
                searchWrap.SetBounds(0, 10, searchWidth, 48);
            }
            else
            {
                searchWrap.SetBounds(0, 10, rowWidth, 48);
                _filterWrap.SetBounds(0, 10, 0, 0);
                _quickFilterArrowMask.SetBounds(0, 0, 0, 0);
            }

            _txtSearch.Width = Math.Max(100, searchWrap.Width - 54);
        }

        private void ConfigureGridColumns()
        {
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                DataPropertyName = "ProductId",
                Visible = false
            });

            _grid.Columns.Add(new DataGridViewImageColumn
            {
                Name = "Rasm",
                DataPropertyName = "PreviewImage",
                HeaderText = "Rasm",
                Width = 64,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                DefaultCellStyle = { NullValue = null }
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MahsulotNomi",
                DataPropertyName = "MahsulotNomi",
                HeaderText = "Mahsulot nomi",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Soni",
                DataPropertyName = "Soni",
                HeaderText = "Soni",
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Format = "#,##0.##"
                }
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NarxiUSD",
                DataPropertyName = "NarxiUSD",
                HeaderText = "Narxi (USD)",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NarxiUZS",
                DataPropertyName = "NarxiUZS",
                HeaderText = "Narxi (UZS)",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            Image? editIcon = BrandingAssets.TryLoadAssetImage("edit-pencil.png");
            if (editIcon != null)
            {
                _grid.Columns.Add(new DataGridViewImageColumn
                {
                    Name = EditColumnName,
                    HeaderText = "Tahrir",
                    Image = editIcon,
                    ImageLayout = DataGridViewImageCellLayout.Zoom,
                    Width = 64,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Visible = _canManageProducts
                });
            }
            else
            {
                _grid.Columns.Add(new DataGridViewButtonColumn
                {
                    Name = EditColumnName,
                    HeaderText = "Tahrir",
                    Text = "Tahrir",
                    UseColumnTextForButtonValue = true,
                    FlatStyle = FlatStyle.Flat,
                    Width = 64,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Visible = _canManageProducts
                });
            }

            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = RemoveColumnName,
                HeaderText = "O'chirish",
                Text = "O'chirish",
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                Width = 92,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Visible = _canManageProducts
            });

            _grid.DataSource = _gridSource;
        }

        private void AddProduct_Click(object? sender, EventArgs e)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageProducts(_currentUser),
                "Mahsulot qo'shish uchun admin huquqi kerak.");

            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Mahsulot nomini kiriting.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string currency = string.Equals(_cmbPriceCurrency.SelectedItem?.ToString(), "UZS", StringComparison.Ordinal) ? "UZS" : "USD";
            double enteredPrice;
            if (currency == "USD")
            {
                if (!double.TryParse(_txtPriceUsd.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out enteredPrice) || enteredPrice <= 0)
                {
                    MessageBox.Show("USD narx musbat son bo'lishi kerak.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                if (!double.TryParse(_txtPriceUzs.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out enteredPrice) || enteredPrice <= 0)
                {
                    MessageBox.Show("UZS narx musbat son bo'lishi kerak.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (!double.TryParse(_txtQty.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double qty) || qty <= 0)
            {
                MessageBox.Show("Soni musbat son bo'lishi kerak.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string savedImagePath = string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(_pendingNewProductImagePath))
                {
                    savedImagePath = ProductImageStore.SaveFromSource(_pendingNewProductImagePath);
                }

                _service.Add(new Product
                {
                    Name = name,
                    PurchaseCurrency = currency,
                    PurchasePrice = enteredPrice,
                    PurchasePriceUZS = currency == "USD" ? enteredPrice * _usdRate : enteredPrice,
                    PurchasePriceUSD = currency == "USD" ? enteredPrice : enteredPrice / (_usdRate > 0 ? _usdRate : 12500),
                    QuantityUSD = qty,
                    ImagePath = savedImagePath
                }, _currentUser);

                _txtName.Clear();
                _txtPriceUsd.Clear();
                _txtQty.Clear();
                _txtPriceUzs.Clear();
                _cmbPriceCurrency.SelectedItem = "USD";
                _pendingNewProductImagePath = string.Empty;
                SetNewProductImagePreview(null);
                LoadData();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(savedImagePath))
                {
                    ProductImageStore.DeleteImage(savedImagePath);
                }
                MessageBox.Show($"Mahsulotni saqlashda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectNewProductImage()
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Rasm fayllari|*.jpg;*.jpeg;*.png;*.bmp;*.webp",
                Title = "Mahsulot rasmi"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _pendingNewProductImagePath = ofd.FileName;
            SetNewProductImagePreview(_pendingNewProductImagePath);
        }

        private void SetNewProductImagePreview(string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                _picNewProductImage.Image = BrandingAssets.TryLoadAssetImage("tile-products.png");
                return;
            }

            try
            {
                using Bitmap loaded = new Bitmap(sourcePath);
                _picNewProductImage.Image = new Bitmap(loaded);
            }
            catch
            {
                _picNewProductImage.Image = null;
            }
        }

        private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (!_canManageProducts)
            {
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            string actionColumn = _grid.Columns[e.ColumnIndex].Name;
            if (!string.Equals(actionColumn, EditColumnName, StringComparison.Ordinal) &&
                !string.Equals(actionColumn, RemoveColumnName, StringComparison.Ordinal))
            {
                return;
            }

            if (_grid.Rows[e.RowIndex].DataBoundItem is not ProductGridRow row)
            {
                return;
            }

            Product? selected = _allProducts.FirstOrDefault(p => p.Id == row.ProductId);
            if (selected == null)
            {
                MessageBox.Show("Mahsulot topilmadi. Ro'yxatni yangilang.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.Equals(actionColumn, RemoveColumnName, StringComparison.Ordinal))
            {
                TryRemoveProductFromList(selected);
                return;
            }

            if (!TryCollectProductUpdates(
                    selected,
                    out string? newName,
                    out double? newPurchasePrice,
                    out double? newQuantity,
                    out bool updateImagePath,
                    out string? newImagePath,
                    out string? oldImagePathToDelete,
                    out string? newImagePathToRollback))
            {
                return;
            }

            try
            {
                _service.UpdateProduct(selected.Id, newName, newPurchasePrice, newQuantity, _currentUser, updateImagePath, newImagePath);
                if (!string.IsNullOrWhiteSpace(oldImagePathToDelete))
                {
                    ProductImageStore.DeleteImage(oldImagePathToDelete);
                }
                LoadData();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(newImagePathToRollback))
                {
                    ProductImageStore.DeleteImage(newImagePathToRollback);
                }
                MessageBox.Show($"Tahrirlash xatosi: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TryRemoveProductFromList(Product product)
        {
            DialogResult confirm = MessageBox.Show(
                $"\"{product.Name}\" mahsulotini ro'yxatdan yashirishni xohlaysizmi?\n\n" +
                "Bu amal mahsulot qoldig'ini 0 ga tushiradi va u ro'yxatda ko'rinmaydi.",
                "Tasdiqlash",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _service.RemoveFromList(product.Id, _currentUser);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"O'chirish xatosi: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool TryCollectProductUpdates(
            Product product,
            out string? newName,
            out double? newPurchasePrice,
            out double? newQuantity,
            out bool updateImagePath,
            out string? newImagePath,
            out string? oldImagePathToDelete,
            out string? newImagePathToRollback)
        {
            newName = null;
            newPurchasePrice = null;
            newQuantity = null;
            updateImagePath = false;
            newImagePath = null;
            oldImagePathToDelete = null;
            newImagePathToRollback = null;
            string? candidateName = null;
            double? candidatePurchasePrice = null;
            double? candidateQuantity = null;
            bool candidateUpdateImagePath = false;
            string? candidateImagePath = null;
            string? candidateOldImagePathToDelete = null;
            string? candidateNewImagePathToRollback = null;
            string? selectedImageSourcePath = null;
            bool removeImage = false;
            bool imageTouched = false;

            using Form dialog = new Form
            {
                Text = " ",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(620, 472),
                BackColor = Color.FromArgb(236, 242, 251),
                Font = UiTheme.BodyFont
            };

            Panel header = new Panel
            {
                Left = 16,
                Top = 14,
                Width = 588,
                Height = 72
            };
            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new LinearGradientBrush(header.ClientRectangle, Color.FromArgb(46, 121, 248), Color.FromArgb(75, 85, 235), 0f);
                using GraphicsPath path = RoundedRect(new Rectangle(0, 0, header.Width - 1, header.Height - 1), 14);
                e.Graphics.FillPath(brush, path);
            };
            Label lblTitle = new Label
            {
                Text = "Mahsulotni tahrirlash",
                Left = 18,
                Top = 12,
                Width = 360,
                Height = 26,
                Font = new Font("Bahnschrift SemiBold", 16, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            Label lblHint = new Label
            {
                Left = 18,
                Top = 40,
                Width = 550,
                Height = 20,
                Text = "Faqat kerakli maydonni belgilang. Belgilanmagan maydonlar o'zgarmaydi.",
                Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(227, 239, 255),
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblHint);

            Panel card = new Panel
            {
                Left = 16,
                Top = 94,
                Width = 588,
                Height = 312,
                BackColor = Color.FromArgb(236, 242, 251)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, card.Width - 3), Math.Max(1, card.Height - 3));
                using SolidBrush brush = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(177, 196, 225), 1.6f);
                using GraphicsPath path = RoundedRect(bounds, 14);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };

            Label lblName = new Label
            {
                Left = 18,
                Top = 18,
                Width = 96,
                Height = 24,
                Text = "Nomi",
                TextAlign = ContentAlignment.MiddleLeft
            };
            CheckBox chkName = new CheckBox
            {
                Left = 116,
                Top = 16,
                Width = 56,
                Height = 28,
                Text = string.Empty
            };
            TextBox txtName = new TextBox
            {
                Left = 10,
                Top = 8,
                Width = 396,
                Enabled = false,
                Text = product.Name
            };
            Panel wrapName = CreateEditInputWrap(txtName);
            wrapName.SetBounds(186, 12, 384, 36);

            Label lblPrice = new Label
            {
                Left = 18,
                Top = 76,
                Width = 96,
                Height = 24,
                Text = $"Narx {product.PurchaseCurrency}",
                TextAlign = ContentAlignment.MiddleLeft
            };
            CheckBox chkPrice = new CheckBox
            {
                Left = 116,
                Top = 74,
                Width = 56,
                Height = 28,
                Text = string.Empty
            };
            TextBox txtPrice = new TextBox
            {
                Left = 10,
                Top = 8,
                Width = 396,
                Enabled = false,
                Text = product.PurchasePrice.ToString("0.##", CultureInfo.CurrentCulture)
            };
            Panel wrapPrice = CreateEditInputWrap(txtPrice);
            wrapPrice.SetBounds(186, 70, 384, 36);

            Label lblQty = new Label
            {
                Left = 18,
                Top = 134,
                Width = 96,
                Height = 24,
                Text = "Soni",
                TextAlign = ContentAlignment.MiddleLeft
            };
            CheckBox chkQty = new CheckBox
            {
                Left = 116,
                Top = 132,
                Width = 56,
                Height = 28,
                Text = string.Empty
            };
            TextBox txtQty = new TextBox
            {
                Left = 10,
                Top = 8,
                Width = 396,
                Enabled = false,
                Text = product.QuantityUSD.ToString("0.##", CultureInfo.CurrentCulture)
            };
            Panel wrapQty = CreateEditInputWrap(txtQty);
            wrapQty.SetBounds(186, 128, 384, 36);

            Label lblImage = new Label
            {
                Left = 18,
                Top = 188,
                Width = 96,
                Height = 24,
                Text = "Rasm",
                TextAlign = ContentAlignment.MiddleLeft
            };
            CheckBox chkImage = new CheckBox
            {
                Left = 116,
                Top = 186,
                Width = 56,
                Height = 28,
                Text = string.Empty
            };
            PictureBox picImage = new PictureBox
            {
                Left = 186,
                Top = 184,
                Width = 74,
                Height = 74,
                BackColor = Color.FromArgb(247, 250, 255),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            Button btnPickImage = new Button
            {
                Text = "Rasm yuklash",
                Left = 272,
                Top = 186,
                Width = 138,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(56, 104, 216),
                ForeColor = Color.White,
                Enabled = false
            };
            btnPickImage.FlatAppearance.BorderSize = 0;
            Button btnRemoveImage = new Button
            {
                Text = "Rasmni olib tashlash",
                Left = 420,
                Top = 186,
                Width = 150,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(133, 149, 173),
                ForeColor = Color.White,
                Enabled = false
            };
            btnRemoveImage.FlatAppearance.BorderSize = 0;

            Image? currentPreview = ProductImageStore.TryLoadPreview(product.ImagePath, picImage.Width - 8, picImage.Height - 8);
            picImage.Image = currentPreview;

            chkImage.CheckedChanged += (_, __) =>
            {
                bool enabled = chkImage.Checked;
                btnPickImage.Enabled = enabled;
                btnRemoveImage.Enabled = enabled;
                if (!enabled)
                {
                    selectedImageSourcePath = null;
                    removeImage = false;
                    imageTouched = false;
                    picImage.Image = ProductImageStore.TryLoadPreview(product.ImagePath, picImage.Width - 8, picImage.Height - 8);
                }
            };

            btnPickImage.Click += (_, __) =>
            {
                using OpenFileDialog ofd = new OpenFileDialog
                {
                    Filter = "Rasm fayllari|*.jpg;*.jpeg;*.png;*.bmp;*.webp",
                    Title = "Mahsulot rasmi"
                };
                if (ofd.ShowDialog(dialog) != DialogResult.OK)
                {
                    return;
                }

                selectedImageSourcePath = ofd.FileName;
                removeImage = false;
                imageTouched = true;
                try
                {
                    using Bitmap preview = new Bitmap(selectedImageSourcePath);
                    picImage.Image = new Bitmap(preview);
                }
                catch
                {
                    picImage.Image = null;
                }
            };

            btnRemoveImage.Click += (_, __) =>
            {
                selectedImageSourcePath = null;
                removeImage = true;
                imageTouched = true;
                picImage.Image = null;
            };

            StyleEditInput(txtName);
            StyleEditInput(txtPrice);
            StyleEditInput(txtQty);
            StyleEditSwitch(chkName);
            StyleEditSwitch(chkPrice);
            StyleEditSwitch(chkQty);
            StyleEditSwitch(chkImage);
            StyleEditFieldLabel(lblName);
            StyleEditFieldLabel(lblPrice);
            StyleEditFieldLabel(lblQty);
            StyleEditFieldLabel(lblImage);

            chkName.CheckedChanged += (_, __) => SetEditInputEnabled(txtName, chkName.Checked);
            chkPrice.CheckedChanged += (_, __) => SetEditInputEnabled(txtPrice, chkPrice.Checked);
            chkQty.CheckedChanged += (_, __) => SetEditInputEnabled(txtQty, chkQty.Checked);

            Button btnSave = new Button
            {
                Text = "Saqlash",
                Left = 390,
                Top = 408,
                Width = 102,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold)
            };
            Button btnCancel = new Button
            {
                Text = "Bekor",
                Left = 502,
                Top = 408,
                Width = 102,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold),
                DialogResult = DialogResult.Cancel
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.BorderSize = 0;
            AttachGradientButton(btnSave, Color.FromArgb(14, 178, 96), Color.FromArgb(7, 150, 83));
            AttachGradientButton(btnCancel, Color.FromArgb(130, 144, 166), Color.FromArgb(101, 117, 142));
            SetEditInputEnabled(txtName, false);
            SetEditInputEnabled(txtPrice, false);
            SetEditInputEnabled(txtQty, false);

            btnSave.Click += (_, __) =>
            {
                if (!chkName.Checked && !chkPrice.Checked && !chkQty.Checked && !chkImage.Checked)
                {
                    MessageBox.Show("Kamida bitta maydonni belgilang.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (chkName.Checked)
                {
                    string candidate = txtName.Text.Trim();
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        MessageBox.Show("Mahsulot nomi bo'sh bo'lmasligi kerak.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!string.Equals(candidate, product.Name, StringComparison.Ordinal))
                    {
                        candidateName = candidate;
                    }
                }

                if (chkPrice.Checked)
                {
                    if (!double.TryParse(txtPrice.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsedPrice) || parsedPrice <= 0)
                    {
                        MessageBox.Show("Narx musbat son bo'lishi kerak.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (Math.Abs(parsedPrice - product.PurchasePrice) > 0.000001)
                    {
                        candidatePurchasePrice = parsedPrice;
                    }
                }

                if (chkQty.Checked)
                {
                    if (!double.TryParse(txtQty.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsedQty) || parsedQty < 0)
                    {
                        MessageBox.Show("Soni manfiy bo'lmasligi kerak.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (Math.Abs(parsedQty - product.QuantityUSD) > 0.000001)
                    {
                        candidateQuantity = parsedQty;
                    }
                }

                if (chkImage.Checked)
                {
                    if (removeImage)
                    {
                        candidateUpdateImagePath = true;
                        candidateImagePath = string.Empty;
                    }
                    else if (!string.IsNullOrWhiteSpace(selectedImageSourcePath))
                    {
                        try
                        {
                            string saved = ProductImageStore.SaveFromSource(selectedImageSourcePath);
                            candidateUpdateImagePath = true;
                            candidateImagePath = saved;
                            candidateNewImagePathToRollback = saved;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Rasmni saqlashda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    else if (imageTouched)
                    {
                        candidateUpdateImagePath = true;
                        candidateImagePath = removeImage ? string.Empty : product.ImagePath;
                    }
                }

                if (candidateName == null && candidatePurchasePrice == null && candidateQuantity == null && !candidateUpdateImagePath)
                {
                    MessageBox.Show("O'zgarish aniqlanmadi.", "Ma'lumot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (candidateUpdateImagePath && !string.IsNullOrWhiteSpace(product.ImagePath) && !string.Equals(product.ImagePath, candidateImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    candidateOldImagePathToDelete = product.ImagePath;
                }

                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };

            card.Controls.Add(lblName);
            card.Controls.Add(chkName);
            card.Controls.Add(wrapName);
            card.Controls.Add(lblPrice);
            card.Controls.Add(chkPrice);
            card.Controls.Add(wrapPrice);
            card.Controls.Add(lblQty);
            card.Controls.Add(chkQty);
            card.Controls.Add(wrapQty);
            card.Controls.Add(lblImage);
            card.Controls.Add(chkImage);
            card.Controls.Add(picImage);
            card.Controls.Add(btnPickImage);
            card.Controls.Add(btnRemoveImage);

            dialog.Controls.Add(header);
            dialog.Controls.Add(card);
            dialog.Controls.Add(btnSave);
            dialog.Controls.Add(btnCancel);
            dialog.AcceptButton = btnSave;
            dialog.CancelButton = btnCancel;

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            newName = candidateName;
            newPurchasePrice = candidatePurchasePrice;
            newQuantity = candidateQuantity;
            updateImagePath = candidateUpdateImagePath;
            newImagePath = candidateImagePath;
            oldImagePathToDelete = candidateOldImagePathToDelete;
            newImagePathToRollback = candidateNewImagePathToRollback;
            return true;
        }

        private static void StyleEditInput(TextBox box)
        {
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Color.FromArgb(248, 251, 255);
            box.ForeColor = Color.FromArgb(50, 66, 92);
            box.Font = new Font("Bahnschrift", 11.5f, FontStyle.Regular);
        }

        private static void SetEditInputEnabled(TextBox box, bool enabled)
        {
            box.Enabled = enabled;
            box.BackColor = enabled ? Color.FromArgb(248, 251, 255) : Color.FromArgb(241, 245, 251);
            box.ForeColor = enabled ? Color.FromArgb(50, 66, 92) : Color.FromArgb(122, 137, 159);
        }

        private static void StyleEditFieldLabel(Label lbl)
        {
            lbl.Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold);
            lbl.ForeColor = Color.FromArgb(53, 70, 96);
            lbl.BackColor = Color.Transparent;
        }

        private static void StyleEditSwitch(CheckBox cb)
        {
            cb.Appearance = Appearance.Button;
            cb.FlatStyle = FlatStyle.Flat;
            cb.FlatAppearance.BorderSize = 0;
            cb.FlatAppearance.CheckedBackColor = Color.Transparent;
            cb.FlatAppearance.MouseDownBackColor = Color.Transparent;
            cb.FlatAppearance.MouseOverBackColor = Color.Transparent;
            cb.UseVisualStyleBackColor = false;
            cb.BackColor = Color.Transparent;
            cb.Cursor = Cursors.Hand;
            cb.AutoSize = false;

            cb.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, cb.Width - 3), Math.Max(1, cb.Height - 3));
                Color trackOn = Color.FromArgb(22, 171, 95);
                Color trackOff = Color.FromArgb(164, 182, 209);
                using SolidBrush trackBrush = new SolidBrush(cb.Checked ? trackOn : trackOff);
                using GraphicsPath trackPath = RoundedRect(bounds, cb.Height / 2);
                e.Graphics.FillPath(trackBrush, trackPath);

                int knobSize = Math.Max(10, cb.Height - 8);
                int knobX = cb.Checked ? bounds.Right - knobSize - 2 : bounds.Left + 2;
                Rectangle knob = new Rectangle(knobX, bounds.Top + 2, knobSize, knobSize);
                using SolidBrush knobBrush = new SolidBrush(Color.White);
                e.Graphics.FillEllipse(knobBrush, knob);
                using Pen knobBorder = new Pen(Color.FromArgb(206, 214, 228), 1f);
                e.Graphics.DrawEllipse(knobBorder, knob);
            };

            cb.CheckedChanged += (s, e) => cb.Invalidate();
        }

        private static Panel CreateEditInputWrap(TextBox box)
        {
            Panel wrap = new Panel
            {
                BackColor = Color.FromArgb(248, 251, 255),
                Padding = new Padding(10, 8, 10, 8)
            };
            wrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, wrap.Width - 3), Math.Max(1, wrap.Height - 3));
                Color borderColor = box.Enabled ? Color.FromArgb(88, 120, 176) : Color.FromArgb(183, 197, 218);
                using SolidBrush brush = new SolidBrush(box.Enabled ? Color.FromArgb(248, 251, 255) : Color.FromArgb(241, 245, 251));
                using Pen border = new Pen(borderColor, 2f);
                using GraphicsPath path = RoundedRect(bounds, 10);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            wrap.Resize += (s, e) =>
            {
                wrap.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, wrap.Width), Math.Max(1, wrap.Height)), 10));
            };
            box.EnabledChanged += (s, e) => wrap.Invalidate();
            box.Dock = DockStyle.Fill;
            wrap.Controls.Add(box);
            return wrap;
        }

        private static void AttachGradientButton(Button btn, Color c1, Color c2)
        {
            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new LinearGradientBrush(btn.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(new Rectangle(0, 0, Math.Max(1, btn.Width - 1), Math.Max(1, btn.Height - 1)), 10);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, btn.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btn.Resize += (s, e) =>
            {
                btn.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btn.Width), Math.Max(1, btn.Height)), 10));
            };
            btn.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btn.Width), Math.Max(1, btn.Height)), 10));
        }

        private void Export_Click(object? sender, EventArgs e)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageProducts(_currentUser),
                "Excel eksport uchun admin huquqi kerak.");

            if (_allProducts.Count == 0)
            {
                MessageBox.Show("Eksport qilish uchun omborda mahsulot yo'q.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"ombor_qoldiqlari_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                ExcelExportHelper.ExportInventory(_allProducts, _usdRate, dialog.FileName);
                MessageBox.Show("Excel eksport muvaffaqiyatli yaratildi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eksport xatosi: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadData()
        {
            _usdRate = GetCurrentRate();
            _allProducts = _service.GetAll();
            _lblRate.Text = $"1 USD = {_usdRate.ToString("N0", CultureInfo.InvariantCulture)} UZS";
            TryApplyInitialQuickFilter();
            ApplyCurrencyInputMode();
            ApplySearchAndBind();
            RefreshStats(GetActiveProducts(_allProducts));
        }

        private void TryApplyInitialQuickFilter()
        {
            if (_initialQuickFilterApplied)
            {
                return;
            }

            string requested = _initialQuickFilter?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(requested))
            {
                _initialQuickFilterApplied = true;
                return;
            }

            for (int i = 0; i < _cmbQuickFilter.Items.Count; i++)
            {
                if (string.Equals(_cmbQuickFilter.Items[i]?.ToString(), requested, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbQuickFilter.SelectedIndex = i;
                    _initialQuickFilterApplied = true;
                    return;
                }
            }

            _initialQuickFilterApplied = true;
        }

        private void ApplySearchAndBind()
        {
            string searchText = _txtSearch.Text.Trim().ToLowerInvariant();
            IEnumerable<Product> filtered = GetActiveProducts(_allProducts)
                .Where(p => p.Name.ToLowerInvariant().Contains(searchText));

            string mode = _cmbQuickFilter.SelectedItem?.ToString() ?? "Hammasi";
            if (mode == "Kam qolgan")
            {
                filtered = filtered.Where(p => p.QuantityUSD <= 5);
            }
            else if (mode == "Suratsizlar")
            {
                filtered = filtered.Where(p => string.IsNullOrWhiteSpace(p.ImagePath));
            }
            else if (mode == "Narxi yuqori")
            {
                _sortColumn = "NarxiUZS";
                _sortAscending = false;
            }
            else if (mode == "Narxi past")
            {
                _sortColumn = "NarxiUZS";
                _sortAscending = true;
            }

            List<Product> filteredList = filtered.ToList();
            filteredList = ApplySort(filteredList);
            BindGrid(filteredList);
        }

        private List<Product> ApplySort(List<Product> products)
        {
            return (_sortColumn, _sortAscending) switch
            {
                ("Soni", true) => products.OrderBy(p => p.QuantityUSD).ThenBy(p => p.Name).ToList(),
                ("Soni", false) => products.OrderByDescending(p => p.QuantityUSD).ThenBy(p => p.Name).ToList(),
                ("NarxiUSD", true) => products.OrderBy(p => p.PurchasePriceUSD).ThenBy(p => p.Name).ToList(),
                ("NarxiUSD", false) => products.OrderByDescending(p => p.PurchasePriceUSD).ThenBy(p => p.Name).ToList(),
                ("NarxiUZS", true) => products.OrderBy(p => p.PurchasePriceUZS).ThenBy(p => p.Name).ToList(),
                ("NarxiUZS", false) => products.OrderByDescending(p => p.PurchasePriceUZS).ThenBy(p => p.Name).ToList(),
                ("MahsulotNomi", false) => products.OrderByDescending(p => p.Name).ToList(),
                _ => products.OrderBy(p => p.Name).ToList()
            };
        }

        private static List<Product> GetActiveProducts(IEnumerable<Product> products)
        {
            return products.Where(p => p.QuantityUSD > 0.000001).ToList();
        }

        private void BindGrid(IEnumerable<Product> products)
        {
            int? selectedProductId = GetSelectedProductId();
            int firstVisibleRow = GetFirstVisibleRowIndex();

            List<ProductGridRow> rows = products.Select(p => new ProductGridRow
            {
                ProductId = p.Id,
                PreviewImage = ProductImageStore.TryLoadPreview(p.ImagePath, 38, 38),
                MahsulotNomi = p.Name,
                Soni = p.QuantityUSD,
                NarxiUSD = p.PurchasePriceUSD,
                NarxiUZS = Math.Round(p.PurchasePriceUZS, 0)
            }).ToList();

            _gridSource.DataSource = rows;
            RestoreGridState(selectedProductId, firstVisibleRow);

            _lblFooter.Text = $"Jami: {rows.Count} ta mahsulot ko'rsatilmoqda";
        }

        private int? GetSelectedProductId()
        {
            if (_grid.CurrentRow?.DataBoundItem is ProductGridRow current)
            {
                return current.ProductId;
            }

            if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].DataBoundItem is ProductGridRow selected)
            {
                return selected.ProductId;
            }

            return null;
        }

        private int GetFirstVisibleRowIndex()
        {
            try
            {
                return Math.Max(0, _grid.FirstDisplayedScrollingRowIndex);
            }
            catch
            {
                return 0;
            }
        }

        private void RestoreGridState(int? selectedProductId, int firstVisibleRow)
        {
            if (_grid.Rows.Count == 0)
            {
                return;
            }

            int rowToSelect = 0;
            if (selectedProductId.HasValue)
            {
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    if (_grid.Rows[i].DataBoundItem is ProductGridRow row && row.ProductId == selectedProductId.Value)
                    {
                        rowToSelect = i;
                        break;
                    }
                }
            }

            _grid.ClearSelection();
            if (rowToSelect >= 0 && rowToSelect < _grid.Rows.Count)
            {
                _grid.Rows[rowToSelect].Selected = true;
                _grid.CurrentCell = _grid.Rows[rowToSelect].Cells["MahsulotNomi"];
            }

            int targetFirstRow = Math.Max(0, Math.Min(firstVisibleRow, _grid.Rows.Count - 1));
            try
            {
                _grid.FirstDisplayedScrollingRowIndex = targetFirstRow;
            }
            catch
            {
                // Qatorlar kam bo'lsa yoki scroll chegarasi bo'lsa ignore qilamiz.
            }
        }

        private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.ColumnIndex >= _grid.Columns.Count)
            {
                return;
            }

            string columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName != "MahsulotNomi" && columnName != "Soni" && columnName != "NarxiUSD" && columnName != "NarxiUZS")
            {
                return;
            }

            if (string.Equals(_sortColumn, columnName, StringComparison.Ordinal))
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = columnName;
                _sortAscending = true;
            }

            ApplySearchAndBind();
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            string col = _grid.Columns[e.ColumnIndex].Name;
            if (col == "NarxiUSD" && e.Value is double usd)
            {
                e.Value = usd.ToString("N2", CultureInfo.InvariantCulture) + " $";
                e.FormattingApplied = true;
            }
            else if (col == "NarxiUZS" && e.Value is double uzs)
            {
                e.Value = uzs.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ") + " so'm";
                e.FormattingApplied = true;
            }
        }

        private void QuickFilter_DrawItem(object? sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || sender is not ComboBox combo)
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color bg = selected ? Color.FromArgb(226, 238, 255) : Color.FromArgb(247, 250, 255);
            Color fg = Color.FromArgb(45, 60, 87);
            using SolidBrush b = new SolidBrush(bg);
            e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(
                e.Graphics,
                combo.Items[e.Index]?.ToString() ?? string.Empty,
                combo.Font,
                e.Bounds,
                fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
        }

        private void Grid_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_grid.RowCount <= 0)
            {
                return;
            }

            int current = _grid.FirstDisplayedScrollingRowIndex;
            if (current < 0)
            {
                return;
            }

            int step = 3;
            int direction = e.Delta > 0 ? -1 : 1;
            int target = current + (direction * step);
            target = Math.Max(0, Math.Min(_grid.RowCount - 1, target));

            if (target == current)
            {
                return;
            }

            try
            {
                _grid.FirstDisplayedScrollingRowIndex = target;
            }
            catch
            {
                // Scroll chegarasida bo'lsa xatoni yutamiz.
            }
        }

        private void RefreshStats(List<Product> products)
        {
            _lblStatProducts.Text = products.Count.ToString("N0");
            _lblStatQty.Text = products.Sum(x => x.QuantityUSD).ToString("#,##0.##");
            double totalValueUzs = products.Sum(x => x.QuantityUSD * x.PurchasePriceUZS);
            _lblStatValue.Text = totalValueUzs.ToString("#,##0", CultureInfo.InvariantCulture).Replace(",", " ") + " so'm";
        }

        private double GetCurrentRate()
        {
            CurrencyRate? lastRate = _dbHelper.GetLastCurrencyRate();
            if (lastRate != null && lastRate.Rate > 1000)
            {
                return lastRate.Rate;
            }

            string? defaultRate = ConfigurationManager.AppSettings["DefaultDollarRate"];
            if (double.TryParse(defaultRate, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRate) && parsedRate > 1000)
            {
                return parsedRate;
            }

            return 12500;
        }

        private void StartRevealAnimation()
        {
            _revealIndex = 0;
            foreach (Panel p in _statCards)
            {
                p.Visible = false;
            }
            _revealTimer.Start();
        }

        private void RevealTimer_Tick(object? sender, EventArgs e)
        {
            if (_revealIndex >= _statCards.Count)
            {
                _revealTimer.Stop();
                return;
            }

            _statCards[_revealIndex].Visible = true;
            _revealIndex++;
        }

        private static void AttachCardHover(Panel panel)
        {
            int baseTop = panel.Top;
            panel.MouseEnter += (s, e) =>
            {
                baseTop = panel.Top;
                panel.Top = baseTop - 2;
            };
            panel.MouseLeave += (s, e) => panel.Top = baseTop;
        }

        private sealed class ProductGridRow
        {
            public int ProductId { get; set; }
            public Image? PreviewImage { get; set; }
            public string MahsulotNomi { get; set; } = string.Empty;
            public double Soni { get; set; }
            public double NarxiUSD { get; set; }
            public double NarxiUZS { get; set; }
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
    }
}



