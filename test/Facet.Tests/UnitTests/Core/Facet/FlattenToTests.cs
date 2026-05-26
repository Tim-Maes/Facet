using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class FlattenToTests
{
    [Fact]
    public void FlattenTo_ShouldCombineParentAndCollectionItemProperties()
    {
        var data = new DataEntity
        {
            Id = 1,
            Name = "Parent Data",
            Description = "Parent Description",
            Extended = new List<ExtendedEntity>
            {
                new() { Id = 10, Name = "Extended 1", DataValue = 100 },
                new() { Id = 20, Name = "Extended 2", DataValue = 200 },
                new() { Id = 30, Name = "Extended 3", DataValue = 300 }
            }
        };

        var facet = new DataFacet(data);

        var flattened = facet.FlattenTo();

        Assert.NotNull(flattened);
        Assert.Equal(3, flattened.Count);

        Assert.Equal(1, flattened[0].Id);                    
        Assert.Equal("Parent Data", flattened[0].Name);      
        Assert.Equal("Parent Description", flattened[0].Description); 
        Assert.Equal(100, flattened[0].DataValue);           
        Assert.Equal("Extended 1", flattened[0].ExtendedName); 

        Assert.Equal(1, flattened[1].Id);
        Assert.Equal("Parent Data", flattened[1].Name);
        Assert.Equal("Parent Description", flattened[1].Description);
        Assert.Equal(200, flattened[1].DataValue);
        Assert.Equal("Extended 2", flattened[1].ExtendedName);

        Assert.Equal(1, flattened[2].Id);
        Assert.Equal("Parent Data", flattened[2].Name);
        Assert.Equal("Parent Description", flattened[2].Description);
        Assert.Equal(300, flattened[2].DataValue);
        Assert.Equal("Extended 3", flattened[2].ExtendedName);
    }

    [Fact]
    public void FlattenTo_WithNullCollection_ShouldReturnEmptyList()
    {
        var data = new DataEntity
        {
            Id = 1,
            Name = "Parent Data",
            Description = "Parent Description",
            Extended = null!
        };

        var facet = new DataFacet(data);

        var flattened = facet.FlattenTo();

        Assert.NotNull(flattened);
        Assert.Empty(flattened);
    }

    [Fact]
    public void FlattenTo_WithEmptyCollection_ShouldReturnEmptyList()
    {
        var data = new DataEntity
        {
            Id = 1,
            Name = "Parent Data",
            Description = "Parent Description",
            Extended = new List<ExtendedEntity>()
        };

        var facet = new DataFacet(data);

        var flattened = facet.FlattenTo();

        Assert.NotNull(flattened);
        Assert.Empty(flattened);
    }

    [Fact]
    public void FlattenTo_WithSingleCollectionItem_ShouldReturnSingleRow()
    {
        var data = new DataEntity
        {
            Id = 42,
            Name = "Single Parent",
            Description = "Single Description",
            Extended = new List<ExtendedEntity>
            {
                new() { Id = 99, Name = "Single Extended", DataValue = 777 }
            }
        };

        var facet = new DataFacet(data);

        var flattened = facet.FlattenTo();

        Assert.NotNull(flattened);
        Assert.Single(flattened);
        Assert.Equal(42, flattened[0].Id);
        Assert.Equal("Single Parent", flattened[0].Name);
        Assert.Equal("Single Description", flattened[0].Description);
        Assert.Equal(777, flattened[0].DataValue);
        Assert.Equal("Single Extended", flattened[0].ExtendedName);
    }

    [Fact]
    public void FlattenTo_ShouldReplicateParentDataForEachCollectionItem()
    {
        var data = new DataEntity
        {
            Id = 5,
            Name = "Shared Parent",
            Description = "Shared Description",
            Extended = new List<ExtendedEntity>
            {
                new() { Id = 1, Name = "Item 1", DataValue = 10 },
                new() { Id = 2, Name = "Item 2", DataValue = 20 }
            }
        };

        var facet = new DataFacet(data);

        var flattened = facet.FlattenTo();

        Assert.Equal(2, flattened.Count);
        
        foreach (var row in flattened)
        {
            Assert.Equal(5, row.Id);
            Assert.Equal("Shared Parent", row.Name);
            Assert.Equal("Shared Description", row.Description);
        }

        Assert.Equal(10, flattened[0].DataValue);
        Assert.Equal(20, flattened[1].DataValue);
    }

    [Fact]
    public void FlattenTo_WithNestedFacetsInLookupTable_ShouldFlattenThroughNestedNavigation()
    {
        var data = new TestModels.DataTableEntity
        {
            Id = 1,
            Name = "Main Data",
            ExtendedLookups = new List<TestModels.DataExtendedLookupEntity>
            {
                new()
                {
                    Id = 10,
                    DataId = 1,
                    ExtendedId = 100,
                    Extended = new TestModels.DataExtendedEntity
                    {
                        Id = 100,
                        ExtendedValue = "First Extended Value",
                        NumericValue = 42
                    }
                },
                new()
                {
                    Id = 20,
                    DataId = 1,
                    ExtendedId = 200,
                    Extended = new TestModels.DataExtendedEntity
                    {
                        Id = 200,
                        ExtendedValue = "Second Extended Value",
                        NumericValue = 99
                    }
                }
            }
        };

        var facet = new TestModels.DataTableFacet(data);

        var flattened = facet.FlattenTo();

        Assert.NotNull(flattened);
        Assert.Equal(2, flattened.Count);

        Assert.Equal(1, flattened[0].Id);                    
        Assert.Equal("Main Data", flattened[0].Name);        
        Assert.Equal(1, flattened[0].DataId);                
        Assert.Equal(100, flattened[0].ExtendedId);          

        Assert.Equal("First Extended Value", flattened[0].ExtendedValue);  
        Assert.Equal(42, flattened[0].NumericValue);                        

        Assert.Equal(1, flattened[1].Id);
        Assert.Equal("Main Data", flattened[1].Name);
        Assert.Equal(1, flattened[1].DataId);
        Assert.Equal(200, flattened[1].ExtendedId);
        Assert.Equal("Second Extended Value", flattened[1].ExtendedValue);
        Assert.Equal(99, flattened[1].NumericValue);
    }

    [Fact]
    public void FlattenTo_WithMultipleNameCollisions_ShouldUseSmartLeafNaming()
    {
        var catalog = new TestModels.ProductCatalogEntity
        {
            Id = 1,
            Name = "Main Catalog",
            ProductLookups = new List<TestModels.ProductLookupEntity>
            {
                new()
                {
                    Id = 100,
                    Category = new TestModels.CategoryEntity
                    {
                        Id = 10,
                        Name = "Electronics",
                        Code = "ELEC"
                    },
                    Supplier = new TestModels.SupplierEntity
                    {
                        Id = 20,
                        Name = "TechCorp",
                        ContactEmail = "contact@techcorp.com"
                    },
                    Brand = new TestModels.BrandEntity
                    {
                        Id = 30,
                        Name = "Samsung",
                        Country = "South Korea"
                    }
                },
                new()
                {
                    Id = 200,
                    Category = new TestModels.CategoryEntity
                    {
                        Id = 11,
                        Name = "Furniture",
                        Code = "FURN"
                    },
                    Supplier = new TestModels.SupplierEntity
                    {
                        Id = 21,
                        Name = "WoodWorks Inc",
                        ContactEmail = "sales@woodworks.com"
                    },
                    Brand = new TestModels.BrandEntity
                    {
                        Id = 31,
                        Name = "IKEA",
                        Country = "Sweden"
                    }
                }
            }
        };

        var facet = new TestModels.ProductCatalogFacet(catalog);

        var flattened = facet.FlattenTo();

        Assert.NotNull(flattened);
        Assert.Equal(2, flattened.Count);

        Assert.Equal(1, flattened[0].Id);
        Assert.Equal("Main Catalog", flattened[0].Name);           
        Assert.Equal("Electronics", flattened[0].CategoryName);     
        Assert.Equal("ELEC", flattened[0].Code);                    
        Assert.Equal("TechCorp", flattened[0].SupplierName);        
        Assert.Equal("contact@techcorp.com", flattened[0].ContactEmail); 
        Assert.Equal("Samsung", flattened[0].BrandName);            
        Assert.Equal("South Korea", flattened[0].Country);          

        Assert.Equal(1, flattened[1].Id);
        Assert.Equal("Main Catalog", flattened[1].Name);
        Assert.Equal("Furniture", flattened[1].CategoryName);
        Assert.Equal("FURN", flattened[1].Code);
        Assert.Equal("WoodWorks Inc", flattened[1].SupplierName);
        Assert.Equal("sales@woodworks.com", flattened[1].ContactEmail);
        Assert.Equal("IKEA", flattened[1].BrandName);
        Assert.Equal("Sweden", flattened[1].Country);
    }
}
