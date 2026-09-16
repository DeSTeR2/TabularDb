using TabularDb.Core.Types;
using TabularDb.Core.Validation;
using TabularDb.Desktop.Services;

namespace TabularDb.Desktop.Forms;

public sealed class CreateTableForm : Form
{
    private readonly IDatabaseClient _client;
    private readonly string _db;
    private readonly TextBox _nameBox = new() { Width = 300 };
    private readonly DataGridView _fields = new()
    {
        Width = 420,
        Height = 220,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
    };
    private readonly ListBox _errorList = new() { Width = 420, Height = 80, IntegralHeight = false };

    public string? CreatedTable { get; private set; }

    public CreateTableForm(IDatabaseClient client, string db, IReadOnlyList<string> types)
    {
        _client = client;
        _db = db;
        Text = "Нова таблиця";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        _fields.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Назва поля" });
        var typeColumn = new DataGridViewComboBoxColumn { Name = "type", HeaderText = "Тип", FlatStyle = FlatStyle.Standard };
        typeColumn.Items.AddRange([.. types.Count > 0 ? types : TypeRegistry.Names]);
        _fields.Columns.Add(typeColumn);
        _fields.DataError += (_, e) => e.ThrowException = false;

        var removeField = Ui.MakeButton("Видалити поле", (_, _) => RemoveCurrentField());
        var ok = Ui.MakeButton("Створити", async (_, _) => await SubmitAsync());
        var cancel = new Button { Text = "Скасувати", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.AddRange([cancel, ok]);

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        layout.Controls.AddRange([
            new Label { Text = "Назва таблиці:", AutoSize = true },
            _nameBox,
            new Label { Text = "Поля (кожен рядок — одне поле):", AutoSize = true },
            _fields,
            removeField,
            new Label { Text = "Помилки:", AutoSize = true },
            _errorList,
            buttons,
        ]);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void RemoveCurrentField()
    {
        var row = _fields.CurrentRow;
        if (row is null || row.IsNewRow)
            return;
        _fields.CancelEdit();
        _fields.Rows.RemoveAt(row.Index);
    }

    private List<(string Name, string? Type)> ReadFields()
    {
        _fields.EndEdit();
        var result = new List<(string, string?)>();
        foreach (DataGridViewRow row in _fields.Rows)
        {
            if (row.IsNewRow)
                continue;
            var name = (row.Cells["name"].Value as string ?? "").Trim();
            var type = row.Cells["type"].Value as string;
            if (name.Length == 0 && type is null)
                continue;
            result.Add((name, type));
        }
        return result;
    }

    private void ShowErrors(IEnumerable<string> errors)
    {
        _errorList.Items.Clear();
        foreach (var error in errors)
            _errorList.Items.Add(error);
    }

    private async Task SubmitAsync()
    {
        var name = _nameBox.Text.Trim();
        var raw = ReadFields();

        var errors = SchemaValidator
            .Validate(null, name, raw.Select(f => new FieldDefinition(f.Name, f.Type)).ToList())
            .Errors;
        ShowErrors(errors);
        if (errors.Count > 0)
            return;

        ClientException? failure = null;
        var ok = await Ui.RunAsync(this, async () =>
        {
            try
            {
                await _client.CreateTableAsync(_db, name, raw.Select(f => new FieldItem(f.Name, f.Type!)).ToList());
            }
            catch (ClientException ex) when (!ex.IsUnavailable)
            {
                failure = ex;
            }
        });
        if (!ok)
            return;
        if (failure is not null)
        {
            ShowErrors(failure.Errors.Count > 0 ? failure.Errors : [failure.Message]);
            return;
        }
        CreatedTable = name;
        DialogResult = DialogResult.OK;
        Close();
    }
}
