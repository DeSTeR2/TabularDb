using TabularDb.Core.Exceptions;

namespace TabularDb.Storage;

public sealed class StorageException : TabularDbException
{
    public StorageException(string message) : base(message) { }
    public StorageException(string message, Exception inner) : base(message, inner) { }
}
