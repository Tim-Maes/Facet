using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Facet.TestConsole.Data;
using Facet.TestConsole.DTOs;
using Facet.Extensions.EFCore;
using Facet.Extensions;

namespace Facet.TestConsole.Services;

public interface IUserService
{
    Task<IEnumerable<DbUserDto>> GetAllUsersAsync();
    Task<DbUserDto?> GetUserByIdAsync(int id);
    Task<DbUserDto> CreateUserAsync(CreateDbUserDto createUserDto);
    Task<DbUserDto?> UpdateUserAsync(int id, UpdateDbUserDto updateUserDto);
    Task<bool> DeleteUserAsync(int id);
    Task<(DbUserDto?, IReadOnlyList<string> changedProperties)> UpdateUserWithChangeTrackingAsync(int id, UpdateDbUserDto updateUserDto);
}

public class UserService : IUserService
{
    private readonly FacetTestDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(FacetTestDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<DbUserDto>> GetAllUsersAsync()
    {
        _logger.LogInformation("Getting all users");
        
        return await _context.Users
            .Where(u => u.IsActive)
            .ToFacetsAsync<DbUserDto>();
    }

    public async Task<DbUserDto?> GetUserByIdAsync(int id)
    {
        _logger.LogInformation("Getting user with ID: {UserId}", id);
        
        return await _context.Users
            .Where(u => u.Id == id)
            .FirstFacetAsync<DbUserDto>();
    }

    public async Task<DbUserDto> CreateUserAsync(CreateDbUserDto createUserDto)
    {
        _logger.LogInformation("Creating new user: {Email}", createUserDto.Email);

        var user = new Data.User
        {
            FirstName = createUserDto.FirstName,
            LastName = createUserDto.LastName,
            Email = createUserDto.Email,
            Password = createUserDto.Password,
            IsActive = createUserDto.IsActive,
            DateOfBirth = createUserDto.DateOfBirth,
            ProfilePictureUrl = createUserDto.ProfilePictureUrl,
            Bio = createUserDto.Bio,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User created with ID: {UserId}", user.Id);
        
        return user.ToFacet<DbUserDto>();
    }

    public async Task<DbUserDto?> UpdateUserAsync(int id, UpdateDbUserDto updateUserDto)
    {
        _logger.LogInformation("Updating user with ID: {UserId}", id);

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found", id);
            return null;
        }

        user.UpdateFromFacet(updateUserDto, _context);

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User {UserId} updated successfully", id);

        return user.ToFacet<DbUserDto>();
    }

    public async Task<(DbUserDto?, IReadOnlyList<string> changedProperties)> UpdateUserWithChangeTrackingAsync(int id, UpdateDbUserDto updateUserDto)
    {
        _logger.LogInformation("Updating user with change tracking for ID: {UserId}", id);

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found", id);
            return (null, new List<string>());
        }

        var result = user.UpdateFromFacetWithChanges(updateUserDto, _context);

        if (result.HasChanges)
        {
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("User {UserId} updated. Changed properties: {Properties}", 
                id, string.Join(", ", result.ChangedProperties));
        }
        else
        {
            _logger.LogInformation("No changes detected for user {UserId}", id);
        }

        return (user.ToFacet<DbUserDto>(), result.ChangedProperties);
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        _logger.LogInformation("Deleting user with ID: {UserId}", id);

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found", id);
            return false;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted successfully", id);
        return true;
    }
}

public interface IProductService
{
    Task<IEnumerable<DbProductDto>> GetAllProductsAsync();
    Task<DbProductDto?> GetProductByIdAsync(int id);
    Task<DbProductDto?> UpdateProductAsync(int id, UpdateDbProductDto updateProductDto);
    Task<(DbProductDto?, IReadOnlyList<string> changedProperties)> UpdateProductWithChangeTrackingAsync(int id, UpdateDbProductDto updateProductDto);
}

public class ProductService : IProductService
{
    private readonly FacetTestDbContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(FacetTestDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<DbProductDto>> GetAllProductsAsync()
    {
        _logger.LogInformation("Getting all products");
        
        return await _context.Products
            .Where(p => p.IsAvailable)
            .ToFacetsAsync<DbProductDto>();
    }

    public async Task<DbProductDto?> GetProductByIdAsync(int id)
    {
        _logger.LogInformation("Getting product with ID: {ProductId}", id);
        
        return await _context.Products
            .Where(p => p.Id == id)
            .FirstFacetAsync<DbProductDto>();
    }

    public async Task<DbProductDto?> UpdateProductAsync(int id, UpdateDbProductDto updateProductDto)
    {
        _logger.LogInformation("Updating product with ID: {ProductId}", id);

        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            _logger.LogWarning("Product with ID {ProductId} not found", id);
            return null;
        }

        product.UpdateFromFacet(updateProductDto, _context);

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Product {ProductId} updated successfully", id);

        return product.ToFacet<DbProductDto>();
    }

    public async Task<(DbProductDto?, IReadOnlyList<string> changedProperties)> UpdateProductWithChangeTrackingAsync(int id, UpdateDbProductDto updateProductDto)
    {
        _logger.LogInformation("Updating product with change tracking for ID: {ProductId}", id);

        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            _logger.LogWarning("Product with ID {ProductId} not found", id);
            return (null, new List<string>());
        }

        var result = product.UpdateFromFacetWithChanges(updateProductDto, _context);

        if (result.HasChanges)
        {
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Product {ProductId} updated. Changed properties: {Properties}", 
                id, string.Join(", ", result.ChangedProperties));
        }
        else
        {
            _logger.LogInformation("No changes detected for product {ProductId}", id);
        }

        return (product.ToFacet<DbProductDto>(), result.ChangedProperties);
    }
}