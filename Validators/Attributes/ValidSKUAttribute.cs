using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ProductsApi.Validators.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ValidSKUAttribute : ValidationAttribute, IClientModelValidator
{
    private static readonly Regex SkuRegex = new("^[A-Za-z0-9-]{5,20}$", RegexOptions.Compiled);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var sku = value as string;
        if (string.IsNullOrWhiteSpace(sku))
        {
            return new ValidationResult(ErrorMessage ?? "SKU is required.");
        }

        sku = sku.Replace(" ", string.Empty);

        if (!SkuRegex.IsMatch(sku))
        {
            return new ValidationResult(ErrorMessage ?? "SKU must be 5-20 characters, alphanumeric with hyphens.");
        }

        return ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-validsku", ErrorMessage ?? "Invalid SKU format.");
        MergeAttribute(context.Attributes, "data-val-validsku-pattern", "^[A-Za-z0-9-]{5,20}$");
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
        {
            attributes.Add(key, value);
        }
    }
}
