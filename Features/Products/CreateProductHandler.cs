using System.Diagnostics;
using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ProductsApi.Common.Logging;
using ProductsApi.Features.Products.DTOs;
using ProductsApi.Persistence;

namespace ProductsApi.Features.Products;

public class CreateProductHandler
{
    private readonly ApplicationContext _context;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CreateProductHandler> _logger;
    private readonly IValidator<CreateProductProfileRequest> _validator;
    private const string CacheKeyAllProducts = "all_products";

    public CreateProductHandler(
        ApplicationContext context,
        IMapper mapper,
        IMemoryCache cache,
        ILogger<CreateProductHandler> logger,
        IValidator<CreateProductProfileRequest> validator)
    {
        _context = context;
        _mapper = mapper;
        _cache = cache;
        _logger = logger;
        _validator = validator;
    }

    public async Task<IResult> Handle(CreateProductProfileRequest request)
    {
        var operationId = Guid.NewGuid().ToString("N")[..8];
        var totalStopwatch = Stopwatch.StartNew();
        var validationStopwatch = new Stopwatch();
        var dbStopwatch = new Stopwatch();

        using var scope = _logger.BeginProductScope(operationId, request.Name, request.SKU, request.Category);

        _logger.LogInformation(
            new EventId(LogEvents.ProductCreationStarted, nameof(LogEvents.ProductCreationStarted)),
            "Starting product creation | Name: {Name}, Brand: {Brand}, SKU: {SKU}, Category: {Category}",
            request.Name, request.Brand, request.SKU, request.Category);

        try
        {
            // Validation phase
            validationStopwatch.Start();
            _logger.LogInformation(
                new EventId(LogEvents.SKUValidationPerformed, nameof(LogEvents.SKUValidationPerformed)),
                "Validating SKU {SKU}", request.SKU);

            var validationResult = await _validator.ValidateAsync(request);
            validationStopwatch.Stop();

            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    new EventId(LogEvents.ProductValidationFailed, nameof(LogEvents.ProductValidationFailed)),
                    "Product validation failed for {Name} ({SKU}). Errors: {Errors}",
                    request.Name,
                    request.SKU,
                    string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));

                throw new FluentValidation.ValidationException(validationResult.Errors);
            }

            // Extra SKU uniqueness guard at handler level
            var skuExists = await _context.Products.AnyAsync(p => p.SKU == request.SKU);
            if (skuExists)
            {
                _logger.LogWarning("SKU {SKU} already exists in database.", request.SKU);
                throw new FluentValidation.ValidationException("SKU already exists.");
            }

            // Map and save
            dbStopwatch.Start();
            _logger.LogInformation(
                new EventId(LogEvents.DatabaseOperationStarted, nameof(LogEvents.DatabaseOperationStarted)),
                "Starting database save operation for {Name} ({SKU})", request.Name, request.SKU);

            var product = _mapper.Map<Product>(request);

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            dbStopwatch.Stop();
            _logger.LogInformation(
                new EventId(LogEvents.DatabaseOperationCompleted, nameof(LogEvents.DatabaseOperationCompleted)),
                "Database save completed for ProductId {ProductId}", product.Id);

            // Invalidate cache
            _logger.LogInformation(
                new EventId(LogEvents.CacheOperationPerformed, nameof(LogEvents.CacheOperationPerformed)),
                "Invalidating cache key {CacheKey}", CacheKeyAllProducts);
            _cache.Remove(CacheKeyAllProducts);

            totalStopwatch.Stop();

            var metrics = new ProductCreationMetrics(
                OperationId: operationId,
                ProductName: product.Name,
                SKU: product.SKU,
                Category: product.Category,
                ValidationDuration: validationStopwatch.Elapsed,
                DatabaseSaveDuration: dbStopwatch.Elapsed,
                TotalDuration: totalStopwatch.Elapsed,
                Success: true,
                ErrorReason: null);

            _logger.LogProductCreationMetrics(metrics);

            var dto = _mapper.Map<ProductProfileDto>(product);
            return Results.Created($"/products/{product.Id}", dto);
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();

            var metrics = new ProductCreationMetrics(
                OperationId: operationId,
                ProductName: request.Name,
                SKU: request.SKU,
                Category: request.Category,
                ValidationDuration: validationStopwatch.Elapsed,
                DatabaseSaveDuration: dbStopwatch.Elapsed,
                TotalDuration: totalStopwatch.Elapsed,
                Success: false,
                ErrorReason: ex.Message);

            _logger.LogProductCreationMetrics(metrics);

            _logger.LogError(ex,
                "Error during product creation | OperationId: {OperationId}, Name: {Name}, SKU: {SKU}, Category: {Category}",
                operationId, request.Name, request.SKU, request.Category);

            throw;
        }
    }
}
