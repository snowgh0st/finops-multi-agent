using FinOps.Api.Hubs;
using FinOps.Api.Workers;
using FinOps.Core.Interfaces;
using FinOps.Infrastructure.Cache;
using FinOps.Infrastructure.Evaluators;
using FinOps.Infrastructure.Filters;
using FinOps.Infrastructure.Persistence;
using FinOps.Infrastructure.Services;
using FinOps.Infrastructure.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

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

// 3. Configure Redis Connection Multiplexer & Feature Store Interface
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    string redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConnectionString);
});
builder.Services.AddScoped<TransactionFeatureStore>();
builder.Services.AddScoped<ITransactionFeatureStore>(sp => sp.GetRequiredService<TransactionFeatureStore>());

// 4. Configure SQLite for Compliance Audit Logging
string dbPath = Path.Combine(builder.Environment.ContentRootPath, "compliance_audit.db");
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// 5. Configure OpenTelemetry Tracing for Audit & Telemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("FinOps.RiskEngine.Api"))
            .AddSource("FinOps.Compliance.AuditTracing")
            .AddConsoleExporter();
    });

// 6. Register ONNX Model Evaluator as Singleton (< 1 ms local inference)
builder.Services.AddSingleton<FraudModelEvaluator>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    string modelPath = Path.Combine(env.ContentRootPath, "Models", "fraud_model.onnx");
    
    return new FraudModelEvaluator(modelPath);
});

// 7. Register Semantic Kernel & Wire the Audit Function Filter
builder.Services.AddSingleton<IAutoFunctionInvocationFilter, AuditFunctionFilter>();

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

    var auditFilter = sp.GetRequiredService<IAutoFunctionInvocationFilter>();
    kernelBuilder.Services.AddSingleton(auditFilter);

    var kernel = kernelBuilder.Build();
    kernel.AutoFunctionInvocationFilters.Add(auditFilter);

    return kernel;
});

// 8. Register Application & Background Services
builder.Services.AddTransient<SarGeneratorService>();
builder.Services.AddHostedService<HighRiskTransactionListener>();

// 9. Register Multi-Agent Coordinator Service
builder.Services.AddTransient<MultiAgentRiskCoordinator>();

// 10. Register Amazon DynamoDB client & Feature Store (MUST be before builder.Build())
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = new AmazonDynamoDBConfig
    {
        ServiceURL = "http://dynamodb-local:8000",
        AuthenticationRegion = "us-east-1"
    };
    return new AmazonDynamoDBClient("local", "local", config);
});

builder.Services.AddScoped<IDynamoDBContext, DynamoDBContext>();
builder.Services.AddScoped<IFeatureStore, DynamoDbFeatureStore>();

var app = builder.Build();

// 11. Initialize/Verify SQLite & DynamoDB Databases on Boot
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    // SQLite Audit DB Initialization
    try
    {
        var dbContext = services.GetRequiredService<AuditDbContext>();
        var created = await dbContext.Database.EnsureCreatedAsync();
        if (created)
        {
            logger.LogInformation("Compliance Audit SQLite database and tables successfully created at: {Path}", dbPath);
        }
        else
        {
            logger.LogInformation("Compliance Audit SQLite database already exists at: {Path}", dbPath);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the Compliance Audit database.");
    }

    // DynamoDB Table Initialization
    try
    {
        var dynamoClient = services.GetRequiredService<IAmazonDynamoDB>();
        var tables = await dynamoClient.ListTablesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (!tables.TableNames.Contains("AccountFeatureAggregates"))
        {
            await dynamoClient.CreateTableAsync(new CreateTableRequest
            {
                TableName = "AccountFeatureAggregates",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("AccountId", KeyType.HASH),
                    new KeySchemaElement("WindowBucket", KeyType.RANGE)
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition("AccountId", ScalarAttributeType.S),
                    new AttributeDefinition("WindowBucket", ScalarAttributeType.S)
                },
                ProvisionedThroughput = new ProvisionedThroughput(5, 5)
            }).WaitAsync(TimeSpan.FromSeconds(5));
            logger.LogInformation("DynamoDB table 'AccountFeatureAggregates' successfully created.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning("DynamoDB initialization note: {Message}", ex.Message);
    }
}

app.UseRouting();

// CORS must sit between UseRouting and endpoint mappings
app.UseCors("AllowAngular");

// Mapped route matches Angular's SignalR connection hub
app.MapHub<RiskHub>("/hubs/risk");

app.MapGet("/", () => "FinOps Risk Engine API Running");

app.Run();