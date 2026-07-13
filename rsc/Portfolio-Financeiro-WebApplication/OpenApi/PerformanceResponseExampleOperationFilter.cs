using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Portfolio_Financeiro_WebApplication.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Portfolio_Financeiro_WebApplication.OpenApi;

/// <summary>
/// Complements the generated performance schema with a representative payload.
/// The values mirror the seeded portfolio so Swagger UI documents units, signs,
/// nullable metrics and the nested position collection as they appear on the wire.
/// </summary>
public sealed class PerformanceResponseExampleOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(AnalyticsController)
            || context.MethodInfo.Name != nameof(AnalyticsController.GetPerformance)
            || !operation.Responses.TryGetValue(StatusCodes.Status200OK.ToString(), out var response))
        {
            return;
        }

        var example = CreateExample();
        foreach (var mediaType in response.Content
                     .Where(content => content.Key.Contains("json", StringComparison.OrdinalIgnoreCase))
                     .Select(content => content.Value))
        {
            mediaType.Example = example;
        }
    }

    private static OpenApiObject CreateExample() => new()
    {
        ["totalInvestment"] = new OpenApiDouble(100_000.00),
        ["currentValue"] = new OpenApiDouble(80_940.00),
        ["totalReturn"] = new OpenApiDouble(-19.0600),
        ["totalReturnAmount"] = new OpenApiDouble(-19_060.00),
        ["annualizedReturn"] = new OpenApiDouble(-25.2678),
        ["volatility"] = new OpenApiDouble(1.2384),
        ["positionsPerformance"] = new OpenApiArray
        {
            CreatePosition("WEGE3", 9_000.00, 8_570.00, -4.7778, 10.5881),
            CreatePosition("ITUB4", 16_800.00, 19_260.00, 14.6429, 23.7954),
            CreatePosition("BBDC4", 18_000.00, 15_800.00, -12.2222, 19.5206),
            CreatePosition("VALE3", 18_000.00, 19_560.00, 8.6667, 24.1660),
            CreatePosition("PETR4", 15_000.00, 17_750.00, 18.3333, 21.9298)
        }
    };

    private static OpenApiObject CreatePosition(
        string symbol,
        double investedAmount,
        double currentValue,
        double returnPercentage,
        double weight) => new()
    {
        ["symbol"] = new OpenApiString(symbol),
        ["investedAmount"] = new OpenApiDouble(investedAmount),
        ["currentValue"] = new OpenApiDouble(currentValue),
        ["return"] = new OpenApiDouble(returnPercentage),
        ["weight"] = new OpenApiDouble(weight)
    };
}
