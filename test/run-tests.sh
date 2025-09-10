#!/bin/bash
echo "Building and Running Facet Unit Tests..."
echo

echo "Restoring packages..."
dotnet restore test/FacetTest.sln

echo
echo "Building solution..."
dotnet build test/FacetTest.sln --configuration Release --no-restore

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo
echo "Running unit tests..."
dotnet test test/Facet.Tests --configuration Release --no-build --verbosity normal

echo
echo "Running console tests (for comparison)..."
dotnet run --project test/Facet.TestConsole --configuration Release --no-build

echo
echo "All tests completed!"