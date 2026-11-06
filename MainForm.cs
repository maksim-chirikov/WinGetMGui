// WinGetMGui - minimal GUI wrapper for winget
// Copyright (C) 2026 Maxim Chirikov
// SPDX-License-Identifier: GPL-3.0-or-later

namespace WinGetMGui
{
    public partial class MainForm : Form
    {
        private CustomTextBox txtSearch = default!;
        private DataGridView dataGridViewPackages = default!;
        private ToolStripProgressBar progressBar = default!;
        private WingetService wingetService = default!;
        private bool _busy;
        private ToolStrip toolStrip = default!;
        private ToolStripStatusLabel statusLabel = default!;
        private StatusStrip statusStrip = default!;
        private TabControl tabs = default!;
        private TabPage _updateTab = default!;
        private TabPage _installTab = default!;
        private TabPage _uninstallTab = default!;
        private TabPage _ignoredTab = default!;
        private TabPage _settingsTab = default!;
        private ToolStripButton btnSearch = default!;
        private ToolStripButton btnAction = default!;
        private ToolStripButton btnRefresh = default!;
        private ToolStripDropDownButton btnScope = default!;
        private ToolStripMenuItem scopeDefaultItem = default!;
        private ToolStripMenuItem scopeUserItem = default!;
        private ToolStripMenuItem scopeMachineItem = default!;
        private List<PackageInfo> _installedPackages = new();
        private List<PackageInfo> _catalogPackages = new();
        private IgnoredPackagesStore _ignored = default!;
        private StoreSettings _settings = default!;
        private ContextMenuStrip _contextMenu = default!;
        private Panel _settingsHost = default!;
        private string? _wingetVersion;
        private CheckBox chkSilent = default!;
        private CheckBox chkAcceptSourceAgreements = default!;
        private CheckBox chkAcceptPackageAgreements = default!;
        private CheckBox chkForce = default!;

        public MainForm()
        {
            InitializeComponent();
            LoadAppIcon();
            _ignored = new IgnoredPackagesStore(IgnoredPackagesStore.DefaultPath);
            LoadStore(IgnoredPackagesStore.DefaultPath, () => _ignored.Load());
            _settings = new StoreSettings(StoreSettings.DefaultPath);
            LoadStore(StoreSettings.DefaultPath, () => _settings.Load());
            SuspendLayout();
            SetupUI();
            ResumeLayout(true);
            wingetService = new WingetService(_settings.WingetOptions);
        }

        private void LoadAppIcon()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream("WinGetMGui.app.ico");
            if (stream != null)
            {
                Icon = new Icon(stream);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F) && IsGridTab && txtSearch.Enabled && txtSearch.TextBox is TextBox box)
            {
                box.Focus();
                box.SelectAll();
                return true;
            }

            if (keyData == Keys.F5 && !_busy && IsGridTab)
            {
                BtnRefresh_Click(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SetupUI()
        {
            // Create TabControl (Top) - tab acts as the action mode
            tabs = new TabControl
            {
                Dock = DockStyle.Top,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(120, 28),
                Height = 33
            };
            _updateTab = new TabPage("Update");
            _installTab = new TabPage("Install");
            _uninstallTab = new TabPage("Uninstall");
            _ignoredTab = new TabPage("Ignored");
            _settingsTab = new TabPage("Settings");
            tabs.TabPages.AddRange(new[] { _updateTab, _installTab, _uninstallTab, _ignoredTab, _settingsTab });
            tabs.SelectedIndexChanged += Tabs_SelectedIndexChanged;

            // Create StatusStrip (Bottom)
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Packages: 0");
            progressBar = new ToolStripProgressBar();
            progressBar.Visible = false;
            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(progressBar);
            statusStrip.Dock = DockStyle.Bottom;

            // Create ToolStrip (below tabs)
            toolStrip = new ToolStrip();
            toolStrip.Dock = DockStyle.Top;

            // Search Box in ToolStrip
            txtSearch = new CustomTextBox();
            txtSearch.Size = new Size(250, 27);
            txtSearch.Cleared += TxtSearch_Cleared;
            txtSearch.Submitted += BtnSearch_Click;
            txtSearch.FilterTextChanged += TxtSearch_TextChanged;
            toolStrip.Items.Add(txtSearch);

            // Search Button
            btnSearch = new ToolStripButton("Search");
            btnSearch.Click += BtnSearch_Click;
            toolStrip.Items.Add(btnSearch);

            // Install scope (Install tab only): check marks show the active selection
            btnScope = new ToolStripDropDownButton("Scope");
            scopeDefaultItem = new ToolStripMenuItem("Default", null, ScopeItem_Click) { Checked = true };
            scopeUserItem = new ToolStripMenuItem("User", null, ScopeItem_Click);
            scopeMachineItem = new ToolStripMenuItem("Machine", null, ScopeItem_Click);
            btnScope.DropDownItems.Add(scopeDefaultItem);
            btnScope.DropDownItems.Add(scopeUserItem);
            btnScope.DropDownItems.Add(scopeMachineItem);
            btnScope.Visible = false;
            toolStrip.Items.Add(btnScope);

            // Action Button (verb follows the selected tab)
            btnAction = new ToolStripButton();
            btnAction.Click += BtnAction_Click;
            toolStrip.Items.Add(btnAction);

            btnRefresh = new ToolStripButton("Refresh");
            btnRefresh.Click += BtnRefresh_Click;
            toolStrip.Items.Add(btnRefresh);

            // Create DataGridView (Center)
            dataGridViewPackages = new CustomDataGridView();
            dataGridViewPackages.Dock = DockStyle.Fill;
            dataGridViewPackages.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewPackages.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewPackages.MultiSelect = false;
            dataGridViewPackages.ReadOnly = true;
            dataGridViewPackages.AllowUserToAddRows = false;
            dataGridViewPackages.AllowUserToDeleteRows = false;
            dataGridViewPackages.AutoGenerateColumns = false;
            dataGridViewPackages.RowHeadersVisible = false;
            dataGridViewPackages.AllowUserToResizeRows = false;
            dataGridViewPackages.CellDoubleClick += DataGridViewPackages_CellDoubleClick;
            dataGridViewPackages.SelectionChanged += DataGridViewPackages_SelectionChanged;
            dataGridViewPackages.MouseUp += DataGridViewPackages_MouseUp;
            dataGridViewPackages.KeyDown += DataGridViewPackages_KeyDown;

            _contextMenu = new ContextMenuStrip();
            _contextMenu.Opening += ContextMenu_Opening;
            dataGridViewPackages.ContextMenuStrip = _contextMenu;

            // Settings panel (Fill GroupBox "Winget Options", replaces the grid on the Settings tab): writes only in-memory winget options
            _settingsHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Visible = false,
            };
            var optionsGroup = new GroupBox
            {
                Text = "Winget Options",
                Dock = DockStyle.Fill,
            };
            var optionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
            };
            chkSilent = new CheckBox { Text = "Silent", AutoSize = true, Checked = _settings.WingetOptions.Silent };
            chkAcceptSourceAgreements = new CheckBox { Text = "Accept source agreements", AutoSize = true, Checked = _settings.WingetOptions.AcceptSourceAgreements };
            chkAcceptPackageAgreements = new CheckBox { Text = "Accept package agreements", AutoSize = true, Checked = _settings.WingetOptions.AcceptPackageAgreements };
            chkForce = new CheckBox { Text = "Force", AutoSize = true, Checked = _settings.WingetOptions.Force };
            var optionChecks = new (CheckBox, Action<bool>)[]
            {
                (chkSilent, v => _settings.WingetOptions.Silent = v),
                (chkAcceptSourceAgreements, v => _settings.WingetOptions.AcceptSourceAgreements = v),
                (chkAcceptPackageAgreements, v => _settings.WingetOptions.AcceptPackageAgreements = v),
                (chkForce, v => _settings.WingetOptions.Force = v),
            };
            foreach (var (check, _) in optionChecks)
                optionsPanel.Controls.Add(check);
            optionsGroup.Controls.Add(optionsPanel);
            _settingsHost.Controls.Add(optionsGroup);

            // Add controls to form (Order matters for Docking)
            this.Controls.Add(dataGridViewPackages); // Fill
            this.Controls.Add(_settingsHost);         // Fill
            this.Controls.Add(statusStrip);         // Bottom
            this.Controls.Add(toolStrip);           // Top
            this.Controls.Add(tabs);                // Top (outermost)

            // Set up data grid columns
            SetupDataGridView();
            foreach (var (check, apply) in optionChecks)
                check.CheckedChanged += (s, e) =>
                {
                    _settings.Changed = true;
                    apply(check.Checked);
                };
            SyncActions();
        }

        private void SetupDataGridView()
        {
            dataGridViewPackages.Columns.Clear();

            int scopeIdx = dataGridViewPackages.Columns.Add("Scope", "Scope");
            dataGridViewPackages.Columns[scopeIdx].DataPropertyName = "Scope";
            dataGridViewPackages.Columns[scopeIdx].FillWeight = 50;

            int nameIdx = dataGridViewPackages.Columns.Add("Name", "Name");
            dataGridViewPackages.Columns[nameIdx].DataPropertyName = "Name";
            dataGridViewPackages.Columns[nameIdx].FillWeight = 200;

            int versionIdx = dataGridViewPackages.Columns.Add("Version", "Version");
            dataGridViewPackages.Columns[versionIdx].DataPropertyName = "Version";

            int availVersionIdx = dataGridViewPackages.Columns.Add("Available Version", "Available Version");
            dataGridViewPackages.Columns[availVersionIdx].DataPropertyName = "AvailableVersion";

            int idIdx = dataGridViewPackages.Columns.Add("ID", "ID");
            dataGridViewPackages.Columns[idIdx].DataPropertyName = "Id";

            int sourceIdx = dataGridViewPackages.Columns.Add("Source", "Source");
            dataGridViewPackages.Columns[sourceIdx].DataPropertyName = "Source";
        }

        private PackageVerb? ModeVerb =>
            IsUpdateTab ? PackageVerb.Update :
            IsInstallTab ? PackageVerb.Install :
            IsUninstallTab ? PackageVerb.Uninstall :
            null;

        private PackageInfo? GetSelectedPackage()
        {
            return dataGridViewPackages.SelectedRows.Count > 0
                ? dataGridViewPackages.SelectedRows[0].DataBoundItem as PackageInfo
                : null;
        }

        private bool IsUpdateTab => tabs.SelectedTab == _updateTab;
        private bool IsInstallTab => tabs.SelectedTab == _installTab;
        private bool IsUninstallTab => tabs.SelectedTab == _uninstallTab;
        private bool IsIgnoredTab => tabs.SelectedTab == _ignoredTab;
        private bool IsSettingsTab => tabs.SelectedTab == _settingsTab;
        private bool IsGridTab => IsUpdateTab || IsInstallTab || IsUninstallTab || IsIgnoredTab;

        private void SyncActions()
        {
            var package = GetSelectedPackage();
            bool idle = !_busy;
            bool installMode = IsInstallTab;

            btnAction.Text = IsIgnoredTab ? "Unignore" : ModeVerb is null ? "No action" : $"{ModeVerb}";
            btnAction.Enabled = (idle && IsIgnoredTab)
                ? dataGridViewPackages.SelectedRows.Count > 0
                : idle && ModeVerb is not null && package is not null;

            txtSearch.Enabled = idle;
            btnSearch.Enabled = idle && installMode;
            btnScope.Visible = installMode;
            btnRefresh.Enabled = idle;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            progressBar.Visible = busy;
            progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            SyncActions();
        }

        private async Task RunAsync(string progressStatus, Func<Task> action)
        {
            if (_busy)
                return;

            SetBusy(true);
            try
            {
                statusLabel.Text = progressStatus;
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                SetBusy(false);
                if (IsSettingsTab)
                    await ShowWingetVersionAsync();
                else
                    ShowPackageCount();
            }
        }

        private async Task LoadPackages()
        {
            var packages = await wingetService.GetInstalledPackagesAsync();
            _installedPackages = packages;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (IsSettingsTab)
                return;

            foreach (var package in _installedPackages)
                package.Ignored = _ignored.IsIgnored(package);

            string filter = txtSearch.Text;
            System.Collections.IEnumerable list = ModeVerb switch
            {
                PackageVerb.Install => _catalogPackages.OrderBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
                PackageVerb.Update => _installedPackages.Where(p => p.HasUpdate && !p.Ignored && RowFilter.Matches(filter, p.Name, p.Id, p.Source, p.Version, p.AvailableVersion)).OrderBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
                PackageVerb.Uninstall => _installedPackages.Where(p => RowFilter.Matches(filter, p.Name, p.Id, p.Source, p.Version, p.AvailableVersion)).OrderBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
                _ => _ignored.Entries.Where(e => RowFilter.Matches(filter, e.Name, e.Id, e.Source, e.AvailableVersion)).OrderBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
            };

            dataGridViewPackages.DataSource = list;
            dataGridViewPackages.ClearSelection();
            dataGridViewPackages.Columns["Scope"].Visible = IsIgnoredTab;
            dataGridViewPackages.Columns["Version"].Visible = IsUpdateTab || IsInstallTab || IsUninstallTab;
            SetupContextMenu();
            ShowPackageCount();
            SyncActions();
        }

        private void SetupContextMenu()
        {
            _contextMenu.Items.Clear();

            if (IsIgnoredTab)
            {
                _contextMenu.Items.Add("Unignore", null, (s, e) => UnignoreSelected());
                return;
            }

            if (ModeVerb is not PackageVerb verb)
                return;

            _contextMenu.Items.Add(verb.ToString(), null, async (s, e) => await TryStart(verb));

            if (verb == PackageVerb.Update)
            {
                _contextMenu.Items.Add(new ToolStripSeparator());
                _contextMenu.Items.Add("Ignore this version", null, (s, e) => IgnoreSelected(IgnoreScope.Version));
                _contextMenu.Items.Add("Ignore this package", null, (s, e) => IgnoreSelected(IgnoreScope.Package));
            }
        }

        private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
                return;
            }

            // The menu is already open (second right-click while open): MouseUp does not fire on the grid again
            // (ToolStripManager owns the mouse), so Opening must reject a non-row source point itself.
            if (sender is ContextMenuStrip menu && menu.SourceControl is DataGridView grid)
            {
                var p = grid.PointToClient(Cursor.Position);
                var hit = grid.HitTest(p.X, p.Y);
                if (hit.RowIndex < 0 || grid.Rows[hit.RowIndex].DataBoundItem is null)
                {
                    e.Cancel = true;
                    return;
                }
            }

            e.Cancel = IsIgnoredTab
                ? dataGridViewPackages.SelectedRows.Count == 0 ||
                    dataGridViewPackages.SelectedRows[0].DataBoundItem is not IgnoredPackage
                : GetSelectedPackage() is null;
        }

        private void ShowPackageCount()
        {
            if (_busy)
                return;

            statusLabel.Text = $"Packages: {dataGridViewPackages.Rows.Count}";
        }

        private async Task ShowWingetVersionAsync()
        {
            if (_busy)
                return;

            if (_wingetVersion is null)
            {
                statusLabel.Text = string.Empty;
                try
                {
                    _wingetVersion = await wingetService.GetVersionAsync();
                }
                catch (Exception)
                {
                }
            }

            if (!_busy && IsSettingsTab)
                statusLabel.Text = _wingetVersion is string version ? $"Winget: {version}" : "Winget version unavailable";
        }

        private async void Tabs_SelectedIndexChanged(object? sender, EventArgs e)
        {
            dataGridViewPackages.Visible = IsGridTab;
            _settingsHost.Visible = IsSettingsTab;
            toolStrip.Visible = IsGridTab;

            if (IsSettingsTab)
            {
                await ShowWingetVersionAsync();
                return;
            }

            ApplyFilter();
        }

        private async void BtnSearch_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
                return;

            await RunAsync("Searching packages...", SearchPackages);
        }

        private async Task SearchPackages()
        {
            var packages = await wingetService.SearchPackagesAsync(txtSearch.Text);
            _catalogPackages = packages;
            ApplyFilter();
        }

        private async void BtnAction_Click(object? sender, EventArgs e)
        {
            if (IsIgnoredTab)
            {
                UnignoreSelected();
                return;
            }

            if (ModeVerb is not PackageVerb verb)
                return;

            await TryStart(verb);
        }

        private PackageScope SelectedScope =>
            scopeUserItem.Checked ? PackageScope.User :
            scopeMachineItem.Checked ? PackageScope.Machine :
            PackageScope.Default;

        private void ScopeItem_Click(object? sender, EventArgs e)
        {
            scopeDefaultItem.Checked = sender == scopeDefaultItem;
            scopeUserItem.Checked = sender == scopeUserItem;
            scopeMachineItem.Checked = sender == scopeMachineItem;
        }

        private async Task TryStart(PackageVerb verb)
        {
            if (_busy)
                return;

            var package = GetSelectedPackage();
            if (package is null)
                return;

            await RunPackageAction(verb, package);
        }

        private async Task RunPackageAction(PackageVerb verb, PackageInfo package)
        {
            await RunAsync($"{verb.ProgressStatus()} {package.Name}...", async () =>
            {
                await wingetService.RunPackageVerbAsync(verb, package.Id, SelectedScope);
                await LoadPackages();
            });
        }

        private void DataGridViewPackages_SelectionChanged(object? sender, EventArgs e)
        {
            SyncActions();
        }

        private void DataGridViewPackages_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            var menu = dataGridViewPackages.ContextMenuStrip;
            if (menu is null)
                return;

            var hit = dataGridViewPackages.HitTest(e.X, e.Y);
            dataGridViewPackages.ClearSelection();
            if (hit.RowIndex < 0)
                menu.Close();
            else
                dataGridViewPackages.Rows[hit.RowIndex].Selected = true;
        }

        private void DataGridViewPackages_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && IsIgnoredTab)
            {
                e.SuppressKeyPress = true;
                UnignoreSelected();
            }
        }

        private void ApplyStoreAction(Action storeAction)
        {
            storeAction();
            ApplyFilter();
        }

        private void IgnoreSelected(IgnoreScope scope)
        {
            if (GetSelectedPackage() is not PackageInfo package)
                return;

            ApplyStoreAction(() => _ignored.Add(package, scope));
        }

        private void UnignoreSelected()
        {
            if (dataGridViewPackages.SelectedRows.Count == 0 ||
                dataGridViewPackages.SelectedRows[0].DataBoundItem is not IgnoredPackage entry)
                return;

            ApplyStoreAction(() => _ignored.Remove(entry));
        }

        private async void DataGridViewPackages_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (IsIgnoredTab)
            {
                UnignoreSelected();
                return;
            }

            var package = GetSelectedPackage();
            if (package is null || ModeVerb is not PackageVerb verb)
                return;

            await TryStart(verb);
        }

        private void TxtSearch_TextChanged(object? sender, EventArgs e)
        {
            if (IsInstallTab)
                return;

            ApplyFilter();
        }

        private void TxtSearch_Cleared(object? sender, EventArgs e)
        {
            if (IsInstallTab)
            {
                _catalogPackages = new();
                ApplyFilter();
            }
        }

        private async void BtnRefresh_Click(object? sender, EventArgs e)
        {
            bool installMode = IsInstallTab;

            await RunAsync("Updating sources...", async () =>
            {
                await wingetService.UpdateSourcesAsync();
                if (installMode)
                {
                    if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                    {
                        statusLabel.Text = "Searching packages...";
                        await SearchPackages();
                    }
                    return;
                }

                statusLabel.Text = "Loading packages...";
                await LoadPackages();
            });
        }

        private async void MainForm_Shown_1(object? sender, EventArgs e)
        {
            await RunAsync("Loading packages...", LoadPackages);
        }

        private static void LoadStore(string filePath, Action load)
        {
            try
            {
                load();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{filePath}\n{ex.Message}");
            }
        }

        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            try
            {
                if (_ignored.Changed)
                    _ignored.Save();
                if (_settings.Changed)
                    _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
