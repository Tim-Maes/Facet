#!/bin/bash
set -euo pipefail

# ─── FAC109 Inner Dev Loop ──────────────────────────────────────────────────
# Builds the Facet generator + analyzer, runs the FAC109 tests, and shows
# any failures immediately. Designed for run → hack → run iteration.
#
# Usage:
#   ./devloop-fac109.sh              # build + test
#   ./devloop-fac109.sh --watch      # rebuild on file change (requires fswatch)
#   ./devloop-fac109.sh --list       # list available tests

ROOT="/Users/darrenkattan/source/repos/Facet"
TEST_PROJECT="$ROOT/test/Facet.Tests/Facet.Tests.csproj"
TEST_FILTER="FullyQualifiedName~GenerateDtosMigrationComplexityAnalyzerTests"

echo "─── Restoring NuGet packages ───"
dotnet restore "$ROOT/src/Facet/Facet.csproj" 2>&1 | tail -3
dotnet restore "$TEST_PROJECT" 2>&1 | tail -3

echo ""
echo "─── Building Facet generator + analyzer ───"
dotnet build "$ROOT/src/Facet/Facet.csproj" --no-restore 2>&1 | tail -5
BUILD_EXIT=$?

if [ $BUILD_EXIT -ne 0 ]; then
  echo "❌ Build failed — aborting tests"
  exit 1
fi

echo ""
echo "─── Running FAC109 tests ───"
TEST_OUTPUT=$(dotnet test "$TEST_PROJECT" \
  --filter "$TEST_FILTER" \
  --logger "console;verbosity=normal" \
  2>&1)
TEST_EXIT=$?
echo "$TEST_OUTPUT" | tail -40

echo ""
# dotnet test returns 0 even when no tests match the filter, so check the output
if echo "$TEST_OUTPUT" | grep -q "No test matches"; then
  echo "⚠️  No tests matched filter '$TEST_FILTER' — did you create the test class?"
  exit 1
elif [ $TEST_EXIT -eq 0 ]; then
  echo "✅ All FAC109 tests passed"
else
  echo "❌ FAC109 tests failed (exit $TEST_EXIT)"
fi

exit $TEST_EXIT
