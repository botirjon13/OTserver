using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class ReceiptForm : Form
    {
        private readonly SaleReceipt _receipt;
        private readonly ComboBox _cmbPaper = new ComboBox();
        private readonly RichTextBox _txtPreview = new RichTextBox();
        private readonly PictureBox _logoPreview = new PictureBox();
        private readonly Panel _receiptPaper = new Panel();
        private readonly PrintDocument _printDocument = new PrintDocument();
        private string[] _printLines = Array.Empty<string>();
        private int _printLineIndex;
        private bool _printLogoPrinted;

        public ReceiptForm(SaleReceipt receipt, AppUser user)
        {
            _receipt = receipt;
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            RefreshPreview();
        }

        private void InitializeComponent()
        {
            Text = "Sotuv Cheki";
            Size = new Size(430, 860);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(230, 235, 244);
            DoubleBuffered = true;
            Padding = new Padding(1);

            Paint += (s, e) =>
            {
                using Pen pen = new Pen(Color.FromArgb(160, 174, 196), 1.2f);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            Panel root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 58 };
            header.Paint += (s, e) =>
            {
                using LinearGradientBrush lg = new LinearGradientBrush(header.ClientRectangle, Color.FromArgb(36, 106, 232), Color.FromArgb(60, 133, 247), 0f);
                e.Graphics.FillRectangle(lg, header.ClientRectangle);
            };
            Label lblTitle = new Label
            {
                Text = "Sotiv Cheki",
                Left = 22,
                Top = 18,
                AutoSize = true,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 16, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            Label btnClose = new Label
            {
                Text = "\uE711",
                Width = 20,
                Height = 20,
                Left = 374,
                Top = 18,
                Font = UiTheme.IconFont(11),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnClose.Click += (s, e) => Close();
            header.Controls.Add(lblTitle);
            header.Controls.Add(btnClose);
            header.Resize += (s, e) => btnClose.Left = header.Width - btnClose.Width - 14;

            Panel topTools = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.FromArgb(246, 248, 252), Padding = new Padding(22, 14, 22, 14) };
            Panel paperRow = new Panel { Width = 280, Height = 36, BackColor = Color.Transparent };
            Label lblPaper = new Label
            {
                Text = "Chek o'lchami:",
                Left = 0,
                Top = 8,
                AutoSize = true,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(45, 62, 87)
            };
            _cmbPaper.Left = 124;
            _cmbPaper.Top = 4;
            _cmbPaper.Width = 110;
            _cmbPaper.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPaper.Items.AddRange(new object[] { "80mm", "58mm" });
            _cmbPaper.SelectedIndex = 0;
            _cmbPaper.SelectedIndexChanged += (s, e) => RefreshPreview();
            paperRow.Controls.Add(lblPaper);
            paperRow.Controls.Add(_cmbPaper);
            topTools.Controls.Add(paperRow);
            topTools.Resize += (s, e) =>
            {
                int left = (topTools.ClientSize.Width - paperRow.Width) / 2;
                paperRow.Location = new Point(Math.Max(0, left), 12);
            };

            Panel scrollArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 248, 252), AutoScroll = true };
            _receiptPaper.BackColor = Color.White;
            _receiptPaper.BorderStyle = BorderStyle.FixedSingle;
            _receiptPaper.Left = 60;
            _receiptPaper.Top = 16;
            _receiptPaper.Width = 300;
            _receiptPaper.Height = 640;
            scrollArea.Controls.Add(_receiptPaper);

            _txtPreview.ReadOnly = true;
            _txtPreview.BorderStyle = BorderStyle.None;
            _txtPreview.ScrollBars = RichTextBoxScrollBars.None;
            _txtPreview.Multiline = true;
            _txtPreview.DetectUrls = false;
            _txtPreview.Font = new Font("Consolas", 10f, FontStyle.Regular);
            _txtPreview.BackColor = Color.White;
            _txtPreview.ForeColor = Color.FromArgb(18, 33, 55);
            _txtPreview.Left = 10;
            _txtPreview.Top = 10;
            _txtPreview.Width = _receiptPaper.Width - 22;
            _txtPreview.Height = 500;
            _receiptPaper.Controls.Add(_txtPreview);

            _logoPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _logoPreview.BackColor = Color.White;
            _logoPreview.Left = 10;
            _logoPreview.Top = 10;
            _logoPreview.Width = _receiptPaper.Width - 22;
            _logoPreview.Height = 66;
            _receiptPaper.Controls.Add(_logoPreview);

            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.FromArgb(246, 248, 252), Padding = new Padding(22, 12, 22, 12) };
            Button btnPrint = new Button
            {
                Text = "Print",
                Left = 0,
                Top = 0,
                Width = 170,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 14, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnPrint.FlatAppearance.BorderSize = 0;
            btnPrint.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using LinearGradientBrush lg = new LinearGradientBrush(btnPrint.ClientRectangle, Color.FromArgb(8, 182, 71), Color.FromArgb(7, 166, 66), 0f);
                using GraphicsPath p = RoundedRect(btnPrint.ClientRectangle, 10);
                e.Graphics.FillPath(lg, p);
                TextRenderer.DrawText(e.Graphics, "Print", btnPrint.Font, btnPrint.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btnPrint.Resize += (s, e) => btnPrint.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btnPrint.Width), Math.Max(1, btnPrint.Height)), 10));
            btnPrint.Click += Print_Click;

            Button btnCloseBottom = new Button
            {
                Text = "Yopish",
                Left = 178,
                Top = 0,
                Width = 180,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(38, 50, 71),
                BackColor = Color.FromArgb(244, 246, 250),
                Cursor = Cursors.Hand
            };
            btnCloseBottom.FlatAppearance.BorderColor = Color.FromArgb(206, 214, 228);
            btnCloseBottom.FlatAppearance.BorderSize = 1;
            btnCloseBottom.Click += (s, e) => Close();
            btnCloseBottom.Resize += (s, e) => btnCloseBottom.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btnCloseBottom.Width), Math.Max(1, btnCloseBottom.Height)), 10));

            bottom.Controls.Add(btnPrint);
            bottom.Controls.Add(btnCloseBottom);
            bottom.Resize += (s, e) =>
            {
                int gap = 10;
                int total = btnPrint.Width + btnCloseBottom.Width + gap;
                int left = (bottom.ClientSize.Width - total) / 2;
                int top = (bottom.ClientSize.Height - btnPrint.Height) / 2;
                btnPrint.Location = new Point(Math.Max(0, left), top);
                btnCloseBottom.Location = new Point(btnPrint.Right + gap, top);
            };

            root.Controls.Add(scrollArea);
            root.Controls.Add(bottom);
            root.Controls.Add(topTools);
            root.Controls.Add(header);

            _printDocument.PrintPage += PrintDocument_PrintPage;
            _printDocument.BeginPrint += PrintDocument_BeginPrint;
            _printDocument.DocumentName = $"Chek_{_receipt.ReceiptNumber}";
            FormClosed += (_, __) => _logoPreview.Image?.Dispose();
        }

        private void RefreshPreview()
        {
            int mm = GetPaperWidthMm();
            int chars = mm >= 80 ? 38 : 28;
            _receiptPaper.Width = mm >= 80 ? 330 : 282;
            _receiptPaper.Left = Math.Max(20, (ClientSize.Width - _receiptPaper.Width) / 2);
            _txtPreview.Width = _receiptPaper.Width - 22;
            _logoPreview.Width = _receiptPaper.Width - 22;
            _logoPreview.Image?.Dispose();
            _logoPreview.Image = CreateLogoBitmap(_receipt.LogoPng);
            _logoPreview.Visible = _logoPreview.Image != null;
            _txtPreview.Top = _logoPreview.Visible ? 82 : 10;

            _txtPreview.Text = BuildReceiptText(chars);
            _receiptPaper.Height = _txtPreview.Top + _txtPreview.GetPreferredSize(new Size(_txtPreview.Width, 0)).Height + 20;
        }

        private string BuildReceiptText(int chars)
        {
            string Sep() => new string('-', chars);
            var sb = new StringBuilder();
            void Wrapped(string text) => AppendWrappedText(sb, text, chars);
            string TwoCol(string l, string r)
            {
                if (l.Length + r.Length + 1 >= chars)
                {
                    Wrapped(l);
                    Wrapped(r);
                    return "";
                }
                return l + new string(' ', chars - l.Length - r.Length) + r;
            }

            if (!string.IsNullOrWhiteSpace(_receipt.BusinessName))
            {
                Wrapped(Center(_receipt.BusinessName.Trim(), chars));
            }
            if (!string.IsNullOrWhiteSpace(_receipt.TIN))
            {
                Wrapped($"STIR: {_receipt.TIN.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(_receipt.StoreAddress))
            {
                Wrapped($"Manzil: {_receipt.StoreAddress.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(_receipt.StorePhone))
            {
                Wrapped($"Telefon: {_receipt.StorePhone.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(_receipt.BusinessName) ||
                !string.IsNullOrWhiteSpace(_receipt.TIN) ||
                !string.IsNullOrWhiteSpace(_receipt.StoreAddress) ||
                !string.IsNullOrWhiteSpace(_receipt.StorePhone))
            {
                sb.AppendLine(Sep());
            }
            Wrapped($"Chek raqami: {_receipt.ReceiptNumber}");
            Wrapped($"Sana/vaqt: {_receipt.IssuedAt:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrWhiteSpace(_receipt.SellerName))
            {
                Wrapped($"Sotuvchi: {_receipt.SellerName.Trim()}");
            }
            sb.AppendLine(Sep());
            sb.AppendLine("Mahsulotlar:");
            foreach (var i in _receipt.Items)
            {
                Wrapped(i.ProductName);
                string columns = TwoCol($"{i.Quantity:#,##0.##} x {i.UnitPriceUZS:N0} UZS", $"{i.LineTotalUZS:N0} UZS");
                if (columns.Length > 0) sb.AppendLine(columns);
            }
            sb.AppendLine(Sep());
            Wrapped($"To'lov turi: {_receipt.PaymentType}");
            Wrapped(_receipt.IsVatPayer
                ? $"QQS: {_receipt.VatAmount:N0} UZS ({_receipt.VatRatePercent:0.##}%)"
                : "QQS: Qo'llanilmaydi");
            if (_receipt.DiscountUZS > 0)
            {
                string discountText = _receipt.DiscountType.Equals("Percent", StringComparison.OrdinalIgnoreCase)
                    ? $"{_receipt.DiscountValue:0.##}%"
                    : $"{_receipt.DiscountValue:N0} UZS";
                string subtotalColumns = TwoCol("Jami (chegirmagacha):", $"{_receipt.SubtotalUZS:N0} UZS");
                if (subtotalColumns.Length > 0) sb.AppendLine(subtotalColumns);
                string discountColumns = TwoCol($"Chegirma ({discountText}):", $"-{_receipt.DiscountUZS:N0} UZS");
                if (discountColumns.Length > 0) sb.AppendLine(discountColumns);
            }
            string totalColumns = TwoCol("Umumiy summa:", $"{_receipt.TotalUZS:N0} UZS");
            if (totalColumns.Length > 0) sb.AppendLine(totalColumns);
            if (!string.IsNullOrWhiteSpace(_receipt.ThankYouMessage))
            {
                sb.AppendLine(Sep());
                Wrapped(Center(_receipt.ThankYouMessage.Trim(), chars));
            }
            return sb.ToString();
        }

        private void Print_Click(object? sender, EventArgs e)
        {
            int mm = GetPaperWidthMm();
            int width = MmToHundredthsInch(mm);
            int chars = mm >= 80 ? 38 : 28;
            int lineCount = BuildReceiptText(chars).Split('\n').Length;
            int logoHeight = _receipt.LogoPng.Length == 0 ? 0 : 76;
            int height = Math.Max(200, lineCount * 16 + logoHeight + 32);
            _printDocument.DefaultPageSettings.PaperSize = new PaperSize($"Receipt{mm}", width, height);
            _printDocument.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);

            using PrintDialog dialog = new PrintDialog
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
            int mm = GetPaperWidthMm();
            int chars = mm >= 80 ? 38 : 28;
            _printLines = BuildReceiptText(chars).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            _printLineIndex = 0;
            _printLogoPrinted = false;
        }

        private void PrintDocument_PrintPage(object? sender, PrintPageEventArgs e)
        {
            if (e.Graphics == null)
            {
                return;
            }

            using Font font = new Font("Consolas", 8.8f, FontStyle.Regular);
            float y = e.MarginBounds.Top;
            float lineH = e.Graphics.MeasureString("A", font).Height + 2;
            float x = e.MarginBounds.Left;
            float bottom = e.MarginBounds.Bottom;

            if (!_printLogoPrinted)
            {
                if (_receipt.LogoPng.Length > 0)
                {
                    using Bitmap? logo = CreateLogoBitmap(_receipt.LogoPng);
                    if (logo != null)
                    {
                        float scale = Math.Min(Math.Min(160f, e.MarginBounds.Width) / logo.Width, 68f / logo.Height);
                        float width = logo.Width * scale;
                        float height = logo.Height * scale;
                        e.Graphics.DrawImage(logo, x + (e.MarginBounds.Width - width) / 2f, y, width, height);
                        y += height + 6;
                    }
                }
                _printLogoPrinted = true;
            }

            while (_printLineIndex < _printLines.Length)
            {
                if (y + lineH > bottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                string line = _printLines[_printLineIndex];
                e.Graphics.DrawString(line, font, Brushes.Black, x, y);
                y += lineH;
                _printLineIndex++;
            }

            e.HasMorePages = false;
        }

        private int GetPaperWidthMm()
        {
            return string.Equals(_cmbPaper.Text, "58mm", StringComparison.OrdinalIgnoreCase) ? 58 : 80;
        }

        private static int MmToHundredthsInch(int mm)
        {
            return (int)Math.Round(mm / 25.4 * 100.0);
        }

        private static string Center(string text, int width)
        {
            if (text.Length >= width) return text;
            int pad = (width - text.Length) / 2;
            return new string(' ', pad) + text;
        }

        private static void AppendWrappedText(StringBuilder builder, string value, int width)
        {
            string remaining = value ?? string.Empty;
            while (remaining.Length > width)
            {
                int splitAt = remaining.LastIndexOf(' ', width);
                if (splitAt <= 0)
                {
                    splitAt = width;
                }

                builder.AppendLine(remaining.Substring(0, splitAt).TrimEnd());
                remaining = remaining.Substring(splitAt).TrimStart();
            }

            builder.AppendLine(remaining);
        }

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
