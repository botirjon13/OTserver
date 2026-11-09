using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class BackupRestoreForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly BackupService _backupService = new BackupService();
        private readonly CloudBackupCryptoService _cloudBackupCrypto = new CloudBackupCryptoService();
        private readonly ActivationService _activationService = new ActivationService();
        private readonly CloudSyncService _cloudSyncService = new CloudSyncService();

        private readonly Panel _content = new Panel();
        private readonly ListBox _listBackups = new ListBox();
        private readonly Button _btnCreate = new Button();
        private readonly Button _btnRestore = new Button();
        private readonly Button _btnRefresh = new Button();
        private readonly Button _btnCloudSetup = new Button();
        private readonly Button _btnCloudUpload = new Button();
        private readonly Button _btnCloudRestore = new Button();
        private readonly Label _lblCloudStatus = new Label();

        public BackupRestoreForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            AuthorizationService.Require(
                AuthorizationService.CanManageBackups(_currentUser),
                "Backup/restore bo'limi faqat admin uchun.");

            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            LoadBackups();
            LoadCloudBackupState();
        }

        private void InitializeComponent()
        {
            Text = "Backup va Restore";
            Size = new Size(1160, 900);
            MinimumSize = new Size(980, 760);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(239, 242, 248);
            Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 11, FontStyle.Regular);
            DoubleBuffered = true;

            Panel canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _content.BackColor = Color.Transparent;
            canvas.Controls.Add(_content);
            Controls.Add(canvas);

            Panel header = new Panel { Height = 90, BackColor = Color.Transparent };
            Panel titleIcon = new Panel
            {
                Left = 0,
                Top = 4,
                Width = 54,
                Height = 54
            };
            Image? backupHeaderIcon = BrandingAssets.TryLoadAssetImage("tile-backup.png");
            if (backupHeaderIcon != null)
            {
                titleIcon.BackColor = Color.Transparent;
                titleIcon.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = backupHeaderIcon
                });
            }
            else
            {
                titleIcon.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using LinearGradientBrush brush = new LinearGradientBrush(titleIcon.ClientRectangle, Color.FromArgb(12, 151, 186), Color.FromArgb(25, 176, 207), 45f);
                    using GraphicsPath path = RoundedRect(titleIcon.ClientRectangle, 16);
                    e.Graphics.FillPath(brush, path);
                    TextRenderer.DrawText(e.Graphics, "\uE896", UiTheme.IconFont(24), titleIcon.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                };
            }
            Label lblTitle = new Label
            {
                Text = "Backup va Restore",
                AutoSize = true,
                Left = 66,
                Top = 4,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 23, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 42, 65)
            };
            Label lblSub = new Label
            {
                Text = "Ma'lumotlar bazasi va mahsulot rasmlarini birga zaxiralash va tiklash",
                AutoSize = true,
                Left = 68,
                Top = 40,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 89, 116)
            };
            header.Controls.Add(titleIcon);
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            Panel listCard = BuildListCard();
            Panel actionCard = BuildActionCard();

            _btnCreate.Click += (s, e) =>
            {
                try
                {
                    string path = _backupService.CreateBackup();
                    MessageBox.Show($"Backup yaratildi:\n{Path.GetFileName(path)}", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadBackups();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            _btnRestore.Click += (s, e) =>
            {
                if (_listBackups.SelectedItem is not BackupItem selected)
                {
                    MessageBox.Show("Avval backup faylni tanlang.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show(
                    "Tanlangan backup tiklansa joriy ma'lumotlar almashtiriladi. Davom etilsinmi?",
                    "Tasdiq",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    _backupService.RestoreBackup(selected.FullPath);
                    MessageBox.Show("Backup tiklandi. O'zgarishlar to'liq qo'llanishi uchun dastur qayta ishga tushiriladi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Application.Restart();
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            _btnRefresh.Click += (s, e) => LoadBackups();
            _btnCloudSetup.Click += (s, e) => ConfigureCloudBackup();
            _btnCloudUpload.Click += async (s, e) => await UploadCloudBackupNowAsync();
            _btnCloudRestore.Click += async (s, e) => await RestoreCloudBackupAsync();

            _content.Controls.Add(actionCard);
            _content.Controls.Add(listCard);
            _content.Controls.Add(header);

            Action applyLayout = () =>
            {
                int w = Math.Min(1120, canvas.ClientSize.Width - 60);
                int x = (canvas.ClientSize.Width - w) / 2;
                _content.SetBounds(Math.Max(12, x), 14, w, Math.Max(700, canvas.ClientSize.Height - 24));
                header.SetBounds(0, 0, _content.Width, 90);
                int listHeight = Math.Max(280, Math.Min(420, _content.Height - 388));
                listCard.SetBounds(0, 104, _content.Width, listHeight);
                actionCard.SetBounds(0, listCard.Bottom + 14, _content.Width, _content.Height - (listCard.Bottom + 14));
            };

            canvas.Resize += (s, e) => applyLayout();
            Load += (s, e) => applyLayout();
            Shown += (s, e) => applyLayout();
            applyLayout();
        }

        private Panel BuildListCard()
        {
            Panel card = NewCard();
            Panel bar = NewBar("Mavjud backup fayllar", Color.FromArgb(12, 151, 186), Color.FromArgb(25, 176, 207), "\uE896", "tile-backup.png");
            bar.Dock = DockStyle.Top;

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(26, 18, 26, 22) };

            Panel listHolder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            listHolder.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(Color.FromArgb(250, 252, 255));
                using Pen border = new Pen(Color.FromArgb(220, 228, 240));
                using GraphicsPath path = RoundedRect(listHolder.ClientRectangle, 12);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            listHolder.Resize += (s, e) => listHolder.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, listHolder.Width), Math.Max(1, listHolder.Height)), 12));

            _listBackups.BorderStyle = BorderStyle.None;
            _listBackups.DrawMode = DrawMode.OwnerDrawFixed;
            _listBackups.ItemHeight = 78;
            _listBackups.Dock = DockStyle.Fill;
            _listBackups.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 12, FontStyle.Regular);
            _listBackups.BackColor = Color.FromArgb(250, 252, 255);
            _listBackups.DrawItem += ListBackups_DrawItem;

            listHolder.Controls.Add(_listBackups);
            body.Controls.Add(listHolder);
            card.Controls.Add(body);
            card.Controls.Add(bar);
            return card;
        }

        private Panel BuildActionCard()
        {
            Panel card = NewCard();

            _btnCreate.Text = "Backup Yaratish";
            StyleActionButton(_btnCreate, Color.FromArgb(7, 184, 70), Color.FromArgb(8, 174, 68), "\uE74D");
            _btnCreate.SetBounds(30, 24, 332, 56);

            _btnRestore.Text = "Tanlanganni Tiklash";
            StyleActionButton(_btnRestore, Color.FromArgb(235, 59, 74), Color.FromArgb(219, 40, 58), "\uE777");
            _btnRestore.SetBounds(380, 24, 332, 56);

            _btnRefresh.Text = "Yangilash";
            StyleActionButton(_btnRefresh, Color.FromArgb(51, 115, 246), Color.FromArgb(58, 136, 247), "\uE72C");
            _btnRefresh.SetBounds(730, 24, 332, 56);

            Panel note = new Panel
            {
                Left = 30,
                Top = 106,
                Width = 1032,
                Height = 88,
                BackColor = Color.White
            };
            note.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(Color.FromArgb(245, 250, 255));
                using Pen border = new Pen(Color.FromArgb(196, 217, 246));
                using GraphicsPath path = RoundedRect(note.ClientRectangle, 10);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            note.Resize += (s, e) => note.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, note.Width), Math.Max(1, note.Height)), 10));

            Label lblNote = new Label
            {
                Left = 18,
                Top = 12,
                Width = 1000,
                Height = 44,
                Text = "Mahalliy backup ma'lumotlar bazasi va mahsulot rasmlarini saqlaydi. Tiklash joriy ma'lumotlarni almashtiradi. Drive nusxasi rozilikdan keyin shifrlangan holda yuboriladi.",
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(47, 89, 166),
                BackColor = Color.Transparent
            };
            note.Controls.Add(lblNote);
            _lblCloudStatus.SetBounds(18, 60, 1000, 24);
            _lblCloudStatus.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11, FontStyle.Bold);
            _lblCloudStatus.BackColor = Color.Transparent;
            note.Controls.Add(_lblCloudStatus);

            _btnCloudSetup.Text = "Oylik Drive zaxirasini sozlash";
            StyleActionButton(_btnCloudSetup, Color.FromArgb(98, 79, 205), Color.FromArgb(72, 60, 177), "\uE713");
            _btnCloudSetup.SetBounds(30, 210, 332, 48);

            _btnCloudUpload.Text = "Drive'ga hozir yuborish";
            StyleActionButton(_btnCloudUpload, Color.FromArgb(12, 151, 186), Color.FromArgb(25, 176, 207), "\uE72A");
            _btnCloudUpload.SetBounds(380, 210, 332, 48);

            _btnCloudRestore.Text = "Drive'dan tiklash";
            StyleActionButton(_btnCloudRestore, Color.FromArgb(235, 130, 35), Color.FromArgb(214, 103, 23), "\uE777");
            _btnCloudRestore.SetBounds(730, 210, 332, 48);

            card.Controls.Add(_btnCreate);
            card.Controls.Add(_btnRestore);
            card.Controls.Add(_btnRefresh);
            card.Controls.Add(note);
            card.Controls.Add(_btnCloudSetup);
            card.Controls.Add(_btnCloudUpload);
            card.Controls.Add(_btnCloudRestore);

            card.Resize += (s, e) =>
            {
                int gap = 16;
                int w = (card.ClientSize.Width - 60 - gap * 2) / 3;
                _btnCreate.SetBounds(30, 24, w, 56);
                _btnRestore.SetBounds(30 + w + gap, 24, w, 56);
                _btnRefresh.SetBounds(30 + (w + gap) * 2, 24, w, 56);
                note.SetBounds(30, 98, card.ClientSize.Width - 60, 92);
                lblNote.Width = note.Width - 30;
                _lblCloudStatus.Width = note.Width - 36;
                _btnCloudSetup.SetBounds(30, 202, w, 48);
                _btnCloudUpload.SetBounds(30 + w + gap, 202, w, 48);
                _btnCloudRestore.SetBounds(30 + (w + gap) * 2, 202, w, 48);
            };

            return card;
        }

        private void LoadCloudBackupState()
        {
            bool enabled = _cloudBackupCrypto.IsEnabled();
            _lblCloudStatus.Text = enabled
                ? "Oylik markaziy Drive zaxirasi: YOQILGAN"
                : "Oylik markaziy Drive zaxirasi: O'CHIRILGAN";
            _lblCloudStatus.ForeColor = enabled
                ? Color.FromArgb(26, 132, 72)
                : Color.FromArgb(106, 120, 142);
            _btnCloudSetup.Text = enabled
                ? "Drive zaxirasini o'chirish"
                : "Oylik Drive zaxirasini yoqish";
            _btnCloudUpload.Enabled = enabled;
            _btnCloudRestore.Enabled = true;
        }

        private void ConfigureCloudBackup()
        {
            if (_cloudBackupCrypto.IsEnabled())
            {
                if (MessageBox.Show(
                    "Kelgusi oylik Drive yuklashlarini to'xtatamizmi? Drive'dagi hozirgi nusxa o'chirilmaydi.",
                    "Drive zaxirasi",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _cloudBackupCrypto.Disable();
                    LoadCloudBackupState();
                }

                return;
            }

            DialogResult consent = MessageBox.Show(
                "Rozilik bersangiz, ushbu qurilmaning ma'lumotlar bazasi va mahsulot rasmlari oyiga bir marta OsonTrack markaziy serveri orqali dastur egasining Google Drive hisobiga yuboriladi. Fayl Google'ga yuborilishidan oldin shifrlanadi. Har bir litsenziya va qurilma uchun faqat eng yangi tekshirilgan nusxa qoldiriladi; yangi nusxa tasdiqlangach eskilari o'chiriladi. Tiklash kalitini saqlamasangiz, kompyuter yo'qolganida zaxirani ochib bo'lmaydi. Davom etasizmi?",
                "Bulut zaxirasi uchun rozilik",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (consent != DialogResult.Yes)
            {
                return;
            }

            string recoveryKey = _cloudBackupCrypto.Enable();
            LoadCloudBackupState();
            if (!string.IsNullOrWhiteSpace(recoveryKey))
            {
                ShowRecoveryKey(recoveryKey);
            }
            else
            {
                MessageBox.Show(
                    "Avvalgi tiklash kaliti ushbu Windows hisobida saqlangan. Agar kompyuter almashtirilsa, Drive zaxirasini ochish uchun o'sha tiklash kaliti kerak bo'ladi.",
                    "Drive zaxirasi yoqildi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private async Task UploadCloudBackupNowAsync()
        {
            if (!_cloudBackupCrypto.IsEnabled())
            {
                MessageBox.Show("Avval oylik Drive zaxirasiga rozilik bering.", "Drive zaxirasi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_activationService.TryGetValidLocalActivation(out LocalActivationRecord? activation, out string message) ||
                activation == null)
            {
                MessageBox.Show(message, "Aktivatsiya", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnCloudUpload.Enabled = false;
            try
            {
                bool uploaded = await _cloudSyncService.UploadBackupNowIfEnabledAsync(activation, Application.ProductVersion);
                MessageBox.Show(
                    uploaded
                        ? "Shifrlangan backup Google Drive'ga yuborildi. Oldingi nusxalar yangi fayl tekshirilgandan keyin tozalandi."
                        : "Backup yuborilmadi. Internet yoki serverdagi Google Drive ulanishini tekshiring.",
                    "Drive zaxirasi",
                    MessageBoxButtons.OK,
                    uploaded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Drive'ga yuborishda xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                LoadCloudBackupState();
            }
        }

        private async Task RestoreCloudBackupAsync()
        {
            if (!_activationService.TryGetValidLocalActivation(out LocalActivationRecord? activation, out string message) ||
                activation == null)
            {
                MessageBox.Show(message, "Aktivatsiya", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(
                "Bulutdagi backup yuklanadi va uning ma'lumotlar bazasi hamda mahsulot rasmlari joriy ma'lumotlarning o'rniga tiklanadi. Avval joriy baza uchun mahalliy backup yaratiladi. Davom etilsinmi?",
                "Drive'dan tiklash",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            string? stagedBackupPath = null;
            byte[]? encryptedBytes = null;
            byte[]? archiveBytes = null;
            _btnCloudRestore.Enabled = false;
            try
            {
                encryptedBytes = await _cloudSyncService.DownloadLatestBackupAsync(activation);
                try
                {
                    archiveBytes = _cloudBackupCrypto.Decrypt(encryptedBytes);
                }
                catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
                {
                    string? recoveryKey = PromptForRecoveryKey();
                    if (string.IsNullOrWhiteSpace(recoveryKey))
                    {
                        return;
                    }

                    byte[] recoveryBytes = Convert.FromBase64String(recoveryKey.Trim());
                    try
                    {
                        archiveBytes = CloudBackupCryptoService.DecryptPayload(encryptedBytes, recoveryBytes);
                        _cloudBackupCrypto.ImportRecoveryKey(recoveryKey);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(recoveryBytes);
                    }
                }

                _ = _backupService.CreateBackup();
                stagedBackupPath = Path.Combine(Database.GetAppDataRoot(), $".cloud_restore_{Guid.NewGuid():N}.zip");
                await File.WriteAllBytesAsync(stagedBackupPath, archiveBytes);
                _backupService.RestoreBackup(stagedBackupPath);
                MessageBox.Show(
                    "Drive backup tiklandi. Dastur qayta ishga tushadi.",
                    "Tiklash tugadi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Application.Restart();
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Drive'dan tiklashda xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (encryptedBytes != null)
                {
                    CryptographicOperations.ZeroMemory(encryptedBytes);
                }

                if (archiveBytes != null)
                {
                    CryptographicOperations.ZeroMemory(archiveBytes);
                }

                if (!string.IsNullOrWhiteSpace(stagedBackupPath) && File.Exists(stagedBackupPath))
                {
                    File.Delete(stagedBackupPath);
                }

                LoadCloudBackupState();
            }
        }

        private static string? PromptForRecoveryKey()
        {
            using var prompt = new Form
            {
                Text = "Drive backup tiklash kaliti",
                Width = 620,
                Height = 190,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var input = new TextBox { Left = 18, Top = 24, Width = 566 };
            var confirm = new Button { Text = "Davom etish", Left = 380, Top = 72, Width = 120, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Bekor qilish", Left = 505, Top = 72, Width = 80, DialogResult = DialogResult.Cancel };
            prompt.Controls.Add(input);
            prompt.Controls.Add(confirm);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = confirm;
            prompt.CancelButton = cancel;
            return prompt.ShowDialog() == DialogResult.OK ? input.Text : null;
        }

        private static void ShowRecoveryKey(string recoveryKey)
        {
            using var prompt = new Form
            {
                Text = "Tiklash kalitini saqlang",
                Width = 700,
                Height = 240,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var instruction = new Label
            {
                Left = 18,
                Top = 18,
                Width = 650,
                Height = 54,
                Text = "Bu kalit faqat bir marta ko'rsatiladi. Uni xavfsiz joyda saqlang. Kompyuter almashtirilsa yoki Windows qayta o'rnatilsa, Drive backup'ni ochish uchun kalit kerak."
            };
            var keyBox = new TextBox { Left = 18, Top = 78, Width = 650, ReadOnly = true, Text = recoveryKey };
            var close = new Button { Text = "Yopish", Left = 568, Top = 128, Width = 100, DialogResult = DialogResult.OK };
            prompt.Controls.Add(instruction);
            prompt.Controls.Add(keyBox);
            prompt.Controls.Add(close);
            prompt.AcceptButton = close;
            prompt.ShowDialog();
        }

        private void ListBackups_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _listBackups.Items.Count)
            {
                return;
            }

            if (_listBackups.Items[e.Index] is not BackupItem item)
            {
                return;
            }

            Rectangle rect = e.Bounds;
            rect.Inflate(-8, -6);
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (SolidBrush clear = new SolidBrush(_listBackups.BackColor))
            {
                e.Graphics.FillRectangle(clear, e.Bounds);
            }

            Color fill = selected ? Color.FromArgb(24, 173, 204) : Color.White;
            Color border = selected ? Color.FromArgb(8, 149, 180) : Color.FromArgb(226, 232, 242);
            Color titleColor = selected ? Color.White : Color.FromArgb(45, 59, 80);
            Color dateColor = selected ? Color.FromArgb(223, 245, 253) : Color.FromArgb(106, 120, 142);
            Color iconColor = selected ? Color.White : Color.FromArgb(19, 176, 213);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border))
            using (GraphicsPath path = RoundedRect(rect, 12))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                item.FileName,
                new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 14, FontStyle.Bold),
                new Rectangle(rect.X + 16, rect.Y + 12, rect.Width - 84, 30),
                titleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(
                e.Graphics,
                item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 12, FontStyle.Regular),
                new Rectangle(rect.X + 16, rect.Y + 42, rect.Width - 84, 24),
                dateColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(
                e.Graphics,
                "\uE8B7",
                UiTheme.IconFont(16),
                new Rectangle(rect.Right - 50, rect.Y + (rect.Height - 32) / 2, 32, 32),
                iconColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
            {
                e.DrawFocusRectangle();
            }
        }

        private void LoadBackups()
        {
            _listBackups.DataSource = null;

            var items = _backupService
                .ListBackups()
                .ConvertAll(path => new BackupItem(path));

            _listBackups.DisplayMember = nameof(BackupItem.DisplayText);
            _listBackups.ValueMember = nameof(BackupItem.FullPath);
            _listBackups.DataSource = items;
            if (items.Count > 0)
            {
                _listBackups.SelectedIndex = 0;
            }
        }

        private static void StyleActionButton(Button b, Color c1, Color c2, string glyph)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 15, FontStyle.Bold);
            b.ForeColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.Paint += (s, e) =>
            {
                if (s is not Button btn)
                {
                    return;
                }
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using LinearGradientBrush brush = new LinearGradientBrush(btn.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(btn.ClientRectangle, 10);
                e.Graphics.FillPath(brush, path);

                Rectangle iconRect = new Rectangle(18, (btn.Height - 20) / 2, 20, 20);
                TextRenderer.DrawText(
                    e.Graphics,
                    glyph,
                    UiTheme.IconFont(14),
                    iconRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                Rectangle textRect = new Rectangle(44, 0, btn.Width - 44, btn.Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    btn.Text,
                    btn.Font,
                    textRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            b.Resize += (s, e) => b.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, b.Width), Math.Max(1, b.Height)), 10));
        }

        private static Panel NewCard()
        {
            Panel panel = new Panel { BackColor = Color.White };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(220, 227, 239));
                using GraphicsPath path = RoundedRect(panel.ClientRectangle, 14);
                e.Graphics.FillPath(fill, path);
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
                Left = 44,
                Top = 15,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 15, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            bar.Controls.Add(icon);
            bar.Controls.Add(title);
            return bar;
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

        private sealed class BackupItem
        {
            public BackupItem(string fullPath)
            {
                FullPath = fullPath;
                FileName = Path.GetFileName(fullPath);
                CreatedAt = File.GetCreationTime(fullPath);
                DisplayText = FileName;
            }

            public string FullPath { get; }
            public string FileName { get; }
            public DateTime CreatedAt { get; }
            public string DisplayText { get; }
        }
    }
}
