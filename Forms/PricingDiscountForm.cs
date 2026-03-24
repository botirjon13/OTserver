using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class PricingDiscountForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly PricingSettingsService _service = new PricingSettingsService();

        private readonly NumericUpDown _numMarkup = new NumericUpDown();
        private readonly CheckBox _chkAutoFill = new CheckBox();
        private readonly CheckBox _chkQuickDiscountEnabled = new CheckBox();
        private readonly Label _lblState = new Label();
        private readonly Button _btnSave = new Button();
        private readonly Button _btnReset = new Button();

        private readonly Label _lblExampleMarkup = new Label();
        private readonly Label _lblExampleTotal = new Label();

        private bool _isDirty;
        private bool _suspendDirtyTracking;

        public PricingDiscountForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            AuthorizationService.Require(
                AuthorizationService.CanManagePricing(_currentUser),
                "Foiz va chegirmalar bo'limi faqat admin uchun.");

            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            LoadSettings();
        }

        private void InitializeComponent()
        {
            Text = "Foiz va chegirmalar";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(668, 760);
            BackColor = Color.FromArgb(236, 241, 239);
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
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(236, 241, 239)
            };
            Controls.Add(root);

            Panel iconWrap = new Panel
            {
                Left = 24,
                Top = 12,
                Width = 34,
                Height = 34,
                BackColor = Color.FromArgb(11, 161, 86)
            };
            Image? headerIconImage = BrandingAssets.TryLoadAssetImage("tile-discount.png");
            iconWrap.Paint += (_, e) =>
            {
                if (headerIconImage != null)
                {
                    return;
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = RoundedRect(new Rectangle(0, 0, iconWrap.Width - 1, iconWrap.Height - 1), 11);
                using SolidBrush fill = new SolidBrush(iconWrap.BackColor);
                e.Graphics.FillPath(fill, path);
            };
            iconWrap.Resize += (_, __) =>
                iconWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, iconWrap.Width), Math.Max(1, iconWrap.Height)), 11));
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
                    Text = "%",
                    Font = new Font("Bahnschrift SemiBold", 14f, FontStyle.Bold),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                iconWrap.Controls.Add(icon);
            }

            header.Controls.Add(new Label
            {
                Text = "Foiz va chegirmalar",
                Left = 76,
                Top = 13,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 45, 68),
                BackColor = Color.Transparent
            });

            header.Controls.Add(new Label
            {
                Text = "Sotuv uchun tavsiya narx sozlamalari",
                Left = 76,
                Top = 40,
                AutoSize = true,
                Font = new Font("Bahnschrift", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(92, 108, 135),
                BackColor = Color.Transparent
            });

            Panel card = CreateCard(32, 20, 604, 528);
            root.Controls.Add(card);

            Panel markupCard = CreateRoundedPanel(24, 22, 556, 238, Color.FromArgb(232, 245, 239), Color.FromArgb(146, 221, 171), 12);
            card.Controls.Add(markupCard);
            markupCard.Controls.Add(new Label
            {
                Text = "Tavsiya ustama foizi (%):",
                Left = 16,
                Top = 14,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 57, 45),
                BackColor = Color.Transparent
            });
            markupCard.Controls.Add(new Label
            {
                Text = "Tovar sotish narxiga qancha ustama foiz qo'shilishini belgilang",
                Left = 16,
                Top = 44,
                AutoSize = true,
                Font = new Font("Bahnschrift", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(63, 92, 82),
                BackColor = Color.Transparent
            });

            Panel inputShell = CreateRoundedPanel(16, 78, 524, 44, Color.White, Color.FromArgb(99, 209, 132), 10);
            markupCard.Controls.Add(inputShell);

            _numMarkup.Left = 10;
            _numMarkup.Top = 8;
            _numMarkup.Width = 474;
            _numMarkup.DecimalPlaces = 2;
            _numMarkup.Minimum = 0;
            _numMarkup.Maximum = 1000;
            _numMarkup.Increment = 0.5M;
            _numMarkup.BorderStyle = BorderStyle.None;
            _numMarkup.Font = new Font("Bahnschrift SemiBold", 15f, FontStyle.Bold);
            _numMarkup.ForeColor = Color.FromArgb(32, 45, 68);
            _numMarkup.TextAlign = HorizontalAlignment.Left;
            _numMarkup.ValueChanged += (_, __) =>
            {
                MarkDirty();
                RefreshExample();
            };
            if (_numMarkup.Controls.Count > 0)
            {
                _numMarkup.Controls[0].Visible = false;
            }
            inputShell.Controls.Add(_numMarkup);

            Label lblPercent = new Label
            {
                Text = "%",
                Left = 492,
                Top = 11,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 15f, FontStyle.Bold),
                ForeColor = Color.FromArgb(11, 161, 86),
                BackColor = Color.Transparent
            };
            inputShell.Controls.Add(lblPercent);

            Panel exampleCard = CreateRoundedPanel(16, 132, 524, 98, Color.FromArgb(246, 251, 247), Color.FromArgb(146, 221, 171), 8);
            markupCard.Controls.Add(exampleCard);
            exampleCard.Controls.Add(new Label
            {
                Text = "Misol:",
                Left = 10,
                Top = 8,
                AutoSize = true,
                Font = new Font("Bahnschrift", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(59, 87, 78),
                BackColor = Color.Transparent
            });
            exampleCard.Controls.Add(new Label
            {
                Text = "Sotib olish narxi:",
                Left = 10,
                Top = 28,
                AutoSize = true,
                Font = new Font("Bahnschrift", 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(40, 63, 55),
                BackColor = Color.Transparent
            });
            exampleCard.Controls.Add(new Label
            {
                Text = "100 000 so'm",
                Left = 430,
                Top = 28,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 48, 73),
                BackColor = Color.Transparent
            });
            exampleCard.Controls.Add(new Label
            {
                Text = "Ustama:",
                Left = 10,
                Top = 49,
                AutoSize = true,
                Font = new Font("Bahnschrift", 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(40, 63, 55),
                BackColor = Color.Transparent
            });

            _lblExampleMarkup.Left = 430;
            _lblExampleMarkup.Top = 49;
            _lblExampleMarkup.AutoSize = true;
            _lblExampleMarkup.Font = new Font("Bahnschrift SemiBold", 10f, FontStyle.Bold);
            _lblExampleMarkup.ForeColor = Color.FromArgb(11, 161, 86);
            _lblExampleMarkup.BackColor = Color.Transparent;
            exampleCard.Controls.Add(_lblExampleMarkup);

            Panel line = new Panel
            {
                Left = 10,
                Top = 70,
                Width = 500,
                Height = 1,
                BackColor = Color.FromArgb(193, 222, 206)
            };
            exampleCard.Controls.Add(line);

            exampleCard.Controls.Add(new Label
            {
                Text = "Sotuv narxi:",
                Left = 10,
                Top = 75,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 48, 73),
                BackColor = Color.Transparent
            });

            _lblExampleTotal.Left = 405;
            _lblExampleTotal.Top = 74;
            _lblExampleTotal.AutoSize = true;
            _lblExampleTotal.Font = new Font("Bahnschrift SemiBold", 13f, FontStyle.Bold);
            _lblExampleTotal.ForeColor = Color.FromArgb(11, 161, 86);
            _lblExampleTotal.BackColor = Color.Transparent;
            exampleCard.Controls.Add(_lblExampleTotal);

            Panel sectionLine = new Panel
            {
                Left = 24,
                Top = 276,
                Width = 556,
                Height = 1,
                BackColor = Color.FromArgb(210, 220, 235)
            };
            card.Controls.Add(sectionLine);

            Panel autoFillCard = CreateRoundedPanel(24, 292, 556, 68, Color.FromArgb(230, 238, 252), Color.FromArgb(167, 194, 238), 11);
            card.Controls.Add(autoFillCard);
            _chkAutoFill.Left = 16;
            _chkAutoFill.Top = 18;
            _chkAutoFill.AutoSize = true;
            _chkAutoFill.Text = "  Tovar tanlanganda sotish narxiga tavsiya qiymatini avtomatik yozish";
            _chkAutoFill.Font = new Font("Bahnschrift", 11f, FontStyle.Regular);
            _chkAutoFill.ForeColor = Color.FromArgb(30, 46, 73);
            _chkAutoFill.BackColor = Color.Transparent;
            _chkAutoFill.CheckedChanged += (_, __) => MarkDirty();
            autoFillCard.Controls.Add(_chkAutoFill);
            autoFillCard.Controls.Add(new Label
            {
                Text = "Yangi tovar qo'shayotganingizda, sotish narxi avtomatik hisoblanadi va maydoncha to'ldiriladi",
                Left = 36,
                Top = 44,
                AutoSize = true,
                Font = new Font("Bahnschrift", 8.8f, FontStyle.Regular),
                ForeColor = Color.FromArgb(68, 89, 122),
                BackColor = Color.Transparent
            });

            Panel quickCard = CreateRoundedPanel(24, 376, 556, 68, Color.FromArgb(250, 247, 232), Color.FromArgb(235, 205, 120), 11);
            card.Controls.Add(quickCard);
            _chkQuickDiscountEnabled.Left = 16;
            _chkQuickDiscountEnabled.Top = 18;
            _chkQuickDiscountEnabled.AutoSize = true;
            _chkQuickDiscountEnabled.Text = "  Tezkor chegirma (foiz/summa) funksiyasini yoqish";
            _chkQuickDiscountEnabled.Font = new Font("Bahnschrift", 11f, FontStyle.Regular);
            _chkQuickDiscountEnabled.ForeColor = Color.FromArgb(49, 57, 66);
            _chkQuickDiscountEnabled.BackColor = Color.Transparent;
            _chkQuickDiscountEnabled.CheckedChanged += (_, __) => MarkDirty();
            quickCard.Controls.Add(_chkQuickDiscountEnabled);
            quickCard.Controls.Add(new Label
            {
                Text = "Sotuvda tezkor chegirma berish imkoniyatini yoqadi (foiz yoki aniq summa)",
                Left = 36,
                Top = 44,
                AutoSize = true,
                Font = new Font("Bahnschrift", 8.8f, FontStyle.Regular),
                ForeColor = Color.FromArgb(97, 83, 58),
                BackColor = Color.Transparent
            });

            _btnReset.Text = "Tozalash";
            _btnReset.Left = 24;
            _btnReset.Top = 470;
            _btnReset.Width = 74;
            _btnReset.Height = 34;
            _btnReset.FlatStyle = FlatStyle.Flat;
            _btnReset.FlatAppearance.BorderSize = 0;
            _btnReset.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 245, 252);
            _btnReset.FlatAppearance.MouseDownBackColor = Color.FromArgb(229, 238, 249);
            _btnReset.BackColor = Color.Transparent;
            _btnReset.Font = new Font("Bahnschrift", 10f, FontStyle.Regular);
            _btnReset.ForeColor = Color.FromArgb(43, 61, 90);
            _btnReset.Cursor = Cursors.Hand;
            _btnReset.Click += (_, __) =>
            {
                _numMarkup.Value = 0;
                _chkAutoFill.Checked = false;
                _chkQuickDiscountEnabled.Checked = false;
                MarkDirty();
                RefreshExample();
            };
            card.Controls.Add(_btnReset);
            _btnReset.Resize += (_, __) =>
                _btnReset.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _btnReset.Width), Math.Max(1, _btnReset.Height)), 10));
            _btnReset.Region = new Region(RoundedRect(new Rectangle(0, 0, _btnReset.Width, _btnReset.Height), 10));
            _btnReset.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(_btnReset.Parent?.BackColor ?? BackColor);
                Rectangle rect = new Rectangle(0, 0, _btnReset.Width - 1, _btnReset.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 10);
                using SolidBrush fill = new SolidBrush(Color.FromArgb(246, 249, 253));
                using Pen border = new Pen(Color.FromArgb(182, 199, 226), 1f);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                TextRenderer.DrawText(e.Graphics, _btnReset.Text, _btnReset.Font, _btnReset.ClientRectangle,
                    Color.FromArgb(43, 61, 90), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            _btnSave.Text = "  Saqlash";
            _btnSave.Left = 480;
            _btnSave.Top = 468;
            _btnSave.Width = 100;
            _btnSave.Height = 38;
            _btnSave.FlatStyle = FlatStyle.Flat;
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnSave.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnSave.Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold);
            _btnSave.ForeColor = Color.White;
            _btnSave.Cursor = Cursors.Hand;
            _btnSave.Enabled = false;
            _btnSave.Click += Save_Click;
            _btnSave.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(_btnSave.Parent?.BackColor ?? BackColor);
                Rectangle rect = new Rectangle(0, 0, _btnSave.Width - 1, _btnSave.Height - 1);
                Color c1 = _btnSave.Enabled ? Color.FromArgb(11, 168, 93) : Color.FromArgb(139, 180, 161);
                Color c2 = _btnSave.Enabled ? Color.FromArgb(8, 147, 79) : Color.FromArgb(130, 168, 151);
                using GraphicsPath path = RoundedRect(rect, 11);
                using LinearGradientBrush lg = new LinearGradientBrush(rect, c1, c2, 0f);
                e.Graphics.FillPath(lg, path);
                TextRenderer.DrawText(e.Graphics, "Saqlash", new Font("Bahnschrift SemiBold", 10.5f, FontStyle.Bold),
                    _btnSave.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _btnSave.Resize += (_, __) =>
                _btnSave.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, _btnSave.Width), Math.Max(1, _btnSave.Height)), 11));
            card.Controls.Add(_btnSave);

            _lblState.Left = 110;
            _lblState.Top = 478;
            _lblState.AutoSize = true;
            _lblState.Font = new Font("Bahnschrift", 9.5f, FontStyle.Regular);
            _lblState.ForeColor = Color.FromArgb(184, 96, 0);
            _lblState.Text = "Sozlamalar o'zgardi (saqlanmagan)";
            card.Controls.Add(_lblState);

            Panel tipCard = CreateRoundedPanel(32, 566, 604, 100, Color.FromArgb(199, 215, 247), Color.FromArgb(154, 183, 236), 11);
            root.Controls.Add(tipCard);
            Panel tipIcon = new Panel
            {
                Left = 16,
                Top = 16,
                Width = 24,
                Height = 24,
                BackColor = Color.FromArgb(37, 103, 231)
            };
            tipIcon.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = RoundedRect(new Rectangle(0, 0, tipIcon.Width - 1, tipIcon.Height - 1), 7);
                using SolidBrush fill = new SolidBrush(tipIcon.BackColor);
                e.Graphics.FillPath(fill, path);
            };
            tipIcon.Resize += (_, __) =>
                tipIcon.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, tipIcon.Width), Math.Max(1, tipIcon.Height)), 7));
            tipCard.Controls.Add(tipIcon);
            tipIcon.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "\u2197",
                Font = new Font("Bahnschrift SemiBold", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });

            tipCard.Controls.Add(new Label
            {
                Text = "Foydali maslahat:",
                Left = 48,
                Top = 16,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 40, 76),
                BackColor = Color.Transparent
            });
            tipCard.Controls.Add(new Label
            {
                Text = "- Tavsiya foizi: Mahsulot sotishda qancha foyda olishingizni belgilaydi",
                Left = 48,
                Top = 39,
                AutoSize = true,
                Font = new Font("Bahnschrift", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(23, 39, 65),
                BackColor = Color.Transparent
            });
            tipCard.Controls.Add(new Label
            {
                Text = "- Avtomatik hisoblash: Yangi tovar kiritishda vaqtingizni tejaydi",
                Left = 48,
                Top = 57,
                AutoSize = true,
                Font = new Font("Bahnschrift", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(23, 39, 65),
                BackColor = Color.Transparent
            });
            tipCard.Controls.Add(new Label
            {
                Text = "- Tezkor chegirma: Mijozlarga chegirma berish osonlashadi",
                Left = 48,
                Top = 75,
                AutoSize = true,
                Font = new Font("Bahnschrift", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(23, 39, 65),
                BackColor = Color.Transparent
            });

            FormClosing += PricingDiscountForm_FormClosing;
            RefreshExample();
        }

        private void LoadSettings()
        {
            try
            {
                _suspendDirtyTracking = true;
                var settings = _service.Get(_currentUser);
                _numMarkup.Value = (decimal)settings.SuggestedMarkupPercent;
                _chkAutoFill.Checked = settings.AutoFillSuggestedPrice;
                _chkQuickDiscountEnabled.Checked = settings.QuickDiscountEnabled;
                _suspendDirtyTracking = false;
                SetDirty(false);
                RefreshExample();
            }
            catch (Exception ex)
            {
                _suspendDirtyTracking = false;
                MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Save_Click(object? sender, EventArgs e)
        {
            try
            {
                _service.Save((double)_numMarkup.Value, _chkAutoFill.Checked, _chkQuickDiscountEnabled.Checked, _currentUser);
                SetDirty(false);
                MessageBox.Show("Sozlamalar saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshExample()
        {
            decimal basePrice = 100000M;
            decimal percent = _numMarkup.Value;
            decimal markup = Math.Round(basePrice * percent / 100M, 0);
            decimal total = basePrice + markup;

            _lblExampleMarkup.Text = $"+{markup:N0} so'm";
            _lblExampleTotal.Text = $"{total:N0} so'm";
        }

        private void MarkDirty()
        {
            if (_suspendDirtyTracking)
            {
                return;
            }

            SetDirty(true);
        }

        private void SetDirty(bool dirty)
        {
            _isDirty = dirty;
            _btnSave.Enabled = dirty;
            _btnSave.Invalidate();
            _lblState.ForeColor = dirty ? Color.FromArgb(184, 96, 0) : Color.FromArgb(23, 137, 74);
            _lblState.Text = dirty ? "Sozlamalar o'zgardi (saqlanmagan)" : "Sozlamalar saqlandi";
            Text = dirty ? "Foiz va chegirmalar *" : "Foiz va chegirmalar";
        }

        private void PricingDiscountForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_isDirty)
            {
                return;
            }

            DialogResult confirm = MessageBox.Show(
                "Saqlanmagan o'zgarishlar bor. Chiqishda bekor qilinsinmi?",
                "Tasdiq",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                e.Cancel = true;
            }
        }

        private static Panel CreateCard(int left, int top, int width, int height)
        {
            Panel panel = new Panel
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                BackColor = Color.FromArgb(247, 250, 255)
            };
            panel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(panel.Parent?.BackColor ?? panel.BackColor);
                Rectangle shadowRect = new Rectangle(4, 5, panel.Width - 9, panel.Height - 9);
                Rectangle rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using GraphicsPath shadowPath = RoundedRect(shadowRect, 12);
                using GraphicsPath path = RoundedRect(rect, 12);
                using SolidBrush shadow = new SolidBrush(Color.FromArgb(20, 21, 40, 71));
                using SolidBrush fill = new SolidBrush(panel.BackColor);
                using Pen border = new Pen(Color.FromArgb(209, 220, 236));
                e.Graphics.FillPath(shadow, shadowPath);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            panel.Resize += (_, __) =>
                panel.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, panel.Width), Math.Max(1, panel.Height)), 12));
            return panel;
        }

        private static Panel CreateRoundedPanel(int left, int top, int width, int height, Color fill, Color borderColor, int radius)
        {
            Panel panel = new Panel
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                BackColor = fill
            };
            panel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(panel.Parent?.BackColor ?? panel.BackColor);
                Rectangle rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using GraphicsPath path = RoundedRect(rect, radius);
                using SolidBrush brush = new SolidBrush(fill);
                using Pen pen = new Pen(borderColor);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            };
            panel.Resize += (_, __) =>
                panel.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, panel.Width), Math.Max(1, panel.Height)), radius));
            return panel;
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



