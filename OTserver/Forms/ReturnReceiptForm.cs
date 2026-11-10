using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Forms
{
    public class ReturnReceiptForm : Form
    {
        private readonly ReturnReceipt _receipt;
        private readonly ComboBox _cmbPaper = new ComboBox();
        private readonly PictureBox _logoPreview = new PictureBox();
        private readonly RichTextBox _preview = new RichTextBox();
        private readonly PrintDocument _printDocument = new PrintDocument();
        private string[] _printLines = Array.Empty<string>();
        private int _printLineIndex;

        public ReturnReceiptForm(ReturnReceipt receipt)
        {
            _receipt = receipt;
            InitializeComponent();
            RefreshPreview();
        }

        private void InitializeComponent()
        {
            Text = "Qaytarish hujjati";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(430, 760);
            MinimumSize = new Size(390, 600);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                RowCount = 3,
                ColumnCount = 1
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            top.Controls.Add(new Label { Text = "Chek o'lchami:", AutoSize = true, Padding = new Padding(0, 7, 8, 0) });
            _cmbPaper.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPaper.Items.AddRange(new object[] { "80mm", "58mm" });
            _cmbPaper.SelectedIndex = 0;
            _cmbPaper.SelectedIndexChanged += (_, __) => RefreshPreview();
            top.Controls.Add(_cmbPaper);
            root.Controls.Add(top, 0, 0);

            _preview.Dock = DockStyle.Fill;
            _preview.ReadOnly = true;
            _preview.WordWrap = false;
            _preview.Font = new Font("Consolas", 10f);
            _preview.BackColor = Color.White;
            var previewArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            previewArea.Controls.Add(_preview);
            _logoPreview.Dock = DockStyle.Top;
            _logoPreview.Height = 64;
            _logoPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _logoPreview.BackColor = Color.White;
            previewArea.Controls.Add(_logoPreview);
            root.Controls.Add(previewArea, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            var close = new Button { Text = "Yopish", Width = 90, Height = 34 };
            close.Click += (_, __) => Close();
            var print = new Button { Text = "Chop etish", Width = 110, Height = 34 };
            print.Click += Print_Click;
            buttons.Controls.Add(close);
            buttons.Controls.Add(print);
            root.Controls.Add(buttons, 0, 2);

            _printDocument.BeginPrint += PrintDocument_BeginPrint;
            _printDocument.PrintPage += PrintDocument_PrintPage;
            _printDocument.DocumentName = $"Qaytarish_{_receipt.ReceiptNumber}";
            FormClosed += (_, __) => _logoPreview.Image?.Dispose();
        }

        private void RefreshPreview()
        {
            int chars = GetPaperWidthMm() >= 80 ? 38 : 28;
            _logoPreview.Image?.Dispose();
            _logoPreview.Image = CreateLogoBitmap(_receipt.LogoPng);
            _logoPreview.Visible = _logoPreview.Image != null;
            _preview.Text = BuildText(chars);
        }

        private string BuildText(int chars)
        {
            string separator = new string('-', chars);
            var builder = new StringBuilder();
            AppendWrapped(builder, "QAYTARISH HUJJATI", chars);
            builder.AppendLine(separator);
            if (!string.IsNullOrWhiteSpace(_receipt.BusinessName))
            {
                AppendWrapped(builder, _receipt.BusinessName, chars);
            }
            if (!string.IsNullOrWhiteSpace(_receipt.TIN))
            {
                AppendWrapped(builder, $"STIR: {_receipt.TIN}", chars);
            }
            if (!string.IsNullOrWhiteSpace(_receipt.StoreAddress))
            {
                AppendWrapped(builder, $"Manzil: {_receipt.StoreAddress}", chars);
            }
            if (!string.IsNullOrWhiteSpace(_receipt.StorePhone))
            {
                AppendWrapped(builder, $"Telefon: {_receipt.StorePhone}", chars);
            }
            AppendWrapped(builder, $"Hujjat raqami: {_receipt.ReceiptNumber}", chars);
            AppendWrapped(builder, $"Asl chek: {_receipt.OriginalReceiptNumber}", chars);
            AppendWrapped(builder, $"Sana/vaqt: {_receipt.IssuedAt:yyyy-MM-dd HH:mm:ss}", chars);
            if (!string.IsNullOrWhiteSpace(_receipt.Reason))
            {
                AppendWrapped(builder, $"Sabab: {_receipt.Reason}", chars);
            }
            builder.AppendLine(separator);
            builder.AppendLine("Qaytarilgan mahsulotlar:");
            foreach (ReturnReceiptItem item in _receipt.Items)
            {
                AppendWrapped(builder, item.ProductName, chars);
                AppendWrapped(
                    builder,
                    $"{item.Quantity:#,##0.##} x {item.UnitPriceUZS:N0} = {item.LineTotalUZS:N0} UZS",
                    chars);
            }
            builder.AppendLine(separator);
            if (_receipt.DiscountUZS > 0)
            {
                AppendWrapped(builder, $"Jami (chegirmagacha): {_receipt.SubtotalUZS:N0} UZS", chars);
                AppendWrapped(builder, $"Chegirma: -{_receipt.DiscountUZS:N0} UZS", chars);
            }
            AppendWrapped(builder, $"Qaytarish summasi: {_receipt.TotalUZS:N0} UZS", chars);
            if (_receipt.DebtReducedUZS > 0)
            {
                AppendWrapped(builder, $"Qarzdan kamaytirildi: {_receipt.DebtReducedUZS:N0} UZS", chars);
            }
            if (_receipt.CashRefundUZS > 0)
            {
                AppendWrapped(builder, $"Mijozga qaytarildi: {_receipt.CashRefundUZS:N0} UZS", chars);
            }
            builder.AppendLine(separator);
            return builder.ToString();
        }

        private void Print_Click(object? sender, EventArgs e)
        {
            int width = MmToHundredthsInch(GetPaperWidthMm());
            int chars = GetPaperWidthMm() >= 80 ? 38 : 28;
            int logoHeight = _receipt.LogoPng.Length == 0 ? 0 : 72;
            int height = Math.Max(200, BuildText(chars).Split('\n').Length * 15 + logoHeight + 32);
            _printDocument.DefaultPageSettings.PaperSize = new PaperSize($"ReturnReceipt{GetPaperWidthMm()}", width, height);
            _printDocument.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);

            using var dialog = new PrintDialog
            {
                AllowCurrentPage = false,
                AllowSelection = false,
                UseEXDialog = true,
                Document = _printDocument
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _printDocument.Print();
            }
        }

        private void PrintDocument_BeginPrint(object? sender, PrintEventArgs e)
        {
            int chars = GetPaperWidthMm() >= 80 ? 38 : 28;
            _printLines = BuildText(chars).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            _printLineIndex = 0;
            _printLogoPrinted = false;
        }

        private void PrintDocument_PrintPage(object? sender, PrintPageEventArgs e)
        {
            if (e.Graphics == null)
            {
                return;
            }

            using var font = new Font("Consolas", 8.8f);
            float y = e.MarginBounds.Top;
            float lineHeight = e.Graphics.MeasureString("A", font).Height + 2;
            if (!_printLogoPrinted)
            {
                if (_receipt.LogoPng.Length > 0)
                {
                    using Bitmap? logo = CreateLogoBitmap(_receipt.LogoPng);
                    if (logo != null)
                    {
                        float scale = Math.Min(Math.Min(160f, e.MarginBounds.Width) / logo.Width, 64f / logo.Height);
                        float width = logo.Width * scale;
                        float height = logo.Height * scale;
                        e.Graphics.DrawImage(logo, e.MarginBounds.Left + (e.MarginBounds.Width - width) / 2f, y, width, height);
                        y += height + 6;
                    }
                }
                _printLogoPrinted = true;
            }
            while (_printLineIndex < _printLines.Length && y + lineHeight <= e.MarginBounds.Bottom)
            {
                e.Graphics.DrawString(_printLines[_printLineIndex], font, Brushes.Black, e.MarginBounds.Left, y);
                y += lineHeight;
                _printLineIndex++;
            }

            e.HasMorePages = _printLineIndex < _printLines.Length;
        }

        private bool _printLogoPrinted;

        private int GetPaperWidthMm() =>
            string.Equals(_cmbPaper.Text, "58mm", StringComparison.OrdinalIgnoreCase) ? 58 : 80;

        private static int MmToHundredthsInch(int mm) =>
            (int)Math.Round(mm / 25.4 * 100.0);

        private static Bitmap? CreateLogoBitmap(byte[] logoPng)
        {
            if (logoPng.Length == 0)
            {
                return null;
            }

            using var stream = new MemoryStream(logoPng, writable: false);
            using Image image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            return new Bitmap(image);
        }

        private static void AppendWrapped(StringBuilder builder, string value, int width)
        {
            string remaining = value ?? string.Empty;
            while (remaining.Length > width)
            {
                int splitAt = remaining.LastIndexOf(' ', width);
                if (splitAt <= 0)
                {
                    splitAt = width;
                }

                builder.AppendLine(remaining.Substring(0, splitAt));
                remaining = remaining.Substring(splitAt).TrimStart();
            }

            builder.AppendLine(remaining);
        }
    }
}
