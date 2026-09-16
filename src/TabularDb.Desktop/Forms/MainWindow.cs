using TabularDb.Desktop.Controllers;
using TabularDb.Desktop.Services;

namespace TabularDb.Desktop.Forms;

public sealed class MainWindow : Form
{
    private readonly IDatabaseClient _client;
    private readonly string _db;
    private readonly ListBox _tableList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TableView _tableView;
    private readonly ToolStripStatusLabel _serverLabel = new();
    private readonly ToolStripStatusLabel _dbLabel = new();
    private readonly ToolStripStatusLabel _modifiedLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private IReadOnlyList<string> _types = [];
    private bool _isModified;
    private bool _closeConfirmed;
    private bool _ignoreSelection;

    public bool SwitchRequested { get; private set; }

    public MainWindow(IDatabaseClient client, string db)
    {
        _client = client;
        _db = db;
        Text = $"{db} — {Ui.AppTitle}";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1000, 640);
        MinimumSize = new Size(700, 400);

        _tableView = new TableView(client, db) { Dock = DockStyle.Fill };
        _tableView.DataChanged += async (_, _) => await RefreshStatusAsync();
        _tableList.SelectedIndexChanged += async (_, _) =>
        {
            if (!_ignoreSelection)
                await _tableView.OpenAsync(_tableList.SelectedItem as string);
        };

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(new Label { Text = "Таблиці:", AutoSize = true }, 0, 0);
        left.Controls.Add(_tableList, 0, 1);
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(_tableView);

        _serverLabel.Text = $"Сервер: {client.ServerUrl}";
        _dbLabel.Text = $"База: {db}";
        var status = new StatusStrip();
        status.Items.AddRange([_serverLabel, _dbLabel, _modifiedLabel]);

        Controls.Add(split);
        Controls.Add(BuildMenu());
        Controls.Add(status);

        Load += async (_, _) =>
        {
            split.SplitterDistance = 220;
            await ReloadTablesAsync();
        };
    }

    private MenuStrip BuildMenu()
    {
        var file = new ToolStripMenuItem("Файл");
        file.DropDownItems.AddRange([
            new ToolStripMenuItem("Зберегти на сервері", null, async (_, _) => await SaveAsync(), Keys.Control | Keys.S),
            new ToolStripMenuItem("Перечитати з диска", null, async (_, _) => await ReloadFromDiskAsync()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Експорт у файл…", null, async (_, _) => await ExportAsync()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Інша база…", null, (_, _) =>
            {
                SwitchRequested = true;
                Close();
            }),
            new ToolStripMenuItem("Вихід", null, (_, _) => Close()),
        ]);

        var table = new ToolStripMenuItem("Таблиця");
        table.DropDownItems.AddRange([
            new ToolStripMenuItem("Нова…", null, async (_, _) => await CreateTableAsync()),
            new ToolStripMenuItem("Видалити", null, async (_, _) => await DropTableAsync()),
            new ToolStripMenuItem("Оновити список", null, async (_, _) => await ReloadTablesAsync()),
        ]);

        var operations = new ToolStripMenuItem("Операції");
        operations.DropDownItems.Add(
            new ToolStripMenuItem("Вилучити повторювані рядки…", null, async (_, _) => await RemoveDuplicatesAsync()));

        var menu = new MenuStrip();
        menu.Items.AddRange([file, table, operations]);
        MainMenuStrip = menu;
        return menu;
    }

    private void SetModified(bool modified)
    {
        _isModified = modified;
        _modifiedLabel.Text = modified ? "● є незбережені зміни" : "усі зміни збережено";
    }

    private async Task RefreshStatusAsync()
    {
        await Ui.RunAsync(this, async () => SetModified((await _client.ListTablesAsync(_db)).IsModified));
    }

    private async Task ReloadTablesAsync(string? select = null)
    {
        select ??= _tableList.SelectedItem as string;
        await Ui.RunAsync(this, async () =>
        {
            if (_types.Count == 0)
                _types = (await _client.ListDatabasesAsync()).Types;
            var listing = await _client.ListTablesAsync(_db);
            SetModified(listing.IsModified);
            _ignoreSelection = true;
            try
            {
                _tableList.Items.Clear();
                _tableList.Items.AddRange([.. listing.Tables]);
                var index = select is null ? -1 : _tableList.Items.IndexOf(select);
                if (index < 0 && _tableList.Items.Count > 0)
                    index = 0;
                _tableList.SelectedIndex = index;
            }
            finally
            {
                _ignoreSelection = false;
            }
        });
        await _tableView.OpenAsync(_tableList.SelectedItem as string);
    }

    private async Task<bool> SaveAsync()
    {
        var ok = await Ui.RunAsync(this, () => _client.SaveDatabaseAsync(_db));
        if (ok)
            SetModified(false);
        return ok;
    }

    private async Task ReloadFromDiskAsync()
    {
        if (_isModified && !Ui.Confirm(this, "Незбережені зміни буде втрачено. Продовжити?"))
            return;
        if (await Ui.RunAsync(this, () => _client.LoadDatabaseAsync(_db)))
            await ReloadTablesAsync();
    }

    private async Task ExportAsync()
    {
        ExportedFile? file = null;
        if (!await Ui.RunAsync(this, async () => file = await _client.ExportDatabaseAsync(_db)) || file is null)
            return;
        using var dialog = new SaveFileDialog
        {
            FileName = file.FileName,
            Filter = "База TabularDb (*.tdb.json)|*.tdb.json",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            await File.WriteAllTextAsync(dialog.FileName, file.Content);
            Ui.ShowInfo(this, "Базу експортовано.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Не вдалося записати файл: {ex.Message}", Ui.AppTitle);
        }
    }

    private async Task CreateTableAsync()
    {
        using var form = new CreateTableForm(_client, _db, _types);
        if (form.ShowDialog(this) == DialogResult.OK)
            await ReloadTablesAsync(form.CreatedTable);
    }

    private async Task DropTableAsync()
    {
        if (_tableList.SelectedItem is not string table)
            return;
        if (!Ui.Confirm(this, $"Видалити таблицю '{table}' разом з усіма рядками?"))
            return;
        if (await Ui.RunAsync(this, () => _client.DropTableAsync(_db, table)))
        {
            _tableList.ClearSelected();
            await ReloadTablesAsync();
        }
    }

    private async Task RemoveDuplicatesAsync()
    {
        var controller = new RemoveDuplicatesController(_client, _db);
        using var form = new RemoveDuplicatesForm(controller, _tableView.TableName);
        form.ShowDialog(this);
        if (form.Changed)
        {
            await _tableView.ReloadAsync();
            await RefreshStatusAsync();
        }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (_closeConfirmed || !_isModified || e.CloseReason == CloseReason.WindowsShutDown)
            return;

        e.Cancel = true;
        var answer = MessageBox.Show(this, "У базі є незбережені зміни. Зберегти їх на сервері?",
            Ui.AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Cancel)
        {
            SwitchRequested = false;
            return;
        }
        if (answer == DialogResult.Yes && !await SaveAsync())
        {
            SwitchRequested = false;
            return;
        }
        _closeConfirmed = true;
        Close();
    }
}
