using TabularDb.Core.Exceptions;
using TabularDb.Core.Model;
using TabularDb.Storage;

namespace TabularDb.Server.Application;

public sealed class DatabaseStore
{
    private readonly Dictionary<string, DbEntry> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();
    private readonly IStorage _storage;

    public string DataDir { get; }

    public DatabaseStore(StorageOptions options, IStorage storage)
    {
        _storage = storage;
        DataDir = Path.GetFullPath(options.DataDir);
        Directory.CreateDirectory(DataDir);
    }

    public sealed class DbEntry(Database database)
    {
        public Database Database { get; set; } = database;
        public Lock Sync { get; } = new();
        public bool IsRemoved { get; set; }
    }

    public static void EnsureAlive(DbEntry entry)
    {
        if (entry.IsRemoved)
            throw new NotFoundException($"Базу '{entry.Database.Name}' вже видалено або замінено");
    }

    public string PathFor(string name) => Path.Combine(DataDir, name + JsonStorage.Extension);

    public IReadOnlyList<DatabaseSummary> List()
    {
        lock (_gate)
        {
            var result = new Dictionary<string, DatabaseSummary>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in FileNames())
                result[name] = new DatabaseSummary(name, false, false);
            foreach (var entry in _loaded.Values)
                result[entry.Database.Name] = new DatabaseSummary(entry.Database.Name, true, entry.Database.IsModified);
            return result.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    public DbEntry Get(string name)
    {
        name = DbNameValidator.Normalize(name);
        lock (_gate)
        {
            if (_loaded.TryGetValue(name, out var entry))
                return entry;
            var fileName = FindFile(name)
                ?? throw new NotFoundException($"Базу '{name}' не знайдено");
            entry = new DbEntry(_storage.Load(PathFor(fileName), fileName));
            _loaded[fileName] = entry;
            return entry;
        }
    }

    public void Create(string name)
    {
        name = DbNameValidator.Normalize(name);
        lock (_gate)
        {
            EnsureAbsent(name);
            var database = new Database(name);
            database.MarkModified();
            _loaded[name] = new DbEntry(database);
        }
    }

    public void Delete(string name)
    {
        name = DbNameValidator.Normalize(name);
        lock (_gate)
        {
            _loaded.TryGetValue(name, out var entry);
            var fileName = FindFile(name);
            if (entry is null && fileName is null)
                throw new NotFoundException($"Базу '{name}' не знайдено");

            if (entry is null)
            {
                DeleteFile(fileName!);
                return;
            }

            lock (entry.Sync)
            {
                if (fileName is not null)
                    DeleteFile(fileName);
                entry.IsRemoved = true;
                _loaded.Remove(name);
            }
        }
    }

    public void Save(string name)
    {
        var entry = Get(name);
        lock (entry.Sync)
        {
            EnsureAlive(entry);
            _storage.Save(entry.Database, PathFor(entry.Database.Name));
        }
    }

    public void Reload(string name)
    {
        var entry = Get(name);
        lock (entry.Sync)
        {
            EnsureAlive(entry);
            var path = PathFor(entry.Database.Name);
            if (!File.Exists(path))
                throw new NotFoundException($"Базу '{entry.Database.Name}' ще не збережено на диск");
            entry.Database = _storage.Load(path, entry.Database.Name);
        }
    }

    public void Import(string name, string json, bool overwrite)
    {
        name = DbNameValidator.Normalize(name);
        var database = DatabaseSerializer.FromJson(json, name);
        lock (_gate)
        {
            if (!overwrite)
                EnsureAbsent(name);

            _loaded.TryGetValue(name, out var old);
            var existing = FindFile(name);
            if (old is null)
            {
                Replace(existing, name, database);
                return;
            }
            lock (old.Sync)
            {
                Replace(existing, name, database);
                old.IsRemoved = true;
            }
        }
    }

    private void Replace(string? existingFile, string name, Database database)
    {
        if (existingFile is not null && existingFile != name)
            DeleteFile(existingFile);
        _storage.Save(database, PathFor(name));
        _loaded[name] = new DbEntry(database);
    }

    private void DeleteFile(string fileName)
    {
        try
        {
            File.Delete(PathFor(fileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw StorageException.Io($"Не вдалося видалити файл бази '{fileName}'", ex);
        }
    }

    private void EnsureAbsent(string name)
    {
        if (_loaded.ContainsKey(name) || FindFile(name) is not null)
            throw new AlreadyExistsException($"База '{name}' вже існує");
    }

    private string? FindFile(string name) =>
        FileNames().FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<string> FileNames() =>
        Directory.EnumerateFiles(DataDir, "*" + JsonStorage.Extension)
            .Select(p => Path.GetFileName(p)[..^JsonStorage.Extension.Length])
            .Where(DbNameValidator.IsValid);
}
