using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TabularDb.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<TabularApiClient>();

await builder.Build().RunAsync();
