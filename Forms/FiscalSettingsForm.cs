using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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
        private readonly TextBox _txtVat = new TextBox();
        private readonly CheckBox _chkVat = new CheckBox();
        private readonly Label _lblVat = new Label();
        private Panel? _vatInputShell;

        private readonly Label _lblPreviewBusiness = new Label();
        private readonly Label _lblPreviewTin = new Label();
        private readonly Label _lblPreviewAddress = new Label();
        private readonly Label _lblPreviewKkm = new Label();
        private readonly Panel _previewBox = new Panel();

        public FiscalSettingsForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            AuthorizationService.Require(
                AuthorizationService.CanManageBackups(_currentUser),
                "Chek rekvizitlari faqat admin tomonidan sozlanadi.");

            InitializeComponent();
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
            BackColor = Color.FromArgb(236, 241, 249);
            DoubleBuffered = true;

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(248, 251, 255)
            };
            header.Paint += (_, e) =>
            {
                using Pen border = new Pen(Color.FromArgb(209, 221, 238));
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
                BackColor = Color.FromArgb(236, 241, 249),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(root);

            Panel iconWrap = new Panel
            {
                Left = 22,
                Top = 14,
                Width = 36,
                Height = 36,
                BackColor = Color.FromArgb(54, 92, 247)
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
                using SolidBrush fill = new SolidBrush(iconWrap.BackColor);
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
                    TextAlign = ContentAlignment.MiddleCenter
                };
                iconWrap.Controls.Add(icon);
            }

            header.Controls.Add(new Label
            {
                Text = "Chek Rekvizitlari",
                Left = 70,
                Top = 13,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 15.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 46, 72),
                BackColor = Color.Transparent
            });

            header.Controls.Add(new Label
            {
                Text = "Chek va faktura uchun sozlamalar",
                Left = 70,
                Top = 39,
                AutoSize = true,
                Font = new Font("Bahnschrift", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(89, 108, 137),
                BackColor = Color.Transparent
            });

            Panel card = CreateCard(32, 24, 604, 532);
            root.Controls.Add(card);

            AddLabel(card, "Tadbirkor nomi:", 24);
            CreateInputShell(card, _txtBusiness, "Masalan: Baraka Savdo MCHJ", 44);

            AddLabel(card, "#  STIR:", 112);
            CreateInputShell(card, _txtTin, "Masalan: 307845921", 132);

            AddLabel(card, "Do'kon manzili:", 200);
            CreateInputShell(card, _txtAddress, "Masalan: Toshkent shahri, Chilonzor 12-mavze", 220);

            AddLabel(card, "KKM raqami:", 288);
            CreateInputShell(card, _txtKkm, "Masalan: KKM-009874", 308);

            Panel separator = new Panel
            {
                Left = 24,
                Top = 360,
                Width = 556,
                Height = 1,
                BackColor = Color.FromArgb(222, 230, 242)
            };
            card.Controls.Add(separator);

            Panel vatPanel = new Panel
            {
                Left = 24,
                Top = 376,
                Width = 556,
                Height = 92,
                BackColor = Color.FromArgb(227, 237, 252)
            };
            vatPanel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, vatPanel.Width - 1, vatPanel.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 13);
                using SolidBrush fill = new SolidBrush(vatPanel.BackColor);
                using Pen border = new Pen(Color.FromArgb(170, 197, 238));
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            vatPanel.Resize += (_, __) =>
                vatPanel.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, vatPanel.Width), Math.Max(1, vatPanel.Height)), 13));
            card.Controls.Add(vatPanel);

            _chkVat.Left = 16;
            _chkVat.Top = 16;
            _chkVat.AutoSize = true;
            _chkVat.Text = "QQS to'lovchi";
            _chkVat.Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold);
            _chkVat.ForeColor = Color.FromArgb(31, 49, 80);
            _chkVat.BackColor = Color.Transparent;
            _chkVat.CheckedChanged += VatChanged;
            vatPanel.Controls.Add(_chkVat);

            vatPanel.Controls.Add(new Label
            {
                Text = "Agar QQS to'lovangiz, ushbu katakchani belgilang",
                Left = 38,
                Top = 45,
                AutoSize = true,
                Font = new Font("Bahnschrift", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(68, 89, 122),
                BackColor = Color.Transparent
            });

            _lblVat.Text = "QQS foizi (%):";
            _lblVat.Left = 350;
            _lblVat.Top = 14;
            _lblVat.AutoSize = true;
            _lblVat.Font = new Font("Bahnschrift", 9.5f, FontStyle.Regular);
            _lblVat.ForeColor = Color.FromArgb(55, 78, 111);
            _lblVat.BackColor = Color.Transparent;
            vatPanel.Controls.Add(_lblVat);

            _vatInputShell = CreateInputShell(vatPanel, _txtVat, "12", 38, 190);
            _vatInputShell.Left = 350;

            Button btnSave = new Button
            {
                Left = 460,
                Top = 478,
                Width = 120,
                Height = 38,
                Text = "Saqlash",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Bahnschrift SemiBold", 10.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
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

            Panel previewCard = CreateCard(32, 566, 604, 198);
            root.Controls.Add(previewCard);

            previewCard.Controls.Add(new Label
            {
                Text = "Chek namunasi:",
                Left = 22,
                Top = 18,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 45, 73),
                BackColor = Color.Transparent
            });

            _previewBox.Left = 22;
            _previewBox.Top = 44;
            _previewBox.Width = 560;
            _previewBox.Height = 124;
            _previewBox.BackColor = Color.FromArgb(250, 252, 255);
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

            _lblPreviewBusiness.SetBounds(0, 16, _previewBox.Width, 20);
            _lblPreviewBusiness.Font = new Font("Consolas", 11f, FontStyle.Bold);
            _lblPreviewBusiness.ForeColor = Color.FromArgb(22, 37, 65);
            _lblPreviewBusiness.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewBusiness.BackColor = Color.Transparent;
            _previewBox.Controls.Add(_lblPreviewBusiness);

            _lblPreviewTin.SetBounds(0, 40, _previewBox.Width, 18);
            _lblPreviewTin.Font = new Font("Consolas", 10f, FontStyle.Regular);
            _lblPreviewTin.ForeColor = Color.FromArgb(66, 83, 111);
            _lblPreviewTin.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewTin.BackColor = Color.Transparent;
            _previewBox.Controls.Add(_lblPreviewTin);

            _lblPreviewAddress.SetBounds(0, 60, _previewBox.Width, 18);
            _lblPreviewAddress.Font = new Font("Consolas", 10f, FontStyle.Regular);
            _lblPreviewAddress.ForeColor = Color.FromArgb(66, 83, 111);
            _lblPreviewAddress.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewAddress.BackColor = Color.Transparent;
            _previewBox.Controls.Add(_lblPreviewAddress);

            _lblPreviewKkm.SetBounds(0, 80, _previewBox.Width, 18);
            _lblPreviewKkm.Font = new Font("Consolas", 10f, FontStyle.Regular);
            _lblPreviewKkm.ForeColor = Color.FromArgb(66, 83, 111);
            _lblPreviewKkm.TextAlign = ContentAlignment.MiddleCenter;
            _lblPreviewKkm.BackColor = Color.Transparent;
            _previewBox.Controls.Add(_lblPreviewKkm);

            Panel dottedLine = new Panel { Left = 20, Top = 98, Width = 520, Height = 1 };
            dottedLine.Paint += (_, e) =>
            {
                using Pen pen = new Pen(Color.FromArgb(174, 196, 226)) { DashStyle = DashStyle.Dot };
                e.Graphics.DrawLine(pen, 0, 0, dottedLine.Width, 0);
            };
            _previewBox.Controls.Add(dottedLine);

            _txtBusiness.TextChanged += (_, __) => RefreshSample();
            _txtTin.TextChanged += (_, __) => RefreshSample();
            _txtAddress.TextChanged += (_, __) => RefreshSample();
            _txtKkm.TextChanged += (_, __) => RefreshSample();

            AlignPreviewLabels();
            VatChanged(null, EventArgs.Empty);
            RefreshSample();
        }

        private void AlignPreviewLabels()
        {
            int w = _previewBox.Width;
            _lblPreviewBusiness.SetBounds(0, 16, w, 20);
            _lblPreviewTin.SetBounds(0, 40, w, 18);
            _lblPreviewAddress.SetBounds(0, 60, w, 18);
            _lblPreviewKkm.SetBounds(0, 80, w, 18);
        }

        private static Panel CreateCard(int left, int top, int width, int height)
        {
            Panel card = new Panel
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                BackColor = Color.FromArgb(247, 250, 255)
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
                using Pen border = new Pen(Color.FromArgb(209, 220, 236));
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
                Font = new Font("Bahnschrift", 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(42, 60, 90),
                BackColor = Color.Transparent
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
                BackColor = Color.White
            };
            shell.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(shell.Parent?.BackColor ?? shell.BackColor);
                Rectangle rect = new Rectangle(0, 0, shell.Width - 1, shell.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 10);
                using SolidBrush fill = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(181, 198, 223));
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            shell.Resize += (_, __) =>
                shell.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, shell.Width), Math.Max(1, shell.Height)), 10));

            box.BorderStyle = BorderStyle.None;
            box.Left = 12;
            box.Top = 8;
            box.Width = shell.Width - 24;
            box.Font = new Font("Bahnschrift", 10.5f, FontStyle.Regular);
            box.ForeColor = Color.FromArgb(37, 55, 86);
            box.BackColor = Color.White;
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
            _lblPreviewBusiness.Text = Fit(string.IsNullOrWhiteSpace(_txtBusiness.Text) ? "[Tadbirkor nomi]" : _txtBusiness.Text, 24);
            _lblPreviewTin.Text = $"STIR: {Fit(string.IsNullOrWhiteSpace(_txtTin.Text) ? "[STIR raqami]" : _txtTin.Text, 18)}";
            _lblPreviewAddress.Text = Fit(string.IsNullOrWhiteSpace(_txtAddress.Text) ? "[Do'kon manzili]" : _txtAddress.Text, 28);
            _lblPreviewKkm.Text = $"KKM: {Fit(string.IsNullOrWhiteSpace(_txtKkm.Text) ? "[KKM raqami]" : _txtKkm.Text, 18)}";
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
            _chkVat.Checked = s.IsVatPayer;
            _txtVat.Text = s.VatRatePercent.ToString("0.##", CultureInfo.InvariantCulture);
            RefreshSample();
            VatChanged(null, EventArgs.Empty);
        }

        private void Save_Click(object? sender, EventArgs e)
        {
            double vat = 0;
            if (_chkVat.Checked && !double.TryParse(_txtVat.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out vat))
            {
                double.TryParse(_txtVat.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out vat);
            }

            _service.Save(new FiscalSettings
            {
                Id = 1,
                BusinessName = _txtBusiness.Text,
                TIN = _txtTin.Text,
                StoreAddress = _txtAddress.Text,
                KkmNumber = _txtKkm.Text,
                IsVatPayer = _chkVat.Checked,
                VatRatePercent = vat
            }, _currentUser);

            MessageBox.Show("Chek rekvizitlari saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}



