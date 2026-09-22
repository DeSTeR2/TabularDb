using TabularDb.Core.Exceptions;

namespace TabularDb.Core.Validation;

public sealed class ValidationResult
{
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Errors => _errors;
    public bool IsValid => _errors.Count == 0;

    public void AddError(string message) => _errors.Add(message);

    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new ValidationException(_errors);
    }
}
