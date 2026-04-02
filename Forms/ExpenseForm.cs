using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class ExpenseForm : Form
    {
        private readonly ExpenseService _service = new ExpenseService();
        private readonly AppUser _currentUser;

        private readonly DateTimePicker _dtDate = new DateTimePicker();
        private readonly TextBox _txtDateDisplay = new TextBox();
        private readonly TextBox _txtType = new TextBox();
        private readonly TextBox _txtDesc = new TextBox();
        private readonly TextBox _txtAmount = new TextBox();
        private ToolStripDropDown? _datePopup;
        private readonly FlowLayoutPanel _recentList = new FlowLayoutPanel();

        private readonly Panel _content = new Panel();
        private readonly Panel _header = new Panel();
        private readonly TableLayoutPanel _main = new TableLayoutPanel();

        public ExpenseForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            AuthorizationService.Require(
                AuthorizationService.CanManageExpenses(_currentUser),
                "Rasxod bo'limi faqat admin uchun.");

            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            LoadRecentExpenses();
        }

        private void InitializeComponent()
        {
            Text = "Yangi Rasxod Qo'shish";
            Size = new Size(1460, 860);
            MinimumSize = new Size(1200, 760);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(239, 242, 248);
            Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            DoubleBuffered = true;

            Panel canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _content.BackColor = Color.Transparent;

            _header.Height = 96;
            _header.BackColor = Color.Transparent;
            Label lblTitle = new Label
            {
                Text = "Yangi Rasxod Qo'shish",
                AutoSize = true,
                Left = 0,
                Top = 2,
                Font = new Font("Bahnschrift SemiBold", 30, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 42, 65)
            };
            Label lblSubTitle = new Label
            {
                Text = "Kundalik xarajatlarni ro'yxatga olish",
                AutoSize = true,
                Left = 2,
                Top = 52,
                Font = new Font("Bahnschrift", 15, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 89, 116)
            };
            _header.Controls.Add(lblTitle);
            _header.Controls.Add(lblSubTitle);

            _main.Dock = DockStyle.Fill;
            _main.ColumnCount = 2;
            _main.RowCount = 1;
            _main.Padding = new Padding(0, 12, 0, 0);
            _main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            Panel leftCard = BuildLeftCard();
            Panel rightCard = BuildRightCard();
            leftCard.Margin = new Padding(0, 0, 10, 0);
            rightCard.Margin = new Padding(10, 0, 0, 0);

            _main.Controls.Add(leftCard, 0, 0);
            _main.Controls.Add(rightCard, 1, 0);

            _content.Controls.Add(_main);
            _content.Controls.Add(_header);
            canvas.Controls.Add(_content);
            Controls.Add(canvas);

            canvas.Resize += (s, e) => ArrangeLayout(canvas);
            Shown += (s, e) => ArrangeLayout(canvas);
        }

        private Panel BuildLeftCard()
        {
            Panel card = NewCard();
            Panel titleBar = NewBar("Yangi Rasxod", Color.FromArgb(7, 188, 74), Color.FromArgb(8, 180, 71), "\uE710", "tile-expenses.png");
            titleBar.Dock = DockStyle.Top;

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 24), BackColor = Color.White };

            Label lblDate = NewCaption("Sana:", 0, 26);
            Panel dateWrap = NewInputWrap();
            dateWrap.SetBounds(0, 58, 100, 52);
            _dtDate.Format = DateTimePickerFormat.Custom;
            _dtDate.CustomFormat = "dd.MM.yyyy";
            _dtDate.Width = 1;
            _dtDate.Height = 1;
            _dtDate.Left = -200;
            _dtDate.Top = -200;
            _dtDate.Font = new Font("Bahnschrift", 11f, FontStyle.Regular);
            _dtDate.CalendarForeColor = Color.FromArgb(40, 52, 74);
            _dtDate.ValueChanged += (s, e) =>
            {
                _txtDateDisplay.Text = _dtDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            };
            StyleInputBox(_txtDateDisplay, "dd.MM.yyyy");
            _txtDateDisplay.ReadOnly = true;
            _txtDateDisplay.Cursor = Cursors.Hand;
            _txtDateDisplay.TabStop = false;
            _txtDateDisplay.Text = _dtDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            _txtDateDisplay.Click += (s, e) => ShowDatePopup(dateWrap);
            dateWrap.Click += (s, e) => ShowDatePopup(dateWrap);
            dateWrap.Controls.Add(_txtDateDisplay);
            dateWrap.Controls.Add(_dtDate);

            Label lblType = NewCaption("Turi (Masalan: Ijara, Ish haqi)", 0, 124);
            Panel typeWrap = NewInputWrap();
            typeWrap.SetBounds(0, 156, 100, 52);
            StyleInputBox(_txtType, "Rasxod turini kiriting");
            typeWrap.Controls.Add(_txtType);

            Label lblDesc = NewCaption("Izoh (Ixtiyoriy)", 0, 222);
            Panel descWrap = NewInputWrap();
            descWrap.SetBounds(0, 254, 100, 52);
            StyleInputBox(_txtDesc, "Qo'shimcha ma'lumot");
            descWrap.Controls.Add(_txtDesc);

            Label lblAmount = NewCaption("Summa UZS", 0, 320);
            Panel amountWrap = NewInputWrap();
            amountWrap.SetBounds(0, 352, 100, 52);
            StyleInputBox(_txtAmount, "0");
            _txtAmount.Text = "0";
            amountWrap.Controls.Add(_txtAmount);

            Button btnSave = NewActionButton("SAQLASH", Color.FromArgb(7, 188, 74), Color.FromArgb(8, 178, 71));
            btnSave.SetBounds(0, 428, 100, 58);
            btnSave.Click += SaveExpense_Click;

            body.Controls.Add(lblDate);
            body.Controls.Add(dateWrap);
            body.Controls.Add(lblType);
            body.Controls.Add(typeWrap);
            body.Controls.Add(lblDesc);
            body.Controls.Add(descWrap);
            body.Controls.Add(lblAmount);
            body.Controls.Add(amountWrap);
            body.Controls.Add(btnSave);

            Action applyLeftLayout = () =>
            {
                int contentWidth = Math.Min(460, body.ClientSize.Width - 12);
                int left = (body.ClientSize.Width - contentWidth) / 2;

                lblDate.Left = left;
                lblType.Left = left;
                lblDesc.Left = left;
                lblAmount.Left = left;

                dateWrap.SetBounds(left, 58, contentWidth, 52);
                _txtDateDisplay.Width = dateWrap.Width - 22;
                typeWrap.SetBounds(left, 156, contentWidth, 52);
                _txtType.Width = typeWrap.Width - 22;
                descWrap.SetBounds(left, 254, contentWidth, 52);
                _txtDesc.Width = descWrap.Width - 22;
                amountWrap.SetBounds(left, 352, contentWidth, 52);
                _txtAmount.Width = amountWrap.Width - 22;
                btnSave.SetBounds(left, 428, contentWidth, 58);
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
            Panel titleBar = NewBar("Oxirgi Rasxodlar", Color.FromArgb(255, 110, 0), Color.FromArgb(255, 98, 0), "\uE9D2", "tile-expenses.png");
            titleBar.Dock = DockStyle.Top;

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 24), BackColor = Color.White };

            _recentList.Dock = DockStyle.Fill;
            _recentList.FlowDirection = FlowDirection.TopDown;
            _recentList.WrapContents = false;
            _recentList.AutoScroll = true;
            _recentList.BackColor = Color.White;
            _recentList.Padding = new Padding(0);
            _recentList.Margin = new Padding(0);
            _recentList.SizeChanged += (s, e) => ResizeRecentCards();

            body.Controls.Add(_recentList);
            card.Controls.Add(body);
            card.Controls.Add(titleBar);
            return card;
        }

        private void SaveExpense_Click(object? sender, EventArgs e)
        {
            try
            {
                string type = _txtType.Text.Trim();
                string desc = _txtDesc.Text.Trim();
                if (string.IsNullOrWhiteSpace(type))
                {
                    MessageBox.Show("Rasxod turini kiriting.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!double.TryParse(_txtAmount.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double amount))
                {
                    double.TryParse(_txtAmount.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
                }

                if (amount <= 0)
                {
                    MessageBox.Show("Summa musbat son bo'lishi kerak.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _service.Add(new Expense
                {
                    Date = _dtDate.Value,
                    Type = type,
                    Description = desc,
                    AmountUZS = amount
                }, _currentUser);

                MessageBox.Show("Rasxod muvaffaqiyatli saqlandi!", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _txtType.Clear();
                _txtDesc.Clear();
                _txtAmount.Text = "0";
                _txtType.Focus();
                LoadRecentExpenses();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xatolik yuz berdi: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadRecentExpenses()
        {
            _recentList.SuspendLayout();
            _recentList.Controls.Clear();

            List<Expense> recent = _service.GetLatest(10, _currentUser);
            if (recent.Count == 0)
            {
                Label empty = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 220,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "Rasxodlar mavjud emas",
                    ForeColor = Color.FromArgb(134, 147, 167),
                    Font = new Font("Bahnschrift", 12, FontStyle.Regular)
                };
                _recentList.Controls.Add(empty);
            }
            else
            {
                foreach (Expense item in recent)
                {
                    _recentList.Controls.Add(BuildRecentExpenseCard(item));
                }
            }

            _recentList.ResumeLayout();
            ResizeRecentCards();
        }

        private Control BuildRecentExpenseCard(Expense item)
        {
            Panel panel = new Panel
            {
                Width = Math.Max(380, _recentList.ClientSize.Width - 6),
                Height = 142,
                Margin = new Padding(0, 0, 0, 14),
                BackColor = Color.White
            };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle drawBounds = new Rectangle(1, 1, Math.Max(1, panel.Width - 3), Math.Max(1, panel.Height - 3));
                using SolidBrush fill = new SolidBrush(Color.FromArgb(247, 244, 238));
                using Pen border = new Pen(Color.FromArgb(255, 158, 64), 2.4f)
                {
                    LineJoin = LineJoin.Round
                };
                using GraphicsPath path = RoundedRect(drawBounds, 12);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            panel.Resize += (s, e) => panel.Region = new Region(RoundedRect(new Rectangle(1, 1, Math.Max(1, panel.Width - 2), Math.Max(1, panel.Height - 2)), 12));

            Label lblType = new Label
            {
                AutoSize = false,
                Left = 16,
                Top = 18,
                Height = 30,
                Text = item.Type,
                Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 57, 76),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            Label lblDate = new Label
            {
                AutoSize = false,
                Top = 22,
                Height = 22,
                Width = 92,
                Text = item.Date.ToString("yyyy-MM-dd"),
                Font = new Font("Bahnschrift", 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(122, 135, 156),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label lblDesc = new Label
            {
                Left = 16,
                Top = 54,
                Width = panel.Width - 32,
                Height = 24,
                Text = string.IsNullOrWhiteSpace(item.Description) ? "-" : item.Description,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(85, 100, 121),
                BackColor = Color.Transparent
            };

            Panel divider = new Panel
            {
                Left = 16,
                Top = 84,
                Width = panel.Width - 32,
                Height = 1,
                BackColor = Color.FromArgb(214, 208, 200)
            };

            Label lblAmount = new Label
            {
                AutoSize = true,
                Top = 96,
                Text = $"{item.AmountUZS:N0} UZS",
                Font = new Font("Bahnschrift SemiBold", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 85, 0),
                BackColor = Color.Transparent
            };

            Button btnEdit = NewMiniButton("Tahrirlash", Color.FromArgb(71, 130, 224), Color.FromArgb(53, 112, 208));
            Button btnDelete = NewMiniButton("O'chirish", Color.FromArgb(224, 88, 88), Color.FromArgb(207, 67, 67));

            btnEdit.Click += (s, e) =>
            {
                if (ShowExpenseEditDialog(item, out Expense? updated) && updated != null)
                {
                    try
                    {
                        _service.Update(updated, _currentUser);
                        LoadRecentExpenses();
                        MessageBox.Show("Rasxod tahrirlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Rasxodni tahrirlashda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            btnDelete.Click += (s, e) =>
            {
                DialogResult confirm = MessageBox.Show(
                    $"Ushbu rasxodni o'chirasizmi?\n\n{item.Type} - {item.AmountUZS:N0} UZS",
                    "Tasdiqlash",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    _service.Delete(item.Id, _currentUser);
                    LoadRecentExpenses();
                    MessageBox.Show("Rasxod o'chirildi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Rasxodni o'chirishda xato: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            panel.Controls.Add(lblType);
            panel.Controls.Add(lblDate);
            panel.Controls.Add(lblDesc);
            panel.Controls.Add(divider);
            panel.Controls.Add(lblAmount);
            panel.Controls.Add(btnEdit);
            panel.Controls.Add(btnDelete);

            panel.Resize += (s, e) =>
            {
                lblDesc.Width = panel.Width - 32;
                divider.Width = panel.Width - 32;
                int right = panel.Width - 16;
                btnDelete.SetBounds(right - 98, 12, 98, 30);
                btnEdit.SetBounds(btnDelete.Left - 8 - 98, 12, 98, 30);
                int dateLeft = btnEdit.Left - 8 - lblDate.Width;
                lblDate.Left = Math.Max(16, dateLeft);
                int typeWidth = Math.Max(120, lblDate.Left - 10 - 16);
                lblType.SetBounds(16, 18, typeWidth, 30);
                lblAmount.Left = panel.Width - lblAmount.Width - 16;
            };
            {
                int right = panel.Width - 16;
                btnDelete.SetBounds(right - 98, 12, 98, 30);
                btnEdit.SetBounds(btnDelete.Left - 8 - 98, 12, 98, 30);
                int dateLeft = btnEdit.Left - 8 - lblDate.Width;
                lblDate.Left = Math.Max(16, dateLeft);
                int typeWidth = Math.Max(120, lblDate.Left - 10 - 16);
                lblType.SetBounds(16, 18, typeWidth, 30);
            }
            lblAmount.Left = panel.Width - lblAmount.Width - 16;

            return panel;
        }

        private void ArrangeLayout(Panel canvas)
        {
            int targetWidth = Math.Min(1080, canvas.ClientSize.Width - 72);
            int x = (canvas.ClientSize.Width - targetWidth) / 2;
            int y = 18;
            int h = Math.Max(620, canvas.ClientSize.Height - 34);
            _content.SetBounds(Math.Max(12, x), y, targetWidth, h);
            _header.SetBounds(0, 0, _content.Width, 96);
            _main.SetBounds(0, 96, _content.Width, _content.Height - 96);
        }

        private void ResizeRecentCards()
        {
            int width = Math.Max(380, _recentList.ClientSize.Width - 4);
            foreach (Control c in _recentList.Controls)
            {
                if (c is Panel p)
                {
                    p.Width = width;
                }
            }
        }

        private static void StyleInputBox(TextBox txt, string placeholder)
        {
            txt.BorderStyle = BorderStyle.None;
            txt.BackColor = Color.White;
            txt.ForeColor = Color.FromArgb(47, 61, 84);
            txt.Font = new Font("Bahnschrift", 13, FontStyle.Regular);
            txt.PlaceholderText = placeholder;
            txt.Left = 12;
            txt.Top = 15;
            txt.Width = 300;
        }

        private static Label NewCaption(string text, int left, int top)
        {
            return new Label
            {
                Text = text,
                Left = left,
                Top = top,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 75, 96)
            };
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
                    Left = 20,
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
                    Left = 20,
                    Top = 18,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
            }

            Label title = new Label
            {
                Text = text,
                AutoSize = true,
                Left = 54,
                Top = 16,
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
                using Pen border = new Pen(Color.FromArgb(15, 156, 79), 3.2f)
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

        private void ShowDatePopup(Control anchor)
        {
            if (_datePopup != null)
            {
                _datePopup.Close();
                _datePopup.Dispose();
                _datePopup = null;
            }

            MonthCalendar calendar = new MonthCalendar
            {
                MaxSelectionCount = 1,
                ShowToday = true,
                ShowWeekNumbers = false
            };
            calendar.SetDate(_dtDate.Value.Date);
            calendar.DateSelected += (s, e) =>
            {
                _dtDate.Value = e.Start.Date;
                _txtDateDisplay.Text = _dtDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                _datePopup?.Close();
            };

            ToolStripControlHost host = new ToolStripControlHost(calendar)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = calendar.Size
            };

            _datePopup = new ToolStripDropDown
            {
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                AutoClose = true
            };
            _datePopup.Items.Add(host);

            Point screen = anchor.PointToScreen(new Point(0, anchor.Height + 2));
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int popupW = calendar.Width;
            int popupH = calendar.Height;
            int x = Math.Max(wa.Left + 4, Math.Min(screen.X, wa.Right - popupW - 4));
            int y = Math.Max(wa.Top + 4, Math.Min(screen.Y, wa.Bottom - popupH - 4));

            _datePopup.Show(new Point(x, y));
        }

        private static Button NewActionButton(string text, Color c1, Color c2)
        {
            Button b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            b.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using LinearGradientBrush brush = new LinearGradientBrush(b.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(b.ClientRectangle, 10);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, b.Text, b.Font, b.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            b.Resize += (s, e) => b.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, b.Width), Math.Max(1, b.Height)), 10));
            return b;
        }

        private static Button NewMiniButton(string text, Color c1, Color c2)
        {
            Button b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font("Bahnschrift SemiBold", 10.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            b.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using LinearGradientBrush brush = new LinearGradientBrush(b.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(b.ClientRectangle, 8);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, b.Text, b.Font, b.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            b.Resize += (s, e) => b.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, b.Width), Math.Max(1, b.Height)), 8));
            return b;
        }

        private bool ShowExpenseEditDialog(Expense source, out Expense? updated)
        {
            updated = null;
            using Form dialog = new Form
            {
                Text = "Rasxodni tahrirlash",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(520, 300),
                BackColor = Color.FromArgb(245, 248, 252),
                Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular)
            };

            Label lblDate = new Label { Text = "Sana", Left = 18, Top = 18, Width = 120 };
            DateTimePicker dtDate = new DateTimePicker
            {
                Left = 18,
                Top = 40,
                Width = 220,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy HH:mm",
                Value = source.Date
            };

            Label lblType = new Label { Text = "Turi", Left = 18, Top = 78, Width = 120 };
            TextBox txtType = new TextBox { Left = 18, Top = 100, Width = 484, Text = source.Type };

            Label lblDesc = new Label { Text = "Izoh", Left = 18, Top = 138, Width = 120 };
            TextBox txtDesc = new TextBox { Left = 18, Top = 160, Width = 484, Text = source.Description };

            Label lblAmount = new Label { Text = "Summa (UZS)", Left = 18, Top = 198, Width = 140 };
            TextBox txtAmount = new TextBox
            {
                Left = 18,
                Top = 220,
                Width = 220,
                Text = source.AmountUZS.ToString(CultureInfo.InvariantCulture)
            };

            Button btnOk = new Button
            {
                Text = "Saqlash",
                Left = 296,
                Top = 252,
                Width = 100,
                Height = 32,
                DialogResult = DialogResult.OK
            };
            Button btnCancel = new Button
            {
                Text = "Bekor",
                Left = 402,
                Top = 252,
                Width = 100,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            dialog.Controls.Add(lblDate);
            dialog.Controls.Add(dtDate);
            dialog.Controls.Add(lblType);
            dialog.Controls.Add(txtType);
            dialog.Controls.Add(lblDesc);
            dialog.Controls.Add(txtDesc);
            dialog.Controls.Add(lblAmount);
            dialog.Controls.Add(txtAmount);
            dialog.Controls.Add(btnOk);
            dialog.Controls.Add(btnCancel);
            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancel;

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            string type = txtType.Text.Trim();
            if (string.IsNullOrWhiteSpace(type))
            {
                MessageBox.Show("Rasxod turi bo'sh bo'lmasligi kerak.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!double.TryParse(txtAmount.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double amount) &&
                !double.TryParse(txtAmount.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
            {
                MessageBox.Show("Summa noto'g'ri formatda.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (amount <= 0)
            {
                MessageBox.Show("Summa musbat son bo'lishi kerak.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            updated = new Expense
            {
                Id = source.Id,
                Date = dtDate.Value,
                Type = type,
                Description = txtDesc.Text.Trim(),
                AmountUZS = amount
            };

            return true;
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



