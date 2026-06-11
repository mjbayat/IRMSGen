using IRMSGen.App.Components;
using IRMSGen.App.Services;
using IRMSGen.Infrastructure.Persistence;


// Add services to the container.
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<VisualGenerationService>();
builder.Services.Configure<PlatformDbOptions>(builder.Configuration.GetSection("PlatformDatabase"));
builder.Services.AddScoped<PlatformDbInitializer>();
builder.Services.AddScoped<PlatformWorkspaceStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await scope.ServiceProvider
            .GetRequiredService<PlatformDbInitializer>()
            .InitializeAsync();
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "IRMSGen platform database initialization failed. The UI will run with in-memory fallback data.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
