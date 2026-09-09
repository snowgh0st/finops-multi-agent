using FinOps.Api;

var builder = WebApplication.CreateBuilder(args);

// 1. Register SignalR & Background Simulator
builder.Services.AddSignalR();
builder.Services.AddHostedService<TransactionSimulator>();
builder.Services.AddScoped<SarGeneratorService>();
builder.Services.AddHostedService<HighRiskTransactionListener>();

// 2. Configure CORS for Angular (http://localhost:4200)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 3. Enable CORS Middleware
app.UseCors("AllowAngular");

// 4. Map Endpoints
app.MapHub<RiskHub>("/hubs/risk");
app.MapGet("/", () => "FinOps Multi-Agent API is Running");

app.Run();