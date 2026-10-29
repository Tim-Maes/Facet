using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Api.Contracts;
using Application;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var user = new UserUseCaseModel
{
    Id = 7,
    Name = "Ada",
    Balance = 42.50m,
    CreatedUtc = new DateTime(2026, 10, 8, 9, 30, 0, DateTimeKind.Utc),
    AuditTrail = "SHOULD NOT APPEAR ANYWHERE",
    Address = new AddressUseCaseModel { Street = "1 Mill Road", City = "Cambridge" },
    Orders =
    {
        new OrderUseCaseModel { Reference = "ORD-1", Total = 19.99m },
        new OrderUseCaseModel { Reference = "ORD-2", Total = 5.50m },
    },
};

Section("1. Mapping in memory");

// The extension method is generated into Api.Mappers but placed in the facet's namespace, so it
// is in scope wherever UserResponse already is — no extra using.
UserResponse response = user.ToUserResponse();

Console.WriteLine($"  Id       {response.Id}");
Console.WriteLine($"  Name     {response.Name}");
Console.WriteLine($"  Display  {response.Display}        <- projection rule, then AfterMap upper-cased it");
Console.WriteLine($"  Address  {response.Address!.Street}, {response.Address.City}");
Console.WriteLine($"  Orders   {string.Join(", ", response.Orders.Select(o => $"{o.Reference} {o.Total:0.00}"))}");
Console.WriteLine($"  AuditTrail property exists on the contract: " +
    $"{typeof(UserResponse).GetProperty("AuditTrail") is not null}   <- excluded");

Section("2. Projecting in the database");

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
using var db = new AppDbContext(connection);
db.Database.EnsureCreated();
db.Users.Add(user);
db.SaveChanges();
db.ChangeTracker.Clear();

var query = db.Users.Where(u => u.Id == 7).SelectUserResponse();

Console.WriteLine("  The whole graph, including the custom Display rule, as one statement:");
Console.WriteLine();
foreach (var line in query.ToQueryString().Split('\n'))
    Console.WriteLine("    " + line.TrimEnd());

var fromDb = query.Single();
Console.WriteLine();
Console.WriteLine($"  Display  {fromDb.Display}            <- no AfterMap here: a query cannot call it");
Console.WriteLine($"  Orders   {fromDb.Orders.Count} loaded without any .Include()");

Section("3. Mapping back");

// ToUserUseCaseModel constructs, so it carries back only the flat members: a fresh nested child
// would read as an insert to anything tracking the original.
var constructed = response.ToUserUseCaseModel();
Console.WriteLine($"  ToUserUseCaseModel   Id {constructed.Id}, Name {constructed.Name}, " +
    $"Address {(constructed.Address is null ? "null" : "set")}   <- flat members only");

// ApplyTo writes onto something that already exists, so the nested instance keeps its identity —
// the shape an update needs.
var tracked = db.Users.Single(u => u.Id == 7);
var addressBefore = tracked.Address;

response.Display = "edited by the client";
response.Name = "Ada Lovelace";
response.Address!.City = "Oxford";
response.ApplyTo(tracked);

Console.WriteLine($"  ApplyTo              Name {tracked.Name}, Address.City {tracked.Address!.City}");
Console.WriteLine($"  nested instance unchanged: {ReferenceEquals(addressBefore, tracked.Address)}" +
    "        <- not replaced, so EF still sees one address");
Console.WriteLine($"  Orders untouched: {tracked.Orders.Count} still there" +
    "             <- collections are never reconciled");

Section("4. What Api.Contracts actually references");

Console.WriteLine("  The reason for all of the above. Facet_GenerateMappings=false removed the");
Console.WriteLine("  members that named Application types, Facet_EmitAttributeMetadata=false removed");
Console.WriteLine("  the attributes holding typeof(...), and NestedFacets rewrote the nested property");
Console.WriteLine("  types — so nothing is left for the compiler to keep a reference for.");
Console.WriteLine();

foreach (var name in AssemblyReferencesOf(typeof(UserResponse).Assembly.Location))
    Console.WriteLine($"    {name}");

Console.WriteLine();
Console.WriteLine("  No Application and no Facet.Attributes. System.Collections is List<T>;");
Console.WriteLine("  System.Runtime is everything else. The contract depends on the framework");
Console.WriteLine("  and nothing more, while still being generated from a type it cannot see.");

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ".PadRight(78, '='));
    Console.WriteLine();
}

static List<string> AssemblyReferencesOf(string path)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();

    return metadata.AssemblyReferences
        .Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name))
        .OrderBy(n => n)
        .ToList();
}

/// <summary>EF Core over the application models, as an infrastructure layer would have it.</summary>
internal sealed class AppDbContext(SqliteConnection connection) : DbContext
{
    public DbSet<UserUseCaseModel> Users => Set<UserUseCaseModel>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSqlite(connection);

    protected override void OnModelCreating(ModelBuilder builder) =>
        builder.Entity<UserUseCaseModel>().OwnsOne(u => u.Address);
}
