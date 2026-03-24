using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SantexnikaSRM.Utils
{
    public static class FormFx
    {
        public static void EnsureFitsScreen(Form form, int margin = 10)
        {
            void Fit()
            {
                if (form.IsDisposed || form.WindowState != FormWindowState.Normal)
                {
                    return;
                }

                Rectangle wa = Screen.FromControl(form).WorkingArea;
                int maxWidth = Math.Max(320, wa.Width - margin * 2);
                int maxHeight = Math.Max(260, wa.Height - margin * 2);

                int minW = form.MinimumSize.Width > 0 ? form.MinimumSize.Width : 0;
                int minH = form.MinimumSize.Height > 0 ? form.MinimumSize.Height : 0;
                if (minW > maxWidth || minH > maxHeight)
                {
                    form.MinimumSize = new Size(Math.Min(minW, maxWidth), Math.Min(minH, maxHeight));
                }

                int targetW = Math.Min(form.Width, maxWidth);
                int targetH = Math.Min(form.Height, maxHeight);
                targetW = Math.Max(targetW, form.MinimumSize.Width > 0 ? form.MinimumSize.Width : targetW);
                targetH = Math.Max(targetH, form.MinimumSize.Height > 0 ? form.MinimumSize.Height : targetH);

                if (form.Width != targetW || form.Height != targetH)
                {
                    form.Size = new Size(targetW, targetH);
                }

                int x = Math.Max(wa.Left, Math.Min(form.Left, wa.Right - form.Width));
                int y = Math.Max(wa.Top, Math.Min(form.Top, wa.Bottom - form.Height));
                if (form.Left != x || form.Top != y)
                {
                    form.Location = new Point(x, y);
                }
            }

            form.Shown += (_, __) => Fit();
            form.Resize += (_, __) => Fit();
        }

        public static void EnableGradientBackground(Form form, Color from, Color to, float angle = 135f)
        {
            form.Paint += (s, e) =>
            {
                using LinearGradientBrush brush = new LinearGradientBrush(form.ClientRectangle, from, to, angle);
                e.Graphics.FillRectangle(brush, form.ClientRectangle);
            };
        }

        public static void StyleGlassCard(Panel panel, int radius = 16, Color? fill = null, Color? border = null)
        {
            Color bg = fill ?? Color.FromArgb(245, 249, 255);
            Color bd = border ?? Color.FromArgb(189, 205, 230);
            panel.BackColor = bg;
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(panel.Parent?.BackColor ?? panel.BackColor);
                using SolidBrush brush = new SolidBrush(bg);
                using Pen pen = new Pen(bd, 1);
                using GraphicsPath path = RoundedRect(panel.ClientRectangle, radius);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            };
            panel.Resize += (s, e) =>
            {
                panel.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, panel.Width), Math.Max(1, panel.Height)), radius));
            };
        }

        public static void StyleNeonButton(Button button, Color c1, Color c2, int radius = 14)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            button.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(button.Parent?.BackColor ?? button.BackColor);
                using LinearGradientBrush brush = new LinearGradientBrush(button.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(button.ClientRectangle, radius);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    button.Text,
                    button.Font,
                    button.ClientRectangle,
                    button.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            button.Resize += (s, e) =>
            {
                button.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, button.Width), Math.Max(1, button.Height)), radius));
            };

            Point basePos = Point.Empty;
            button.MouseDown += (s, e) =>
            {
                basePos = button.Location;
                button.Location = new Point(basePos.X, basePos.Y + 1);
            };
            button.MouseUp += (s, e) => button.Location = basePos;
            button.MouseLeave += (s, e) => button.Location = basePos;
        }

        public static void AttachLiftHover(Control control, int liftPx = 2)
        {
            // Layout panellarda (Flow/Table) koordinatani o'zgartirish controlni "sakratadi".
            // Tugmalar uchun faqat yengil vizual feedback beramiz, pozitsiya o'zgarmaydi.
            if (control is Button button)
            {
                Color baseColor = button.ForeColor;
                button.MouseEnter += (s, e) => button.ForeColor = Color.WhiteSmoke;
                button.MouseLeave += (s, e) => button.ForeColor = baseColor;
                return;
            }

            bool canMove = control.Parent is not FlowLayoutPanel && control.Parent is not TableLayoutPanel;
            if (!canMove)
            {
                return;
            }

            int baseTop = control.Top;
            control.MouseEnter += (s, e) =>
            {
                baseTop = control.Top;
                control.Top = baseTop - liftPx;
            };
            control.MouseLeave += (s, e) => control.Top = baseTop;
        }

        public static void StartStaggerReveal(Form owner, IEnumerable<Control> controls, int intervalMs = 70)
        {
            List<Control> items = controls.ToList();
            foreach (Control c in items)
            {
                c.Visible = false;
            }

            int idx = 0;
            var timer = new System.Windows.Forms.Timer { Interval = intervalMs };
            timer.Tick += (s, e) =>
            {
                if (idx >= items.Count)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                items[idx].Visible = true;
                idx++;
            };

            timer.Start();
            owner.FormClosed += (s, e) =>
            {
                if (timer.Enabled)
                {
                    timer.Stop();
                }
                timer.Dispose();
            };
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
