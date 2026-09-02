using System.Globalization;
using HarryPotterExplorer.Data;
using HarryPotterExplorer.Hubs;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// The interface is English throughout, so formatting must not follow the host machine's
// locale - otherwise a wand is "10,75 inches" on a Russian box and "10.75" on an English one.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Most PaaS hosts (Render, Railway, Fly, Heroku) inject the port to listen on rather than
// letting the app pick one. Honour it when present so the same build runs locally and there.
var assignedPort = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(assignedPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{assignedPort}");
}

builder.Services.Configure<HarryPotterApiOptions>(
    builder.Configuration.GetSection(HarryPotterApiOptions.SectionName));

var apiOptions = builder.Configuration.GetSection(HarryPotterApiOptions.SectionName)
                     .Get<HarryPotterApiOptions>() ?? new HarryPotterApiOptions();

// ---------------------------------------------------------------------------
// Storage. SQLite keeps the project clone-and-run: no container, no credentials.
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Hogwarts")
                       ?? "Data Source=App_Data/hogwarts.db";

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

builder.Services.AddDbContext<HogwartsContext>(options =>
    options.UseSqlite(connectionString));

// ---------------------------------------------------------------------------
// The only outbound HTTP client in the app. Registered as a typed client so that
// retries, timeouts and the base address are configured in exactly one place.
// ---------------------------------------------------------------------------
builder.Services.AddHttpClient<IHarryPotterApiClient, HarryPotterApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiOptions.BaseAddress);
        client.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HarryPotterExplorer/1.0 (+github)");
    })
    .AddStandardResilienceHandler(resilience =>
    {
        // The upstream is a free instance that sleeps. Give a cold start room to wake up
        // instead of failing the whole sync on the first slow response.
        resilience.Retry.MaxRetryAttempts = apiOptions.RetryCount;
        resilience.Retry.Delay = TimeSpan.FromSeconds(3);
        resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds / 2.0);
        resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
        resilience.CircuitBreaker.SamplingDuration =
            TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
    });

builder.Services.AddSingleton<IHouseCatalog, HouseCatalog>();
builder.Services.AddSingleton<ISortingHatService, SortingHatService>();
builder.Services.AddSingleton<ICatalogSyncCoordinator, CatalogSyncCoordinator>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddHostedService<CatalogSyncBackgroundService>();

builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Registered in every environment so an unknown address gets the castle's own 404 page
// rather than an empty response.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

app.UseResponseCompression();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<GreatHallHub>("/hubs/great-hall");

app.Run();
