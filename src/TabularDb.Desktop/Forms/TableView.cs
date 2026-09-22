using TabularDb.Desktop.Services;

namespace TabularDb.Desktop.Forms;

public sealed class TableView : UserControl
{
    public const int PageSize = 100;

    private readonly IDatabaseClient _client;
    private readonly string _db;
    private readonly Label _title = new() { AutoSize = true, Text = "Оберіть таблицю" };
    private readonly Label _pageLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
    };
    private readonly FlowLayoutPanel _buttons = new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
    private readonly Button _prevButton;
    private readonly Button _nextButton;

    private TableSchema? _schema;
    private int _skip;
    private int _total;

    public event EventHandler? DataChanged;

    public string? TableName => _schema?.Name;

    public TableView(IDatabaseClient client, string db)
    {
        _client = client;
        _db = db;

        _grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0)
                await EditAsync();
        };

        _prevButton = Ui.MakeButton("◀", async (_, _) => await ChangePageAsync(-PageSize));
        _nextButton = Ui.MakeButton("▶", async (_, _) => await ChangePageAsync(PageSize));
        _buttons.Controls.AddRange([
            Ui.MakeButton("Додати…", async (_, _) => await AddAsync()),
            Ui.MakeButton("Редагувати…", async (_, _) => await EditAsync()),
            Ui.MakeButton("Видалити", async (_, _) => await DeleteAsync()),
            Ui.MakeButton("Оновити", async (_, _) => await ReloadAsync()),
            _prevButton,
            _pageLabel,
            _nextButton,
        ]);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_title, 0, 0);
        layout.Controls.Add(_grid, 0, 1);
        layout.Controls.Add(_buttons, 0, 2);
        Controls.Add(layout);

        ShowTable(null);
    }

    public async Task OpenAsync(string? table)
    {
        _skip = 0;
        if (table is null)
        {
            ShowTable(null);
            return;
        }
        ShowTable(null);
        await Ui.RunAsync(this, async () =>
        {
            var schema = await _client.GetSchemaAsync(_db, table);
            ShowTable(schema);
            await LoadPageAsync();
        });
    }

    public Task ReloadAsync() =>
        _schema is null ? Task.CompletedTask : Ui.RunAsync(this, LoadPageAsync);

    private void ShowTable(TableSchema? schema)
    {
        _schema = schema;
        _grid.Rows.Clear();
        _grid.Columns.Clear();
        _buttons.Enabled = schema is not null;
        if (schema is null)
        {
            _title.Text = "Оберіть таблицю";
            _pageLabel.Text = "";
            return;
        }

        _title.Text = $"Таблиця: {schema.Name}";
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "id", HeaderText = "id", SortMode = DataGridViewColumnSortMode.NotSortable });
        foreach (var field in schema.Fields)
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = $"{field.Name} : {field.Type}",
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });
    }

    private async Task LoadPageAsync()
    {
        if (_schema is null)
            return;
        var page = await _client.GetRowsAsync(_db, _schema.Name, _skip, PageSize);
        if (page.Rows.Count == 0 && _skip > 0 && page.Total > 0)
        {
            _skip = Math.Max(0, (page.Total - 1) / PageSize * PageSize);
            page = await _client.GetRowsAsync(_db, _schema.Name, _skip, PageSize);
        }

        _total = page.Total;
        _grid.Rows.Clear();
        foreach (var row in page.Rows)
        {
            var cells = new object[row.Values.Count + 1];
            cells[0] = row.Id;
            for (var i = 0; i < row.Values.Count; i++)
                cells[i + 1] = row.Values[i] ?? "";
            var index = _grid.Rows.Add(cells);
            _grid.Rows[index].Tag = row;
        }

        var from = _total == 0 ? 0 : _skip + 1;
        _pageLabel.Text = $"{from}–{_skip + page.Rows.Count} з {_total}";
        _prevButton.Enabled = _skip > 0;
        _nextButton.Enabled = _skip + PageSize < _total;
    }

    private async Task ChangePageAsync(int delta)
    {
        _skip = Math.Max(0, _skip + delta);
        await ReloadAsync();
    }

    private RowItem? SelectedRow() => _grid.CurrentRow?.Tag as RowItem;

    private async Task AddAsync()
    {
        if (_schema is null)
            return;
        var schema = _schema;
        using var form = new RowEditForm(schema, null, values => _client.AddRowAsync(_db, schema.Name, values));
        if (form.ShowDialog(this) != DialogResult.OK)
            return;
        _skip = Math.Max(0, (_total) / PageSize * PageSize);
        await AfterChangeAsync();
    }

    private async Task EditAsync()
    {
        var row = SelectedRow();
        if (_schema is null || row is null)
            return;
        var schema = _schema;
        using var form = new RowEditForm(schema, row, values => _client.UpdateRowAsync(_db, schema.Name, row.Id, values));
        if (form.ShowDialog(this) == DialogResult.OK)
            await AfterChangeAsync();
    }

    private async Task DeleteAsync()
    {
        var row = SelectedRow();
        if (_schema is null || row is null)
            return;
        if (!Ui.Confirm(this, $"Видалити рядок з id {row.Id}?"))
            return;
        var schema = _schema;
        if (await Ui.RunAsync(this, () => _client.DeleteRowAsync(_db, schema.Name, row.Id)))
            await AfterChangeAsync();
    }

    private async Task AfterChangeAsync()
    {
        await ReloadAsync();
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
