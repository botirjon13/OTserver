using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class FiscalSettingsForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly FiscalSettingsService _service = new FiscalSettingsService();

        private readonly TextBox _txtBusiness = new TextBox();
        private readonly TextBox _txtTin = new TextBox();
        private readonly TextBox _txtAddress = new TextBox();
        private readonly TextBox _txtKkm = new TextBox();
        private readonly TextBox _txtPhone = new TextBox();
        private readonly TextBox _txtThankYou = new TextBox();
        private readonly TextBox _txtVat = new TextBox();
        private readonly CheckBox _chkVat = new CheckBox();
        private readonly Label _lblVat = new Label();
        private Panel? _vatInputShell;
        private readonly PictureBox _logoPreview = new PictureBox();
        private byte[] _logoPng = Array.Empty<byte>();

        private readonly Label _lblPreviewBusiness = new Label();
        private readonly Label _lblPreviewTin = new Label();
        private readonly Label _lblPreviewAddress = new Label();
        private readonly Label _lblPreviewPhone = new Label();
        private readonly Label _lblPreviewSeller = new Label();
        private readonly Label _lblPreviewThankYou = new Label();
        private readonly PictureBox _sampleLogo = new PictureBox();
        private readonly ComboBox _cmbPreviewPaper = new ComboBox();
        private readonly Panel _previewSeparator = new Panel();
        private readonly Panel _previewBox = new Panel();

        public FiscalSettingsForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            AuthorizationService.Require(
                AuthorizationService.CanManageBackups(_currentUser),
                "Chek rekvizitlari faqat admin tomonidan sozlanadi.");

            InitializeComponent();
            ApplyThemeStyles();
            UiTheme.ThemeChanged += FiscalSettingsForm_ThemeChanged;
            FormClosed += (_, __) => UiTheme.ThemeChanged -= FiscalSettingsForm_ThemeChanged;
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            LoadSettings();
        }

        private void InitializeComponent()
        {
            Text = "Chek Rekvizitlari";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(668, 808);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = UiTheme.BackgroundSecondary;
            DoubleBuffered = true;

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = UiTheme.Card,
                Tag = "theme-header"
            };
            header.Paint += (_, e) =>
            {
                using Pen border = new Pen(UiTheme.Border);
                e.Graphics.DrawLine(border, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Controls.Add(header);

            Panel root = new Panel
            {
                Left = 0,
                Top = header.Height,
                Width = ClientSize.Width,
                Height = ClientSize.Height - header.Height,
                AutoScroll = true,
                Padding = new Padding(0, 12, 0, 16),
                BackColor = UiTheme.BackgroundSecondary,
                Tag = "theme-root",
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(root);

            Panel iconWrap = new Panel
            {
                Left = 22,
                Top = 14,
                Width = 36,
                Height = 36,
                BackColor = UiTheme.Primary,
                Tag = "theme-icon"
            };
            Image? headerIconImage = BrandingAssets.TryLoadAssetImage("tile-receipt.png");
            iconWrap.Paint += (_, e) =>
            {
                if (headerIconImage != null)
                {
                    return;
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = RoundedRect(new Rectangle(0, 0, iconWrap.Width - 1, iconWrap.Height - 1), 10);
                using SolidBrush fill = new SolidBrush(UiTheme.Primary);
                e.Graphics.FillPath(fill, path);
            };
            iconWrap.Resize += (_, __) =>
                iconWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, iconWrap.Width), Math.Max(1, iconWrap.Height)), 10));
            header.Controls.Add(iconWrap);

            if (headerIconImage != null)
            {
                iconWrap.BackColor = Color.Transparent;
                iconWrap.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = headerIconImage
                });
            }
            else
            {
                Label icon = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "\uE8A5",
                    Font = UiTheme.IconFont(14),
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Tag = "theme-icon-glyph"
                };
                iconWrap.Controls.Add(icon);
            }

            header.Controls.Add(new Label
            {
                Text = "Chek Rekvizitlari",
                Left = 70,
                Top = 13,
                AutoSize = true,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 15.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 46, 72),
                BackColor = Color.Transparent,
                Tag = "theme-title"
            });

            header.Controls.Add(new Label
            {
                Text = "Chek va faktura uchun sozlamalar",
                Left = 70,
                Top = 39,
                AutoSize = true,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(89, 108, 137),
                BackColor = Color.Transparent,
                Tag = "theme-muted"
            });

            Panel card = CreateCard(32, 24, 604, 700);
            root.Controls.Add(card);

            AddLabel(card, "Do'kon nomi:", 24);
            CreateInputShell(card, _txtBusiness, "Masalan: Baraka Savdo MCHJ", 44);

            AddLabel(card, "STIR:", 98);
            CreateInputShell(card, _txtTin, "Masalan: 307845921", 118);

            AddLabel(card, "Do'kon manzili:", 172);
            CreateInputShell(card, _txtAddress, "Masalan: Toshkent shahri, Chilonzor 12-mavze", 192);

            AddLabel(card, "Telefon raqami:", 246);
            CreateInputShell(card, _txtPhone, "Masalan: +998 90 123 45 67", 266);
            _txtPhone.MaxLength = 64;

            AddLabel(card, "KKM raqami:", 320);
            CreateInputShell(card, _txtKkm, "Masalan: KKM-009874", 340);

            AddLabel(card, "Do'kon logotipi (PNG, 1 MB gacha):", 390);
            _logoPreview.SetBounds(24, 416, 100, 54);
            _logoPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _logoPreview.BackColor = Color.White;
            _logoPreview.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(_logoPreview);

            Button btnChooseLogo = new Button
            {
                Text = "PNG tanlash",
                Left = 138,
                Top = 416,
                Width = 120,
                Height = 34,
                Tag = "theme-button"
            };
            btnChooseLogo.Click += ChooseLogo_Click;
            card.Controls.Add(btnChooseLogo);

            Button btnRemoveLogo = new Button
            {
                Text = "Logoni o'chirish",
                Left = 268,
                Top = 416,
                Width = 130,
                Height = 34,
                Tag = "theme-button"
            };
            btnRemoveLogo.Click += (_, __) =>
            {
                _logoPng = Array.Empty<byte>();
                SetLogoPreview(_logoPng);
            };
            card.Controls.Add(btnRemoveLogo);

            AddLabel(card, "Chek oxiridagi rahmatnoma (ixtiyoriy):", 472);
            Panel thanksShell = CreateInputShell(card, _txtThankYou, "Masalan: Xaridingiz uchun rahmat!", 492);
            thanksShell.Height = 54;
            _txtThankYou.Multiline = true;
            _txtThankYou.MaxLength = 500;
            _txtThankYou.ScrollBars = ScrollBars.Vertical;
            _txtThankYou.SetBounds(12, 7, thanksShell.Width - 24, 40);

            Panel separator = new Panel
            {
                Left = 24,
                Top = 558,
                Width = 556,
                Height = 1,
                BackColor = UiTheme.Border,
                Tag = "theme-separator"
            };
            card.Controls.Add(separator);

            Panel vatPanel = new Panel
            {
                Left = 24,
                Top = 572,
                Width = 556,
                Height = 82,
                BackColor = Color.FromArgb(227, 237, 252),
                Tag = "theme-info"
            };
            vatPanel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, vatPanel.Width - 1, vatPanel.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 13);
                using SolidBrush fill = new SolidBrush(vatPanel.BackColor);
                using Pen border = new Pen(UiTheme.Border);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            vatPanel.Resize += (_, __) =>
                vatPanel.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, vatPanel.Width), Math.Max(1, vatPanel.Height)), 13));
            card.Controls.Add(vatPanel);

            _chkVat.Left = 16;
            _chkVat.Top = 12;
            _chkVat.AutoSize = true;
            _chkVat.Text = "QQS to'lovchi";
            _chkVat.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11f, FontStyle.Bold);
            _chkVat.ForeColor = Color.FromArgb(31, 49, 80);
            _chkVat.BackColor = Color.Transparent;
            _chkVat.Tag = "theme-text";
            _chkVat.CheckedChanged += VatChanged;
            vatPanel.Controls.Add(_chkVat);

            vatPanel.Controls.Add(new Label
            {
                Text = "Agar QQS to'lovangiz, ushbu katakchani belgilang",
                Left = 38,
                Top = 40,
                AutoSize = true,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(68, 89, 122),
                BackColor = Color.Transparent,
                Tag = "theme-muted"
            });

            _lblVat.Text = "QQS foizi (%):";
            _lblVat.Left = 350;
            _lblVat.Top = 10;
            _lblVat.AutoSize = true;
            _lblVat.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 9.5f, FontStyle.Regular);
            _lblVat.ForeColor = Color.FromArgb(55, 78, 111);
            _lblVat.BackColor = Color.Transparent;
            _lblVat.Tag = "theme-muted";
            vatPanel.Controls.Add(_lblVat);

            _vatInputShell = CreateInputShell(vatPanel, _txtVat, "12", 34, 190);
            _vatInputShell.Left = 350;

            Button btnSave = new Button
            {
                Left = 460,
                Top = 648,
                Width = 120,
                Height = 38,
                Text = "Saqlash",
                FlatStyle = FlatStyle.Flat,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 10.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Tag = "theme-primary-button"
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnSave.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btnSave.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(btnSave.Parent?.BackColor ?? BackColor);
                Rectangle rect = new Rectangle(0, 0, btnSave.Width - 1, btnSave.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 12);
                using LinearGradientBrush lg = new LinearGradientBrush(rect, Color.FromArgb(45, 130, 255), Color.FromArgb(73, 54, 233), 0f);
                e.Graphics.FillPath(lg, path);
                TextRenderer.DrawText(e.Graphics, btnSave.Text, btnSave.Font, btnSave.ClientRectangle, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btnSave.Resize += (_, __) =>
                btnSave.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, btnSave.Width), Math.Max(1, btnSave.Height)), 12));
            btnSave.Click += Save_Click;
            card.Controls.Add(btnSave);

            Panel previewCard = CreateCard(32, 740, 604, 254);
            root.Controls.Add(previewCard);

            previewCard.Controls.Add(new Label
            {
                Text = "Chek namunasi:",
                Left = 22,
                Top = 18,
                AutoSize = true,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift SemiBold"), 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 45, 73),
                BackColor = Color.Transparent,
                Tag = "theme-title"
            });

            _cmbPreviewPaper.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPreviewPaper.Items.AddRange(new object[] { "80mm", "58mm" });
            _cmbPreviewPaper.SelectedIndex = 0;
            _cmbPreviewPaper.SetBounds(488, 12, 72, 28);
            _cmbPreviewPaper.SelectedIndexChanged += (_, __) => RefreshSample();
            previewCard.Controls.Add(_cmbPreviewPaper);

            _previewBox.Left = 22;
            _previewBox.Top = 44;
            _previewBox.Width = 560;
            _previewBox.Height = 192;
            _previewBox.BackColor = Color.White;
            _previewBox.Tag = "theme-paper";
            _previewBox.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(_previewBox.Parent?.BackColor ?? BackColor);
                Rectangle rect = new Rectangle(0, 0, _previewBox.Width - 1, _previewBox.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 12);
                using SolidBrush fill = new SolidBrush(_previewBox.BackColor);
                using Pen border = new Pen(Color.FromArgb(184, 203, 230)) { DashStyle = DashStyle.Dot };
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            _previewBox.Resize += (_, __) =>
            {
                _previewBox.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _previewBox.Width), Math.Max(1, _previewBox.Height)), 12));
                AlignPreviewLabels();
            };
            previewCard.Controls.Add(_previewBox);

            _sampleLogo.SetBounds(0, 6, 90, 38);
            _sampleLogo.SizeMode = PictureBoxSizeMode.Zoom;
            _sampleLogo.BackColor = Color.Transparent;
            _sampleLogo.Visible = false;
            _previewBox.Controls.Add(_sampleLogo);

            _lblPreviewBusiness.SetBounds(0, 46, _previewBox.Width, 18);
            _lblPreviewBusiness.Font = new Font("Consolas", 11f, FontStyle.Bold);
            _lblPreviewBusiness.ForeColor = Color.FromArgb(22, 37, 65);
            _lblPreviewBusiness.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewBusiness.BackColor = Color.Transparent;
            _lblPreviewBusiness.Tag = "paper-ink";
            _previewBox.Controls.Add(_lblPreviewBusiness);

            _lblPreviewTin.SetBounds(0, 65, _previewBox.Width, 16);
            _lblPreviewTin.Font = new Font("Consolas", 10f, FontStyle.Regular);
            _lblPreviewTin.ForeColor = Color.FromArgb(66, 83, 111);
            _lblPreviewTin.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewTin.BackColor = Color.Transparent;
            _lblPreviewTin.Tag = "paper-ink";
            _previewBox.Controls.Add(_lblPreviewTin);

            _lblPreviewAddress.SetBounds(0, 82, _previewBox.Width, 16);
            _lblPreviewAddress.Font = new Font("Consolas", 10f, FontStyle.Regular);
            _lblPreviewAddress.ForeColor = Color.FromArgb(66, 83, 111);
            _lblPreviewAddress.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewAddress.BackColor = Color.Transparent;
            _lblPreviewAddress.Tag = "paper-ink";
            _previewBox.Controls.Add(_lblPreviewAddress);

            _lblPreviewPhone.SetBounds(0, 99, _previewBox.Width, 16);
            _lblPreviewPhone.Font = new Font("Consolas", 10f, FontStyle.Regular);
            _lblPreviewPhone.ForeColor = Color.FromArgb(66, 83, 111);
            _lblPreviewPhone.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewPhone.BackColor = Color.Transparent;
            _lblPreviewPhone.Tag = "paper-ink";
            _previewBox.Controls.Add(_lblPreviewPhone);

            _lblPreviewSeller.SetBounds(0, 116, _previewBox.Width, 16);
            _lblPreviewSeller.Font = new Font("Consolas", 10f, FontStyle.Regular);
            _lblPreviewSeller.ForeColor = Color.FromArgb(66, 83, 111);
            _lblPreviewSeller.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewSeller.BackColor = Color.Transparent;
            _lblPreviewSeller.Tag = "paper-ink";
            _previewBox.Controls.Add(_lblPreviewSeller);

            _lblPreviewThankYou.SetBounds(0, 145, _previewBox.Width, 32);
            _lblPreviewThankYou.Font = new Font("Consolas", 10f, FontStyle.Bold);
            _lblPreviewThankYou.ForeColor = Color.FromArgb(22, 37, 65);
            _lblPreviewThankYou.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewThankYou.BackColor = Color.Transparent;
            _lblPreviewThankYou.Tag = "paper-ink";
            _previewBox.Controls.Add(_lblPreviewThankYou);

            _previewSeparator.SetBounds(20, 137, 520, 1);
            _previewSeparator.Paint += (_, e) =>
            {
                using Pen pen = new Pen(Color.FromArgb(174, 196, 226)) { DashStyle = DashStyle.Dot };
                e.Graphics.DrawLine(pen, 0, 0, _previewSeparator.Width, 0);
            };
            _previewBox.Controls.Add(_previewSeparator);

            _txtBusiness.TextChanged += (_, __) => RefreshSample();
            _txtTin.TextChanged += (_, __) => RefreshSample();
            _txtAddress.TextChanged += (_, __) => RefreshSample();
            _txtKkm.TextChanged += (_, __) => RefreshSample();
            _txtPhone.TextChanged += (_, __) => RefreshSample();
            _txtThankYou.TextChanged += (_, __) => RefreshSample();
            FormClosed += (_, __) =>
            {
                _logoPreview.Image?.Dispose();
                _sampleLogo.Image?.Dispose();
            };

            AlignPreviewLabels();
            VatChanged(null, EventArgs.Empty);
            RefreshSample();
        }

        private void AlignPreviewLabels()
        {
            int width = string.Equals(_cmbPreviewPaper.Text, "58mm", StringComparison.OrdinalIgnoreCase)
                ? Math.Min(280, _previewBox.Width)
                : _previewBox.Width;
            int left = (_previewBox.Width - width) / 2;
            _sampleLogo.SetBounds(left + (width - _sampleLogo.Width) / 2, 6, _sampleLogo.Width, 38);
            _lblPreviewBusiness.SetBounds(left, 46, width, 18);
            _lblPreviewTin.SetBounds(left, 65, width, 16);
            _lblPreviewAddress.SetBounds(left, 82, width, 16);
            _lblPreviewPhone.SetBounds(left, 99, width, 16);
            _lblPreviewSeller.SetBounds(left, 116, width, 16);
            _lblPreviewThankYou.SetBounds(left, 145, width, 32);
            _previewSeparator.SetBounds(left + 20, 137, Math.Max(1, width - 40), 1);
        }

        private void FiscalSettingsForm_ThemeChanged(object? sender, EventArgs e)
        {
            ApplyThemeStyles();
        }

        private void ApplyThemeStyles()
        {
            ApplyThemeStyles(this);
            Invalidate(true);
        }

        private static void ApplyThemeStyles(Control control)
        {
            bool isLight = UiTheme.CurrentMode == UiTheme.ThemeMode.Light;
            string tag = control.Tag?.ToString() ?? string.Empty;

            if (control is Form form)
            {
                form.BackColor = UiTheme.BackgroundSecondary;
                form.ForeColor = UiTheme.Text;
            }
            else if (control is Panel panel)
            {
                panel.BackColor = tag switch
                {
                    "theme-header" => UiTheme.Card,
                    "theme-root" => UiTheme.BackgroundSecondary,
                    "theme-card" => UiTheme.Card,
                    "theme-info" => isLight ? Color.FromArgb(227, 237, 252) : Color.FromArgb(29, 47, 64),
                    "theme-input" => isLight ? Color.White : Color.FromArgb(26, 37, 49),
                    "theme-icon" => UiTheme.Primary,
                    "theme-separator" => UiTheme.Border,
                    "theme-paper" => Color.White,
                    _ => panel.BackColor
                };
            }
            else if (control is TextBox textBox)
            {
                textBox.BackColor = isLight ? Color.White : Color.FromArgb(26, 37, 49);
                textBox.ForeColor = UiTheme.Text;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.BackColor = isLight ? Color.White : Color.FromArgb(26, 37, 49);
                comboBox.ForeColor = UiTheme.Text;
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.ForeColor = UiTheme.Text;
            }
            else if (control is Label label && tag != "paper-ink" && tag != "theme-icon-glyph")
            {
                label.ForeColor = tag == "theme-muted" ? UiTheme.Muted : UiTheme.Text;
            }
            else if (control is Button button && tag != "theme-primary-button")
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = UiTheme.Card;
                button.ForeColor = UiTheme.Text;
                button.FlatAppearance.BorderColor = UiTheme.Border;
            }

            foreach (Control child in control.Controls)
            {
                ApplyThemeStyles(child);
            }
        }

        private static Panel CreateCard(int left, int top, int width, int height)
        {
            Panel card = new Panel
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                BackColor = UiTheme.Card,
                Tag = "theme-card"
            };
            card.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(card.Parent?.BackColor ?? card.BackColor);
                Rectangle shadowRect = new Rectangle(4, 5, card.Width - 9, card.Height - 9);
                Rectangle rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using GraphicsPath shadowPath = RoundedRect(shadowRect, 12);
                using GraphicsPath path = RoundedRect(rect, 12);
                using SolidBrush shadow = new SolidBrush(Color.FromArgb(20, 21, 40, 71));
                using SolidBrush fill = new SolidBrush(card.BackColor);
                using Pen border = new Pen(UiTheme.Border);
                e.Graphics.FillPath(shadow, shadowPath);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            card.Resize += (_, __) =>
                card.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, card.Width), Math.Max(1, card.Height)), 12));
            return card;
        }

        private static void AddLabel(Control parent, string text, int top)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Left = 24,
                Top = top,
                AutoSize = true,
                Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(42, 60, 90),
                BackColor = Color.Transparent,
                Tag = "theme-muted"
            });
        }

        private static Panel CreateInputShell(Control parent, TextBox box, string placeholder, int top, int width = 556)
        {
            Panel shell = new Panel
            {
                Left = 24,
                Top = top,
                Width = width,
                Height = 36,
                BackColor = Color.White,
                Tag = "theme-input"
            };
            shell.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(shell.Parent?.BackColor ?? shell.BackColor);
                Rectangle rect = new Rectangle(0, 0, shell.Width - 1, shell.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 10);
                using SolidBrush fill = new SolidBrush(shell.BackColor);
                using Pen border = new Pen(UiTheme.Border);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            shell.Resize += (_, __) =>
                shell.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, shell.Width), Math.Max(1, shell.Height)), 10));

            box.BorderStyle = BorderStyle.None;
            box.Left = 12;
            box.Top = 8;
            box.Width = shell.Width - 24;
            box.Font = new Font(SantexnikaSRM.Utils.UiTheme.ResolveFontFamily("Bahnschrift"), 10.5f, FontStyle.Regular);
            box.ForeColor = UiTheme.Text;
            box.BackColor = shell.BackColor;
            box.PlaceholderText = placeholder;
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            shell.Controls.Add(box);

            parent.Controls.Add(shell);
            return shell;
        }

        private void VatChanged(object? sender, EventArgs e)
        {
            bool visible = _chkVat.Checked;
            _lblVat.Visible = visible;
            _txtVat.Visible = visible;
            if (_vatInputShell != null)
            {
                _vatInputShell.Visible = visible;
            }
            if (visible && string.IsNullOrWhiteSpace(_txtVat.Text))
            {
                _txtVat.Text = "12";
            }
        }

        private void RefreshSample()
        {
            int maxLength = string.Equals(_cmbPreviewPaper.Text, "58mm", StringComparison.OrdinalIgnoreCase) ? 28 : 48;
            AlignPreviewLabels();
            SetOptionalPreview(_lblPreviewBusiness, _txtBusiness.Text, maxLength);
            SetOptionalPreview(_lblPreviewTin, string.IsNullOrWhiteSpace(_txtTin.Text) ? "" : $"STIR: {_txtTin.Text}", maxLength);
            SetOptionalPreview(_lblPreviewAddress, _txtAddress.Text, maxLength);
            SetOptionalPreview(_lblPreviewPhone, _txtPhone.Text, maxLength);
            SetOptionalPreview(
                _lblPreviewSeller,
                string.IsNullOrWhiteSpace(_currentUser.Username) ? "" : $"Sotuvchi: {_currentUser.Username}",
                maxLength);
            _lblPreviewThankYou.Text = Fit(_txtThankYou.Text.Trim(), maxLength);
            _lblPreviewThankYou.Visible = !string.IsNullOrWhiteSpace(_txtThankYou.Text);
        }

        private static void SetOptionalPreview(Label label, string value, int maxLength)
        {
            label.Text = Fit(value.Trim(), maxLength);
            label.Visible = !string.IsNullOrWhiteSpace(value);
        }

        private static string Fit(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, Math.Max(1, max - 3)) + "...";
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

        private void LoadSettings()
        {
            FiscalSettings s = _service.Get(_currentUser);
            _txtBusiness.Text = s.BusinessName;
            _txtTin.Text = s.TIN;
            _txtAddress.Text = s.StoreAddress;
            _txtKkm.Text = s.KkmNumber;
            _txtPhone.Text = s.StorePhone;
            _txtThankYou.Text = s.ThankYouMessage;
            _logoPng = s.LogoPng ?? Array.Empty<byte>();
            SetLogoPreview(_logoPng);
            _chkVat.Checked = s.IsVatPayer;
            _txtVat.Text = s.VatRatePercent.ToString("0.##", CultureInfo.InvariantCulture);
            RefreshSample();
            VatChanged(null, EventArgs.Empty);
        }

        private void Save_Click(object? sender, EventArgs e)
        {
            try
            {
                double vat = 0;
                if (_chkVat.Checked &&
                    !double.TryParse(_txtVat.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out vat) &&
                    !double.TryParse(_txtVat.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out vat))
                {
                    throw new Exception("QQS foizini raqam bilan kiriting.");
                }

                _service.Save(new FiscalSettings
                {
                    Id = 1,
                    BusinessName = _txtBusiness.Text,
                    TIN = _txtTin.Text,
                    StoreAddress = _txtAddress.Text,
                    KkmNumber = _txtKkm.Text,
                    StorePhone = _txtPhone.Text,
                    ThankYouMessage = _txtThankYou.Text,
                    LogoPng = (byte[])_logoPng.Clone(),
                    IsVatPayer = _chkVat.Checked,
                    VatRatePercent = vat
                }, _currentUser);

                MessageBox.Show("Chek rekvizitlari saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chek rekvizitlarini saqlashda xatolik: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ChooseLogo_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Do'kon logotipini tanlang",
                Filter = "PNG rasm (*.png)|*.png",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(dialog.FileName);
                ValidateLogo(bytes);
                _logoPng = bytes;
                SetLogoPreview(_logoPng);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Logotipni yuklashda xatolik: {ex.Message}", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetLogoPreview(byte[] logoPng)
        {
            _logoPreview.Image?.Dispose();
            _sampleLogo.Image?.Dispose();
            _logoPreview.Image = CreateLogoBitmap(logoPng);
            _sampleLogo.Image = CreateLogoBitmap(logoPng);
            _sampleLogo.Visible = _sampleLogo.Image != null;
            RefreshSample();
        }

        private static Bitmap? CreateLogoBitmap(byte[] logoPng)
        {
            if (logoPng.Length == 0)
            {
                return null;
            }

            using var stream = new MemoryStream(logoPng, writable: false);
            using Image image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            return new Bitmap(image);
        }

        private static void ValidateLogo(byte[] logoPng)
        {
            if (logoPng.Length == 0 || logoPng.Length > 1_048_576)
            {
                throw new Exception("PNG fayl 1 MB gacha bo'lishi kerak.");
            }

            using var stream = new MemoryStream(logoPng, writable: false);
            using Image image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            if (image.RawFormat.Guid != System.Drawing.Imaging.ImageFormat.Png.Guid)
            {
                throw new Exception("Faqat haqiqiy PNG formatidagi rasmni tanlang.");
            }
            if (image.Width > 2048 || image.Height > 2048)
            {
                throw new Exception("Logotip o'lchami 2048 x 2048 pikseldan oshmasligi kerak.");
            }
        }
    }
}
