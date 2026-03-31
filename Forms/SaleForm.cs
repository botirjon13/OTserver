using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class SaleForm : Form
    {
        private const int SB_HORZ = 0;
        private const int SB_VERT = 1;
        private const int SB_BOTH = 3;

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private readonly ProductService _productService = new ProductService();
        private readonly SaleService _saleService = new SaleService();
        private readonly ReceiptService _receiptService = new ReceiptService();
        private readonly DebtService _debtService = new DebtService();
        private readonly CustomerService _customerService = new CustomerService();
        private readonly PricingSettingsService _pricingSettingsService = new PricingSettingsService();
        private readonly AppUser _currentUser;

        private List<Product> _allProducts = new List<Product>();
        private readonly List<SaleItem> _basketItems = new List<SaleItem>();
        private Product? _selectedProduct;
        private double _usdRate;

        private readonly TextBox _txtSearch = new TextBox();
        private readonly ListBox _lstProducts = new ListBox();
        private readonly TextBox _txtQty = new TextBox();
        private readonly TextBox _txtPrice = new TextBox();
        private readonly Label _lblRate = new Label();
        private readonly Label _lblTotal = new Label();
        private readonly Label _lblSubtotal = new Label();
        private readonly Label _lblDiscountSummary = new Label();
        private readonly Label _lblEmpty = new Label();
        private readonly Label _lblEmptyIcon = new Label();
        private readonly Label _lblEmptyHint = new Label();
        private readonly Label _lblStockText = new Label();
        private readonly Label _lblStockValue = new Label();
        private readonly Label _lblPurchaseText = new Label();
        private readonly Label _lblPurchaseValue = new Label();
        private readonly Label _lblSuggestedText = new Label();
        private readonly Label _lblSuggestedValue = new Label();
        private readonly Label _lblAutoFillState = new Label();
        private readonly DataGridView _gridBasket = new DataGridView();
        private Button _btnRemoveSelected = new Button();
        private readonly Panel _rightBody = new Panel();
        private readonly Panel _discountCard = new Panel();
        private readonly ComboBox _cmbDiscountType = new ComboBox();
        private readonly TextBox _txtDiscountValue = new TextBox();
        private double _suggestedMarkupPercent = 20;
        private bool _autoFillSuggestedPrice = true;
        private bool _quickDiscountEnabled = true;
        private string _activeDiscountType = "None";
        private double _activeDiscountValue;
        private Action? _applyRightLayout;
        private readonly Dictionary<int, string> _productNameById = new Dictionary<int, string>();

        public SaleForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            AuthorizationService.Require(
                AuthorizationService.CanCreateSales(_currentUser),
                "Sotuv yaratish huquqi mavjud emas.");

            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            LoadInitialData();
            Shown += (_, __) =>
            {
                ReloadPricingSettings();
                UpdateBasket();
            };
        }

        private void InitializeComponent()
        {
            Text = "Yangi Sotuv Operatsiyasi";
            Size = new Size(1360, 850);
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(237, 242, 250);
            Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            DoubleBuffered = true;

            Panel root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 20, 28, 20) };

            Panel header = new Panel { Dock = DockStyle.Top, Height = 84 };
            Panel icon = new Panel
            {
                Left = 0,
                Top = 4,
                Width = 54,
                Height = 54
            };
            Image? saleHeaderIcon = BrandingAssets.TryLoadAssetImage("tile-sales.png");
            icon.Paint += (s, e) =>
            {
                if (saleHeaderIcon != null)
                {
                    return;
                }

                using var brush = new LinearGradientBrush(icon.ClientRectangle, Color.FromArgb(40, 132, 244), Color.FromArgb(33, 113, 223), 45f);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = RoundedRect(icon.ClientRectangle, 16);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, "\uE7BF", UiTheme.IconFont(24), icon.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            if (saleHeaderIcon != null)
            {
                icon.BackColor = Color.Transparent;
                icon.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = saleHeaderIcon
                });
            }

            Label lblSub = new Label
            {
                Text = "Yangi savdo operatsiyasini rasmiylashtirish",
                AutoSize = true,
                Left = 68,
                Top = 16,
                Font = new Font("Bahnschrift SemiBold", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 42, 65)
            };

            Panel rateBadge = new Panel
            {
                Width = 270,
                Height = 42,
                Top = 10,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            rateBadge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, rateBadge.Width - 3), Math.Max(1, rateBadge.Height - 3));
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
            rateBadge.Controls.Add(lblDollar);
            rateBadge.Controls.Add(_lblRate);

            Button btnReceiptHistory = NewHeaderIconButton("Chek tarixi", "sale-action-history.png", "\uE81C");
            btnReceiptHistory.Top = 10;
            btnReceiptHistory.Click += (_, __) =>
            {
                using var form = new ReceiptHistoryForm(_currentUser);
                form.ShowDialog(this);
            };

            Button btnReturn = NewHeaderIconButton("Qaytarib olish", "sale-action-return.png", "\uE7A7");
            btnReturn.Top = 10;
            btnReturn.Click += (_, __) =>
            {
                using var form = new ReturnForm(_currentUser);
                form.ShowDialog(this);
            };

            var tip = new ToolTip();
            tip.SetToolTip(btnReceiptHistory, "Chek tarixi");
            tip.SetToolTip(btnReturn, "Qaytarib olish");

            header.Controls.Add(icon);
            header.Controls.Add(lblSub);
            header.Controls.Add(rateBadge);
            header.Controls.Add(btnReceiptHistory);
            header.Controls.Add(btnReturn);
            header.Resize += (s, e) =>
            {
                rateBadge.Left = Math.Max(0, header.ClientSize.Width - rateBadge.Width);
                btnReturn.Left = Math.Max(0, rateBadge.Left - 10 - btnReturn.Width);
                btnReceiptHistory.Left = Math.Max(0, btnReturn.Left - 8 - btnReceiptHistory.Width);
            };
            rateBadge.Left = Math.Max(0, header.ClientSize.Width - rateBadge.Width);
            btnReturn.Left = Math.Max(0, rateBadge.Left - 10 - btnReturn.Width);
            btnReceiptHistory.Left = Math.Max(0, btnReturn.Left - 8 - btnReceiptHistory.Width);

            TableLayoutPanel main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 0, 0, 0)
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            Panel leftCard = BuildLeftCard();
            Panel rightCard = BuildRightCard();

            main.Controls.Add(leftCard, 0, 0);
            main.Controls.Add(rightCard, 1, 0);

            root.Controls.Add(main);
            root.Controls.Add(header);
            Controls.Add(root);
        }

        private Panel BuildLeftCard()
        {
            Panel card = NewCard();
            card.Margin = new Padding(0, 0, 12, 0);

            Panel titleBar = NewBar("Mahsulot qidirish", Color.FromArgb(44, 103, 246), Color.FromArgb(52, 127, 233), "\uECAA", "tile-products.png");
            titleBar.Dock = DockStyle.Top;

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 18, 22, 22), BackColor = Color.White };

            Panel searchWrap = NewInputWrap();
            searchWrap.SetBounds(0, 0, 100, 46);
            searchWrap.Padding = new Padding(46, 10, 12, 10);
            Label searchIcon = new Label
            {
                Text = "\uE721",
                Font = UiTheme.IconFont(16),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 30,
                Height = 30,
                Left = 10,
                Top = 8,
                ForeColor = Color.FromArgb(130, 147, 171),
                BackColor = Color.Transparent
            };
            _txtSearch.BorderStyle = BorderStyle.None;
            _txtSearch.Font = new Font("Bahnschrift", 14, FontStyle.Regular);
            _txtSearch.PlaceholderText = "Nomini yozing...";
            _txtSearch.BackColor = Color.White;
            _txtSearch.ForeColor = Color.FromArgb(42, 58, 84);
            _txtSearch.Dock = DockStyle.Fill;
            _txtSearch.TextChanged += (s, e) => FilterProducts();
            searchWrap.Controls.Add(searchIcon);
            searchWrap.Controls.Add(_txtSearch);

            Panel listWrap = new Panel();
            listWrap.Top = 64;
            listWrap.Height = 330;
            listWrap.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            listWrap.BackColor = Color.White;
            listWrap.Padding = new Padding(12, 10, 12, 10);
            listWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, listWrap.Width - 3), Math.Max(1, listWrap.Height - 3));
                using SolidBrush brush = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(66, 118, 198), 2.2f)
                {
                    LineJoin = LineJoin.Round
                };
                using GraphicsPath path = RoundedRect(bounds, 14);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            listWrap.Resize += (s, e) =>
            {
                listWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, listWrap.Width), Math.Max(1, listWrap.Height)), 14));
            };

            _lstProducts.Dock = DockStyle.Fill;
            _lstProducts.BorderStyle = BorderStyle.None;
            _lstProducts.BackColor = Color.White;
            _lstProducts.DrawMode = DrawMode.OwnerDrawFixed;
            _lstProducts.ItemHeight = 54;
            _lstProducts.IntegralHeight = false;
            _lstProducts.Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold);
            _lstProducts.DrawItem += ProductList_DrawItem;
            _lstProducts.SelectedIndexChanged += ProductList_SelectedIndexChanged;
            _lstProducts.MouseWheel += ListProducts_MouseWheel;
            _lstProducts.HandleCreated += (s, e) => HideListScrollbars();
            _lstProducts.Resize += (s, e) => HideListScrollbars();
            listWrap.Controls.Add(_lstProducts);

            Panel divider = new Panel
            {
                Height = 1,
                Left = 0,
                Width = 100,
                Top = 408,
                BackColor = Color.FromArgb(228, 234, 244),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lblQty = new Label
            {
                Text = "Soni:",
                Left = 0,
                Top = 432,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(57, 71, 92)
            };
            Label lblPrice = new Label
            {
                Text = "Sotish narxi (UZS):",
                Left = 420,
                Top = 432,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(57, 71, 92)
            };

            Panel qtyWrap = NewInputWrap();
            qtyWrap.SetBounds(0, 468, 390, 46);
            qtyWrap.Padding = new Padding(12, 10, 12, 10);
            _txtQty.BorderStyle = BorderStyle.None;
            _txtQty.BackColor = Color.White;
            _txtQty.Font = new Font("Bahnschrift", 14, FontStyle.Regular);
            _txtQty.Text = "1";
            _txtQty.Dock = DockStyle.Fill;
            qtyWrap.Controls.Add(_txtQty);

            Panel priceWrap = NewInputWrap();
            priceWrap.SetBounds(420, 468, 390, 46);
            priceWrap.Padding = new Padding(12, 10, 12, 10);
            _txtPrice.BorderStyle = BorderStyle.None;
            _txtPrice.BackColor = Color.White;
            _txtPrice.Font = new Font("Bahnschrift", 14, FontStyle.Regular);
            _txtPrice.Dock = DockStyle.Fill;
            priceWrap.Controls.Add(_txtPrice);

            Panel infoWrap = new Panel
            {
                Left = 0,
                Top = 530,
                Width = 810,
                Height = 110
            };
            infoWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush brush = new SolidBrush(Color.FromArgb(237, 243, 252));
                using GraphicsPath path = RoundedRect(infoWrap.ClientRectangle, 12);
                e.Graphics.FillPath(brush, path);
            };

            _lblStockText.Text = "Ombordagi qoldiq:";
            _lblStockText.AutoSize = false;
            _lblStockText.Left = 18;
            _lblStockText.Top = 14;
            _lblStockText.Width = 270;
            _lblStockText.Height = 28;
            _lblStockText.TextAlign = ContentAlignment.MiddleLeft;
            _lblStockText.Font = new Font("Bahnschrift", 13, FontStyle.Regular);
            _lblStockText.ForeColor = Color.FromArgb(93, 108, 128);
            _lblStockText.BackColor = Color.Transparent;

            _lblStockValue.AutoSize = false;
            _lblStockValue.Left = infoWrap.Width - 270 - 18;
            _lblStockValue.Top = 14;
            _lblStockValue.Width = 270;
            _lblStockValue.Height = 28;
            _lblStockValue.TextAlign = ContentAlignment.MiddleRight;
            _lblStockValue.Font = new Font("Bahnschrift SemiBold", 13, FontStyle.Bold);
            _lblStockValue.ForeColor = Color.FromArgb(42, 58, 84);
            _lblStockValue.BackColor = Color.Transparent;
            _lblStockValue.Text = "-";

            _lblPurchaseText.Text = "Olingan narx (UZS):";
            _lblPurchaseText.AutoSize = false;
            _lblPurchaseText.Left = 18;
            _lblPurchaseText.Top = 42;
            _lblPurchaseText.Width = 270;
            _lblPurchaseText.Height = 28;
            _lblPurchaseText.TextAlign = ContentAlignment.MiddleLeft;
            _lblPurchaseText.Font = new Font("Bahnschrift", 13, FontStyle.Regular);
            _lblPurchaseText.ForeColor = Color.FromArgb(93, 108, 128);
            _lblPurchaseText.BackColor = Color.Transparent;

            _lblPurchaseValue.AutoSize = false;
            _lblPurchaseValue.Left = infoWrap.Width - 270 - 18;
            _lblPurchaseValue.Top = 42;
            _lblPurchaseValue.Width = 270;
            _lblPurchaseValue.Height = 28;
            _lblPurchaseValue.TextAlign = ContentAlignment.MiddleRight;
            _lblPurchaseValue.Font = new Font("Bahnschrift SemiBold", 13, FontStyle.Bold);
            _lblPurchaseValue.ForeColor = Color.FromArgb(42, 58, 84);
            _lblPurchaseValue.BackColor = Color.Transparent;
            _lblPurchaseValue.Text = "-";

            _lblSuggestedText.Text = "Sotish tavsiyasi (UZS):";
            _lblSuggestedText.AutoSize = false;
            _lblSuggestedText.Left = 18;
            _lblSuggestedText.Top = 70;
            _lblSuggestedText.Width = 270;
            _lblSuggestedText.Height = 28;
            _lblSuggestedText.TextAlign = ContentAlignment.MiddleLeft;
            _lblSuggestedText.Font = new Font("Bahnschrift", 13, FontStyle.Regular);
            _lblSuggestedText.ForeColor = Color.FromArgb(93, 108, 128);
            _lblSuggestedText.BackColor = Color.Transparent;

            _lblSuggestedValue.AutoSize = false;
            _lblSuggestedValue.Left = infoWrap.Width - 270 - 18;
            _lblSuggestedValue.Top = 70;
            _lblSuggestedValue.Width = 270;
            _lblSuggestedValue.Height = 28;
            _lblSuggestedValue.TextAlign = ContentAlignment.MiddleRight;
            _lblSuggestedValue.Font = new Font("Bahnschrift SemiBold", 13, FontStyle.Bold);
            _lblSuggestedValue.ForeColor = Color.FromArgb(10, 156, 70);
            _lblSuggestedValue.BackColor = Color.Transparent;
            _lblSuggestedValue.Text = "-";

            _lblAutoFillState.AutoSize = false;
            _lblAutoFillState.Width = 150;
            _lblAutoFillState.Height = 22;
            _lblAutoFillState.TextAlign = ContentAlignment.MiddleCenter;
            _lblAutoFillState.Font = new Font("Bahnschrift SemiBold", 9, FontStyle.Bold);
            _lblAutoFillState.ForeColor = Color.FromArgb(184, 96, 0);
            _lblAutoFillState.BackColor = Color.FromArgb(255, 243, 220);
            _lblAutoFillState.Text = "Avto-yozish o'chiq";
            _lblAutoFillState.Visible = false;

            infoWrap.Controls.Add(_lblStockText);
            infoWrap.Controls.Add(_lblStockValue);
            infoWrap.Controls.Add(_lblPurchaseText);
            infoWrap.Controls.Add(_lblPurchaseValue);
            infoWrap.Controls.Add(_lblSuggestedText);
            infoWrap.Controls.Add(_lblSuggestedValue);
            infoWrap.Controls.Add(_lblAutoFillState);

            Button btnAdd = NewGradientButton("SAVATGA QO'SHISH", Color.FromArgb(127, 166, 238), Color.FromArgb(117, 152, 220));
            btnAdd.SetBounds(0, 632, 810, 52);
            btnAdd.Click += AddToBasket_Click;

            body.Controls.Add(searchWrap);
            body.Controls.Add(listWrap);
            body.Controls.Add(divider);
            body.Controls.Add(lblQty);
            body.Controls.Add(lblPrice);
            body.Controls.Add(qtyWrap);
            body.Controls.Add(priceWrap);
            body.Controls.Add(infoWrap);
            body.Controls.Add(btnAdd);

            Action applyLeftLayout = () =>
            {
                int bodyW = body.ClientSize.Width;
                int bodyH = body.ClientSize.Height;

                int searchTop = 10;
                int searchH = 46;
                int listTop = 92;
                int listMinH = 150;
                int dividerGapTop = 14;
                int dividerH = 1;
                int labelGapTop = 14;
                int labelH = 24;
                int inputGapTop = 8;
                int inputH = 48;
                int infoGapTop = 14;
                int infoH = 110;
                int btnGapTop = 16;
                int btnH = 52;
                int bottomPad = 6;

                int fixedAfterList =
                    dividerGapTop + dividerH +
                    labelGapTop + labelH +
                    inputGapTop + inputH +
                    infoGapTop + infoH +
                    btnGapTop + btnH + bottomPad;

                int listH = Math.Max(listMinH, bodyH - listTop - fixedAfterList);

                int searchW = Math.Min(640, bodyW);
                int searchLeft = (bodyW - searchW) / 2;
                searchWrap.SetBounds(searchLeft, searchTop, searchW, searchH);
                _txtSearch.Width = Math.Max(120, searchWrap.Width - 56);

                int listW = Math.Min(640, bodyW);
                int listLeft = (bodyW - listW) / 2;
                listWrap.SetBounds(listLeft, listTop, listW, listH);

                int dividerTop = listTop + listH + dividerGapTop;
                divider.SetBounds(listLeft, dividerTop, listW, dividerH);

                int labelTop = dividerTop + dividerH + labelGapTop;
                lblQty.Top = labelTop;
                lblPrice.Top = labelTop;

                int inputTop = labelTop + labelH + inputGapTop;
                int half = Math.Min(320, (body.ClientSize.Width - 30) / 2);
                int pairWidth = (half * 2) + 30;
                int pairLeft = (body.ClientSize.Width - pairWidth) / 2;
                qtyWrap.Left = pairLeft;
                qtyWrap.Width = half;
                qtyWrap.Top = inputTop;
                qtyWrap.Height = inputH;
                priceWrap.Left = pairLeft + half + 30;
                priceWrap.Width = half;
                priceWrap.Top = inputTop;
                priceWrap.Height = inputH;
                lblQty.Left = qtyWrap.Left;
                lblPrice.Left = priceWrap.Left;

                int infoWidth = Math.Min(810, body.ClientSize.Width);
                int infoTop = inputTop + inputH + infoGapTop;
                infoWrap.Left = (body.ClientSize.Width - infoWidth) / 2;
                infoWrap.Top = infoTop;
                infoWrap.Width = infoWidth;
                infoWrap.Height = infoH;
                _lblStockValue.Left = infoWrap.Width - _lblStockValue.Width - 18;
                _lblPurchaseValue.Left = infoWrap.Width - _lblPurchaseValue.Width - 18;
                _lblSuggestedValue.Left = infoWrap.Width - _lblSuggestedValue.Width - 18;
                _lblAutoFillState.Left = _lblSuggestedValue.Left - _lblAutoFillState.Width - 8;
                _lblAutoFillState.Top = _lblSuggestedValue.Top + 3;

                int btnTop = infoTop + infoH + btnGapTop;
                btnAdd.Left = infoWrap.Left;
                btnAdd.Width = infoWrap.Width;
                btnAdd.Top = btnTop;
                btnAdd.Height = btnH;
            };
            body.Resize += (s, e) => applyLeftLayout();
            body.SizeChanged += (s, e) => applyLeftLayout();
            applyLeftLayout();

            card.Controls.Add(body);
            card.Controls.Add(titleBar);
            return card;
        }

        private Panel BuildRightCard()
        {
            Panel card = NewCard();
            card.Margin = new Padding(12, 0, 0, 0);

            Panel titleBar = NewBar("Savatdagi mahsulotlar", Color.FromArgb(137, 34, 226), Color.FromArgb(161, 67, 227), "\uE7BF", "tile-sales.png");
            titleBar.Dock = DockStyle.Top;

            _rightBody.Dock = DockStyle.Fill;
            _rightBody.BackColor = Color.White;
            _rightBody.Padding = new Padding(18, 16, 18, 18);

            _gridBasket.Dock = DockStyle.Top;
            _gridBasket.Height = 390;
            _gridBasket.ReadOnly = true;
            _gridBasket.AllowUserToAddRows = false;
            _gridBasket.AllowUserToDeleteRows = false;
            _gridBasket.RowHeadersVisible = false;
            _gridBasket.BorderStyle = BorderStyle.None;
            _gridBasket.BackgroundColor = Color.White;
            _gridBasket.EnableHeadersVisualStyles = false;
            _gridBasket.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridBasket.ColumnHeadersHeight = 42;
            _gridBasket.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(42, 58, 84);
            _gridBasket.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _gridBasket.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(42, 58, 84);
            _gridBasket.ColumnHeadersDefaultCellStyle.Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold);
            _gridBasket.DefaultCellStyle.Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            _gridBasket.DefaultCellStyle.ForeColor = Color.FromArgb(42, 58, 84);
            _gridBasket.DefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 243, 252);
            _gridBasket.DefaultCellStyle.SelectionForeColor = Color.FromArgb(26, 40, 62);
            _gridBasket.RowTemplate.Height = 38;
            _gridBasket.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridBasket.MultiSelect = false;
            _gridBasket.Visible = false;
            _gridBasket.SelectionChanged += (s, e) => UpdateRemoveButtonState();

            _lblEmptyIcon.Text = "\uE7BF";
            _lblEmptyIcon.Font = UiTheme.IconFont(48);
            _lblEmptyIcon.ForeColor = Color.FromArgb(184, 198, 220);
            _lblEmptyIcon.AutoSize = false;
            _lblEmptyIcon.TextAlign = ContentAlignment.MiddleCenter;
            _lblEmptyIcon.Dock = DockStyle.Top;
            _lblEmptyIcon.Height = 120;
            _lblEmptyIcon.Padding = new Padding(0);
            _lblEmptyIcon.BackColor = Color.Transparent;

            _lblEmpty.Text = "Savat bo'sh";
            _lblEmpty.Font = new Font("Bahnschrift", 13, FontStyle.Regular);
            _lblEmpty.ForeColor = Color.FromArgb(148, 162, 186);
            _lblEmpty.AutoSize = false;
            _lblEmpty.Height = 40;
            _lblEmpty.Dock = DockStyle.Top;
            _lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _lblEmpty.BackColor = Color.Transparent;

            _lblEmptyHint.Text = "Chap tomondan mahsulot tanlab savatga qo'shing";
            _lblEmptyHint.Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular);
            _lblEmptyHint.ForeColor = Color.FromArgb(163, 176, 196);
            _lblEmptyHint.AutoSize = false;
            _lblEmptyHint.Height = 24;
            _lblEmptyHint.TextAlign = ContentAlignment.MiddleCenter;
            _lblEmptyHint.BackColor = Color.Transparent;

            Panel divider = new Panel
            {
                Height = 1,
                BackColor = Color.FromArgb(228, 234, 244)
            };

            _discountCard.Height = 198;
            _discountCard.Visible = _quickDiscountEnabled;
            _discountCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush brush = new SolidBrush(Color.FromArgb(242, 246, 252));
                using GraphicsPath path = RoundedRect(_discountCard.ClientRectangle, 12);
                e.Graphics.FillPath(brush, path);
            };

            Label lblDiscountTitle = new Label
            {
                Text = "Tezkor chegirma:",
                AutoSize = true,
                Left = 14,
                Top = 10,
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 68, 94),
                BackColor = Color.Transparent
            };

            Panel discountTypeWrap = NewInputWrap();
            discountTypeWrap.BackColor = Color.White;
            Panel discountValueWrap = NewInputWrap();
            discountValueWrap.BackColor = Color.White;
            Panel discountArrowMask = new Panel
            {
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };

            _cmbDiscountType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbDiscountType.Items.AddRange(new object[] { "Yo'q", "Foiz (%)", "Summa (UZS)" });
            _cmbDiscountType.SelectedIndex = 0;
            _cmbDiscountType.FlatStyle = FlatStyle.Flat;
            _cmbDiscountType.Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular);
            _cmbDiscountType.BackColor = Color.White;
            _cmbDiscountType.ForeColor = Color.FromArgb(42, 58, 84);
            _cmbDiscountType.Left = 10;
            _cmbDiscountType.Top = 2;
            _cmbDiscountType.Height = 26;
            discountArrowMask.Click += (s, e) =>
            {
                _cmbDiscountType.Focus();
                _cmbDiscountType.DroppedDown = true;
            };

            _txtDiscountValue.BorderStyle = BorderStyle.None;
            _txtDiscountValue.PlaceholderText = "Qiymat kiriting";
            _txtDiscountValue.Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular);
            _txtDiscountValue.BackColor = Color.White;
            _txtDiscountValue.ForeColor = Color.FromArgb(42, 58, 84);
            _txtDiscountValue.Left = 10;
            _txtDiscountValue.Top = 6;

            discountTypeWrap.Controls.Add(_cmbDiscountType);
            discountTypeWrap.Controls.Add(discountArrowMask);
            discountValueWrap.Controls.Add(_txtDiscountValue);

            Button btnApplyDiscount = NewGradientButton("Qo'llash", Color.FromArgb(75, 149, 248), Color.FromArgb(57, 132, 235));
            btnApplyDiscount.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
            btnApplyDiscount.Click += (s, e) => ApplyQuickDiscount();

            Button btnResetDiscount = NewGradientButton("Bekor", Color.FromArgb(120, 138, 164), Color.FromArgb(95, 114, 142));
            btnResetDiscount.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
            btnResetDiscount.Click += (s, e) =>
            {
                _activeDiscountType = "None";
                _activeDiscountValue = 0;
                _cmbDiscountType.SelectedIndex = 0;
                _txtDiscountValue.Text = string.Empty;
                UpdateBasket();
            };

            _discountCard.Controls.Add(lblDiscountTitle);
            _discountCard.Controls.Add(discountTypeWrap);
            _discountCard.Controls.Add(discountValueWrap);
            _discountCard.Controls.Add(btnApplyDiscount);
            _discountCard.Controls.Add(btnResetDiscount);
            _discountCard.Resize += (s, e) =>
            {
                int w = _discountCard.ClientSize.Width;
                int cardPadding = 14;
                int controlW = Math.Max(120, w - (cardPadding * 2));
                int wrapH = 36;
                int btnH = 28;
                int y = 36;

                discountTypeWrap.SetBounds(cardPadding, y, controlW, wrapH);
                y += wrapH + 8;
                discountValueWrap.SetBounds(cardPadding, y, controlW, wrapH);
                y += wrapH + 10;

                _cmbDiscountType.SetBounds(12, 4, Math.Max(90, discountTypeWrap.Width - 24), 24);
                discountArrowMask.SetBounds(Math.Max(0, discountTypeWrap.Width - 30), 5, 24, Math.Max(1, discountTypeWrap.Height - 10));
                discountArrowMask.BringToFront();
                _txtDiscountValue.SetBounds(12, 7, Math.Max(90, discountValueWrap.Width - 24), 20);

                btnApplyDiscount.SetBounds(cardPadding, y, controlW, btnH);
                y += btnH + 8;
                btnResetDiscount.SetBounds(cardPadding, y, controlW, btnH);
            };

            Panel totalCard = new Panel
            {
                Height = 96
            };
            totalCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush brush = new SolidBrush(Color.FromArgb(238, 247, 243));
                using GraphicsPath path = RoundedRect(totalCard.ClientRectangle, 12);
                e.Graphics.FillPath(brush, path);
            };

            Label lblSubtotalTitle = new Label
            {
                Text = "Jami:",
                AutoSize = true,
                Left = 16,
                Top = 10,
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 77, 97),
                BackColor = Color.Transparent
            };

            _lblSubtotal.Text = "0 UZS";
            _lblSubtotal.AutoSize = false;
            _lblSubtotal.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
            _lblSubtotal.ForeColor = Color.FromArgb(62, 77, 97);
            _lblSubtotal.Top = 10;
            _lblSubtotal.Height = 24;
            _lblSubtotal.TextAlign = ContentAlignment.MiddleRight;
            _lblSubtotal.BackColor = Color.Transparent;

            _lblDiscountSummary.Text = "Chegirma: 0 UZS";
            _lblDiscountSummary.AutoSize = false;
            _lblDiscountSummary.Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold);
            _lblDiscountSummary.ForeColor = Color.FromArgb(41, 96, 189);
            _lblDiscountSummary.Top = 34;
            _lblDiscountSummary.Height = 22;
            _lblDiscountSummary.TextAlign = ContentAlignment.MiddleRight;
            _lblDiscountSummary.BackColor = Color.Transparent;

            Label lblTotalTitle = new Label
            {
                Text = "To'lov:",
                AutoSize = true,
                Left = 16,
                Top = 60,
                Font = new Font("Bahnschrift SemiBold", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 77, 97),
                BackColor = Color.Transparent
            };
            _lblTotal.Text = "0 UZS";
            _lblTotal.AutoSize = false;
            _lblTotal.Font = new Font("Bahnschrift SemiBold", 16, FontStyle.Bold);
            _lblTotal.ForeColor = Color.FromArgb(10, 156, 70);
            _lblTotal.Top = 60;
            _lblTotal.Height = 24;
            _lblTotal.TextAlign = ContentAlignment.MiddleRight;
            _lblTotal.BackColor = Color.Transparent;
            totalCard.Controls.Add(lblSubtotalTitle);
            totalCard.Controls.Add(_lblSubtotal);
            totalCard.Controls.Add(_lblDiscountSummary);
            totalCard.Controls.Add(lblTotalTitle);
            totalCard.Controls.Add(_lblTotal);
            totalCard.Resize += (s, e) =>
            {
                int valueLeft = 126;
                int valueWidth = Math.Max(120, totalCard.Width - valueLeft - 16);
                _lblSubtotal.Left = valueLeft;
                _lblSubtotal.Width = valueWidth;
                _lblDiscountSummary.Left = valueLeft;
                _lblDiscountSummary.Width = valueWidth;
                _lblTotal.Left = valueLeft;
                _lblTotal.Width = valueWidth;
            };

            Button btnComplete = NewGradientButton("SOTUVNI YAKUNLASH", Color.FromArgb(130, 210, 165), Color.FromArgb(107, 199, 147));
            btnComplete.Height = 50;
            btnComplete.Click += CompleteSale_Click;

            _btnRemoveSelected = NewGradientButton("Tanlanganni o'chirish", Color.FromArgb(163, 176, 196), Color.FromArgb(141, 156, 178));
            _btnRemoveSelected.Height = 42;
            _btnRemoveSelected.Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold);
            _btnRemoveSelected.Enabled = false;
            _btnRemoveSelected.Click += RemoveSelectedBasketItem_Click;

            _rightBody.Controls.Add(_gridBasket);
            _rightBody.Controls.Add(_lblEmpty);
            _rightBody.Controls.Add(_lblEmptyIcon);
            _rightBody.Controls.Add(_lblEmptyHint);
            _rightBody.Controls.Add(divider);
            _rightBody.Controls.Add(_discountCard);
            _rightBody.Controls.Add(_btnRemoveSelected);
            _rightBody.Controls.Add(totalCard);
            _rightBody.Controls.Add(btnComplete);

            Action applyRightLayout = () =>
            {
                int w = _rightBody.ClientSize.Width;
                int h = _rightBody.ClientSize.Height;

                int btnH = 50;
                int totalH = 96;
                int removeH = 42;
                int discountH = _discountCard.Visible ? _discountCard.Height : 0;
                int gap = 12;
                int bottomPad = 6;
                int dividerTop;

                if (_discountCard.Visible)
                {
                    int pairGap = 12;
                    int colW = Math.Max(180, (w - pairGap) / 2);
                    int leftX = 0;
                    int rightX = w - colW;

                    int rightStackH = totalH + gap + btnH;
                    int blockH = Math.Max(discountH, rightStackH);
                    int blockTop = h - bottomPad - blockH;

                    int removeTop = blockTop - gap - removeH;
                    int removeW = Math.Min(240, w);
                    int removeLeft = w - removeW;
                    _btnRemoveSelected.SetBounds(removeLeft, removeTop, removeW, removeH);

                    _discountCard.SetBounds(leftX, blockTop, colW, discountH);
                    totalCard.SetBounds(rightX, blockTop, colW, totalH);
                    btnComplete.SetBounds(rightX, totalCard.Bottom + gap, colW, btnH);

                    dividerTop = removeTop - 14;
                }
                else
                {
                    int blockTop = h - bottomPad - (totalH + gap + btnH);
                    int btnW = Math.Min(340, w);
                    int btnLeft = (w - btnW) / 2;
                    int btnTop = blockTop + totalH + gap;
                    btnComplete.SetBounds(btnLeft, btnTop, btnW, btnH);

                    int totalTop = blockTop;
                    totalCard.SetBounds(btnLeft, totalTop, btnW, totalH);

                    int removeTop = totalTop - gap - removeH;
                    _btnRemoveSelected.SetBounds(btnLeft, removeTop, btnW, removeH);
                    dividerTop = removeTop - 14;
                }

                divider.SetBounds(0, dividerTop, w, 1);

                int contentBottom = dividerTop - 10;
                int contentHeight = Math.Max(140, contentBottom);
                _gridBasket.SetBounds(0, 0, w, contentHeight);

                int emptyIconTop = Math.Max(12, (contentHeight - 194) / 2);
                _lblEmptyIcon.SetBounds(0, emptyIconTop, w, 120);
                _lblEmpty.SetBounds(0, _lblEmptyIcon.Bottom + 4, w, 40);
                _lblEmptyHint.SetBounds(0, _lblEmpty.Bottom + 2, w, 24);
            };
            _applyRightLayout = applyRightLayout;
            _rightBody.Resize += (s, e) => applyRightLayout();
            _rightBody.SizeChanged += (s, e) => applyRightLayout();
            applyRightLayout();

            card.Controls.Add(_rightBody);
            card.Controls.Add(titleBar);
            return card;
        }

        private void LoadInitialData()
        {
            _allProducts = _productService.GetAll()
                .Where(p => p.QuantityUSD > 0.000001)
                .ToList();
            _productNameById.Clear();
            foreach (Product product in _allProducts)
            {
                _productNameById[product.Id] = product.Name;
            }
            _lstProducts.DataSource = _allProducts;
            _lstProducts.DisplayMember = "Name";

            try
            {
                _usdRate = _saleService.GetCurrentRate();
            }
            catch
            {
                _usdRate = 12500;
            }
            ReloadPricingSettings();

            _lblRate.Text = $"1 USD = {_usdRate.ToString("N0", CultureInfo.InvariantCulture)} UZS";
            UpdateBasket();
        }

        private void ReloadPricingSettings()
        {
            try
            {
                var pricing = _pricingSettingsService.Get(_currentUser);
                _suggestedMarkupPercent = pricing.SuggestedMarkupPercent;
                _autoFillSuggestedPrice = pricing.AutoFillSuggestedPrice;
                ApplyQuickDiscountMode(pricing.QuickDiscountEnabled);
                UpdateAutoFillBadge();
            }
            catch
            {
                _suggestedMarkupPercent = 20;
                _autoFillSuggestedPrice = true;
                ApplyQuickDiscountMode(true);
                UpdateAutoFillBadge();
            }
        }

        private void ApplyQuickDiscountMode(bool enabled)
        {
            _quickDiscountEnabled = enabled;
            _discountCard.Visible = enabled;

            if (!enabled)
            {
                _activeDiscountType = "None";
                _activeDiscountValue = 0;
                _cmbDiscountType.SelectedIndex = 0;
                _txtDiscountValue.Clear();
            }

            _applyRightLayout?.Invoke();
            _rightBody.PerformLayout();
            _rightBody.Invalidate();
        }

        private void UpdateAutoFillBadge()
        {
            _lblAutoFillState.Visible = !_autoFillSuggestedPrice;
            _lblAutoFillState.Text = _autoFillSuggestedPrice ? "Avto-yozish yoqilgan" : "Avto-yozish o'chiq";
            _lblAutoFillState.ForeColor = _autoFillSuggestedPrice ? Color.FromArgb(16, 120, 56) : Color.FromArgb(184, 96, 0);
            _lblAutoFillState.BackColor = _autoFillSuggestedPrice ? Color.FromArgb(225, 244, 233) : Color.FromArgb(255, 243, 220);
        }

        private void FilterProducts()
        {
            string search = _txtSearch.Text.Trim().ToLowerInvariant();
            List<Product> result = _allProducts.Where(p => p.Name.ToLowerInvariant().Contains(search)).ToList();
            _lstProducts.DataSource = null;
            _lstProducts.DataSource = result;
            _lstProducts.DisplayMember = "Name";
            HideListScrollbars();
        }

        private void ProductList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_lstProducts.SelectedItem is Product p)
            {
                _selectedProduct = p;
                if (!_autoFillSuggestedPrice)
                {
                    _txtPrice.Clear();
                }
                UpdateSelectedInfo();
            }
        }

        private void ProductList_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstProducts.Items.Count)
            {
                return;
            }

            using (SolidBrush rowBg = new SolidBrush(_lstProducts.BackColor))
            {
                e.Graphics.FillRectangle(rowBg, e.Bounds);
            }

            Product product = (Product)_lstProducts.Items[e.Index];
            Rectangle rect = e.Bounds;
            rect.Inflate(-4, -4);
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush bg = new SolidBrush(isSelected ? Color.FromArgb(53, 123, 234) : Color.FromArgb(252, 254, 255)))
            using (GraphicsPath path = RoundedRect(rect, 10))
            {
                e.Graphics.FillPath(bg, path);
                using Pen border = new Pen(isSelected ? Color.FromArgb(50, 108, 215) : Color.FromArgb(92, 126, 182), 1.8f);
                e.Graphics.DrawPath(border, path);
            }
            using (Pen rowLine = new Pen(Color.FromArgb(170, 188, 214), 1f))
            {
                e.Graphics.DrawLine(rowLine, rect.Left + 8, rect.Bottom - 1, rect.Right - 8, rect.Bottom - 1);
            }

            using (Brush nameBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(43, 56, 80)))
            {
                e.Graphics.DrawString(product.Name, new Font("Bahnschrift SemiBold", 12, FontStyle.Bold), nameBrush, rect.X + 14, rect.Y + 14);
            }

            string qtyText = $"{FormatQuantity(product.QuantityUSD)} dona";
            Size qtySize = TextRenderer.MeasureText(qtyText, new Font("Bahnschrift SemiBold", 10, FontStyle.Bold));
            Rectangle badge = new Rectangle(rect.Right - qtySize.Width - 24, rect.Y + (rect.Height - 28) / 2, qtySize.Width + 18, 28);
            Color badgeColor = isSelected
                ? Color.FromArgb(236, 244, 255)
                : (product.QuantityUSD <= 10 ? Color.FromArgb(231, 37, 72) : Color.FromArgb(9, 14, 34));
            Color badgeTextColor = isSelected
                ? Color.FromArgb(43, 98, 220)
                : Color.White;

            using (SolidBrush b = new SolidBrush(badgeColor))
            using (GraphicsPath bp = RoundedRect(badge, 12))
            {
                e.Graphics.FillPath(b, bp);
            }
            TextRenderer.DrawText(
                e.Graphics,
                qtyText,
                new Font("Bahnschrift SemiBold", 10, FontStyle.Bold),
                badge,
                badgeTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            e.DrawFocusRectangle();
        }

        private void AddToBasket_Click(object? sender, EventArgs e)
        {
            if (_selectedProduct == null)
            {
                MessageBox.Show("Mahsulotni tanlang.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!double.TryParse(_txtQty.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double qty) || qty <= 0)
            {
                MessageBox.Show("Miqdor musbat son bo'lishi kerak.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!double.TryParse(_txtPrice.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double price) || price <= 0)
            {
                MessageBox.Show("Narx musbat son bo'lishi kerak.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            double minSellPriceUzs = _selectedProduct.PurchasePriceUZS;
            if (price < minSellPriceUzs)
            {
                MessageBox.Show($"Sotish narxi olingan narxdan past bo'lmasligi kerak.\nMinimal narx: {minSellPriceUzs:N0} UZS", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            double reservedQty = _basketItems.Where(x => x.ProductId == _selectedProduct.Id).Sum(x => x.Quantity);
            if (reservedQty + qty > _selectedProduct.QuantityUSD)
            {
                MessageBox.Show("Omborda buncha mahsulot yo'q.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _basketItems.Add(new SaleItem
            {
                ProductId = _selectedProduct.Id,
                Quantity = qty,
                SellPriceUZS = price
            });

            _txtSearch.Clear();
            _txtQty.Text = "1";
            _txtPrice.Clear();
            UpdateSelectedInfo();
            UpdateBasket();
        }

        private void CompleteSale_Click(object? sender, EventArgs e)
        {
            if (_basketItems.Count == 0)
            {
                MessageBox.Show("Savat bo'sh.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using PaymentTypeForm paymentForm = new PaymentTypeForm();
                if (paymentForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                bool isDebt = string.Equals(paymentForm.SelectedPaymentType, "Nasiya (Qarz)", StringComparison.OrdinalIgnoreCase);
                using SaleCustomerForm customerForm = new SaleCustomerForm(_customerService.GetAll(), customerRequired: isDebt, debtMode: isDebt);
                if (customerForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var discountedItems = BuildDiscountedSaleItems(out double subtotal, out double appliedDiscount, out double finalTotal);
                SaleReceipt? receipt = null;
                int saleId = _saleService.CreateSale(
                    new Sale
                    {
                        Date = DateTime.Now,
                        Items = discountedItems,
                        SubtotalUZS = subtotal,
                        DiscountType = _activeDiscountType,
                        DiscountValue = _activeDiscountValue,
                        DiscountUZS = appliedDiscount,
                        TotalUZS = finalTotal
                    },
                    _currentUser,
                    (connection, transaction, createdSaleId) =>
                    {
                        int? customerId = customerForm.SelectedCustomerId;
                        if (customerId == null && !string.IsNullOrWhiteSpace(customerForm.NewCustomerName))
                        {
                            customerId = _customerService.FindOrCreate(
                                connection,
                                transaction,
                                customerForm.NewCustomerName,
                                customerForm.NewCustomerPhone,
                                customerForm.NewCustomerNote);
                        }

                        if (customerId.HasValue)
                        {
                            _customerService.AttachCustomerToSale(connection, transaction, createdSaleId, customerId.Value);
                        }

                        if (isDebt)
                        {
                            _debtService.CreateDebtForSale(
                                connection,
                                transaction,
                                createdSaleId,
                                customerId ?? 0,
                                customerForm.InitialPaymentUZS,
                                customerForm.DueDate,
                                _currentUser);
                        }

                        receipt = _receiptService.CreateAndSave(connection, transaction, createdSaleId, paymentForm.SelectedPaymentType, _currentUser);
                    });

                if (receipt == null)
                {
                    throw new Exception("Chek yaratilmadi. Amal bekor qilindi.");
                }

                using (SaleSuccessForm successForm = new SaleSuccessForm())
                {
                    successForm.ShowDialog(this);
                }

                using ReceiptForm preview = new ReceiptForm(receipt, _currentUser);
                preview.ShowDialog(this);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xatolik yuz berdi: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateBasket()
        {
            var discountedItems = BuildDiscountedSaleItems(out double subtotal, out double appliedDiscount, out double finalTotal);
            var rows = discountedItems.Select((x, index) => new BasketGridRow
            {
                BasketIndex = index,
                Nomi = _productNameById.TryGetValue(x.ProductId, out string? name) ? name : $"Noma'lum mahsulot #{x.ProductId}",
                Soni = x.Quantity,
                Narxi = x.SellPriceUZS,
                Chegirma = x.DiscountUZS,
                Jami = x.Quantity * x.SellPriceUZS
            }).ToList();

            _gridBasket.DataSource = null;
            _gridBasket.DataSource = rows;
            if (_gridBasket.Columns.Count > 0)
            {
                if (_gridBasket.Columns["BasketIndex"] != null)
                {
                    _gridBasket.Columns["BasketIndex"].Visible = false;
                }

                if (_gridBasket.Columns["Nomi"] != null)
                {
                    _gridBasket.Columns["Nomi"].HeaderText = "Nomi";
                }

                if (_gridBasket.Columns["Soni"] != null)
                {
                    _gridBasket.Columns["Soni"].HeaderText = "Soni";
                    _gridBasket.Columns["Soni"].DefaultCellStyle.Format = "N2";
                }

                if (_gridBasket.Columns["Narxi"] != null)
                {
                    _gridBasket.Columns["Narxi"].HeaderText = "Narxi";
                    _gridBasket.Columns["Narxi"].DefaultCellStyle.Format = "N0";
                }

                if (_gridBasket.Columns["Chegirma"] != null)
                {
                    _gridBasket.Columns["Chegirma"].HeaderText = "Chegirma";
                    _gridBasket.Columns["Chegirma"].DefaultCellStyle.Format = "N0";
                }

                if (_gridBasket.Columns["Jami"] != null)
                {
                    _gridBasket.Columns["Jami"].HeaderText = "Jami";
                    _gridBasket.Columns["Jami"].DefaultCellStyle.Format = "N0";
                }

                foreach (DataGridViewColumn column in _gridBasket.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                }
            }
            _gridBasket.Visible = rows.Count > 0;
            _lblEmpty.Visible = rows.Count == 0;
            _lblEmptyIcon.Visible = rows.Count == 0;
            _lblEmptyHint.Visible = rows.Count == 0;
            UpdateRemoveButtonState();

            _lblSubtotal.Text = $"{subtotal:N0} UZS";
            _lblDiscountSummary.Text = $"Chegirma: {appliedDiscount:N0} UZS";
            _lblTotal.Text = $"{finalTotal:N0} UZS";
        }

        private void RemoveSelectedBasketItem_Click(object? sender, EventArgs e)
        {
            if (_basketItems.Count == 0)
            {
                MessageBox.Show("Savat bo'sh.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_gridBasket.SelectedRows.Count == 0)
            {
                MessageBox.Show("O'chirish uchun savatdan bitta tovarni tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DataGridViewRow selectedRow = _gridBasket.SelectedRows[0];
            BasketGridRow? row = selectedRow.DataBoundItem as BasketGridRow;
            int basketIndex = row?.BasketIndex ?? selectedRow.Index;
            if (basketIndex < 0 || basketIndex >= _basketItems.Count)
            {
                MessageBox.Show("Tanlangan qatorni aniqlab bo'lmadi. Qaytadan urinib ko'ring.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaleItem selectedItem = _basketItems[basketIndex];
            string productName = _productNameById.TryGetValue(selectedItem.ProductId, out string? name)
                ? name
                : $"Noma'lum mahsulot #{selectedItem.ProductId}";

            DialogResult confirm = MessageBox.Show(
                $"Siz xaqiqatda korzinkadan \"{productName}\" tovarini olib tashlashni xoxlaysizmi?",
                "Tasdiqlash",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            _basketItems.RemoveAt(basketIndex);
            UpdateBasket();
        }

        private void UpdateRemoveButtonState()
        {
            _btnRemoveSelected.Enabled = _basketItems.Count > 0
                && _gridBasket.Visible
                && _gridBasket.SelectedRows.Count > 0;
        }

        private void ApplyQuickDiscount()
        {
            if (!_quickDiscountEnabled)
            {
                _activeDiscountType = "None";
                _activeDiscountValue = 0;
                UpdateBasket();
                return;
            }

            if (_basketItems.Count == 0)
            {
                MessageBox.Show("Avval savatga mahsulot qo'shing.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string selected = _cmbDiscountType.SelectedItem?.ToString() ?? "Yo'q";
            if (selected == "Yo'q")
            {
                _activeDiscountType = "None";
                _activeDiscountValue = 0;
                UpdateBasket();
                return;
            }

            if (!double.TryParse(_txtDiscountValue.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) || value <= 0)
            {
                MessageBox.Show("Chegirma qiymati musbat son bo'lishi kerak.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (selected == "Foiz (%)" && value > 100)
            {
                MessageBox.Show("Foizli chegirma 100% dan katta bo'lmasligi kerak.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _activeDiscountType = selected == "Foiz (%)" ? "Percent" : "Amount";
            _activeDiscountValue = value;

            var discountedItems = BuildDiscountedSaleItems(out _, out double appliedDiscount, out _);
            double requestedDiscount = _activeDiscountType == "Percent"
                ? _basketItems.Sum(x => x.Quantity * x.SellPriceUZS) * (_activeDiscountValue / 100.0)
                : _activeDiscountValue;
            if (appliedDiscount + 0.5 < requestedDiscount)
            {
                MessageBox.Show("Chegirma avtomatik cheklab qo'llandi. Sabab: ayrim tovarlar tannarxdan pastga tushmasligi kerak.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            UpdateBasket();
        }

        private List<SaleItem> BuildDiscountedSaleItems(out double subtotal, out double appliedDiscount, out double finalTotal)
        {
            subtotal = _basketItems.Sum(x => x.Quantity * x.SellPriceUZS);
            appliedDiscount = 0;
            finalTotal = subtotal;

            List<SaleItem> result = _basketItems
                .Select(x => new SaleItem
                {
                    ProductId = x.ProductId,
                    Quantity = x.Quantity,
                    SellPriceUZS = x.SellPriceUZS,
                    DiscountUZS = 0
                })
                .ToList();

            if (subtotal <= 0 || _activeDiscountType == "None" || _activeDiscountValue <= 0)
            {
                return result;
            }

            if (!_quickDiscountEnabled)
            {
                return result;
            }

            double requestedDiscount = _activeDiscountType == "Percent"
                ? subtotal * (_activeDiscountValue / 100.0)
                : _activeDiscountValue;
            requestedDiscount = Math.Min(requestedDiscount, subtotal);

            int count = result.Count;
            double[] lineTotals = new double[count];
            double[] capacities = new double[count];
            for (int i = 0; i < count; i++)
            {
                SaleItem line = result[i];
                Product? product = _allProducts.FirstOrDefault(p => p.Id == line.ProductId);
                double purchaseUnitUzs = product?.PurchasePriceUZS ?? 0;
                double lineTotal = line.Quantity * line.SellPriceUZS;
                double minLineTotal = line.Quantity * purchaseUnitUzs;
                lineTotals[i] = lineTotal;
                capacities[i] = Math.Max(0, lineTotal - minLineTotal);
            }

            double maxDiscount = capacities.Sum();
            double targetDiscount = Math.Min(requestedDiscount, maxDiscount);
            if (targetDiscount <= 0)
            {
                return result;
            }

            double[] alloc = new double[count];
            for (int i = 0; i < count; i++)
            {
                if (lineTotals[i] <= 0)
                {
                    continue;
                }

                alloc[i] = targetDiscount * (lineTotals[i] / subtotal);
                if (alloc[i] > capacities[i])
                {
                    alloc[i] = capacities[i];
                }
            }

            double remaining = targetDiscount - alloc.Sum();
            int safeGuard = 0;
            while (remaining > 0.5 && safeGuard < 20)
            {
                safeGuard++;
                double remainCapacity = 0;
                for (int i = 0; i < count; i++)
                {
                    remainCapacity += Math.Max(0, capacities[i] - alloc[i]);
                }

                if (remainCapacity <= 0.0001)
                {
                    break;
                }

                for (int i = 0; i < count; i++)
                {
                    double free = Math.Max(0, capacities[i] - alloc[i]);
                    if (free <= 0)
                    {
                        continue;
                    }

                    double add = Math.Min(free, remaining * (free / remainCapacity));
                    alloc[i] += add;
                }

                remaining = targetDiscount - alloc.Sum();
            }

            long[] rounded = alloc.Select(x => (long)Math.Round(x, MidpointRounding.AwayFromZero)).ToArray();
            long roundedTotal = rounded.Sum();
            long targetRounded = (long)Math.Round(targetDiscount, MidpointRounding.AwayFromZero);
            long diff = targetRounded - roundedTotal;
            if (diff != 0)
            {
                int sign = diff > 0 ? 1 : -1;
                diff = Math.Abs(diff);
                for (int i = 0; i < count && diff > 0; i++)
                {
                    if (sign > 0 && rounded[i] + 1 <= capacities[i] + 0.0001)
                    {
                        rounded[i] += 1;
                        diff--;
                    }
                    else if (sign < 0 && rounded[i] > 0)
                    {
                        rounded[i] -= 1;
                        diff--;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                double lineTotal = lineTotals[i];
                double lineDiscount = rounded[i];
                double finalLineTotal = Math.Max(0.01, lineTotal - lineDiscount);
                result[i].DiscountUZS = lineDiscount;
                result[i].SellPriceUZS = result[i].Quantity > 0
                    ? finalLineTotal / result[i].Quantity
                    : result[i].SellPriceUZS;
            }

            appliedDiscount = result.Sum(x => x.DiscountUZS);
            finalTotal = Math.Max(0, subtotal - appliedDiscount);
            return result;
        }

        private void UpdateSelectedInfo()
        {
            if (_selectedProduct == null)
            {
                _lblStockValue.Text = "-";
                _lblPurchaseValue.Text = "-";
                _lblSuggestedValue.Text = "-";
                return;
            }

            _lblStockValue.Text = $"{FormatQuantity(_selectedProduct.QuantityUSD)} dona";
            double purchaseUzs = _selectedProduct.PurchasePriceUZS;
            _lblPurchaseValue.Text = $"{purchaseUzs:N0} UZS";

            double suggestedUzs = purchaseUzs * (1 + (_suggestedMarkupPercent / 100.0));
            _lblSuggestedValue.Text = $"{suggestedUzs:N0} UZS ({_suggestedMarkupPercent:0.##}%)";

            if (_autoFillSuggestedPrice)
            {
                _txtPrice.Text = Math.Ceiling(suggestedUzs).ToString(CultureInfo.InvariantCulture);
            }
        }

        private static Panel NewCard()
        {
            Panel panel = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(0),
                Dock = DockStyle.Fill
            };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush brush = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(220, 227, 239));
                using GraphicsPath path = RoundedRect(panel.ClientRectangle, 16);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            panel.Resize += (s, e) => panel.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, panel.Width), Math.Max(1, panel.Height)), 16));
            return panel;
        }

        private static Panel NewBar(string text, Color c1, Color c2, string glyph, string? iconImageFile = null)
        {
            Panel bar = new Panel { Height = 52 };
            bar.Paint += (s, e) =>
            {
                using var lg = new LinearGradientBrush(bar.ClientRectangle, c1, c2, 0f);
                e.Graphics.FillRectangle(lg, bar.ClientRectangle);
            };

            Image? iconImage = string.IsNullOrWhiteSpace(iconImageFile) ? null : BrandingAssets.TryLoadAssetImage(iconImageFile);
            Control icon;
            if (iconImage != null)
            {
                icon = new PictureBox
                {
                    Width = 24,
                    Height = 24,
                    Left = 20,
                    Top = 14,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = iconImage
                };
            }
            else
            {
                icon = new Label
                {
                    Text = glyph,
                    Font = UiTheme.IconFont(18),
                    ForeColor = Color.White,
                    AutoSize = false,
                    Width = 28,
                    Height = 28,
                    Left = 20,
                    Top = 12,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
            }

            Label title = new Label
            {
                Text = text,
                AutoSize = true,
                Left = 54,
                Top = 14,
                Font = new Font("Bahnschrift SemiBold", 16, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(icon);
            bar.Controls.Add(title);
            return bar;
        }

        private static Panel NewInputWrap()
        {
            Panel p = new Panel { BackColor = Color.White };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(2, 2, Math.Max(1, p.Width - 5), Math.Max(1, p.Height - 5));
                using SolidBrush brush = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(50, 103, 188), 3.2f)
                {
                    LineJoin = LineJoin.Round
                };
                using GraphicsPath path = RoundedRect(bounds, 10);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            p.Resize += (s, e) => p.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, p.Width), Math.Max(1, p.Height)), 10));
            return p;
        }

        private void HideListScrollbars()
        {
            if (!_lstProducts.IsHandleCreated)
            {
                return;
            }

            _ = ShowScrollBar(_lstProducts.Handle, SB_VERT, false);
            _ = ShowScrollBar(_lstProducts.Handle, SB_HORZ, false);
            _ = ShowScrollBar(_lstProducts.Handle, SB_BOTH, false);
        }

        private void ListProducts_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_lstProducts.Items.Count == 0)
            {
                return;
            }

            int step = e.Delta > 0 ? -1 : 1;
            int target = Math.Max(0, Math.Min(_lstProducts.Items.Count - 1, _lstProducts.TopIndex + step));
            if (target != _lstProducts.TopIndex)
            {
                _lstProducts.TopIndex = target;
            }

            HideListScrollbars();
        }

        private static Button NewGradientButton(string text, Color c1, Color c2)
        {
            Button b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font("Bahnschrift SemiBold", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            b.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var lg = new LinearGradientBrush(b.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(b.ClientRectangle, 10);
                e.Graphics.FillPath(lg, path);
                TextRenderer.DrawText(e.Graphics, b.Text, b.Font, b.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            b.Resize += (s, e) => b.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, b.Width), Math.Max(1, b.Height)), 10));
            return b;
        }

        private static Button NewHeaderIconButton(string text, string iconFileName, string fallbackGlyph)
        {
            Button button = new Button
            {
                Width = 42,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(31, 55, 86),
                Font = UiTheme.IconFont(16),
                Text = fallbackGlyph,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(194, 207, 227);

            Image? actionImage = BrandingAssets.TryLoadAssetImage(iconFileName);
            if (actionImage != null)
            {
                button.Image = actionImage;
                button.Text = string.Empty;
                button.ImageAlign = ContentAlignment.MiddleCenter;
            }

            return button;
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

        private static string FormatQuantity(double qty)
        {
            return Math.Abs(qty % 1) < 0.000001 ? qty.ToString("N0") : qty.ToString("#,##0.##");
        }

        private sealed class BasketGridRow
        {
            public int BasketIndex { get; set; }
            public string Nomi { get; set; } = string.Empty;
            public double Soni { get; set; }
            public double Narxi { get; set; }
            public double Chegirma { get; set; }
            public double Jami { get; set; }
        }
    }
}



