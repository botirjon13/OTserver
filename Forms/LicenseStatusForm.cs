using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SantexnikaSRM.Services;

namespace SantexnikaSRM.Forms
{
    public class LicenseStatusForm : Form
    {
        private readonly ActivationService _activationService = new ActivationService();
        private readonly Label _lblState = new Label();
        private readonly Label _lblLicenseKey = new Label();
        private readonly Label _lblDeviceId = new Label();
        private readonly Label _lblActivatedAt = new Label();
        private readonly Label _lblExpiresAt = new Label();

        public LicenseStatusForm()
        {
            InitializeComponent();
            LoadState();
        }

        private void InitializeComponent()
        {
            Text = "License holati";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(760, 360);
            BackColor = Color.FromArgb(243, 247, 253);
            Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular);

            Panel card = new Panel
            {
                Left = 18,
                Top = 18,
                Width = 724,
                Height = 280,
                BackColor = Color.White
            };

            Label title = new Label
            {
                Text = "Aktivatsiya ma'lumotlari",
                Left = 16,
                Top = 14,
                Width = 400,
                Height = 28,
                Font = new Font("Bahnschrift SemiBold", 15f, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 58, 96)
            };

            _lblState.SetBounds(16, 50, 680, 24);
            _lblState.Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold);

            _lblLicenseKey.SetBounds(16, 86, 680, 24);
            _lblDeviceId.SetBounds(16, 118, 680, 24);
            _lblActivatedAt.SetBounds(16, 150, 680, 24);
            _lblExpiresAt.SetBounds(16, 182, 680, 24);

            card.Controls.Add(title);
            card.Controls.Add(_lblState);
            card.Controls.Add(_lblLicenseKey);
            card.Controls.Add(_lblDeviceId);
            card.Controls.Add(_lblActivatedAt);
            card.Controls.Add(_lblExpiresAt);

            Button btnRefresh = new Button
            {
                Text = "Yangilash",
                Left = 18,
                Top = 308,
                Width = 180,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(47, 111, 216),
                ForeColor = Color.White
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadState();

            Button btnReactivate = new Button
            {
                Text = "Qayta aktivatsiya",
                Left = 206,
                Top = 308,
                Width = 180,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(47, 111, 216),
                ForeColor = Color.White
            };
            btnReactivate.FlatAppearance.BorderSize = 0;
            btnReactivate.Click += (s, e) =>
            {
                using var activationForm = new ActivationForm(_activationService, "Yangi license key bilan qayta aktivatsiya qiling.");
                if (activationForm.ShowDialog(this) == DialogResult.OK && activationForm.Activation != null)
                {
                    LoadState();
                    MessageBox.Show("Qayta aktivatsiya muvaffaqiyatli.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            Button btnClose = new Button
            {
                Text = "Yopish",
                Left = 562,
                Top = 308,
                Width = 180,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(133, 149, 173),
                ForeColor = Color.White
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();

            Controls.Add(card);
            Controls.Add(btnRefresh);
            Controls.Add(btnReactivate);
            Controls.Add(btnClose);
        }

        private void LoadState()
        {
            if (!_activationService.TryGetValidLocalActivation(out LocalActivationRecord? activation, out string message) || activation == null)
            {
                _lblState.Text = $"Holat: Aktiv emas ({message})";
                _lblState.ForeColor = Color.FromArgb(186, 61, 61);
                _lblLicenseKey.Text = "License key: -";
                _lblDeviceId.Text = $"Device ID: {_activationService.GetDeviceId()}";
                _lblActivatedAt.Text = "Aktivatsiya vaqti: -";
                _lblExpiresAt.Text = "Muddat: -";
                return;
            }

            _lblState.Text = "Holat: Aktiv";
            _lblState.ForeColor = Color.FromArgb(28, 126, 72);
            _lblLicenseKey.Text = $"License key: {activation.LicenseKey}";
            _lblDeviceId.Text = $"Device ID: {activation.DeviceId}";
            _lblActivatedAt.Text = $"Aktivatsiya vaqti (UTC): {activation.ActivatedAtUtc}";
            _lblExpiresAt.Text = $"Muddat: {FormatExpiry(activation.ExpiresAtUtc)}";
        }

        private static string FormatExpiry(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Cheklanmagan";
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime dt))
            {
                return $"{dt:yyyy-MM-dd}";
            }

            return value;
        }
    }
}
