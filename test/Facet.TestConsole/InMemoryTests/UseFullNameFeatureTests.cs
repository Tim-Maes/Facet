using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests
{
    public class UseFullNameFeatureTests : ITestSuite
    {
        public Task<List<(string name, bool passed)>> RunTestsAsync()
        {
            var results = new List<(string name, bool passed)>();

            try
            {
                TestGeneratedTypesFullName();
                results.Add(("Generated Types with UseFullName", true));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] FullName Feature: {ex.Message}");
                results.Add(("Generated Types with UseFullName", false));
            }

            return Task.FromResult(results);
        }

        private static void TestGeneratedTypesFullName()
        {
            Console.WriteLine("9. Testing Generated Types with UseFullName = true:");
            Console.WriteLine("================================================");

            var assembly = Assembly.GetExecutingAssembly();
            var allTypes = assembly.GetTypes();

            // types that should have UseFullName = true
            var createUsers = allTypes
                .Where(t => t.Name.Contains("Request") && t.FullName.Contains("CreateUser"))
                .ToArray();

            var updateUsers = allTypes
                .Where(t => t.Name.Contains("Request") && t.FullName.Contains("UpdateUser"))
                .ToArray();

            Console.WriteLine("Create user request types:");
            foreach (var t in createUsers)
                Console.WriteLine($"  - {t.FullName}");

            Console.WriteLine("Update user request types:");
            foreach (var t in updateUsers)
                Console.WriteLine($"  - {t.FullName}");

            // Verify is every full name is unique
            var allFullNames = createUsers.Select(t => t.FullName)
                .Concat(updateUsers.Select(t => t.FullName))
                .ToArray();

            if (allFullNames.Distinct().Count() == allFullNames.Length)
                Console.WriteLine("All generated types have unique full names.");
            else
                throw new Exception("Conflict detected in generated types!");

            // Test Facet mapping
            Console.WriteLine("Testing ToFacet mappings for full name DTOs...");

            var createUserSample = TestDataFactory.CreateSampleUsers().First();
            var createUserRequest = createUserSample.ToFacet<User, CreateUser.Request>();
            Console.WriteLine($"  SUCCESS: Mapped {createUserSample.CreatedAt} -> {createUserRequest.GetType().FullName}");

            var updateUserSample = TestDataFactory.CreateSampleUsers().Skip(1).First();
            var updateUserRequest = updateUserSample.ToFacet<User, UpdateUser.Request>();
            Console.WriteLine($"  SUCCESS: Mapped {updateUserSample.FirstName} -> {updateUserRequest.GetType().FullName}");

            Console.WriteLine("UseFullName feature tests passed successfully!");
        }
    }
}
