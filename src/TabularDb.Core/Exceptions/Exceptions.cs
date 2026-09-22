namespace TabularDb.Core.Exceptions;

public class TabularDbException : Exception
{
    public TabularDbException(string message) : base(message) { }
    public TabularDbException(string message, Exception inner) : base(message, inner) { }
}

public sealed class ValidationException : TabularDbException
{
    public IReadOnlyList<string> Errors { get; }

    public ValidationException(IEnumerable<string> errors)
        : this(errors.ToList()) { }

    private ValidationException(List<string> errors)
        : base(errors.Count == 0 ? "Помилка валідації" : string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public ValidationException(string error) : this(new List<string> { error }) { }
}

public sealed class NotFoundException(string message) : TabularDbException(message);

public sealed class AlreadyExistsException(string message) : TabularDbException(message);
