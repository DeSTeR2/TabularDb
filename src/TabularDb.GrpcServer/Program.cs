using TabularDb.GrpcServer.Infrastructure;
using TabularDb.GrpcServer.Services;
using TabularDb.Server.Application;
using TabularDb.Storage;

const int MaxMessageSize = 64 * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxMessageSize + 1024 * 1024);

var storageOptions = builder.Configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<IStorage, JsonStorage>();
builder.Services.AddSingleton<DatabaseStore>();
builder.Services.AddSingleton<DatabaseAppService>();
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.Section));

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<ApiKeyInterceptor>();
    options.Interceptors.Add<ExceptionInterceptor>();
    options.MaxReceiveMessageSize = MaxMessageSize;
});
if (builder.Environment.IsDevelopment())
    builder.Services.AddGrpcReflection();

var app = builder.Build();

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
app.MapGrpcService<TabularGrpcService>();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();
app.MapGet("/", () => "TabularDb gRPC server");

app.Run();

public partial class Program;
