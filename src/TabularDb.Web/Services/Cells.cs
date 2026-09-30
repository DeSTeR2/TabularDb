namespace TabularDb.Web.Services;

public static class Cells
{
    public static string? Align(string? type) =>
        string.Equals(type, "integer", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "real", StringComparison.OrdinalIgnoreCase)
            ? "num"
            : null;
}
