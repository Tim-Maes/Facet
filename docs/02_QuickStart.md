# Quick Start Guide

This guide will help you get up and running with Facet in just a few steps.

## 1. Install the NuGet Package
dotnet add package Facet
## 2. Define Your Source Model
public class Person
{
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
}
## 3. Create a Facet (DTO/Projection)
using Facet;

// Class
[Facet(typeof(Person), exclude: nameof(Person.Email))]
public partial class PersonDto { }

// Record
[Facet(typeof(Person), FacetKind.Record)]
public partial record PersonDto { }

// Struct
[Facet(typeof(Person), FacetKind.Struct)]
public partial struct PersonDto { }
## 4. Use the Generated Type
var person = new Person { Name = "Alice", Email = "a@b.com", Age = 30 };

var dto = new PersonDto(person); // Uses generated constructor
## 5. LINQ Integration
var query = dbContext.People.Select(PersonDto.Projection).ToList();
Or with built-in extension methods:
using Facet.Extensions;

var dto = person.ToFacet<Person, PersonDto>();

var dtos = personList.SelectFacets<Person, PersonDto>();
---

See the [Attribute Reference](03_AttributeReference.md) and [Extension Methods](05_Extensions.md) for more details.
