using ProductsApi.Features.Products;

namespace ProductsApi.Common.Logging;

public static class LogEvents
{
    public const int ProductCreationStarted = 2001;
    public const int ProductValidationFailed = 2002;
    public const int ProductCreationCompleted = 2003;
    public const int DatabaseOperationStarted = 2004;
    public const int DatabaseOperationCompleted = 2005;
    public const int CacheOperationPerformed = 2006;
    public const int SKUValidationPerformed = 2007;
    public const int StockValidationPerformed = 2008;
}

public record ProductCreationMetrics(
    string OperationId,
    string ProductName,
    string SKU,
    ProductCategory Category,
    TimeSpan ValidationDuration,
    TimeSpan DatabaseSaveDuration,
    TimeSpan TotalDuration,
    bool Success,
    string? ErrorReason);
