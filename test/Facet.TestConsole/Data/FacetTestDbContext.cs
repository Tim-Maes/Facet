using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Facet.TestConsole.Data;

// Entity classes that will be stored in the database
public class User
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Password { get; set; } = string.Empty; // This will be excluded in DTOs
    
    public bool IsActive { get; set; } = true;
    
    public DateTime DateOfBirth { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginAt { get; set; }
    
    [MaxLength(500)]
    public string? ProfilePictureUrl { get; set; }
    
    [MaxLength(1000)]
    public string? Bio { get; set; }
}

public class Product
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public decimal Price { get; set; }
    
    public int CategoryId { get; set; }
    
    public bool IsAvailable { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(1000)]
    public string InternalNotes { get; set; } = string.Empty; // This will be excluded in DTOs
}

public class Category
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

// DbContext for the test application
public class FacetTestDbContext : DbContext
{
    public FacetTestDbContext(DbContextOptions<FacetTestDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure Product entity
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            
            // Configure relationship with Category
            entity.HasOne<Category>()
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Category entity
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // Seed data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Categories
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Electronics", Description = "Electronic devices and gadgets", CreatedAt = DateTime.UtcNow.AddDays(-30) },
            new Category { Id = 2, Name = "Books", Description = "Books and educational materials", CreatedAt = DateTime.UtcNow.AddDays(-25) },
            new Category { Id = 3, Name = "Clothing", Description = "Apparel and accessories", CreatedAt = DateTime.UtcNow.AddDays(-20) }
        );

        // Seed Users
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Password = "hashedpassword123",
                IsActive = true,
                DateOfBirth = new DateTime(1990, 5, 15),
                CreatedAt = DateTime.UtcNow.AddDays(-100),
                LastLoginAt = DateTime.UtcNow.AddHours(-2),
                Bio = "Software developer passionate about clean code"
            },
            new User
            {
                Id = 2,
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@example.com",
                Password = "hashedpassword456",
                IsActive = true,
                DateOfBirth = new DateTime(1985, 12, 3),
                CreatedAt = DateTime.UtcNow.AddDays(-90),
                LastLoginAt = DateTime.UtcNow.AddDays(-1),
                Bio = "UX designer with 10+ years experience"
            },
            new User
            {
                Id = 3,
                FirstName = "Bob",
                LastName = "Johnson",
                Email = "bob.johnson@example.com",
                Password = "hashedpassword789",
                IsActive = false,
                DateOfBirth = new DateTime(1992, 8, 22),
                CreatedAt = DateTime.UtcNow.AddDays(-80),
                LastLoginAt = null,
                Bio = "Data analyst and machine learning enthusiast"
            }
        );

        // Seed Products
        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                Id = 1,
                Name = "MacBook Pro",
                Description = "High-performance laptop for professionals",
                Price = 1299.99m,
                CategoryId = 1,
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow.AddDays(-50),
                InternalNotes = "Supplier: Apple Inc. - Margin: 25% - Volume discount available"
            },
            new Product
            {
                Id = 2,
                Name = "iPhone 15",
                Description = "Latest smartphone with advanced features",
                Price = 899.99m,
                CategoryId = 1,
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow.AddDays(-40),
                InternalNotes = "Supplier: Apple Inc. - Margin: 30% - High demand item"
            },
            new Product
            {
                Id = 3,
                Name = "Clean Code",
                Description = "A Handbook of Agile Software Craftsmanship by Robert C. Martin",
                Price = 49.99m,
                CategoryId = 2,
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                InternalNotes = "Publisher: Prentice Hall - Margin: 40% - Educational discount available"
            },
            new Product
            {
                Id = 4,
                Name = "Winter Jacket",
                Description = "Warm and comfortable winter jacket",
                Price = 159.99m,
                CategoryId = 3,
                IsAvailable = false,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                InternalNotes = "Supplier: OutdoorGear Co. - Margin: 50% - Seasonal item"
            }
        );
    }
}