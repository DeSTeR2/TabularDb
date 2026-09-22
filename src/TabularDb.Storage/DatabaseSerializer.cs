using System.Text.Encodings.Web;
using System.Text.Json;
using TabularDb.Core.Exceptions;
using TabularDb.Core.Model;
using TabularDb.Core.Types;
using TabularDb.Core.Validation;

namespace TabularDb.Storage;

public static class DatabaseSerializer
{
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson(Database database)
    {
        var file = new DatabaseFile
        {
            FormatVersion = CurrentFormatVersion,
            Name = database.Name,
            Tables = database.Tables.Select(t => new TableFile
            {
                Name = t.Name,
                NextRowId = t.NextRowId,
                Fields = t.Schema.Fields.Select(f => new FieldFile { Name = f.Name, Type = f.Type.Name }).ToList(),
                Rows = t.Rows.Select(r => new RowFile
                {
                    Id = r.Id,
                    Values = RowValidator.Format(t.Schema, r.Values).ToList(),
                }).ToList(),
            }).ToList(),
        };
        return JsonSerializer.Serialize(file, Options);
    }

    public static Database FromJson(string json, string? nameOverride = null)
    {
        DatabaseFile? file;
        try
        {
            file = JsonSerializer.Deserialize<DatabaseFile>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new StorageException("Файл бази пошкоджено: некоректний JSON", ex);
        }

        if (file is null)
            throw new StorageException("Файл бази порожній");
        if (file.FormatVersion != CurrentFormatVersion)
            throw new StorageException($"Непідтримувана версія формату: {file.FormatVersion}");

        var name = nameOverride ?? file.Name;
        if (string.IsNullOrWhiteSpace(name))
            throw new StorageException("У файлі не вказано назву бази");

        var database = new Database(name);
        try
        {
            foreach (var tableFile in file.Tables ?? [])
                database.AddTable(ReadTable(database, tableFile));
        }
        catch (TabularDbException ex) when (ex is not StorageException)
        {
            throw new StorageException($"Файл бази пошкоджено: {ex.Message}", ex);
        }
        return database;
    }

    private static Table ReadTable(Database database, TableFile tableFile)
    {
        var fields = new List<Field>();
        foreach (var f in tableFile.Fields ?? [])
        {
            if (!TypeRegistry.TryGet(f.Type, out var type))
                throw new StorageException($"Таблиця '{tableFile.Name}': невідомий тип '{f.Type}'");
            fields.Add(new Field(f.Name?.Trim() ?? "", type));
        }
        SchemaValidator.Validate(database, tableFile.Name, fields).ThrowIfInvalid();

        var table = new Table(tableFile.Name!.Trim(), new Schema(fields));
        foreach (var rowFile in tableFile.Rows ?? [])
        {
            var values = RowValidator.Parse(table.Schema, rowFile.Values ?? []);
            table.RestoreRow(rowFile.Id, values);
        }
        table.RestoreNextRowId(tableFile.NextRowId);
        return table;
    }
}
