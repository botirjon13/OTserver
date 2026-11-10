using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class LoginForm : Form
    {
        private readonly DatabaseHelper dbHelper = new DatabaseHelper();
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
        private Color _cardFill = UiTheme.Card;
        private Color _inputRowBg = UiTheme.BackgroundSecondary;
        private Color _inputRowBorder = UiTheme.Border;
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
            Size = new Size(760, 700);
            MinimumSize = new Size(420, 560);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor = UiTheme.Background;
            Font = UiTheme.BodyFont;
            DoubleBuffered = true;

            btnTheme.Size = new Size(112, 34);
            btnTheme.FlatStyle = FlatStyle.Flat;
            btnTheme.FlatAppearance.BorderSize = 1;
            btnTheme.Cursor = Cursors.Hand;
            btnTheme.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 10, FontStyle.Bold);
            btnTheme.Click += (s, e) =>
            {
                UiTheme.ToggleTheme();
                ApplyTheme();
                Invalidate();
            };

            card.Size = new Size(460, 560);
            card.Padding = new Padding(28);
            card.Paint += Card_Paint;

            lblTitle.Text = "Tizimga kirish";
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 20, FontStyle.Bold);

            lblSubtitle.Text = "Davom etish uchun login va parolingizni kiriting.";
            lblSubtitle.AutoSize = true;
            lblSubtitle.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10, FontStyle.Regular);

            Image? logoImage = BrandingAssets.TryLoadLogo();
            _logoSize = logoImage != null ? new Size(260, 88) : new Size(56, 56);
            _logoControl = BrandingAssets.CreateLogoControl(_logoSize, 20, "\uE77B");

            lblLogin.Text = "Foydalanuvchi nomi";
            lblLogin.AutoSize = true;
            lblLogin.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 10, FontStyle.Bold);

            SetupInputRow(usernameRow, txtUsername, "Loginni kiriting");

            lblPassword.Text = "Parol";
            lblPassword.AutoSize = true;
            lblPassword.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 10, FontStyle.Bold);

            SetupInputRow(passwordRow, txtPassword, "Parolni kiriting");
            txtPassword.PasswordChar = '*';

            btnPasswordToggle.Size = new Size(56, 34);
            btnPasswordToggle.FlatStyle = FlatStyle.Flat;
            btnPasswordToggle.FlatAppearance.BorderSize = 0;
            btnPasswordToggle.Cursor = Cursors.Hand;
            btnPasswordToggle.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 9, FontStyle.Regular);
            btnPasswordToggle.Text = "KO'R";
            btnPasswordToggle.Click += (s, e) =>
            {
                _showPassword = !_showPassword;
                if (!IsPlaceholderActive(txtPassword))
                {
                    txtPassword.PasswordChar = _showPassword ? '\0' : '*';
                }
                btnPasswordToggle.Text = _showPassword ? "YOP" : "KO'R";
                btnPasswordToggle.ForeColor = _showPassword ? UiTheme.Primary : UiTheme.Muted;
            };
            passwordRow.Controls.Add(btnPasswordToggle);
            btnPasswordToggle.Location = new Point(passwordRow.Width - 60, 10);
            btnPasswordToggle.Anchor = AnchorStyles.Right | AnchorStyles.Top;

            lnkForgot.Text = "Parolni unutdingizmi?";
            lnkForgot.AutoSize = true;
            lnkForgot.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkForgot.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 11, FontStyle.Regular);
            lnkForgot.LinkColor = UiTheme.Primary;
            lnkForgot.ActiveLinkColor = UiTheme.Primary;
            lnkForgot.VisitedLinkColor = UiTheme.Primary;
            lnkForgot.Click += (s, e) =>
            {
                MessageBox.Show(
                    "Parolni tiklash uchun admin bilan bog'laning.",
                    "Parolni tiklash",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };

            btnLogin.Text = "KIRISH";
            btnLogin.Size = new Size(400, 48);
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Cursor = Cursors.Hand;
            btnLogin.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 12, FontStyle.Bold);
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

        private void SetupInputRow(Panel row, TextBox textBox, string placeholder)
        {
            row.Size = new Size(400, 50);
            row.Padding = new Padding(12, 7, 12, 7);
            row.BackColor = _inputRowBg;
            row.Paint += InputRow_Paint;

            textBox.BorderStyle = BorderStyle.None;
            textBox.Left = 12;
            textBox.Top = 12;
            textBox.Width = row.Width - 36;
            textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBox.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 12, FontStyle.Regular);
            textBox.PlaceholderText = string.Empty;
            AttachPlaceholder(textBox, placeholder, textBox == txtPassword);
            textBox.GotFocus += (s, e) => row.Invalidate();
            textBox.LostFocus += (s, e) => row.Invalidate();

            row.Controls.Add(textBox);
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
            BackColor = UiTheme.Background;
            _cardFill = UiTheme.Card;
            card.BackColor = Color.Transparent;
            _inputRowBg = UiTheme.CurrentMode == UiTheme.ThemeMode.Light
                ? Color.White
                : UiTheme.BackgroundSecondary;
            _inputRowBorder = UiTheme.Border;
            Color inputBg = _inputRowBg;
            Color titleColor = UiTheme.Text;
            Color subText = UiTheme.Muted;

            btnTheme.Text = UiTheme.CurrentMode == UiTheme.ThemeMode.Light ? "Tungi rejim" : "Yorug' rejim";
            btnTheme.BackColor = UiTheme.Card;
            btnTheme.ForeColor = UiTheme.Text;
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

            lnkForgot.LinkColor = UiTheme.Primary;
            lnkForgot.ActiveLinkColor = UiTheme.Primary;
            lnkForgot.VisitedLinkColor = lnkForgot.LinkColor;

            txtUsername.BackColor = inputBg;
            txtUsername.ForeColor = IsPlaceholderActive(txtUsername) ? subText : titleColor;
            txtUsername.BorderStyle = BorderStyle.None;
            txtPassword.BackColor = inputBg;
            txtPassword.ForeColor = IsPlaceholderActive(txtPassword) ? subText : titleColor;
            txtPassword.BorderStyle = BorderStyle.None;
            usernameRow.BackColor = inputBg;
            passwordRow.BackColor = inputBg;
            btnPasswordToggle.BackColor = inputBg;
            btnPasswordToggle.UseVisualStyleBackColor = false;
            btnPasswordToggle.FlatAppearance.MouseOverBackColor = inputBg;
            btnPasswordToggle.FlatAppearance.MouseDownBackColor = inputBg;
            btnPasswordToggle.ForeColor = _showPassword ? UiTheme.Primary : UiTheme.Muted;

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
                textBox.ForeColor = UiTheme.Text;
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
            textBox.ForeColor = UiTheme.Muted;
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

        private void Relayout()
        {
            card.Width = Math.Min(460, ClientSize.Width - 40);
            card.Height = Math.Min(560, ClientSize.Height - 40);
            card.Left = (ClientSize.Width - card.Width) / 2;
            card.Top = (ClientSize.Height - card.Height) / 2;

            btnTheme.Left = card.Right - btnTheme.Width - 16;
            btnTheme.Top = card.Top + 16;
            btnTheme.Region = new Region(RoundedRect(new Rectangle(0, 0, btnTheme.Width, btnTheme.Height), 14));

            int logoBlockHeight = 80;
            _logoControl.Left = (card.Width - _logoControl.Width) / 2;
            _logoControl.Top = 52 + (logoBlockHeight - _logoControl.Height) / 2;

            lblTitle.Left = (card.Width - lblTitle.Width) / 2;
            lblTitle.Top = 52 + logoBlockHeight + 10;

            lblSubtitle.Left = (card.Width - lblSubtitle.Width) / 2;
            lblSubtitle.Top = lblTitle.Bottom + 5;

            int contentLeft = 28;
            int contentWidth = card.Width - contentLeft * 2;
            lblLogin.Left = contentLeft;
            lblLogin.Top = lblSubtitle.Bottom + 20;

            usernameRow.Left = contentLeft;
            usernameRow.Top = lblLogin.Bottom + 8;
            usernameRow.Width = contentWidth;
            txtUsername.Width = usernameRow.Width - 24;

            lblPassword.Left = contentLeft;
            lblPassword.Top = usernameRow.Bottom + 14;

            passwordRow.Left = contentLeft;
            passwordRow.Top = lblPassword.Bottom + 8;
            passwordRow.Width = contentWidth;
            btnPasswordToggle.Left = passwordRow.Width - btnPasswordToggle.Width - 8;
            btnPasswordToggle.Top = 10;
            txtPassword.Width = btnPasswordToggle.Left - txtPassword.Left - 8;

            lnkForgot.Left = card.Width - lnkForgot.Width - 34;
            lnkForgot.Top = passwordRow.Bottom + 10;

            btnLogin.Left = contentLeft;
            btnLogin.Width = contentWidth;
            btnLogin.Top = lnkForgot.Bottom + 18;

            usernameRow.Region = new Region(RoundedRect(new Rectangle(0, 0, usernameRow.Width, usernameRow.Height), 8));
            passwordRow.Region = new Region(RoundedRect(new Rectangle(0, 0, passwordRow.Width, passwordRow.Height), 8));
            btnLogin.Region = new Region(RoundedRect(new Rectangle(0, 0, btnLogin.Width, btnLogin.Height), 8));
        }

        private void Card_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(card.Parent?.BackColor ?? UiTheme.Background);
            Rectangle borderRect = new Rectangle(1, 1, card.ClientSize.Width - 3, card.ClientSize.Height - 3);
            using GraphicsPath path = RoundedRect(borderRect, 14);
            using SolidBrush brush = new SolidBrush(_cardFill);
            e.Graphics.FillPath(brush, path);

            using Pen border = new Pen(UiTheme.Border, 1f);
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
            e.Graphics.Clear(_cardFill);
            Rectangle borderRect = new Rectangle(1, 1, row.ClientSize.Width - 3, row.ClientSize.Height - 3);
            using GraphicsPath path = RoundedRect(borderRect, 8);
            using SolidBrush brush = new SolidBrush(row.BackColor);
            Color borderColor = row.ContainsFocus ? UiTheme.Primary : _inputRowBorder;
            using Pen border = new Pen(borderColor, 1f);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(border, path);
        }

        private void BtnLogin_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = btnLogin.ClientRectangle;
            Color buttonColor = UiTheme.Primary;
            if (_loginHover)
            {
                buttonColor = ControlPaint.Dark(buttonColor, 0.08f);
            }
            if (_loginPressed)
            {
                buttonColor = ControlPaint.Dark(buttonColor, 0.16f);
            }
            using SolidBrush brush = new SolidBrush(buttonColor);
            using GraphicsPath path = RoundedRect(rect, 8);
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
