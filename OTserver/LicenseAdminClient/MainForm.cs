using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace LicenseAdminClient;

public class MainForm : Form
{
    private readonly TextBox _txtServer = new();
    private readonly TextBox _txtUser = new();
    private readonly TextBox _txtPass = new();
    private readonly Button _btnConnect = new();
    private readonly Button _btnEditConnection = new();
    private readonly Label _lblStatus = new();

    private readonly TabControl _tabs = new();
    private readonly TabPage _tabDashboard = new("Dashboard");
    private readonly TabPage _tabLicenses = new("Licenses");
    private readonly TabPage _tabDevices = new("Devices");
    private readonly TabPage _tabAudit = new("Audit");
    private readonly TabPage _tabManagement = new("Boshqaruv");
    private readonly TabPage _tabClients = new("Mijozlar");
    private readonly TabPage _tabUpdate = new("Yangilanish");

    private readonly Label _lblTotalLicenses = new();
    private readonly Label _lblActiveLicenses = new();
    private readonly Label _lblTotalDevices = new();
    private readonly Label _lblActiveToday = new();
    private readonly Button _btnRefreshStats = new();
    private readonly ComboBox _cmbDashboardRange = new();
    private readonly Label _lblExpiryWarning = new();
    private readonly Label _lblInactiveWarning = new();
    private readonly Label _lblEventTrend = new();
    private readonly Label _lblOnlineTrend = new();
    private readonly DataGridView _gridDashboardRecent = new();
    private readonly DataGridView _gridTopCustomers = new();
    private readonly DataGridView _gridTopLicenses = new();
    private readonly Button _btnGoLicenses = new();
    private readonly Button _btnGoClients = new();
    private readonly Button _btnGoDevices = new();
    private readonly System.Windows.Forms.Timer _dashboardAutoRefreshTimer = new();
    private readonly System.Windows.Forms.Timer _licenseSearchDebounceTimer = new();
    private readonly System.Windows.Forms.Timer _deviceSearchDebounceTimer = new();

    private readonly DataGridView _gridLicenses = new();
    private readonly TextBox _txtCustomer = new();
    private readonly NumericUpDown _numMaxDevices = new();
    private readonly TextBox _txtExpires = new();
    private readonly TextBox _txtLicenseKeyEdit = new();
    private readonly Button _btnCreateLicense = new();
    private readonly Button _btnSaveLicense = new();
    private readonly Button _btnToggleLicense = new();
    private readonly Button _btnDeleteLicense = new();
    private readonly Button _btnCopyLicense = new();
    private readonly Button _btnRefreshLicenses = new();
    private readonly TextBox _txtLicenseSearch = new();
    private readonly ComboBox _cmbLicenseStatus = new();
    private readonly Button _btnExportLicenses = new();

    private readonly DataGridView _gridDevices = new();
    private readonly Button _btnRefreshDevices = new();
    private readonly Button _btnUnlinkDevice = new();
    private readonly TextBox _txtDeviceSearch = new();
    private readonly Button _btnExportDevices = new();

    private readonly DataGridView _gridAudit = new();
    private readonly Button _btnRefreshAudit = new();

    private readonly Button _btnLicenseHistory = new();
    private readonly DataGridView _gridUsers = new();
    private readonly Button _btnRefreshUsers = new();
    private readonly Button _btnCreateUser = new();
    private readonly Button _btnSetUserPassword = new();
    private readonly TextBox _txtNewUsername = new();
    private readonly TextBox _txtNewUserPassword = new();
    private readonly ComboBox _cmbNewUserRole = new();
    private readonly Button _btnResetMyPassword = new();

    private readonly TextBox _txtVersion = new();
    private readonly TextBox _txtVersionUrl = new();
    private readonly TextBox _txtVersionSha256 = new();
    private readonly TextBox _txtVersionNote = new();
    private readonly CheckBox _chkVersionMandatory = new();
    private readonly Button _btnLoadVersion = new();
    private readonly Button _btnSaveVersion = new();

    private readonly DataGridView _gridBackups = new();
    private readonly Button _btnRefreshBackups = new();
    private readonly Button _btnCreateBackup = new();
    private readonly Button _btnDownloadBackup = new();
    private readonly Button _btnRestoreBackup = new();
    private readonly DataGridView _gridClientTelemetry = new();
    private readonly Button _btnRefreshClientTelemetry = new();
    private readonly TextBox _txtTelemetryLicense = new();
    private readonly TextBox _txtTelemetryDevice = new();
    private readonly DataGridView _gridFirstLogin = new();
    private readonly Button _btnRefreshFirstLogin = new();
    private readonly TextBox _txtFirstLoginLicense = new();
    private readonly TextBox _txtFirstLoginDevice = new();
    private readonly TextBox _txtResetLicenseKey = new();
    private readonly TextBox _txtResetDeviceId = new();
    private readonly TextBox _txtResetUsername = new();
    private readonly TextBox _txtResetTempPassword = new();
    private readonly Button _btnSendClientReset = new();
    private readonly TextBox _txtSupportContact = new();
    private readonly Button _btnSaveSupportContact = new();

    private ApiClient? _api;
    private LicenseItem? _selectedLicense;
    private DeviceItem? _selectedDevice;
    private string _currentRole = "Operator";
    private readonly string _connConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SantexnikaSRM",
        "license_admin_client_connection.json");

    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "License Admin Client";
        Width = 1200;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(980, 620);
        Font = new System.Drawing.Font("Bahnschrift", 10.5f);

        Panel top = new() { Dock = DockStyle.Top, Height = 110, Padding = new Padding(14, 12, 14, 10) };
        top.BackColor = System.Drawing.Color.FromArgb(240, 245, 252);

        Label lbl1 = NewLabel("Server URL", 12, 12);
        _txtServer.SetBounds(12, 34, 310, 32);
        _txtServer.Text = "http://localhost:5000";

        Label lbl2 = NewLabel("Admin login", 334, 12);
        _txtUser.SetBounds(334, 34, 190, 32);
        _txtUser.Text = "admin";

        Label lbl3 = NewLabel("Parol", 536, 12);
        _txtPass.SetBounds(536, 34, 190, 32);
        _txtPass.UseSystemPasswordChar = true;
        _txtPass.Text = string.Empty;

        _btnConnect.Text = "Ulanish";
        _btnConnect.SetBounds(740, 33, 140, 34);
        _btnConnect.Click += BtnConnect_Click;

        _btnEditConnection.Text = "Qayta kiritish";
        _btnEditConnection.SetBounds(890, 33, 150, 34);
        _btnEditConnection.Visible = false;
        _btnEditConnection.Click += (_, _) => EnableConnectionEditing();

        _lblStatus.SetBounds(12, 72, 860, 24);
        _lblStatus.ForeColor = System.Drawing.Color.FromArgb(46, 86, 150);
        _lblStatus.Text = "Serverga ulanmagan.";

        top.Controls.Add(lbl1);
        top.Controls.Add(_txtServer);
        top.Controls.Add(lbl2);
        top.Controls.Add(_txtUser);
        top.Controls.Add(lbl3);
        top.Controls.Add(_txtPass);
        top.Controls.Add(_btnConnect);
        top.Controls.Add(_btnEditConnection);
        top.Controls.Add(_lblStatus);

        _tabs.Dock = DockStyle.Fill;
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new System.Drawing.Size(130, 30);
        _tabs.TabPages.Add(_tabDashboard);
        _tabs.TabPages.Add(_tabLicenses);
        _tabs.TabPages.Add(_tabDevices);
        _tabs.TabPages.Add(_tabAudit);
        _tabs.TabPages.Add(_tabManagement);
        _tabs.TabPages.Add(_tabClients);
        _tabs.TabPages.Add(_tabUpdate);
        _tabs.Enabled = false;

        BuildDashboardTab();
        BuildLicensesTab();
        BuildDevicesTab();
        BuildAuditTab();
        BuildManagementTab();
        BuildClientsTab();
        BuildUpdateTab();

        Controls.Add(_tabs);
        Controls.Add(top);

        LoadSavedConnection();
    }

    private void BuildDashboardTab()
    {
        Panel wrap = new() { Dock = DockStyle.Fill, Padding = new Padding(20), AutoScroll = true };
        _btnRefreshStats.Text = "Yangilash";
        _btnRefreshStats.SetBounds(20, 20, 120, 34);
        _btnRefreshStats.Click += async (_, _) => await RefreshStatsAsync();

        Label lblRange = NewLabel("Vaqt oralig'i", 160, 26);
        _cmbDashboardRange.SetBounds(250, 20, 150, 34);
        _cmbDashboardRange.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDashboardRange.Items.AddRange(new object[] { "Bugun", "7 kun", "30 kun" });
        _cmbDashboardRange.SelectedIndex = 1;
        _cmbDashboardRange.SelectedIndexChanged += async (_, _) => await RefreshDashboardInsightsAsync();

        var card1 = NewStatCard("Jami license", _lblTotalLicenses, 20, 68);
        var card2 = NewStatCard("Aktiv license", _lblActiveLicenses, 290, 68);
        var card3 = NewStatCard("Jami qurilma", _lblTotalDevices, 560, 68);
        var card4 = NewStatCard("Bugun online", _lblActiveToday, 830, 68);

        Panel alertsCard = new()
        {
            Left = 20,
            Top = 198,
            Width = 520,
            Height = 108,
            BackColor = System.Drawing.Color.White
        };
        Label alertsTitle = NewLabel("Ogohlantirishlar", 14, 12);
        alertsTitle.ForeColor = System.Drawing.Color.FromArgb(60, 84, 120);
        _lblExpiryWarning.Left = 14;
        _lblExpiryWarning.Top = 42;
        _lblExpiryWarning.Width = 490;
        _lblExpiryWarning.ForeColor = System.Drawing.Color.FromArgb(168, 95, 25);
        _lblInactiveWarning.Left = 14;
        _lblInactiveWarning.Top = 68;
        _lblInactiveWarning.Width = 490;
        _lblInactiveWarning.ForeColor = System.Drawing.Color.FromArgb(156, 45, 45);
        alertsCard.Controls.Add(alertsTitle);
        alertsCard.Controls.Add(_lblExpiryWarning);
        alertsCard.Controls.Add(_lblInactiveWarning);

        Panel quickCard = new()
        {
            Left = 560,
            Top = 198,
            Width = 520,
            Height = 108,
            BackColor = System.Drawing.Color.White
        };
        Label quickTitle = NewLabel("Tezkor amallar", 14, 12);
        quickTitle.ForeColor = System.Drawing.Color.FromArgb(60, 84, 120);
        _btnGoLicenses.Text = "Licenselar";
        _btnGoLicenses.SetBounds(14, 46, 150, 38);
        _btnGoLicenses.Click += (_, _) => _tabs.SelectedTab = _tabLicenses;
        _btnGoClients.Text = "Mijozlar";
        _btnGoClients.SetBounds(184, 46, 150, 38);
        _btnGoClients.Click += (_, _) => _tabs.SelectedTab = _tabClients;
        _btnGoDevices.Text = "Qurilmalar";
        _btnGoDevices.SetBounds(354, 46, 150, 38);
        _btnGoDevices.Click += (_, _) => _tabs.SelectedTab = _tabDevices;
        quickCard.Controls.Add(quickTitle);
        quickCard.Controls.Add(_btnGoLicenses);
        quickCard.Controls.Add(_btnGoClients);
        quickCard.Controls.Add(_btnGoDevices);

        GroupBox recentBox = new()
        {
            Text = "So'nggi faoliyat",
            Left = 20,
            Top = 320,
            Width = 1060,
            Height = 280
        };
        _gridDashboardRecent.SetBounds(12, 28, 1036, 238);
        _gridDashboardRecent.ReadOnly = true;
        _gridDashboardRecent.AllowUserToAddRows = false;
        _gridDashboardRecent.AllowUserToDeleteRows = false;
        _gridDashboardRecent.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridDashboardRecent.MultiSelect = false;
        _gridDashboardRecent.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        recentBox.Controls.Add(_gridDashboardRecent);

        Panel trendCard = new()
        {
            Left = 20,
            Top = 612,
            Width = 520,
            Height = 118,
            BackColor = System.Drawing.Color.White
        };
        Label trendTitle = NewLabel("Trend ko'rsatkichlari", 14, 12);
        trendTitle.ForeColor = System.Drawing.Color.FromArgb(60, 84, 120);
        _lblEventTrend.Left = 14;
        _lblEventTrend.Top = 42;
        _lblEventTrend.Width = 490;
        _lblEventTrend.ForeColor = System.Drawing.Color.FromArgb(44, 92, 164);
        _lblOnlineTrend.Left = 14;
        _lblOnlineTrend.Top = 68;
        _lblOnlineTrend.Width = 490;
        _lblOnlineTrend.ForeColor = System.Drawing.Color.FromArgb(28, 126, 88);
        trendCard.Controls.Add(trendTitle);
        trendCard.Controls.Add(_lblEventTrend);
        trendCard.Controls.Add(_lblOnlineTrend);

        GroupBox topBox = new()
        {
            Text = "Top ro'yxatlar",
            Left = 560,
            Top = 612,
            Width = 520,
            Height = 270
        };
        Label topCustomersTitle = NewLabel("Top mijozlar (qurilma soni)", 12, 26);
        _gridTopCustomers.SetBounds(12, 46, 496, 90);
        _gridTopCustomers.ReadOnly = true;
        _gridTopCustomers.AllowUserToAddRows = false;
        _gridTopCustomers.AllowUserToDeleteRows = false;
        _gridTopCustomers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridTopCustomers.MultiSelect = false;
        _gridTopCustomers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        Label topLicensesTitle = NewLabel("Top licenselar (eng faol)", 12, 146);
        _gridTopLicenses.SetBounds(12, 166, 496, 90);
        _gridTopLicenses.ReadOnly = true;
        _gridTopLicenses.AllowUserToAddRows = false;
        _gridTopLicenses.AllowUserToDeleteRows = false;
        _gridTopLicenses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridTopLicenses.MultiSelect = false;
        _gridTopLicenses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        topBox.Controls.Add(topCustomersTitle);
        topBox.Controls.Add(_gridTopCustomers);
        topBox.Controls.Add(topLicensesTitle);
        topBox.Controls.Add(_gridTopLicenses);

        wrap.Controls.Add(_btnRefreshStats);
        wrap.Controls.Add(lblRange);
        wrap.Controls.Add(_cmbDashboardRange);
        wrap.Controls.Add(card1);
        wrap.Controls.Add(card2);
        wrap.Controls.Add(card3);
        wrap.Controls.Add(card4);
        wrap.Controls.Add(alertsCard);
        wrap.Controls.Add(quickCard);
        wrap.Controls.Add(recentBox);
        wrap.Controls.Add(trendCard);
        wrap.Controls.Add(topBox);
        _tabDashboard.Controls.Add(wrap);

        _dashboardAutoRefreshTimer.Interval = 60000;
        _dashboardAutoRefreshTimer.Tick += async (_, _) =>
        {
            if (_api != null && _tabs.Enabled)
            {
                await RefreshStatsAsync();
            }
        };
        _dashboardAutoRefreshTimer.Start();
    }

    private void BuildLicensesTab()
    {
        Panel wrap = new() { Dock = DockStyle.Fill, Padding = new Padding(14) };
        Panel form = new() { Dock = DockStyle.Top, Height = 148 };

        Label l1 = NewLabel("Mijoz", 0, 8);
        _txtCustomer.SetBounds(0, 30, 220, 30);

        Label l2 = NewLabel("Qurilma limiti", 232, 8);
        _numMaxDevices.SetBounds(232, 30, 110, 30);
        _numMaxDevices.Minimum = 1;
        _numMaxDevices.Maximum = 100;
        _numMaxDevices.Value = 1;

        Label l3 = NewLabel("Muddat (YYYY-MM-DD)", 354, 8);
        _txtExpires.SetBounds(354, 30, 150, 30);

        Label l4 = NewLabel("License key", 516, 8);
        _txtLicenseKeyEdit.SetBounds(516, 30, 300, 30);

        _btnCreateLicense.Text = "License yaratish";
        _btnCreateLicense.SetBounds(0, 68, 150, 32);
        _btnCreateLicense.Click += async (_, _) => await CreateLicenseAsync();

        _btnSaveLicense.Text = "Tanlanganni saqlash";
        _btnSaveLicense.SetBounds(160, 68, 170, 32);
        _btnSaveLicense.Click += async (_, _) => await SaveSelectedLicenseAsync();

        _btnToggleLicense.Text = "Tanlanganni block/active";
        _btnToggleLicense.SetBounds(340, 68, 170, 32);
        _btnToggleLicense.Click += async (_, _) => await ToggleLicenseAsync();

        _btnDeleteLicense.Text = "Tanlanganni o'chirish";
        _btnDeleteLicense.SetBounds(520, 68, 130, 32);
        _btnDeleteLicense.BackColor = System.Drawing.Color.FromArgb(226, 82, 82);
        _btnDeleteLicense.ForeColor = System.Drawing.Color.White;
        _btnDeleteLicense.FlatStyle = FlatStyle.Flat;
        _btnDeleteLicense.FlatAppearance.BorderSize = 0;
        _btnDeleteLicense.Click += async (_, _) => await DeleteSelectedLicenseAsync();

        _btnCopyLicense.Text = "Key nusxalash";
        _btnCopyLicense.SetBounds(660, 68, 130, 32);
        _btnCopyLicense.Click += (_, _) => CopySelectedLicenseKey();

        _btnRefreshLicenses.Text = "Yangilash";
        _btnRefreshLicenses.SetBounds(800, 68, 120, 32);
        _btnRefreshLicenses.Click += async (_, _) => await RefreshLicensesAsync();

        Label l5 = NewLabel("Qidiruv", 0, 108);
        _txtLicenseSearch.SetBounds(0, 126, 220, 30);
        _txtLicenseSearch.PlaceholderText = "key yoki mijoz...";
        _licenseSearchDebounceTimer.Interval = 350;
        _licenseSearchDebounceTimer.Tick += async (_, _) =>
        {
            _licenseSearchDebounceTimer.Stop();
            await RefreshLicensesAsync();
        };
        _txtLicenseSearch.TextChanged += (_, _) =>
        {
            _licenseSearchDebounceTimer.Stop();
            _licenseSearchDebounceTimer.Start();
        };

        Label l6 = NewLabel("Holat", 232, 108);
        _cmbLicenseStatus.SetBounds(232, 126, 140, 30);
        _cmbLicenseStatus.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbLicenseStatus.Items.AddRange(new object[] { "All", "Active", "Blocked" });
        _cmbLicenseStatus.SelectedIndex = 0;
        _cmbLicenseStatus.SelectedIndexChanged += async (_, _) => await RefreshLicensesAsync();

        _btnExportLicenses.Text = "CSV export";
        _btnExportLicenses.SetBounds(384, 125, 120, 32);
        _btnExportLicenses.Click += async (_, _) => await ExportLicensesAsync();

        _btnLicenseHistory.Text = "History";
        _btnLicenseHistory.SetBounds(514, 125, 120, 32);
        _btnLicenseHistory.Click += async (_, _) => await ShowSelectedLicenseHistoryAsync();

        form.Controls.Add(l1);
        form.Controls.Add(_txtCustomer);
        form.Controls.Add(l2);
        form.Controls.Add(_numMaxDevices);
        form.Controls.Add(l3);
        form.Controls.Add(_txtExpires);
        form.Controls.Add(l4);
        form.Controls.Add(_txtLicenseKeyEdit);
        form.Controls.Add(_btnCreateLicense);
        form.Controls.Add(_btnSaveLicense);
        form.Controls.Add(_btnToggleLicense);
        form.Controls.Add(_btnDeleteLicense);
        form.Controls.Add(_btnCopyLicense);
        form.Controls.Add(_btnRefreshLicenses);
        form.Controls.Add(l5);
        form.Controls.Add(_txtLicenseSearch);
        form.Controls.Add(l6);
        form.Controls.Add(_cmbLicenseStatus);
        form.Controls.Add(_btnExportLicenses);
        form.Controls.Add(_btnLicenseHistory);

        _gridLicenses.Dock = DockStyle.Fill;
        _gridLicenses.ReadOnly = true;
        _gridLicenses.AllowUserToAddRows = false;
        _gridLicenses.AllowUserToDeleteRows = false;
        _gridLicenses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridLicenses.MultiSelect = false;
        _gridLicenses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridLicenses.SelectionChanged += (_, _) =>
        {
            _selectedLicense = _gridLicenses.CurrentRow?.DataBoundItem as LicenseItem;
            BindSelectedLicenseToEditor();
        };

        wrap.Controls.Add(_gridLicenses);
        wrap.Controls.Add(form);
        _tabLicenses.Controls.Add(wrap);
    }

    private void BuildDevicesTab()
    {
        Panel wrap = new() { Dock = DockStyle.Fill, Padding = new Padding(14) };
        _btnRefreshDevices.Text = "Yangilash";
        _btnRefreshDevices.SetBounds(0, 0, 110, 32);
        _btnRefreshDevices.Click += async (_, _) => await RefreshDevicesAsync();

        _txtDeviceSearch.SetBounds(300, 0, 260, 32);
        _txtDeviceSearch.PlaceholderText = "qidiruv: key/device...";
        _deviceSearchDebounceTimer.Interval = 350;
        _deviceSearchDebounceTimer.Tick += async (_, _) =>
        {
            _deviceSearchDebounceTimer.Stop();
            await RefreshDevicesAsync();
        };
        _txtDeviceSearch.TextChanged += (_, _) =>
        {
            _deviceSearchDebounceTimer.Stop();
            _deviceSearchDebounceTimer.Start();
        };

        _btnExportDevices.Text = "CSV export";
        _btnExportDevices.SetBounds(570, 0, 120, 32);
        _btnExportDevices.Click += async (_, _) => await ExportDevicesAsync();

        _gridDevices.Dock = DockStyle.Fill;
        _gridDevices.ReadOnly = true;
        _gridDevices.AllowUserToAddRows = false;
        _gridDevices.AllowUserToDeleteRows = false;
        _gridDevices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridDevices.MultiSelect = false;
        _gridDevices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridDevices.SelectionChanged += (_, _) =>
        {
            _selectedDevice = _gridDevices.CurrentRow?.DataBoundItem as DeviceItem;
        };

        _btnUnlinkDevice.Text = "Tanlanganni unlink";
        _btnUnlinkDevice.SetBounds(120, 0, 170, 32);
        _btnUnlinkDevice.BackColor = System.Drawing.Color.FromArgb(226, 82, 82);
        _btnUnlinkDevice.ForeColor = System.Drawing.Color.White;
        _btnUnlinkDevice.FlatStyle = FlatStyle.Flat;
        _btnUnlinkDevice.FlatAppearance.BorderSize = 0;
        _btnUnlinkDevice.Click += async (_, _) => await UnlinkSelectedDeviceAsync();

        Panel top = new() { Dock = DockStyle.Top, Height = 40 };
        top.Controls.Add(_btnRefreshDevices);
        top.Controls.Add(_btnUnlinkDevice);
        top.Controls.Add(_txtDeviceSearch);
        top.Controls.Add(_btnExportDevices);

        wrap.Controls.Add(_gridDevices);
        wrap.Controls.Add(top);
        _tabDevices.Controls.Add(wrap);
    }

    private void BuildAuditTab()
    {
        Panel wrap = new() { Dock = DockStyle.Fill, Padding = new Padding(14) };
        _btnRefreshAudit.Text = "Yangilash";
        _btnRefreshAudit.SetBounds(0, 0, 110, 32);
        _btnRefreshAudit.Click += async (_, _) => await RefreshAuditAsync();

        _gridAudit.Dock = DockStyle.Fill;
        _gridAudit.ReadOnly = true;
        _gridAudit.AllowUserToAddRows = false;
        _gridAudit.AllowUserToDeleteRows = false;
        _gridAudit.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridAudit.MultiSelect = false;
        _gridAudit.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        Panel top = new() { Dock = DockStyle.Top, Height = 40 };
        top.Controls.Add(_btnRefreshAudit);

        wrap.Controls.Add(_gridAudit);
        wrap.Controls.Add(top);
        _tabAudit.Controls.Add(wrap);
    }

    private void BuildManagementTab()
    {
        Panel wrap = new() { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };

        GroupBox usersBox = new() { Text = "Admin foydalanuvchilar", Left = 10, Top = 10, Width = 560, Height = 300 };
        _btnRefreshUsers.Text = "Yangilash";
        _btnRefreshUsers.SetBounds(10, 26, 100, 30);
        _btnRefreshUsers.Click += async (_, _) => await RefreshUsersAsync();

        _btnResetMyPassword.Text = "Parolimni almashtirish";
        _btnResetMyPassword.SetBounds(120, 26, 190, 30);
        _btnResetMyPassword.Click += async (_, _) => await ResetMyPasswordAsync();

        _gridUsers.SetBounds(10, 64, 540, 150);
        _gridUsers.ReadOnly = true;
        _gridUsers.AllowUserToAddRows = false;
        _gridUsers.AllowUserToDeleteRows = false;
        _gridUsers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridUsers.MultiSelect = false;
        _gridUsers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        Label lu1 = NewLabel("Username", 10, 222);
        _txtNewUsername.SetBounds(10, 242, 150, 30);
        Label lu2 = NewLabel("Parol", 170, 222);
        _txtNewUserPassword.SetBounds(170, 242, 140, 30);
        _txtNewUserPassword.UseSystemPasswordChar = true;
        Label lu3 = NewLabel("Role", 320, 222);
        _cmbNewUserRole.SetBounds(320, 242, 100, 30);
        _cmbNewUserRole.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbNewUserRole.Items.AddRange(new object[] { "Operator", "SuperAdmin" });
        _cmbNewUserRole.SelectedIndex = 0;
        _btnCreateUser.Text = "User yaratish";
        _btnCreateUser.SetBounds(430, 242, 120, 30);
        _btnCreateUser.Click += async (_, _) => await CreateAdminUserAsync();

        _btnSetUserPassword.Text = "Tanlangan user paroli";
        _btnSetUserPassword.SetBounds(360, 26, 190, 30);
        _btnSetUserPassword.Click += async (_, _) => await SetSelectedUserPasswordAsync();

        usersBox.Controls.Add(_btnRefreshUsers);
        usersBox.Controls.Add(_btnResetMyPassword);
        usersBox.Controls.Add(_btnSetUserPassword);
        usersBox.Controls.Add(_gridUsers);
        usersBox.Controls.Add(lu1);
        usersBox.Controls.Add(_txtNewUsername);
        usersBox.Controls.Add(lu2);
        usersBox.Controls.Add(_txtNewUserPassword);
        usersBox.Controls.Add(lu3);
        usersBox.Controls.Add(_cmbNewUserRole);
        usersBox.Controls.Add(_btnCreateUser);

        GroupBox backupBox = new() { Text = "Backup va tiklash", Left = 580, Top = 10, Width = 560, Height = 300 };
        _btnRefreshBackups.Text = "Yangilash";
        _btnRefreshBackups.SetBounds(10, 26, 90, 30);
        _btnRefreshBackups.Click += async (_, _) => await RefreshBackupsAsync();
        _btnCreateBackup.Text = "Backup yaratish";
        _btnCreateBackup.SetBounds(110, 26, 120, 30);
        _btnCreateBackup.Click += async (_, _) => await CreateBackupAsync();
        _btnDownloadBackup.Text = "Yuklab olish";
        _btnDownloadBackup.SetBounds(240, 26, 110, 30);
        _btnDownloadBackup.Click += async (_, _) => await DownloadSelectedBackupAsync();
        _btnRestoreBackup.Text = "Restore";
        _btnRestoreBackup.SetBounds(360, 26, 90, 30);
        _btnRestoreBackup.BackColor = System.Drawing.Color.FromArgb(226, 82, 82);
        _btnRestoreBackup.ForeColor = System.Drawing.Color.White;
        _btnRestoreBackup.FlatStyle = FlatStyle.Flat;
        _btnRestoreBackup.FlatAppearance.BorderSize = 0;
        _btnRestoreBackup.Click += async (_, _) => await RestoreSelectedBackupAsync();

        _gridBackups.SetBounds(10, 64, 540, 226);
        _gridBackups.ReadOnly = true;
        _gridBackups.AllowUserToAddRows = false;
        _gridBackups.AllowUserToDeleteRows = false;
        _gridBackups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridBackups.MultiSelect = false;
        _gridBackups.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        backupBox.Controls.Add(_btnRefreshBackups);
        backupBox.Controls.Add(_btnCreateBackup);
        backupBox.Controls.Add(_btnDownloadBackup);
        backupBox.Controls.Add(_btnRestoreBackup);
        backupBox.Controls.Add(_gridBackups);

        wrap.Controls.Add(usersBox);
        wrap.Controls.Add(backupBox);
        _tabManagement.Controls.Add(wrap);
    }

    private void BuildClientsTab()
    {
        Panel wrap = new() { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };

        GroupBox clientResetBox = new() { Text = "Mijoz parolini masofadan tiklash", Left = 10, Top = 10, Width = 560, Height = 190 };
        Label lr1 = NewLabel("License key", 10, 30);
        _txtResetLicenseKey.SetBounds(10, 48, 260, 30);
        Label lr2 = NewLabel("Device ID (ixtiyoriy)", 280, 30);
        _txtResetDeviceId.SetBounds(280, 48, 270, 30);
        Label lr3 = NewLabel("Username", 10, 92);
        _txtResetUsername.SetBounds(10, 112, 170, 30);
        Label lr4 = NewLabel("Temporary parol", 190, 92);
        _txtResetTempPassword.SetBounds(190, 112, 220, 30);
        _txtResetTempPassword.UseSystemPasswordChar = true;
        _btnSendClientReset.Text = "Reset yuborish";
        _btnSendClientReset.SetBounds(420, 112, 130, 30);
        _btnSendClientReset.Click += async (_, _) => await SendClientPasswordResetAsync();
        clientResetBox.Controls.Add(lr1);
        clientResetBox.Controls.Add(_txtResetLicenseKey);
        clientResetBox.Controls.Add(lr2);
        clientResetBox.Controls.Add(_txtResetDeviceId);
        clientResetBox.Controls.Add(lr3);
        clientResetBox.Controls.Add(_txtResetUsername);
        clientResetBox.Controls.Add(lr4);
        clientResetBox.Controls.Add(_txtResetTempPassword);
        clientResetBox.Controls.Add(_btnSendClientReset);

        GroupBox telemetryBox = new() { Text = "Mijoz foydalanuvchi ma'lumotlari", Left = 10, Top = 220, Width = 1130, Height = 250 };
        Label lt1 = NewLabel("License key", 10, 28);
        _txtTelemetryLicense.SetBounds(10, 48, 260, 30);
        Label lt2 = NewLabel("Device ID", 280, 28);
        _txtTelemetryDevice.SetBounds(280, 48, 260, 30);
        _btnRefreshClientTelemetry.Text = "Yangilash";
        _btnRefreshClientTelemetry.SetBounds(550, 48, 120, 30);
        _btnRefreshClientTelemetry.Click += async (_, _) => await RefreshClientTelemetryAsync();

        _gridClientTelemetry.SetBounds(10, 86, 1110, 150);
        _gridClientTelemetry.ReadOnly = true;
        _gridClientTelemetry.AllowUserToAddRows = false;
        _gridClientTelemetry.AllowUserToDeleteRows = false;
        _gridClientTelemetry.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridClientTelemetry.MultiSelect = false;
        _gridClientTelemetry.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        telemetryBox.Controls.Add(lt1);
        telemetryBox.Controls.Add(_txtTelemetryLicense);
        telemetryBox.Controls.Add(lt2);
        telemetryBox.Controls.Add(_txtTelemetryDevice);
        telemetryBox.Controls.Add(_btnRefreshClientTelemetry);
        telemetryBox.Controls.Add(_gridClientTelemetry);

        GroupBox firstLoginBox = new() { Text = "Birinchi kirish login/parol (eslatma)", Left = 10, Top = 490, Width = 1130, Height = 260 };
        Label lf1 = NewLabel("License key", 10, 28);
        _txtFirstLoginLicense.SetBounds(10, 48, 260, 30);
        Label lf2 = NewLabel("Device ID", 280, 28);
        _txtFirstLoginDevice.SetBounds(280, 48, 260, 30);
        _btnRefreshFirstLogin.Text = "Yangilash";
        _btnRefreshFirstLogin.SetBounds(550, 48, 120, 30);
        _btnRefreshFirstLogin.Click += async (_, _) => await RefreshFirstLoginCredentialsAsync();

        _gridFirstLogin.SetBounds(10, 86, 1110, 160);
        _gridFirstLogin.ReadOnly = true;
        _gridFirstLogin.AllowUserToAddRows = false;
        _gridFirstLogin.AllowUserToDeleteRows = false;
        _gridFirstLogin.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridFirstLogin.MultiSelect = false;
        _gridFirstLogin.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        firstLoginBox.Controls.Add(lf1);
        firstLoginBox.Controls.Add(_txtFirstLoginLicense);
        firstLoginBox.Controls.Add(lf2);
        firstLoginBox.Controls.Add(_txtFirstLoginDevice);
        firstLoginBox.Controls.Add(_btnRefreshFirstLogin);
        firstLoginBox.Controls.Add(_gridFirstLogin);

        wrap.Controls.Add(clientResetBox);
        wrap.Controls.Add(telemetryBox);
        wrap.Controls.Add(firstLoginBox);
        _tabClients.Controls.Add(wrap);
    }

    private void BuildUpdateTab()
    {
        Panel wrap = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };
        GroupBox versionBox = new() { Text = "Dastur versiyasini boshqarish", Left = 10, Top = 10, Width = 1130, Height = 250 };
        Label lv1 = NewLabel("Version", 10, 28);
        _txtVersion.SetBounds(10, 48, 140, 32);
        Label lv2 = NewLabel("Update URL", 160, 28);
        _txtVersionUrl.SetBounds(160, 48, 960, 32);
        Label lvSha = NewLabel("SHA-256", 10, 88);
        _txtVersionSha256.SetBounds(10, 108, 1110, 32);
        Label lv3 = NewLabel("Izoh", 10, 148);
        _txtVersionNote.SetBounds(10, 168, 1110, 32);
        _chkVersionMandatory.Text = "Majburiy update";
        _chkVersionMandatory.SetBounds(10, 204, 170, 30);
        _btnLoadVersion.Text = "O'qish";
        _btnLoadVersion.SetBounds(980, 202, 70, 32);
        _btnLoadVersion.Click += async (_, _) => await LoadVersionAsync();
        _btnSaveVersion.Text = "Saqlash";
        _btnSaveVersion.SetBounds(1055, 202, 70, 32);
        _btnSaveVersion.Click += async (_, _) => await SaveVersionAsync();

        versionBox.Controls.Add(lv1);
        versionBox.Controls.Add(_txtVersion);
        versionBox.Controls.Add(lv2);
        versionBox.Controls.Add(_txtVersionUrl);
        versionBox.Controls.Add(lvSha);
        versionBox.Controls.Add(_txtVersionSha256);
        versionBox.Controls.Add(lv3);
        versionBox.Controls.Add(_txtVersionNote);
        versionBox.Controls.Add(_chkVersionMandatory);
        versionBox.Controls.Add(_btnLoadVersion);
        versionBox.Controls.Add(_btnSaveVersion);

        GroupBox contactBox = new() { Text = "Mijoz bilan aloqa ma'lumoti", Left = 10, Top = 270, Width = 1130, Height = 140 };
        Label lc1 = NewLabel("Kontakt matni", 10, 28);
        _txtSupportContact.SetBounds(10, 48, 980, 32);
        _btnSaveSupportContact.Text = "Saqlash";
        _btnSaveSupportContact.SetBounds(1000, 48, 120, 32);
        _btnSaveSupportContact.Click += async (_, _) => await SaveSupportContactAsync();
        contactBox.Controls.Add(lc1);
        contactBox.Controls.Add(_txtSupportContact);
        contactBox.Controls.Add(_btnSaveSupportContact);

        wrap.Controls.Add(versionBox);
        wrap.Controls.Add(contactBox);
        _tabUpdate.Controls.Add(wrap);
    }

    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        try
        {
            string baseUrl = _txtServer.Text.Trim();
            string user = _txtUser.Text.Trim();
            string pass = _txtPass.Text;
            if (!IsAllowedServerUrl(baseUrl))
            {
                throw new Exception("Xavfsizlik uchun faqat HTTPS URL ruxsat etiladi (localhost bundan mustasno).");
            }

            _api?.Dispose();
            _api = new ApiClient(baseUrl, user, pass);
            await _api.PingAsync();
            _tabs.Enabled = true;
            _lblStatus.Text = "Ulanish muvaffaqiyatli.";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(22, 123, 76);
            SaveConnection(baseUrl, user);
            _txtServer.Enabled = false;
            _txtUser.Enabled = false;
            _btnEditConnection.Visible = false;
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            _tabs.Enabled = false;
            _lblStatus.Text = $"Ulanishda xato: {ex.Message}";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(184, 48, 48);
            _btnEditConnection.Visible = true;
        }
    }

    private void LoadSavedConnection()
    {
        try
        {
            if (!File.Exists(_connConfigPath))
            {
                return;
            }

            string json = File.ReadAllText(_connConfigPath);
            var saved = JsonSerializer.Deserialize<ConnectionConfig>(json);
            if (saved == null || string.IsNullOrWhiteSpace(saved.ServerUrl) || string.IsNullOrWhiteSpace(saved.Username))
            {
                return;
            }

            _txtServer.Text = saved.ServerUrl;
            _txtUser.Text = saved.Username;
            _txtPass.Text = string.Empty;
            _txtServer.Enabled = false;
            _txtUser.Enabled = false;
            _btnEditConnection.Visible = false;
            _lblStatus.Text = "Server va login saqlangan. Parolni kiriting.";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(46, 86, 150);
        }
        catch
        {
            // Config o'qishda xato bo'lsa eski flow bilan davom etadi.
        }
    }

    private void SaveConnection(string serverUrl, string username)
    {
        try
        {
            string dir = Path.GetDirectoryName(_connConfigPath)!;
            Directory.CreateDirectory(dir);
            var saved = new ConnectionConfig
            {
                ServerUrl = serverUrl,
                Username = username
            };
            string json = JsonSerializer.Serialize(saved);
            File.WriteAllText(_connConfigPath, json);
        }
        catch
        {
            // Saqlash xatosi dastur ishiga halaqit bermasin.
        }
    }

    private void EnableConnectionEditing()
    {
        _txtServer.Enabled = true;
        _txtUser.Enabled = true;
        _txtServer.Focus();
    }

    private async Task RefreshAllAsync()
    {
        await RefreshStatsAsync();
        await RefreshLicensesAsync();
        await RefreshDevicesAsync();
        await RefreshAuditAsync();
        await RefreshUsersAsync();
        await LoadVersionAsync();
        await LoadSupportContactAsync();
        await RefreshBackupsAsync();
        await RefreshClientTelemetryAsync();
        await RefreshFirstLoginCredentialsAsync();
    }

    private async Task RefreshStatsAsync()
    {
        if (_api == null) return;
        var stats = await _api.GetStatsAsync();
        _lblTotalLicenses.Text = stats.TotalLicenses.ToString();
        _lblActiveLicenses.Text = stats.ActiveLicenses.ToString();
        _lblTotalDevices.Text = stats.TotalDevices.ToString();
        _lblActiveToday.Text = stats.ActiveToday.ToString();
        await RefreshDashboardInsightsAsync();
    }

    private async Task RefreshDashboardInsightsAsync()
    {
        if (_api == null) return;
        try
        {
            int rangeDays = GetDashboardRangeDays();
            DateTime now = DateTime.Now;
            DateTime rangeFrom = now.AddDays(-rangeDays);

            var licenses = await _api.GetLicensesAsync();
            int expiringSoon = licenses.Count(x =>
                !string.IsNullOrWhiteSpace(x.ExpiresAt) &&
                DateTime.TryParse(x.ExpiresAt, out DateTime exp) &&
                exp >= now.Date &&
                exp <= now.Date.AddDays(rangeDays));
            _lblExpiryWarning.Text = $"Muddat tugashiga yaqin license: {expiringSoon} ta";

            var devices = await _api.GetDevicesAsync();
            int inactiveDevices = devices.Count(x =>
                DateTime.TryParse(x.LastSeenAt, out DateTime lastSeen) &&
                lastSeen < rangeFrom);
            _lblInactiveWarning.Text = $"Uzoq vaqtdan beri offline qurilmalar: {inactiveDevices} ta";

            var audit = await _api.GetAuditAsync(300);
            var recent = audit
                .Where(x => DateTime.TryParse(x.CreatedAt, out DateTime at) && at >= rangeFrom)
                .OrderByDescending(x =>
                {
                    DateTime.TryParse(x.CreatedAt, out DateTime at);
                    return at;
                })
                .Take(12)
                .Select(x => new DashboardRecentItem
                {
                    Vaqt = x.CreatedAt,
                    Hodisa = x.EventType,
                    Izoh = x.Message
                })
                .ToList();
            _gridDashboardRecent.DataSource = recent;

            DateTime prevFrom = now.AddDays(-2 * rangeDays);
            int currentEvents = audit.Count(x => DateTime.TryParse(x.CreatedAt, out DateTime at) && at >= rangeFrom);
            int previousEvents = audit.Count(x => DateTime.TryParse(x.CreatedAt, out DateTime at) && at >= prevFrom && at < rangeFrom);
            string eventDelta = FormatTrendDelta(currentEvents, previousEvents);
            _lblEventTrend.Text = $"Faollik trendi: {currentEvents} ta hodisa ({eventDelta})";

            int currentOnline = devices.Count(x => DateTime.TryParse(x.LastSeenAt, out DateTime at) && at >= rangeFrom);
            int previousOnline = devices.Count(x => DateTime.TryParse(x.LastSeenAt, out DateTime at) && at >= prevFrom && at < rangeFrom);
            string onlineDelta = FormatTrendDelta(currentOnline, previousOnline);
            _lblOnlineTrend.Text = $"Online trendi: {currentOnline} ta qurilma ({onlineDelta})";

            var licenseCustomer = licenses.ToDictionary(x => x.LicenseKey, x => string.IsNullOrWhiteSpace(x.CustomerName) ? "-" : x.CustomerName, StringComparer.OrdinalIgnoreCase);
            var topCustomers = devices
                .GroupBy(x => x.LicenseKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TopCustomerItem
                {
                    Mijoz = licenseCustomer.TryGetValue(g.Key, out string? name) ? name : g.Key,
                    QurilmaSoni = g.Count()
                })
                .OrderByDescending(x => x.QurilmaSoni)
                .Take(5)
                .ToList();
            _gridTopCustomers.DataSource = topCustomers;

            var topLicenses = devices
                .GroupBy(x => x.LicenseKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    DateTime last = DateTime.MinValue;
                    foreach (var item in g)
                    {
                        if (DateTime.TryParse(item.LastSeenAt, out DateTime at) && at > last)
                        {
                            last = at;
                        }
                    }

                    return new TopLicenseItem
                    {
                        LicenseKey = g.Key,
                        QurilmaSoni = g.Count(),
                        OxirgiFaollik = last == DateTime.MinValue ? "-" : last.ToString("yyyy-MM-dd HH:mm")
                    };
                })
                .OrderByDescending(x => x.QurilmaSoni)
                .ThenByDescending(x => x.OxirgiFaollik)
                .Take(5)
                .ToList();
            _gridTopLicenses.DataSource = topLicenses;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Dashboard: {ex.Message}";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(184, 48, 48);
        }
    }

    private static string FormatTrendDelta(int currentValue, int previousValue)
    {
        if (previousValue <= 0)
        {
            return currentValue > 0 ? "+100%" : "0%";
        }

        double delta = ((double)(currentValue - previousValue) / previousValue) * 100.0;
        string sign = delta >= 0 ? "+" : string.Empty;
        return $"{sign}{delta:0.#}%";
    }

    private int GetDashboardRangeDays()
    {
        string? selected = _cmbDashboardRange.SelectedItem?.ToString();
        if (string.Equals(selected, "Bugun", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(selected, "30 kun", StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        return 7;
    }

    private async Task RefreshLicensesAsync()
    {
        if (_api == null) return;
        string? status = _cmbLicenseStatus.SelectedItem?.ToString();
        if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }

        var items = await _api.GetLicensesAsync(_txtLicenseSearch.Text.Trim(), status);
        _gridLicenses.DataSource = items;
        if (_gridLicenses.Rows.Count > 0)
        {
            _gridLicenses.ClearSelection();
            _gridLicenses.Rows[0].Selected = true;
            _gridLicenses.CurrentCell = _gridLicenses.Rows[0].Cells[0];
        }

        _selectedLicense = _gridLicenses.CurrentRow?.DataBoundItem as LicenseItem;
        BindSelectedLicenseToEditor();
    }

    private async Task RefreshDevicesAsync()
    {
        if (_api == null) return;
        var items = await _api.GetDevicesAsync(_txtDeviceSearch.Text.Trim());
        _gridDevices.DataSource = items;
        if (_gridDevices.Rows.Count > 0)
        {
            _gridDevices.ClearSelection();
            _gridDevices.Rows[0].Selected = true;
            _gridDevices.CurrentCell = _gridDevices.Rows[0].Cells[0];
        }

        _selectedDevice = _gridDevices.CurrentRow?.DataBoundItem as DeviceItem;
    }

    private async Task RefreshAuditAsync()
    {
        if (_api == null) return;
        var items = await _api.GetAuditAsync();
        _gridAudit.DataSource = items;
    }

    private async Task RefreshUsersAsync()
    {
        if (_api == null) return;
        try
        {
            var items = await _api.GetAdminUsersAsync();
            _gridUsers.DataSource = items;
            string currentUsername = _txtUser.Text.Trim();
            var me = items.FirstOrDefault(x => string.Equals(x.Username, currentUsername, StringComparison.OrdinalIgnoreCase));
            _currentRole = me?.Role ?? "Operator";
            ApplyRoleUiPermissions();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Users: {ex.Message}";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(184, 48, 48);
            _currentRole = "Operator";
            ApplyRoleUiPermissions();
        }
    }

    private async Task CreateAdminUserAsync()
    {
        if (_api == null) return;
        await _api.CreateAdminUserAsync(_txtNewUsername.Text.Trim(), _txtNewUserPassword.Text, _cmbNewUserRole.SelectedItem?.ToString() ?? "Operator");
        MessageBox.Show("Admin user yaratildi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _txtNewUsername.Text = string.Empty;
        _txtNewUserPassword.Text = string.Empty;
        await RefreshUsersAsync();
    }

    private async Task SetSelectedUserPasswordAsync()
    {
        if (_api == null) return;
        if (_gridUsers.CurrentRow?.DataBoundItem is not AdminUserItem user)
        {
            MessageBox.Show("Avval user tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? newPassword = PromptText("Yangi parolni kiriting:", "User parolini yangilash", true);
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return;
        }

        await _api.UpdateAdminUserPasswordAsync(user.Id, newPassword);
        MessageBox.Show("Parol yangilandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ResetMyPasswordAsync()
    {
        if (_api == null) return;
        string? oldPassword = PromptText("Joriy parolingiz:", "Parolni almashtirish", true);
        if (string.IsNullOrWhiteSpace(oldPassword)) return;
        string? newPassword = PromptText("Yangi parol:", "Parolni almashtirish", true);
        if (string.IsNullOrWhiteSpace(newPassword)) return;
        await _api.ResetMyPasswordAsync(oldPassword, newPassword);
        MessageBox.Show("Parolingiz yangilandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task SendClientPasswordResetAsync()
    {
        if (_api == null) return;
        string licenseKey = _txtResetLicenseKey.Text.Trim();
        string deviceId = _txtResetDeviceId.Text.Trim();
        string username = _txtResetUsername.Text.Trim();
        string tempPassword = _txtResetTempPassword.Text;
        if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(tempPassword))
        {
            MessageBox.Show("License key, username va temporary parolni kiriting.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int id = await _api.CreateClientPasswordResetAsync(licenseKey, string.IsNullOrWhiteSpace(deviceId) ? null : deviceId, username, tempPassword);
        MessageBox.Show($"Reset yuborildi. ID: {id}\nMijoz dasturini qayta ochganda parol avtomatik tiklanadi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task LoadVersionAsync()
    {
        if (_api == null) return;
        try
        {
            var v = await _api.GetLatestVersionAsync();
            _txtVersion.Text = v.Version;
            _txtVersionUrl.Text = v.Url;
            _txtVersionSha256.Text = v.Sha256;
            _txtVersionNote.Text = v.Note;
            _chkVersionMandatory.Checked = v.Mandatory;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Version: {ex.Message}";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(184, 48, 48);
        }
    }

    private async Task SaveVersionAsync()
    {
        if (_api == null) return;
        await _api.SetVersionAsync(
            _txtVersion.Text.Trim(),
            _txtVersionUrl.Text.Trim(),
            _txtVersionNote.Text.Trim(),
            _chkVersionMandatory.Checked,
            _txtVersionSha256.Text.Trim());
        MessageBox.Show("Version saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task LoadSupportContactAsync()
    {
        if (_api == null) return;
        try
        {
            _txtSupportContact.Text = await _api.GetSupportContactAsync();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Contact: {ex.Message}";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(184, 48, 48);
        }
    }

    private async Task SaveSupportContactAsync()
    {
        if (_api == null) return;
        await _api.SetSupportContactAsync(_txtSupportContact.Text.Trim());
        MessageBox.Show("Aloqa ma'lumoti saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RefreshBackupsAsync()
    {
        if (_api == null) return;
        try
        {
            var items = await _api.GetBackupsAsync();
            _gridBackups.DataSource = items;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Backups: {ex.Message}";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(184, 48, 48);
        }
    }

    private async Task RefreshClientTelemetryAsync()
    {
        if (_api == null) return;
        try
        {
            var items = await _api.GetClientTelemetryUsersAsync(_txtTelemetryLicense.Text.Trim(), _txtTelemetryDevice.Text.Trim());
            _gridClientTelemetry.DataSource = items;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Client telemetry: {ex.Message}";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(184, 48, 48);
        }
    }

    private async Task RefreshFirstLoginCredentialsAsync()
    {
        if (_api == null) return;
        try
        {
            var items = await _api.GetClientFirstLoginCredentialsAsync(_txtFirstLoginLicense.Text.Trim(), _txtFirstLoginDevice.Text.Trim());
            _gridFirstLogin.DataSource = items;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"First login: {ex.Message}";
            _lblStatus.ForeColor = System.Drawing.Color.FromArgb(184, 48, 48);
        }
    }

    private async Task CreateBackupAsync()
    {
        if (_api == null) return;
        string name = await _api.CreateBackupAsync();
        MessageBox.Show($"Backup yaratildi: {name}", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RefreshBackupsAsync();
    }

    private async Task DownloadSelectedBackupAsync()
    {
        if (_api == null) return;
        if (_gridBackups.CurrentRow?.DataBoundItem is not BackupItem item)
        {
            MessageBox.Show("Avval backup tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using SaveFileDialog dlg = new()
        {
            Filter = "Database files (*.db)|*.db|All files (*.*)|*.*",
            FileName = item.FileName
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        await _api.DownloadBackupAsync(item.FileName, dlg.FileName);
        MessageBox.Show("Backup yuklab olindi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RestoreSelectedBackupAsync()
    {
        if (_api == null) return;
        if (_gridBackups.CurrentRow?.DataBoundItem is not BackupItem item)
        {
            MessageBox.Show("Avval backup tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            $"Diqqat! Restore amali joriy bazani almashtiradi.\nTanlangan backup: {item.FileName}\nDavom etilsinmi?",
            "Tasdiqlash",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;
        await _api.RestoreBackupAsync(item.FileName);
        MessageBox.Show("Restore bajarildi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RefreshAllAsync();
    }

    private async Task ShowSelectedLicenseHistoryAsync()
    {
        if (_api == null || _selectedLicense == null)
        {
            MessageBox.Show("Avval bitta license tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var items = await _api.GetLicenseHistoryAsync(_selectedLicense.Id);
        using Form dlg = new()
        {
            Text = $"License history - {_selectedLicense.LicenseKey}",
            Width = 900,
            Height = 520,
            StartPosition = FormStartPosition.CenterParent
        };
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            DataSource = items
        };
        dlg.Controls.Add(grid);
        dlg.ShowDialog(this);
    }

    private async Task CreateLicenseAsync()
    {
        if (_api == null) return;
        string customer = _txtCustomer.Text.Trim();
        int max = Convert.ToInt32(_numMaxDevices.Value);
        string? expires = string.IsNullOrWhiteSpace(_txtExpires.Text) ? null : _txtExpires.Text.Trim();

        string key = await _api.CreateLicenseAsync(customer, max, expires);
        if (TrySetClipboardText(key))
        {
            MessageBox.Show($"License yaratildi va nusxalandi:\n{key}", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show($"License yaratildi:\n{key}", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        await RefreshLicensesAsync();
    }

    private async Task ToggleLicenseAsync()
    {
        if (_api == null || _selectedLicense == null)
        {
            MessageBox.Show("Avval bitta license tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await _api.ToggleLicenseAsync(_selectedLicense.Id);
        await RefreshLicensesAsync();
        await RefreshAuditAsync();
    }

    private async Task SaveSelectedLicenseAsync()
    {
        if (_api == null || _selectedLicense == null)
        {
            MessageBox.Show("Avval bitta license tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string key = _txtLicenseKeyEdit.Text.Trim();
        string customer = _txtCustomer.Text.Trim();
        int max = Convert.ToInt32(_numMaxDevices.Value);
        string? expires = string.IsNullOrWhiteSpace(_txtExpires.Text) ? null : _txtExpires.Text.Trim();

        await _api.UpdateLicenseAsync(_selectedLicense.Id, key, customer, max, _selectedLicense.Status, expires);
        MessageBox.Show("License saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RefreshLicensesAsync();
        await RefreshAuditAsync();
    }

    private async Task DeleteSelectedLicenseAsync()
    {
        if (_api == null || _selectedLicense == null)
        {
            MessageBox.Show("Avval bitta license tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            $"Rostdan ham ushbu keyni o'chirmoqchimisiz?\n{_selectedLicense.LicenseKey}",
            "Tasdiqlash",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await _api.DeleteLicenseAsync(_selectedLicense.Id);
        MessageBox.Show("License o'chirildi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RefreshLicensesAsync();
        await RefreshAuditAsync();
    }

    private async Task UnlinkSelectedDeviceAsync()
    {
        if (_api == null || _selectedDevice == null)
        {
            MessageBox.Show("Avval bitta qurilma tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            $"Qurilmani unlink qilmoqchimisiz?\n{_selectedDevice.DeviceName} ({_selectedDevice.DeviceId})",
            "Tasdiqlash",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await _api.UnlinkDeviceAsync(_selectedDevice.Id);
        MessageBox.Show("Qurilma unlink qilindi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RefreshDevicesAsync();
        await RefreshAuditAsync();
    }

    private async Task ExportLicensesAsync()
    {
        if (_api == null) return;
        using SaveFileDialog dlg = new()
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"licenses_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dlg.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        string? status = _cmbLicenseStatus.SelectedItem?.ToString();
        if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }

        await _api.ExportLicensesCsvAsync(_txtLicenseSearch.Text.Trim(), status, dlg.FileName);
        MessageBox.Show("Licenses CSV saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ExportDevicesAsync()
    {
        if (_api == null) return;
        using SaveFileDialog dlg = new()
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"devices_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dlg.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        await _api.ExportDevicesCsvAsync(_txtDeviceSearch.Text.Trim(), dlg.FileName);
        MessageBox.Show("Devices CSV saqlandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CopySelectedLicenseKey()
    {
        string key = GetSelectedLicenseKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Nusxalash uchun avval bitta license tanlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (TrySetClipboardText(key))
        {
            MessageBox.Show("License key nusxalandi.", "Tayyor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Clipboard band. Birozdan keyin qayta urinib ko'ring.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string GetSelectedLicenseKey()
    {
        if (_selectedLicense != null && !string.IsNullOrWhiteSpace(_selectedLicense.LicenseKey))
        {
            return _selectedLicense.LicenseKey.Trim();
        }

        if (_gridLicenses.CurrentRow?.DataBoundItem is LicenseItem item && !string.IsNullOrWhiteSpace(item.LicenseKey))
        {
            return item.LicenseKey.Trim();
        }

        if (_gridLicenses.CurrentRow != null && _gridLicenses.Columns.Contains("LicenseKey"))
        {
            object? raw = _gridLicenses.CurrentRow.Cells["LicenseKey"].Value;
            return raw?.ToString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private void BindSelectedLicenseToEditor()
    {
        if (_selectedLicense == null)
        {
            return;
        }

        _txtCustomer.Text = _selectedLicense.CustomerName ?? string.Empty;
        _numMaxDevices.Value = Math.Max(_numMaxDevices.Minimum, Math.Min(_numMaxDevices.Maximum, _selectedLicense.MaxDevices <= 0 ? 1 : _selectedLicense.MaxDevices));
        _txtExpires.Text = _selectedLicense.ExpiresAt ?? string.Empty;
        _txtLicenseKeyEdit.Text = _selectedLicense.LicenseKey ?? string.Empty;
    }

    private static bool TrySetClipboardText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        for (int i = 0; i < 3; i++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch
            {
                System.Threading.Thread.Sleep(80);
            }
        }

        return false;
    }

    private static string? PromptText(string message, string title, bool password)
    {
        using Form prompt = new()
        {
            Width = 440,
            Height = 180,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false
        };
        Label textLabel = new() { Left = 16, Top = 16, Width = 390, Text = message };
        TextBox input = new() { Left = 16, Top = 42, Width = 390, UseSystemPasswordChar = password };
        Button ok = new() { Text = "OK", Left = 246, Width = 75, Top = 80, DialogResult = DialogResult.OK };
        Button cancel = new() { Text = "Bekor", Left = 331, Width = 75, Top = 80, DialogResult = DialogResult.Cancel };
        prompt.Controls.Add(textLabel);
        prompt.Controls.Add(input);
        prompt.Controls.Add(ok);
        prompt.Controls.Add(cancel);
        prompt.AcceptButton = ok;
        prompt.CancelButton = cancel;
        return prompt.ShowDialog() == DialogResult.OK ? input.Text : null;
    }

    private static Label NewLabel(string text, int left, int top)
    {
        return new Label
        {
            Text = text,
            Left = left,
            Top = top,
            AutoSize = true,
            ForeColor = System.Drawing.Color.FromArgb(56, 80, 116)
        };
    }

    private static Panel NewStatCard(string title, Label valueLabel, int left, int top)
    {
        Panel p = new()
        {
            Left = left,
            Top = top,
            Width = 250,
            Height = 110,
            BackColor = System.Drawing.Color.White
        };

        Label t = new()
        {
            Text = title,
            Left = 14,
            Top = 14,
            AutoSize = true,
            ForeColor = System.Drawing.Color.FromArgb(95, 116, 148)
        };
        valueLabel.Left = 14;
        valueLabel.Top = 44;
        valueLabel.AutoSize = true;
        valueLabel.Font = new System.Drawing.Font("Bahnschrift SemiBold", 22, System.Drawing.FontStyle.Bold);
        valueLabel.ForeColor = System.Drawing.Color.FromArgb(32, 61, 104);
        valueLabel.Text = "0";

        p.Controls.Add(t);
        p.Controls.Add(valueLabel);
        return p;
    }

    private void ApplyRoleUiPermissions()
    {
        bool isSuperAdmin = string.Equals(_currentRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        _btnCreateUser.Enabled = isSuperAdmin;
        _btnSetUserPassword.Enabled = isSuperAdmin;
        _btnSendClientReset.Enabled = isSuperAdmin;
        _btnSaveVersion.Enabled = isSuperAdmin;
        _btnSaveSupportContact.Enabled = isSuperAdmin;
        _btnCreateBackup.Enabled = isSuperAdmin;
        _btnRestoreBackup.Enabled = isSuperAdmin;

        if (isSuperAdmin)
        {
            if (!_tabs.TabPages.Contains(_tabClients))
            {
                _tabs.TabPages.Add(_tabClients);
            }

            if (!_tabs.TabPages.Contains(_tabUpdate))
            {
                _tabs.TabPages.Add(_tabUpdate);
            }
        }
        else
        {
            if (_tabs.SelectedTab == _tabClients || _tabs.SelectedTab == _tabUpdate)
            {
                _tabs.SelectedTab = _tabDashboard;
            }

            if (_tabs.TabPages.Contains(_tabClients))
            {
                _tabs.TabPages.Remove(_tabClients);
            }

            if (_tabs.TabPages.Contains(_tabUpdate))
            {
                _tabs.TabPages.Remove(_tabUpdate);
            }
        }
    }

    private static bool IsAllowedServerUrl(string urlText)
    {
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            string host = uri.Host ?? string.Empty;
            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("::1", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

internal sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public ApiClient(string baseUrl, string username, string password)
    {
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(20);
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    public async Task PingAsync()
    {
        using var res = await _http.GetAsync("api/ping");
        res.EnsureSuccessStatusCode();
    }

    public async Task<DashboardStats> GetStatsAsync()
    {
        using var res = await _http.GetAsync("api/admin/stats");
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<StatsResponse>(s, _json) ?? throw new Exception("Stats parse xato.");
        return wrapper.Stats;
    }

    public async Task<List<LicenseItem>> GetLicensesAsync(string? search = null, string? status = null)
    {
        string url = "api/admin/licenses";
        List<string> query = new();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add("q=" + Uri.EscapeDataString(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add("status=" + Uri.EscapeDataString(status));
        }

        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        using var res = await _http.GetAsync(url);
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<LicensesResponse>(s, _json) ?? throw new Exception("Licenses parse xato.");
        return wrapper.Items ?? new List<LicenseItem>();
    }

    public async Task<string> CreateLicenseAsync(string customerName, int maxDevices, string? expiresAt)
    {
        var req = new { customerName, maxDevices, expiresAt };
        using var res = await _http.PostAsync("api/admin/licenses/create", JsonContent.Create(req));
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<CreateLicenseResponse>(s, _json) ?? throw new Exception("Create response parse xato.");
        return wrapper.LicenseKey ?? "-";
    }

    public async Task ToggleLicenseAsync(int id)
    {
        using var res = await _http.PostAsync($"api/admin/licenses/{id}/toggle", null);
        await EnsureSuccessOrThrow(res);
    }

    public async Task UpdateLicenseAsync(int id, string licenseKey, string customerName, int maxDevices, string status, string? expiresAt)
    {
        var req = new { licenseKey, customerName, maxDevices, status, expiresAt };
        using var res = await _http.PutAsync($"api/admin/licenses/{id}", JsonContent.Create(req));
        await EnsureSuccessOrThrow(res);
    }

    public async Task DeleteLicenseAsync(int id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/admin/licenses/{id}");
        using var res = await _http.SendAsync(req);
        await EnsureSuccessOrThrow(res);
    }

    public async Task<List<DeviceItem>> GetDevicesAsync(string? search = null)
    {
        string url = "api/admin/devices";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += "?q=" + Uri.EscapeDataString(search);
        }

        using var res = await _http.GetAsync(url);
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<DevicesResponse>(s, _json) ?? throw new Exception("Devices parse xato.");
        return wrapper.Items ?? new List<DeviceItem>();
    }

    public async Task UnlinkDeviceAsync(int id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/admin/devices/{id}");
        using var res = await _http.SendAsync(req);
        await EnsureSuccessOrThrow(res);
    }

    public async Task<List<AuditItem>> GetAuditAsync(int limit = 200)
    {
        using var res = await _http.GetAsync($"api/admin/audit?limit={limit}");
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<AuditResponse>(s, _json) ?? throw new Exception("Audit parse xato.");
        return wrapper.Items ?? new List<AuditItem>();
    }

    public async Task ExportLicensesCsvAsync(string? search, string? status, string filePath)
    {
        string url = "api/admin/licenses/export.csv";
        List<string> query = new();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add("q=" + Uri.EscapeDataString(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add("status=" + Uri.EscapeDataString(status));
        }

        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        using var res = await _http.GetAsync(url);
        await EnsureSuccessOrThrow(res);
        string text = await res.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(filePath, text, Encoding.UTF8);
    }

    public async Task ExportDevicesCsvAsync(string? search, string filePath)
    {
        string url = "api/admin/devices/export.csv";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += "?q=" + Uri.EscapeDataString(search);
        }

        using var res = await _http.GetAsync(url);
        await EnsureSuccessOrThrow(res);
        string text = await res.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(filePath, text, Encoding.UTF8);
    }

    public async Task<List<LicenseHistoryItem>> GetLicenseHistoryAsync(int licenseId)
    {
        using var res = await _http.GetAsync($"api/admin/licenses/{licenseId}/history");
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<LicenseHistoryResponse>(s, _json) ?? throw new Exception("History parse xato.");
        return wrapper.Items ?? new List<LicenseHistoryItem>();
    }

    public async Task<List<AdminUserItem>> GetAdminUsersAsync()
    {
        using var res = await _http.GetAsync("api/admin/users");
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<AdminUsersResponse>(s, _json) ?? throw new Exception("Users parse xato.");
        return wrapper.Items ?? new List<AdminUserItem>();
    }

    public async Task CreateAdminUserAsync(string username, string password, string role)
    {
        using var res = await _http.PostAsync("api/admin/users/create", JsonContent.Create(new { username, password, role }));
        await EnsureSuccessOrThrow(res);
    }

    public async Task UpdateAdminUserPasswordAsync(int id, string newPassword)
    {
        using var res = await _http.PutAsync($"api/admin/users/{id}/password", JsonContent.Create(new { newPassword }));
        await EnsureSuccessOrThrow(res);
    }

    public async Task ResetMyPasswordAsync(string oldPassword, string newPassword)
    {
        using var res = await _http.PostAsync("api/admin/reset-password", JsonContent.Create(new { oldPassword, newPassword }));
        await EnsureSuccessOrThrow(res);
    }

    public async Task<VersionItem> GetLatestVersionAsync()
    {
        using var res = await _http.GetAsync("api/version/latest");
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var item = await JsonSerializer.DeserializeAsync<VersionItem>(s, _json) ?? throw new Exception("Version parse xato.");
        return item;
    }

    public async Task SetVersionAsync(string version, string url, string note, bool mandatory, string sha256)
    {
        using var res = await _http.PutAsync("api/admin/version", JsonContent.Create(new { version, url, note, mandatory, sha256 }));
        await EnsureSuccessOrThrow(res);
    }

    public async Task<string> GetSupportContactAsync()
    {
        using var res = await _http.GetAsync("api/admin/contact/info");
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<SupportContactResponse>(s, _json) ?? throw new Exception("Contact parse xato.");
        return wrapper.Contact ?? string.Empty;
    }

    public async Task SetSupportContactAsync(string contact)
    {
        using var res = await _http.PutAsync("api/admin/contact/info", JsonContent.Create(new { contact }));
        await EnsureSuccessOrThrow(res);
    }

    public async Task<List<BackupItem>> GetBackupsAsync()
    {
        using var res = await _http.GetAsync("api/admin/backups");
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<BackupsResponse>(s, _json) ?? throw new Exception("Backups parse xato.");
        return wrapper.Items ?? new List<BackupItem>();
    }

    public async Task<string> CreateBackupAsync()
    {
        using var res = await _http.PostAsync("api/admin/backups/create", null);
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<CreateBackupResponse>(s, _json) ?? throw new Exception("Backup create parse xato.");
        return wrapper.FileName ?? "-";
    }

    public async Task DownloadBackupAsync(string fileName, string savePath)
    {
        using var res = await _http.GetAsync($"api/admin/backups/{Uri.EscapeDataString(fileName)}");
        await EnsureSuccessOrThrow(res);
        byte[] bytes = await res.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(savePath, bytes);
    }

    public async Task RestoreBackupAsync(string fileName)
    {
        using var res = await _http.PostAsync("api/admin/backups/restore", JsonContent.Create(new { fileName }));
        await EnsureSuccessOrThrow(res);
    }

    public async Task<int> CreateClientPasswordResetAsync(string licenseKey, string? deviceId, string username, string tempPassword)
    {
        var payload = new { licenseKey, deviceId, username, tempPassword };
        using var res = await _http.PostAsync("api/admin/client/password-resets/create", JsonContent.Create(payload));
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<CreateClientResetResponse>(s, _json) ?? throw new Exception("Reset response parse xato.");
        return wrapper.Id;
    }

    public async Task<List<ClientTelemetryItem>> GetClientTelemetryUsersAsync(string? licenseKey, string? deviceId, int limit = 200)
    {
        List<string> q = new();
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            q.Add("licenseKey=" + Uri.EscapeDataString(licenseKey));
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            q.Add("deviceId=" + Uri.EscapeDataString(deviceId));
        }

        q.Add("limit=" + limit.ToString());
        string url = "api/admin/client/telemetry/users?" + string.Join("&", q);

        using var res = await _http.GetAsync(url);
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<ClientTelemetryResponse>(s, _json) ?? throw new Exception("Client telemetry parse xato.");
        return wrapper.Items ?? new List<ClientTelemetryItem>();
    }

    public async Task<List<ClientFirstLoginItem>> GetClientFirstLoginCredentialsAsync(string? licenseKey, string? deviceId, int limit = 200)
    {
        List<string> q = new();
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            q.Add("licenseKey=" + Uri.EscapeDataString(licenseKey));
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            q.Add("deviceId=" + Uri.EscapeDataString(deviceId));
        }

        q.Add("limit=" + limit.ToString());
        string url = "api/admin/client/first-login-credentials?" + string.Join("&", q);

        using var res = await _http.GetAsync(url);
        await EnsureSuccessOrThrow(res);
        using var s = await res.Content.ReadAsStreamAsync();
        var wrapper = await JsonSerializer.DeserializeAsync<ClientFirstLoginResponse>(s, _json) ?? throw new Exception("First-login parse xato.");
        return wrapper.Items ?? new List<ClientFirstLoginItem>();
    }

    private static async Task EnsureSuccessOrThrow(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string text = await response.Content.ReadAsStringAsync();
        throw new Exception($"HTTP {(int)response.StatusCode}: {text}");
    }
}

internal sealed class StatsResponse
{
    [JsonPropertyName("stats")]
    public DashboardStats Stats { get; set; } = new();
}

internal sealed class LicensesResponse
{
    [JsonPropertyName("items")]
    public List<LicenseItem>? Items { get; set; }
}

internal sealed class DevicesResponse
{
    [JsonPropertyName("items")]
    public List<DeviceItem>? Items { get; set; }
}

internal sealed class AuditResponse
{
    [JsonPropertyName("items")]
    public List<AuditItem>? Items { get; set; }
}

internal sealed class CreateLicenseResponse
{
    [JsonPropertyName("licenseKey")]
    public string? LicenseKey { get; set; }
}

internal sealed class LicenseHistoryResponse
{
    [JsonPropertyName("items")]
    public List<LicenseHistoryItem>? Items { get; set; }
}

internal sealed class AdminUsersResponse
{
    [JsonPropertyName("items")]
    public List<AdminUserItem>? Items { get; set; }
}

internal sealed class BackupsResponse
{
    [JsonPropertyName("items")]
    public List<BackupItem>? Items { get; set; }
}

internal sealed class CreateBackupResponse
{
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }
}

internal sealed class CreateClientResetResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

internal sealed class ClientTelemetryResponse
{
    [JsonPropertyName("items")]
    public List<ClientTelemetryItem>? Items { get; set; }
}

internal sealed class ClientFirstLoginResponse
{
    [JsonPropertyName("items")]
    public List<ClientFirstLoginItem>? Items { get; set; }
}

internal sealed class SupportContactResponse
{
    [JsonPropertyName("contact")]
    public string? Contact { get; set; }
}

public sealed class DashboardStats
{
    [JsonPropertyName("totalLicenses")]
    public int TotalLicenses { get; set; }

    [JsonPropertyName("activeLicenses")]
    public int ActiveLicenses { get; set; }

    [JsonPropertyName("totalDevices")]
    public int TotalDevices { get; set; }

    [JsonPropertyName("activeToday")]
    public int ActiveToday { get; set; }
}

public sealed class LicenseItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("licenseKey")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("maxDevices")]
    public int MaxDevices { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public string? ExpiresAt { get; set; }
}

public sealed class DeviceItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("licenseKey")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = string.Empty;

    [JsonPropertyName("firstSeenAt")]
    public string FirstSeenAt { get; set; } = string.Empty;

    [JsonPropertyName("lastSeenAt")]
    public string LastSeenAt { get; set; } = string.Empty;
}

public sealed class AuditItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class LicenseHistoryItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("licenseId")]
    public int? LicenseId { get; set; }

    [JsonPropertyName("licenseKey")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class AdminUserItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class BackupItem
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public string CreatedAtUtc { get; set; } = string.Empty;
}

public sealed class VersionItem
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }
}

public sealed class ClientTelemetryItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("licenseKey")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = string.Empty;

    [JsonPropertyName("userCount")]
    public int UserCount { get; set; }

    [JsonPropertyName("usernamesJson")]
    public string UsernamesJson { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class ClientFirstLoginItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("licenseKey")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("tempPassword")]
    public string TempPassword { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("appliedAt")]
    public string? AppliedAt { get; set; }
}

public sealed class DashboardRecentItem
{
    public string Vaqt { get; set; } = string.Empty;
    public string Hodisa { get; set; } = string.Empty;
    public string Izoh { get; set; } = string.Empty;
}

public sealed class TopCustomerItem
{
    public string Mijoz { get; set; } = string.Empty;
    public int QurilmaSoni { get; set; }
}

public sealed class TopLicenseItem
{
    public string LicenseKey { get; set; } = string.Empty;
    public int QurilmaSoni { get; set; }
    public string OxirgiFaollik { get; set; } = string.Empty;
}

internal sealed class ConnectionConfig
{
    [JsonPropertyName("serverUrl")]
    public string ServerUrl { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}
