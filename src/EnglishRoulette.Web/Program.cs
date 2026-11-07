using EnglishRoulette.Web;
using EnglishRoulette.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ =>
{
    var client = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    client.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
    return client;
});
builder.Services.AddScoped<AdventureClient>();
builder.Services.AddScoped<AdminClient>();

await builder.Build().RunAsync();
