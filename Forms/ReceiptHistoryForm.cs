using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;

namespace SantexnikaSRM.Forms
{
    public class ReceiptHistoryForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly ReceiptService _receiptService = new ReceiptService();
        private readonly DateTimePicker _dtFrom = new DateTimePicker();
        private readonly DateTimePicker _dtTo = new DateTimePicker();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _lblSummary = new Label();

        public ReceiptHistoryForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();
            LoadHistory();
        }

        private void InitializeComponent()
        {
            Text = "Chek tarixi";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 620);
            MinimumSize = new Size(900, 560);
            BackColor = Color.White;
            Font = new Font("Bahnschrift", 11f);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 2
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            Panel top = new Panel { Dock = DockStyle.Fill, Height = 92 };

            var lblFrom = new Label { Text = "Dan:", AutoSize = true, Left = 0, Top = 10 };
            _dtFrom.Format = DateTimePickerFormat.Short;
            _dtFrom.Value = DateTime.Today;
            _dtFrom.SetBounds(45, 6, 130, 30);

            var lblTo = new Label { Text = "Gacha:", AutoSize = true, Left = 190, Top = 10 };
            _dtTo.Format = DateTimePickerFormat.Short;
            _dtTo.Value = DateTime.Today;
            _dtTo.SetBounds(255, 6, 130, 30);

            Button btnFilter = new Button { Text = "Ko'rish", Left = 400, Top = 4, Width = 120, Height = 34 };
            btnFilter.Click += (_, __) => LoadHistory();

            Button btnToday = new Button { Text = "Bugun", Left = 530, Top = 4, Width = 120, Height = 34 };
            btnToday.Click += (_, __) =>
            {
                _dtFrom.Value = DateTime.Today;
                _dtTo.Value = DateTime.Today;
                LoadHistory();
            };

            Button btnMonth = new Button { Text = "Shu oy", Left = 660, Top = 4, Width = 120, Height = 34 };
            btnMonth.Click += (_, __) =>
            {
                DateTime now = DateTime.Today;
                _dtFrom.Value = new DateTime(now.Year, now.Month, 1);
                _dtTo.Value = now;
                LoadHistory();
            };

            Button btnReprint = new Button { Text = "Qayta chop etish", Left = 0, Top = 48, Width = 210, Height = 34 };
            btnReprint.Click += ReprintSelected_Click;

            Button btnClose = new Button { Text = "Yopish", Left = 220, Top = 48, Width = 120, Height = 34 };
            btnClose.Click += (_, __) => Close();

            _lblSummary.AutoSize = true;
            _lblSummary.Left = 360;
            _lblSummary.Top = 55;
            _lblSummary.ForeColor = Color.FromArgb(72, 89, 110);

            top.Controls.AddRange(new Control[] { lblFrom, _dtFrom, lblTo, _dtTo, btnFilter, btnToday, btnMonth, btnReprint, btnClose, _lblSummary });

            var header = BuildStaticHeader(
                new[] { "Sotuv ID", "Chek raqami", "Sana", "To'lov turi", "Jami (UZS)" },
                new[] { 14f, 26f, 24f, 20f, 20f });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.ColumnHeadersVisible = false;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _grid.BackgroundColor = Color.White;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _grid.RowTemplate.Height = 30;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = 34;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.DoubleClick += ReprintSelected_Click;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sotuv ID", Name = "SaleId", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chek raqami", Name = "ReceiptNumber", FillWeight = 26 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sana", Name = "IssuedAt", FillWeight = 24 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "To'lov turi", Name = "PaymentType", FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Jami (UZS)", Name = "Total", FillWeight = 20 });
            Panel gridHost = new Panel { Dock = DockStyle.Fill };
            gridHost.Controls.Add(_grid);
            gridHost.Controls.Add(header);
            root.Controls.Add(top, 0, 0);
            root.Controls.Add(gridHost, 0, 1);
        }

        private void LoadHistory()
        {
            try
            {
                DateTime from = _dtFrom.Value.Date;
                DateTime to = _dtTo.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
                List<ReceiptService.ReceiptHistoryItem> items = _receiptService.GetHistory(from, to);

                _grid.SuspendLayout();
                _grid.Rows.Clear();
                foreach (ReceiptService.ReceiptHistoryItem x in items)
                {
                    _grid.Rows.Add(x.SaleId, x.ReceiptNumber, x.IssuedAt.ToString("yyyy-MM-dd HH:mm"), x.PaymentType, $"{x.TotalUZS:N0}");
                }
                _grid.ResumeLayout();
                ResetGridTop(_grid);

                double total = items.Sum(x => x.TotalUZS);
                _lblSummary.Text = $"Topildi: {items.Count} ta chek | Ko'rindi: {_grid.Rows.Count} ta | Jami: {total:N0} UZS";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chek tarixini yuklashda xatolik: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ReprintSelected_Click(object? sender, EventArgs e)
        {
            if (_grid.CurrentRow == null)
            {
                MessageBox.Show("Qayta chop etish uchun chek tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int saleId = Convert.ToInt32(_grid.CurrentRow.Cells[0].Value ?? 0);
            if (saleId <= 0)
            {
                MessageBox.Show("Qayta chop etish uchun chek tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SaleReceipt? receipt = _receiptService.GetBySaleId(saleId);
                if (receipt == null)
                {
                    MessageBox.Show("Tanlangan sotuv bo'yicha chek topilmadi.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var preview = new ReceiptForm(receipt, _currentUser);
                preview.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chekni ochishda xatolik: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
                BackColor = Color.FromArgb(212, 222, 236),
                ColumnCount = titles.Length,
                RowCount = 1
            };

            for (int i = 0; i < titles.Length; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, widths[i]));
                var lbl = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = titles[i],
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Bahnschrift SemiBold", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(24, 35, 48),
                    BackColor = Color.FromArgb(233, 239, 248),
                    Padding = new Padding(6, 0, 0, 0)
                };
                panel.Controls.Add(lbl, i, 0);
            }

            return panel;
        }
    }
}
