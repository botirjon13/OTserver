using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class DebtorsForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly DebtService _debtService = new DebtService();
        private readonly string? _initialStatusFilter;

        private readonly Panel _container = new Panel();
        private readonly Panel _cardOutstanding = new Panel();
        private readonly Panel _cardOverdue = new Panel();
        private readonly Panel _cardStatus = new Panel();
        private readonly Label _lblOutstanding = new Label();
        private readonly Label _lblOverdue = new Label();
        private readonly Label _lblStatusSummary = new Label();
        private readonly Label _lblRows = new Label();

        private readonly Panel _searchWrap = new Panel();
        private readonly Panel _statusWrap = new Panel();
        private readonly Panel _statusArrowMask = new Panel();
        private readonly TextBox _txtSearch = new TextBox();
        private readonly ComboBox _cmbStatus = new ComboBox();
        private readonly Button _btnRefresh = new Button();
        private readonly Button _btnAddPayment = new Button();
        private readonly Button _btnClose = new Button();

        private readonly Panel _gridWrap = new Panel();
        private readonly DataGridView _grid = new DataGridView();
        private readonly BindingSource _gridSource = new BindingSource();
        private readonly Panel _titleBadge = new Panel();
        private readonly Label _lblTitleIcon = new Label();
        private readonly Label _lblSearchIcon = new Label();
        private static readonly Color InputBorderColor = Color.FromArgb(58, 113, 212);

        private List<Debt> _debts = new List<Debt>();

        public DebtorsForm(AppUser currentUser, string? initialStatusFilter = null)
        {
            _currentUser = currentUser;
            _initialStatusFilter = initialStatusFilter;
            AuthorizationService.Require(
                AuthorizationService.CanManageDebts(_currentUser),
                "Qarzdorlar bo'limiga ruxsat yo'q.");

            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            bool initialApplied = TryApplyInitialStatusFilter();
            if (!initialApplied)
            {
                ReloadData();
            }
        }

        private void InitializeComponent()
        {
            Text = "Qarzdorlar ro'yxati";
            Size = new Size(1280, 780);
            MinimumSize = new Size(980, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(236, 242, 252);
            Font = new Font("Bahnschrift", 10, FontStyle.Regular);
            DoubleBuffered = true;

            Controls.Add(_container);

            _titleBadge.BackColor = Color.FromArgb(245, 2, 49);
            _titleBadge.Size = new Size(52, 52);
            Image? debtorsHeaderIcon = BrandingAssets.TryLoadAssetImage("tile-customers-stat-debtors.png");
            if (debtorsHeaderIcon != null)
            {
                _titleBadge.BackColor = Color.Transparent;
                _titleBadge.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = debtorsHeaderIcon
                });
            }
            else
            {
                _lblTitleIcon.Text = "\uEA18";
                _lblTitleIcon.Dock = DockStyle.Fill;
                _lblTitleIcon.TextAlign = ContentAlignment.MiddleCenter;
                _lblTitleIcon.ForeColor = Color.White;
                _lblTitleIcon.Font = new Font("Segoe MDL2 Assets", 22, FontStyle.Regular);
                _titleBadge.Controls.Add(_lblTitleIcon);
            }

            Label lblTitle = new Label
            {
                Text = "Qarzdorlar ro'yxati",
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 32, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 47, 76)
            };
            Label lblSub = new Label
            {
                Text = "Barcha qarzlar va to'lovlar ma'lumoti",
                AutoSize = true,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(91, 110, 142)
            };

            InitSummaryCard(_cardOutstanding, "Jami qoldiq", _lblOutstanding, "money", Color.FromArgb(255, 33, 78), Color.FromArgb(240, 4, 64), "tile-debtors-stat-total.png");
            InitSummaryCard(_cardOverdue, "Muddati o'tgan", _lblOverdue, "overdue", Color.FromArgb(255, 133, 0), Color.FromArgb(242, 38, 0), "tile-debtors-stat-overdue.png");
            InitSummaryCard(_cardStatus, "Holat", _lblStatusSummary, "status", Color.FromArgb(56, 111, 235), Color.FromArgb(78, 58, 224), "tile-debtors-stat-status.png");

            _searchWrap.BackColor = Color.FromArgb(250, 252, 255);
            _searchWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(2, 2, Math.Max(1, _searchWrap.Width - 5), Math.Max(1, _searchWrap.Height - 5));
                using Pen border = new Pen(InputBorderColor, 3f) { LineJoin = LineJoin.Round };
                using GraphicsPath path = RoundedRect(bounds, 14);
                e.Graphics.DrawPath(border, path);
            };

            _txtSearch.BorderStyle = BorderStyle.None;
            _txtSearch.BackColor = Color.FromArgb(250, 252, 255);
            _txtSearch.Font = new Font("Bahnschrift", 14, FontStyle.Regular);
            _txtSearch.PlaceholderText = "Mijoz ismi yoki telefon...";
            _txtSearch.TextChanged += (s, e) => ReloadGrid();
            _lblSearchIcon.Text = "\uE721";
            _lblSearchIcon.AutoSize = false;
            _lblSearchIcon.Width = 28;
            _lblSearchIcon.Height = 28;
            _lblSearchIcon.Left = 10;
            _lblSearchIcon.Top = 10;
            _lblSearchIcon.TextAlign = ContentAlignment.MiddleCenter;
            _lblSearchIcon.Font = new Font("Segoe MDL2 Assets", 16, FontStyle.Regular);
            _lblSearchIcon.ForeColor = Color.FromArgb(140, 156, 182);
            _searchWrap.Controls.Add(_lblSearchIcon);
            _searchWrap.Controls.Add(_txtSearch);

            _cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbStatus.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            _cmbStatus.FlatStyle = FlatStyle.Flat;
            _cmbStatus.BackColor = Color.FromArgb(250, 252, 255);
            _cmbStatus.Items.AddRange(new object[] { "All", "Open", "Overdue", "Closed" });
            _cmbStatus.SelectedIndex = 0;
            _cmbStatus.SelectedIndexChanged += (s, e) => ReloadData();
            _statusWrap.BackColor = Color.FromArgb(250, 252, 255);
            _statusWrap.Padding = new Padding(12, 8, 12, 8);
            _statusWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(2, 2, Math.Max(1, _statusWrap.Width - 5), Math.Max(1, _statusWrap.Height - 5));
                using Pen border = new Pen(InputBorderColor, 3f) { LineJoin = LineJoin.Round };
                using GraphicsPath path = RoundedRect(bounds, 14);
                e.Graphics.DrawPath(border, path);
            };
            _cmbStatus.Dock = DockStyle.Fill;
            _statusWrap.Controls.Add(_cmbStatus);
            _statusArrowMask.Dock = DockStyle.Right;
            _statusArrowMask.Width = 24;
            _statusArrowMask.BackColor = Color.FromArgb(250, 252, 255);
            _statusArrowMask.Cursor = Cursors.Hand;
            _statusArrowMask.Click += (s, e) => _cmbStatus.DroppedDown = true;
            _statusWrap.Controls.Add(_statusArrowMask);
            _statusArrowMask.BringToFront();
            _statusWrap.Click += (s, e) => _cmbStatus.DroppedDown = true;

            StyleActionButton(_btnRefresh, "Yangilash", "\uE72C", Color.FromArgb(56, 112, 242), Color.FromArgb(74, 83, 232));
            _btnRefresh.Click += (s, e) => ReloadData();
            StyleActionButton(_btnAddPayment, "To'lov qo'shish", "\uECC8", Color.FromArgb(0, 186, 109), Color.FromArgb(0, 162, 87));
            _btnAddPayment.Click += AddPayment_Click;
            StyleActionButton(_btnClose, "Yopish", "\uE711", Color.FromArgb(73, 92, 126), Color.FromArgb(53, 73, 106));
            _btnClose.Click += (s, e) => Close();

            _gridWrap.BackColor = Color.White;
            _gridWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(201, 216, 236));
                using GraphicsPath path = RoundedRect(_gridWrap.ClientRectangle, 16);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.None;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.GridColor = Color.FromArgb(230, 236, 246);
            _grid.ColumnHeadersHeight = 48;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 2, 49);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(244, 2, 49);
            _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 2, 0, 2);
            _grid.DefaultCellStyle.BackColor = Color.White;
            _grid.DefaultCellStyle.Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 235, 247);
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 39, 64);
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 252, 255);
            _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 235, 247);
            _grid.RowTemplate.Height = 46;
            _grid.CellDoubleClick += (s, e) => AddPayment_Click(s, EventArgs.Empty);
            _grid.CellFormatting += Grid_CellFormatting;

            SetGridDoubleBuffered(_grid);
            BuildGridColumns();
            _grid.DataSource = _gridSource;
            _gridWrap.Controls.Add(_grid);

            _lblRows.AutoSize = true;
            _lblRows.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            _lblRows.ForeColor = Color.FromArgb(71, 92, 128);

            _container.Controls.Add(_titleBadge);
            _container.Controls.Add(lblTitle);
            _container.Controls.Add(lblSub);
            _container.Controls.Add(_cardOutstanding);
            _container.Controls.Add(_cardOverdue);
            _container.Controls.Add(_cardStatus);
            _container.Controls.Add(_searchWrap);
            _container.Controls.Add(_statusWrap);
            _container.Controls.Add(_btnRefresh);
            _container.Controls.Add(_btnAddPayment);
            _container.Controls.Add(_btnClose);
            _container.Controls.Add(_gridWrap);
            _container.Controls.Add(_lblRows);

            void ApplyLayout()
            {
                int containerWidth = Math.Min(1340, ClientSize.Width - 36);
                int left = (ClientSize.Width - containerWidth) / 2;
                _container.SetBounds(left, 16, containerWidth, ClientSize.Height - 24);

                _titleBadge.Location = new Point(0, 4);
                _titleBadge.Region = new Region(RoundedRect(new Rectangle(0, 0, _titleBadge.Width, _titleBadge.Height), 16));
                lblTitle.Location = new Point(_titleBadge.Right + 14, -2);
                lblSub.Location = new Point(_titleBadge.Right + 16, 56);

                int gap = 14;
                int cardsTop = 96;
                int cardW = (containerWidth - (gap * 2)) / 3;
                _cardOutstanding.SetBounds(0, cardsTop, cardW, 96);
                _cardOverdue.SetBounds(cardW + gap, cardsTop, cardW, 96);
                _cardStatus.SetBounds((cardW + gap) * 2, cardsTop, cardW, 96);

                int searchTop = cardsTop + 114;
                int statusW = 132;
                int statusGap = 12;
                int searchW = containerWidth - statusW - statusGap;
                _searchWrap.SetBounds(0, searchTop, searchW, 50);
                _txtSearch.SetBounds(42, 14, _searchWrap.Width - 54, 22);
                _statusWrap.SetBounds(searchW + statusGap, searchTop, statusW, 50);

                int actionsTop = searchTop + 66;
                _btnRefresh.SetBounds(0, actionsTop, 140, 50);
                _btnAddPayment.SetBounds(154, actionsTop, 180, 50);
                _btnClose.SetBounds(348, actionsTop, 126, 50);

                int gridTop = actionsTop + 68;
                int gridH = _container.Height - gridTop - 62;
                _gridWrap.SetBounds(0, gridTop, containerWidth, Math.Max(260, gridH));
                _lblRows.Left = Math.Max(0, (containerWidth - _lblRows.Width) / 2);
                _lblRows.Top = _gridWrap.Bottom + 10;

                _cardOutstanding.Region = new Region(RoundedRect(new Rectangle(0, 0, _cardOutstanding.Width, _cardOutstanding.Height), 14));
                _cardOverdue.Region = new Region(RoundedRect(new Rectangle(0, 0, _cardOverdue.Width, _cardOverdue.Height), 14));
                _cardStatus.Region = new Region(RoundedRect(new Rectangle(0, 0, _cardStatus.Width, _cardStatus.Height), 14));
                _searchWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, _searchWrap.Width, _searchWrap.Height), 14));
                _statusWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, _statusWrap.Width, _statusWrap.Height), 14));
                _gridWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, _gridWrap.Width, _gridWrap.Height), 16));
            }

            Resize += (s, e) => ApplyLayout();
            Shown += (s, e) => ApplyLayout();
            ApplyLayout();
        }

        private static void InitSummaryCard(Panel card, string title, Label valueLabel, string iconKind, Color c1, Color c2, string? iconImageFile = null)
        {
            const int rightIconReserve = 96;
            Image? cardIconImage = string.IsNullOrWhiteSpace(iconImageFile) ? null : BrandingAssets.TryLoadAssetImage(iconImageFile);
            Label lblTitle = new Label
            {
                Text = title,
                Left = 16,
                Top = 14,
                AutoSize = true,
                ForeColor = Color.FromArgb(245, 249, 255),
                Font = new Font("Bahnschrift", 13, FontStyle.Regular),
                BackColor = Color.Transparent
            };

            valueLabel.Left = 16;
            valueLabel.Top = 46;
            valueLabel.Width = Math.Max(80, card.Width - rightIconReserve - 16);
            valueLabel.Height = 36;
            valueLabel.ForeColor = Color.White;
            valueLabel.Font = new Font("Bahnschrift SemiBold", 19, FontStyle.Bold);
            valueLabel.BackColor = Color.Transparent;
            valueLabel.AutoEllipsis = true;

            card.Controls.Add(lblTitle);
            card.Controls.Add(valueLabel);
            card.Resize += (s, e) =>
            {
                lblTitle.MaximumSize = new Size(Math.Max(40, card.Width - rightIconReserve - 10), 0);
                valueLabel.Width = Math.Max(80, card.Width - rightIconReserve - 16);
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using LinearGradientBrush brush = new LinearGradientBrush(card.ClientRectangle, c1, c2, 25f);
                e.Graphics.FillRectangle(brush, card.ClientRectangle);
                if (cardIconImage != null)
                {
                    Rectangle imageRect = new Rectangle(card.Width - 76, (card.Height - 58) / 2, 58, 58);
                    e.Graphics.DrawImage(cardIconImage, imageRect);
                }
                else
                {
                    Rectangle iconRect = new Rectangle(card.Width - 62, (card.Height - 46) / 2, 46, 46);
                    DrawCardIcon(e.Graphics, iconKind, iconRect, Color.FromArgb(140, 255, 255, 255));
                }
            };
        }

        private static void DrawCardIcon(Graphics g, string kind, Rectangle rect, Color color)
        {
            using Pen pen = new Pen(color, 2.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            if (string.Equals(kind, "money", StringComparison.OrdinalIgnoreCase))
            {
                TextRenderer.DrawText(
                    g,
                    "$",
                    new Font("Bahnschrift SemiBold", 33, FontStyle.Bold),
                    rect,
                    color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            if (string.Equals(kind, "overdue", StringComparison.OrdinalIgnoreCase))
            {
                Point p1 = new Point(rect.X + 8, rect.Y + 30);
                Point p2 = new Point(rect.X + 18, rect.Y + 20);
                Point p3 = new Point(rect.X + 27, rect.Y + 29);
                Point p4 = new Point(rect.X + 37, rect.Y + 18);
                g.DrawLines(pen, new[] { p1, p2, p3, p4 });
                g.DrawLine(pen, rect.X + 31, rect.Y + 18, rect.X + 37, rect.Y + 18);
                g.DrawLine(pen, rect.X + 37, rect.Y + 18, rect.X + 37, rect.Y + 24);
                return;
            }

            // status
            Rectangle ring = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
            g.DrawEllipse(pen, ring);
            g.DrawLine(pen, rect.X + rect.Width / 2, rect.Y + 13, rect.X + rect.Width / 2, rect.Bottom - 19);
            using SolidBrush statusBrush = new SolidBrush(color);
            g.FillEllipse(statusBrush, rect.X + rect.Width / 2 - 2, rect.Bottom - 14, 4, 4);
        }

        private static void StyleActionButton(Button button, string text, string iconGlyph, Color c1, Color c2)
        {
            button.Text = text;
            button.Tag = iconGlyph;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Bahnschrift SemiBold", 13, FontStyle.Bold);
            button.ForeColor = Color.White;
            button.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using LinearGradientBrush brush = new LinearGradientBrush(button.ClientRectangle, c1, c2, 0f);
                using GraphicsPath path = RoundedRect(button.ClientRectangle, 14);
                e.Graphics.FillPath(brush, path);

                string glyph = button.Tag?.ToString() ?? string.Empty;
                Rectangle iconRect = new Rectangle(14, 0, 20, button.Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    glyph,
                    new Font("Segoe MDL2 Assets", 12, FontStyle.Regular),
                    iconRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                Rectangle textRect = new Rectangle(34, 0, button.Width - 38, button.Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    button.Text,
                    button.Font,
                    textRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            button.Resize += (s, e) =>
                button.Region = new Region(RoundedRect(new Rectangle(0, 0, button.Width, button.Height), 14));
        }

        private bool TryApplyInitialStatusFilter()
        {
            string requested = _initialStatusFilter?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(requested))
            {
                return false;
            }

            for (int i = 0; i < _cmbStatus.Items.Count; i++)
            {
                if (!string.Equals(_cmbStatus.Items[i]?.ToString(), requested, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_cmbStatus.SelectedIndex == i)
                {
                    return false;
                }

                _cmbStatus.SelectedIndex = i;
                return true;
            }

            return false;
        }

        private void ReloadData()
        {
            _debts = _debtService.GetDebts(_cmbStatus.Text, string.Empty, _currentUser);
            ReloadGrid();

            DebtSummary summary = _debtService.GetSummary(_currentUser);
            _lblOutstanding.Text = $"{summary.OutstandingUZS:N0} UZS";
            _lblOverdue.Text = $"{summary.OverdueUZS:N0} UZS";
            _lblStatusSummary.Text = $"Open: {summary.OpenDebts} | Overdue: {summary.OverdueDebts}";
        }

        private void ReloadGrid()
        {
            string search = _txtSearch.Text.Trim().ToLowerInvariant();
            List<DebtGridRow> filtered = _debts
                .Where(d =>
                    string.IsNullOrWhiteSpace(search) ||
                    d.CustomerFullName.ToLowerInvariant().Contains(search) ||
                    d.CustomerPhone.ToLowerInvariant().Contains(search))
                .Select(d => new DebtGridRow
                {
                    Id = d.Id,
                    Mijoz = d.CustomerFullName,
                    Telefon = string.IsNullOrWhiteSpace(d.CustomerPhone) ? "-" : d.CustomerPhone,
                    SotuvId = $"#{d.SaleId}",
                    Jami = $"{d.TotalAmountUZS:N0}",
                    Tolangan = $"{d.PaidAmountUZS:N0}",
                    Qoldiq = $"{d.RemainingAmountUZS:N0}",
                    Muddat = d.DueDate.ToString("yyyy-MM-dd"),
                    Holat = d.Status
                })
                .ToList();

            int currentRow = _grid.CurrentCell?.RowIndex ?? -1;
            int firstDisplayedRow = _grid.FirstDisplayedScrollingRowIndex >= 0 ? _grid.FirstDisplayedScrollingRowIndex : 0;

            _grid.SuspendLayout();
            _gridSource.DataSource = filtered;
            _grid.ResumeLayout();

            if (_grid.Rows.Count > 0 && currentRow >= 0)
            {
                int targetRow = Math.Min(currentRow, _grid.Rows.Count - 1);
                _grid.CurrentCell = _grid.Rows[targetRow].Cells["Mijoz"];
            }
            if (_grid.Rows.Count > 0 && firstDisplayedRow >= 0 && firstDisplayedRow < _grid.Rows.Count)
            {
                _grid.FirstDisplayedScrollingRowIndex = firstDisplayedRow;
            }

            _lblRows.Text = $"Jami: {filtered.Count} ta qarz ko'rsatilmoqda";
            _lblRows.Left = Math.Max(0, (_container.Width - _lblRows.Width) / 2);
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Clear();
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mijoz", HeaderText = "Mijoz", DataPropertyName = "Mijoz", FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Telefon", HeaderText = "Telefon", DataPropertyName = "Telefon", FillWeight = 19 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SotuvId", HeaderText = "SotuvId", DataPropertyName = "SotuvId", FillWeight = 10 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Jami", HeaderText = "Jami", DataPropertyName = "Jami", FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tolangan", HeaderText = "To'langan", DataPropertyName = "Tolangan", FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qoldiq", HeaderText = "Qoldiq", DataPropertyName = "Qoldiq", FillWeight = 13 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Muddat", HeaderText = "Muddat", DataPropertyName = "Muddat", FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Holat", HeaderText = "Holat", DataPropertyName = "Holat", FillWeight = 10 });

            foreach (DataGridViewColumn column in _grid.Columns)
            {
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            DataGridViewCellStyle? style = e.CellStyle;
            if (style == null)
            {
                return;
            }

            string col = _grid.Columns[e.ColumnIndex].Name;
            if (col == "Tolangan")
            {
                style.ForeColor = Color.FromArgb(0, 163, 70);
                style.Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold);
            }
            else if (col == "Qoldiq")
            {
                style.ForeColor = Color.FromArgb(224, 13, 44);
                style.Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold);
            }
            else if (col == "Holat" && e.Value is string status)
            {
                if (string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase))
                {
                    style.ForeColor = Color.FromArgb(0, 152, 72);
                }
                else if (string.Equals(status, "Overdue", StringComparison.OrdinalIgnoreCase))
                {
                    style.ForeColor = Color.FromArgb(218, 26, 26);
                }
                else
                {
                    style.ForeColor = Color.FromArgb(30, 84, 224);
                }
                style.Font = new Font("Bahnschrift SemiBold", 11, FontStyle.Bold);
            }
        }

        private static void SetGridDoubleBuffered(DataGridView grid)
        {
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(grid, true, null);
        }

        private void AddPayment_Click(object? sender, EventArgs e)
        {
            if (_grid.CurrentRow == null)
            {
                MessageBox.Show("Qarz yozuvini tanlang.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            object? idObj = _grid.CurrentRow.Cells["Id"].Value;
            if (idObj == null || !int.TryParse(idObj.ToString(), out int debtId))
            {
                MessageBox.Show("Qarz identifikatori topilmadi.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Debt? debt = _debts.FirstOrDefault(x => x.Id == debtId);
            if (debt == null)
            {
                MessageBox.Show("Tanlangan qarz topilmadi.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (debt.RemainingAmountUZS <= 0)
            {
                MessageBox.Show("Ushbu qarz allaqachon yopilgan.", "Ma'lumot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var paymentForm = new DebtPaymentForm(debt.RemainingAmountUZS);
            if (paymentForm.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                _debtService.AddPayment(debtId, paymentForm.AmountUZS, paymentForm.PaymentType, paymentForm.CommentText, _currentUser);
                ReloadData();
                MessageBox.Show("To'lov saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private sealed class DebtGridRow
        {
            public int Id { get; set; }
            public string Mijoz { get; set; } = string.Empty;
            public string Telefon { get; set; } = string.Empty;
            public string SotuvId { get; set; } = string.Empty;
            public string Jami { get; set; } = string.Empty;
            public string Tolangan { get; set; } = string.Empty;
            public string Qoldiq { get; set; } = string.Empty;
            public string Muddat { get; set; } = string.Empty;
            public string Holat { get; set; } = string.Empty;
        }
    }
}



