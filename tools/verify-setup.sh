#!/bin/bash
# Recipe Engine Verification Script
# This script verifies that the recipe engine is properly configured and can run

set -e

echo "=== Recipe Engine MVP Verification ==="
echo ""

# Check MongoDB is running
echo "1. Checking MongoDB status..."
if docker ps | grep -q recipe-engine-mongodb; then
    echo "   ✅ MongoDB is running"
else
    echo "   ❌ MongoDB is not running"
    echo "   Starting MongoDB..."
    cd /home/runner/work/easy-meals/easy-meals/apps/recipe-engine
    docker compose up -d mongodb
    echo "   Waiting for MongoDB to be ready..."
    sleep 5
fi
echo ""

# Check provider configuration
echo "2. Checking Hello Fresh provider configuration..."
PROVIDER_COUNT=$(docker exec recipe-engine-mongodb mongosh -u admin -p devpassword --authenticationDatabase admin easymeals --quiet --eval "db.provider_configurations.countDocuments({providerId: 'hellofresh', enabled: true})")
if [ "$PROVIDER_COUNT" -eq "1" ]; then
    echo "   ✅ Hello Fresh provider is configured and enabled"
else
    echo "   ❌ Hello Fresh provider not found"
    echo "   Seeding provider configuration..."
    cd /home/runner/work/easy-meals/easy-meals/apps/recipe-engine
    docker exec -i recipe-engine-mongodb mongosh -u admin -p devpassword --authenticationDatabase admin < seed-hellofresh.js > /dev/null 2>&1
    echo "   ✅ Provider configuration seeded"
fi
echo ""

# Check build status
echo "3. Checking build status..."
cd /home/runner/work/easy-meals/easy-meals
if dotnet build apps/recipe-engine/EasyMeals.RecipeEngine.sln --no-restore > /dev/null 2>&1; then
    echo "   ✅ Solution builds successfully"
else
    echo "   ❌ Build failed"
    exit 1
fi
echo ""

# Run tests
echo "4. Running tests..."
TEST_OUTPUT=$(dotnet test apps/recipe-engine/EasyMeals.RecipeEngine.sln --no-build --logger "console;verbosity=quiet" 2>&1)
if echo "$TEST_OUTPUT" | grep -q "Test Run Successful"; then
    TOTAL_TESTS=$(echo "$TEST_OUTPUT" | grep "Total tests:" | awk '{print $3}')
    PASSED_TESTS=$(echo "$TEST_OUTPUT" | grep "Passed:" | awk '{print $2}')
    echo "   ✅ All tests passed ($PASSED_TESTS/$TOTAL_TESTS)"
else
    echo "   ❌ Some tests failed"
    echo "$TEST_OUTPUT" | tail -20
    exit 1
fi
echo ""

# Display provider configuration
echo "5. Provider Configuration Details:"
docker exec recipe-engine-mongodb mongosh -u admin -p devpassword --authenticationDatabase admin easymeals --quiet --eval "
db.provider_configurations.find({providerId: 'hellofresh'}).forEach(function(doc) {
    print('   Provider ID: ' + doc.providerId);
    print('   Enabled: ' + doc.enabled);
    print('   Discovery Strategy: ' + doc.discoveryStrategy);
    print('   Recipe URL: ' + doc.recipeRootUrl);
    print('   Batch Size: ' + doc.batchSize);
    print('   Time Window: ' + doc.timeWindowMinutes + ' minutes');
    print('   Rate Limit: ' + doc.maxRequestsPerMinute + ' requests/minute');
});
"
echo ""

echo "=== Verification Complete ==="
echo ""
echo "Summary:"
echo "✅ MongoDB is running and accessible"
echo "✅ Hello Fresh provider is configured"
echo "✅ Solution builds without errors"
echo "✅ All tests are passing"
echo ""
echo "The recipe engine is ready for MVP deployment!"
echo ""
echo "To run the engine manually:"
echo "  cd apps/recipe-engine/src/EasyMeals.RecipeEngine"
echo "  dotnet run"
echo ""
echo "Note: Network access to hellofresh.com is required for actual recipe processing."
