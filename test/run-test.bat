@echo off
echo Building Facet Test Console Application...
echo.

echo Restoring packages...
dotnet restore test/FacetTest.sln

echo.
echo Building solution...
dotnet build test/FacetTest.sln --configuration Release

echo.
echo Running test console...
dotnet run --project test/Facet.TestConsole --configuration Release

echo.
echo Build and test completed!
pause