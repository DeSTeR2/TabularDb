namespace TabularDb.Desktop.Services;

public sealed class ClientSettings
{
    public const string Section = "Client";

    public string ServerUrl { get; set; } = "http://localhost:5100";
    public bool UseGrpcWeb { get; set; }
    public string? ApiKey { get; set; }
}
