using TabularDb.Core.Types;
using TabularDb.Desktop.Services;

namespace TabularDb.Desktop.Forms;

public sealed class RowEditForm : Form
{
    private readonly TableSchema _schema;
    private readonly Func<IReadOnlyList<string?>, Task<RowItem>> _submit;
    private readonly List<TextBox> _inputs = [];
    private readonly ErrorProvider _errors = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public RowEditForm(TableSchema schema, RowItem? row, Func<IReadOnlyList<string?>, Task<RowItem>> submit)
    {
        _schema = schema;
        _submit = submit;
        Text = row is null ? $"Новий рядок — {schema.Name}" : $"Рядок {row.Id} — {schema.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        var layout = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        for (var i = 0; i < schema.Fields.Count; i++)
        {
            var field = schema.Fields[i];
            var hint = TypeRegistry.TryGet(field.Type, out var type) ? type.FormatHint : "";
            var input = new TextBox { Width = 240, Text = row?.Values.ElementAtOrDefault(i) ?? "" };
            _inputs.Add(input);
            layout.Controls.Add(new Label { Text = $"{field.Name} ({field.Type}):", AutoSize = true, Anchor = AnchorStyles.Left });
            layout.Controls.Add(input);
            layout.Controls.Add(new Label { Text = hint, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(16, 0, 0, 0) });
        }

        var ok = Ui.MakeButton("Зберегти", async (_, _) => await SubmitAsync());
        var cancel = new Button { Text = "Скасувати", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.AddRange([cancel, ok]);
        layout.Controls.Add(new Label { Text = "Порожнє поле означає відсутнє значення.", AutoSize = true }, 0, schema.Fields.Count);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, schema.Fields.Count)!, 3);
        layout.Controls.Add(buttons, 0, schema.Fields.Count + 1);
        layout.SetColumnSpan(buttons, 3);
        Controls.Add(layout);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    private string?[] CollectValues() =>
        _inputs.Select(t => string.IsNullOrWhiteSpace(t.Text) ? null : t.Text).ToArray();

    private bool ValidateLocally(string?[] values)
    {
        var valid = true;
        for (var i = 0; i < _schema.Fields.Count; i++)
        {
            var message = "";
            if (TypeRegistry.TryGet(_schema.Fields[i].Type, out var type)
                && !type.TryParse(values[i], out _, out var error))
            {
                message = error ?? "некоректне значення";
                valid = false;
            }
            _errors.SetError(_inputs[i], message);
        }
        return valid;
    }

    private async Task SubmitAsync()
    {
        var values = CollectValues();
        if (!ValidateLocally(values))
            return;

        ClientException? failure = null;
        var ok = await Ui.RunAsync(this, async () =>
        {
            try
            {
                await _submit(values);
            }
            catch (ClientException ex) when (!ex.IsUnavailable && ex.Errors.Count > 0)
            {
                failure = ex;
            }
        });
        if (!ok)
            return;
        if (failure is not null)
        {
            ShowServerErrors(failure);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowServerErrors(ClientException ex)
    {
        var unmatched = new List<string>();
        foreach (var error in ex.Errors)
        {
            var index = -1;
            for (var i = 0; i < _schema.Fields.Count; i++)
                if (error.StartsWith($"Поле '{_schema.Fields[i].Name}'", StringComparison.OrdinalIgnoreCase))
                    index = i;
            if (index >= 0)
                _errors.SetError(_inputs[index], error);
            else
                unmatched.Add(error);
        }
        if (unmatched.Count > 0)
            Ui.ShowError(this, new ClientException(ex.Message, unmatched));
    }
}
