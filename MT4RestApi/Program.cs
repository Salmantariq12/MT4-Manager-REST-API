using MT4RestApi.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/mt4api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MT4 REST API", Version = "v1" });
});

// Register MT4 Manager service as singleton
builder.Services.AddSingleton<IMT4ManagerService, MT4ManagerService>();

// Register FIX Protocol Price service to connect to MT4 price feed via FIX 4.3
// Replaces old SignalR implementation with FIX protocol
builder.Services.AddSingleton<IPriceWebSocketService, FixPriceService>();
builder.Services.AddHostedService(provider => (FixPriceService)provider.GetRequiredService<IPriceWebSocketService>());

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Enable Swagger in all environments for testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MT4 REST API v1");
});

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Get the URLs from configuration or environment
var urls = builder.Configuration["ASPNETCORE_URLS"] ??
           builder.Configuration["Kestrel:Endpoints:Http:Url"] ??
           "http://*:5000";

Log.Information("MT4 REST API Server starting on {Urls}...", urls);
app.Run();

Log.CloseAndFlush();