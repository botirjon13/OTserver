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
    public class CustomersForm : Form
    {
        private readonly AppUser _currentUser;
        private readonly CustomerService _customerService = new CustomerService();
        private readonly Panel _container = new Panel();
        private readonly TextBox _txtSearch = new TextBox();
        private readonly CheckBox _chkOnlyDebtors = new CheckBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _lblTotalCustomers = new Label();
        private readonly Label _lblDebtorCustomers = new Label();
        private readonly Label _lblOutstanding = new Label();
        private readonly Label _lblCount = new Label();
        private readonly Panel _cardTotal = new Panel();
        private readonly Panel _cardDebtors = new Panel();
        private readonly Panel _cardOutstanding = new Panel();
        private readonly Panel _searchWrap = new Panel();
        private readonly Panel _gridWrap = new Panel();
        private readonly Button _btnDebtors = new Button();
        private readonly Button _btnAdd = new Button();
        private readonly BindingSource _gridSource = new BindingSource();
        private readonly Panel _titleBadge = new Panel();
        private readonly Label _lblTitleIcon = new Label();
        private readonly Label _lblSearchIcon = new Label();
        private static readonly Color InputBorderColor = Color.FromArgb(58, 113, 212);
        private const string EditColumnName = "AmalEdit";
        private const string DeleteColumnName = "AmalDelete";

        public CustomersForm(AppUser currentUser)
        {
            _currentUser = currentUser;
            AuthorizationService.Require(
                AuthorizationService.CanManageDebts(_currentUser),
                "Mijozlar bo'limiga ruxsat yo'q.");

            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = "Mijozlar";
            Size = new Size(1260, 760);
            MinimumSize = new Size(980, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(238, 243, 252);
            Font = new Font("Bahnschrift", 10, FontStyle.Regular);
            DoubleBuffered = true;

            _container.BackColor = Color.Transparent;
            Controls.Add(_container);

            _titleBadge.BackColor = Color.FromArgb(242, 14, 127);
            _titleBadge.Size = new Size(52, 52);
            Image? headerIconImage = BrandingAssets.TryLoadAssetImage("tile-customers.png");
            if (headerIconImage != null)
            {
                _titleBadge.BackColor = Color.Transparent;
                _titleBadge.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = headerIconImage
                });
            }
            else
            {
                _lblTitleIcon.Text = "\uE716";
                _lblTitleIcon.Dock = DockStyle.Fill;
                _lblTitleIcon.TextAlign = ContentAlignment.MiddleCenter;
                _lblTitleIcon.ForeColor = Color.White;
                _lblTitleIcon.Font = new Font("Segoe MDL2 Assets", 22, FontStyle.Regular);
                _titleBadge.Controls.Add(_lblTitleIcon);
            }

            Label lblTitle = new Label
            {
                Text = "Mijozlar",
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 31, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 40, 65)
            };
            Label lblSubtitle = new Label
            {
                Text = "Barcha mijozlar ro'yxati va ma'lumotlari",
                AutoSize = true,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(94, 110, 139)
            };

            InitSummaryCard(_cardTotal, "Jami mijozlar", _lblTotalCustomers, "users", Color.FromArgb(148, 61, 255), Color.FromArgb(215, 39, 190), "tile-customers-stat-total.png");
            InitSummaryCard(_cardDebtors, "Qarzdorlar", _lblDebtorCustomers, "warning", Color.FromArgb(255, 58, 99), Color.FromArgb(240, 16, 81), "tile-customers-stat-debtors.png");
            InitSummaryCard(_cardOutstanding, "Umumiy qoldiq", _lblOutstanding, "money", Color.FromArgb(0, 196, 112), Color.FromArgb(0, 168, 84), "tile-customers-stat-balance.png");

            _searchWrap.BackColor = Color.FromArgb(249, 251, 255);
            _searchWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(2, 2, Math.Max(1, _searchWrap.Width - 5), Math.Max(1, _searchWrap.Height - 5));
                using Pen border = new Pen(InputBorderColor, 3f) { LineJoin = LineJoin.Round };
                using GraphicsPath path = RoundedRect(bounds, 14);
                e.Graphics.DrawPath(border, path);
            };

            _txtSearch.BorderStyle = BorderStyle.None;
            _txtSearch.BackColor = Color.FromArgb(249, 251, 255);
            _txtSearch.Font = new Font("Bahnschrift", 13, FontStyle.Regular);
            _txtSearch.PlaceholderText = "Mijoz ismi yoki telefon raqamini kiriting...";
            _txtSearch.TextChanged += (s, e) => LoadGrid();
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

            _chkOnlyDebtors.Text = "Faqat qarzdorlar";
            _chkOnlyDebtors.AutoSize = true;
            _chkOnlyDebtors.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            _chkOnlyDebtors.CheckedChanged += (s, e) => LoadGrid();

            StyleActionButton(_btnDebtors, "Qarzdorlar oynasi", "\uEA18", Color.FromArgb(255, 45, 95), Color.FromArgb(244, 20, 77));
            _btnDebtors.Click += (s, e) =>
            {
                using var debtorsForm = new DebtorsForm(_currentUser);
                debtorsForm.ShowDialog(this);
                LoadData();
            };

            StyleActionButton(_btnAdd, "Qo'shish", "\uECC8", Color.FromArgb(167, 52, 255), Color.FromArgb(228, 0, 145));
            _btnAdd.Click += AddCustomer_Click;

            _gridWrap.BackColor = Color.White;
            _gridWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush fill = new SolidBrush(Color.White);
                using Pen border = new Pen(Color.FromArgb(206, 218, 236));
                using GraphicsPath path = RoundedRect(_gridWrap.ClientRectangle, 16);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BorderStyle = BorderStyle.None;
            _grid.BackgroundColor = Color.White;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.GridColor = Color.FromArgb(229, 236, 247);
            _grid.ColumnHeadersHeight = 44;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(163, 56, 253);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(163, 56, 253);
            _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 2, 0, 2);
            _grid.DefaultCellStyle.BackColor = Color.White;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 232, 255);
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(34, 44, 68);
            _grid.DefaultCellStyle.Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 252, 255);
            _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 232, 255);
            _grid.RowTemplate.Height = 42;
            _grid.AutoGenerateColumns = false;
            _grid.CellContentClick += Grid_CellContentClick;
            SetGridDoubleBuffered(_grid);
            BuildGridColumns();
            _grid.DataSource = _gridSource;
            _gridWrap.Controls.Add(_grid);

            _lblCount.AutoSize = true;
            _lblCount.Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            _lblCount.ForeColor = Color.FromArgb(71, 92, 128);

            _container.Controls.Add(_titleBadge);
            _container.Controls.Add(lblTitle);
            _container.Controls.Add(lblSubtitle);
            _container.Controls.Add(_cardTotal);
            _container.Controls.Add(_cardDebtors);
            _container.Controls.Add(_cardOutstanding);
            _container.Controls.Add(_searchWrap);
            _container.Controls.Add(_chkOnlyDebtors);
            _container.Controls.Add(_btnDebtors);
            _container.Controls.Add(_btnAdd);
            _container.Controls.Add(_gridWrap);
            _container.Controls.Add(_lblCount);

            void ApplyLayout()
            {
                int containerWidth = Math.Min(1140, ClientSize.Width - 36);
                int left = (ClientSize.Width - containerWidth) / 2;
                _container.SetBounds(left, 14, containerWidth, ClientSize.Height - 20);

                _titleBadge.Location = new Point(0, 4);
                _titleBadge.Region = new Region(RoundedRect(new Rectangle(0, 0, _titleBadge.Width, _titleBadge.Height), 16));
                lblTitle.Location = new Point(_titleBadge.Right + 14, -2);
                lblSubtitle.Location = new Point(_titleBadge.Right + 16, 52);

                int gap = 12;
                int cardTop = 90;
                int cardW = (containerWidth - gap * 2) / 3;
                _cardTotal.SetBounds(0, cardTop, cardW, 92);
                _cardDebtors.SetBounds(cardW + gap, cardTop, cardW, 92);
                _cardOutstanding.SetBounds((cardW + gap) * 2, cardTop, cardW, 92);

                int searchTop = cardTop + 108;
                int actionGap = 12;
                int debtBtnW = 186;
                int addBtnW = 106;
                int actionWidth = debtBtnW + addBtnW + actionGap;
                int checkWidth = _chkOnlyDebtors.PreferredSize.Width;
                int searchWidth = containerWidth - actionWidth - checkWidth - (actionGap * 2);
                searchWidth = Math.Max(320, Math.Min(760, searchWidth));
                _searchWrap.SetBounds(0, searchTop, searchWidth, 48);
                _txtSearch.SetBounds(42, 14, _searchWrap.Width - 54, 22);

                int checkLeft = _searchWrap.Right + actionGap;
                int checkTop = searchTop + ((_searchWrap.Height - _chkOnlyDebtors.PreferredSize.Height) / 2);
                _chkOnlyDebtors.Location = new Point(checkLeft, checkTop);

                int actionsLeft = containerWidth - actionWidth;
                _btnDebtors.SetBounds(actionsLeft, searchTop, debtBtnW, 48);
                _btnAdd.SetBounds(actionsLeft + debtBtnW + actionGap, searchTop, addBtnW, 48);

                int availableGridHeight = _container.Height - (searchTop + 116);
                int gridHeight = Math.Min(460, availableGridHeight);
                gridHeight = Math.Max(250, gridHeight);
                _gridWrap.SetBounds(0, searchTop + 64, containerWidth, gridHeight);
                _grid.Padding = new Padding(1);
                _lblCount.Location = new Point((containerWidth - _lblCount.Width) / 2, _gridWrap.Bottom + 8);

                _cardTotal.Region = new Region(RoundedRect(new Rectangle(0, 0, _cardTotal.Width, _cardTotal.Height), 14));
                _cardDebtors.Region = new Region(RoundedRect(new Rectangle(0, 0, _cardDebtors.Width, _cardDebtors.Height), 14));
                _cardOutstanding.Region = new Region(RoundedRect(new Rectangle(0, 0, _cardOutstanding.Width, _cardOutstanding.Height), 14));
                _searchWrap.Region = new Region(RoundedRect(new Rectangle(0, 0, _searchWrap.Width, _searchWrap.Height), 14));
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
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                BackColor = Color.Transparent
            };

            valueLabel.Left = 16;
            valueLabel.Top = 38;
            valueLabel.Width = Math.Max(80, card.Width - rightIconReserve - 16);
            valueLabel.Height = 42;
            valueLabel.ForeColor = Color.White;
            valueLabel.Font = new Font("Bahnschrift SemiBold", 20, FontStyle.Bold);
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
                    DrawCardIcon(e.Graphics, iconKind, iconRect, Color.FromArgb(145, 255, 255, 255));
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

            if (string.Equals(kind, "warning", StringComparison.OrdinalIgnoreCase))
            {
                Rectangle ring = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
                g.DrawEllipse(pen, ring);
                Rectangle dot = new Rectangle(rect.X + rect.Width / 2 - 2, rect.Bottom - 14, 4, 4);
                using SolidBrush dotBrush = new SolidBrush(color);
                g.FillEllipse(dotBrush, dot);
                g.DrawLine(pen, rect.X + rect.Width / 2, rect.Y + 13, rect.X + rect.Width / 2, rect.Bottom - 19);
                return;
            }

            // users
            g.DrawEllipse(pen, rect.X + 6, rect.Y + 7, 13, 13);
            g.DrawArc(pen, rect.X + 2, rect.Y + 20, 22, 16, 16, 148);
            g.DrawEllipse(pen, rect.X + 23, rect.Y + 10, 11, 11);
            g.DrawArc(pen, rect.X + 19, rect.Y + 21, 18, 14, 14, 146);
        }

        private static void StyleActionButton(Button button, string text, string iconGlyph, Color c1, Color c2)
        {
            button.Text = text;
            button.Tag = iconGlyph;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
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
            button.Resize += (s, e) => button.Region = new Region(RoundedRect(new Rectangle(0, 0, button.Width, button.Height), 14));
        }

        private void AddCustomer_Click(object? sender, EventArgs e)
        {
            if (!ShowCustomerEditor(null))
            {
                return;
            }

            LoadData();
        }

        private bool ShowCustomerEditor(int? customerId)
        {
            using Form f = new Form
            {
                Text = customerId.HasValue ? "Mijozni tahrirlash" : "Yangi mijoz",
                Size = new Size(420, 260),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            TextBox txtName = new TextBox { Left = 120, Top = 20, Width = 260 };
            TextBox txtPhone = new TextBox { Left = 120, Top = 58, Width = 260 };
            TextBox txtNote = new TextBox { Left = 120, Top = 96, Width = 260, Height = 58, Multiline = true };

            if (customerId.HasValue)
            {
                Customer? existing = _customerService.GetById(customerId.Value);
                if (existing != null)
                {
                    txtName.Text = existing.FullName;
                    txtPhone.Text = existing.Phone;
                    txtNote.Text = existing.Note;
                }
            }

            f.Controls.Add(new Label { Text = "F.I.Sh *", Left = 20, Top = 24, Width = 90 });
            f.Controls.Add(new Label { Text = "Telefon", Left = 20, Top = 62, Width = 90 });
            f.Controls.Add(new Label { Text = "Izoh", Left = 20, Top = 100, Width = 90 });
            f.Controls.Add(txtName);
            f.Controls.Add(txtPhone);
            f.Controls.Add(txtNote);

            Button btnSave = new Button { Text = customerId.HasValue ? "Yangilash" : "Saqlash", Left = 296, Top = 168, Width = 84 };
            btnSave.Click += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Mijoz nomini kiriting.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    if (customerId.HasValue)
                    {
                        _customerService.UpdateCustomer(customerId.Value, txtName.Text, txtPhone.Text, txtNote.Text);
                    }
                    else
                    {
                        _customerService.FindOrCreate(txtName.Text, txtPhone.Text, txtNote.Text);
                    }

                    f.DialogResult = DialogResult.OK;
                    f.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            f.Controls.Add(btnSave);

            return f.ShowDialog(this) == DialogResult.OK;
        }

        private void LoadData()
        {
            int totalCustomers = _customerService.GetTotalCustomerCount();
            int debtorCustomers = _customerService.GetDebtorCustomerCount();
            double outstanding = _customerService.GetOverview(string.Empty, true).Sum(x => x.OutstandingUZS);

            _lblTotalCustomers.Text = totalCustomers.ToString("N0");
            _lblDebtorCustomers.Text = debtorCustomers.ToString("N0");
            _lblOutstanding.Text = $"{outstanding:N0} so'm";
            LoadGrid();
        }

        private void LoadGrid()
        {
            var rows = _customerService.GetOverview(_txtSearch.Text, _chkOnlyDebtors.Checked)
                .Select(x => new CustomerGridRow
                {
                    Id = x.Id,
                    Mijoz = x.FullName,
                    Telefon = string.IsNullOrWhiteSpace(x.Phone) ? "-" : x.Phone,
                    SavdoSoni = x.SalesCount.ToString(),
                    QarzlarSoni = x.OpenDebtCount.ToString(),
                    QoldiqUZS = $"{x.OutstandingUZS:N0} so'm"
                })
                .ToList();

            int currentRow = _grid.CurrentCell?.RowIndex ?? -1;
            int firstDisplayedRow = _grid.FirstDisplayedScrollingRowIndex >= 0 ? _grid.FirstDisplayedScrollingRowIndex : 0;

            _grid.SuspendLayout();
            _gridSource.DataSource = rows;
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

            _lblCount.Text = $"Jami: {rows.Count} ta mijoz ko'rsatilmoqda";
            _lblCount.Left = Math.Max(0, (_container.Width - _lblCount.Width) / 2);
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mijoz", HeaderText = "Mijoz", DataPropertyName = "Mijoz", FillWeight = 28 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Telefon", HeaderText = "Telefon", DataPropertyName = "Telefon", FillWeight = 22 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SavdoSoni", HeaderText = "SavdoSoni", DataPropertyName = "SavdoSoni", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "QarzlarSoni", HeaderText = "QarzlarSoni", DataPropertyName = "QarzlarSoni", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "QoldiqUZS", HeaderText = "QoldiqUZS", DataPropertyName = "QoldiqUZS", FillWeight = 18 });

            var editCol = new DataGridViewButtonColumn
            {
                Name = EditColumnName,
                HeaderText = "Amallar",
                Text = "\uE70F",
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                Width = 92,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            editCol.DefaultCellStyle.BackColor = Color.FromArgb(237, 220, 255);
            editCol.DefaultCellStyle.ForeColor = Color.FromArgb(118, 50, 211);
            editCol.DefaultCellStyle.SelectionBackColor = Color.FromArgb(226, 200, 255);
            editCol.DefaultCellStyle.SelectionForeColor = Color.FromArgb(92, 36, 166);
            editCol.DefaultCellStyle.Font = new Font("Segoe MDL2 Assets", 13, FontStyle.Regular);
            editCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(editCol);

            var delCol = new DataGridViewButtonColumn
            {
                Name = DeleteColumnName,
                HeaderText = " ",
                Text = "\uE74D",
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                Width = 62,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            delCol.DefaultCellStyle.BackColor = Color.FromArgb(255, 221, 227);
            delCol.DefaultCellStyle.ForeColor = Color.FromArgb(201, 35, 64);
            delCol.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 205, 214);
            delCol.DefaultCellStyle.SelectionForeColor = Color.FromArgb(167, 22, 48);
            delCol.DefaultCellStyle.Font = new Font("Segoe MDL2 Assets", 13, FontStyle.Regular);
            delCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(delCol);

            foreach (DataGridViewColumn column in _grid.Columns)
            {
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private static void SetGridDoubleBuffered(DataGridView grid)
        {
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(grid, true, null);
        }

        private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex] == null)
            {
                return;
            }

            string columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName != EditColumnName && columnName != DeleteColumnName)
            {
                return;
            }

            object? idObj = _grid.Rows[e.RowIndex].Cells["Id"].Value;
            if (idObj == null || !int.TryParse(idObj.ToString(), out int customerId))
            {
                MessageBox.Show("Mijoz identifikatori topilmadi.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (columnName == EditColumnName)
            {
                if (ShowCustomerEditor(customerId))
                {
                    LoadData();
                }
                return;
            }

            if (MessageBox.Show("Mijozni o'chirishni tasdiqlaysizmi?", "Tasdiq", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _customerService.DeleteCustomer(customerId);
                LoadData();
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

        private sealed class CustomerGridRow
        {
            public int Id { get; set; }
            public string Mijoz { get; set; } = string.Empty;
            public string Telefon { get; set; } = string.Empty;
            public string SavdoSoni { get; set; } = "0";
            public string QarzlarSoni { get; set; } = "0";
            public string QoldiqUZS { get; set; } = "0 so'm";
        }
    }
}



