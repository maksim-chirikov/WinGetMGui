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
        // The product name shown in the title bar and the About box (not ProductName: Control has that member)
        private const string ProductDisplayName = "WinGet Minimal GUI";

        private TabControl tabs = default!;
        private TabPage _updateTab = default!;
        private TabPage _installTab = default!;
        private TabPage _uninstallTab = default!;
        private TabPage _ignoredTab = default!;
        private TabPage _settingsTab = default!;
        private Dictionary<TabPage, CaptionInfo> _tabCaptions = default!;
        private ToolTip _tabToolTips = default!;
        private TabPage? _hoveredTab;
        // The two grid columns whose visibility follows the tab; captured here so ApplyFilter does not look them up by name
        private DataGridViewColumn _gridSourceColumn = default!;
        private DataGridViewColumn _gridScopeColumn = default!;
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
        private Dictionary<SearchBy, ToolStripMenuItem> _searchByItems = default!;
        // The installed list; its Id index serves every cross-list lookup (InstalledPackages.cs).
        private InstalledPackages _installedPackages = InstalledPackages.Empty;
        private List<PackageInfo> _catalogPackages = new();
        private IgnoredPackagesStore _ignored = default!;
        private IReadOnlyList<IgnoredRow> _ignoredRows = Array.Empty<IgnoredRow>();
        private StoreSettings _settings = default!;
        private ContextMenuStrip _contextMenu = default!;
        private Panel _settingsHost = default!;
        private string? _wingetVersion;
        private Task<string?>? _versionTask;

        private sealed class CaptionInfo
        {
            public string Caption { get; }
            public string? KeyHint { get; }
            public Keys? Key { get; }

            public CaptionInfo(string caption, string? keyHint = null, Keys? key = null)
            {
                Caption = caption;
                KeyHint = keyHint;
                Key = key;
            }

            public string ToolTip =>
                string.IsNullOrEmpty(KeyHint) ? Caption
                : string.IsNullOrEmpty(Caption) ? KeyHint
                : $"{Caption} ({KeyHint})";
        }

        private static class ActionInfo
        {
            internal static readonly CaptionInfo Search = new("Search");
            internal static readonly CaptionInfo ShowInfo = new("Show");
            internal static readonly CaptionInfo Refresh = new("Refresh", "F5", Keys.F5);
            internal static readonly CaptionInfo Unignore = new("Unignore", "Delete", Keys.Delete);
            internal static readonly CaptionInfo NoAction = new("No action");
            internal static readonly CaptionInfo IgnoreVersion = new("Ignore version");
            internal static readonly CaptionInfo IgnorePackage = new("Ignore package");
            internal static readonly CaptionInfo CopyCommandLine = new("Copy command line");

            internal static readonly CaptionInfo SearchFocus = new("", "Ctrl+F", Keys.Control | Keys.F);
        }

        private static class TabInfo
        {
            internal static readonly CaptionInfo Update = new("Update", "F1", Keys.F1);
            internal static readonly CaptionInfo Install = new("Install", "F2", Keys.F2);
            internal static readonly CaptionInfo Uninstall = new("Uninstall", "F3", Keys.F3);
            internal static readonly CaptionInfo Ignored = new("Ignored", "F4", Keys.F4);
            internal static readonly CaptionInfo Settings = new("Settings");
        }

        // One winget option: the flag token shown as the checkbox text, its help description, and the accessor pair
        private sealed record class OptionInfo(
            string Flag,
            string Description,
            Func<WingetOptions, bool> Get,
            Action<WingetOptions, bool> Set);

        private static class OptionInfoTable
        {
            // Order = the order the checkboxes are shown in the Settings tab
            internal static readonly OptionInfo[] All =
            {
                new(WingetFlags.Silent, "Request silent installation", o => o.Silent, (o, v) => o.Silent = v),
                new(WingetFlags.DisableInteractivity, "Disable interactive prompts", o => o.DisableInteractivity, (o, v) => o.DisableInteractivity = v),
                new(WingetFlags.AcceptSourceAgreements, "Accept all source agreements during source operations", o => o.AcceptSourceAgreements, (o, v) => o.AcceptSourceAgreements = v),
                new(WingetFlags.AcceptPackageAgreements, "Accept all license agreements for packages", o => o.AcceptPackageAgreements, (o, v) => o.AcceptPackageAgreements = v),
                new(WingetFlags.IncludeUnknown, "Upgrade packages even if their current version cannot be determined", o => o.IncludeUnknown, (o, v) => o.IncludeUnknown = v),
                new(WingetFlags.Wait, "Prompts the user to press any key before exiting", o => o.Wait, (o, v) => o.Wait = v),
                new(WingetFlags.Force, "Direct run the command and continue with non security related issues", o => o.Force, (o, v) => o.Force = v),
            };
        }

        public MainForm()
        {
            InitializeComponent();
            LoadAppIcon();
            _ignored = new IgnoredPackagesStore(IgnoredPackagesStore.DefaultPath);
            LoadStore(IgnoredPackagesStore.DefaultPath, () => _ignored.Load());
            // The Ignored tab is reachable while the startup winget list is still running, so its rows must
            // exist from the rules alone (no live installed state yet).
            RecalcIgnored();
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
            if (keyData == ActionInfo.SearchFocus.Key && IsGridTab && txtSearch.Enabled && txtSearch.TextBox is TextBox box)
            {
                box.Focus();
                box.SelectAll();
                return true;
            }

            if (keyData == ActionInfo.Refresh.Key && !_busy && IsGridTab)
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

            foreach (var (page, caption) in _tabCaptions)
            {
                if (keyData == caption.Key)
                {
                    tabs.SelectedTab = page;
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SetupUI()
        {
            MinimumSize = new Size(700, 440);

            // Bold font: action button text
            _boldFont = new Font(Font, FontStyle.Bold);

            // Create TabControl (Top) - tab acts as the action mode
            tabs = new TabControl
            {
                Dock = DockStyle.Top,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(120, 28),
                Height = 33
            };
            _updateTab = new TabPage(TabInfo.Update.Caption);
            _installTab = new TabPage(TabInfo.Install.Caption);
            _uninstallTab = new TabPage(TabInfo.Uninstall.Caption);
            _ignoredTab = new TabPage(TabInfo.Ignored.Caption);
            _settingsTab = new TabPage(TabInfo.Settings.Caption);
            tabs.TabPages.AddRange(new[] { _updateTab, _installTab, _uninstallTab, _ignoredTab, _settingsTab });
            // State the initial mode. The first tab is the default only once the control's handle exists -
            // until then SelectedTab reads null (SelectedIndex = -1)
            tabs.SelectedTab = _updateTab;
            tabs.SelectedIndexChanged += Tabs_SelectedIndexChanged;

            // One entry per tab: caption + its F1-F4 key (the Settings tab has no key, so it has no shortcut)
            _tabCaptions = new Dictionary<TabPage, CaptionInfo>
            {
                [_updateTab] = TabInfo.Update,
                [_installTab] = TabInfo.Install,
                [_uninstallTab] = TabInfo.Uninstall,
                [_ignoredTab] = TabInfo.Ignored,
                [_settingsTab] = TabInfo.Settings
            };
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
            txtSearch.ToolTipText = ActionInfo.SearchFocus.ToolTip;
            toolStrip.Items.Add(txtSearch);

            // Search Button
            btnSearch = new ToolStripButton(ActionInfo.Search.Caption);
            btnSearch.Click += BtnSearch_Click;
            toolStrip.Items.Add(btnSearch);

            // SearchBy selector (Install tab only): the query option is passed to winget search, no filtering in code
            btnSearchBy = new ToolStripDropDownButton("SearchBy");
            _searchByItems = new Dictionary<SearchBy, ToolStripMenuItem>();
            foreach (var searchBy in Enum.GetValues<SearchBy>())
            {
                var item = new ToolStripMenuItem(searchBy.ToString(), null, SearchByItem_Click)
                {
                    Checked = searchBy == SearchBy.Default,
                    Tag = searchBy,
                };
                _searchByItems[searchBy] = item;
                btnSearchBy.DropDownItems.Add(item);
            }
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
            btnShowInfo = new ToolStripButton(ActionInfo.ShowInfo.Caption);
            btnShowInfo.Click += BtnShowInfo_Click;
            toolStrip.Items.Add(btnShowInfo);

            // Ignore actions (Update tab only): same rules as the context menu items
            btnIgnoreVersion = new ToolStripButton(ActionInfo.IgnoreVersion.Caption);
            btnIgnoreVersion.Click += (s, e) => IgnoreSelected(IgnoreScope.Version);
            btnIgnorePackage = new ToolStripButton(ActionInfo.IgnorePackage.Caption);
            btnIgnorePackage.Click += (s, e) => IgnoreSelected(IgnoreScope.Package);
            toolStrip.Items.Add(btnIgnoreVersion);
            toolStrip.Items.Add(btnIgnorePackage);

            btnRefresh = new ToolStripButton(ActionInfo.Refresh.Caption);
            btnRefresh.ToolTipText = ActionInfo.Refresh.ToolTip;
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

            _contextMenu = new ContextMenuStrip();
            components.Add(_contextMenu);
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
            var optionToolTips = new ToolTip();
            components.Add(optionToolTips);
            foreach (var option in OptionInfoTable.All)
            {
                // Checked comes from the store in the initializer, before CheckedChanged is attached, so the
                // initial value never marks the store Changed; the handler is the only writer of the option.
                var check = new CheckBox { Text = option.Flag, AutoSize = true, Checked = option.Get(_settings.WingetOptions) };
                check.CheckedChanged += (s, e) =>
                {
                    _settings.Changed = true;
                    option.Set(_settings.WingetOptions, check.Checked);
                };
                optionsPanel.Controls.Add(check);
                optionToolTips.SetToolTip(check, option.Description);
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
            SyncActions();
        }

        private void SetupDataGridView()
        {
            dataGridViewPackages.Columns.Clear();

            int nameIdx = dataGridViewPackages.Columns.Add("Name", "Name");
            dataGridViewPackages.Columns[nameIdx].DataPropertyName = "Name";
            dataGridViewPackages.Columns[nameIdx].FillWeight = 200;

            int versionIdx = dataGridViewPackages.Columns.Add("Version", "Version");
            dataGridViewPackages.Columns[versionIdx].DataPropertyName = "Version";

            int availVersionIdx = dataGridViewPackages.Columns.Add("Available Version", "Available Version");
            dataGridViewPackages.Columns[availVersionIdx].DataPropertyName = "AvailableVersion";

            int idIdx = dataGridViewPackages.Columns.Add("Id", "Id");
            dataGridViewPackages.Columns[idIdx].DataPropertyName = "Id";

            int sourceIdx = dataGridViewPackages.Columns.Add("Source", "Source");
            _gridSourceColumn = dataGridViewPackages.Columns[sourceIdx];
            _gridSourceColumn.DataPropertyName = "Source";

            int scopeIdx = dataGridViewPackages.Columns.Add("Scope", "Scope");
            _gridScopeColumn = dataGridViewPackages.Columns[scopeIdx];
            _gridScopeColumn.DataPropertyName = "Scope";
            _gridScopeColumn.FillWeight = 100;

            // DataSource is a plain List: DataGridView's built-in column sorting (header click, F3) needs an
            // IBindingList and throws InvalidOperationException otherwise. Row order is owned by ApplyFilter (Name).
            foreach (DataGridViewColumn column in dataGridViewPackages.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        // The package action the selected tab drives. One explicit branch per tab (same shape as
        // ApplyFilter): Ignored and Settings run no winget verb, so they return null.
        private PackageVerb? GetModeVerb()
        {
            if (IsUpdateTab)
                return PackageVerb.Update;
            else if (IsInstallTab)
                return PackageVerb.Install;
            else if (IsUninstallTab)
                return PackageVerb.Uninstall;
            else if (IsIgnoredTab || IsSettingsTab)
                return null;
            else
                throw new InvalidOperationException("No action for the selected tab");
        }

        // The selected grid row is read only through these: each tab binds one row model
        // (Update/Uninstall -> PackageInfo, Install -> CatalogRow, Ignored -> IgnoredRow), so IPackageRow is
        // the shape every action needs, and the Ignored accessor narrows it without a cast.
        private IPackageRow? GetSelectedRow() =>
            dataGridViewPackages.SelectedRows.Count > 0
                ? dataGridViewPackages.SelectedRows[0].DataBoundItem as IPackageRow
                : null;

        private IgnoredRow? GetSelectedIgnored() => GetSelectedRow() as IgnoredRow;

        private bool IsUpdateTab => tabs.SelectedTab == _updateTab;
        private bool IsInstallTab => tabs.SelectedTab == _installTab;
        private bool IsUninstallTab => tabs.SelectedTab == _uninstallTab;
        private bool IsIgnoredTab => tabs.SelectedTab == _ignoredTab;
        private bool IsSettingsTab => tabs.SelectedTab == _settingsTab;
        private bool IsGridTab => IsUpdateTab || IsInstallTab || IsUninstallTab || IsIgnoredTab;

        private void SyncActions()
        {
            bool idle = !_busy;
            bool installMode = IsInstallTab;
            bool updateMode = IsUpdateTab;
            bool rowSelected = GetSelectedRow() is not null;
            PackageVerb? verb = GetModeVerb();

            btnAction.Text = IsIgnoredTab ? ActionInfo.Unignore.Caption : verb?.Caption() ?? ActionInfo.NoAction.Caption;
            btnAction.ToolTipText = IsIgnoredTab ? ActionInfo.Unignore.ToolTip : string.Empty;
            btnAction.Enabled = idle && (IsIgnoredTab
                ? GetSelectedIgnored() is not null
                : verb is not null && rowSelected);

            txtSearch.Enabled = idle;
            btnSearch.Enabled = idle && installMode;
            btnShowInfo.Enabled = idle && IsGridTab && rowSelected;
            btnScope.Visible = installMode;
            btnSearchBy.Visible = installMode;
            btnIgnoreVersion.Visible = btnIgnorePackage.Visible = updateMode;
            btnIgnoreVersion.Enabled = btnIgnorePackage.Enabled = idle && updateMode && rowSelected;
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
            _installedPackages = new InstalledPackages(await wingetService.GetInstalledPackagesAsync());
            RecalcIgnored();
            ApplyFilter();
        }

        // The ignore-derived state: the Ignored flag on each installed row, and the Ignored-tab rows.
        // Both depend only on the ignore store plus each installed row's Id/AvailableVersion (fixed once the
        // row is parsed). Recompute it when those change - the store load, an installed reload, an
        // ignore/unignore - not on every search keystroke (ApplyFilter runs there).
        private void RecalcIgnored()
        {
            foreach (var package in _installedPackages)
                package.Ignored = _ignored.IsIgnored(package);
            _ignoredRows = _ignored.BuildRows(_installedPackages);
        }

        private void ApplyFilter()
        {
            if (IsSettingsTab)
                return;

            string filter = txtSearch.Text;
            // One row source per tab, stated here (same shape as GetModeVerb): every list is sorted by
            // Name and filtered by the search text, except the Install catalog - its text was the winget query,
            // so its rows are only joined with the installed list (InstalledPackages.Match).
            IEnumerable<IPackageRow> list;
            if (IsUpdateTab)
                list = SortByName(_installedPackages.Where(p => p.HasUpdate && !p.Ignored && RowFilter.Matches(filter, p)));
            else if (IsInstallTab)
                list = SortByName(_catalogPackages.Select(_installedPackages.Match));
            else if (IsUninstallTab)
                list = SortByName(_installedPackages.Where(p => RowFilter.Matches(filter, p)));
            else if (IsIgnoredTab)
                list = SortByName(_ignoredRows.Where(r => RowFilter.Matches(filter, r)));
            else
                throw new InvalidOperationException("No row source for the selected tab");

            dataGridViewPackages.DataSource = list;
            // Binding auto-selects row 0; drop it so a refresh never leaves an unclicked row selected
            dataGridViewPackages.ClearSelection();
            _gridSourceColumn.Visible = !IsIgnoredTab;
            _gridScopeColumn.Visible = IsIgnoredTab;
            SetupContextMenu();
            ShowPackageCount();
            SyncActions();
        }

        private static List<T> SortByName<T>(IEnumerable<T> items) where T : IPackageRow =>
            items.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

        private Font _boldFont = null!;

        private void SetupContextMenu()
        {
            _contextMenu.Items.Clear();

            if (IsIgnoredTab)
            {
                _contextMenu.Items.Add(ActionInfo.Unignore.Caption, null, (s, e) => UnignoreSelected());
                _contextMenu.Items.Add(new ToolStripSeparator());
                _contextMenu.Items.Add(ActionInfo.ShowInfo.Caption, null, async (s, e) => await ShowInfoSelected());
                return;
            }

            if (GetModeVerb() is not PackageVerb verb)
                return;

            _contextMenu.Items.Add(verb.Caption(), null, async (s, e) => await TryStart(verb));

            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(ActionInfo.ShowInfo.Caption, null, async (s, e) => await ShowInfoSelected());

            if (verb == PackageVerb.Update)
            {
                _contextMenu.Items.Add(new ToolStripSeparator());
                _contextMenu.Items.Add(ActionInfo.IgnoreVersion.Caption, null, (s, e) => IgnoreSelected(IgnoreScope.Version));
                _contextMenu.Items.Add(ActionInfo.IgnorePackage.Caption, null, (s, e) => IgnoreSelected(IgnoreScope.Package));
            }

            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(ActionInfo.CopyCommandLine.Caption, null, (s, e) => CopyCommandLine(verb));
        }

        private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = IsIgnoredTab
                ? GetSelectedIgnored() is null
                : GetSelectedRow() is null;
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
            if (hovered is null || !_tabCaptions.TryGetValue(hovered, out var caption))
            {
                _tabToolTips.Hide(tabs);
                return;
            }

            Rectangle rect = tabs.GetTabRect(tabs.TabPages.IndexOf(hovered));
            _tabToolTips.Show(caption.ToolTip, tabs, rect.Left, rect.Bottom);
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

        private async void BtnSearch_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
                return;

            await RunAsync(SearchPackages);
        }

        private async Task SearchPackages()
        {
            statusLabel.Text = "Searching packages...";
            var packages = await wingetService.SearchPackagesAsync(txtSearch.Text, GetSelectedSearchBy());
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

            if (GetModeVerb() is not PackageVerb verb)
                return;

            await TryStart(verb);
        }

        private async void BtnShowInfo_Click(object? sender, EventArgs e)
        {
            await ShowInfoSelected();
        }

        private PackageScope GetSelectedScope() =>
            scopeUserItem.Checked ? PackageScope.User :
            scopeMachineItem.Checked ? PackageScope.Machine :
            PackageScope.Default;

        private void ScopeItem_Click(object? sender, EventArgs e)
        {
            scopeDefaultItem.Checked = sender == scopeDefaultItem;
            scopeUserItem.Checked = sender == scopeUserItem;
            scopeMachineItem.Checked = sender == scopeMachineItem;
        }

        private SearchBy GetSelectedSearchBy() =>
            _searchByItems.First(kv => kv.Value.Checked).Key;

        private void SearchByItem_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem clicked)
                return;
            foreach (var item in _searchByItems.Values)
                item.Checked = ReferenceEquals(item, clicked);
        }

        private async Task TryStart(PackageVerb verb)
        {
            if (_busy)
                return;

            var row = GetSelectedRow();
            if (row is null)
                return;

            await RunAsync(() => RunVerbAsync(verb, row));
        }

        // The verb needs only the row's Id: which winget action runs is the tab's, not the row's.
        private async Task RunVerbAsync(PackageVerb verb, IPackageRow row)
        {
            statusLabel.Text = $"{verb.ProgressStatus()} {row.Name}...";
            await wingetService.RunPackageVerbAsync(verb, row.Id, GetSelectedScope());
            await LoadPackages();
        }

        private void CopyCommandLine(PackageVerb verb)
        {
            var row = GetSelectedRow();
            if (row is null)
                return;

            try
            {
                Clipboard.SetText(wingetService.BuildVerbCommandLine(verb, row.Id, GetSelectedScope()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async Task ShowInfoSelected()
        {
            var row = GetSelectedRow();
            if (_busy || row is null || row.Id.Length == 0)
                return;

            await ShowInfoAsync(row.Name, row.Id);
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
            if (e.KeyCode == ActionInfo.Unignore.Key && IsIgnoredTab)
            {
                e.SuppressKeyPress = true;
                UnignoreSelected();
            }
        }

        private void ApplyStoreAction(Action storeAction)
        {
            storeAction();
            RecalcIgnored();
            ApplyFilter();
        }

        private void IgnoreSelected(IgnoreScope scope)
        {
            var row = GetSelectedRow();
            if (row is null)
                return;

            ApplyStoreAction(() => _ignored.Add(row, scope));
        }

        private void UnignoreSelected()
        {
            var row = GetSelectedIgnored();
            if (row is null)
                return;

            ApplyStoreAction(() => _ignored.Remove(row.Rule));
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
            // Same on every tab: sources + installed list (which also refreshes the Ignored store's
            // derived fields). A catalog search is re-run only by the Search button/Enter.
            statusLabel.Text = "Updating sources...";
            await wingetService.UpdateSourcesAsync();
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
