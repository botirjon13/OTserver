using System;
using System.Drawing;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;

namespace SantexnikaSRM.Forms
{
    public class ReturnReceiptHistoryForm : Form
    {
        private readonly ReturnService _returnService = new ReturnService();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly DataGridView _grid = new DataGridView();

        public ReturnReceiptHistoryForm()
        {
            Text = "Qaytarish hujjatlari tarixi";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(850, 520);
            MinimumSize = new Size(700, 420);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                RowCount = 2,
                ColumnCount = 1
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill };
            filters.Controls.Add(new Label { Text = "Dan:", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
            _from.Format = DateTimePickerFormat.Short;
            _from.Value = DateTime.Today.AddDays(-30);
            filters.Controls.Add(_from);
            filters.Controls.Add(new Label { Text = "Gacha:", AutoSize = true, Padding = new Padding(8, 7, 4, 0) });
            _to.Format = DateTimePickerFormat.Short;
            _to.Value = DateTime.Today;
            filters.Controls.Add(_to);
            var load = new Button { Text = "Ko'rish", Width = 90, Height = 30, Margin = new Padding(10, 0, 0, 0) };
            load.Click += (_, __) => LoadHistory();
            filters.Controls.Add(load);
            var reprint = new Button { Text = "Qayta chop etish", Width = 140, Height = 30, Margin = new Padding(8, 0, 0, 0) };
            reprint.Click += ReprintSelected;
            filters.Controls.Add(reprint);
            root.Controls.Add(filters, 0, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.DoubleClick += ReprintSelected;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReturnId", HeaderText = "Qaytarish hujjati", FillWeight = 22 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "OriginalReceipt", HeaderText = "Asl chek", FillWeight = 22 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IssuedAt", HeaderText = "Sana", FillWeight = 24 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Qaytarish summasi (UZS)", FillWeight = 32 });
            root.Controls.Add(_grid, 0, 1);
            LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                var items = _returnService.GetReturnHistory(_from.Value.Date, _to.Value.Date);
                _grid.Rows.Clear();
                foreach (ReturnService.ReturnHistoryItem item in items)
                {
                    _grid.Rows.Add(
                        $"RET-{item.ReturnId:D6}",
                        item.OriginalReceiptNumber,
                        item.IssuedAt.ToString("yyyy-MM-dd HH:mm"),
                        item.TotalUZS.ToString("N0"));
                    _grid.Rows[_grid.Rows.Count - 1].Tag = item.ReturnId;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Qaytarish hujjatlari tarixini yuklashda xatolik: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ReprintSelected(object? sender, EventArgs e)
        {
            if (_grid.CurrentRow?.Tag is not int returnId || returnId <= 0)
            {
                MessageBox.Show("Qayta chop etish uchun hujjat tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ReturnReceipt? receipt = _returnService.GetReturnReceipt(returnId);
                if (receipt == null)
                {
                    MessageBox.Show("Tanlangan qaytarish hujjati topilmadi.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var preview = new ReturnReceiptForm(receipt);
                preview.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Qaytarish hujjatini ochishda xatolik: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
