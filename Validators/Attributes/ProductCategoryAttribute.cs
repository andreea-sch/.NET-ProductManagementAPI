using System.ComponentModel.DataAnnotations;
using ProductsApi.Features.Products;

namespace ProductsApi.Validators.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ProductCategoryAttribute : ValidationAttribute
{
    private readonly ProductCategory[] _allowedCategories;

    public ProductCategoryAttribute(params ProductCategory[] allowedCategories)
    {
        _allowedCategories = allowedCategories;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not ProductCategory category)
        {
            return new ValidationResult(GetErrorMessage());
        }

        if (!_allowedCategories.Contains(category))
        {
            return new ValidationResult(GetErrorMessage());
        }

        return ValidationResult.Success;
    }

    private string GetErrorMessage()
    {
        var allowed = string.Join(", ", _allowedCategories.Select(c => c.ToString()));
        return ErrorMessage ?? $"Category must be one of the following: {allowed}.";
    }
}
