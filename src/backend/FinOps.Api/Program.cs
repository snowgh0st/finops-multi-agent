using FinOps.Api.Hubs;
using FinOps.Api.Workers;
using FinOps.Infrastructure.Cache;
using FinOps.Infrastructure.Services;
using FinOps.Infrastructure.Evaluators;
using Microsoft.SemanticKernel;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure CORS for Angular Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR WebSockets & HTTP Negotiation
    });
});

// 2. Configure SignalR with explicit camelCase JSON serialization
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = 
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 3. Configure Redis Connection Multiplexer & Feature Store
// Connects to local Redis instance (defaulting to localhost:6379 if connection string is omitted in appsettings.json)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    string redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

// Register the Feature Store for real-time velocity and volume aggregation tracking
builder.Services.AddScoped<TransactionFeatureStore>();

// 4. Register ONNX Model Evaluator as Singleton
builder.Services.AddSingleton<FraudModelEvaluator>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    string modelPath = Path.Combine(env.ContentRootPath, "Models", "fraud_model.onnx");
    
    return new FraudModelEvaluator(modelPath);
});

// 5. Register Semantic Kernel Services
builder.Services.AddTransient<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    string apiKey = builder.Configuration["OpenAI:ApiKey"] ?? "";
    string modelId = builder.Configuration["OpenAI:ModelId"] ?? "gpt-4o";

    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
    };
    var customHttpClient = new HttpClient(handler);

    kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey, httpClient: customHttpClient);
    return kernelBuilder.Build();
});

// 6. Register Application & Background Services
builder.Services.AddTransient<SarGeneratorService>();
builder.Services.AddHostedService<HighRiskTransactionListener>();

var app = builder.Build();

app.UseRouting();

// CORS must sit between UseRouting and endpoint mappings
app.UseCors("AllowAngular");

// Mapped route now matches Angular's http://localhost:5000/hubs/risk
app.MapHub<RiskHub>("/hubs/risk");

app.MapGet("/", () => "FinOps Risk Engine API Running");

app.Run();