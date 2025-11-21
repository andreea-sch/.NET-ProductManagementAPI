using Microsoft.Extensions.Logging;
using ProductsApi.Features.Products;

namespace ProductsApi.Common.Logging;

public static class LoggingExtensions
{
    public static void LogProductCreationMetrics(this ILogger logger, ProductCreationMetrics metrics)
    {
        logger.LogInformation(
            eventId: new EventId(LogEvents.ProductCreationCompleted, nameof(LogEvents.ProductCreationCompleted)),
            message: "Product creation metrics | OperationId: {OperationId}, Name: {ProductName}, SKU: {SKU}, Category: {Category}, " +
                     "ValidationMs: {ValidationMs}, DbSaveMs: {DbSaveMs}, TotalMs: {TotalMs}, Success: {Success}, ErrorReason: {ErrorReason}",
            metrics.OperationId,
            metrics.ProductName,
            metrics.SKU,
            metrics.Category.ToString(),
            metrics.ValidationDuration.TotalMilliseconds,
            metrics.DatabaseSaveDuration.TotalMilliseconds,
            metrics.TotalDuration.TotalMilliseconds,
            metrics.Success,
            metrics.ErrorReason ?? string.Empty
        );
    }

    public static IDisposable BeginProductScope(this ILogger logger, string operationId, string name, string sku, ProductCategory category)
    {
        var scope = new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["ProductName"] = name,
            ["SKU"] = sku,
            ["Category"] = category.ToString()
        };
        return logger.BeginScope(scope);
    }
}
