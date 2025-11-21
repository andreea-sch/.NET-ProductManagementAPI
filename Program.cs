using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProductsApi.Common.Logging;
using ProductsApi.Common.Mapping;
using ProductsApi.Common.Middleware;
using ProductsApi.Features.Products;
using ProductsApi.Features.Products.DTOs;
using ProductsApi.Persistence;
using ProductsApi.Validators;

var builder = WebApplication.CreateBuilder(args);

// Logging & configuration
builder.Services.AddLogging();
builder.Services.AddMemoryCache();

// DbContext
builder.Services.AddDbContext<ApplicationContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                           ?? "Data Source=products.db";
    options.UseSqlite(connectionString);
});

// AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<AdvancedProductMappingProfile>();
}, typeof(AdvancedProductMappingProfile));

// Validators
builder.Services.AddScoped<IValidator<CreateProductProfileRequest>, CreateProductProfileValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductProfileValidator>();
builder.Services.AddFluentValidationAutoValidation();

// Handlers
builder.Services.AddScoped<CreateProductHandler>();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Products Management API",
        Version = "v1",
        Description = "Advanced API for managing products with AutoMapper, validation and logging."
    });
});

// CORS (dev)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Ensure DB exists
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
    ctx.Database.EnsureCreated();
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("DevCors");

// Correlation middleware
app.UseMiddleware<CorrelationMiddleware>();

app.UseHttpsRedirection();

// Product endpoints
app.MapPost("/products", async (CreateProductProfileRequest req, CreateProductHandler handler) =>
{
    return await handler.Handle(req);
})
.WithName("CreateProduct")
.WithTags("Products")
.WithOpenApi(op =>
{
    op.Summary = "Create a new product";
    op.Description = "Creates a new product with advanced validation, mapping and logging.";
    return op;
});

app.Run();
