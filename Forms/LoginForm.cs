using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        public LoginForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Santexnika SRM";
            Size = new Size(500, 420);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.BodyFont;

            Paint += (s, e) =>
            {
                using LinearGradientBrush brush = new LinearGradientBrush(
                    ClientRectangle,
                    Color.FromArgb(235, 245, 255),
                    Color.FromArgb(226, 240, 251),
                    90f);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            };

            Panel card = new Panel
            {
                Size = new Size(360, 270),
                BackColor = UiTheme.Card,
                Left = 70,
                Top = 70
            };

            Label lblTitle = new Label
            {
                Text = "Tizimga kirish",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 16, FontStyle.Bold),
                ForeColor = UiTheme.Text,
                Top = 24,
                Left = 112
            };

            txtUsername.PlaceholderText = "Login";
            txtUsername.Left = 55;
            txtUsername.Top = 90;
            txtUsername.Width = 250;
            UiTheme.StyleInput(txtUsername);

            txtPassword.PlaceholderText = "Parol";
            txtPassword.Left = 55;
            txtPassword.Top = 130;
            txtPassword.Width = 250;
            txtPassword.PasswordChar = '*';
            UiTheme.StyleInput(txtPassword);

            Button btnLogin = new Button
            {
                Text = "KIRISH",
                Width = 250,
                Height = 42,
                Left = 55,
                Top = 182
            };
            UiTheme.StylePrimaryButton(btnLogin);
            btnLogin.Click += BtnLogin_Click;

            card.Controls.Add(lblTitle);
            card.Controls.Add(txtUsername);
            card.Controls.Add(txtPassword);
            card.Controls.Add(btnLogin);

            Controls.Add(card);
        }

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            AppUser? user = dbHelper.AuthenticateUser(txtUsername.Text, txtPassword.Text);
            if (user == null)
            {
                MessageBox.Show("Login yoki parol noto'g'ri!", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using MainForm mainForm = new MainForm(user);
            Hide();
            DialogResult result = mainForm.ShowDialog(this);
            Show();
            Activate();

            if (result == DialogResult.Retry)
            {
                txtPassword.Clear();
                txtUsername.Focus();
                return;
            }

            Close();
        }
    }
}
