using Api.Contracts;
using Application;
using Facet;
using Facet.Mapping;

// One line registers a mapper for every facet Api.Contracts declares. It reads the contract
// manifest that assembly records — AssemblyMetadata entries holding two strings, so the record
// survives Facet_EmitAttributeMetadata=false without reintroducing a reference.
//
// Add a facet over in Api.Contracts and its mapper appears here with no edit. Anything this does
// not cover is reported as FAC115, so the two sides cannot drift apart silently.
[assembly: FacetMappersForAssembly(typeof(UserResponse))]

// An explicit registration wins over the blanket one. Here to attach custom mapping and the
// reverse direction to one pair.
[assembly: FacetMapper(typeof(UserUseCaseModel), typeof(UserResponse),
    Configuration = typeof(Api.Mappers.UserMapping),
    AfterMapConfiguration = typeof(Api.Mappers.UserMapping),
    GenerateToSource = true)]

namespace Api.Mappers;

/// <summary>
/// Custom mapping for the user pair. This type lives here, in the assembly that references both
/// layers — which is why it can name <see cref="UserUseCaseModel"/> and <see cref="UserResponse"/>
/// in one signature at all. The same configuration on <c>[Facet]</c> would have to sit in
/// Api.Contracts and would reference the application layer from there.
/// </summary>
public sealed class UserMapping
    : IFacetProjectionMapConfiguration<UserUseCaseModel, UserResponse>,
      IFacetAfterMapConfiguration<UserUseCaseModel, UserResponse>
{
    /// <summary>
    /// An expression, so it is merged into Projection's object initializer and translated by the
    /// query provider. Run the host to see it in the generated SQL.
    /// </summary>
    public static void ConfigureProjection(IFacetProjectionBuilder<UserUseCaseModel, UserResponse> builder) =>
        builder.Map(response => response.Display, user => user.Name + " #" + user.Id);

    /// <summary>
    /// Imperative, so it runs in ToUserResponse and is absent from Projection — a query cannot
    /// call it. The host prints both so the difference is visible.
    /// </summary>
    public static void AfterMap(UserUseCaseModel source, UserResponse target) =>
        target.Display = target.Display.ToUpperInvariant();
}
