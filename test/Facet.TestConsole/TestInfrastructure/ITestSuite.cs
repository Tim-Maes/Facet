using System.Collections.Generic;
using System.Threading.Tasks;

namespace Facet.TestConsole.TestInfrastructure;

public interface ITestSuite
{
    Task<List<(string name, bool passed)>> RunTestsAsync();
}