using System.Diagnostics.CodeAnalysis;

namespace TabularDb.Core.Types;

public static class TypeRegistry
{
    private static readonly Dictionary<string, DataType> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [IntegerType.Instance.Name] = IntegerType.Instance,
        [RealType.Instance.Name] = RealType.Instance,
        [CharType.Instance.Name] = CharType.Instance,
        [StringType.Instance.Name] = StringType.Instance,
        [TimeType.Instance.Name] = TimeType.Instance,
        [TimeInvlType.Instance.Name] = TimeInvlType.Instance,
    };

    public static IReadOnlyList<string> Names { get; } =
        [IntegerType.Instance.Name, RealType.Instance.Name, CharType.Instance.Name,
         StringType.Instance.Name, TimeType.Instance.Name, TimeInvlType.Instance.Name];

    public static bool TryGet(string? name, [NotNullWhen(true)] out DataType? type)
    {
        type = null;
        return name is not null && Types.TryGetValue(name.Trim(), out type);
    }

    public static DataType Create(string name) =>
        TryGet(name, out var type) ? type : throw new ArgumentException($"Невідомий тип '{name}'", nameof(name));
}
