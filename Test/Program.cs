using Facet;
// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

[Facet(typeof(Person), Exclude = new[] { "Name" }, IncludeFields = true, GenerateConstructor = true)]
public partial class PersonDto
{

}