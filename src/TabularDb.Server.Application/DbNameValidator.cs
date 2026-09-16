using System.Text.RegularExpressions;
using TabularDb.Core.Exceptions;

namespace TabularDb.Server.Application;

public static partial class DbNameValidator
{
    [GeneratedRegex(@"^[\p{L}\p{N}_-]{1,64}$")]
    private static partial Regex NamePattern();

    public static bool IsValid(string? name) => name is not null && NamePattern().IsMatch(name);

    public static string Normalize(string? name)
    {
        var trimmed = name?.Trim();
        if (!IsValid(trimmed))
            throw new ValidationException(
                "Назва бази може містити лише літери, цифри, '_' та '-' (від 1 до 64 символів)");
        return trimmed!;
    }
}
