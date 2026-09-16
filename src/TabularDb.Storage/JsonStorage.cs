using TabularDb.Core.Model;

namespace TabularDb.Storage;

public sealed class JsonStorage : IStorage
{
    public const string Extension = ".tdb.json";

    public void Save(Database database, string path)
    {
        var json = DatabaseSerializer.ToJson(database);
        var tmp = path + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Не вдалося записати файл '{path}': {ex.Message}", ex);
        }
        database.MarkSaved();
    }

    public Database Load(string path, string? nameOverride = null)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Не вдалося прочитати файл '{path}': {ex.Message}", ex);
        }
        return DatabaseSerializer.FromJson(json, nameOverride);
    }
}
