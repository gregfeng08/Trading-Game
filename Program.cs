using TradingGame.Data;
using TradingGame.Endpoints;
using TradingGame.Services;

var builder = WebApplication.CreateBuilder(args);

var contentRoot = builder.Environment.ContentRootPath;
var dbPath = Path.Combine(contentRoot, "Data", "database.db");
var schemaPath = Path.Combine(contentRoot, "Data", "schema.sql");
var knowledgeGraphPath = Path.Combine(contentRoot, "Data", "knowledge_graph.json");
var historicalEventsPath = Path.Combine(contentRoot, "Data", "historical_events.json");
var arcsPath = Path.Combine(contentRoot, "Data", "arcs.json");
var newspaperTemplatePath = Path.Combine(contentRoot, "Data", "newspaper_template.html");
var newspaperCachePath = Path.Combine(contentRoot, "Data", "newspaper_images");

var database = new Database(dbPath, schemaPath);
builder.Services.AddSingleton(database);

builder.Services.AddSingleton<EntityService>();
builder.Services.AddSingleton<GameStateService>();
builder.Services.AddSingleton<TradingService>();
builder.Services.AddSingleton<MarketDataService>();
builder.Services.AddSingleton(sp => new KnowledgeGraphService(sp.GetRequiredService<Database>(), knowledgeGraphPath));
if (File.Exists(historicalEventsPath))
{
    builder.Services.AddSingleton(sp => new NewspaperService(
        sp.GetRequiredService<Database>(),
        sp.GetRequiredService<GameStateService>(),
        historicalEventsPath,
        sp.GetService<ArcService>()));

    if (File.Exists(newspaperTemplatePath))
    {
        builder.Services.AddSingleton(sp => new NewspaperRendererService(
            sp.GetRequiredService<NewspaperService>(),
            newspaperTemplatePath,
            newspaperCachePath,
            sp.GetService<ArcService>()));
    }
}
else
{
    Console.WriteLine($"[WARNING] historical_events.json not found at {historicalEventsPath}. Newspaper endpoint disabled.");
}
builder.Services.AddSingleton(sp => new OrderService(
    sp.GetRequiredService<Database>(),
    sp.GetRequiredService<EntityService>(),
    sp.GetRequiredService<GameStateService>(),
    sp.GetRequiredService<KnowledgeGraphService>()));
if (File.Exists(arcsPath))
{
    builder.Services.AddSingleton(sp => new ArcService(
        sp.GetRequiredService<Database>(),
        sp.GetRequiredService<EntityService>(),
        sp.GetRequiredService<GameStateService>(),
        arcsPath));
}
else
{
    Console.WriteLine($"[WARNING] arcs.json not found at {arcsPath}. Arc system disabled.");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://127.0.0.1", "http://localhost")
              .AllowAnyMethod().AllowAnyHeader());
});

builder.WebHost.UseUrls("http://127.0.0.1:5000");

var app = builder.Build();

app.UseCors();

SystemEndpoints.Map(app);
MarketEndpoints.Map(app);
EntityEndpoints.Map(app);
TradeEndpoints.Map(app);
OrderEndpoints.Map(app);
SaveStateEndpoints.Map(app);
GameEndpoints.Map(app);
NewspaperEndpoints.Map(app);
ArcEndpoints.Map(app);
KnowledgeGraphEndpoints.Map(app);

app.Run();
