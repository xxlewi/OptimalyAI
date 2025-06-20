#!/bin/bash

# Build Performance Analysis Tool for OptimalyAI
# Measures and analyzes build times to identify bottlenecks

echo "================================================"
echo "OptimalyAI Build Performance Analyzer"
echo "================================================"
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Clean previous results
rm -f build-analysis.log

# Function to measure time
measure_time() {
    local start=$(date +%s.%N)
    "$@"
    local end=$(date +%s.%N)
    echo $(echo "$end - $start" | bc)
}

echo "1. Cleaning build artifacts..."
dotnet clean -v minimal > /dev/null 2>&1

echo ""
echo "2. Measuring NuGet restore time..."
RESTORE_TIME=$(measure_time dotnet restore --force --no-cache)
echo -e "${YELLOW}NuGet Restore Time: ${RESTORE_TIME}s${NC}"

echo ""
echo "3. Measuring build time (without restore)..."
BUILD_TIME=$(measure_time dotnet build --no-restore --configuration Debug -v minimal)
echo -e "${YELLOW}Build Time (no restore): ${BUILD_TIME}s${NC}"

echo ""
echo "4. Measuring full build time (with restore)..."
dotnet clean -v minimal > /dev/null 2>&1
FULL_BUILD_TIME=$(measure_time dotnet build --configuration Debug -v minimal)
echo -e "${YELLOW}Full Build Time: ${FULL_BUILD_TIME}s${NC}"

echo ""
echo "5. Analyzing build with detailed logging..."
dotnet build --no-restore --configuration Debug -v diag > build-analysis.log 2>&1

# Extract slowest targets
echo ""
echo "6. Identifying slowest build targets..."
echo "================================================"

# Parse the build log for target timings
if [ -f build-analysis.log ]; then
    echo "Top 10 Slowest Targets:"
    grep -E "Target \".*\" in project.*Done|Target \".*\" skipped" build-analysis.log | \
        grep -E "[0-9]+\.[0-9]+ ms" | \
        sed -E 's/.*Target "([^"]+)".*\(([0-9]+\.[0-9]+) ms\).*/\2 \1/' | \
        sort -rn | \
        head -10 | \
        while read time target; do
            printf "${RED}%-10s ms - %s${NC}\n" "$time" "$target"
        done
fi

echo ""
echo "7. Project Dependency Analysis..."
echo "================================================"

# Count project references
echo "Project Reference Count:"
find . -name "*.csproj" -exec basename {} \; | while read proj; do
    count=$(grep -c "ProjectReference" "$proj" 2>/dev/null || echo 0)
    if [ $count -gt 0 ]; then
        echo "  $proj: $count references"
    fi
done

echo ""
echo "8. File Count Analysis..."
echo "================================================"

CS_COUNT=$(find . -name "*.cs" -not -path "./bin/*" -not -path "./obj/*" -not -path "./OAI.*/bin/*" -not -path "./OAI.*/obj/*" | wc -l)
RAZOR_COUNT=$(find . -name "*.cshtml" | wc -l)
JS_COUNT=$(find ./wwwroot -name "*.js" 2>/dev/null | wc -l)
CSS_COUNT=$(find ./wwwroot -name "*.css" 2>/dev/null | wc -l)

echo "Source Files:"
echo "  C# Files: $CS_COUNT"
echo "  Razor Views: $RAZOR_COUNT"
echo "  JavaScript Files: $JS_COUNT"
echo "  CSS Files: $CSS_COUNT"

echo ""
echo "9. Build Optimization Recommendations..."
echo "================================================"

# Check if using optimized settings
if [ ! -f "Directory.Build.props" ]; then
    echo -e "${RED}⚠ Missing Directory.Build.props - global optimizations not applied${NC}"
fi

if [ ! -f "NuGet.config" ]; then
    echo -e "${RED}⚠ Missing NuGet.config - package restore not optimized${NC}"
fi

# Check for incremental build
if grep -q "false" OptimalyAI.csproj | grep -q "GenerateDocumentationFile"; then
    echo -e "${GREEN}✓ Documentation generation disabled${NC}"
else
    echo -e "${YELLOW}⚠ Consider disabling GenerateDocumentationFile for faster builds${NC}"
fi

# Check CPU usage
CPU_COUNT=$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)
echo ""
echo -e "${GREEN}Available CPU cores: $CPU_COUNT${NC}"
echo "Ensure MSBUILDNODECOUNT=$CPU_COUNT for parallel builds"

echo ""
echo "10. Summary..."
echo "================================================"
TOTAL_TIME=$(echo "$RESTORE_TIME + $BUILD_TIME" | bc)
echo -e "${GREEN}Total Build Time (restore + build): ${TOTAL_TIME}s${NC}"
echo ""
echo "To improve build performance:"
echo "1. Use ./run-dev-optimized.py for hot reload development"
echo "2. Enable build caching with incremental builds"
echo "3. Consider using a local NuGet cache"
echo "4. Exclude unnecessary files from compilation"
echo "5. Use SSD storage for development"
echo ""

# Clean up
rm -f build-analysis.log