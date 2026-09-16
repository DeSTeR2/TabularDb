namespace TabularDb.Web.Services;

public static class Links
{
    private static string Esc(string value) => Uri.EscapeDataString(value);

    public static string Database(string db) => $"db/{Esc(db)}";
    public static string NewTable(string db) => $"{Database(db)}/new-table";
    public static string Table(string db, string table) => $"{Database(db)}/t/{Esc(table)}";
    public static string Dedup(string db, string? table = null) =>
        table is null ? $"{Database(db)}/dedup" : $"{Database(db)}/dedup?table={Esc(table)}";
}
