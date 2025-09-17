@echo off
echo Building and Running Facet Unit Tests...
echo.

echo Restoring packages...
dotnet restore test/FacetTest.sln

echo.
echo Building solution...
dotnet build test/FacetTest.sln --configuration Release --no-restore

if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Running unit tests...
dotnet test test/Facet.Tests --configuration Release --no-build --verbosity normal

echo.
echo All tests completed!
pause