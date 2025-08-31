using System;
using Facet;

namespace Example
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? PasswordHash { get; set; }
    }

    [GenerateDtos]
    public class BasicUser
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    [GenerateDtos(
        Types = DtoTypes.Create | DtoTypes.Response,
        OutputType = FacetKind.Class,
        ExcludeProperties = new[] { "PasswordHash" },
        Namespace = "Example.DTOs"
    )]
    public class CustomUser
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    [GenerateAuditableDtos]
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }
    }

    [GenerateDtos(
        NamingConvention = DtoNamingConvention.Custom,
        CustomPrefix = "Api",
        CustomSuffix = "Model",
        Types = DtoTypes.Response
    )]
    public class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public DateTime OrderDate { get; set; }
    }
}

/*
This will generate the following DTOs:

For BasicUser:
- CreateBasicUserRequest (excludes Id, CreatedAt)
- UpdateBasicUserRequest (excludes CreatedAt)
- BasicUserResponse (includes all properties)
- BasicUserQuery (all properties nullable)

For CustomUser (in Example.DTOs namespace):
- CreateCustomUserRequest (excludes Id, CreatedAt, PasswordHash)
- CustomUserResponse (excludes PasswordHash)

For Product:
- CreateProductRequest (excludes Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
- UpdateProductRequest (excludes CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
- ProductResponse (includes all properties)
- ProductQuery (all properties nullable)

For Order:
- ApiOrderModel (using custom naming)

Each generated DTO will include:
- Constructor that accepts the source type
- LINQ Expression projection for Entity Framework
- Proper property mapping based on DTO type conventions
*/