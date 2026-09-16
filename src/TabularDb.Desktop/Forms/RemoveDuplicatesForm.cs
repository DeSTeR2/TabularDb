using TabularDb.Desktop.Controllers;
using TabularDb.Desktop.Services;

namespace TabularDb.Desktop.Forms;

public sealed class RemoveDuplicatesForm : Form
{
    private readonly RemoveDuplicatesController _controller;
    private readonly ComboBox _tableBox = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _preview = new()
    {
        Width = 560,
        Height = 260,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
    };
    private readonly Label _resultLabel = new() { AutoSize = true, Text = "Оберіть таблицю і натисніть «Переглянути»." };
    private readonly Button _removeButton;

    public bool Changed { get; private set; }

    public RemoveDuplicatesForm(RemoveDuplicatesController controller, string? initialTable)
    {
        _controller = controller;
        Text = "Вилучення повторюваних рядків";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        _removeButton = Ui.MakeButton("Вилучити", async (_, _) => await RemoveAsync());
        _removeButton.Enabled = false;
        _tableBox.SelectedIndexChanged += (_, _) =>
        {
            _removeButton.Enabled = false;
            _preview.Columns.Clear();
            _resultLabel.Text = "Натисніть «Переглянути», щоб побачити рядки, які буде вилучено.";
        };

        var top = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        top.Controls.AddRange([
            new Label { Text = "Таблиця:", AutoSize = true, Anchor = AnchorStyles.Left },
            _tableBox,
            Ui.MakeButton("Переглянути", async (_, _) => await PreviewAsync()),
            _removeButton,
        ]);

        var close = new Button { Text = "Закрити", AutoSize = true, DialogResult = DialogResult.Cancel };
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        layout.Controls.AddRange([
            top,
            new Label { Text = "Рядки, які буде вилучено (лишається перше входження):", AutoSize = true },
            _preview,
            _resultLabel,
            close,
        ]);
        Controls.Add(layout);
        CancelButton = close;

        Load += async (_, _) => await LoadTablesAsync(initialTable);
    }

    private string? SelectedTable => _tableBox.SelectedItem as string;

    private async Task LoadTablesAsync(string? initialTable)
    {
        await Ui.RunAsync(this, async () =>
        {
            var names = await _controller.GetTableNamesAsync();
            _tableBox.Items.Clear();
            _tableBox.Items.AddRange([.. names.Cast<object>()]);
            var index = initialTable is null ? -1 : _tableBox.Items.IndexOf(initialTable);
            if (index >= 0)
                _tableBox.SelectedIndex = index;
            else if (_tableBox.Items.Count > 0)
                _tableBox.SelectedIndex = 0;
        });
    }

    private async Task PreviewAsync()
    {
        var table = SelectedTable;
        if (table is null)
            return;
        await Ui.RunAsync(this, async () =>
        {
            var schema = await _controller.GetSchemaAsync(table);
            var outcome = await _controller.PreviewAsync(table);
            FillPreview(schema, outcome.RemovedRows);
            _removeButton.Enabled = outcome.RemovedCount > 0;
            _resultLabel.Text = outcome.RemovedCount == 0
                ? "Повторюваних рядків немає."
                : outcome.RemovedRows.Count < outcome.RemovedCount
                    ? $"Буде вилучено рядків: {outcome.RemovedCount} (показано перші {outcome.RemovedRows.Count})"
                    : $"Буде вилучено рядків: {outcome.RemovedCount}";
        });
    }

    private async Task RemoveAsync()
    {
        var table = SelectedTable;
        if (table is null || !Ui.Confirm(this, $"Вилучити повторювані рядки з таблиці '{table}'?"))
            return;
        await Ui.RunAsync(this, async () =>
        {
            var outcome = await _controller.RemoveDuplicatesAsync(table);
            Changed |= outcome.RemovedCount > 0;
            _preview.Rows.Clear();
            _removeButton.Enabled = false;
            _resultLabel.Text = $"Вилучено рядків: {outcome.RemovedCount}";
        });
    }

    private void FillPreview(TableSchema schema, IReadOnlyList<RowItem> rows)
    {
        _preview.Rows.Clear();
        _preview.Columns.Clear();
        _preview.Columns.Add("id", "id");
        foreach (var field in schema.Fields)
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = $"{field.Name} : {field.Type}" });
        foreach (var row in rows)
            _preview.Rows.Add([row.Id, .. row.Values.Select(v => (object)(v ?? ""))]);
    }
}
