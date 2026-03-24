using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SantexnikaSRM.Forms
{
    public class PaymentTypeForm : Form
    {
        private readonly string[] _paymentTypes =
        {
            "Naqd",
            "Karta",
            "Click",
            "Payme",
            "Bank o'tkazma",
            "Nasiya (Qarz)"
        };

        private readonly Panel _root = new Panel();
        private readonly Panel _header = new Panel();
        private readonly Panel _body = new Panel();
        private readonly Button _btnClose = new Button();
        private readonly Label _lblCaption = new Label();

        private readonly Panel _selector = new Panel();
        private readonly Label _lblSelectorValue = new Label();
        private readonly Label _lblSelectorArrow = new Label();

        private readonly Panel _dropdown = new Panel();
        private readonly ListBox _lstOptions = new ListBox();

        private readonly Panel _preview = new Panel();
        private readonly Label _lblPreviewValue = new Label();

        private readonly Button _btnOk = new Button();
        private readonly Button _btnCancel = new Button();
        private readonly System.Windows.Forms.Timer _hoverTimer = new System.Windows.Forms.Timer();

        private const string PlaceholderSelection = "Tanlang";
        private string _selected = PlaceholderSelection;
        private readonly Color _previewFillColor = Color.FromArgb(232, 238, 248);
        private const int ContentWidth = 406;
        private int _contentLeft = 10;
        private int _okHoverProgress;
        private int _cancelHoverProgress;
        private int _closeHoverProgress;
        private bool _okHoverTarget;
        private bool _cancelHoverTarget;
        private bool _closeHoverTarget;

        public string SelectedPaymentType => _selected;

        public PaymentTypeForm()
        {
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            ApplySelection(PlaceholderSelection);
        }

        private void InitializeComponent()
        {
            Text = "To'lov turi";
            ClientSize = new Size(474, 390);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(236, 240, 249);
            KeyPreview = true;
            MinimizeBox = false;
            MaximizeBox = false;
            DoubleBuffered = true;
            Padding = Padding.Empty;

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            Resize += (s, e) =>
            {
                Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 18));
                ReflowLayout();
            };

            _root.Dock = DockStyle.Fill;
            _root.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle outer = new Rectangle(0, 0, _root.Width - 1, _root.Height - 1);
                Rectangle inner = new Rectangle(1, 1, Math.Max(1, _root.Width - 3), Math.Max(1, _root.Height - 3));
                using SolidBrush fill = new SolidBrush(Color.FromArgb(245, 248, 253));
                using Pen border = new Pen(Color.FromArgb(170, 188, 218), 1.6f);
                using Pen glow = new Pen(Color.FromArgb(65, 102, 149, 226), 1f);
                using GraphicsPath path = RoundedRect(outer, 18);
                using GraphicsPath innerPath = RoundedRect(inner, 17);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(glow, path);
                e.Graphics.DrawPath(border, innerPath);
            };
            _root.Resize += (s, e) =>
                _root.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _root.Width), Math.Max(1, _root.Height)), 18));

            _header.Dock = DockStyle.Top;
            _header.Height = 64;
            _header.Paint += (s, e) =>
            {
                using LinearGradientBrush brush = new LinearGradientBrush(
                    _header.ClientRectangle,
                    Color.FromArgb(36, 103, 236),
                    Color.FromArgb(82, 63, 228),
                    0f);
                e.Graphics.FillRectangle(brush, _header.ClientRectangle);
            };

            Label lblTitle = new Label
            {
                Text = "To'lov turi",
                Left = 22,
                Top = 18,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 17, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            _btnClose.Text = string.Empty;
            _btnClose.SetBounds(420, 14, 34, 34);
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnClose.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnClose.Font = new Font("Bahnschrift", 18, FontStyle.Regular);
            _btnClose.ForeColor = Color.White;
            _btnClose.BackColor = Color.Transparent;
            _btnClose.UseVisualStyleBackColor = false;
            _btnClose.Cursor = Cursors.Hand;
            _btnClose.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, _btnClose.Width - 3), Math.Max(1, _btnClose.Height - 3));
                int fillAlpha = 20 + (_closeHoverProgress * 55 / 100);
                int borderAlpha = 40 + (_closeHoverProgress * 80 / 100);
                using GraphicsPath path = RoundedRect(rect, 10);
                using SolidBrush fill = new SolidBrush(Color.FromArgb(fillAlpha, 255, 255, 255));
                using Pen border = new Pen(Color.FromArgb(borderAlpha, 255, 255, 255), 1.2f);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    "\uE711",
                    new Font("Segoe MDL2 Assets", 12, FontStyle.Regular),
                    _btnClose.ClientRectangle,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _btnClose.MouseEnter += (s, e) => SetHoverTarget(_btnClose, true);
            _btnClose.MouseLeave += (s, e) => SetHoverTarget(_btnClose, false);
            _btnClose.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            _header.Resize += (s, e) => _btnClose.Left = _header.Width - _btnClose.Width - 10;

            _header.Controls.Add(lblTitle);
            _header.Controls.Add(_btnClose);

            _body.Dock = DockStyle.Fill;
            _body.Padding = new Padding(24, 14, 24, 20);
            _body.BackColor = Color.Transparent;

            _lblCaption.Text = "To'lov turini tanlang:";
            _lblCaption.Left = _contentLeft;
            _lblCaption.Top = 8;
            _lblCaption.AutoSize = true;
            _lblCaption.Font = new Font("Bahnschrift SemiBold", 11.5f, FontStyle.Bold);
            _lblCaption.ForeColor = Color.FromArgb(45, 63, 94);

            _selector.SetBounds(_contentLeft, 38, ContentWidth, 56);
            _selector.BackColor = Color.White;
            _selector.Cursor = Cursors.Hand;
            _selector.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, _selector.Width - 3), Math.Max(1, _selector.Height - 3));
                using Pen border = new Pen(Color.FromArgb(48, 124, 246), 2f);
                using GraphicsPath path = RoundedRect(rect, 16);
                using SolidBrush fill = new SolidBrush(Color.White);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            _selector.Resize += (s, e) =>
                _selector.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _selector.Width - 1), Math.Max(1, _selector.Height - 1)), 16));

            _lblSelectorValue.SetBounds(18, 13, 340, 30);
            _lblSelectorValue.Font = new Font("Bahnschrift", 18, FontStyle.Regular);
            _lblSelectorValue.ForeColor = Color.FromArgb(26, 47, 80);
            _lblSelectorValue.BackColor = Color.Transparent;

            _lblSelectorArrow.Text = "\uE70D";
            _lblSelectorArrow.Font = new Font("Segoe MDL2 Assets", 13, FontStyle.Regular);
            _lblSelectorArrow.ForeColor = Color.FromArgb(118, 139, 172);
            _lblSelectorArrow.TextAlign = ContentAlignment.MiddleCenter;
            _lblSelectorArrow.SetBounds(_selector.Width - 40, 16, 24, 24);
            _lblSelectorArrow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblSelectorArrow.BackColor = Color.Transparent;
            _lblSelectorArrow.Cursor = Cursors.Hand;

            _selector.Controls.Add(_lblSelectorValue);
            _selector.Controls.Add(_lblSelectorArrow);
            _selector.Click += ToggleDropdown_Click;
            _lblSelectorValue.Click += ToggleDropdown_Click;
            _lblSelectorArrow.Click += ToggleDropdown_Click;

            _dropdown.SetBounds(_contentLeft + 8, 96, ContentWidth - 16, 140);
            _dropdown.Visible = false;
            _dropdown.BackColor = Color.White;
            _dropdown.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, _dropdown.Width - 3), Math.Max(1, _dropdown.Height - 3));
                using SolidBrush fill = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(191, 205, 228), 1);
                using GraphicsPath path = RoundedRect(rect, 12);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            _dropdown.Resize += (s, e) =>
                _dropdown.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _dropdown.Width - 1), Math.Max(1, _dropdown.Height - 1)), 12));

            _lstOptions.Dock = DockStyle.Fill;
            _lstOptions.BorderStyle = BorderStyle.None;
            _lstOptions.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            _lstOptions.BackColor = Color.White;
            _lstOptions.ForeColor = Color.FromArgb(35, 54, 83);
            _lstOptions.ItemHeight = 30;
            _lstOptions.Items.AddRange(_paymentTypes);
            _lstOptions.Click += (s, e) =>
            {
                if (_lstOptions.SelectedItem is string value)
                {
                    ApplySelection(value);
                    HideDropdown();
                }
            };
            _dropdown.Controls.Add(_lstOptions);

            _preview.SetBounds(_contentLeft, 110, ContentWidth, 78);
            _preview.BackColor = Color.Transparent;
            _preview.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, _preview.Width - 3), Math.Max(1, _preview.Height - 3));
                using SolidBrush fill = new SolidBrush(_previewFillColor);
                using Pen border = new Pen(Color.FromArgb(183, 201, 230), 1);
                using GraphicsPath path = RoundedRect(rect, 14);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            _preview.Resize += (s, e) =>
                _preview.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _preview.Width - 1), Math.Max(1, _preview.Height - 1)), 14));

            Panel iconBox = new Panel
            {
                Left = 16,
                Top = 21,
                Width = 36,
                Height = 36,
                BackColor = Color.FromArgb(64, 77, 236)
            };
            iconBox.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(iconBox.BackColor);
                using GraphicsPath path = RoundedRect(iconBox.ClientRectangle, 10);
                e.Graphics.FillPath(fill, path);
            };
            iconBox.Resize += (s, e) =>
                iconBox.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, iconBox.Width), Math.Max(1, iconBox.Height)), 10));

            Label icon = new Label
            {
                Dock = DockStyle.Fill,
                Text = "\uE8C7",
                Font = new Font("Segoe MDL2 Assets", 12, FontStyle.Regular),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            iconBox.Controls.Add(icon);

            Label lblPreviewTitle = new Label
            {
                Text = "Tanlangan to'lov turi:",
                Left = 66,
                Top = 16,
                AutoSize = true,
                Font = new Font("Bahnschrift", 11.2f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 89, 119),
                BackColor = Color.Transparent
            };

            _lblPreviewValue.Left = 66;
            _lblPreviewValue.Top = 37;
            _lblPreviewValue.AutoSize = true;
            _lblPreviewValue.Font = new Font("Bahnschrift SemiBold", 14.5f, FontStyle.Bold);
            _lblPreviewValue.ForeColor = Color.FromArgb(24, 43, 76);
            _lblPreviewValue.BackColor = Color.Transparent;

            _preview.Controls.Add(iconBox);
            _preview.Controls.Add(lblPreviewTitle);
            _preview.Controls.Add(_lblPreviewValue);

            _btnOk.SetBounds(_contentLeft, 220, 180, 50);
            _btnOk.Text = "OK";
            _btnOk.FlatStyle = FlatStyle.Flat;
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnOk.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnOk.UseVisualStyleBackColor = false;
            _btnOk.Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold);
            _btnOk.ForeColor = Color.White;
            _btnOk.Cursor = Cursors.Hand;
            _btnOk.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, _btnOk.Width - 3), Math.Max(1, _btnOk.Height - 3));
                float t = _okHoverProgress / 100f;
                Color okStart = Blend(Color.FromArgb(31, 92, 234), Color.FromArgb(53, 118, 247), t);
                Color okEnd = Blend(Color.FromArgb(82, 60, 230), Color.FromArgb(105, 82, 247), t);
                using LinearGradientBrush brush = new LinearGradientBrush(rect, okStart, okEnd, 0f);
                using GraphicsPath path = RoundedRect(rect, 14);
                e.Graphics.FillPath(brush, path);
                using Pen border = new Pen(Color.FromArgb(25 + (_okHoverProgress * 30 / 100), 255, 255, 255), 1f);
                e.Graphics.DrawPath(border, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    _btnOk.Text,
                    _btnOk.Font,
                    _btnOk.ClientRectangle,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _btnOk.Resize += (s, e) =>
                _btnOk.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _btnOk.Width - 1), Math.Max(1, _btnOk.Height - 1)), 14));
            _btnOk.MouseEnter += (s, e) => SetHoverTarget(_btnOk, true);
            _btnOk.MouseLeave += (s, e) => SetHoverTarget(_btnOk, false);
            _btnOk.Click += (s, e) =>
            {
                if (_selected == PlaceholderSelection)
                {
                    MessageBox.Show("To'lov turini tanlang.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };

            _btnCancel.SetBounds(_contentLeft + 194, 220, 212, 50);
            _btnCancel.Text = "Bekor qilish";
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnCancel.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnCancel.UseVisualStyleBackColor = false;
            _btnCancel.Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold);
            _btnCancel.ForeColor = Color.FromArgb(50, 74, 109);
            _btnCancel.BackColor = Color.FromArgb(246, 249, 253);
            _btnCancel.Cursor = Cursors.Hand;
            _btnCancel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, _btnCancel.Width - 3), Math.Max(1, _btnCancel.Height - 3));
                float t = _cancelHoverProgress / 100f;
                Color fillColor = Blend(Color.FromArgb(246, 249, 253), Color.FromArgb(239, 245, 253), t);
                Color borderColor = Blend(Color.FromArgb(182, 198, 223), Color.FromArgb(127, 161, 220), t);
                Color textColor = Blend(Color.FromArgb(50, 74, 109), Color.FromArgb(36, 87, 174), t);
                using SolidBrush fill = new SolidBrush(fillColor);
                using Pen border = new Pen(borderColor, 2f);
                using GraphicsPath path = RoundedRect(rect, 14);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    _btnCancel.Text,
                    _btnCancel.Font,
                    _btnCancel.ClientRectangle,
                    textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _btnCancel.Resize += (s, e) =>
                _btnCancel.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _btnCancel.Width - 1), Math.Max(1, _btnCancel.Height - 1)), 14));
            _btnCancel.MouseEnter += (s, e) => SetHoverTarget(_btnCancel, true);
            _btnCancel.MouseLeave += (s, e) => SetHoverTarget(_btnCancel, false);
            _btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _body.Controls.Add(_lblCaption);
            _body.Controls.Add(_selector);
            _body.Controls.Add(_preview);
            _body.Controls.Add(_btnOk);
            _body.Controls.Add(_btnCancel);
            _body.Controls.Add(_dropdown);

            _root.Controls.Add(_body);
            _root.Controls.Add(_header);
            Controls.Add(_root);

            _hoverTimer.Interval = 15;
            _hoverTimer.Tick += HoverTimer_Tick;

            Deactivate += (s, e) => HideDropdown();
            MouseDown += (s, e) => HideDropdown();
            Shown += (s, e) =>
            {
                Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 18));
                ReflowLayout();
            };
        }

        private void ToggleDropdown_Click(object? sender, EventArgs e)
        {
            _dropdown.Visible = !_dropdown.Visible;
            if (_dropdown.Visible)
            {
                _lstOptions.SelectedItem = _selected;
                _dropdown.BringToFront();
            }
        }

        private void HideDropdown()
        {
            _dropdown.Visible = false;
        }

        private void ApplySelection(string value)
        {
            _selected = value;
            _lblSelectorValue.Text = value;
            _lblPreviewValue.Text = value == PlaceholderSelection ? "-" : value;
        }

        private void ReflowLayout()
        {
            if (_body.Width <= 0)
            {
                return;
            }

            _contentLeft = Math.Max(6, (_body.ClientSize.Width - ContentWidth) / 2);
            _lblCaption.Left = _contentLeft;
            _selector.Left = _contentLeft;
            _preview.Left = _contentLeft;
            _btnOk.Left = _contentLeft;
            _btnCancel.Left = _contentLeft + 194;
            _dropdown.Left = _contentLeft + 8;
        }

        private void SetHoverTarget(Control control, bool hover)
        {
            if (ReferenceEquals(control, _btnOk))
            {
                _okHoverTarget = hover;
            }
            else if (ReferenceEquals(control, _btnCancel))
            {
                _cancelHoverTarget = hover;
            }
            else if (ReferenceEquals(control, _btnClose))
            {
                _closeHoverTarget = hover;
            }

            if (!_hoverTimer.Enabled)
            {
                _hoverTimer.Start();
            }
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            bool changed = false;
            changed |= AnimateHover(ref _okHoverProgress, _okHoverTarget);
            changed |= AnimateHover(ref _cancelHoverProgress, _cancelHoverTarget);
            changed |= AnimateHover(ref _closeHoverProgress, _closeHoverTarget);

            if (changed)
            {
                _btnOk.Invalidate();
                _btnCancel.Invalidate();
                _btnClose.Invalidate();
            }

            if (!NeedsHoverAnimation())
            {
                _hoverTimer.Stop();
            }
        }

        private bool NeedsHoverAnimation()
        {
            return (_okHoverTarget && _okHoverProgress < 100) || (!_okHoverTarget && _okHoverProgress > 0) ||
                   (_cancelHoverTarget && _cancelHoverProgress < 100) || (!_cancelHoverTarget && _cancelHoverProgress > 0) ||
                   (_closeHoverTarget && _closeHoverProgress < 100) || (!_closeHoverTarget && _closeHoverProgress > 0);
        }

        private static bool AnimateHover(ref int value, bool target)
        {
            int next = target ? Math.Min(100, value + 20) : Math.Max(0, value - 20);
            if (next == value)
            {
                return false;
            }

            value = next;
            return true;
        }

        private static Color Blend(Color from, Color to, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            int r = from.R + (int)((to.R - from.R) * t);
            int g = from.G + (int)((to.G - from.G) * t);
            int b = from.B + (int)((to.B - from.B) * t);
            int a = from.A + (int)((to.A - from.A) * t);
            return Color.FromArgb(a, r, g, b);
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




