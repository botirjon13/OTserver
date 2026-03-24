using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class LoginForm : Form
    {
        private readonly DatabaseHelper dbHelper = new DatabaseHelper();
        private readonly ActivationService activationService = new ActivationService();
        private readonly TextBox txtUsername = new TextBox();
        private readonly TextBox txtPassword = new TextBox();
        private readonly Panel card = new Panel();
        private readonly Panel usernameRow = new Panel();
        private readonly Panel passwordRow = new Panel();
        private readonly Button btnTheme = new Button();
        private readonly Button btnLogin = new Button();
        private readonly Button btnPasswordToggle = new Button();
        private readonly System.Windows.Forms.Timer loadingTimer = new System.Windows.Forms.Timer();
        private readonly Label lblTitle = new Label();
        private readonly Label lblSubtitle = new Label();
        private readonly Label lblLogin = new Label();
        private readonly Label lblPassword = new Label();
        private readonly LinkLabel lnkForgot = new LinkLabel();
        private Control _logoControl = new Panel();
        private Size _logoSize = new Size(74, 74);

        private bool _showPassword;
        private bool _isLoading;
        private int _loadingFrame;
        private bool _loginHover;
        private bool _loginPressed;
        private Color _cardFill = Color.FromArgb(20, 30, 44);
        private Color _inputRowBg = Color.FromArgb(31, 43, 60);
        private Color _inputRowBorder = Color.FromArgb(0, 235, 140);
        private readonly Dictionary<TextBox, string> _placeholders = new Dictionary<TextBox, string>();

        public LoginForm()
        {
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            Icon = BrandingAssets.TryLoadAppIcon() ?? Icon;
        }

        private void InitializeComponent()
        {
            Text = "Osontrack SRM tizimi";
            Size = new Size(980, 780);
            MinimumSize = new Size(420, 600);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor = UiTheme.Background;
            Font = UiTheme.BodyFont;
            DoubleBuffered = true;

            Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.White);
            };

            btnTheme.Size = new Size(96, 36);
            btnTheme.FlatStyle = FlatStyle.Flat;
            btnTheme.FlatAppearance.BorderSize = 1;
            btnTheme.Cursor = Cursors.Hand;
            btnTheme.Font = new Font("Bahnschrift SemiBold", 10, FontStyle.Bold);
            btnTheme.Click += (s, e) =>
            {
                UiTheme.ToggleTheme();
                ApplyTheme();
                Invalidate();
            };

            card.Size = new Size(470, 640);
            card.Padding = new Padding(34, 28, 34, 30);
            card.Paint += Card_Paint;

            lblTitle.Text = "Tizimga kirish";
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);

            lblSubtitle.Text = string.Empty;
            lblSubtitle.AutoSize = true;
            lblSubtitle.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            lblSubtitle.Visible = false;

            Image? logoImage = BrandingAssets.TryLoadLogo();
            _logoSize = logoImage != null ? new Size(320, 130) : new Size(74, 74);
            _logoControl = BrandingAssets.CreateLogoControl(_logoSize, 20, "\uE77B");

            lblLogin.Text = "Login";
            lblLogin.AutoSize = true;
            lblLogin.Font = new Font("Bahnschrift SemiLight", 12, FontStyle.Regular);

            SetupInputRow(usernameRow, "\uE77B", txtUsername, "Loginni kiriting");

            lblPassword.Text = "Parol";
            lblPassword.AutoSize = true;
            lblPassword.Font = new Font("Bahnschrift SemiLight", 12, FontStyle.Regular);

            SetupInputRow(passwordRow, "\uE72E", txtPassword, "Parolni kiriting");
            txtPassword.PasswordChar = '*';

            btnPasswordToggle.Size = new Size(52, 34);
            btnPasswordToggle.FlatStyle = FlatStyle.Flat;
            btnPasswordToggle.FlatAppearance.BorderSize = 0;
            btnPasswordToggle.Cursor = Cursors.Hand;
            btnPasswordToggle.Font = new Font("Bahnschrift SemiBold", 9, FontStyle.Bold);
            btnPasswordToggle.Text = "KO'R";
            btnPasswordToggle.Click += (s, e) =>
            {
                _showPassword = !_showPassword;
                if (!IsPlaceholderActive(txtPassword))
                {
                    txtPassword.PasswordChar = _showPassword ? '\0' : '*';
                }
                btnPasswordToggle.Text = _showPassword ? "YOP" : "KO'R";
                btnPasswordToggle.ForeColor = _showPassword ? Color.FromArgb(56, 88, 233) : UiTheme.Muted;
            };
            passwordRow.Controls.Add(btnPasswordToggle);
            btnPasswordToggle.Location = new Point(passwordRow.Width - 60, 10);
            btnPasswordToggle.Anchor = AnchorStyles.Right | AnchorStyles.Top;

            lnkForgot.Text = "Parolni unutdingizmi?";
            lnkForgot.AutoSize = true;
            lnkForgot.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkForgot.Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            lnkForgot.LinkColor = Color.FromArgb(80, 72, 255);
            lnkForgot.ActiveLinkColor = Color.FromArgb(58, 45, 235);
            lnkForgot.VisitedLinkColor = Color.FromArgb(80, 72, 255);
            lnkForgot.Click += (s, e) =>
            {
                MessageBox.Show(
                    "Parolni tiklash uchun admin bilan bog'laning.",
                    "Parolni tiklash",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };

            btnLogin.Text = "KIRISH";
            btnLogin.Size = new Size(400, 56);
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Cursor = Cursors.Hand;
            btnLogin.Font = new Font("Bahnschrift SemiBold", 16, FontStyle.Bold);
            btnLogin.ForeColor = Color.White;
            btnLogin.Paint += BtnLogin_Paint;
            btnLogin.MouseEnter += (s, e) => { _loginHover = true; btnLogin.Invalidate(); };
            btnLogin.MouseLeave += (s, e) => { _loginHover = false; _loginPressed = false; btnLogin.Invalidate(); };
            btnLogin.MouseDown += (s, e) => { _loginPressed = true; btnLogin.Invalidate(); };
            btnLogin.MouseUp += (s, e) => { _loginPressed = false; btnLogin.Invalidate(); };
            btnLogin.Click += BtnLogin_Click;

            loadingTimer.Interval = 170;
            loadingTimer.Tick += (s, e) =>
            {
                _loadingFrame = (_loadingFrame + 1) % 4;
                btnLogin.Text = $"KIRILMOQDA{new string('.', _loadingFrame)}";
            };

            card.Controls.Add(_logoControl);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblSubtitle);
            card.Controls.Add(lblLogin);
            card.Controls.Add(usernameRow);
            card.Controls.Add(lblPassword);
            card.Controls.Add(passwordRow);
            card.Controls.Add(lnkForgot);
            card.Controls.Add(btnLogin);

            Controls.Add(card);
            Controls.Add(btnTheme);

            Resize += (s, e) => Relayout();
            Relayout();
            ApplyTheme();
            txtUsername.Focus();
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            SetLoadingState(true);
            await Task.Delay(420);

            string username = GetInputText(txtUsername);
            string password = GetInputText(txtPassword);

            try
            {
                if (activationService.TryGetValidLocalActivation(out LocalActivationRecord? activation, out _) && activation != null)
                {
                    var cloudSync = new CloudSyncService();
                    await cloudSync.ApplyPendingPasswordResetsNowAsync(activation);
                }
            }
            catch
            {
                // Reset sync bajarilmasa login oqimini to'xtatmaymiz.
            }

            AppUser? user = dbHelper.AuthenticateUser(username, password);
            if (user == null)
            {
                SetLoadingState(false);
                MessageBox.Show("Login yoki parol noto'g'ri!", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (user.MustChangePassword)
            {
                SetLoadingState(false);
                using ChangePasswordForm changePasswordForm = new ChangePasswordForm(user, dbHelper);
                if (changePasswordForm.ShowDialog(this) != DialogResult.OK)
                {
                    MessageBox.Show("Parol yangilanmaguncha tizimga kirib bo'lmaydi.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SetLoadingState(true);
            }

            using MainForm mainForm = new MainForm(user);
            Hide();
            DialogResult result = mainForm.ShowDialog(this);
            Show();
            Activate();

            if (result == DialogResult.Retry)
            {
                SetLoadingState(false);
                txtPassword.Clear();
                txtUsername.Focus();
                return;
            }

            Close();
        }

        private void SetupInputRow(Panel row, string iconGlyph, TextBox textBox, string placeholder)
        {
            row.Size = new Size(400, 54);
            row.Padding = new Padding(14, 8, 12, 8);
            row.BackColor = _inputRowBg;
            row.Paint += InputRow_Paint;

            Label icon = new Label
            {
                Text = iconGlyph,
                AutoSize = false,
                Width = 28,
                Dock = DockStyle.Left,
                Font = UiTheme.IconFont(14),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            textBox.BorderStyle = BorderStyle.None;
            textBox.Left = 44;
            textBox.Top = 14;
            textBox.Width = row.Width - 58;
            textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBox.Font = new Font("Bahnschrift", 14, FontStyle.Regular);
            textBox.PlaceholderText = string.Empty;
            AttachPlaceholder(textBox, placeholder, textBox == txtPassword);

            row.Controls.Add(textBox);
            row.Controls.Add(icon);
        }

        private void SetLoadingState(bool loading)
        {
            _isLoading = loading;
            txtUsername.Enabled = !loading;
            txtPassword.Enabled = !loading;
            btnPasswordToggle.Enabled = !loading;
            btnTheme.Enabled = !loading;
            btnLogin.Enabled = !loading;

            if (loading)
            {
                _loadingFrame = 0;
                loadingTimer.Start();
            }
            else
            {
                loadingTimer.Stop();
                btnLogin.Text = "KIRISH";
            }
        }

        private void ApplyTheme()
        {
            BackColor = Color.White;
            _cardFill = Color.FromArgb(20, 30, 44);
            card.BackColor = Color.Transparent;
            _inputRowBg = Color.FromArgb(31, 43, 60);
            _inputRowBorder = Color.FromArgb(0, 235, 140);
            Color inputBg = _inputRowBg;
            Color titleColor = Color.FromArgb(238, 245, 255);
            Color subText = Color.FromArgb(168, 186, 212);

            btnTheme.Text = UiTheme.CurrentMode == UiTheme.ThemeMode.Light ? "QORA" : "OQ";
            btnTheme.BackColor = UiTheme.CurrentMode == UiTheme.ThemeMode.Light
                ? Color.FromArgb(248, 251, 255)
                : Color.FromArgb(30, 40, 56);
            btnTheme.ForeColor = titleColor;
            btnTheme.FlatAppearance.BorderColor = UiTheme.Border;

            lblTitle.ForeColor = titleColor;
            lblSubtitle.ForeColor = subText;
            lblLogin.ForeColor = titleColor;
            lblPassword.ForeColor = titleColor;
            lblTitle.BackColor = Color.Transparent;
            lblSubtitle.BackColor = Color.Transparent;
            lblLogin.BackColor = Color.Transparent;
            lblPassword.BackColor = Color.Transparent;
            lnkForgot.BackColor = Color.Transparent;

            lnkForgot.LinkColor = UiTheme.CurrentMode == UiTheme.ThemeMode.Light ? Color.FromArgb(80, 72, 255) : Color.FromArgb(149, 167, 255);
            lnkForgot.ActiveLinkColor = UiTheme.CurrentMode == UiTheme.ThemeMode.Light ? Color.FromArgb(58, 45, 235) : Color.FromArgb(178, 196, 255);
            lnkForgot.VisitedLinkColor = lnkForgot.LinkColor;

            ApplyRowIconColor(usernameRow, subText);
            ApplyRowIconColor(passwordRow, subText);

            txtUsername.BackColor = inputBg;
            txtUsername.ForeColor = IsPlaceholderActive(txtUsername) ? Color.FromArgb(162, 182, 212) : titleColor;
            txtUsername.BorderStyle = BorderStyle.None;
            txtPassword.BackColor = inputBg;
            txtPassword.ForeColor = IsPlaceholderActive(txtPassword) ? Color.FromArgb(162, 182, 212) : titleColor;
            txtPassword.BorderStyle = BorderStyle.None;
            usernameRow.BackColor = inputBg;
            passwordRow.BackColor = inputBg;
            btnPasswordToggle.BackColor = inputBg;
            btnPasswordToggle.UseVisualStyleBackColor = false;
            btnPasswordToggle.FlatAppearance.MouseOverBackColor = inputBg;
            btnPasswordToggle.FlatAppearance.MouseDownBackColor = inputBg;
            btnPasswordToggle.ForeColor = _showPassword ? Color.FromArgb(110, 150, 255) : Color.FromArgb(170, 190, 220);

            Invalidate(true);
        }

        private void AttachPlaceholder(TextBox textBox, string placeholder, bool isPassword)
        {
            _placeholders[textBox] = placeholder;
            textBox.GotFocus += (s, e) =>
            {
                if (!IsPlaceholderActive(textBox))
                {
                    return;
                }

                textBox.Text = string.Empty;
                textBox.ForeColor = Color.FromArgb(238, 245, 255);
                if (isPassword)
                {
                    textBox.PasswordChar = _showPassword ? '\0' : '*';
                }
            };
            textBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    SetPlaceholder(textBox, isPassword);
                }
            };
            SetPlaceholder(textBox, isPassword);
        }

        private void SetPlaceholder(TextBox textBox, bool isPassword)
        {
            if (!_placeholders.TryGetValue(textBox, out string? placeholder))
            {
                return;
            }

            textBox.Text = placeholder;
            textBox.ForeColor = Color.FromArgb(162, 182, 212);
            if (isPassword)
            {
                textBox.PasswordChar = '\0';
            }
        }

        private bool IsPlaceholderActive(TextBox textBox)
        {
            return _placeholders.TryGetValue(textBox, out string? placeholder)
                && string.Equals(textBox.Text, placeholder, StringComparison.Ordinal);
        }

        private string GetInputText(TextBox textBox)
        {
            return IsPlaceholderActive(textBox) ? string.Empty : textBox.Text;
        }

        private static void ApplyRowIconColor(Panel row, Color color)
        {
            foreach (Control c in row.Controls)
            {
                if (c is Label lbl)
                {
                    lbl.ForeColor = color;
                    lbl.BackColor = Color.Transparent;
                }
            }
        }

        private void Relayout()
        {
            card.Left = (ClientSize.Width - card.Width) / 2;
            card.Top = (ClientSize.Height - card.Height) / 2;

            btnTheme.Left = card.Right - btnTheme.Width - 16;
            btnTheme.Top = card.Top + 16;
            btnTheme.Region = new Region(RoundedRect(new Rectangle(0, 0, btnTheme.Width, btnTheme.Height), 14));

            _logoControl.Left = (card.Width - _logoControl.Width) / 2;
            _logoControl.Top = 30;

            lblTitle.Left = (card.Width - lblTitle.Width) / 2;
            lblTitle.Top = _logoControl.Bottom + 12;

            lblSubtitle.Left = (card.Width - lblSubtitle.Width) / 2;
            lblSubtitle.Top = lblTitle.Bottom + 6;

            lblLogin.Left = 34;
            lblLogin.Top = lblTitle.Bottom + 20;

            usernameRow.Left = 34;
            usernameRow.Top = lblLogin.Bottom + 8;

            lblPassword.Left = 34;
            lblPassword.Top = usernameRow.Bottom + 16;

            passwordRow.Left = 34;
            passwordRow.Top = lblPassword.Bottom + 8;
            btnPasswordToggle.Left = passwordRow.Width - btnPasswordToggle.Width - 8;
            btnPasswordToggle.Top = 10;
            txtPassword.Width = btnPasswordToggle.Left - txtPassword.Left - 8;

            lnkForgot.Left = card.Width - lnkForgot.Width - 34;
            lnkForgot.Top = passwordRow.Bottom + 12;

            btnLogin.Left = 34;
            btnLogin.Top = lnkForgot.Bottom + 22;

            usernameRow.Region = new Region(RoundedRect(new Rectangle(0, 0, usernameRow.Width, usernameRow.Height), 16));
            passwordRow.Region = new Region(RoundedRect(new Rectangle(0, 0, passwordRow.Width, passwordRow.Height), 16));
            btnLogin.Region = new Region(RoundedRect(new Rectangle(0, 0, btnLogin.Width, btnLogin.Height), 16));
        }

        private void Card_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(card.Parent?.BackColor ?? UiTheme.Background);
            Rectangle borderRect = new Rectangle(1, 1, card.ClientSize.Width - 3, card.ClientSize.Height - 3);
            using GraphicsPath path = RoundedRect(borderRect, 28);
            using SolidBrush brush = new SolidBrush(_cardFill);
            e.Graphics.FillPath(brush, path);

            using Pen border = new Pen(Color.FromArgb(0, 214, 122), 1.8f);
            e.Graphics.DrawPath(border, path);
        }

        private void InputRow_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel row)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.Clear(row.Parent?.BackColor ?? _cardFill);
            Rectangle borderRect = new Rectangle(1, 1, row.ClientSize.Width - 3, row.ClientSize.Height - 3);
            using GraphicsPath path = RoundedRect(borderRect, 15);
            using SolidBrush brush = new SolidBrush(row.BackColor);
            using Pen border = new Pen(_inputRowBorder, 2f)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(border, path);
        }

        private void BtnLogin_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = btnLogin.ClientRectangle;
            Color c1 = Color.FromArgb(38, 102, 250);
            Color c2 = Color.FromArgb(85, 61, 235);
            if (_loginHover)
            {
                c1 = Color.FromArgb(49, 112, 255);
                c2 = Color.FromArgb(95, 72, 244);
            }
            if (_loginPressed)
            {
                c1 = Color.FromArgb(30, 90, 230);
                c2 = Color.FromArgb(72, 50, 220);
            }
            using LinearGradientBrush brush = new LinearGradientBrush(
                rect,
                c1,
                c2,
                0f);
            using GraphicsPath path = RoundedRect(rect, 16);
            e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(
                e.Graphics,
                btnLogin.Text,
                btnLogin.Font,
                rect,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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



