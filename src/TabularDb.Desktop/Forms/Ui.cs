using TabularDb.Desktop.Services;

namespace TabularDb.Desktop.Forms;

internal static class Ui
{
    public const string AppTitle = "TabularDb";

    public static async Task<bool> RunAsync(Control owner, Func<Task> action)
    {
        var form = owner.FindForm() ?? owner;
        var wasEnabled = form.Enabled;
        form.UseWaitCursor = true;
        form.Enabled = false;
        try
        {
            while (true)
            {
                try
                {
                    await action();
                    return true;
                }
                catch (ClientException ex) when (ex.IsUnavailable)
                {
                    var answer = MessageBox.Show(form, ex.Message + Environment.NewLine + "Спробувати ще раз?",
                        AppTitle, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                    if (answer != DialogResult.Retry)
                        return false;
                }
                catch (ClientException ex)
                {
                    ShowError(form, ex);
                    return false;
                }
            }
        }
        finally
        {
            form.Enabled = wasEnabled;
            form.UseWaitCursor = false;
        }
    }

    public static void ShowError(IWin32Window owner, ClientException ex)
    {
        var text = ex.Errors.Count == 0
            ? ex.Message
            : ex.Message + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, ex.Errors);
        MessageBox.Show(owner, text, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static void ShowInfo(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirm(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += onClick;
        return button;
    }
}
