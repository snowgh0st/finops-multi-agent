using System.Diagnostics;
using System.Text.Json;
using FinOps.Domain.Entities;
using FinOps.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FinOps.Infrastructure.Filters;

public class AuditFunctionFilter : IAutoFunctionInvocationFilter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditFunctionFilter> _logger;
    private static readonly ActivitySource AuditActivitySource = new("FinOps.Compliance.AuditTracing");

    public AuditFunctionFilter(IServiceProvider serviceProvider, ILogger<AuditFunctionFilter> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        using var activity = AuditActivitySource.StartActivity($"SK.Plugin.{context.Function.PluginName}.{context.Function.Name}");
        var stopwatch = Stopwatch.StartNew();

        string pluginName = context.Function.PluginName ?? "UnknownPlugin";
        string functionName = context.Function.Name;
        string argsJson = JsonSerializer.Serialize(context.Arguments);

        activity?.SetTag("compliance.plugin", pluginName);
        activity?.SetTag("compliance.function", functionName);

        try
        {
            // Execute the tool
            await next(context);
            stopwatch.Stop();

            string resultJson = JsonSerializer.Serialize(context.Result?.GetValue<object>());

            activity?.SetTag("compliance.execution_status", "Success");
            activity?.SetTag("compliance.duration_ms", stopwatch.ElapsedMilliseconds);

            // Persist audit log record
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

            var auditRecord = new ComplianceAuditLog
            {
                EventType = "ToolInvocation",
                PluginName = pluginName,
                FunctionName = functionName,
                InputParameters = argsJson,
                OutputResult = resultJson,
                ExecutionDurationMs = stopwatch.ElapsedMilliseconds
            };

            dbContext.AuditLogs.Add(auditRecord);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("[AuditLog] Saved tool execution: {Plugin}.{Function} ({Ms}ms)", pluginName, functionName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("compliance.execution_status", "Failed");
            activity?.SetTag("error.message", ex.Message);
            _logger.LogError(ex, "[AuditLog] Failed tool execution: {Plugin}.{Function}", pluginName, functionName);
            throw;
        }
    }
}