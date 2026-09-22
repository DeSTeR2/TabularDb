using Microsoft.AspNetCore.Diagnostics;
using Scalar.AspNetCore;
using TabularDb.Server.Application;
using TabularDb.Storage;
using TabularDb.WebApi.Components;
using TabularDb.WebApi.Infrastructure;

const long MaxUploadSize = 64 * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxUploadSize + 1024 * 1024);

var storageOptions = builder.Configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<IStorage, JsonStorage>();
builder.Services.AddSingleton<DatabaseStore>();
builder.Services.AddSingleton<DatabaseAppService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.Use((context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && context.Features.Get<IStatusCodePagesFeature>() is { } feature)
        feature.Enabled = false;
    return next(context);
});
app.UseAntiforgery();
app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(TabularDb.Web._Imports).Assembly);

app.Run();

public partial class Program;
