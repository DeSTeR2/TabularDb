using TabularDb.Desktop.Services;

namespace TabularDb.Desktop.Forms;

public sealed class ConnectForm : Form
{
    private readonly ClientSettings _settings;
    private readonly TextBox _serverBox = new() { Width = 320 };
    private readonly TextBox _keyBox = new() { Width = 320, UseSystemPasswordChar = true };
    private readonly CheckBox _grpcWebBox = new() { Text = "gRPC-Web через HTTP/1.1 (локальний сервер: порт 5101)", AutoSize = true };
    private readonly ListBox _dbList = new() { Width = 320, Height = 180, IntegralHeight = false };
    private readonly TextBox _newDbBox = new() { Width = 200 };
    private readonly Label _statusLabel = new() { AutoSize = true, Text = "Не підключено" };
    private readonly Button _openButton;
    private readonly Button _createButton;
    private List<DatabaseItem> _databases = [];

    public IDatabaseClient? Client { get; private set; }
    public string? DatabaseName { get; private set; }

    public ConnectForm(ClientSettings settings)
    {
        _settings = settings;
        Text = "Підключення до сервера";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        _serverBox.Text = settings.ServerUrl;
        _keyBox.Text = settings.ApiKey ?? "";
        _grpcWebBox.Checked = settings.UseGrpcWeb;
        _openButton = Ui.MakeButton("Відкрити", async (_, _) => await OpenAsync());
        _dbList.DoubleClick += async (_, _) => await OpenAsync();

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "Адреса сервера:", AutoSize = true, Anchor = AnchorStyles.Left });
        layout.Controls.Add(_serverBox);
        layout.Controls.Add(new Label { Text = "API-ключ:", AutoSize = true, Anchor = AnchorStyles.Left });
        layout.Controls.Add(_keyBox);
        layout.Controls.Add(new Label());
        layout.Controls.Add(_grpcWebBox);
        layout.Controls.Add(new Label());
        layout.Controls.Add(Row(Ui.MakeButton("Підключитися", async (_, _) => await ConnectAsync()), _statusLabel));
        layout.Controls.Add(new Label { Text = "Бази даних:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top });
        layout.Controls.Add(_dbList);
        layout.Controls.Add(new Label());
        layout.Controls.Add(Row(
            _openButton,
            Ui.MakeButton("Видалити", async (_, _) => await DeleteAsync()),
            Ui.MakeButton("Імпорт з файлу…", async (_, _) => await ImportAsync())));
        layout.Controls.Add(new Label { Text = "Нова база:", AutoSize = true, Anchor = AnchorStyles.Left });
        _createButton = Ui.MakeButton("Створити", async (_, _) => await CreateAsync());
        layout.Controls.Add(Row(_newDbBox, _createButton));
        Controls.Add(layout);

        AcceptButton = _openButton;
        _newDbBox.Enter += (_, _) => AcceptButton = _createButton;
        _newDbBox.Leave += (_, _) => AcceptButton = _openButton;
        UpdateButtons();
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        panel.Controls.AddRange(controls);
        return panel;
    }

    private void UpdateButtons() => _openButton.Enabled = Client is not null;

    private async Task ConnectAsync()
    {
        var url = _serverBox.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            MessageBox.Show(this, "Вкажіть адресу у форматі http://host:port", Ui.AppTitle);
            return;
        }

        Client?.Dispose();
        Client = null;
        _settings.ServerUrl = url;
        _settings.ApiKey = _keyBox.Text;
        _settings.UseGrpcWeb = _grpcWebBox.Checked;
        var client = new GrpcDatabaseClient(_settings);
        var ok = await Ui.RunAsync(this, async () =>
        {
            await client.ListDatabasesAsync();
            Client = client;
        });
        if (!ok)
        {
            client.Dispose();
            _statusLabel.Text = "Не підключено";
            _dbList.Items.Clear();
        }
        else
        {
            _statusLabel.Text = "Підключено";
            await RefreshListAsync();
        }
        UpdateButtons();
    }

    private async Task RefreshListAsync(string? select = null)
    {
        if (Client is null)
            return;
        await Ui.RunAsync(this, async () =>
        {
            var listing = await Client.ListDatabasesAsync();
            _databases = listing.Databases.ToList();
            _dbList.Items.Clear();
            foreach (var db in _databases)
                _dbList.Items.Add(db.IsModified ? $"{db.Name}  (є незбережені зміни)" : db.Name);
            var index = _databases.FindIndex(d => string.Equals(d.Name, select, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                _dbList.SelectedIndex = index;
        });
    }

    private string? SelectedName() =>
        _dbList.SelectedIndex >= 0 && _dbList.SelectedIndex < _databases.Count
            ? _databases[_dbList.SelectedIndex].Name
            : null;

    private bool EnsureConnected()
    {
        if (Client is not null)
            return true;
        MessageBox.Show(this, "Спочатку підключіться до сервера", Ui.AppTitle);
        return false;
    }

    private async Task OpenAsync()
    {
        if (!EnsureConnected())
            return;
        var name = SelectedName();
        if (name is null)
        {
            MessageBox.Show(this, "Оберіть базу зі списку", Ui.AppTitle);
            return;
        }
        var ok = await Ui.RunAsync(this, async () => await Client!.ListTablesAsync(name));
        if (!ok)
            return;
        DatabaseName = name;
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task CreateAsync()
    {
        if (!EnsureConnected())
            return;
        var name = _newDbBox.Text.Trim();
        if (await Ui.RunAsync(this, () => Client!.CreateDatabaseAsync(name)))
        {
            _newDbBox.Clear();
            await RefreshListAsync(name);
        }
    }

    private async Task DeleteAsync()
    {
        if (!EnsureConnected())
            return;
        var name = SelectedName();
        if (name is null || !Ui.Confirm(this, $"Видалити базу '{name}' разом з файлом на сервері?"))
            return;
        if (await Ui.RunAsync(this, () => Client!.DeleteDatabaseAsync(name)))
            await RefreshListAsync();
    }

    private async Task ImportAsync()
    {
        if (!EnsureConnected())
            return;
        using var dialog = new OpenFileDialog { Filter = "База TabularDb (*.tdb.json)|*.tdb.json|JSON (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var fileName = Path.GetFileName(dialog.FileName);
        var name = fileName.EndsWith(".tdb.json", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^".tdb.json".Length]
            : Path.GetFileNameWithoutExtension(fileName);
        string content;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 60 * 1024 * 1024)
            {
                MessageBox.Show(this, "Файл завеликий (понад 60 МБ)", Ui.AppTitle);
                return;
            }
            content = await File.ReadAllTextAsync(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Не вдалося прочитати файл: {ex.Message}", Ui.AppTitle);
            return;
        }
        var overwrite = _databases.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        if (overwrite && !Ui.Confirm(this, $"База '{name}' вже існує. Замінити її даними з файлу?"))
            return;
        if (await Ui.RunAsync(this, () => Client!.ImportDatabaseAsync(name, content, overwrite)))
            await RefreshListAsync(name);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (DialogResult != DialogResult.OK)
            Client?.Dispose();
        base.OnFormClosed(e);
    }
}
