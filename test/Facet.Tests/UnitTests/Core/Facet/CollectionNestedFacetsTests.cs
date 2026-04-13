using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CollectionNestedFacetsTests
{
    [Fact]
    public void ToFacet_ShouldMapListCollection_WhenUsingNestedFacets()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-2025-001",
            OrderDate = new DateTime(2025, 1, 15),
            Items = new List<OrderItemEntity>
            {
                new() { Id = 1, ProductName = "Laptop", Price = 1200.00m, Quantity = 1 },
                new() { Id = 2, ProductName = "Mouse", Price = 25.00m, Quantity = 2 },
                new() { Id = 3, ProductName = "Keyboard", Price = 75.00m, Quantity = 1 }
            },
            ShippingAddress = new AddressEntity
            {
                Street = "123 Main St",
                City = "Seattle",
                State = "WA",
                ZipCode = "98101",
                Country = "USA"
            }
        };

        // Act
        var dto = new OrderFacet(order);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-2025-001");
        dto.OrderDate.Should().Be(new DateTime(2025, 1, 15));

        // Verify collection mapping
        dto.Items.Should().NotBeNull();
        dto.Items.Should().HaveCount(3);
        dto.Items.Should().AllBeOfType<OrderItemFacet>();

        // Verify first item
        dto.Items[0].Id.Should().Be(1);
        dto.Items[0].ProductName.Should().Be("Laptop");
        dto.Items[0].Price.Should().Be(1200.00m);
        dto.Items[0].Quantity.Should().Be(1);

        // Verify second item
        dto.Items[1].Id.Should().Be(2);
        dto.Items[1].ProductName.Should().Be("Mouse");
        dto.Items[1].Price.Should().Be(25.00m);
        dto.Items[1].Quantity.Should().Be(2);

        // Verify nested address
        dto.ShippingAddress.Should().NotBeNull();
        dto.ShippingAddress.Street.Should().Be("123 Main St");
        dto.ShippingAddress.City.Should().Be("Seattle");
    }

    [Fact]
    public void ToFacet_ShouldMapArrayCollection_WhenUsingNestedFacets()
    {
        // Arrange
        var team = new TeamEntity
        {
            Id = 10,
            Name = "Development Team",
            Members = new[]
            {
                new StaffMember
                {
                    Id = 1,
                    FirstName = "Alice",
                    LastName = "Johnson",
                    Email = "alice@example.com",
                    PasswordHash = "hash1",
                    Salary = 90000m,
                    HireDate = new DateTime(2020, 1, 1),
                    Company = new CompanyEntity { Id = 100, Name = "Tech Corp", Industry = "Technology", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "Seattle" }
                },
                new StaffMember
                {
                    Id = 2,
                    FirstName = "Bob",
                    LastName = "Smith",
                    Email = "bob@example.com",
                    PasswordHash = "hash2",
                    Salary = 95000m,
                    HireDate = new DateTime(2019, 5, 15),
                    Company = new CompanyEntity { Id = 100, Name = "Tech Corp", Industry = "Technology", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "Portland" }
                }
            }
        };

        // Act
        var dto = new TeamFacet(team);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(10);
        dto.Name.Should().Be("Development Team");

        // Verify array mapping
        dto.Members.Should().NotBeNull();
        dto.Members.Should().HaveCount(2);
        dto.Members.Should().AllBeOfType<StaffMemberFacet>();

        // Verify first member (PasswordHash and Salary should be excluded)
        dto.Members[0].Id.Should().Be(1);
        dto.Members[0].FirstName.Should().Be("Alice");
        dto.Members[0].LastName.Should().Be("Johnson");
        dto.Members[0].Email.Should().Be("alice@example.com");
        dto.Members[0].HireDate.Should().Be(new DateTime(2020, 1, 1));

        var dtoType = dto.Members[0].GetType();
        dtoType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should be excluded");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should be excluded");

        // Verify second member
        dto.Members[1].FirstName.Should().Be("Bob");
        dto.Members[1].LastName.Should().Be("Smith");
    }

    [Fact]
    public void ToFacet_ShouldMapICollectionType_WhenUsingNestedFacets()
    {
        // Arrange
        var project = new ProjectEntity
        {
            Id = 500,
            Name = "Project Phoenix",
            Teams = new List<TeamEntity>
            {
                new()
                {
                    Id = 1,
                    Name = "Backend Team",
                    Members = new[]
                    {
                        new StaffMember
                        {
                            Id = 10,
                            FirstName = "Charlie",
                            LastName = "Brown",
                            Email = "charlie@example.com",
                            PasswordHash = "hash",
                            Salary = 100000m,
                            Company = new CompanyEntity { Id = 1, Name = "Corp", Industry = "Tech", HeadquartersAddress = new AddressEntity() },
                            HomeAddress = new AddressEntity()
                        }
                    }
                },
                new()
                {
                    Id = 2,
                    Name = "Frontend Team",
                    Members = Array.Empty<StaffMember>()
                }
            }
        };

        // Act
        var dto = new ProjectFacet(project);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(500);
        dto.Name.Should().Be("Project Phoenix");

        // Verify ICollection mapping
        dto.Teams.Should().NotBeNull();
        dto.Teams.Should().HaveCount(2);
        dto.Teams.Should().AllBeOfType<TeamFacet>();

        // Verify first team
        var firstTeam = dto.Teams.ElementAt(0);
        firstTeam.Id.Should().Be(1);
        firstTeam.Name.Should().Be("Backend Team");
        firstTeam.Members.Should().HaveCount(1);
        firstTeam.Members[0].FirstName.Should().Be("Charlie");

        // Verify second team
        var secondTeam = dto.Teams.ElementAt(1);
        secondTeam.Id.Should().Be(2);
        secondTeam.Name.Should().Be("Frontend Team");
        secondTeam.Members.Should().BeEmpty();
    }

    [Fact]
    public void ToFacet_ShouldHandleEmptyCollections_WhenUsingNestedFacets()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-EMPTY",
            OrderDate = DateTime.Now,
            Items = new List<OrderItemEntity>(),
            ShippingAddress = new AddressEntity()
        };

        // Act
        var dto = new OrderFacet(order);

        // Assert
        dto.Should().NotBeNull();
        dto.Items.Should().NotBeNull();
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public void BackTo_ShouldMapListCollectionBackToSource()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-2025-001",
            OrderDate = new DateTime(2025, 1, 15),
            Items = new List<OrderItemEntity>
            {
                new() { Id = 1, ProductName = "Laptop", Price = 1200.00m, Quantity = 1 },
                new() { Id = 2, ProductName = "Mouse", Price = 25.00m, Quantity = 2 }
            },
            ShippingAddress = new AddressEntity
            {
                Street = "123 Main St",
                City = "Seattle",
                State = "WA",
                ZipCode = "98101",
                Country = "USA"
            }
        };

        var dto = new OrderFacet(order);

        // Act
        var mappedOrder = dto.ToSource();

        // Assert
        mappedOrder.Should().NotBeNull();
        mappedOrder.Id.Should().Be(1);
        mappedOrder.OrderNumber.Should().Be("ORD-2025-001");
        mappedOrder.Items.Should().HaveCount(2);
        mappedOrder.Items.Should().AllBeOfType<OrderItemEntity>();

        mappedOrder.Items[0].Id.Should().Be(1);
        mappedOrder.Items[0].ProductName.Should().Be("Laptop");
        mappedOrder.Items[0].Price.Should().Be(1200.00m);

        mappedOrder.ShippingAddress.Street.Should().Be("123 Main St");
    }

    [Fact]
    public void ToSource_ShouldMapArrayCollectionBackToSource()
    {
        // Arrange
        var team = new TeamEntity
        {
            Id = 10,
            Name = "Development Team",
            Members = new[]
            {
                new StaffMember
                {
                    Id = 1,
                    FirstName = "Alice",
                    LastName = "Johnson",
                    Email = "alice@example.com",
                    PasswordHash = "hash1",
                    Salary = 90000m,
                    HireDate = new DateTime(2020, 1, 1),
                    Company = new CompanyEntity { Id = 100, Name = "Tech Corp", Industry = "Technology", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "Seattle" }
                }
            }
        };

        var dto = new TeamFacet(team);

        // Act
        var mappedTeam = dto.ToSource();

        // Assert
        mappedTeam.Should().NotBeNull();
        mappedTeam.Id.Should().Be(10);
        mappedTeam.Name.Should().Be("Development Team");
        mappedTeam.Members.Should().HaveCount(1);
        mappedTeam.Members.Should().BeOfType<StaffMember[]>();

        mappedTeam.Members[0].FirstName.Should().Be("Alice");
        mappedTeam.Members[0].LastName.Should().Be("Johnson");
    }

    [Fact]
    public void ToSource_ShouldMapICollectionBackToSource()
    {
        // Arrange
        var project = new ProjectEntity
        {
            Id = 500,
            Name = "Project Phoenix",
            Teams = new List<TeamEntity>
            {
                new() { Id = 1, Name = "Backend Team", Members = Array.Empty<StaffMember>() },
                new() { Id = 2, Name = "Frontend Team", Members = Array.Empty<StaffMember>() }
            }
        };

        var dto = new ProjectFacet(project);

        // Act
        var mappedProject = dto.ToSource();

        // Assert
        mappedProject.Should().NotBeNull();
        mappedProject.Id.Should().Be(500);
        mappedProject.Name.Should().Be("Project Phoenix");
        mappedProject.Teams.Should().HaveCount(2);
        mappedProject.Teams.Should().AllBeOfType<TeamEntity>();

        mappedProject.Teams.ElementAt(0).Name.Should().Be("Backend Team");
        mappedProject.Teams.ElementAt(1).Name.Should().Be("Frontend Team");
    }

    [Fact]
    public void Collection_ShouldPreserveTypeFromSource()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-001",
            OrderDate = DateTime.Now,
            Items = new List<OrderItemEntity>
            {
                new() { Id = 1, ProductName = "Item1", Price = 10, Quantity = 1 }
            },
            ShippingAddress = new AddressEntity()
        };

        // Act
        var dto = new OrderFacet(order);

        // Assert - Items should be List<OrderItemFacet>
        dto.Items.Should().BeAssignableTo<List<OrderItemFacet>>();
    }

    [Fact]
    public void ToFacet_ShouldMapIReadOnlyListCollection_WhenUsingNestedFacets()
    {
        // Arrange
        var library = new LibraryEntity
        {
            Id = 1,
            Name = "City Library",
            Books = new List<LibraryBookEntity>
            {
                new() { Id = 1, Title = "1984", Author = "George Orwell", ISBN = "978-0451524935" },
                new() { Id = 2, Title = "Brave New World", Author = "Aldous Huxley", ISBN = "978-0060850524" },
                new() { Id = 3, Title = "Fahrenheit 451", Author = "Ray Bradbury", ISBN = "978-1451673319" }
            },
            Staff = new List<StaffMember>
            {
                new()
                {
                    Id = 100,
                    FirstName = "Jane",
                    LastName = "Doe",
                    Email = "jane@library.com",
                    PasswordHash = "hash123",
                    Salary = 50000m,
                    HireDate = new DateTime(2020, 6, 1),
                    Company = new CompanyEntity { Id = 1, Name = "Library Corp", Industry = "Education", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "New York" }
                }
            }
        };

        // Act
        var dto = new LibraryFacet(library);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("City Library");

        // Verify IReadOnlyList<T> mapping
        dto.Books.Should().NotBeNull();
        dto.Books.Should().HaveCount(3);
        dto.Books.Should().AllBeOfType<LibraryBookFacet>();
        dto.Books.Should().BeAssignableTo<IReadOnlyList<LibraryBookFacet>>();

        // Verify first book
        dto.Books[0].Id.Should().Be(1);
        dto.Books[0].Title.Should().Be("1984");
        dto.Books[0].Author.Should().Be("George Orwell");
        dto.Books[0].ISBN.Should().Be("978-0451524935");

        // Verify IReadOnlyCollection<T> mapping
        dto.Staff.Should().NotBeNull();
        dto.Staff.Should().HaveCount(1);
        dto.Staff.Should().AllBeOfType<StaffMemberFacet>();
        dto.Staff.Should().BeAssignableTo<IReadOnlyCollection<StaffMemberFacet>>();

        var staffMember = dto.Staff.First();
        staffMember.Id.Should().Be(100);
        staffMember.FirstName.Should().Be("Jane");
        staffMember.LastName.Should().Be("Doe");
        staffMember.Email.Should().Be("jane@library.com");

        // Verify excluded properties
        var staffType = staffMember.GetType();
        staffType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should be excluded");
        staffType.GetProperty("Salary").Should().BeNull("Salary should be excluded");
    }

    [Fact]
    public void ToSource_ShouldMapIReadOnlyListCollectionBackToSource()
    {
        // Arrange
        var library = new LibraryEntity
        {
            Id = 1,
            Name = "City Library",
            Books = new List<LibraryBookEntity>
            {
                new() { Id = 1, Title = "1984", Author = "George Orwell", ISBN = "978-0451524935" },
                new() { Id = 2, Title = "Brave New World", Author = "Aldous Huxley", ISBN = "978-0060850524" }
            },
            Staff = new List<StaffMember>()
        };

        var dto = new LibraryFacet(library);

        // Act
        var mappedLibrary = dto.ToSource();

        // Assert
        mappedLibrary.Should().NotBeNull();
        mappedLibrary.Id.Should().Be(1);
        mappedLibrary.Name.Should().Be("City Library");

        // Verify IReadOnlyList mapping back
        mappedLibrary.Books.Should().NotBeNull();
        mappedLibrary.Books.Should().HaveCount(2);
        mappedLibrary.Books.Should().AllBeOfType<LibraryBookEntity>();
        mappedLibrary.Books.Should().BeAssignableTo<IReadOnlyList<LibraryBookEntity>>();

        mappedLibrary.Books[0].Id.Should().Be(1);
        mappedLibrary.Books[0].Title.Should().Be("1984");
        mappedLibrary.Books[0].Author.Should().Be("George Orwell");

        // Verify IReadOnlyCollection mapping back
        mappedLibrary.Staff.Should().NotBeNull();
        mappedLibrary.Staff.Should().BeEmpty();
        mappedLibrary.Staff.Should().BeAssignableTo<IReadOnlyCollection<StaffMember>>();
    }

    [Fact]
    public void IReadOnlyList_ShouldWorkWithNestedFacets_ReproducesIssue218()
    {
        // This test reproduces the exact scenario from GitHub issue #218
        // Arrange
        var bob = new Bob
        {
            ReadOnlyRelationships = new List<BobChild>
            {
                new() { Name = "Alice" },
                new() { Name = "Charlie" }
            },
            Relationships = new List<BobChild>
            {
                new() { Name = "Dave" },
                new() { Name = "Eve" }
            }
        };

        // Act
        var bobModel = new BobModel(bob);

        // Assert
        bobModel.Should().NotBeNull();

        // Verify IReadOnlyList<BobChild> was correctly mapped to IReadOnlyList<BobChildModel>
        bobModel.ReadOnlyRelationships.Should().NotBeNull();
        bobModel.ReadOnlyRelationships.Should().HaveCount(2);
        bobModel.ReadOnlyRelationships.Should().AllBeOfType<BobChildModel>();
        bobModel.ReadOnlyRelationships.Should().BeAssignableTo<IReadOnlyList<BobChildModel>>();
        bobModel.ReadOnlyRelationships[0].Name.Should().Be("Alice");
        bobModel.ReadOnlyRelationships[1].Name.Should().Be("Charlie");

        // Verify List<BobChild> was correctly mapped to List<BobChildModel>
        bobModel.Relationships.Should().NotBeNull();
        bobModel.Relationships.Should().HaveCount(2);
        bobModel.Relationships.Should().AllBeOfType<BobChildModel>();
        bobModel.Relationships.Should().BeAssignableTo<List<BobChildModel>>();
        bobModel.Relationships[0].Name.Should().Be("Dave");
        bobModel.Relationships[1].Name.Should().Be("Eve");
    }

    [Fact]
    public void ToFacet_ShouldMapImmutableCollections_WhenUsingNestedFacets()
    {
        // Arrange
        var museum = new MuseumEntity
        {
            Id = 1,
            Name = "National Museum",
            Artifacts = System.Collections.Immutable.ImmutableList.Create(
                new MuseumArtifactEntity { Id = 1, Name = "Ancient Vase", Description = "Greek pottery", YearAcquired = 1950 },
                new MuseumArtifactEntity { Id = 2, Name = "Medieval Sword", Description = "15th century weapon", YearAcquired = 1975 }
            ),
            Curators = System.Collections.Immutable.ImmutableArray.Create(
                new StaffMember
                {
                    Id = 10,
                    FirstName = "Dr. Sarah",
                    LastName = "Johnson",
                    Email = "sarah@museum.com",
                    PasswordHash = "hash",
                    Salary = 80000m,
                    HireDate = new DateTime(2015, 3, 1),
                    Company = new CompanyEntity { Id = 1, Name = "Museum Corp", Industry = "Culture", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "Boston" }
                }
            ),
            ArchiveBooks = System.Collections.Immutable.ImmutableList.Create(
                new LibraryBookEntity { Id = 1, Title = "History of Art", Author = "E.H. Gombrich", ISBN = "978-0714832470" }
            )
        };

        // Act
        var dto = new MuseumFacet(museum);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("National Museum");

        // Verify ImmutableList<T> mapping
        dto.Artifacts.Should().NotBeNull();
        dto.Artifacts.Should().HaveCount(2);
        dto.Artifacts.Should().AllBeOfType<MuseumArtifactFacet>();
        dto.Artifacts.Should().BeAssignableTo<System.Collections.Immutable.ImmutableList<MuseumArtifactFacet>>();

        dto.Artifacts[0].Id.Should().Be(1);
        dto.Artifacts[0].Name.Should().Be("Ancient Vase");
        dto.Artifacts[0].Description.Should().Be("Greek pottery");
        dto.Artifacts[0].YearAcquired.Should().Be(1950);

        dto.Artifacts[1].Id.Should().Be(2);
        dto.Artifacts[1].Name.Should().Be("Medieval Sword");

        // Verify ImmutableArray<T> mapping
        dto.Curators.Should().NotBeNull();
        dto.Curators.Should().HaveCount(1);
        dto.Curators.Should().AllBeOfType<StaffMemberFacet>();

        var curator = dto.Curators[0];
        curator.Id.Should().Be(10);
        curator.FirstName.Should().Be("Dr. Sarah");
        curator.LastName.Should().Be("Johnson");
        curator.Email.Should().Be("sarah@museum.com");

        // Verify excluded properties
        var curatorType = curator.GetType();
        curatorType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should be excluded");
        curatorType.GetProperty("Salary").Should().BeNull("Salary should be excluded");

        // Verify IImmutableList<T> mapping
        dto.ArchiveBooks.Should().NotBeNull();
        dto.ArchiveBooks.Should().HaveCount(1);
        dto.ArchiveBooks.Should().AllBeOfType<LibraryBookFacet>();
        dto.ArchiveBooks.Should().BeAssignableTo<System.Collections.Immutable.IImmutableList<LibraryBookFacet>>();

        var book = dto.ArchiveBooks[0];
        book.Id.Should().Be(1);
        book.Title.Should().Be("History of Art");
        book.Author.Should().Be("E.H. Gombrich");
    }

    [Fact]
    public void ToSource_ShouldMapImmutableCollectionsBackToSource()
    {
        // Arrange
        var museum = new MuseumEntity
        {
            Id = 1,
            Name = "National Museum",
            Artifacts = System.Collections.Immutable.ImmutableList.Create(
                new MuseumArtifactEntity { Id = 1, Name = "Ancient Vase", Description = "Greek pottery", YearAcquired = 1950 }
            ),
            Curators = System.Collections.Immutable.ImmutableArray.Create(
                new StaffMember
                {
                    Id = 10,
                    FirstName = "Dr. Sarah",
                    LastName = "Johnson",
                    Email = "sarah@museum.com",
                    PasswordHash = "hash",
                    Salary = 80000m,
                    HireDate = new DateTime(2015, 3, 1),
                    Company = new CompanyEntity { Id = 1, Name = "Museum Corp", Industry = "Culture", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "Boston" }
                }
            ),
            ArchiveBooks = System.Collections.Immutable.ImmutableList.Create(
                new LibraryBookEntity { Id = 1, Title = "History of Art", Author = "E.H. Gombrich", ISBN = "978-0714832470" }
            )
        };

        var dto = new MuseumFacet(museum);

        // Act
        var mappedMuseum = dto.ToSource();

        // Assert
        mappedMuseum.Should().NotBeNull();
        mappedMuseum.Id.Should().Be(1);
        mappedMuseum.Name.Should().Be("National Museum");

        // Verify ImmutableList mapping back
        mappedMuseum.Artifacts.Should().NotBeNull();
        mappedMuseum.Artifacts.Should().HaveCount(1);
        mappedMuseum.Artifacts.Should().AllBeOfType<MuseumArtifactEntity>();
        mappedMuseum.Artifacts.Should().BeAssignableTo<System.Collections.Immutable.ImmutableList<MuseumArtifactEntity>>();

        mappedMuseum.Artifacts[0].Id.Should().Be(1);
        mappedMuseum.Artifacts[0].Name.Should().Be("Ancient Vase");
        mappedMuseum.Artifacts[0].Description.Should().Be("Greek pottery");
        mappedMuseum.Artifacts[0].YearAcquired.Should().Be(1950);

        // Verify ImmutableArray mapping back
        mappedMuseum.Curators.Should().NotBeNull();
        mappedMuseum.Curators.Should().HaveCount(1);
        mappedMuseum.Curators.Should().AllBeOfType<StaffMember>();

        var curator = mappedMuseum.Curators[0];
        curator.Id.Should().Be(10);
        curator.FirstName.Should().Be("Dr. Sarah");
        curator.LastName.Should().Be("Johnson");

        // Verify IImmutableList mapping back
        mappedMuseum.ArchiveBooks.Should().NotBeNull();
        mappedMuseum.ArchiveBooks.Should().HaveCount(1);
        mappedMuseum.ArchiveBooks.Should().AllBeOfType<LibraryBookEntity>();
        mappedMuseum.ArchiveBooks.Should().BeAssignableTo<System.Collections.Immutable.IImmutableList<LibraryBookEntity>>();

        mappedMuseum.ArchiveBooks[0].Id.Should().Be(1);
        mappedMuseum.ArchiveBooks[0].Title.Should().Be("History of Art");
    }

    [Fact]
    public void ToFacet_ShouldHandleEmptyImmutableCollections()
    {
        // Arrange
        var museum = new MuseumEntity
        {
            Id = 1,
            Name = "Empty Museum",
            Artifacts = System.Collections.Immutable.ImmutableList<MuseumArtifactEntity>.Empty,
            Curators = System.Collections.Immutable.ImmutableArray<StaffMember>.Empty,
            ArchiveBooks = System.Collections.Immutable.ImmutableList<LibraryBookEntity>.Empty
        };

        // Act
        var dto = new MuseumFacet(museum);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Empty Museum");

        dto.Artifacts.Should().NotBeNull();
        dto.Artifacts.Should().BeEmpty();
        dto.Artifacts.Should().BeAssignableTo<System.Collections.Immutable.ImmutableList<MuseumArtifactFacet>>();

        dto.Curators.Should().NotBeNull();
        dto.Curators.Should().BeEmpty();

        dto.ArchiveBooks.Should().NotBeNull();
        dto.ArchiveBooks.Should().BeEmpty();
        dto.ArchiveBooks.Should().BeAssignableTo<System.Collections.Immutable.IImmutableList<LibraryBookFacet>>();
    }

    [Fact]
    public void ToFacet_ShouldMapCustomCollectionTypes_AutomaticallyViaInterfaceDetection()
    {
        // Arrange - custom collection type
        var gallery = new GalleryEntity
        {
            Id = 1,
            Name = "Modern Art Gallery",
            Exhibits = new CustomReadOnlyList<MuseumArtifactEntity>(new[]
            {
                new MuseumArtifactEntity { Id = 1, Name = "Starry Night", Description = "Van Gogh painting", YearAcquired = 1941 },
                new MuseumArtifactEntity { Id = 2, Name = "The Scream", Description = "Edvard Munch painting", YearAcquired = 1910 }
            })
        };

        // Act (this should work automatically via interface detection)
        var facet = new GalleryFacet(gallery);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Modern Art Gallery");

        // Verify that the custom collection was detected as IReadOnlyList and mapped correctly
        facet.Exhibits.Should().NotBeNull();
        facet.Exhibits.Should().HaveCount(2);
        facet.Exhibits.Should().AllBeOfType<MuseumArtifactFacet>();

        // The custom collection implements IReadOnlyList, so it should be detected as such
        facet.Exhibits.Should().BeAssignableTo<System.Collections.Generic.IReadOnlyList<MuseumArtifactFacet>>();

        facet.Exhibits[0].Name.Should().Be("Starry Night");
        facet.Exhibits[1].Name.Should().Be("The Scream");
    }

}
