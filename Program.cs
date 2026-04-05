using TradingGame.Data;
using TradingGame.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Resolve paths relative to the project directory (not CWD)
var contentRoot = builder.Environment.ContentRootPath;
var dbPath = Path.Combine(contentRoot, "Data", "database.db");
var schemaPath = Path.Combine(contentRoot, "Data", "schema.sql");

// Register the database as a singleton service
builder.Services.AddSingleton(new Database(dbPath, schemaPath));

// CORS — allow Unity or any local client
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Listen on port 5000 to match Unity's default BootstrapConfig
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();

app.UseCors();

// Map all endpoint groups
SystemEndpoints.Map(app);
MarketEndpoints.Map(app);
EntityEndpoints.Map(app);
TradeEndpoints.Map(app);
SaveStateEndpoints.Map(app);
GameEndpoints.Map(app);

app.Run();
