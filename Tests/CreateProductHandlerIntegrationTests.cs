using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsApi.Common.Mapping;
using ProductsApi.Features.Products;
using ProductsApi.Features.Products.DTOs;
using ProductsApi.Persistence;
using ProductsApi.Validators;
using Xunit;

namespace ProductsApi.Tests;

public class CreateProductHandlerIntegrationTests : IDisposable
{
    private readonly ApplicationContext _context;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<CreateProductHandler>> _loggerMock;
    private readonly IValidator<CreateProductProfileRequest> _validator;
    private readonly CreateProductHandler _handler;

    public CreateProductHandlerIntegrationTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase($"Products_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationContext(dbOptions);

        var mapperConfig = new MapperConfiguration(
            cfg =>
            {
                cfg.AddProfile(new AdvancedProductMappingProfile());
            },
            loggerFactory: null);
        _mapper = mapperConfig.CreateMapper();

        _cache = new MemoryCache(new MemoryCacheOptions());

        _loggerMock = new Mock<ILogger<CreateProductHandler>>();

        var loggerValidator = new Mock<ILogger<CreateProductProfileValidator>>();
        _validator = new CreateProductProfileValidator(_context, loggerValidator.Object);

        _handler = new CreateProductHandler(_context, _mapper, _cache, _loggerMock.Object, _validator);
    }

    [Fact]
    public async Task Handle_ValidElectronicsProductRequest_CreatesProductWithCorrectMappings()
    {
        // Arrange
        var request = new CreateProductProfileRequest
        {
            Name = "Smart Tech Laptop",
            Brand = "Mega Tech",
            SKU = "LAP-12345",
            Category = ProductCategory.Electronics,
            Price = 1500m,
            ReleaseDate = DateTime.UtcNow.AddMonths(-2),
            StockQuantity = 3,
            ImageUrl = "https://example.com/laptop.jpg"
        };

        // Act
        var result = await _handler.Handle(request) as IResult;

        // Assert
        Assert.NotNull(result);
        var httpResult = result as Microsoft.AspNetCore.Http.IResult;
        // Result.Created is not easily introspected, but we can query DB and mapping directly
        var product = await _context.Products.FirstOrDefaultAsync(p => p.SKU == request.SKU);
        Assert.NotNull(product);

        var dto = _mapper.Map<ProductsApi.Features.Products.DTOs.ProductProfileDto>(product!);
        Assert.Equal("Electronics & Technology", dto.CategoryDisplayName);
        Assert.Equal("MT", dto.BrandInitials);
        Assert.False(string.IsNullOrWhiteSpace(dto.ProductAge));
        Assert.StartsWith(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, dto.FormattedPrice);
        Assert.Equal("Limited Stock", dto.AvailabilityStatus);

        _loggerMock.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_DuplicateSKU_ThrowsValidationExceptionWithLogging()
    {
        // Arrange
        var sku = "DUP-001";
        _context.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Existing Product",
            Brand = "Brand X",
            SKU = sku,
            Category = ProductCategory.Books,
            Price = 20m,
            ReleaseDate = DateTime.UtcNow.AddYears(-1),
            StockQuantity = 5,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var request = new CreateProductProfileRequest
        {
            Name = "New Product",
            Brand = "Brand Y",
            SKU = sku,
            Category = ProductCategory.Books,
            Price = 25m,
            ReleaseDate = DateTime.UtcNow.AddMonths(-3),
            StockQuantity = 2
        };

        // Act & Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _handler.Handle(request));

        _loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(ll => ll == LogLevel.Warning || ll == LogLevel.Information),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_HomeProductRequest_AppliesDiscountAndConditionalMapping()
    {
        // Arrange
        var request = new CreateProductProfileRequest
        {
            Name = "Soft Cushion",
            Brand = "Cozy Home",
            SKU = "HOME-001",
            Category = ProductCategory.Home,
            Price = 100m,
            ReleaseDate = DateTime.UtcNow.AddMonths(-6),
            StockQuantity = 10,
            ImageUrl = "https://example.com/cushion.jpg"
        };

        // Act
        await _handler.Handle(request);

        // Assert
        var product = await _context.Products.FirstOrDefaultAsync(p => p.SKU == request.SKU);
        Assert.NotNull(product);

        var dto = _mapper.Map<ProductProfileDto>(product!);
        Assert.Equal("Home & Garden", dto.CategoryDisplayName);
        Assert.Equal(90m, dto.Price); // 10% discount
        Assert.Null(dto.ImageUrl); // filtered out for Home category
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }
}
