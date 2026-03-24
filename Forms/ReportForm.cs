using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class ReportForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly ReportService _service = new ReportService();
        private readonly SaleService _saleService = new SaleService();
        private readonly ExpenseService _expenseService = new ExpenseService();

        private readonly Panel _content = new Panel();
        private readonly DateTimePicker _dtFrom = new DateTimePicker();
        private readonly DateTimePicker _dtTo = new DateTimePicker();

        private readonly Label _lblSales = new Label();
        private readonly Label _lblProfit = new Label();
        private readonly Label _lblExpenses = new Label();
        private readonly Label _lblNetProfit = new Label();
        private readonly Label _lblNetTitle = new Label();
        private readonly Label _lblWarning = new Label();
        private readonly Panel _netRow = new Panel();
        private readonly Panel _warningRow = new Panel();
        private readonly Panel _netIcon = new Panel();
        private bool _isNegativeNet;

        public ReportForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            AuthorizationService.Require(
                AuthorizationService.CanViewReports(_currentUser),
                "Hisobot bo'limi faqat admin uchun.");

            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            RenderResult(0, 0, 0, 0);
        }

        private void InitializeComponent()
        {
            Text = "Oylik Hisobot va Tahlil";
            Size = new Size(1420, 880);
            MinimumSize = new Size(1180, 760);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(239, 242, 248);
            Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            DoubleBuffered = true;

            Panel canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _content.BackColor = Color.Transparent;
            canvas.Controls.Add(_content);
            Controls.Add(canvas);

            Panel header = new Panel { Height = 88, BackColor = Color.Transparent };
            Panel titleIcon = new Panel
            {
                Left = 0,
                Top = 4,
                Width = 54,
                Height = 54
            };
            Image? reportHeaderIcon = BrandingAssets.TryLoadAssetImage("tile-reports.png");
            if (reportHeaderIcon != null)
            {
                titleIcon.BackColor = Color.Transparent;
                titleIcon.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = reportHeaderIcon
                });
            }
            else
            {
                titleIcon.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using LinearGradientBrush brush = new LinearGradientBrush(titleIcon.ClientRectangle, Color.FromArgb(74, 56, 230), Color.FromArgb(90, 95, 246), 45f);
                    using GraphicsPath path = RoundedRect(titleIcon.ClientRectangle, 16);
                    e.Graphics.FillPath(brush, path);
                    TextRenderer.DrawText(e.Graphics, "\uE9D2", UiTheme.IconFont(24), titleIcon.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                };
            }
            Label lblTitle = new Label
            {
                Text = "Oylik Hisobot va Tahlil",
                Left = 66,
                Top = 4,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 23, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 42, 65)
            };
            Label lblSub = new Label
            {
                Text = "Moliyaviy ko'rsatkichlarni tahlil qilish",
                Left = 68,
                Top = 40,
                AutoSize = true,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 89, 116)
            };
            header.Controls.Add(titleIcon);
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            Panel filterCard = BuildFilterCard();
            Panel resultCard = BuildResultCard();

            _content.Controls.Add(resultCard);
            _content.Controls.Add(filterCard);
            _content.Controls.Add(header);

            Action applyLayout = () =>
            {
                int w = Math.Min(1080, canvas.ClientSize.Width - 68);
                int x = (canvas.ClientSize.Width - w) / 2;
                _content.SetBounds(Math.Max(12, x), 16, w, Math.Max(620, canvas.ClientSize.Height - 30));

                header.SetBounds(0, 0, _content.Width, 88);
                filterCard.SetBounds(0, 94, _content.Width, 166);
                resultCard.SetBounds(0, 280, _content.Width, _content.Height - 280);
            };
            canvas.Resize += (s, e) => applyLayout();
            Load += (s, e) => applyLayout();
            Shown += (s, e) => applyLayout();
            applyLayout();
        }

        private Panel BuildFilterCard()
        {
            Panel card = NewCard();

            Panel titleBar = NewBar("Hisobot Davri", Color.FromArgb(136, 31, 226), Color.FromArgb(172, 70, 234), "\uE823", "tile-reports.png");
            titleBar.Dock = DockStyle.Top;

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(26, 16, 26, 20) };

            Label lblFrom = NewCaption("Boshlanish sanasi:", 0, 18);
            Panel fromWrap = NewInputWrap();
            fromWrap.SetBounds(0, 42, 440, 48);

            _dtFrom.Format = DateTimePickerFormat.Custom;
            _dtFrom.CustomFormat = "dd.MM.yyyy";
            _dtFrom.Font = new Font("Segoe UI", 11f, FontStyle.Regular);
            _dtFrom.CalendarMonthBackground = Color.White;
            _dtFrom.CalendarTitleBackColor = Color.FromArgb(233, 241, 255);
            _dtFrom.CalendarForeColor = Color.FromArgb(44, 58, 86);
            _dtFrom.Left = 18;
            _dtFrom.Top = 10;
            _dtFrom.Width = 404;
            fromWrap.Controls.Add(_dtFrom);

            Label lblTo = NewCaption("Tugash sanasi:", 462, 18);
            Panel toWrap = NewInputWrap();
            toWrap.SetBounds(462, 42, 440, 48);

            _dtTo.Format = DateTimePickerFormat.Custom;
            _dtTo.CustomFormat = "dd.MM.yyyy";
            _dtTo.Font = new Font("Segoe UI", 11f, FontStyle.Regular);
            _dtTo.CalendarMonthBackground = Color.White;
            _dtTo.CalendarTitleBackColor = Color.FromArgb(233, 241, 255);
            _dtTo.CalendarForeColor = Color.FromArgb(44, 58, 86);
            _dtTo.Left = 18;
            _dtTo.Top = 10;
            _dtTo.Width = 404;
            toWrap.Controls.Add(_dtTo);

            Button btnLoad = NewActionButton("KO'RISH", Color.FromArgb(40, 106, 243), Color.FromArgb(33, 95, 224), 12);
            btnLoad.SetBounds(924, 42, 122, 48);
            btnLoad.Click += (s, e) => LoadReportToScreen();

            Button btnExport = NewActionButton("EXCEL", Color.FromArgb(8, 180, 73), Color.FromArgb(7, 165, 66), 12);
            btnExport.SetBounds(1056, 42, 114, 48);
            btnExport.Click += (s, e) => ExportReportToExcel();

            body.Controls.Add(lblFrom);
            body.Controls.Add(fromWrap);
            body.Controls.Add(lblTo);
            body.Controls.Add(toWrap);
            body.Controls.Add(btnLoad);
            body.Controls.Add(btnExport);

            body.Resize += (s, e) =>
            {
                int buttonArea = 260;
                int gap = 12;
                int leftAreaWidth = Math.Max(280, body.ClientSize.Width - buttonArea);
                int each = Math.Min(300, (leftAreaWidth - gap) / 2);
                int pairWidth = (each * 2) + gap;
                int startX = Math.Max(0, (leftAreaWidth - pairWidth) / 2);
                int rightStart = startX + each + gap;

                lblFrom.Left = startX;
                fromWrap.SetBounds(startX, 42, each, 48);
                _dtFrom.Width = each - 36;
                lblTo.Left = rightStart;
                toWrap.SetBounds(rightStart, 42, each, 48);
                _dtTo.Width = each - 36;

                int btnLeft = body.ClientSize.Width - buttonArea + 2;
                btnLoad.SetBounds(btnLeft, 42, 122, 48);
                btnExport.SetBounds(btnLeft + 132, 42, 114, 48);
            };

            card.Controls.Add(body);
            card.Controls.Add(titleBar);
            return card;
        }

        private Panel BuildResultCard()
        {
            Panel card = NewCard();

            Panel titleBar = NewBar("Moliyaviy Natijalar", Color.FromArgb(74, 56, 230), Color.FromArgb(90, 95, 246), "\uE9D2", "tile-reports.png");
            titleBar.Dock = DockStyle.Top;

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(26, 18, 26, 20) };

            Panel rowSales = BuildMetricRow(Color.FromArgb(60, 133, 247), Color.FromArgb(237, 246, 255), "Jami Sotuv:", _lblSales, "money");
            Panel rowProfit = BuildMetricRow(Color.FromArgb(8, 188, 87), Color.FromArgb(238, 250, 243), "Jami Foyda:", _lblProfit, "profit");
            Panel rowExpense = BuildMetricRow(Color.FromArgb(255, 110, 0), Color.FromArgb(252, 247, 236), "Jami Xarajat:", _lblExpenses, "expense");

            rowSales.SetBounds(0, 30, 100, 72);
            rowProfit.SetBounds(0, 118, 100, 72);
            rowExpense.SetBounds(0, 206, 100, 72);

            Panel divider = new Panel
            {
                Left = 0,
                Top = 298,
                Width = 100,
                Height = 1,
                BackColor = Color.FromArgb(226, 230, 238)
            };

            _netRow.Left = 0;
            _netRow.Top = 320;
            _netRow.Width = 100;
            _netRow.Height = 98;
            _netRow.BackColor = Color.White;
            _netRow.Paint += NetRowPaintPositive;
            _netRow.Resize += (s, e) => _netRow.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _netRow.Width), Math.Max(1, _netRow.Height)), 12));

            _netIcon.Left = 18;
            _netIcon.Top = 18;
            _netIcon.Width = 44;
            _netIcon.Height = 44;
            _netIcon.BackColor = Color.FromArgb(11, 180, 79);
            _netIcon.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Image? netIconImage = GetReportMetricIcon(_isNegativeNet ? "loss" : "net");
                if (netIconImage != null)
                {
                    Rectangle imageRect = new Rectangle(0, 0, _netIcon.Width, _netIcon.Height);
                    e.Graphics.DrawImage(netIconImage, imageRect);
                }
                else
                {
                    using SolidBrush fill = new SolidBrush(_netIcon.BackColor);
                    using GraphicsPath path = RoundedRect(_netIcon.ClientRectangle, 10);
                    e.Graphics.FillPath(fill, path);
                    Rectangle iconRect = new Rectangle(10, 10, _netIcon.Width - 20, _netIcon.Height - 20);
                    DrawMiniIcon(e.Graphics, _isNegativeNet ? "loss" : "profit", iconRect, Color.White);
                }
            };
            _netIcon.Resize += (s, e) => _netIcon.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _netIcon.Width), Math.Max(1, _netIcon.Height)), 10));

            _lblNetTitle.Text = "Sof foyda:";
            _lblNetTitle.Left = 68;
            _lblNetTitle.Top = 32;
            _lblNetTitle.AutoSize = true;
            _lblNetTitle.Font = new Font("Bahnschrift SemiBold", 18, FontStyle.Bold);
            _lblNetTitle.ForeColor = Color.FromArgb(37, 49, 67);
            _lblNetTitle.BackColor = Color.Transparent;
            _lblNetProfit.AutoSize = false;
            _lblNetProfit.Top = 22;
            _lblNetProfit.Width = 440;
            _lblNetProfit.Height = 56;
            _lblNetProfit.TextAlign = ContentAlignment.MiddleRight;
            _lblNetProfit.Font = new Font("Bahnschrift SemiBold", 32, FontStyle.Bold);
            _lblNetProfit.ForeColor = Color.FromArgb(10, 156, 70);
            _lblNetProfit.BackColor = Color.Transparent;

            _netRow.Controls.Add(_netIcon);
            _netRow.Controls.Add(_lblNetTitle);
            _netRow.Controls.Add(_lblNetProfit);
            _netRow.Resize += (s, e) =>
            {
                _lblNetProfit.Left = _netRow.Width - _lblNetProfit.Width - 18;
            };

            _warningRow.Left = 0;
            _warningRow.Top = 436;
            _warningRow.Width = 100;
            _warningRow.Height = 56;
            _warningRow.BackColor = Color.White;
            _warningRow.Paint += WarningRowPaintPositive;
            _warningRow.Resize += (s, e) => _warningRow.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _warningRow.Width), Math.Max(1, _warningRow.Height)), 10));

            _lblWarning.Left = 16;
            _lblWarning.Top = 12;
            _lblWarning.Width = 1060;
            _lblWarning.Height = 40;
            _lblWarning.Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            _lblWarning.ForeColor = Color.FromArgb(22, 113, 57);
            _lblWarning.TextAlign = ContentAlignment.MiddleLeft;
            _lblWarning.BackColor = Color.Transparent;

            _warningRow.Controls.Add(_lblWarning);
            _warningRow.Resize += (s, e) =>
            {
                _lblWarning.Left = 16;
                _lblWarning.Width = Math.Max(220, _warningRow.Width - 32);
                _lblWarning.Top = Math.Max(8, (_warningRow.Height - _lblWarning.Height) / 2);
            };

            body.Controls.Add(rowSales);
            body.Controls.Add(rowProfit);
            body.Controls.Add(rowExpense);
            body.Controls.Add(divider);
            body.Controls.Add(_netRow);
            body.Controls.Add(_warningRow);

            Action applyResultLayout = () =>
            {
                int contentWidth = Math.Min(860, body.ClientSize.Width);
                int left = (body.ClientSize.Width - contentWidth) / 2;

                rowSales.SetBounds(left, 20, contentWidth, 72);
                rowProfit.SetBounds(left, 106, contentWidth, 72);
                rowExpense.SetBounds(left, 192, contentWidth, 72);
                divider.SetBounds(left, 278, contentWidth, 1);

                int warningHeight = 64;
                int warningTop = Math.Max(386, body.ClientSize.Height - warningHeight - 12);
                _warningRow.SetBounds(left, warningTop, contentWidth, warningHeight);
                _netRow.SetBounds(left, warningTop - 112, contentWidth, 98);
                _lblWarning.Width = Math.Max(260, _warningRow.Width - 24);
            };
            body.Resize += (s, e) => applyResultLayout();
            body.SizeChanged += (s, e) => applyResultLayout();
            applyResultLayout();

            card.Controls.Add(body);
            card.Controls.Add(titleBar);
            return card;
        }

        private Panel BuildMetricRow(Color accent, Color bg, string title, Label valueLabel, string iconKind)
        {
            Panel row = new Panel { BackColor = Color.White };
            row.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, row.Width - 3), Math.Max(1, row.Height - 3));
                using SolidBrush fill = new SolidBrush(bg);
                using GraphicsPath path = RoundedRect(bounds, 10);
                e.Graphics.FillPath(fill, path);
                using Pen border = new Pen(Color.FromArgb(165, accent.R, accent.G, accent.B), 2f);
                e.Graphics.DrawPath(border, path);
                using SolidBrush strip = new SolidBrush(accent);
                Rectangle stripRect = new Rectangle(1, 1, 6, Math.Max(1, row.Height - 3));
                using GraphicsPath stripPath = RoundedRect(stripRect, 5);
                e.Graphics.FillPath(strip, stripPath);
            };
            row.Resize += (s, e) => row.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, row.Width), Math.Max(1, row.Height)), 10));

            Panel iconWrap = new Panel
            {
                Left = 18,
                Top = 17,
                Width = 40,
                Height = 40,
                BackColor = accent
            };
            iconWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(iconWrap.BackColor);
                using GraphicsPath path = RoundedRect(iconWrap.ClientRectangle, 10);
                e.Graphics.FillPath(fill, path);
            };
            iconWrap.Resize += (s, e) => iconWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, iconWrap.Width), Math.Max(1, iconWrap.Height)), 10));
            Image? metricIconImage = GetReportMetricIcon(iconKind);
            if (metricIconImage != null)
            {
                iconWrap.BackColor = Color.Transparent;
                iconWrap.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = metricIconImage
                });
            }
            else
            {
                Label icon = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = string.Empty,
                    BackColor = Color.Transparent
                };
                iconWrap.Controls.Add(icon);
                iconWrap.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle iconRect = new Rectangle(10, 10, iconWrap.Width - 20, iconWrap.Height - 20);
                    DrawMiniIcon(e.Graphics, iconKind, iconRect, Color.White);
                };
            }

            Label lblTitle = new Label
            {
                Text = title,
                Left = 68,
                Top = 24,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 15.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(61, 74, 94),
                BackColor = Color.Transparent
            };

            valueLabel.AutoSize = false;
            valueLabel.Top = 14;
            valueLabel.Width = 380;
            valueLabel.Height = 44;
            valueLabel.TextAlign = ContentAlignment.MiddleRight;
            valueLabel.Font = new Font("Bahnschrift SemiBold", 22, FontStyle.Bold);
            valueLabel.ForeColor = accent;
            valueLabel.BackColor = Color.Transparent;

            row.Controls.Add(iconWrap);
            row.Controls.Add(lblTitle);
            row.Controls.Add(valueLabel);
            row.Resize += (s, e) => valueLabel.Left = row.Width - valueLabel.Width - 24;

            return row;
        }

        private static void DrawMiniIcon(Graphics g, string kind, Rectangle rect, Color color)
        {
            using Pen pen = new Pen(color, 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            if (string.Equals(kind, "money", StringComparison.OrdinalIgnoreCase))
            {
                TextRenderer.DrawText(
                    g,
                    "$",
                    new Font("Bahnschrift SemiBold", 14, FontStyle.Bold),
                    rect,
                    color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            if (string.Equals(kind, "profit", StringComparison.OrdinalIgnoreCase))
            {
                g.DrawLine(pen, rect.Left + 1, rect.Bottom - 3, rect.Right - 4, rect.Top + 4);
                g.DrawLine(pen, rect.Right - 9, rect.Top + 4, rect.Right - 4, rect.Top + 4);
                g.DrawLine(pen, rect.Right - 4, rect.Top + 4, rect.Right - 4, rect.Top + 9);
                return;
            }

            if (string.Equals(kind, "expense", StringComparison.OrdinalIgnoreCase))
            {
                g.DrawLine(pen, rect.Left + 2, rect.Top + 2, rect.Right - 2, rect.Bottom - 2);
                g.DrawLine(pen, rect.Left + 2, rect.Bottom - 2, rect.Right - 2, rect.Top + 2);
                return;
            }

            // loss
            g.DrawLine(pen, rect.Left + 1, rect.Top + 4, rect.Right - 4, rect.Bottom - 3);
            g.DrawLine(pen, rect.Right - 9, rect.Bottom - 3, rect.Right - 4, rect.Bottom - 3);
            g.DrawLine(pen, rect.Right - 4, rect.Bottom - 8, rect.Right - 4, rect.Bottom - 3);
        }

        private void LoadReportToScreen()
        {
            try
            {
                DateTime startDate = _dtFrom.Value.Date;
                DateTime endDate = _dtTo.Value.Date;
                var result = _service.GetMonthlyReport(startDate, endDate, _currentUser);
                RenderResult(result.totalSales, result.totalProfit, result.totalExpenses, result.netProfit);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hisobot yuklashda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenderResult(double totalSales, double totalProfit, double totalExpenses, double netProfit)
        {
            _lblSales.Text = $"{totalSales:N0} so'm";
            _lblProfit.Text = $"{totalProfit:N0} so'm";
            _lblExpenses.Text = $"{totalExpenses:N0} so'm";
            _lblNetProfit.Text = $"{netProfit:N0} so'm";

            bool isNegative = netProfit < 0;
            _lblNetProfit.ForeColor = isNegative ? Color.FromArgb(225, 16, 31) : Color.FromArgb(10, 156, 70);
            _lblWarning.Text = isNegative
                ? "Diqqat! Ushbu davrda zarar ko'rildi. Xarajatlarni kamaytirish yoki sotuvni oshirish tavsiya etiladi."
                : "Yaxshi natija! Ushbu davrda ijobiy sof foyda qayd etildi.";

            _lblNetTitle.Text = isNegative ? "Sof natija:" : "Sof foyda:";
            Image? netIconImage = GetReportMetricIcon(isNegative ? "loss" : "net");
            _netIcon.BackColor = netIconImage != null
                ? Color.Transparent
                : (isNegative ? Color.FromArgb(255, 54, 69) : Color.FromArgb(11, 180, 79));
            _isNegativeNet = isNegative;
            _netRow.Invalidate();
            _warningRow.Invalidate();
            _netIcon.Invalidate();

            Color warningText = isNegative ? Color.FromArgb(192, 41, 41) : Color.FromArgb(22, 113, 57);
            _lblWarning.ForeColor = warningText;

            // Text o'zgarganda ham qiymatlar doim o'ngga tekis turishi uchun.
            foreach (Control c in _netRow.Controls)
            {
                c.Invalidate();
            }
            if (_lblSales.Parent != null)
            {
                _lblSales.Left = Math.Max(280, _lblSales.Parent.Width - _lblSales.Width - 18);
            }
            if (_lblProfit.Parent != null)
            {
                _lblProfit.Left = Math.Max(280, _lblProfit.Parent.Width - _lblProfit.Width - 18);
            }
            if (_lblExpenses.Parent != null)
            {
                _lblExpenses.Left = Math.Max(280, _lblExpenses.Parent.Width - _lblExpenses.Width - 18);
            }
            _lblNetProfit.Left = Math.Max(320, _netRow.Width - _lblNetProfit.Width - 18);
            ApplySemanticPanels(isNegative);
            _netRow.Invalidate();
        }

        private void ApplySemanticPanels(bool isNegative)
        {
            _netRow.Paint -= NetRowPaintPositive;
            _netRow.Paint -= NetRowPaintNegative;
            _warningRow.Paint -= WarningRowPaintPositive;
            _warningRow.Paint -= WarningRowPaintNegative;

            if (isNegative)
            {
                _netRow.Paint += NetRowPaintNegative;
                _warningRow.Paint += WarningRowPaintNegative;
            }
            else
            {
                _netRow.Paint += NetRowPaintPositive;
                _warningRow.Paint += WarningRowPaintPositive;
            }
        }

        private void NetRowPaintPositive(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Math.Max(1, _netRow.Width - 3), Math.Max(1, _netRow.Height - 3));
            using SolidBrush fill = new SolidBrush(Color.FromArgb(238, 250, 243));
            using Pen border = new Pen(Color.FromArgb(122, 196, 147), 2);
            using GraphicsPath path = RoundedRect(bounds, 12);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }

        private void NetRowPaintNegative(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Math.Max(1, _netRow.Width - 3), Math.Max(1, _netRow.Height - 3));
            using SolidBrush fill = new SolidBrush(Color.FromArgb(255, 238, 240));
            using Pen border = new Pen(Color.FromArgb(231, 136, 150), 2);
            using GraphicsPath path = RoundedRect(bounds, 12);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }

        private void WarningRowPaintPositive(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Math.Max(1, _warningRow.Width - 3), Math.Max(1, _warningRow.Height - 3));
            using SolidBrush fill = new SolidBrush(Color.FromArgb(244, 251, 246));
            using Pen border = new Pen(Color.FromArgb(145, 202, 168), 2);
            using GraphicsPath path = RoundedRect(bounds, 10);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }

        private void WarningRowPaintNegative(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Math.Max(1, _warningRow.Width - 3), Math.Max(1, _warningRow.Height - 3));
            using SolidBrush fill = new SolidBrush(Color.FromArgb(255, 245, 246));
            using Pen border = new Pen(Color.FromArgb(236, 160, 172), 2);
            using GraphicsPath path = RoundedRect(bounds, 10);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }

        private static Image? GetReportMetricIcon(string iconKind)
        {
            if (string.Equals(iconKind, "money", StringComparison.OrdinalIgnoreCase))
            {
                return BrandingAssets.TryLoadAssetImage("tile-report-sales.png");
            }

            if (string.Equals(iconKind, "profit", StringComparison.OrdinalIgnoreCase))
            {
                return BrandingAssets.TryLoadAssetImage("tile-report-profit.png");
            }

            if (string.Equals(iconKind, "expense", StringComparison.OrdinalIgnoreCase))
            {
                return BrandingAssets.TryLoadAssetImage("tile-report-expenses.png");
            }

            if (string.Equals(iconKind, "loss", StringComparison.OrdinalIgnoreCase))
            {
                return BrandingAssets.TryLoadAssetImage("tile-report-net-loss.png");
            }

            return BrandingAssets.TryLoadAssetImage("tile-report-net-profit.png");
        }

        private void ExportReportToExcel()
        {
            try
            {
                DateTime startDate = _dtFrom.Value.Date;
                DateTime endDate = _dtTo.Value.Date.AddDays(1).AddSeconds(-1);
                double usdRate = _saleService.GetCurrentRate();
                var sales = _saleService.GetSaleReportRows(startDate, endDate, usdRate, _currentUser);
                var expenses = _expenseService.GetByDateRange(startDate, endDate, _currentUser);

                using SaveFileDialog dialog = new SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"hisobot_{startDate:yyyyMMdd}_{_dtTo.Value.Date:yyyyMMdd}.xlsx"
                };

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                ExcelExportHelper.ExportReport(
                    sales,
                    expenses,
                    usdRate,
                    dialog.FileName);

                MessageBox.Show("Hisobot Excel fayliga eksport qilindi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eksport xatosi: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Label NewCaption(string text, int left, int top)
        {
            return new Label
            {
                Text = text,
                Left = left,
                Top = top,
                AutoSize = true,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 95, 116)
            };
        }

        private static Panel NewCard()
        {
            Panel panel = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush brush = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(220, 227, 239));
                using GraphicsPath path = RoundedRect(panel.ClientRectangle, 14);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            };
            panel.Resize += (s, e) => panel.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, panel.Width), Math.Max(1, panel.Height)), 14));
            return panel;
        }

        private static Panel NewBar(string text, Color c1, Color c2, string glyph, string? iconImageFile = null)
        {
            Panel bar = new Panel { Height = 56 };
            bar.Paint += (s, e) =>
            {
                using LinearGradientBrush brush = new LinearGradientBrush(bar.ClientRectangle, c1, c2, 0f);
                e.Graphics.FillRectangle(brush, bar.ClientRectangle);
            };

            Image? iconImage = string.IsNullOrWhiteSpace(iconImageFile) ? null : BrandingAssets.TryLoadAssetImage(iconImageFile);
            Control icon;
            if (iconImage != null)
            {
                icon = new PictureBox
                {
                    Width = 20,
                    Height = 20,
                    Left = 18,
                    Top = 18,
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
                    Font = UiTheme.IconFont(14),
                    ForeColor = Color.White,
                    AutoSize = false,
                    Width = 20,
                    Height = 20,
                    Left = 18,
                    Top = 18,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
            }
            Label title = new Label
            {
                Text = text,
                AutoSize = true,
                Left = 42,
                Top = 15,
                Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold),
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
                Rectangle outer = new Rectangle(1, 1, Math.Max(1, p.Width - 3), Math.Max(1, p.Height - 3));
                using SolidBrush brush = new SolidBrush(Color.FromArgb(248, 250, 255));
                using Pen borderStrong = new Pen(Color.FromArgb(88, 121, 176), 2f);
                using GraphicsPath outerPath = RoundedRect(outer, 8);
                e.Graphics.FillPath(brush, outerPath);
                e.Graphics.DrawPath(borderStrong, outerPath);
            };
            p.Resize += (s, e) => p.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, p.Width), Math.Max(1, p.Height)), 8));
            return p;
        }

        private static Button NewActionButton(string text, Color c1, Color c2, int radius)
        {
            Button b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            b.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using LinearGradientBrush brush = new LinearGradientBrush(b.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(b.ClientRectangle, radius);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, b.Text, b.Font, b.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            b.Resize += (s, e) => b.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, b.Width), Math.Max(1, b.Height)), radius));
            return b;
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



