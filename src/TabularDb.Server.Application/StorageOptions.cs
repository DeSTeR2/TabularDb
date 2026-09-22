namespace TabularDb.Server.Application;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    public string DataDir { get; set; } = "data";
}
