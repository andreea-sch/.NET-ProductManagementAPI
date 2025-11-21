using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace ProductsApi.Validators.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class PriceRangeAttribute : ValidationAttribute
{
    private readonly decimal _min;
    private readonly decimal _max;

    public PriceRangeAttribute(double min, double max)
    {
        _min = (decimal)min;
        _max = (decimal)max;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return new ValidationResult(ErrorMessage ?? GetDefaultMessage());
        }

        if (value is not decimal price)
        {
            return new ValidationResult(ErrorMessage ?? GetDefaultMessage());
        }

        if (price < _min || price > _max)
        {
            return new ValidationResult(ErrorMessage ?? GetDefaultMessage());
        }

        return ValidationResult.Success;
    }

    private string GetDefaultMessage()
        => $"Price must be between {_min.ToString("C2", CultureInfo.CurrentCulture)} and {_max.ToString("C2", CultureInfo.CurrentCulture)}.";
}
