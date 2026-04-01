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
        private readonly ListView _list = new ListView();
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

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.GridLines = true;
            _list.HideSelection = false;
            _list.MultiSelect = false;
            _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _list.DoubleClick += ReprintSelected_Click;

            _list.Columns.Add("Sotuv ID", 100);
            _list.Columns.Add("Chek raqami", 190);
            _list.Columns.Add("Sana", 170);
            _list.Columns.Add("To'lov turi", 140);
            _list.Columns.Add("Jami (UZS)", 140);
            _list.Resize += (_, __) => AdjustHistoryColumns();

            root.Controls.Add(_list);
        }

        private void LoadHistory()
        {
            try
            {
                DateTime from = _dtFrom.Value.Date;
                DateTime to = _dtTo.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
                List<ReceiptService.ReceiptHistoryItem> items = _receiptService.GetHistory(from, to);

                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (ReceiptService.ReceiptHistoryItem x in items)
                {
                    var li = new ListViewItem(x.SaleId.ToString());
                    li.SubItems.Add(x.ReceiptNumber);
                    li.SubItems.Add(x.IssuedAt.ToString("yyyy-MM-dd HH:mm"));
                    li.SubItems.Add(x.PaymentType);
                    li.SubItems.Add($"{x.TotalUZS:N0}");
                    li.Tag = x.SaleId;
                    _list.Items.Add(li);
                }
                _list.EndUpdate();

                AdjustHistoryColumns();

                double total = items.Sum(x => x.TotalUZS);
                _lblSummary.Text = $"Topildi: {items.Count} ta chek | Ko'rindi: {_list.Items.Count} ta | Jami: {total:N0} UZS";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chek tarixini yuklashda xatolik: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ReprintSelected_Click(object? sender, EventArgs e)
        {
            if (_list.SelectedItems.Count == 0)
            {
                MessageBox.Show("Qayta chop etish uchun chek tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int saleId = _list.SelectedItems[0].Tag is int id ? id : 0;
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

        private void AdjustHistoryColumns()
        {
            if (_list.Columns.Count != 5)
            {
                return;
            }

            int width = Math.Max(760, _list.ClientSize.Width - 4);
            _list.Columns[0].Width = 100;
            _list.Columns[1].Width = 190;
            _list.Columns[2].Width = 170;
            _list.Columns[3].Width = 150;
            _list.Columns[4].Width = Math.Max(120, width - (100 + 190 + 170 + 150));
        }
    }
}
