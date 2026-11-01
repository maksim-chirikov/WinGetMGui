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
        private ToolStripButton btnSearch = default!;
        private ToolStripButton btnAction = default!;
        private ToolStripButton btnRefresh = default!;
        private List<PackageInfo> _installedPackages = new();
        private List<PackageInfo> _catalogPackages = new();
        private IgnoredPackagesStore _ignored = default!;
        private ContextMenuStrip _contextMenu = default!;

        public MainForm()
        {
            InitializeComponent();
            LoadAppIcon();
            _ignored = new IgnoredPackagesStore(IgnoredPackagesStore.DefaultPath);
            _ignored.Load();
            SuspendLayout();
            SetupUI();
            ResumeLayout(true);
            wingetService = new WingetService();
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
            if (keyData == (Keys.Control | Keys.F) && txtSearch.Enabled && txtSearch.TextBox is TextBox box)
            {
                box.Focus();
                box.SelectAll();
                return true;
            }

            if (keyData == Keys.F5 && !_busy)
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
            tabs.TabPages.Add(new TabPage("Update"));
            tabs.TabPages.Add(new TabPage("Install"));
            tabs.TabPages.Add(new TabPage("Uninstall"));
            tabs.TabPages.Add(new TabPage("Ignored"));
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

            // Add controls to form (Order matters for Docking)
            this.Controls.Add(dataGridViewPackages); // Fill
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

        private PackageVerb? ModeVerb => tabs.SelectedIndex switch
        {
            0 => PackageVerb.Update,
            1 => PackageVerb.Install,
            2 => PackageVerb.Uninstall,
            _ => null,
        };

        private PackageInfo? GetSelectedPackage()
        {
            return dataGridViewPackages.SelectedRows.Count > 0
                ? dataGridViewPackages.SelectedRows[0].DataBoundItem as PackageInfo
                : null;
        }

        private bool IsIgnoredTab => tabs.SelectedIndex == 3;

        private void SyncActions()
        {
            var package = GetSelectedPackage();
            bool idle = !_busy;
            bool installMode = ModeVerb == PackageVerb.Install;

            btnAction.Text = IsIgnoredTab ? "Unignore" : ModeVerb is null ? "No action" : $"{ModeVerb}";
            btnAction.Enabled = (idle && IsIgnoredTab)
                ? dataGridViewPackages.SelectedRows.Count > 0
                : idle && ModeVerb is PackageVerb verb && package is not null && package.Allow(verb) is null;

            txtSearch.Enabled = idle;
            btnSearch.Enabled = idle && installMode;
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
            foreach (var package in _installedPackages)
                package.Ignored = _ignored.IsIgnored(package);

            string filter = txtSearch.Text;
            System.Collections.IEnumerable list = ModeVerb switch
            {
                PackageVerb.Install => _catalogPackages.OrderBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
                PackageVerb.Update => _installedPackages.Where(p => p.HasUpdate && RowFilter.Matches(filter, p.Name, p.Id, p.Source, p.Version, p.AvailableVersion)).OrderBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
                PackageVerb.Uninstall => _installedPackages.Where(p => RowFilter.Matches(filter, p.Name, p.Id, p.Source, p.Version, p.AvailableVersion)).OrderBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
                _ => _ignored.Entries.Where(e => RowFilter.Matches(filter, e.Name, e.Id, e.Source, e.AvailableVersion)).OrderBy(e => e.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
            };

            dataGridViewPackages.DataSource = list;
            dataGridViewPackages.ClearSelection();
            dataGridViewPackages.Columns["Scope"].Visible = IsIgnoredTab;
            dataGridViewPackages.Columns["Version"].Visible = !IsIgnoredTab;
            SetupContextMenu();
            dataGridViewPackages.ContextMenuStrip = _contextMenu;
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

        private async void Tabs_SelectedIndexChanged(object? sender, EventArgs e)
        {
            ApplyFilter();
            if (ModeVerb is PackageVerb.Update or PackageVerb.Uninstall && _installedPackages.Count == 0)
                await RunAsync("Loading packages...", LoadPackages);
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

        private async Task TryStart(PackageVerb verb)
        {
            if (_busy)
                return;

            var package = GetSelectedPackage();
            if (package is null)
                return;

            var blockedReason = package.Allow(verb);
            if (blockedReason is not null)
            {
                MessageBox.Show($"{package.Name}: {blockedReason}");
                return;
            }

            await RunPackageAction(verb, package);
        }

        private async Task RunPackageAction(PackageVerb verb, PackageInfo package)
        {
            await RunAsync($"{verb.ProgressStatus()} {package.Name}...", async () =>
            {
                switch (verb)
                {
                    case PackageVerb.Install:
                        await wingetService.InstallPackageAsync(package.Id);
                        break;
                    case PackageVerb.Uninstall:
                        await wingetService.UninstallPackageAsync(package.Id);
                        break;
                    case PackageVerb.Update:
                        await wingetService.UpdatePackageAsync(package.Id);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(verb), verb, null);
                }
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
            if (e.KeyCode != Keys.Delete || !IsIgnoredTab)
                return;

            e.SuppressKeyPress = true;
            UnignoreSelected();
        }

        private void TryStoreAction(Action storeAction)
        {
            try
            {
                storeAction();
                ApplyFilter();
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void IgnoreSelected(IgnoreScope scope)
        {
            if (GetSelectedPackage() is not PackageInfo package)
                return;

            TryStoreAction(() => _ignored.Add(package, scope));
        }

        private void UnignoreSelected()
        {
            if (dataGridViewPackages.SelectedRows.Count == 0 ||
                dataGridViewPackages.SelectedRows[0].DataBoundItem is not IgnoredPackage entry)
                return;

            TryStoreAction(() => _ignored.Remove(entry));
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
            if (ModeVerb == PackageVerb.Install)
                return;

            ApplyFilter();
        }

        private void TxtSearch_Cleared(object? sender, EventArgs e)
        {
            if (ModeVerb != PackageVerb.Install)
                return;

            _catalogPackages = new();
            ApplyFilter();
        }

        private async void BtnRefresh_Click(object? sender, EventArgs e)
        {
            bool installMode = ModeVerb == PackageVerb.Install;

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
    }
}
