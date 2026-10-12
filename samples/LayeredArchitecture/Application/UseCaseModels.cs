using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Application;

/// <summary>
/// An application-layer model, carrying both contract concerns (validation) and persistence
/// concerns (column mapping, precision). Only the former should reach the API contract.
/// </summary>
public class UserUseCaseModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Column("created_utc")]
    public DateTime CreatedUtc { get; set; }

    [Precision(18, 2)]
    public decimal Balance { get; set; }

    /// <summary>Internal; the contract excludes it.</summary>
    public string AuditTrail { get; set; } = "";

    public AddressUseCaseModel? Address { get; set; }

    public List<OrderUseCaseModel> Orders { get; set; } = new();
}

public class AddressUseCaseModel
{
    public int Id { get; set; }
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

public class OrderUseCaseModel
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";

    [Precision(18, 2)]
    public decimal Total { get; set; }
}
