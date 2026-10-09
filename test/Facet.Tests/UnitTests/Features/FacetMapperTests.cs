using System.Linq.Expressions;
using System.Reflection;
using Facet;
using Facet.Mapping;
using Facet.Tests.ExternalLib;

// Facet.Tests.ExternalLib is a contract assembly: its [Facet] types are built with
// Facet_GenerateMappings=false, so they carry property declarations and nothing that names the
// type they were shaped from. Every mapping below is therefore generated here, into this
// assembly, which is the whole point of [assembly: FacetMapper].

// One line covering every facet the contract assembly records in its manifest. No pair is named
// twice, so adding a facet over there cannot drift out of step with a registration over here.
[assembly: FacetMappersForAssembly(typeof(ExternalCustomerContract),
    Exclude = new[] { nameof(ExternalEntityContract) })]

[assembly: FacetMapper(typeof(ExternalCustomer), typeof(ExternalCustomerContract),
    Configuration = typeof(Facet.Tests.UnitTests.Features.CustomerMapping),
    BeforeMapConfiguration = typeof(Facet.Tests.UnitTests.Features.CustomerMapping),
    AfterMapConfiguration = typeof(Facet.Tests.UnitTests.Features.CustomerMapping))]

[assembly: FacetMapper(typeof(ExternalSource), typeof(ExternalSourceContract),
    nameof(ExternalSource.Description))]

[assembly: FacetMapper(typeof(ExternalDomainEntity), typeof(ExternalEntityContract),
    GenerateToSource = true,
    ToSourceConfiguration = typeof(Facet.Tests.UnitTests.Features.EntityReverseMapping))]

namespace Facet.Tests.UnitTests.Features
{
    /// <summary>
    /// Every hook on one type, to pin down the order they run in and which path each reaches.
    /// </summary>
    public sealed class CustomerMapping
        : IFacetProjectionMapConfiguration<ExternalCustomer, ExternalCustomerContract>,
          IFacetMapConfiguration<ExternalCustomer, ExternalCustomerContract>,
          IFacetBeforeMapConfiguration<ExternalCustomer, ExternalCustomerContract>,
          IFacetAfterMapConfiguration<ExternalCustomer, ExternalCustomerContract>
    {
        /// <summary>An expression, so it survives into the query the provider translates.</summary>
        public static void ConfigureProjection(
            IFacetProjectionBuilder<ExternalCustomer, ExternalCustomerContract> builder) =>
            builder.Map(contract => contract.Name, source => source.Name + "!");

        public static void BeforeMap(ExternalCustomer source, ExternalCustomerContract target) =>
            target.Id = -1;

        /// <summary>Imperative, so it runs in the mapper method and never in the projection.</summary>
        public static void Map(ExternalCustomer source, ExternalCustomerContract target) =>
            target.Name += "[map]";

        public static void AfterMap(ExternalCustomer source, ExternalCustomerContract target) =>
            target.Name += "[after]";
    }

    public sealed class EntityReverseMapping
        : IFacetToSourceConfiguration<ExternalEntityContract, ExternalDomainEntity>
    {
        public static void Map(ExternalEntityContract facet, ExternalDomainEntity target) =>
            target.Description = "reversed";
    }

    public class FacetMapperTests
    {
        [Fact]
        public void ContractAssembly_RecordsItsPairsInAManifest()
        {
            // Strings on AssemblyMetadata, so the record survives an assembly that emits no Facet
            // attributes at all — which is what makes auto-registration and FAC115 possible.
            var manifest = typeof(ExternalOrderContract).Assembly
                .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
                .Where(a => a.Key?.StartsWith("Facet.Contract.") == true)
                .ToDictionary(a => a.Key!, a => a.Value);

            manifest.Should().ContainKey("Facet.Contract.Facet.Tests.ExternalLib.ExternalOrderContract")
                .WhoseValue.Should().Be("Facet.Tests.ExternalLib.ExternalOrder");
        }

        [Fact]
        public void MappersForAssembly_RegistersEveryFacetWithoutNamingThem()
        {
            // Neither of these has an [assembly: FacetMapper] of its own — the single
            // FacetMappersForAssembly line is what generated their mappers.
            MapperType("ExternalOrderContractMapper").Should().NotBeNull();
            MapperType("ExternalAddressContractMapper").Should().NotBeNull();
        }

        [Fact]
        public void MappersForAssembly_SkipsAnExcludedFacet_LeavingItToTheExplicitRegistration()
        {
            // Excluded from the blanket line, then registered explicitly with GenerateToSource.
            var mapper = MapperType("ExternalEntityContractMapper");

            mapper.Should().NotBeNull();
            mapper!.GetMethod("ToExternalDomainEntity").Should().NotBeNull(
                "the explicit registration is the one that applies, and it sets GenerateToSource");
        }

        [Fact]
        public void ExplicitRegistration_WinsOverTheBlanketOne()
        {
            // Only one mapper exists for the customer pair, and it is the configured one: the
            // hooks ran, which the blanket registration would not have done.
            NewCustomer().ToExternalCustomerContract().Name.Should().Be("Ada![map][after]");
        }

        [Fact]
        public void Mapper_IsCompiledIntoThisAssembly_NotTheFacetsOwn()
        {
            // The whole reason the mapper is a static class and not a partial: it lands in the
            // assembly that registered it, while the facet type stays in its own.
            var mapper = typeof(FacetMapperTests).Assembly
                .GetType("Facet.Tests.ExternalLib.ExternalCustomerContractMapper");

            mapper.Should().NotBeNull("the mapper is generated here even though the facet is not");
            typeof(ExternalCustomerContract).Assembly
                .Should().NotBeSameAs(typeof(FacetMapperTests).Assembly);

            // A C# static class: abstract + sealed in metadata.
            mapper!.IsAbstract.Should().BeTrue();
            mapper.IsSealed.Should().BeTrue();
        }

        [Fact]
        public void Mapper_MapsAcrossTheAssemblyBoundary()
        {
            var contract = NewCustomer().ToExternalCustomerContract();

            contract.Id.Should().Be(7);
            contract.Address!.City.Should().Be("Cambridge");
            contract.Orders.Should().HaveCount(2);
            contract.Orders.Select(o => o.Total).Should().Equal(19.99m, 5.50m);
        }

        [Fact]
        public void Mapper_MapsNullNestedSourceToNull()
        {
            var customer = NewCustomer();
            customer.Address = null;

            customer.ToExternalCustomerContract().Address.Should().BeNull();
        }

        [Fact]
        public void Hooks_RunInOrder_AroundTheMappedMembers()
        {
            var contract = NewCustomer().ToExternalCustomerContract();

            // BeforeMap set Id to -1; the mapped members then overwrote it. That ordering is the
            // point of constructing the instance first rather than using an initializer.
            contract.Id.Should().Be(7);

            // ConfigureProjection, then Map, then AfterMap.
            contract.Name.Should().Be("Ada![map][after]");
        }

        [Fact]
        public void ProjectionConfiguration_AppliesOnTheQueryPath_WithoutTheImperativeHooks()
        {
            var contracts = new[] { NewCustomer() }.AsQueryable()
                .SelectExternalCustomerContract()
                .ToList();

            // The expression rule is in the projection; Map and AfterMap are not, because
            // imperative code cannot be translated.
            contracts[0].Name.Should().Be("Ada!");
        }

        [Fact]
        public void ProjectionConfiguration_StaysOneExpression()
        {
            // If the rule were applied after the projection rather than merged into its bindings,
            // the body would no longer be an object initializer — and a query provider would fall
            // back to loading rows and mapping them in memory.
            var projection = MapperType("ExternalCustomerContractMapper")!
                .GetProperty("Projection", BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null) as LambdaExpression;

            projection.Should().NotBeNull();
            projection!.Body.Should().BeOfType<MemberInitExpression>();
        }

        [Fact]
        public void Mapper_OffersAnOverloadTakingAConfigurationInstance()
        {
            // The attribute-declared hooks are static, so a rule needing a service has nowhere to
            // get one. This overload leaves that to the caller, where a container already lives.
            var overload = MapperType("ExternalOrderContractMapper")!
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "ToExternalOrderContract")
                .FirstOrDefault(m => m.GetParameters().Length == 2);

            overload.Should().NotBeNull();
            overload!.GetParameters()[1].ParameterType.Name
                .Should().StartWith("IFacetMapConfigurationInstance");
        }

        [Fact]
        public void ToSourceConfiguration_RunsAfterTheReverseMapping()
        {
            var entity = new ExternalDomainEntity { Id = 3, Name = "Grace", Description = "original" };

            var roundTripped = entity.ToExternalEntityContract().ToExternalDomainEntity();

            roundTripped.Name.Should().Be("Grace");
            roundTripped.Description.Should().Be("reversed", "the hook overwrites the mapped value");
        }

        [Fact]
        public void Mapper_LeavesExcludedPropertyAtItsDefault()
        {
            var contract = new ExternalSource { Description = "ignored" }.ToExternalSourceContract();

            // Its declared initializer, carried over from the source property — not the mapped
            // value, which is what being excluded means.
            contract.Description.Should().BeEmpty();
        }

        private static Type? MapperType(string name) =>
            typeof(FacetMapperTests).Assembly.GetType($"Facet.Tests.ExternalLib.{name}");

        private static ExternalCustomer NewCustomer() => new()
        {
            Id = 7,
            Name = "Ada",
            Address = new ExternalAddress { Street = "1 Mill Rd", City = "Cambridge" },
            Orders =
            {
                new ExternalOrder { Reference = Guid.NewGuid(), Total = 19.99m },
                new ExternalOrder { Reference = Guid.NewGuid(), Total = 5.50m },
            },
        };
    }
}
