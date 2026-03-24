using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class ChangePasswordForm : Form
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly AppUser _currentUser;

        private readonly TextBox _txtPassword = new TextBox();
        private readonly TextBox _txtPasswordRepeat = new TextBox();

        public ChangePasswordForm(AppUser currentUser, DatabaseHelper dbHelper)
        {
            _currentUser = currentUser;
            _dbHelper = dbHelper;
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
        }

        private void InitializeComponent()
        {
            Text = "Parolni Yangilash";
            Size = new Size(460, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(239, 242, 248);
            DoubleBuffered = true;
            Padding = new Padding(1);

            Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using Pen pen = new Pen(Color.FromArgb(166, 177, 196), 1.5f);
                using GraphicsPath path = RoundedRect(new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3), 14);
                e.Graphics.DrawPath(pen, path);
            };
            Resize += (s, e) => Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)), 14));

            Panel root = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(1) };
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(220, 227, 239));
                using GraphicsPath path = RoundedRect(card.ClientRectangle, 14);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            card.Resize += (s, e) => card.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, card.Width), Math.Max(1, card.Height)), 14));

            Panel header = new Panel { Dock = DockStyle.Top, Height = 78 };
            header.Paint += (s, e) =>
            {
                using LinearGradientBrush brush = new LinearGradientBrush(header.ClientRectangle, Color.FromArgb(135, 31, 226), Color.FromArgb(176, 72, 234), 0f);
                e.Graphics.FillRectangle(brush, header.ClientRectangle);
            };

            Label lblTitle = new Label
            {
                Text = "Parolni Yangilash",
                Left = 24,
                Top = 24,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 20, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            Label btnClose = new Label
            {
                Text = "\uE711",
                Font = UiTheme.IconFont(12),
                ForeColor = Color.FromArgb(70, 29, 106),
                BackColor = Color.Transparent,
                Width = 24,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => Close();
            header.Controls.Add(lblTitle);
            header.Controls.Add(btnClose);
            header.Resize += (s, e) => btnClose.Location = new Point(header.Width - 30, 10);

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 18, 24, 24) };

            Label lblInfo = new Label
            {
                Text = "Xavfsizlik uchun yangi parol kiriting.",
                Left = 0,
                Top = 20,
                AutoSize = true,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(96, 108, 125)
            };

            Label lblPass = new Label
            {
                Text = "Yangi parol (kamida 8 belgi)",
                Left = 0,
                Top = 58,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 75, 95)
            };
            Panel passWrap = NewInputWrap();
            passWrap.SetBounds(0, 86, 352, 52);
            StylePasswordBox(_txtPassword, "Yangi parol (kamida 8 belgi)");
            passWrap.Controls.Add(_txtPassword);

            Label lblPass2 = new Label
            {
                Text = "Yangi parolni qayta kiriting",
                Left = 0,
                Top = 152,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(62, 75, 95)
            };
            Panel pass2Wrap = NewInputWrap();
            pass2Wrap.SetBounds(0, 180, 352, 52);
            StylePasswordBox(_txtPasswordRepeat, "Yangi parolni qayta kiriting");
            pass2Wrap.Controls.Add(_txtPasswordRepeat);

            Button btnSave = new Button
            {
                Text = "SAQLASH",
                Left = 0,
                Top = 258,
                Width = 352,
                Height = 52,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Bahnschrift SemiBold", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using LinearGradientBrush brush = new LinearGradientBrush(btnSave.ClientRectangle, Color.FromArgb(7, 184, 70), Color.FromArgb(8, 175, 68), 0f);
                using GraphicsPath path = RoundedRect(btnSave.ClientRectangle, 10);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, btnSave.Text, btnSave.Font, btnSave.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btnSave.Resize += (s, e) => btnSave.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btnSave.Width), Math.Max(1, btnSave.Height)), 10));
            btnSave.Click += BtnSave_Click;

            body.Controls.Add(lblInfo);
            body.Controls.Add(lblPass);
            body.Controls.Add(passWrap);
            body.Controls.Add(lblPass2);
            body.Controls.Add(pass2Wrap);
            body.Controls.Add(btnSave);

            body.Resize += (s, e) =>
            {
                int contentWidth = Math.Min(352, body.ClientSize.Width);
                int left = (body.ClientSize.Width - contentWidth) / 2;

                lblInfo.Left = left;
                lblPass.Left = left;
                lblPass2.Left = left;

                passWrap.SetBounds(left, 86, contentWidth, 52);
                _txtPassword.Width = contentWidth - 24;
                pass2Wrap.SetBounds(left, 180, contentWidth, 52);
                _txtPasswordRepeat.Width = contentWidth - 24;
                btnSave.SetBounds(left, 258, contentWidth, 52);
            };

            card.Controls.Add(body);
            card.Controls.Add(header);
            root.Controls.Add(card);
            Controls.Add(root);

            // Header orqali oynani sudrash.
            Point dragStart = Point.Empty;
            bool dragging = false;
            header.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; } };
            header.MouseMove += (s, e) =>
            {
                if (!dragging)
                {
                    return;
                }
                Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
            };
            header.MouseUp += (s, e) => dragging = false;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                string pass1 = _txtPassword.Text;
                string pass2 = _txtPasswordRepeat.Text;

                if (!string.Equals(pass1, pass2, StringComparison.Ordinal))
                {
                    MessageBox.Show("Parollar mos emas.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _dbHelper.ChangePassword(_currentUser.Id, pass1);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Panel NewInputWrap()
        {
            Panel p = new Panel { BackColor = Color.White };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(Color.FromArgb(248, 250, 255));
                using Pen border = new Pen(Color.FromArgb(190, 202, 223));
                using GraphicsPath path = RoundedRect(p.ClientRectangle, 9);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            p.Resize += (s, e) => p.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, p.Width), Math.Max(1, p.Height)), 9));
            return p;
        }

        private static void StylePasswordBox(TextBox textBox, string placeholder)
        {
            textBox.BorderStyle = BorderStyle.None;
            textBox.BackColor = Color.FromArgb(248, 250, 255);
            textBox.ForeColor = Color.FromArgb(60, 74, 95);
            textBox.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            textBox.PlaceholderText = placeholder;
            textBox.Left = 12;
            textBox.Top = 14;
            textBox.Width = 300;
            textBox.PasswordChar = '*';
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



