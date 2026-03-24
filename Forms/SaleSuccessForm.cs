using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SantexnikaSRM.Forms
{
    public class SaleSuccessForm : Form
    {
        private readonly Panel _card = new Panel();
        private readonly Panel _iconWrap = new Panel();
        private readonly Panel _messageWrap = new Panel();
        private readonly Button _btnOk = new Button();

        public SaleSuccessForm()
        {
            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
        }

        private void InitializeComponent()
        {
            Text = "Tayyor";
            ClientSize = new Size(446, 278);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(238, 242, 248);
            KeyPreview = true;
            DoubleBuffered = true;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            Resize += (_, __) =>
            {
                Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 16));
            };

            _card.Dock = DockStyle.Fill;
            _card.BackColor = Color.Transparent;
            _card.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle shadowRect = new Rectangle(4, 6, _card.Width - 11, _card.Height - 11);
                Rectangle outerRect = new Rectangle(0, 0, _card.Width - 1, _card.Height - 1);
                Rectangle innerRect = new Rectangle(1, 1, _card.Width - 3, _card.Height - 3);
                using GraphicsPath shadowPath = RoundedRect(shadowRect, 16);
                using GraphicsPath outerPath = RoundedRect(outerRect, 16);
                using GraphicsPath innerPath = RoundedRect(innerRect, 15);
                using SolidBrush shadow = new SolidBrush(Color.FromArgb(96, 0, 0, 0));
                using SolidBrush fill = new SolidBrush(Color.FromArgb(250, 252, 255));
                using Pen outerBorder = new Pen(Color.FromArgb(12, 186, 106), 2.6f);
                using Pen innerBorder = new Pen(Color.FromArgb(171, 235, 204), 1.6f);
                e.Graphics.FillPath(shadow, shadowPath);
                e.Graphics.FillPath(fill, outerPath);
                e.Graphics.DrawPath(outerBorder, outerPath);
                e.Graphics.DrawPath(innerBorder, innerPath);
            };

            _iconWrap.SetBounds(183, 34, 80, 80);
            _iconWrap.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle outer = new Rectangle(0, 0, _iconWrap.Width - 1, _iconWrap.Height - 1);
                Rectangle inner = new Rectangle(18, 18, 44, 44);
                using SolidBrush green = new SolidBrush(Color.FromArgb(5, 181, 104));
                using Pen innerBorder = new Pen(Color.White, 3f);
                using GraphicsPath outerPath = RoundedRect(outer, 40);
                using GraphicsPath innerPath = RoundedRect(inner, 22);
                e.Graphics.FillPath(green, outerPath);
                e.Graphics.DrawPath(innerBorder, innerPath);
                TextRenderer.DrawText(
                    e.Graphics,
                    "\uE73E",
                    new Font("Segoe MDL2 Assets", 16f, FontStyle.Regular),
                    inner,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            _messageWrap.SetBounds(24, 128, 398, 62);
            _messageWrap.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, _messageWrap.Width - 1, _messageWrap.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 12);
                using SolidBrush fill = new SolidBrush(Color.FromArgb(229, 244, 237));
                using Pen border = new Pen(Color.FromArgb(126, 224, 177), 1.5f);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };

            Label lblMessage = new Label
            {
                Text = "Sotuv muvaffaqiyatli saqlandi!",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Bahnschrift SemiBold", 18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 39, 66),
                BackColor = Color.Transparent
            };
            _messageWrap.Controls.Add(lblMessage);

            _btnOk.SetBounds(24, 206, 398, 48);
            _btnOk.Text = "OK";
            _btnOk.FlatStyle = FlatStyle.Flat;
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnOk.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnOk.Font = new Font("Bahnschrift SemiBold", 18f, FontStyle.Bold);
            _btnOk.ForeColor = Color.White;
            _btnOk.Cursor = Cursors.Hand;
            _btnOk.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, _btnOk.Width - 1, _btnOk.Height - 1);
                using GraphicsPath path = RoundedRect(rect, 14);
                using SolidBrush fill = new SolidBrush(Color.FromArgb(6, 177, 92));
                e.Graphics.FillPath(fill, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    _btnOk.Text,
                    _btnOk.Font,
                    _btnOk.ClientRectangle,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _btnOk.Click += (_, __) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            _card.Controls.Add(_iconWrap);
            _card.Controls.Add(_messageWrap);
            _card.Controls.Add(_btnOk);
            Controls.Add(_card);

            Shown += (_, __) =>
            {
                Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 16));
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



