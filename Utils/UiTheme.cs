using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SantexnikaSRM.Utils
{
    public static class UiTheme
    {
        public enum ThemeMode
        {
            Light,
            Dark
        }

        public static ThemeMode CurrentMode { get; private set; } = ThemeMode.Light;
        public static event System.EventHandler? ThemeChanged;

        public static Color Background =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(239, 244, 250) : Color.FromArgb(14, 18, 24);

        public static Color BackgroundSecondary =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(231, 238, 247) : Color.FromArgb(20, 27, 35);

        public static Color Card =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(252, 255, 255) : Color.FromArgb(22, 31, 41);

        public static Color Primary =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(0, 112, 156) : Color.FromArgb(0, 157, 220);

        public static Color Accent =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(11, 173, 139) : Color.FromArgb(14, 196, 167);

        public static Color Danger =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(224, 62, 62) : Color.FromArgb(247, 92, 92);

        public static Color Text =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(24, 35, 48) : Color.FromArgb(226, 235, 246);

        public static Color Muted =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(105, 120, 141) : Color.FromArgb(146, 164, 186);

        public static Color Border =>
            CurrentMode == ThemeMode.Light ? Color.FromArgb(209, 220, 236) : Color.FromArgb(47, 62, 81);

        public static Font TitleFont => new Font("Bahnschrift SemiBold", 20, FontStyle.Bold);
        public static Font BodyFont => new Font("Bahnschrift", 10, FontStyle.Regular);
        public static Font ButtonFont => new Font("Bahnschrift SemiBold", 10, FontStyle.Bold);
        public static string IconFontFamily { get; } = ResolveIconFontFamily();

        public static Font IconFont(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(IconFontFamily, size, style, GraphicsUnit.Point);
        }

        public static void StylePrimaryButton(Button button)
        {
            button.Tag = "btn:primary";
            StyleButton(button, Primary, Color.FromArgb(11, 76, 127));
        }

        public static void StyleSuccessButton(Button button)
        {
            button.Tag = "btn:success";
            StyleButton(button, Accent, Color.FromArgb(12, 156, 109));
        }

        public static void StyleDangerButton(Button button)
        {
            button.Tag = "btn:danger";
            StyleButton(button, Danger, Color.FromArgb(185, 28, 28));
        }

        public static void StyleNeutralButton(Button button)
        {
            button.Tag = "btn:neutral";
            StyleButton(button, CurrentMode == ThemeMode.Light ? Color.FromArgb(87, 102, 122) : Color.FromArgb(58, 79, 103),
                CurrentMode == ThemeMode.Light ? Color.FromArgb(70, 85, 106) : Color.FromArgb(71, 94, 121));
        }

        public static void SetButtonIcon(Button button, Icon icon, int size = 16)
        {
            int iconSize = size < 12 ? 12 : size;
            button.Image = new Bitmap(icon.ToBitmap(), new Size(iconSize, iconSize));
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(18, 0, 14, 0);
        }

        public static void SetButtonGlyphIcon(Button button, string glyph, int size = 16)
        {
            int iconSize = size < 12 ? 12 : size;
            int canvas = iconSize + 4;
            Bitmap bmp = new Bitmap(canvas, canvas);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                TextRenderer.DrawText(
                    g,
                    glyph,
                    IconFont(iconSize),
                    new Rectangle(0, 0, canvas, canvas),
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            button.Image = bmp;
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(18, 0, 14, 0);
        }

        private static void StyleButton(Button button, Color baseColor, Color hoverColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = baseColor;
            button.ForeColor = Color.White;
            button.Font = ButtonFont;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Cursor = Cursors.Hand;
            button.Padding = new Padding(12, 0, 12, 0);
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.FlatAppearance.MouseDownBackColor = hoverColor;
            if (button.Height < 40)
            {
                button.Height = 40;
            }
        }

        public static void StyleInput(Control input)
        {
            input.Tag = "input";
            input.Font = BodyFont;
            input.BackColor = CurrentMode == ThemeMode.Light ? Color.White : Color.FromArgb(26, 37, 49);
            input.ForeColor = Text;

            if (input is ComboBox combo)
            {
                combo.FlatStyle = FlatStyle.Flat;
            }

            if (input is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.Tag = "grid";
            grid.BackgroundColor = Card;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = CurrentMode == ThemeMode.Light ? Color.FromArgb(228, 238, 249) : Color.FromArgb(31, 44, 59);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
            grid.ColumnHeadersDefaultCellStyle.Font = ButtonFont;
            grid.DefaultCellStyle.Font = BodyFont;
            grid.DefaultCellStyle.BackColor = Card;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = CurrentMode == ThemeMode.Light ? Color.FromArgb(214, 230, 248) : Color.FromArgb(42, 61, 81);
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.GridColor = Border;
        }

        public static void StyleCard(Control control)
        {
            control.Tag = "card";
            control.BackColor = Card;
            control.ForeColor = Text;
        }

        public static void RegisterThemedForm(Form form)
        {
            ApplyTheme(form);
            System.EventHandler handler = (_, __) =>
            {
                ApplyTheme(form);
                form.Invalidate(true);
            };

            ThemeChanged += handler;
            form.FormClosed += (_, __) => ThemeChanged -= handler;
        }

        public static void ToggleTheme()
        {
            CurrentMode = CurrentMode == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light;
            ThemeChanged?.Invoke(null, System.EventArgs.Empty);
        }

        public static string GetThemeToggleButtonText()
        {
            return CurrentMode == ThemeMode.Light ? "Qora Fon" : "Oq Fon";
        }

        public static void ApplyTheme(Control root)
        {
            if (root is Form form)
            {
                form.BackColor = Background;
                form.ForeColor = Text;
                form.Font = BodyFont;
            }

            foreach (Control control in root.Controls)
            {
                ApplyTheme(control);
            }

            if (root is Panel panel)
            {
                string tag = panel.Tag?.ToString() ?? string.Empty;
                panel.BackColor = tag == "card" ? Card : Background;
                panel.ForeColor = Text;
            }
            else if (root is Label label)
            {
                string tag = label.Tag?.ToString() ?? string.Empty;
                label.ForeColor = tag == "muted" ? Muted : Text;
                label.Font = label.Font.Size >= 13 ? new Font(TitleFont.FontFamily, label.Font.Size, label.Font.Style) : BodyFont;
            }
            else if (root is Button button)
            {
                string tag = button.Tag?.ToString() ?? string.Empty;
                if (tag == "btn:success")
                {
                    StyleSuccessButton(button);
                }
                else if (tag == "btn:danger")
                {
                    StyleDangerButton(button);
                }
                else if (tag == "btn:neutral")
                {
                    StyleNeutralButton(button);
                }
                else
                {
                    StylePrimaryButton(button);
                }
            }
            else if (root is DataGridView grid)
            {
                StyleGrid(grid);
            }
            else if (root is TextBox || root is ComboBox || root is DateTimePicker || root is ListBox)
            {
                StyleInput(root);
            }
        }

        private static void AttachHoverAnimation(Button button, Color normal, Color hover)
        {
            Color target = normal;
            button.BackColor = normal;
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 16 };
            timer.Tick += (s, e) =>
            {
                button.BackColor = Lerp(button.BackColor, target, 0.25f);
                if (IsClose(button.BackColor, target))
                {
                    button.BackColor = target;
                    timer.Stop();
                }
            };

            button.MouseEnter += (s, e) =>
            {
                target = hover;
                timer.Start();
            };
            button.MouseLeave += (s, e) =>
            {
                target = normal;
                timer.Start();
            };
            button.Disposed += (s, e) => timer.Dispose();
        }

        private static bool IsClose(Color a, Color b)
        {
            return System.Math.Abs(a.R - b.R) <= 2
                && System.Math.Abs(a.G - b.G) <= 2
                && System.Math.Abs(a.B - b.B) <= 2;
        }

        private static Color Lerp(Color from, Color to, float t)
        {
            int r = from.R + (int)((to.R - from.R) * t);
            int g = from.G + (int)((to.G - from.G) * t);
            int b = from.B + (int)((to.B - from.B) * t);
            return Color.FromArgb(r, g, b);
        }

        private static string ResolveIconFontFamily()
        {
            InstalledFontCollection fonts = new InstalledFontCollection();
            string[] preferred =
            {
                "Segoe MDL2 Assets",
                "Segoe Fluent Icons",
                "Segoe UI Symbol",
                "Segoe UI Emoji"
            };

            foreach (string family in preferred)
            {
                if (fonts.Families.Any(f => string.Equals(f.Name, family, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return family;
                }
            }

            return "Segoe UI";
        }
    }
}
