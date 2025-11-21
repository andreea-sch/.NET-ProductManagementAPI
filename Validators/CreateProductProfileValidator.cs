using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductsApi.Features.Products;
using ProductsApi.Features.Products.DTOs;
using ProductsApi.Persistence;

namespace ProductsApi.Validators;

public class CreateProductProfileValidator : AbstractValidator<CreateProductProfileRequest>
{
    private readonly ApplicationContext _context;
    private readonly ILogger<CreateProductProfileValidator> _logger;

    private static readonly string[] InappropriateWords =
    [
        "bad",
        "fake",
        "illegal"
    ];

    private static readonly string[] TechnologyKeywords =
    [
        "tech", "smart", "laptop", "phone", "tablet", "gaming", "pc", "monitor", "headphones"
    ];

    private static readonly string[] HomeRestrictedWords =
    [
        "explosive", "toxic"
    ];

    public CreateProductProfileValidator(ApplicationContext context, ILogger<CreateProductProfileValidator> logger)
    {
        _context = context;
        _logger = logger;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MinimumLength(1).WithMessage("Product name must be at least 1 character.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.")
            .Must(BeValidName).WithMessage("Product name contains inappropriate content.")
            .MustAsync(BeUniqueName).WithMessage("A product with the same name already exists for this brand.");

        RuleFor(x => x.Brand)
            .NotEmpty().WithMessage("Brand is required.")
            .MinimumLength(2).WithMessage("Brand must be at least 2 characters.")
            .MaximumLength(100).WithMessage("Brand must not exceed 100 characters.")
            .Must(BeValidBrandName).WithMessage("Brand contains invalid characters.");

        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("SKU is required.")
            .Must(BeValidSKU).WithMessage("SKU must be 5-20 characters, alphanumeric with hyphens.")
            .MustAsync(BeUniqueSKU).WithMessage("A product with this SKU already exists.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Category must be a valid value.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0.")
            .LessThan(10_000m).WithMessage("Price must be less than 10,000.");

        RuleFor(x => x.ReleaseDate)
            .LessThanOrEqualTo(DateTime.UtcNow.Date).WithMessage("Release date cannot be in the future.")
            .GreaterThan(new DateTime(1900, 1, 1)).WithMessage("Release date cannot be before 1900.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.")
            .LessThanOrEqualTo(100_000).WithMessage("Stock cannot exceed 100,000.");

        When(x => !string.IsNullOrWhiteSpace(x.ImageUrl), () =>
        {
            RuleFor(x => x.ImageUrl!)
                .Must(BeValidImageUrl).WithMessage("ImageUrl must be a valid image URL.");
        });

        RuleFor(x => x)
            .MustAsync(PassBusinessRules)
            .WithMessage("Product does not satisfy business rules.");

        // Conditional validation
        When(x => x.Category == ProductCategory.Electronics, () =>
        {
            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(50m).WithMessage("Electronics must have a minimum price of $50.00.");

            RuleFor(x => x.Name)
                .Must(ContainTechnologyKeywords)
                .WithMessage("Electronics products must contain technology-related keywords in the name.");

            RuleFor(x => x.ReleaseDate)
                .GreaterThanOrEqualTo(DateTime.UtcNow.Date.AddYears(-5))
                .WithMessage("Electronics products must be released within the last 5 years.");
        });

        When(x => x.Category == ProductCategory.Home, () =>
        {
            RuleFor(x => x.Price)
                .LessThanOrEqualTo(200m).WithMessage("Home products must not exceed a price of $200.00.");

            RuleFor(x => x.Name)
                .Must(BeAppropriateForHome)
                .WithMessage("Home product name contains inappropriate content.");
        });

        When(x => x.Category == ProductCategory.Clothing, () =>
        {
            RuleFor(x => x.Brand)
                .MinimumLength(3).WithMessage("Clothing brand must be at least 3 characters.");
        });

        RuleFor(x => x)
            .Must(ExpensiveProductsMustHaveLimitedStock)
            .WithMessage("Expensive products (price > 100) must have stock ≤ 20 units.");
    }

    private bool BeValidName(string name)
        => !InappropriateWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> BeUniqueName(CreateProductProfileRequest request, string name, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking unique name for product {Name} and brand {Brand}", name, request.Brand);
        return !await _context.Products
            .AnyAsync(p => p.Name == name && p.Brand == request.Brand, cancellationToken);
    }

    private bool BeValidBrandName(string brand)
    {
        var regex = new Regex("^[A-Za-z0-9 .\\-']+$");
        return regex.IsMatch(brand);
    }

    private bool BeValidSKU(string sku)
    {
        sku = sku.Replace(" ", string.Empty);
        var regex = new Regex("^[A-Za-z0-9-]{5,20}$");
        return regex.IsMatch(sku);
    }

    private async Task<bool> BeUniqueSKU(string sku, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking SKU uniqueness for {SKU}", sku);
        return !await _context.Products.AnyAsync(p => p.SKU == sku, cancellationToken);
    }

    private bool BeValidImageUrl(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var lower = uri.AbsolutePath.ToLowerInvariant();
        return lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png")
               || lower.EndsWith(".gif") || lower.EndsWith(".webp");
    }

    private async Task<bool> PassBusinessRules(CreateProductProfileRequest request, CancellationToken cancellationToken)
    {
        // Rule 1: Daily product addition limit check (max 500 per day)
        var today = DateTime.UtcNow.Date;
        var todaysCount = await _context.Products
            .CountAsync(p => p.CreatedAt.Date == today, cancellationToken);
        if (todaysCount >= 500)
        {
            _logger.LogWarning("Daily product addition limit reached: {Count}", todaysCount);
            return false;
        }

        // Rule 2: Electronics minimum price check ($50.00)
        if (request.Category == ProductCategory.Electronics && request.Price < 50m)
        {
            _logger.LogWarning("Electronics product below minimum price: {Price}", request.Price);
            return false;
        }

        // Rule 3: Home product content restrictions
        if (request.Category == ProductCategory.Home &&
            HomeRestrictedWords.Any(w => request.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Home product contains restricted word in name: {Name}", request.Name);
            return false;
        }

        // Rule 4: High-value product stock limit (>$500 = max 10 stock)
        if (request.Price > 500m && request.StockQuantity > 10)
        {
            _logger.LogWarning("High-value product has too much stock: {Stock}", request.StockQuantity);
            return false;
        }

        return true;
    }

    private bool ContainTechnologyKeywords(CreateProductProfileRequest request, string name)
        => TechnologyKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

    private bool BeAppropriateForHome(CreateProductProfileRequest request, string name)
        => !HomeRestrictedWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));

    private bool ExpensiveProductsMustHaveLimitedStock(CreateProductProfileRequest request)
        => request.Price <= 100m || request.StockQuantity <= 20;
}
