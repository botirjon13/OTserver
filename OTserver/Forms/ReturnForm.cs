using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class ReturnForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly ReturnService _returnService = new ReturnService();
        private readonly ReceiptService _receiptService = new ReceiptService();
        private readonly DateTimePicker _dtFrom = new DateTimePicker();
        private readonly DateTimePicker _dtTo = new DateTimePicker();
        private readonly TextBox _txtSaleId = new TextBox();
        private readonly DataGridView _gridSales = new DataGridView();
        private readonly DataGridView _gridLines = new DataGridView();
        private readonly BindingSource _lineBinding = new BindingSource();
        private readonly TextBox _txtReason = new TextBox();
        private readonly Label _lblPreview = new Label();
        private int _activeSaleId;

        public ReturnForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();
            ApplyThemeStyles();
            UiTheme.ThemeChanged += ReturnForm_ThemeChanged;
            FormClosed += (s, e) => UiTheme.ThemeChanged -= ReturnForm_ThemeChanged;
            LoadSales();
        }

        private void InitializeComponent()
        {
            Text = "Tovarni qaytarib olish";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1200, 760);
            MinimumSize = new Size(1040, 680);
            BackColor = UiTheme.BackgroundSecondary;
            Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift"), 11f);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 2
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            Panel top = new Panel { Dock = DockStyle.Fill, Height = 118, BackColor = UiTheme.BackgroundSecondary };

            var lblDate = new Label { Text = "Sana:", AutoSize = true, Left = 0, Top = 10 };
            _dtFrom.Format = DateTimePickerFormat.Short;
            _dtFrom.Value = DateTime.Today.AddDays(-30);
            _dtFrom.SetBounds(50, 6, 130, 32);

            var lblTo = new Label { Text = "gacha", AutoSize = true, Left = 190, Top = 10 };
            _dtTo.Format = DateTimePickerFormat.Short;
            _dtTo.Value = DateTime.Today;
            _dtTo.SetBounds(246, 6, 130, 32);

            Button btnLoadSales = new Button { Text = "Cheklarni ko'rish", Left = 392, Top = 6, Width = 150, Height = 32, Tag = "action-primary" };
            btnLoadSales.Click += (_, __) => LoadSales();

            var lblSaleId = new Label { Text = "Sotuv ID:", AutoSize = true, Left = 560, Top = 10 };
            _txtSaleId.SetBounds(632, 6, 100, 32);

            Button btnOpenById = new Button { Text = "Ochish", Left = 740, Top = 6, Width = 100, Height = 32, Tag = "action-primary" };
            btnOpenById.Click += (_, __) => OpenSaleById();

            Button btnToday = new Button { Text = "Bugun", Left = 852, Top = 6, Width = 100, Height = 32, Tag = "action-neutral" };
            btnToday.Click += (_, __) =>
            {
                _dtFrom.Value = DateTime.Today;
                _dtTo.Value = DateTime.Today;
                LoadSales();
            };

            Button btnMonth = new Button { Text = "Shu oy", Left = 960, Top = 6, Width = 100, Height = 32, Tag = "action-neutral" };
            btnMonth.Click += (_, __) =>
            {
                DateTime now = DateTime.Today;
                _dtFrom.Value = new DateTime(now.Year, now.Month, 1);
                _dtTo.Value = now;
                LoadSales();
            };

            Button btnReturnHistory = new Button { Text = "Qaytarish tarixi", Left = 852, Top = 90, Width = 120, Height = 26, Tag = "action-neutral" };
            btnReturnHistory.Click += (_, __) =>
            {
                using var history = new ReturnReceiptHistoryForm();
                history.ShowDialog(this);
            };

            var lblReason = new Label { Text = "Sabab:", AutoSize = true, Left = 0, Top = 54 };
            _txtReason.SetBounds(50, 50, 520, 60);
            _txtReason.Multiline = true;

            Button btnApply = new Button
            {
                Text = "Qaytarishni tasdiqlash",
                Left = 590,
                Top = 50,
                Width = 250,
                Height = 36,
                BackColor = UiTheme.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Tag = "action-success"
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += ApplyReturn_Click;

            Button btnClose = new Button { Text = "Yopish", Left = 852, Top = 50, Width = 120, Height = 36, Tag = "action-neutral" };
            btnClose.Click += (_, __) => Close();

            _lblPreview.Left = 980;
            _lblPreview.Top = 58;
            _lblPreview.Width = 200;
            _lblPreview.Height = 56;
            _lblPreview.TextAlign = ContentAlignment.MiddleRight;
            _lblPreview.ForeColor = UiTheme.Accent;
            _lblPreview.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11f, FontStyle.Bold);
            _lblPreview.Text = "Qaytarish: 0 UZS";
            _lblPreview.Tag = "accent-text";

            top.Controls.AddRange(new Control[]
            {
                lblDate, _dtFrom, lblTo, _dtTo, btnLoadSales, lblSaleId, _txtSaleId, btnOpenById, btnToday, btnMonth, btnReturnHistory,
                lblReason, _txtReason, btnApply, btnClose, _lblPreview
            });

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 320
            };

            BuildSalesGrid();
            BuildLinesGrid();
            Panel salesHost = new Panel { Dock = DockStyle.Fill };
            salesHost.Controls.Add(_gridSales);
            salesHost.Controls.Add(BuildStaticHeader(
                new[] { "Sotuv ID", "Chek", "Sana", "To'lov", "Jami (UZS)", "Amal" },
                new[] { 12f, 18f, 18f, 16f, 16f, 12f }));
            split.Panel1.Controls.Add(salesHost);
            split.Panel2.Controls.Add(_gridLines);
            root.Controls.Add(top, 0, 0);
            root.Controls.Add(split, 0, 1);
        }

        private void BuildSalesGrid()
        {
            _gridSales.Dock = DockStyle.Fill;
            _gridSales.ReadOnly = true;
            _gridSales.AllowUserToAddRows = false;
            _gridSales.AllowUserToDeleteRows = false;
            _gridSales.AllowUserToResizeRows = false;
            _gridSales.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridSales.MultiSelect = false;
            _gridSales.AutoGenerateColumns = false;
            _gridSales.RowHeadersVisible = false;
            _gridSales.ColumnHeadersVisible = false;
            _gridSales.EnableHeadersVisualStyles = false;
            _gridSales.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _gridSales.BackgroundColor = UiTheme.Card;
            _gridSales.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridSales.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _gridSales.RowTemplate.Height = 30;
            _gridSales.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridSales.ColumnHeadersHeight = 34;
            _gridSales.ScrollBars = ScrollBars.Both;
            _gridSales.CellDoubleClick += (_, __) => OpenSelectedSale();

            _gridSales.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sotuv ID", Name = "SaleId", FillWeight = 12 });
            _gridSales.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chek", Name = "ReceiptNumber", FillWeight = 18 });
            _gridSales.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sana", Name = "IssuedAt", FillWeight = 18 });
            _gridSales.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "To'lov", Name = "PaymentType", FillWeight = 16 });
            _gridSales.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Jami (UZS)", Name = "Total", FillWeight = 16 });

            var openBtn = new DataGridViewButtonColumn
            {
                HeaderText = "",
                Text = "Tanlash",
                UseColumnTextForButtonValue = true,
                FillWeight = 12
            };
            _gridSales.Columns.Add(openBtn);
            _gridSales.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == openBtn.Index)
                {
                    OpenSelectedSale();
                }
            };
        }

        private void BuildLinesGrid()
        {
            _gridLines.Dock = DockStyle.Fill;
            _gridLines.AllowUserToAddRows = false;
            _gridLines.AllowUserToDeleteRows = false;
            _gridLines.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridLines.MultiSelect = false;
            _gridLines.AutoGenerateColumns = false;
            _gridLines.RowHeadersVisible = false;
            _gridLines.BackgroundColor = UiTheme.Card;
            _gridLines.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridLines.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _gridLines.RowTemplate.Height = 30;
            _gridLines.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridLines.ColumnHeadersHeight = 34;
            _gridLines.ScrollBars = ScrollBars.Both;
            _gridLines.CellEndEdit += (_, __) => UpdatePreview();

            _gridLines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SaleItemId", DataPropertyName = nameof(LineRow.SaleItemId), Visible = false });
            _gridLines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mahsulot", DataPropertyName = nameof(LineRow.ProductName), FillWeight = 32, ReadOnly = true });
            _gridLines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sotilgan", DataPropertyName = nameof(LineRow.SoldQtyText), FillWeight = 12, ReadOnly = true });
            _gridLines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qaytgan", DataPropertyName = nameof(LineRow.ReturnedQtyText), FillWeight = 12, ReadOnly = true });
            _gridLines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Maksimal", DataPropertyName = nameof(LineRow.AvailableQtyText), FillWeight = 12, ReadOnly = true });
            _gridLines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Narx", DataPropertyName = nameof(LineRow.UnitPriceText), FillWeight = 12, ReadOnly = true });
            _gridLines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qaytarish soni", DataPropertyName = nameof(LineRow.ReturnQty), FillWeight = 12 });

            _gridLines.DataSource = _lineBinding;
        }

        private void LoadSales()
        {
            try
            {
                DateTime from = _dtFrom.Value.Date;
                DateTime to = _dtTo.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
                var rows = _receiptService.GetHistory(from, to)
                    .Select(x => new SaleRow
                    {
                        SaleId = x.SaleId,
                        ReceiptNumber = x.ReceiptNumber,
                        IssuedAt = x.IssuedAt.ToString("yyyy-MM-dd HH:mm"),
                        PaymentType = x.PaymentType,
                        TotalText = $"{x.TotalUZS:N0}"
                    })
                    .ToList();

                _gridSales.SuspendLayout();
                _gridSales.Rows.Clear();
                foreach (SaleRow row in rows)
                {
                    _gridSales.Rows.Add(row.SaleId, row.ReceiptNumber, row.IssuedAt, row.PaymentType, row.TotalText, "Tanlash");
                }
                _gridSales.ResumeLayout();
                ResetGridTop(_gridSales);

                _activeSaleId = 0;
                _txtSaleId.Text = string.Empty;
                _lineBinding.DataSource = new List<LineRow>();
                _lblPreview.Text = "Qaytarish: 0 UZS";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sotuvlarni yuklashda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenSelectedSale()
        {
            if (_gridSales.CurrentRow == null)
            {
                MessageBox.Show("Avval sotuv tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int saleId = Convert.ToInt32(_gridSales.CurrentRow.Cells[0].Value ?? 0);
            if (saleId <= 0)
            {
                MessageBox.Show("Avval sotuv tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenSaleLines(saleId);
        }

        private void OpenSaleById()
        {
            if (!int.TryParse(_txtSaleId.Text.Trim(), out int saleId) || saleId <= 0)
            {
                MessageBox.Show("To'g'ri Sotuv ID kiriting.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenSaleLines(saleId);
        }

        private void OpenSaleLines(int saleId)
        {
            try
            {
                var rows = _returnService.GetSaleLines(saleId);
                if (rows.Count == 0)
                {
                    MessageBox.Show("Bu sotuv uchun qaytariladigan mahsulot topilmadi.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _activeSaleId = saleId;
                _txtSaleId.Text = saleId.ToString(CultureInfo.InvariantCulture);
                _lineBinding.DataSource = rows.Select(x => new LineRow
                {
                    SaleItemId = x.SaleItemId,
                    ProductName = x.ProductName,
                    SoldQty = x.SoldQty,
                    ReturnedQty = x.ReturnedQty,
                    AvailableQty = x.AvailableQty,
                    UnitPriceUZS = x.UnitPriceUZS,
                    ReturnQty = 0
                }).ToList();
                UpdatePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sotuv qatorlarini ochishda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyReturn_Click(object? sender, EventArgs e)
        {
            if (_activeSaleId <= 0)
            {
                MessageBox.Show("Avval sotuvni tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_lineBinding.List is not IEnumerable<LineRow> lineRows)
            {
                MessageBox.Show("Qaytarish satrlari topilmadi.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<(int saleItemId, double quantity)> lines = new List<(int saleItemId, double quantity)>();
            foreach (LineRow row in lineRows)
            {
                if (row.ReturnQty <= 0)
                {
                    continue;
                }

                if (row.ReturnQty > row.AvailableQty + 0.000001)
                {
                    MessageBox.Show($"\"{row.ProductName}\" uchun maksimal qaytarish: {row.AvailableQty:0.##}", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                lines.Add((row.SaleItemId, row.ReturnQty));
            }

            if (lines.Count == 0)
            {
                MessageBox.Show("Hech bo'lmasa bitta mahsulot uchun qaytarish sonini kiriting.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                "Qaytarishni tasdiqlaysizmi? Bu amal ombor va sotuv summalariga ta'sir qiladi.",
                "Tasdiqlash",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ReturnService.ReturnApplyResult result;
            try
            {
                result = _returnService.ApplyReturn(_activeSaleId, lines, _txtReason.Text, _currentUser);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Qaytarishda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OpenSaleLines(_activeSaleId);
            LoadSales();
            try
            {
                ReturnReceipt? receipt = _returnService.GetReturnReceipt(result.ReturnId);
                if (receipt == null)
                {
                    throw new Exception("Qaytarish hujjati topilmadi.");
                }

                using var preview = new ReturnReceiptForm(receipt);
                preview.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Qaytarish saqlandi (ID={result.ReturnId}), ammo hujjatni ochib bo'lmadi: {ex.Message}",
                    "Xato",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void UpdatePreview()
        {
            if (_lineBinding.List is not IEnumerable<LineRow> rows)
            {
                _lblPreview.Text = "Qaytarish: 0 UZS";
                return;
            }

            double total = rows.Where(x => x.ReturnQty > 0).Sum(x => x.ReturnQty * x.UnitPriceUZS);
            _lblPreview.Text = $"Qaytarish:\n{total:N0} UZS";
        }

        private sealed class SaleRow
        {
            public int SaleId { get; set; }
            public string ReceiptNumber { get; set; } = "";
            public string IssuedAt { get; set; } = "";
            public string PaymentType { get; set; } = "";
            public string TotalText { get; set; } = "";
        }

        private sealed class LineRow
        {
            public int SaleItemId { get; set; }
            public string ProductName { get; set; } = "";
            public double SoldQty { get; set; }
            public double ReturnedQty { get; set; }
            public double AvailableQty { get; set; }
            public double UnitPriceUZS { get; set; }
            public double ReturnQty { get; set; }
            public string SoldQtyText => $"{SoldQty:0.##}";
            public string ReturnedQtyText => $"{ReturnedQty:0.##}";
            public string AvailableQtyText => $"{AvailableQty:0.##}";
            public string UnitPriceText => $"{UnitPriceUZS:N0}";
        }

        private static void ResetGridTop(DataGridView grid)
        {
            void Apply()
            {
                if (grid.IsDisposed || grid.Rows.Count == 0)
                {
                    return;
                }

                grid.ClearSelection();
                grid.CurrentCell = null;
                grid.FirstDisplayedScrollingColumnIndex = 0;
                grid.FirstDisplayedScrollingRowIndex = 0;
                grid.CurrentCell = grid.Rows[0].Cells[0];
                grid.Rows[0].Selected = true;
            }

            if (grid.IsHandleCreated)
            {
                grid.BeginInvoke((Action)Apply);
            }
            else
            {
                Apply();
            }
        }

        private static TableLayoutPanel BuildStaticHeader(string[] titles, float[] widths)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                BackColor = UiTheme.Border,
                ColumnCount = titles.Length,
                RowCount = 1,
                Tag = "static-header"
            };

            for (int i = 0; i < titles.Length; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, widths[i]));
                var lbl = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = titles[i],
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font(UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 10f, FontStyle.Bold),
                    ForeColor = UiTheme.Text,
                    BackColor = UiTheme.Card,
                    Padding = new Padding(6, 0, 0, 0),
                    Tag = "static-header-cell"
                };
                panel.Controls.Add(lbl, i, 0);
            }

            return panel;
        }

        private void ReturnForm_ThemeChanged(object? sender, EventArgs e)
        {
            ApplyThemeStyles();
        }

        private void ApplyThemeStyles()
        {
            ApplyThemeStyles(this);

            if (_gridSales.Columns.Count > 0)
            {
                DataGridViewCellStyle actionStyle = _gridSales.Columns[_gridSales.Columns.Count - 1].DefaultCellStyle;
                actionStyle.BackColor = UiTheme.Accent;
                actionStyle.ForeColor = Color.White;
                actionStyle.SelectionBackColor = UiTheme.Accent;
                actionStyle.SelectionForeColor = Color.White;
            }

            Invalidate(true);
        }

        private static void ApplyThemeStyles(Control control)
        {
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            string tag = control.Tag?.ToString() ?? string.Empty;

            if (control is Form form)
            {
                form.BackColor = UiTheme.BackgroundSecondary;
                form.ForeColor = UiTheme.Text;
            }
            else if (control is DataGridView grid)
            {
                UiTheme.StyleGrid(grid);
                grid.BackgroundColor = UiTheme.Card;
                grid.AlternatingRowsDefaultCellStyle.BackColor = isLight
                    ? Color.FromArgb(246, 249, 253)
                    : Color.FromArgb(25, 35, 46);
            }
            else if (control is Button button)
            {
                StyleActionButton(button, tag, isLight);
            }
            else if (control is DateTimePicker datePicker)
            {
                datePicker.BackColor = UiTheme.Card;
                datePicker.ForeColor = UiTheme.Text;
                datePicker.CalendarMonthBackground = UiTheme.Card;
                datePicker.CalendarTitleBackColor = UiTheme.BackgroundSecondary;
                datePicker.CalendarForeColor = UiTheme.Text;
            }
            else if (control is TextBox textBox)
            {
                textBox.BackColor = isLight ? Color.White : Color.FromArgb(26, 37, 49);
                textBox.ForeColor = UiTheme.Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is Label label)
            {
                label.ForeColor = tag == "accent-text" ? UiTheme.Accent : UiTheme.Text;
                label.BackColor = tag == "static-header-cell"
                    ? (isLight ? Color.FromArgb(228, 238, 249) : Color.FromArgb(31, 44, 59))
                    : Color.Transparent;
            }
            else if (control is TableLayoutPanel table && tag == "static-header")
            {
                table.BackColor = UiTheme.Border;
            }
            else if (control is Panel panel)
            {
                panel.BackColor = UiTheme.BackgroundSecondary;
            }
            else if (control is SplitContainer split)
            {
                split.BackColor = UiTheme.BackgroundSecondary;
                split.Panel1.BackColor = UiTheme.BackgroundSecondary;
                split.Panel2.BackColor = UiTheme.BackgroundSecondary;
            }

            foreach (Control child in control.Controls)
            {
                ApplyThemeStyles(child);
            }
        }

        private static void StyleActionButton(Button button, string tag, bool isLight)
        {
            Color backColor = tag switch
            {
                "action-primary" => UiTheme.Primary,
                "action-success" => UiTheme.Accent,
                _ => isLight ? Color.FromArgb(87, 102, 122) : Color.FromArgb(58, 79, 103)
            };
            Color hoverColor = tag switch
            {
                "action-primary" => isLight ? Color.FromArgb(0, 92, 130) : Color.FromArgb(0, 130, 185),
                "action-success" => isLight ? Color.FromArgb(12, 156, 109) : Color.FromArgb(16, 164, 143),
                _ => isLight ? Color.FromArgb(70, 85, 106) : Color.FromArgb(71, 94, 121)
            };

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.FlatAppearance.MouseDownBackColor = hoverColor;
            button.BackColor = backColor;
            button.ForeColor = Color.White;
            button.Font = UiTheme.ButtonFont;
            button.Cursor = Cursors.Hand;
        }
    }
}
