using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;

namespace SantexnikaSRM.Forms
{
    public class ReturnForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly ReturnService _returnService = new ReturnService();
        private readonly DateTimePicker _dtFrom = new DateTimePicker();
        private readonly DateTimePicker _dtTo = new DateTimePicker();
        private readonly TextBox _txtSaleId = new TextBox();
        private readonly ListView _salesList = new ListView();
        private readonly DataGridView _gridLines = new DataGridView();
        private readonly BindingSource _lineBinding = new BindingSource();
        private readonly TextBox _txtReason = new TextBox();
        private readonly Label _lblPreview = new Label();
        private int _activeSaleId;

        public ReturnForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();
            LoadSales();
        }

        private void InitializeComponent()
        {
            Text = "Tovarni qaytarib olish";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1200, 760);
            MinimumSize = new Size(1040, 680);
            BackColor = Color.White;
            Font = new Font("Bahnschrift", 11f);

            Panel root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            Controls.Add(root);

            Panel top = new Panel { Dock = DockStyle.Top, Height = 118 };
            root.Controls.Add(top);

            var lblDate = new Label { Text = "Sana:", AutoSize = true, Left = 0, Top = 10 };
            _dtFrom.Format = DateTimePickerFormat.Short;
            _dtFrom.Value = DateTime.Today.AddDays(-30);
            _dtFrom.SetBounds(50, 6, 130, 32);

            var lblTo = new Label { Text = "gacha", AutoSize = true, Left = 190, Top = 10 };
            _dtTo.Format = DateTimePickerFormat.Short;
            _dtTo.Value = DateTime.Today;
            _dtTo.SetBounds(246, 6, 130, 32);

            Button btnLoadSales = new Button
            {
                Text = "Cheklarni ko'rish",
                Left = 392,
                Top = 6,
                Width = 150,
                Height = 32
            };
            btnLoadSales.Click += (_, __) => LoadSales();

            var lblSaleId = new Label { Text = "Sotuv ID:", AutoSize = true, Left = 560, Top = 10 };
            _txtSaleId.SetBounds(632, 6, 100, 32);

            Button btnOpenById = new Button
            {
                Text = "Ochish",
                Left = 740,
                Top = 6,
                Width = 100,
                Height = 32
            };
            btnOpenById.Click += (_, __) => OpenSaleById();

            Button btnToday = new Button
            {
                Text = "Bugun",
                Left = 852,
                Top = 6,
                Width = 100,
                Height = 32
            };
            btnToday.Click += (_, __) =>
            {
                _dtFrom.Value = DateTime.Today;
                _dtTo.Value = DateTime.Today;
                LoadSales();
            };

            Button btnMonth = new Button
            {
                Text = "Shu oy",
                Left = 960,
                Top = 6,
                Width = 100,
                Height = 32
            };
            btnMonth.Click += (_, __) =>
            {
                DateTime now = DateTime.Today;
                _dtFrom.Value = new DateTime(now.Year, now.Month, 1);
                _dtTo.Value = now;
                LoadSales();
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
                BackColor = Color.FromArgb(16, 140, 82),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += ApplyReturn_Click;

            Button btnClose = new Button
            {
                Text = "Yopish",
                Left = 852,
                Top = 50,
                Width = 120,
                Height = 36
            };
            btnClose.Click += (_, __) => Close();

            _lblPreview.Left = 980;
            _lblPreview.Top = 58;
            _lblPreview.Width = 200;
            _lblPreview.Height = 56;
            _lblPreview.TextAlign = ContentAlignment.MiddleRight;
            _lblPreview.ForeColor = Color.FromArgb(30, 58, 92);
            _lblPreview.Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold);
            _lblPreview.Text = "Qaytarish: 0 UZS";

            top.Controls.AddRange(new Control[]
            {
                lblDate, _dtFrom, lblTo, _dtTo, btnLoadSales, lblSaleId, _txtSaleId, btnOpenById, btnToday, btnMonth,
                lblReason, _txtReason, btnApply, btnClose, _lblPreview
            });

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 320
            };
            root.Controls.Add(split);

            BuildSalesList();
            BuildLinesGrid();
            split.Panel1.Controls.Add(_salesList);
            split.Panel2.Controls.Add(_gridLines);
        }

        private void BuildSalesList()
        {
            _salesList.Dock = DockStyle.Fill;
            _salesList.View = View.Details;
            _salesList.FullRowSelect = true;
            _salesList.GridLines = true;
            _salesList.HideSelection = false;
            _salesList.MultiSelect = false;
            _salesList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _salesList.DoubleClick += (_, __) => OpenSelectedSale();

            _salesList.Columns.Add("Sotuv ID", 100);
            _salesList.Columns.Add("Chek", 180);
            _salesList.Columns.Add("Sana", 170);
            _salesList.Columns.Add("To'lov", 170);
            _salesList.Columns.Add("Jami (UZS)", 150);
            _salesList.Columns.Add("Amal", 120);
            _salesList.Resize += (_, __) => AdjustSalesColumns();
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
            _gridLines.BackgroundColor = Color.White;
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
                var rows = _returnService.GetSales(from, to)
                    .Select(x => new SaleRow
                    {
                        SaleId = x.SaleId,
                        ReceiptNumber = x.ReceiptNumber,
                        IssuedAt = x.IssuedAt.ToString("yyyy-MM-dd HH:mm"),
                        PaymentType = x.PaymentType,
                        TotalText = $"{x.TotalUZS:N0}"
                    })
                    .ToList();

                _salesList.BeginUpdate();
                _salesList.Items.Clear();
                foreach (SaleRow row in rows)
                {
                    var li = new ListViewItem(row.SaleId.ToString());
                    li.SubItems.Add(row.ReceiptNumber);
                    li.SubItems.Add(row.IssuedAt);
                    li.SubItems.Add(row.PaymentType);
                    li.SubItems.Add(row.TotalText);
                    li.SubItems.Add("Tanlash");
                    li.Tag = row.SaleId;
                    _salesList.Items.Add(li);
                }
                _salesList.EndUpdate();
                AdjustSalesColumns();

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
            if (_salesList.SelectedItems.Count == 0)
            {
                MessageBox.Show("Avval sotuv tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int saleId = _salesList.SelectedItems[0].Tag is int id ? id : 0;
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

            try
            {
                var result = _returnService.ApplyReturn(_activeSaleId, lines, _txtReason.Text, _currentUser);
                MessageBox.Show(
                    $"Qaytarish saqlandi.\n\nQaytarish ID: #{result.ReturnId}\nQaytarish summasi: {result.TotalUZS:N0} UZS",
                    "Muvaffaqiyatli",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                OpenSaleLines(_activeSaleId);
                LoadSales();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Qaytarishda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private void AdjustSalesColumns()
        {
            if (_salesList.Columns.Count != 6)
            {
                return;
            }

            int width = Math.Max(900, _salesList.ClientSize.Width - 4);
            _salesList.Columns[0].Width = 100;
            _salesList.Columns[1].Width = 180;
            _salesList.Columns[2].Width = 170;
            _salesList.Columns[3].Width = 170;
            _salesList.Columns[5].Width = 120;
            _salesList.Columns[4].Width = Math.Max(120, width - (100 + 180 + 170 + 170 + 120));
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
    }
}
