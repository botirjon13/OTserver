using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using SantexnikaSRM.Utils;

namespace SantexnikaSRM.Forms
{
    public class UserManagementForm : Form
    {
        private readonly DatabaseHelper _dbHelper = new DatabaseHelper();
        private readonly AppUser _currentUser;

        private readonly Panel _content = new Panel();
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _txtUsername = new TextBox();
        private readonly TextBox _txtPassword = new TextBox();
        private readonly ComboBox _cmbRole = new ComboBox();
        private readonly Button _btnAdd = new Button();
        private readonly Button _btnUpdate = new Button();
        private readonly Button _btnDelete = new Button();

        private int? _selectedUserId;
        private string _selectedUserRole = string.Empty;

        public UserManagementForm(AppUser currentUser)
        {
            _currentUser = currentUser;

            if (!string.Equals(_currentUser.Role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Faqat admin foydalanuvchi akkauntlarni boshqara oladi.");
            }

            InitializeComponent();
            SantexnikaSRM.Utils.FormFx.EnsureFitsScreen(this);
            LoadUsers();
        }

        private void InitializeComponent()
        {
            Text = "Foydalanuvchilar Boshqaruvi";
            Size = new Size(1360, 760);
            MinimumSize = new Size(1100, 680);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(239, 242, 248);
            Font = new Font("Bahnschrift", 11, FontStyle.Regular);
            DoubleBuffered = true;

            Panel canvas = new Panel { Dock = DockStyle.Fill };
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
            Image? usersHeaderIcon = BrandingAssets.TryLoadAssetImage("tile-users.png");
            if (usersHeaderIcon != null)
            {
                titleIcon.BackColor = Color.Transparent;
                titleIcon.Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = usersHeaderIcon
                });
            }
            else
            {
                titleIcon.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using LinearGradientBrush brush = new LinearGradientBrush(titleIcon.ClientRectangle, Color.FromArgb(74, 56, 230), Color.FromArgb(90, 95, 246), 45f);
                    using GraphicsPath path = RoundedRect(titleIcon.ClientRectangle, 16);
                    e.Graphics.FillPath(brush, path);
                    TextRenderer.DrawText(e.Graphics, "\uE77B", UiTheme.IconFont(24), titleIcon.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                };
            }
            Label lblTitle = new Label
            {
                Text = "Foydalanuvchilar Boshqaruvi",
                AutoSize = true,
                Left = 66,
                Top = 4,
                Font = new Font("Bahnschrift SemiBold", 23, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 42, 65)
            };
            Label lblSub = new Label
            {
                Text = "Tizim foydalanuvchilarini boshqarish",
                AutoSize = true,
                Left = 68,
                Top = 40,
                Font = new Font("Bahnschrift", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 89, 116)
            };
            header.Controls.Add(titleIcon);
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            Panel editorCard = BuildEditorCard();
            Panel listCard = BuildListCard();

            _content.Controls.Add(listCard);
            _content.Controls.Add(editorCard);
            _content.Controls.Add(header);

            Action applyLayout = () =>
            {
                int w = Math.Min(1240, canvas.ClientSize.Width - 68);
                int x = (canvas.ClientSize.Width - w) / 2;
                _content.SetBounds(Math.Max(12, x), 14, w, Math.Max(600, canvas.ClientSize.Height - 24));
                header.SetBounds(0, 0, _content.Width, 90);
                editorCard.SetBounds(0, 102, _content.Width, 190);
                listCard.SetBounds(0, 316, _content.Width, _content.Height - 316);
            };

            canvas.Resize += (s, e) => applyLayout();
            Load += (s, e) => applyLayout();
            Shown += (s, e) => applyLayout();
            applyLayout();
        }

        private Panel BuildEditorCard()
        {
            Panel card = NewCard();

            Panel bar = NewBar("Yangi Foydalanuvchi Qo'shish", Color.FromArgb(74, 56, 230), Color.FromArgb(90, 95, 246), "\uE77B", "tile-users.png");
            bar.Dock = DockStyle.Top;

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 16, 18, 16) };

            Label lblUsername = NewCaption("Login", 0, 18);
            Panel userWrap = NewInputWrap();
            userWrap.SetBounds(0, 48, 290, 46);
            StyleTextBox(_txtUsername, "Login");
            userWrap.Controls.Add(_txtUsername);

            Label lblPassword = NewCaption("Parol", 306, 18);
            Panel passWrap = NewInputWrap();
            passWrap.SetBounds(306, 48, 290, 46);
            StyleTextBox(_txtPassword, "Yangi parol (ixtiyoriy)");
            passWrap.Controls.Add(_txtPassword);

            Label lblRole = NewCaption("Rol", 612, 18);
            Panel roleWrap = NewInputWrap();
            roleWrap.SetBounds(612, 48, 270, 46);
            _cmbRole.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbRole.FlatStyle = FlatStyle.Flat;
            _cmbRole.BackColor = Color.FromArgb(248, 250, 255);
            _cmbRole.ForeColor = Color.FromArgb(42, 58, 84);
            _cmbRole.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            _cmbRole.Left = 10;
            _cmbRole.Top = 8;
            _cmbRole.Width = 250;
            _cmbRole.Items.Add(DatabaseHelper.RoleAdmin);
            _cmbRole.Items.Add(DatabaseHelper.RoleSeller);
            _cmbRole.SelectedIndex = 1;
            roleWrap.Controls.Add(_cmbRole);

            _btnAdd.Text = "Qo'shish";
            _btnAdd.Click += BtnAdd_Click;
            ButtonStyle(_btnAdd, Color.FromArgb(7, 188, 74), Color.FromArgb(8, 175, 69));
            _btnAdd.SetBounds(840, 48, 98, 46);

            _btnUpdate.Text = "Saqlash";
            _btnUpdate.Click += BtnUpdate_Click;
            ButtonStyle(_btnUpdate, Color.FromArgb(40, 106, 243), Color.FromArgb(33, 95, 224));
            _btnUpdate.SetBounds(950, 48, 98, 46);

            _btnDelete.Text = "O'chirish";
            _btnDelete.Click += BtnDelete_Click;
            ButtonStyle(_btnDelete, Color.FromArgb(246, 30, 56), Color.FromArgb(228, 22, 49));
            _btnDelete.SetBounds(1060, 48, 106, 46);

            body.Controls.Add(lblUsername);
            body.Controls.Add(userWrap);
            body.Controls.Add(lblPassword);
            body.Controls.Add(passWrap);
            body.Controls.Add(lblRole);
            body.Controls.Add(roleWrap);
            body.Controls.Add(_btnAdd);
            body.Controls.Add(_btnUpdate);
            body.Controls.Add(_btnDelete);

            body.Resize += (s, e) =>
            {
                int buttons = 326;
                int colGap = 28;
                int buttonGap = 40;
                int start = body.ClientSize.Width - buttons - 38;
                int fieldsRight = Math.Max(320, start - buttonGap);
                int fields = Math.Max(420, fieldsRight - colGap * 2);
                int w = Math.Min(236, fields / 3);
                int blockWidth = (w * 3) + (colGap * 2);
                int x1 = Math.Max(0, (fieldsRight - blockWidth) / 2);
                int x2 = x1 + w + colGap;
                int x3 = x2 + w + colGap;

                lblUsername.Left = x1;
                userWrap.SetBounds(x1, 48, w, 46);
                _txtUsername.Width = w - 24;
                lblPassword.Left = x2;
                passWrap.SetBounds(x2, 48, w, 46);
                _txtPassword.Width = w - 24;
                lblRole.Left = x3;
                roleWrap.SetBounds(x3, 48, w, 46);
                _cmbRole.Width = w - 24;

                _btnAdd.SetBounds(start, 48, 98, 46);
                _btnUpdate.SetBounds(start + 110, 48, 98, 46);
                _btnDelete.SetBounds(start + 220, 48, 106, 46);
            };

            card.Controls.Add(body);
            card.Controls.Add(bar);
            return card;
        }

        private Panel BuildListCard()
        {
            Panel card = NewCard();
            Panel bar = NewBar("Foydalanuvchilar Ro'yxati", Color.FromArgb(136, 31, 226), Color.FromArgb(172, 70, 234), "\uE716", "tile-users.png");
            bar.Dock = DockStyle.Top;

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0, 10, 0, 0) };

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BorderStyle = BorderStyle.None;
            _grid.BackgroundColor = Color.White;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.GridColor = Color.FromArgb(229, 236, 247);
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ColumnHeadersHeight = 46;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 248, 253);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(63, 76, 97);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(245, 248, 253);
            _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(63, 76, 97);
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 2, 0, 2);
            _grid.DefaultCellStyle.BackColor = Color.White;
            _grid.DefaultCellStyle.ForeColor = Color.FromArgb(40, 54, 75);
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(206, 222, 247);
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(19, 33, 58);
            _grid.DefaultCellStyle.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 252, 255);
            _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(206, 222, 247);
            _grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.FromArgb(19, 33, 58);
            _grid.RowTemplate.Height = 42;
            _grid.CellClick += Grid_CellClick;
            _grid.CellPainting += Grid_CellPainting;

            body.Controls.Add(_grid);
            card.Controls.Add(body);
            card.Controls.Add(bar);
            return card;
        }

        private void LoadUsers()
        {
            List<AppUser> users = _dbHelper.GetAllUsers();
            _grid.DataSource = null;
            _grid.DataSource = users;
            ConfigureGridColumns();

            _selectedUserId = null;
            _selectedUserRole = string.Empty;
            _txtUsername.Clear();
            _txtPassword.Clear();
            _cmbRole.SelectedItem = DatabaseHelper.RoleSeller;
            _grid.ClearSelection();
            UpdateActionButtons();
        }

        private void ConfigureGridColumns()
        {
            if (_grid.Columns.Count == 0)
            {
                return;
            }

            _grid.Columns[nameof(AppUser.Id)].HeaderText = "Id";
            _grid.Columns[nameof(AppUser.Id)].FillWeight = 15;

            _grid.Columns[nameof(AppUser.Username)].HeaderText = "Username";
            _grid.Columns[nameof(AppUser.Username)].FillWeight = 35;

            _grid.Columns[nameof(AppUser.Role)].HeaderText = "Role";
            _grid.Columns[nameof(AppUser.Role)].FillWeight = 25;

            _grid.Columns[nameof(AppUser.MustChangePassword)].HeaderText = "Parolni almashtirish shart";
            _grid.Columns[nameof(AppUser.MustChangePassword)].FillWeight = 25;
            _grid.Columns[nameof(AppUser.MustChangePassword)].ReadOnly = true;
            _grid.Columns[nameof(AppUser.MustChangePassword)].SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns[nameof(AppUser.MustChangePassword)].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns[nameof(AppUser.MustChangePassword)].DefaultCellStyle.NullValue = false;

            foreach (DataGridViewColumn col in _grid.Columns)
            {
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            string columnName = _grid.Columns[e.ColumnIndex].DataPropertyName;
            if (!string.Equals(columnName, nameof(AppUser.Role), StringComparison.Ordinal)
                && !string.Equals(columnName, nameof(AppUser.MustChangePassword), StringComparison.Ordinal))
            {
                return;
            }
            if (e.Graphics == null)
            {
                return;
            }

            if (string.Equals(columnName, nameof(AppUser.MustChangePassword), StringComparison.Ordinal))
            {
                e.PaintBackground(e.CellBounds, true);
                bool isChecked = false;
                if (e.Value is bool b)
                {
                    isChecked = b;
                }
                else if (e.Value != null && bool.TryParse(Convert.ToString(e.Value), out bool parsed))
                {
                    isChecked = parsed;
                }

                int boxSize = 15;
                Rectangle box = new Rectangle(
                    e.CellBounds.X + ((e.CellBounds.Width - boxSize) / 2),
                    e.CellBounds.Y + ((e.CellBounds.Height - boxSize) / 2),
                    boxSize,
                    boxSize);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush fill = new SolidBrush(Color.White))
                using (Pen border = new Pen(Color.FromArgb(95, 114, 145), 1.8f))
                using (GraphicsPath path = RoundedRect(box, 3))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }

                if (isChecked)
                {
                    using Pen tick = new Pen(Color.FromArgb(28, 104, 214), 2.2f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    };
                    Point p1 = new Point(box.X + 3, box.Y + 8);
                    Point p2 = new Point(box.X + 6, box.Y + 11);
                    Point p3 = new Point(box.X + 12, box.Y + 5);
                    e.Graphics.DrawLines(tick, new[] { p1, p2, p3 });
                }

                e.Handled = true;
                return;
            }

            e.PaintBackground(e.CellBounds, true);
            string role = Convert.ToString(e.Value) ?? string.Empty;
            Color bg = role.Equals(DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(228, 217, 246)
                : Color.FromArgb(196, 215, 243);
            Color fg = role.Equals(DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(111, 43, 182)
                : Color.FromArgb(25, 76, 170);

            const int badgeWidth = 86;
            int badgeLeft = e.CellBounds.X + ((e.CellBounds.Width - badgeWidth) / 2);
            Rectangle badge = new Rectangle(badgeLeft, e.CellBounds.Y + 9, badgeWidth, e.CellBounds.Height - 18);
            using (SolidBrush brush = new SolidBrush(bg))
            using (GraphicsPath path = RoundedRect(badge, 12))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                role,
                new Font("Bahnschrift SemiBold", 10, FontStyle.Bold),
                badge,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            e.Handled = true;
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (_grid.Rows[e.RowIndex].DataBoundItem is not AppUser user)
            {
                return;
            }

            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
            _selectedUserId = user.Id;
            _selectedUserRole = user.Role ?? string.Empty;
            _txtUsername.Text = user.Username;
            _txtPassword.Clear();
            _cmbRole.SelectedItem = user.Role;
            UpdateActionButtons();
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            try
            {
                _dbHelper.AddUser(_txtUsername.Text, _txtPassword.Text, _cmbRole.Text);
                LoadUsers();
                UpdateActionButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnUpdate_Click(object? sender, EventArgs e)
        {
            if (_selectedUserId == null)
            {
                MessageBox.Show("Tahrirlash uchun foydalanuvchini tanlang.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _dbHelper.UpdateUser(_selectedUserId.Value, _txtUsername.Text, _txtPassword.Text, _cmbRole.Text);
                LoadUsers();
                UpdateActionButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_selectedUserId == null)
            {
                MessageBox.Show("O'chirish uchun foydalanuvchini tanlang.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_selectedUserId.Value == _currentUser.Id)
            {
                MessageBox.Show("Joriy foydalanuvchini o'chirib bo'lmaydi.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.Equals(_selectedUserRole, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Admin foydalanuvchini bu oynadan o'chirish bloklangan.", "Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _dbHelper.DeleteUser(_selectedUserId.Value);
                LoadUsers();
                UpdateActionButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateActionButtons()
        {
            bool hasSelection = _selectedUserId.HasValue;
            _btnUpdate.Enabled = hasSelection;
            bool canDelete = false;
            if (hasSelection && _selectedUserId.HasValue)
            {
                canDelete = _selectedUserId.Value != _currentUser.Id
                    && !string.Equals(_selectedUserRole, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase);
            }
            _btnDelete.Enabled = canDelete;
        }

        private static void StyleTextBox(TextBox textBox, string placeholder)
        {
            textBox.BorderStyle = BorderStyle.None;
            textBox.BackColor = Color.FromArgb(248, 250, 255);
            textBox.ForeColor = Color.FromArgb(42, 58, 84);
            textBox.Font = new Font("Bahnschrift", 12, FontStyle.Regular);
            textBox.PlaceholderText = placeholder;
            textBox.Left = 12;
            textBox.Top = 12;
            textBox.Width = 240;
        }

        private static Label NewCaption(string text, int left, int top)
        {
            return new Label
            {
                Text = text,
                Left = left,
                Top = top,
                AutoSize = true,
                Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(68, 82, 102)
            };
        }

        private static void ButtonStyle(Button button, Color c1, Color c2)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Bahnschrift SemiBold", 12, FontStyle.Bold);
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            button.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Color from = button.Enabled ? c1 : Color.FromArgb(156, 172, 196);
                Color to = button.Enabled ? c2 : Color.FromArgb(141, 160, 186);
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, button.Width - 3), Math.Max(1, button.Height - 3));
                using LinearGradientBrush brush = new LinearGradientBrush(bounds, from, to, 0f);
                using Pen border = new Pen(Color.FromArgb(67, 109, 173), 2.6f) { LineJoin = LineJoin.Round };
                using GraphicsPath path = RoundedRect(bounds, 10);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
                TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            button.Resize += (s, e) => button.Region = new Region(RoundedRect(new Rectangle(0, 0, Math.Max(1, button.Width), Math.Max(1, button.Height)), 10));
            button.EnabledChanged += (s, e) => button.Invalidate();
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
            Panel bar = new Panel { Height = 54 };
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
                    Left = 20,
                    Top = 17,
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
                    Left = 20,
                    Top = 17,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
            }

            Label title = new Label
            {
                Text = text,
                AutoSize = true,
                Left = 44,
                Top = 14,
                Font = new Font("Bahnschrift SemiBold", 15, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(icon);
            bar.Controls.Add(title);
            return bar;
        }

        private static Panel NewInputWrap()
        {
            Panel p = new Panel { BackColor = Color.White };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle bounds = new Rectangle(2, 2, Math.Max(1, p.Width - 5), Math.Max(1, p.Height - 5));
                using SolidBrush fill = new SolidBrush(Color.FromArgb(248, 250, 255));
                using Pen border = new Pen(Color.FromArgb(58, 113, 212), 3f)
                {
                    LineJoin = LineJoin.Round
                };
                using GraphicsPath path = RoundedRect(bounds, 9);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            p.Resize += (s, e) => p.Region = new Region(RoundedRect(new Rectangle(1, 1, Math.Max(1, p.Width - 2), Math.Max(1, p.Height - 2)), 9));
            return p;
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



