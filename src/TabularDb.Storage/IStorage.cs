using TabularDb.Core.Model;

namespace TabularDb.Storage;

public interface IStorage
{
    void Save(Database database, string path);
    Database Load(string path, string? nameOverride = null);
}
