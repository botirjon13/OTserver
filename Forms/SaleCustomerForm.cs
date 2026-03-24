using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Forms
{
    public class SaleCustomerForm : Form
    {
        private readonly bool _customerRequired;
        private readonly bool _debtMode;

        private readonly Panel _root = new Panel();
        private readonly Panel _header = new Panel();
        private readonly Panel _body = new Panel();
        private readonly Panel _group = new Panel();
        private readonly Panel _paymentGroup = new Panel();

        private readonly Button _btnClose = new Button();
        private readonly Button _btnOk = new Button();
        private readonly Button _btnCancel = new Button();

        private readonly ComboBox _cmbExisting = new ComboBox();
        private readonly TextBox _txtName = new TextBox();
        private readonly TextBox _txtPhone = new TextBox();
        private readonly TextBox _txtNote = new TextBox();
        private readonly DateTimePicker _dueDate = new DateTimePicker();
        private readonly TextBox _txtInitialPayment = new TextBox();

        private readonly Panel _existingWrap = new Panel();
        private readonly Panel _nameWrap = new Panel();
        private readonly Panel _phoneWrap = new Panel();
        private readonly Panel _noteWrap = new Panel();
        private readonly Panel _initialWrap = new Panel();
        private readonly Panel _dueWrap = new Panel();

        private bool _okHover;
        private bool _cancelHover;
        private bool _closeHover;

        public int? SelectedCustomerId { get; private set; }
        public string NewCustomerName => _txtName.Text.Trim();
        public string NewCustomerPhone => _txtPhone.Text.Trim();
        public string NewCustomerNote => _txtNote.Text.Trim();
        public DateTime DueDate => _dueDate.Value.Date;
        public double InitialPaymentUZS { get; private set; }

        public SaleCustomerForm(List<Customer> customers, bool customerRequired, bool debtMode)
        {
            _customerRequired = customerRequired;
            _debtMode = debtMode;
            InitializeComponent(customers);
        }

        private void InitializeComponent(List<Customer> customers)
        {
            Text = _debtMode ? "Nasiya uchun mijoz" : "Mijoz tanlash (ixtiyoriy)";
            ClientSize = new Size(_debtMode ? 690 : 526, _debtMode ? 760 : 690);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(238, 240, 246);
            MinimizeBox = false;
            MaximizeBox = false;
            KeyPreview = true;
            DoubleBuffered = true;

            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            Resize += (_, __) =>
            {
                Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 18));
                ReflowLayout();
            };

            _root.Dock = DockStyle.Fill;
            _root.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, _root.Width - 1, _root.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 18);
                using SolidBrush fill = new SolidBrush(Color.FromArgb(245, 247, 251));
                using Pen border = new Pen(Color.FromArgb(188, 199, 220), 1.3f);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };

            BuildHeader();
            BuildBody(customers);

            _root.Controls.Add(_body);
            _root.Controls.Add(_header);
            Controls.Add(_root);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Shown += (_, __) =>
            {
                Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 18));
                ReflowLayout();
            };
        }

        private void BuildHeader()
        {
            _header.Dock = DockStyle.Top;
            _header.Height = 60;
            _header.Paint += (_, e) =>
            {
                using LinearGradientBrush brush = new LinearGradientBrush(
                    _header.ClientRectangle,
                    Color.FromArgb(35, 98, 238),
                    Color.FromArgb(80, 58, 228),
                    0f);
                e.Graphics.FillRectangle(brush, _header.ClientRectangle);
            };

            Label lblTitle = new Label
            {
                Text = _debtMode ? "Nasiya uchun mijoz" : "Mijoz tanlash (ixtiyoriy)",
                Left = 24,
                Top = 15,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 17f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            _btnClose.SetBounds(478, 12, 34, 34);
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnClose.BackColor = Color.Transparent;
            _btnClose.Cursor = Cursors.Hand;
            _btnClose.Text = string.Empty;
            _btnClose.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, _btnClose.Width - 3), Math.Max(1, _btnClose.Height - 3));
                int alpha = _closeHover ? 52 : 26;
                using GraphicsPath path = RoundedRect(rect, 10);
                using SolidBrush fill = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
                e.Graphics.FillPath(fill, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    "X",
                    new Font("Bahnschrift SemiBold", 12f, FontStyle.Bold),
                    _btnClose.ClientRectangle,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _btnClose.MouseEnter += (_, __) => { _closeHover = true; _btnClose.Invalidate(); };
            _btnClose.MouseLeave += (_, __) => { _closeHover = false; _btnClose.Invalidate(); };
            _btnClose.Click += (_, __) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _header.Resize += (_, __) => _btnClose.Left = _header.Width - _btnClose.Width - 12;

            _header.Controls.Add(lblTitle);
            _header.Controls.Add(_btnClose);
        }

        private void BuildBody(List<Customer> customers)
        {
            _body.Dock = DockStyle.Fill;
            _body.BackColor = Color.Transparent;
            _body.Padding = new Padding(24, 18, 24, 20);

            int contentWidth = _debtMode ? 624 : 478;
            int left = 24;

            Label lblExisting = new Label
            {
                Text = "Mavjud mijoz:",
                Left = left,
                Top = 10,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 53, 86),
                BackColor = Color.Transparent
            };

            PrepareInputWrap(_existingWrap, left, 40, contentWidth, 52, 16);
            ConfigureComboBox(_cmbExisting, customers);
            _existingWrap.Controls.Add(_cmbExisting);

            _group.SetBounds(left, 114, contentWidth, _debtMode ? 252 : 398);
            _group.BackColor = Color.Transparent;
            _group.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, _group.Width - 1, _group.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 14);
                Color fill = _debtMode ? Color.FromArgb(247, 244, 234) : Color.FromArgb(235, 240, 247);
                Color border = _debtMode ? Color.FromArgb(236, 201, 91) : Color.FromArgb(201, 212, 229);
                using SolidBrush fillBrush = new SolidBrush(fill);
                using Pen borderPen = new Pen(border, _debtMode ? 1.4f : 1f);
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(borderPen, path);
            };

            Label lblGroupIcon = new Label
            {
                Text = "\uE77B",
                Left = 18,
                Top = 18,
                Width = 22,
                Height = 22,
                Font = new Font("Segoe MDL2 Assets", 14f, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 126, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            Label lblGroupTitle = new Label
            {
                Text = "Yoki yangi mijoz kiriting:",
                Left = 44,
                Top = 18,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 17f, FontStyle.Bold),
                ForeColor = Color.FromArgb(13, 36, 72),
                BackColor = Color.Transparent
            };

            Label lblName = CreateFieldLabel("F.I.Sh:", 18, 62);
            PrepareInputWrap(_nameWrap, 18, 86, _group.Width - 36, 52, 14);
            PrepareTextBox(_txtName, "To'liq ism kiriting", false);
            _txtName.SetBounds(14, 14, _nameWrap.Width - 28, 24);
            _nameWrap.Controls.Add(_txtName);

            _group.Controls.Add(lblGroupIcon);
            _group.Controls.Add(lblGroupTitle);
            _group.Controls.Add(lblName);
            _group.Controls.Add(_nameWrap);

            if (_debtMode)
            {
                int rowY = 176;
                int rowWidth = (_group.Width - 48) / 2;

                Label lblPhoneIcon = new Label
                {
                    Text = "\uE717",
                    Left = 18,
                    Top = 152,
                    Width = 18,
                    Height = 20,
                    Font = new Font("Segoe MDL2 Assets", 12f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(47, 63, 92),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                Label lblPhone = CreateFieldLabel("Telefon:", 40, 152);

                Label lblNoteIcon = new Label
                {
                    Text = "\uE70B",
                    Left = 18 + rowWidth + 12,
                    Top = 152,
                    Width = 18,
                    Height = 20,
                    Font = new Font("Segoe MDL2 Assets", 12f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(47, 63, 92),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                Label lblNote = CreateFieldLabel("Izoh:", 18 + rowWidth + 34, 152);

                PrepareInputWrap(_phoneWrap, 18, rowY, rowWidth, 52, 14);
                PrepareTextBox(_txtPhone, "+998 90 123 45 67", false);
                _txtPhone.SetBounds(14, 14, _phoneWrap.Width - 28, 24);
                _phoneWrap.Controls.Add(_txtPhone);

                PrepareInputWrap(_noteWrap, 18 + rowWidth + 12, rowY, rowWidth, 52, 14);
                PrepareTextBox(_txtNote, "Qo'shimcha ma'lumot", false);
                _txtNote.SetBounds(14, 14, _noteWrap.Width - 28, 24);
                _noteWrap.Controls.Add(_txtNote);

                _group.Controls.Add(lblPhoneIcon);
                _group.Controls.Add(lblPhone);
                _group.Controls.Add(_phoneWrap);
                _group.Controls.Add(lblNoteIcon);
                _group.Controls.Add(lblNote);
                _group.Controls.Add(_noteWrap);

                _paymentGroup.SetBounds(left, _group.Bottom + 24, contentWidth, 156);
                _paymentGroup.BackColor = Color.Transparent;
                _paymentGroup.Paint += (_, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle rect = new Rectangle(0, 0, _paymentGroup.Width - 1, _paymentGroup.Height - 1);
                    using GraphicsPath path = RoundedRect(rect, 14);
                    using SolidBrush fillBrush = new SolidBrush(Color.FromArgb(235, 241, 251));
                    using Pen borderPen = new Pen(Color.FromArgb(154, 194, 246), 1.4f);
                    e.Graphics.FillPath(fillBrush, path);
                    e.Graphics.DrawPath(borderPen, path);
                };

                Label lblPaymentIcon = new Label
                {
                    Text = "\uEAFD",
                    Left = 18,
                    Top = 18,
                    Width = 22,
                    Height = 22,
                    Font = new Font("Segoe MDL2 Assets", 14f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(45, 109, 222),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };

                Label lblPaymentTitle = new Label
                {
                    Text = "To'lov ma'lumotlari:",
                    Left = 44,
                    Top = 18,
                    AutoSize = true,
                    Font = new Font("Bahnschrift SemiBold", 17f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(13, 36, 72),
                    BackColor = Color.Transparent
                };

                Label lblInitial = CreateFieldLabel("Boshlang'ich to'lov:", 18, 62);
                Label lblDueIcon = new Label
                {
                    Text = "\uE787",
                    Left = 18 + rowWidth + 12,
                    Top = 62,
                    Width = 18,
                    Height = 20,
                    Font = new Font("Segoe MDL2 Assets", 12f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(47, 63, 92),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                Label lblDue = CreateFieldLabel("Muddat:", 18 + rowWidth + 34, 62);

                PrepareInputWrap(_initialWrap, 18, 86, rowWidth, 52, 14);
                PrepareTextBox(_txtInitialPayment, "0", false);
                _txtInitialPayment.SetBounds(14, 14, _initialWrap.Width - 28, 24);
                _txtInitialPayment.Text = "0";
                _initialWrap.Controls.Add(_txtInitialPayment);

                PrepareInputWrap(_dueWrap, 18 + rowWidth + 12, 86, rowWidth, 52, 14);
                _dueDate.Format = DateTimePickerFormat.Custom;
                _dueDate.CustomFormat = "dd.MM.yyyy";
                _dueDate.Value = DateTime.Today.AddDays(7);
                _dueDate.MinDate = DateTime.Today;
                _dueDate.Font = new Font("Segoe UI", 11f, FontStyle.Regular);
                _dueDate.CalendarForeColor = Color.FromArgb(28, 45, 74);
                _dueDate.CalendarMonthBackground = Color.White;
                _dueDate.CalendarTitleBackColor = Color.FromArgb(54, 103, 237);
                _dueDate.CalendarTitleForeColor = Color.White;
                _dueDate.CalendarTrailingForeColor = Color.FromArgb(151, 165, 186);
                _dueDate.SetBounds(18, 12, _dueWrap.Width - 36, 28);
                _dueDate.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _dueWrap.Controls.Add(_dueDate);

                _paymentGroup.Controls.Add(lblPaymentIcon);
                _paymentGroup.Controls.Add(lblPaymentTitle);
                _paymentGroup.Controls.Add(lblInitial);
                _paymentGroup.Controls.Add(_initialWrap);
                _paymentGroup.Controls.Add(lblDueIcon);
                _paymentGroup.Controls.Add(lblDue);
                _paymentGroup.Controls.Add(_dueWrap);
            }
            else
            {
                Label lblPhone = CreateFieldLabel("Telefon:", 20, 156);
                PrepareInputWrap(_phoneWrap, 18, 180, 442, 52, 14);
                PrepareTextBox(_txtPhone, "+998 90 123 45 67", false);
                _phoneWrap.Controls.Add(_txtPhone);

                Label lblNote = CreateFieldLabel("Izoh:", 20, 248);
                PrepareInputWrap(_noteWrap, 18, 272, 442, 96, 14);
                PrepareTextBox(_txtNote, "Qo'shimcha ma'lumot...", true);
                _txtNote.Multiline = true;
                _txtNote.ScrollBars = ScrollBars.Vertical;
                _noteWrap.Controls.Add(_txtNote);

                _group.Controls.Add(lblPhone);
                _group.Controls.Add(_phoneWrap);
                _group.Controls.Add(lblNote);
                _group.Controls.Add(_noteWrap);
            }

            _btnOk.SetBounds(left, _debtMode ? 550 : 528, _debtMode ? 304 : 224, 50);
            _btnOk.Text = "Davom etish";
            _btnOk.FlatStyle = FlatStyle.Flat;
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnOk.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnOk.Cursor = Cursors.Hand;
            _btnOk.Font = new Font("Bahnschrift SemiBold", 15f, FontStyle.Bold);
            _btnOk.ForeColor = Color.White;
            _btnOk.Paint += PaintPrimaryButton;
            _btnOk.MouseEnter += (_, __) => { _okHover = true; _btnOk.Invalidate(); };
            _btnOk.MouseLeave += (_, __) => { _okHover = false; _btnOk.Invalidate(); };
            _btnOk.Click += Ok_Click;

            _btnCancel.SetBounds(_debtMode ? (left + 318) : 272, _debtMode ? 550 : 528, _debtMode ? 306 : 230, 50);
            _btnCancel.Text = "Bekor";
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnCancel.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnCancel.Cursor = Cursors.Hand;
            _btnCancel.Font = new Font("Bahnschrift SemiBold", 15f, FontStyle.Bold);
            _btnCancel.ForeColor = Color.FromArgb(50, 74, 109);
            _btnCancel.Paint += PaintNeutralButton;
            _btnCancel.MouseEnter += (_, __) => { _cancelHover = true; _btnCancel.Invalidate(); };
            _btnCancel.MouseLeave += (_, __) => { _cancelHover = false; _btnCancel.Invalidate(); };
            _btnCancel.Click += (_, __) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _body.Controls.Add(lblExisting);
            _body.Controls.Add(_existingWrap);
            _body.Controls.Add(_group);
            if (_debtMode)
            {
                _body.Controls.Add(_paymentGroup);
            }
            _body.Controls.Add(_btnOk);
            _body.Controls.Add(_btnCancel);
        }

        private void ConfigureComboBox(ComboBox combo, List<Customer> customers)
        {
            combo.SetBounds(14, 10, _existingWrap.Width - 28, _existingWrap.Height - 20);
            combo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.Font = new Font("Bahnschrift", 12f, FontStyle.Regular);
            combo.BackColor = Color.White;
            combo.ForeColor = Color.FromArgb(29, 49, 79);
            combo.DisplayMember = "FullName";
            combo.ValueMember = "Id";

            combo.Items.Add("Tanlanmagan");
            foreach (Customer customer in customers)
            {
                combo.Items.Add(customer);
            }

            combo.SelectedIndex = 0;
        }

        private static void PrepareInputWrap(Panel wrap, int left, int top, int width, int height, int radius)
        {
            wrap.SetBounds(left, top, width, height);
            wrap.BackColor = Color.Transparent;
            wrap.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1);
                using GraphicsPath path = RoundedRect(rect, radius);
                using SolidBrush fill = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(179, 194, 216), 2f);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            wrap.Resize += (_, __) =>
                wrap.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, wrap.Width), Math.Max(1, wrap.Height)), radius));
        }

        private static void PrepareTextBox(TextBox box, string placeholder, bool multiline)
        {
            box.BorderStyle = BorderStyle.None;
            box.Font = new Font("Bahnschrift", 12f, FontStyle.Regular);
            box.ForeColor = Color.FromArgb(28, 45, 74);
            box.BackColor = Color.White;
            box.PlaceholderText = placeholder;
            box.Multiline = multiline;
            if (multiline)
            {
                box.SetBounds(14, 12, 412, 72);
            }
            else
            {
                box.SetBounds(14, 14, 412, 24);
            }
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        }

        private static Label CreateFieldLabel(string text, int left, int top)
        {
            return new Label
            {
                Text = text,
                Left = left,
                Top = top,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(47, 63, 92),
                BackColor = Color.Transparent
            };
        }

        private void ReflowLayout()
        {
            int targetWidth = _debtMode ? 624 : 478;
            int left = Math.Max(14, (_body.ClientSize.Width - targetWidth) / 2);

            _existingWrap.Left = left;
            _group.Left = left;
            _btnOk.Left = left;
            _btnCancel.Left = _debtMode ? left + 318 : left + 248;
            if (_debtMode)
            {
                _paymentGroup.Left = left;
            }
        }

        private void PaintPrimaryButton(object? sender, PaintEventArgs e)
        {
            if (sender is not Button btn)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(1, 1, Math.Max(1, btn.Width - 3), Math.Max(1, btn.Height - 3));
            Color c1 = _okHover ? Color.FromArgb(8, 173, 83) : Color.FromArgb(7, 163, 77);
            Color c2 = _okHover ? Color.FromArgb(6, 160, 74) : Color.FromArgb(5, 154, 71);
            using GraphicsPath path = RoundedRect(rect, 14);
            using LinearGradientBrush brush = new LinearGradientBrush(rect, c1, c2, 0f);
            e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, btn.ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void PaintNeutralButton(object? sender, PaintEventArgs e)
        {
            if (sender is not Button btn)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(1, 1, Math.Max(1, btn.Width - 3), Math.Max(1, btn.Height - 3));
            Color fill = _cancelHover ? Color.FromArgb(241, 246, 252) : Color.FromArgb(246, 249, 253);
            Color border = _cancelHover ? Color.FromArgb(141, 167, 210) : Color.FromArgb(182, 198, 223);
            Color text = _cancelHover ? Color.FromArgb(36, 87, 174) : Color.FromArgb(50, 74, 109);
            using GraphicsPath path = RoundedRect(rect, 14);
            using SolidBrush brush = new SolidBrush(fill);
            using Pen pen = new Pen(border, 2f);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
            TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, btn.ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void Ok_Click(object? sender, EventArgs e)
        {
            SelectedCustomerId = null;
            if (_cmbExisting.SelectedItem is Customer customer)
            {
                SelectedCustomerId = customer.Id;
            }

            bool hasNewCustomer = !string.IsNullOrWhiteSpace(NewCustomerName);
            if (_customerRequired && SelectedCustomerId == null && !hasNewCustomer)
            {
                MessageBox.Show("Mijoz tanlash yoki yangi mijoz kiritish majburiy.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_debtMode)
            {
                if (!double.TryParse(_txtInitialPayment.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double initial) &&
                    !double.TryParse(_txtInitialPayment.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out initial))
                {
                    MessageBox.Show("Boshlang'ich to'lov noto'g'ri.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (initial < 0)
                {
                    MessageBox.Show("Boshlang'ich to'lov manfiy bo'lmasligi kerak.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                InitialPaymentUZS = initial;
            }

            DialogResult = DialogResult.OK;
            Close();
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
