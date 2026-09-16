namespace TabularDb.GrpcServer.Infrastructure;

public sealed class SecurityOptions
{
    public const string Section = "Security";
    public const string HeaderName = "x-api-key";

    public string? ApiKey { get; set; }
}
