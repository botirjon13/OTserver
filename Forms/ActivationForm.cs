using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SantexnikaSRM.Services;

namespace SantexnikaSRM.Forms
{
    public class ActivationForm : Form
    {
        private readonly ActivationService _activationService;
        private readonly TextBox _txtLicense = new TextBox();
        private readonly Label _lblStatus = new Label();
        private readonly Button _btnActivate = new Button();
        private readonly Button _btnExit = new Button();
        private readonly Button _btnContact = new Button();
        private readonly string _serverUrl;

        public LocalActivationRecord? Activation { get; private set; }

        public ActivationForm(ActivationService activationService, string infoMessage)
        {
            _activationService = activationService;
            _serverUrl = _activationService.GetDefaultServerUrl();
            InitializeComponent(infoMessage);
        }

        private void InitializeComponent(string infoMessage)
        {
            Text = "Dastur aktivatsiyasi";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 390);
            BackColor = Color.FromArgb(244, 248, 253);
            Font = new Font("Bahnschrift", 11f, FontStyle.Regular);

            Label lblTitle = new Label
            {
                Text = "Birinchi ishga tushirish uchun aktivatsiya kerak",
                AutoSize = false,
                Left = 18,
                Top = 14,
                Width = 520,
                Height = 30,
                Font = new Font("Bahnschrift SemiBold", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 58, 96)
            };

            Label lblInfo = new Label
            {
                Text = infoMessage,
                AutoSize = false,
                Left = 18,
                Top = 46,
                Width = 520,
                Height = 42,
                ForeColor = Color.FromArgb(92, 112, 140)
            };

            Label lblLicense = new Label
            {
                Text = "License key",
                AutoSize = true,
                Left = 18,
                Top = 108
            };

            _txtLicense.SetBounds(18, 132, 520, 34);

            _lblStatus.SetBounds(18, 178, 520, 42);
            _lblStatus.ForeColor = Color.FromArgb(92, 112, 140);
            _lblStatus.Text = "Aktivatsiya qilgandan keyin dastur offline ishlay oladi.";

            _btnContact.Text = "Biz bilan bog'lanish";
            _btnContact.SetBounds(18, 280, 520, 40);
            _btnContact.FlatStyle = FlatStyle.Flat;
            _btnContact.FlatAppearance.BorderSize = 0;
            _btnContact.BackColor = Color.FromArgb(90, 120, 166);
            _btnContact.ForeColor = Color.White;
            _btnContact.Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold);
            _btnContact.Click += async (s, e) => await ShowContactAsync();

            _btnActivate.Text = "Aktivlashtirish";
            _btnActivate.SetBounds(18, 332, 250, 40);
            _btnActivate.FlatStyle = FlatStyle.Flat;
            _btnActivate.FlatAppearance.BorderSize = 0;
            _btnActivate.BackColor = Color.FromArgb(46, 110, 216);
            _btnActivate.ForeColor = Color.White;
            _btnActivate.Font = new Font("Bahnschrift SemiBold", 12f, FontStyle.Bold);
            _btnActivate.Click += async (s, e) => await ActivateAsync();

            _btnExit.Text = "Chiqish";
            _btnExit.SetBounds(288, 332, 250, 40);
            _btnExit.FlatStyle = FlatStyle.Flat;
            _btnExit.FlatAppearance.BorderSize = 0;
            _btnExit.BackColor = Color.FromArgb(143, 157, 177);
            _btnExit.ForeColor = Color.White;
            _btnExit.Font = new Font("Bahnschrift SemiBold", 12f, FontStyle.Bold);
            _btnExit.Click += (s, e) => Close();

            Controls.Add(lblTitle);
            Controls.Add(lblInfo);
            Controls.Add(lblLicense);
            Controls.Add(_txtLicense);
            Controls.Add(_lblStatus);
            Controls.Add(_btnContact);
            Controls.Add(_btnActivate);
            Controls.Add(_btnExit);
        }

        private async Task ActivateAsync()
        {
            string server = _serverUrl;
            string key = _txtLicense.Text.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                SetStatus("License key kiriting.", true);
                return;
            }

            try
            {
                ToggleBusy(true);
                SetStatus("Aktivatsiya yuborilmoqda...", false);
                var result = await _activationService.ActivateAsync(server, key, Application.ProductVersion);
                if (!result.Ok || result.Activation == null)
                {
                    SetStatus(result.Error ?? "Aktivatsiya xatosi.", true);
                    return;
                }

                Activation = result.Activation;
                if (!string.IsNullOrWhiteSpace(result.FirstLoginUsername) && !string.IsNullOrWhiteSpace(result.FirstLoginPassword))
                {
                    string contactText = string.IsNullOrWhiteSpace(result.SupportContact) ? "-" : result.SupportContact;
                    MessageBox.Show(
                        "Aktivatsiya muvaffaqiyatli.\n\n" +
                        "Birinchi kirish uchun bir martalik ma'lumotlar:\n" +
                        $"Login: {result.FirstLoginUsername}\n" +
                        $"Parol: {result.FirstLoginPassword}\n\n" +
                        "Muhim: birinchi kirishda parolni almashtirish talab qilinadi.\n\n" +
                        $"Yordam uchun aloqa: {contactText}",
                        "Birinchi kirish ma'lumoti",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                SetStatus("Aktivatsiya muvaffaqiyatli.", false);
                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                ToggleBusy(false);
            }
        }

        private void ToggleBusy(bool busy)
        {
            _btnActivate.Enabled = !busy;
            _btnExit.Enabled = !busy;
            _btnContact.Enabled = !busy;
            _txtLicense.Enabled = !busy;
        }

        private async Task ShowContactAsync()
        {
            _btnContact.Enabled = false;
            try
            {
                string contact = await _activationService.GetSupportContactAsync(_serverUrl);
                if (string.IsNullOrWhiteSpace(contact) || contact == "-")
                {
                    contact = "Aloqa ma'lumoti hali kiritilmagan. Iltimos sotuvchi bilan bog'laning.";
                }
                MessageBox.Show($"Litsenziya olish uchun aloqa:\n{contact}", "Biz bilan bog'lanish", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                _btnContact.Enabled = true;
            }
        }

        private void SetStatus(string text, bool isError)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = isError ? Color.FromArgb(188, 58, 58) : Color.FromArgb(46, 110, 84);
        }
    }
}
