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
        private readonly BindingSource _binding = new BindingSource();
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

            Panel root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            Controls.Add(root);

            Panel top = new Panel { Dock = DockStyle.Top, Height = 92 };
            root.Controls.Add(top);

            var lblFrom = new Label { Text = "Dan:", AutoSize = true, Left = 0, Top = 10 };
            _dtFrom.Format = DateTimePickerFormat.Short;
            _dtFrom.Value = DateTime.Today;
            _dtFrom.SetBounds(45, 6, 130, 30);

            var lblTo = new Label { Text = "Gacha:", AutoSize = true, Left = 190, Top = 10 };
            _dtTo.Format = DateTimePickerFormat.Short;
            _dtTo.Value = DateTime.Today;
            _dtTo.SetBounds(255, 6, 130, 30);

            Button btnFilter = new Button
            {
                Text = "Ko'rish",
                Left = 400,
                Top = 4,
                Width = 120,
                Height = 34
            };
            btnFilter.Click += (_, __) => LoadHistory();

            Button btnToday = new Button
            {
                Text = "Bugun",
                Left = 530,
                Top = 4,
                Width = 120,
                Height = 34
            };
            btnToday.Click += (_, __) =>
            {
                _dtFrom.Value = DateTime.Today;
                _dtTo.Value = DateTime.Today;
                LoadHistory();
            };

            Button btnMonth = new Button
            {
                Text = "Shu oy",
                Left = 660,
                Top = 4,
                Width = 120,
                Height = 34
            };
            btnMonth.Click += (_, __) =>
            {
                DateTime now = DateTime.Today;
                _dtFrom.Value = new DateTime(now.Year, now.Month, 1);
                _dtTo.Value = now;
                LoadHistory();
            };

            Button btnReprint = new Button
            {
                Text = "Qayta chop etish",
                Left = 0,
                Top = 48,
                Width = 210,
                Height = 34
            };
            btnReprint.Click += ReprintSelected_Click;

            Button btnClose = new Button
            {
                Text = "Yopish",
                Left = 220,
                Top = 48,
                Width = 120,
                Height = 34
            };
            btnClose.Click += (_, __) => Close();

            _lblSummary.AutoSize = true;
            _lblSummary.Left = 360;
            _lblSummary.Top = 55;
            _lblSummary.ForeColor = Color.FromArgb(72, 89, 110);

            top.Controls.AddRange(new Control[]
            {
                lblFrom, _dtFrom, lblTo, _dtTo, btnFilter, btnToday, btnMonth, btnReprint, btnClose, _lblSummary
            });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = Color.White;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _grid.RowTemplate.Height = 30;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = 34;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.DoubleClick += ReprintSelected_Click;

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Sotuv ID",
                DataPropertyName = nameof(ReceiptRow.SaleId),
                FillWeight = 16
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Chek raqami",
                DataPropertyName = nameof(ReceiptRow.ReceiptNumber),
                FillWeight = 24
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Sana",
                DataPropertyName = nameof(ReceiptRow.IssuedAtText),
                FillWeight = 24
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "To'lov turi",
                DataPropertyName = nameof(ReceiptRow.PaymentType),
                FillWeight = 16
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Jami (UZS)",
                DataPropertyName = nameof(ReceiptRow.TotalText),
                FillWeight = 20
            });

            _grid.DataSource = _binding;
            root.Controls.Add(_grid);
        }

        private void LoadHistory()
        {
            try
            {
                DateTime from = _dtFrom.Value.Date;
                DateTime to = _dtTo.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
                List<ReceiptService.ReceiptHistoryItem> items = _receiptService.GetHistory(from, to);

                var rows = items.Select(x => new ReceiptRow
                {
                    SaleId = x.SaleId,
                    ReceiptNumber = x.ReceiptNumber,
                    IssuedAt = x.IssuedAt,
                    IssuedAtText = x.IssuedAt.ToString("yyyy-MM-dd HH:mm"),
                    PaymentType = x.PaymentType,
                    TotalText = $"{x.TotalUZS:N0}"
                }).ToList();

                _binding.DataSource = rows;
                ResetGridViewport(_grid);
                double total = items.Sum(x => x.TotalUZS);
                _lblSummary.Text = $"Topildi: {items.Count} ta chek | Jami: {total:N0} UZS";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chek tarixini yuklashda xatolik: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ReprintSelected_Click(object? sender, EventArgs e)
        {
            if (_grid.CurrentRow?.DataBoundItem is not ReceiptRow row)
            {
                MessageBox.Show("Qayta chop etish uchun chek tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SaleReceipt? receipt = _receiptService.GetBySaleId(row.SaleId);
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

        private sealed class ReceiptRow
        {
            public int SaleId { get; set; }
            public string ReceiptNumber { get; set; } = "";
            public DateTime IssuedAt { get; set; }
            public string IssuedAtText { get; set; } = "";
            public string PaymentType { get; set; } = "";
            public string TotalText { get; set; } = "";
        }

        private static void ResetGridViewport(DataGridView grid)
        {
            grid.ClearSelection();
            if (grid.Rows.Count == 0)
            {
                return;
            }

            grid.FirstDisplayedScrollingRowIndex = 0;
            DataGridViewCell? firstVisibleCell = grid.Rows[0].Cells
                .Cast<DataGridViewCell>()
                .FirstOrDefault(x => x.Visible);

            if (firstVisibleCell != null)
            {
                grid.CurrentCell = firstVisibleCell;
                firstVisibleCell.OwningRow.Selected = true;
            }
        }
    }
}
