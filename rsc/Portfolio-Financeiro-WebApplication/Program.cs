using System.Threading.RateLimiting;
using DAL.Sower;
using IoC;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio_Financeiro_WebApplication.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddPortfolioPersistence(builder.Configuration, builder.Environment);
builder.Services.AddPortfolioPerformanceAnalysis();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("analytics", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:Analytics:PermitLimit", 60),
                Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:Analytics:WindowSeconds", 60)),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SupportNonNullableReferenceTypes();
    options.OperationFilter<PerformanceResponseExampleOperationFilter>();
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dataSower = scope.ServiceProvider.GetRequiredService<IDataSower>();
    await dataSower.SowAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
