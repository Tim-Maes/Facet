using Facet;
using Application;

namespace Api.Contracts;

// The API surface, shaped from the application layer at build time and referencing none of it at
// run time. Run `dotnet run --project Api.Host` to see the emitted assembly references.

[Facet(typeof(AddressUseCaseModel))]
public partial class AddressResponse;

[Facet(typeof(OrderUseCaseModel))]
public partial class OrderResponse;

/// <summary>
/// NestedFacets is what keeps this clean. Without it, Address would be generated as
/// <c>AddressUseCaseModel?</c> and Orders as <c>List&lt;OrderUseCaseModel&gt;</c> — and the
/// reference to Application would come back through the property types alone, even with the
/// mappings and attributes suppressed. FAC116 reports that if you forget.
/// </summary>
[Facet(typeof(UserUseCaseModel),
    nameof(UserUseCaseModel.AuditTrail),
    NestedFacets = new[] { typeof(AddressResponse), typeof(OrderResponse) })]
public partial class UserResponse
{
    /// <summary>
    /// Has no counterpart on the source. UserMapping in Api.Mappers fills it — as an expression,
    /// so it reaches SQL.
    /// </summary>
    public string Display { get; set; } = "";
}
