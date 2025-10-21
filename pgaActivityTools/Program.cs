using FluentMigrator.Runner;
using Microsoft.EntityFrameworkCore;
using pgaActivityTools.Data;
using pgaActivityTools.Endpoints;
using pgaActivityTools.Services.DatabaseMigrator;
using pgaActivityTools.Services.DatabaseMigrator.Service;
using pgaActivityTools.Services.Strava;
using pgaActivityTools.Services.Strava.Service;
using pgaActivityTools.Services.Version;
using pgaActivityTools.Services.Version.Service;
using pgaActivityTools.Services.Weather;
using pgaActivityTools.Services.Weather.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuration Postgres
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configuration de FluentMigrator
builder.Services
    .AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(builder.Configuration.GetConnectionString("DefaultConnection"))
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());


// Ajout des services HTTP
builder.Services.AddSingleton<IDatabaseMigrator, DatabaseMigratorService>();
builder.Services.AddScoped<IVersion, VersionService>();
builder.Services.AddHttpClient<IStravaTokenRefresher, StravaTokenRefresherService>();
builder.Services.AddHttpClient<IWeather, WeatherService>();
builder.Services.AddHttpClient<IStravaService, StravaService>();
builder.Services.AddHttpClient<IStravaWebhook, StravaWebhookService>();

var app = builder.Build();

// Exécuter les migrations au démarrage
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    migrator.Migrate();
}


// Créer/Migrer la base de données au démarrage
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        db.Database.EnsureCreated();
        app.Logger.LogInformation("✅ Database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "❌ Error initializing database");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Désactiver la redirection HTTPS en développement
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapSystemEndpoints();
app.MapWebhookEndpoints();
app.MapAuthEndpoints();

app.Run();