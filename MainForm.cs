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
        private (Keys Key, TabPage Tab)[] _tabShortcuts = default!;
        private ToolTip _tabToolTips = default!;
        private TabPage? _hoveredTab;
        private ToolStripButton btnSearch = default!;
        private ToolStripButton btnShowInfo = default!;
        private ToolStripButton btnAction = default!;
        private ToolStripButton btnRefresh = default!;
        private ToolStripDropDownButton btnScope = default!;
        private ToolStripDropDownButton btnSearchBy = default!;
        private ToolStripButton btnIgnoreVersion = default!;
        private ToolStripButton btnIgnorePackage = default!;
        private ToolStripMenuItem scopeDefaultItem = default!;
        private ToolStripMenuItem scopeUserItem = default!;
        private ToolStripMenuItem scopeMachineItem = default!;
        private ToolStripMenuItem searchByDefaultItem = default!;
        private ToolStripMenuItem searchByNameItem = default!;
        private ToolStripMenuItem searchByIdItem = default!;
        private ToolStripMenuItem searchByMonikerItem = default!;
        private ToolStripMenuItem searchByTagItem = default!;
        private List<PackageInfo> _installedPackages = new();
        private List<PackageInfo> _catalogPackages = new();
        private IgnoredPackagesStore _ignored = default!;
        private StoreSettings _settings = default!;
        private ContextMenuStrip _contextMenu = default!;
        private Panel _settingsHost = default!;
        private string? _wingetVersion;
        private Task<string?>? _versionTask;
        private CheckBox chkSilent = default!;
        private CheckBox chkAcceptSourceAgreements = default!;
        private CheckBox chkAcceptPackageAgreements = default!;
        private CheckBox chkIncludeUnknown = default!;
        private CheckBox chkForce = default!;
        private CheckBox chkDisableInteractivity = default!;
        private CheckBox chkWait = default!;

        private static class ActionNames
        {
            internal const string Search = "Search";
            internal const string ShowInfo = "Show";
            internal const string Refresh = "Refresh";
            internal const string Unignore = "Unignore";
            internal const string NoAction = "No action";
            internal const string IgnoreVersion = "Ignore version";
            internal const string IgnorePackage = "Ignore package";
            internal const string CopyCommandLine = "Copy command line";
        }

        private static class KeyHints
        {
            internal const string CtrlF = "Ctrl+F";
            internal const string Delete = "Delete";
            internal const string RefreshKey = "F5";
        }

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

            if (keyData == (Keys.Control | Keys.Tab) || keyData == (Keys.Control | Keys.Shift | Keys.Tab))
            {
                int delta = keyData == (Keys.Control | Keys.Shift | Keys.Tab) ? -1 : 1;
                tabs.SelectedIndex = (tabs.SelectedIndex + delta + tabs.TabCount) % tabs.TabCount;
                return true;
            }

            foreach (var (key, tab) in _tabShortcuts)
            {
                if (keyData == key)
                {
                    tabs.SelectedTab = tab;
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SetupUI()
        {
            MinimumSize = new Size(700, 440);

            // Bold font: action button text and changed versions on the Ignored tab
            _boldFont = new Font(Font, FontStyle.Bold);

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

            _tabShortcuts = new[] { (Keys.F1, _updateTab), (Keys.F2, _installTab), (Keys.F3, _uninstallTab), (Keys.F4, _ignoredTab) };
            _tabToolTips = new ToolTip { AutoPopDelay = 30_000, ShowAlways = true };
            components.Add(_tabToolTips);
            tabs.MouseMove += Tabs_MouseMove;
            tabs.MouseLeave += Tabs_MouseLeave;

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
            txtSearch.ToolTipText = KeyHints.CtrlF;
            toolStrip.Items.Add(txtSearch);

            // Search Button
            btnSearch = new ToolStripButton(ActionNames.Search);
            btnSearch.Click += BtnSearch_Click;
            toolStrip.Items.Add(btnSearch);

            // SearchBy selector (Install tab only): the query option is passed to winget search, no filtering in code
            btnSearchBy = new ToolStripDropDownButton("SearchBy");
            searchByDefaultItem = new ToolStripMenuItem("Default", null, SearchByItem_Click) { Checked = true };
            searchByNameItem = new ToolStripMenuItem("Name", null, SearchByItem_Click);
            searchByIdItem = new ToolStripMenuItem("ID", null, SearchByItem_Click);
            searchByMonikerItem = new ToolStripMenuItem("Moniker", null, SearchByItem_Click);
            searchByTagItem = new ToolStripMenuItem("Tag", null, SearchByItem_Click);
            btnSearchBy.DropDownItems.Add(searchByDefaultItem);
            btnSearchBy.DropDownItems.Add(searchByNameItem);
            btnSearchBy.DropDownItems.Add(searchByIdItem);
            btnSearchBy.DropDownItems.Add(searchByMonikerItem);
            btnSearchBy.DropDownItems.Add(searchByTagItem);
            btnSearchBy.Visible = false;
            toolStrip.Items.Add(btnSearchBy);

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

            // Action Button (verb follows the selected tab): bold - the main action of the current tab
            btnAction = new ToolStripButton();
            btnAction.Font = _boldFont;
            btnAction.Click += BtnAction_Click;
            toolStrip.Items.Add(btnAction);

            // ShowInfo button (label "Show"; grid tabs, selected row): winget show details in a modal window; run by grid double-click
            btnShowInfo = new ToolStripButton(ActionNames.ShowInfo);
            btnShowInfo.Click += BtnShowInfo_Click;
            toolStrip.Items.Add(btnShowInfo);

            // Ignore actions (Update tab only): same rules as the context menu items
            btnIgnoreVersion = new ToolStripButton(ActionNames.IgnoreVersion);
            btnIgnoreVersion.Click += (s, e) => IgnoreSelected(IgnoreScope.Version);
            btnIgnorePackage = new ToolStripButton(ActionNames.IgnorePackage);
            btnIgnorePackage.Click += (s, e) => IgnoreSelected(IgnoreScope.Package);
            toolStrip.Items.Add(btnIgnoreVersion);
            toolStrip.Items.Add(btnIgnorePackage);

            btnRefresh = new ToolStripButton(ActionNames.Refresh);
            btnRefresh.ToolTipText = $"{ActionNames.Refresh} ({KeyHints.RefreshKey})";
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
            dataGridViewPackages.MouseDown += DataGridViewPackages_MouseDown;
            dataGridViewPackages.CellMouseDown += DataGridViewPackages_CellMouseDown;
            dataGridViewPackages.KeyDown += DataGridViewPackages_KeyDown;
            dataGridViewPackages.CellFormatting += DataGridViewPackages_CellFormatting;

            _contextMenu = new ContextMenuStrip();
            _contextMenu.Opening += ContextMenu_Opening;
            dataGridViewPackages.ContextMenuStrip = _contextMenu;

            // Settings panel (Top-docked AutoSize GroupBox "Winget Options", replaces the grid on the Settings tab): writes only in-memory winget options
            _settingsHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Visible = false,
            };
            var optionsGroup = new GroupBox
            {
                Text = "Winget Options",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
            };
            var optionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
            };
            chkSilent = new CheckBox { Text = WingetFlags.Silent, AutoSize = true, Checked = _settings.WingetOptions.Silent };
            chkDisableInteractivity = new CheckBox { Text = WingetFlags.DisableInteractivity, AutoSize = true, Checked = _settings.WingetOptions.DisableInteractivity };
            chkAcceptSourceAgreements = new CheckBox { Text = WingetFlags.AcceptSourceAgreements, AutoSize = true, Checked = _settings.WingetOptions.AcceptSourceAgreements };
            chkAcceptPackageAgreements = new CheckBox { Text = WingetFlags.AcceptPackageAgreements, AutoSize = true, Checked = _settings.WingetOptions.AcceptPackageAgreements };
            chkIncludeUnknown = new CheckBox { Text = WingetFlags.IncludeUnknown, AutoSize = true, Checked = _settings.WingetOptions.IncludeUnknown };
            chkWait = new CheckBox { Text = WingetFlags.Wait, AutoSize = true, Checked = _settings.WingetOptions.Wait };
            chkForce = new CheckBox { Text = WingetFlags.Force, AutoSize = true, Checked = _settings.WingetOptions.Force };
            var optionChecks = new (CheckBox, Action<bool>, string)[]
            {
                (chkSilent, v => _settings.WingetOptions.Silent = v, "Request silent installation"),
                (chkDisableInteractivity, v => _settings.WingetOptions.DisableInteractivity = v, "Disable interactive prompts"),
                (chkAcceptSourceAgreements, v => _settings.WingetOptions.AcceptSourceAgreements = v, "Accept all source agreements during source operations"),
                (chkAcceptPackageAgreements, v => _settings.WingetOptions.AcceptPackageAgreements = v, "Accept all license agreements for packages"),
                (chkIncludeUnknown, v => _settings.WingetOptions.IncludeUnknown = v, "Upgrade packages even if their current version cannot be determined"),
                (chkWait, v => _settings.WingetOptions.Wait = v, "Prompts the user to press any key before exiting"),
                (chkForce, v => _settings.WingetOptions.Force = v, "Direct run the command and continue with non security related issues"),
            };
            var optionToolTips = new ToolTip();
            components.Add(optionToolTips);
            foreach (var (check, _, description) in optionChecks)
            {
                optionsPanel.Controls.Add(check);
                optionToolTips.SetToolTip(check, description);
            }
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
            foreach (var (check, apply, _) in optionChecks)
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

            // DataSource is a plain List: DataGridView's built-in column sorting (header click, F3) needs an
            // IBindingList and throws InvalidOperationException otherwise. Row order is owned by ApplyFilter (Name).
            foreach (DataGridViewColumn column in dataGridViewPackages.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
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

            btnAction.Text = IsIgnoredTab ? ActionNames.Unignore : ModeVerb is null ? ActionNames.NoAction : $"{ModeVerb}";
            if (IsIgnoredTab)
                btnAction.ToolTipText = $"{ActionNames.Unignore} ({KeyHints.Delete})";
            btnAction.Enabled = (idle && IsIgnoredTab)
                ? dataGridViewPackages.SelectedRows.Count > 0
                : idle && ModeVerb is not null && package is not null;

            txtSearch.Enabled = idle;
            btnSearch.Enabled = idle && installMode;
            btnShowInfo.Enabled = idle && IsGridTab && dataGridViewPackages.SelectedRows.Count > 0;
            btnScope.Visible = installMode;
            btnSearchBy.Visible = installMode;
            bool updateMode = IsUpdateTab;
            btnIgnoreVersion.Visible = btnIgnorePackage.Visible = updateMode;
            btnIgnoreVersion.Enabled = btnIgnorePackage.Enabled = idle && updateMode && dataGridViewPackages.SelectedRows.Count > 0;
            btnRefresh.Enabled = idle;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            progressBar.Visible = busy;
            progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            SyncActions();
        }

        private async Task<bool> RunAsync(Func<Task> action)
        {
            if (_busy)
                return false;

            SetBusy(true);
            bool completed = false;
            try
            {
                await action();
                completed = true;
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
            return completed;
        }

        private async Task LoadPackages()
        {
            statusLabel.Text = "Loading packages...";
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
                PackageVerb.Install => SortByName(_catalogPackages),
                PackageVerb.Update => SortByName(_installedPackages.Where(p => p.HasUpdate && !p.Ignored && RowFilter.Matches(filter, p))),
                PackageVerb.Uninstall => SortByName(_installedPackages.Where(p => RowFilter.Matches(filter, p))),
                _ => SortByName(_ignored.Entries.Select(CurrentEntry).Where(e => RowFilter.Matches(filter, e))),
            };

            dataGridViewPackages.DataSource = list;
            // Binding auto-selects row 0; drop it so a refresh never leaves an unclicked row selected
            dataGridViewPackages.ClearSelection();
            dataGridViewPackages.Columns["Scope"]!.Visible = IsIgnoredTab;
            SetupContextMenu();
            ShowPackageCount();
            SyncActions();
        }

        private static List<T> SortByName<T>(IEnumerable<T> items) where T : IPackageRow =>
            items.OrderBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList();

        private IgnoredPackage CurrentEntry(IgnoredPackage entry)
        {
            var match = _installedPackages.Find(p => string.Equals(p.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
            string name = match is null || string.IsNullOrWhiteSpace(match.Name) ? entry.Name : match.Name;
            string availableVersion = match?.AvailableVersion ?? string.Empty;
            if (name == entry.Name && availableVersion == entry.AvailableVersion)
                return entry;

            return new IgnoredPackage
            {
                Scope = entry.Scope,
                Id = entry.Id,
                Name = name,
                Version = entry.Version,
                AvailableVersion = availableVersion,
                Source = entry.Source,
            };
        }

        private Font _boldFont = null!;

        private void DataGridViewPackages_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (!IsIgnoredTab || e.RowIndex < 0 ||
                dataGridViewPackages.Columns[e.ColumnIndex].DataPropertyName != "AvailableVersion" ||
                dataGridViewPackages.Rows[e.RowIndex].DataBoundItem is not IgnoredPackage entry)
            {
                return;
            }

            // Store entries mirror Version = AvailableVersion; only CurrentEntry clones can diverge.
            bool updated = !string.IsNullOrWhiteSpace(entry.AvailableVersion) &&
                !entry.AvailableVersion.Trim().Equals(entry.Version.Trim(), StringComparison.OrdinalIgnoreCase);
            if (updated)
            {
                // Verified: e.CellStyle is non-null; setting Font affects only this cell,
                // not the column default or sibling cells, and repeated calls are safe.
                e.CellStyle!.Font = _boldFont;
            }
        }

        private void SetupContextMenu()
        {
            _contextMenu.Items.Clear();

            if (IsIgnoredTab)
            {
                _contextMenu.Items.Add(ActionNames.Unignore, null, (s, e) => UnignoreSelected());
                _contextMenu.Items.Add(new ToolStripSeparator());
                _contextMenu.Items.Add(ActionNames.ShowInfo, null, async (s, e) => await ShowInfoSelected());
                return;
            }

            if (ModeVerb is not PackageVerb verb)
                return;

            _contextMenu.Items.Add(verb.ToString(), null, async (s, e) => await TryStart(verb));

            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(ActionNames.ShowInfo, null, async (s, e) => await ShowInfoSelected());

            if (verb == PackageVerb.Update)
            {
                _contextMenu.Items.Add(new ToolStripSeparator());
                _contextMenu.Items.Add(ActionNames.IgnoreVersion, null, (s, e) => IgnoreSelected(IgnoreScope.Version));
                _contextMenu.Items.Add(ActionNames.IgnorePackage, null, (s, e) => IgnoreSelected(IgnoreScope.Package));
            }

            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(ActionNames.CopyCommandLine, null, (s, e) => CopyCommandLine(verb));
        }

        private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
                return;
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

            statusLabel.Text = string.Empty;
            if (_wingetVersion is null)
                _wingetVersion = await EnsureWingetVersionAsync();

            if (!_busy && IsSettingsTab)
                statusLabel.Text = _wingetVersion is string version ? $"Winget: {version}" : "Winget version unavailable";
        }

        private Task<string?> EnsureWingetVersionAsync()
        {
            if (_wingetVersion is string version)
                return Task.FromResult<string?>(version);
            if (_versionTask is Task<string?> task && (!task.IsCompleted || task.Result is not null))
                return task;

            _versionTask = Task.Run(FetchWingetVersionAsync);
            return _versionTask;
        }

        private async Task<string?> FetchWingetVersionAsync()
        {
            try
            {
                return await wingetService.GetVersionAsync();
            }
            catch (Exception)
            {
                return null;
            }
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

            if (txtSearch.Enabled && txtSearch.TextBox is TextBox box)
                box.Focus();
        }

        private void Tabs_MouseMove(object? sender, MouseEventArgs e)
        {
            TabPage? hovered = TabAt(e.Location);
            if (hovered == _hoveredTab)
                return;

            _hoveredTab = hovered;
            if (hovered is null || TabKeyText(hovered) is not string key)
            {
                _tabToolTips.Hide(tabs);
                return;
            }

            Rectangle rect = tabs.GetTabRect(tabs.TabPages.IndexOf(hovered));
            _tabToolTips.Show($"{hovered.Text} ({key})", tabs, rect.Left, rect.Bottom);
        }

        private void Tabs_MouseLeave(object? sender, EventArgs e)
        {
            _tabToolTips.Hide(tabs);
            _hoveredTab = null;
        }

        private TabPage? TabAt(Point point)
        {
            for (int index = 0; index < tabs.TabPages.Count; index++)
                if (tabs.GetTabRect(index).Contains(point))
                    return tabs.TabPages[index];

            return null;
        }

        private string? TabKeyText(TabPage? tab)
        {
            if (tab is null)
                return null;

            foreach (var (key, shortcutTab) in _tabShortcuts)
                if (shortcutTab == tab)
                    return key.ToString();

            return null;
        }

        private async void BtnSearch_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
                return;

            await RunAsync(SearchPackages);
        }

        private async Task SearchPackages()
        {
            statusLabel.Text = "Searching packages...";
            var packages = await wingetService.SearchPackagesAsync(txtSearch.Text, SelectedSearchBy);
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

        private async void BtnShowInfo_Click(object? sender, EventArgs e)
        {
            await ShowInfoSelected();
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

        private SearchBy SelectedSearchBy =>
            searchByNameItem.Checked ? SearchBy.Name :
            searchByIdItem.Checked ? SearchBy.Id :
            searchByMonikerItem.Checked ? SearchBy.Moniker :
            searchByTagItem.Checked ? SearchBy.Tag :
            SearchBy.Default;

        private void SearchByItem_Click(object? sender, EventArgs e)
        {
            searchByDefaultItem.Checked = sender == searchByDefaultItem;
            searchByNameItem.Checked = sender == searchByNameItem;
            searchByIdItem.Checked = sender == searchByIdItem;
            searchByMonikerItem.Checked = sender == searchByMonikerItem;
            searchByTagItem.Checked = sender == searchByTagItem;
        }

        private async Task TryStart(PackageVerb verb)
        {
            if (_busy)
                return;

            var package = GetSelectedPackage();
            if (package is null)
                return;

            await RunAsync(() => RunVerbAsync(verb, package));
        }

        private async Task RunVerbAsync(PackageVerb verb, PackageInfo package)
        {
            statusLabel.Text = $"{verb.ProgressStatus()} {package.Name}...";
            await wingetService.RunPackageVerbAsync(verb, package.Id, SelectedScope);
            await LoadPackages();
        }

        private void CopyCommandLine(PackageVerb verb)
        {
            if (GetSelectedPackage() is not PackageInfo package)
                return;

            try
            {
                Clipboard.SetText(wingetService.BuildVerbCommandLine(verb, package.Id, SelectedScope));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async Task ShowInfoSelected()
        {
            if (_busy || dataGridViewPackages.SelectedRows.Count == 0)
                return;

            string name = dataGridViewPackages.SelectedRows[0].DataBoundItem switch
            {
                PackageInfo package => package.Name,
                IgnoredPackage entry => entry.Name,
                _ => string.Empty,
            };
            string id = dataGridViewPackages.SelectedRows[0].DataBoundItem switch
            {
                PackageInfo package => package.Id,
                IgnoredPackage entry => entry.Id,
                _ => string.Empty,
            };
            if (id.Length == 0)
                return;

            await ShowInfoAsync(name, id);
        }

        private async Task ShowInfoAsync(string name, string id)
        {
            string output = string.Empty;
            bool completed = await RunAsync(async () =>
            {
                statusLabel.Text = $"Getting info {name}...";
                output = await wingetService.ShowPackageAsync(id);
            });

            if (!completed)
                return;

            string content = string.IsNullOrWhiteSpace(output) ? "No information available." : output;
            using var dialog = new InfoForm($"Show - {name}", content);
            dialog.ShowDialog(this);
        }

        private void DataGridViewPackages_SelectionChanged(object? sender, EventArgs e)
        {
            SyncActions();
        }

        private void DataGridViewPackages_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            // Right click outside a row (headers, blank space below the rows): CellMouseDown does not fire
            // for the blank space, so drop the selection here to make ContextMenu_Opening cancel the menu.
            var hit = dataGridViewPackages.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0)
                dataGridViewPackages.ClearSelection();
        }

        private void DataGridViewPackages_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            // CellMouseDown (right-button down) fires before ContextMenu_Opening, so the selection made here
            // is already in place when Opening validates it - on the first right click after start, and on a
            // right click that arrives while the menu is open (ToolStripManager closes the menu first, then
            // forwards the click to the grid). MultiSelect = false: selecting a row replaces any previous one.
            if (e.RowIndex >= 0)
                dataGridViewPackages.Rows[e.RowIndex].Selected = true;
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

            await ShowInfoSelected();
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
            await RunAsync(RefreshAsync);
        }

        private async Task RefreshAsync()
        {
            bool installMode = IsInstallTab;

            statusLabel.Text = "Updating sources...";
            await wingetService.UpdateSourcesAsync();
            if (installMode)
            {
                if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                    await SearchPackages();
                return;
            }

            await LoadPackages();
        }

        private async void MainForm_Shown_1(object? sender, EventArgs e)
        {
            await RunAsync(LoadPackages);
            if (txtSearch.Enabled && txtSearch.TextBox is TextBox box)
                box.Focus();
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
                if (_settings.Changed)
                    _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            try
            {
                if (_ignored.Changed)
                    _ignored.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            _boldFont.Dispose();
        }
    }
}
