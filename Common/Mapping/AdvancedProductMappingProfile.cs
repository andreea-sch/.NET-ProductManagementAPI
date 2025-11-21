using AutoMapper;
using ProductsApi.Features.Products;
using ProductsApi.Features.Products.DTOs;

namespace ProductsApi.Common.Mapping;

public class AdvancedProductMappingProfile : Profile
{
    public AdvancedProductMappingProfile()
    {
        // Request -> Entity
        CreateMap<CreateProductProfileRequest, Product>()
            .ConstructUsing(src => new Product
            {
                Id = Guid.NewGuid(),
                Name = src.Name,
                Brand = src.Brand,
                SKU = src.SKU,
                Category = src.Category,
                Price = src.Price,
                ReleaseDate = src.ReleaseDate,
                ImageUrl = src.ImageUrl,
                StockQuantity = src.StockQuantity,
                IsAvailable = src.StockQuantity > 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            });

        // Entity -> DTO
        CreateMap<Product, ProductProfileDto>()
            .ForMember(dest => dest.CategoryDisplayName,
                opt => opt.MapFrom<CategoryDisplayResolver>())
            .ForMember(dest => dest.FormattedPrice,
                opt => opt.MapFrom<PriceFormatterResolver>())
            .ForMember(dest => dest.ProductAge,
                opt => opt.MapFrom<ProductAgeResolver>())
            .ForMember(dest => dest.BrandInitials,
                opt => opt.MapFrom<BrandInitialsResolver>())
            .ForMember(dest => dest.AvailabilityStatus,
                opt => opt.MapFrom<AvailabilityStatusResolver>())
            // Conditional ImageUrl mapping
            .ForMember(dest => dest.ImageUrl, opt =>
            {
                opt.PreCondition(src => src.Category != ProductCategory.Home);
                opt.MapFrom(src => src.ImageUrl);
            })
            // Conditional price mapping with Home discount
            .ForMember(dest => dest.Price, opt =>
            {
                opt.MapFrom(src => src.Category == ProductCategory.Home
                    ? Math.Round(src.Price * 0.9m, 2)
                    : src.Price);
            });
    }
}

public class CategoryDisplayResolver : IValueResolver<Product, ProductProfileDto, string>
{
    public string Resolve(Product source, ProductProfileDto destination, string destMember, ResolutionContext context)
        => source.Category switch
        {
            ProductCategory.Electronics => "Electronics & Technology",
            ProductCategory.Clothing => "Clothing & Fashion",
            ProductCategory.Books => "Books & Media",
            ProductCategory.Home => "Home & Garden",
            _ => "Uncategorized"
        };
}

public class PriceFormatterResolver : IValueResolver<Product, ProductProfileDto, string>
{
    public string Resolve(Product source, ProductProfileDto destination, string destMember, ResolutionContext context)
        => source.Category == ProductCategory.Home
            ? (source.Price * 0.9m).ToString("C2")
            : source.Price.ToString("C2");
}

public class ProductAgeResolver : IValueResolver<Product, ProductProfileDto, string>
{
    public string Resolve(Product source, ProductProfileDto destination, string destMember, ResolutionContext context)
    {
        var days = (DateTime.UtcNow.Date - source.ReleaseDate.Date).TotalDays;

        if (days < 30)
            return "New Release";
        if (days < 365)
        {
            var months = (int)(days / 30);
            return months <= 1 ? "1 month old" : $"{months} months old";
        }
        if (days < 1825)
        {
            var years = (int)(days / 365);
            return years <= 1 ? "1 year old" : $"{years} years old";
        }
        if (Math.Abs(days - 1825) < 0.1)
            return "Classic";

        var approxYears = (int)(days / 365);
        return approxYears <= 1 ? "1 year old" : $"{approxYears} years old";
    }
}

public class BrandInitialsResolver : IValueResolver<Product, ProductProfileDto, string>
{
    public string Resolve(Product source, ProductProfileDto destination, string destMember, ResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(source.Brand))
            return "?";

        var parts = source.Brand
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
            return parts[0][0].ToString().ToUpperInvariant();

        var first = parts.First()[0];
        var last = parts.Last()[0];
        return $"{char.ToUpperInvariant(first)}{char.ToUpperInvariant(last)}";
    }
}

public class AvailabilityStatusResolver : IValueResolver<Product, ProductProfileDto, string>
{
    public string Resolve(Product source, ProductProfileDto destination, string destMember, ResolutionContext context)
    {
        if (!source.IsAvailable)
            return "Out of Stock";

        if (source.StockQuantity <= 0)
            return "Unavailable";
        if (source.StockQuantity == 1)
            return "Last Item";
        if (source.StockQuantity <= 5)
            return "Limited Stock";
        return "In Stock";
    }
}
