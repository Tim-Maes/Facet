# write_tests.md - MCPlatform Testing Guide

You are tasked with writing tests for the referenced feature, code defined by an implementation plan, or referenced file(s).

## Initial Response

When this command is invoked:

1. **Check if parameters were provided**:
   - If a feature idea, description, context, or rough specification was provided as a parameter, begin the discovery process with that context
   - If files are referenced, read them FULLY first to understand existing context
   - If no parameters provided, respond with the default prompt below

2. IMPORTANT: **If no parameters provided**, respond with:
```
I'm ready to help you define and write tests. Please provide a feature, implementation plan, file path(s) or directory, and I will analyze it thoroughly by exploring the codebase and proceed to write tests for it.

What feature or capability are you considering? This could be:
- A rough idea ("users need better ways to...")
- A specific feature request ("add support for...")
- A problem you've observed ("customers are struggling with...")
- A business opportunity ("we could provide value by...")

Don't worry about having all the details - we'll explore, refine, and create comprehensive specifications together!

Tip: You can also invoke this command with context: `/write_tests 09-example-feature`
```

Then wait for the user's input.

## Core Testing Principles


### 1. NEVER Mock What You're Testing
- **DON'T** mock the database, server actions, or oRPC calls.
- **DON'T** create fake implementations of core functionality
- **DON'T** duplicate existing code in the codebase. For example, if you are testing validateion for a schema, don't re-create it in the test file; import it from the code base. If it is not exported or is otherwise inaccessible, ask the user for permission to export or extract it.
- **DO** test the actual implementation that the application uses
- **DO** use real database connections and real auth flows

### 2. Write Integration Tests, Not Unit Tests
- Test the complete flow as users experience it
- If the app uses oRPC actions, test those actions directly by calling them as functions.
- Don't test "layers" - test features and functionality

### 3. One Test File Per Feature
- Name test files after the feature, not the implementation detail
- Place tests in `packages/dashboard/tests/[feature]`, or `packages/dashboard/tests/[feature]/[sub-feature]` for sub-features
- All test files should end with `.test.ts`
- Don't create separate test files for "actions", "schemas", "validation" etc.

## Test Setup

### Database Setup and Resource Tracking
```typescript
import { afterAll, beforeAll, beforeEach, describe, expect, test } from 'bun:test'
import { db, schema } from 'database'
import { nanoid } from 'common/nanoid'

// CRITICAL: Track all created resources for cleanup
const createdResources = {
    organizations: new Set<string>(),
    servers: new Set<string>(),
    walkthroughs: new Set<string>(),
    assignments: new Set<string>(),
    steps: new Set<string>(),
    users: new Set<string>(),
    sessions: new Set<string>()
}

// Use the real database - tests should run against a test database
// Create real records and TRACK THEM
const [testOrg] = await db.insert(schema.organization).values({
    id: `org_${nanoid(8)}`,
    name: 'Test Organization',
    slug: 'test-org',
    createdAt: new Date()
}).returning()
createdResources.organizations.add(testOrg.id)
```

### Authentication Setup
```typescript
// Create real sessions and users in the database AND TRACK THEM
const [testUser] = await db.insert(schema.user).values({
    id: `user_${nanoid(8)}`,
    email: 'test@example.com',
    emailVerified: true,
    name: 'Test User',
    createdAt: new Date(),
    updatedAt: new Date()
}).returning()
createdResources.users.add(testUser.id)

const [testSession] = await db.insert(schema.session).values({
    id: `sess_${nanoid(8)}`,
    userId: testUser.id,
    activeOrganizationId: testOrg.id,
    expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000),
    token: `token_${nanoid(16)}`,
    ipAddress: '127.0.0.1',
    userAgent: 'test-agent',
    createdAt: new Date(),
    updatedAt: new Date()
}).returning()
createdResources.sessions.add(testSession.id)
```

## Testing oRPC Actions

### Test the Actual Actions
```typescript
import { createWalkthroughAction } from '@/lib/orpc/actions'

// DON'T mock requireSession - set up real session data
// The action will read from the real database

test('should create walkthrough', async () => {
    // Call the actual action
    const [error, result] = await createWalkthroughAction({
        title: 'Test Walkthrough',
        type: 'course',
        isPublished: false
    })
    
    // Verify in the real database
    const created = await db.select()
        .from(schema.walkthroughs)
        .where(eq(schema.walkthroughs.id, result.id))
    
    expect(created[0].title).toBe('Test Walkthrough')
})
```

### Testing with Authentication Context
For actions that require authentication, you need to set up the auth context properly:

```typescript
import { headers } from 'next/headers'

// If testing server actions that use cookies/headers, you may need to:
// 1. Run tests in an environment that supports Next.js context
// 2. Or restructure actions to accept session as a parameter for testability
```

## Test Structure

### Good Test File Structure
```typescript
import { afterAll, beforeAll, beforeEach, describe, expect, test } from 'bun:test'
import { db, schema } from 'database'
import { nanoid } from 'common/nanoid'
import { eq } from 'drizzle-orm'
import { actualActionFromApp } from '@/lib/orpc/actions'

describe('Feature: Walkthrough Management', () => {
    // Track created resources for cleanup - USE SETS TO AVOID DUPLICATES
    const createdResources = {
        organizations: new Set<string>(),
        servers: new Set<string>(),
        walkthroughs: new Set<string>(),
        assignments: new Set<string>(),
        steps: new Set<string>(),
        users: new Set<string>(),
        sessions: new Set<string>()
    }
    
    beforeAll(async () => {
        // Set up test data using real database
        // TRACK EVERY RESOURCE YOU CREATE
    })
    
    afterAll(async () => {
        // Clean up ONLY the specific resources we created
        // Delete in reverse order of foreign key dependencies
        
        // Delete assignments first (they reference servers and walkthroughs)
        for (const serverId of createdResources.servers) {
            await db.delete(schema.mcpServerWalkthroughs)
                .where(eq(schema.mcpServerWalkthroughs.mcpServerId, serverId))
        }
        
        // Delete steps (they reference walkthroughs)
        for (const stepId of createdResources.steps) {
            await db.delete(schema.walkthroughSteps)
                .where(eq(schema.walkthroughSteps.id, stepId))
        }
        
        // Delete walkthroughs
        for (const walkthroughId of createdResources.walkthroughs) {
            await db.delete(schema.walkthroughs)
                .where(eq(schema.walkthroughs.id, walkthroughId))
        }
        
        // Delete servers
        for (const serverId of createdResources.servers) {
            await db.delete(schema.mcpServers)
                .where(eq(schema.mcpServers.id, serverId))
        }
        
        // Delete sessions before users
        for (const sessionId of createdResources.sessions) {
            await db.delete(schema.session)
                .where(eq(schema.session.id, sessionId))
        }
        
        // Delete users
        for (const userId of createdResources.users) {
            await db.delete(schema.user)
                .where(eq(schema.user.id, userId))
        }
        
        // Delete organizations last
        for (const orgId of createdResources.organizations) {
            await db.delete(schema.organization)
                .where(eq(schema.organization.id, orgId))
        }
    })
    
    test('complete user flow for creating and editing walkthrough', async () => {
        // Test the actual flow a user would experience
        // Use real actions, verify real database state
        // TRACK EVERY RESOURCE CREATED IN THE TEST
    })
})
```

## What NOT to Do

### Bad Example - Mocking Everything
```typescript
// DON'T DO THIS
const mockDb = {
    insert: jest.fn().mockReturnValue({
        values: jest.fn().mockReturnValue({
            returning: jest.fn().mockResolvedValue([{ id: 'fake' }])
        })
    })
}

// DON'T DO THIS
spyOn(authModule, 'requireSession').mockResolvedValue(mockSession)
```

### Bad Example - Deleting Entire Tables
```typescript
// NEVER DO THIS - This is destructive and affects other tests
beforeEach(async () => {
    await db.delete(schema.mcpServerWalkthroughs)  // WRONG!
    await db.delete(schema.walkthroughs)           // WRONG!
    await db.delete(schema.mcpServers)             // WRONG!
    await db.delete(schema.organization)           // WRONG!
})

// DO THIS INSTEAD - Track and clean up specific resources
const createdResources = {
    servers: new Set<string>()
}

test('...', async () => {
    const [server] = await db.insert(schema.mcpServers).values({...}).returning()
    createdResources.servers.add(server.id)  // Track it!
})

afterAll(async () => {
    for (const serverId of createdResources.servers) {
        await db.delete(schema.mcpServers)
            .where(eq(schema.mcpServers.id, serverId))  // Delete only what we created
    }
})
```

### Bad Example - Testing Implementation Details
```typescript
// DON'T DO THIS - Testing schema validation separately
describe('Walkthrough Schema Validation', () => {
    test('should validate title length', () => {
        const schema = z.object({ title: z.string().max(100) })
        // This tests Zod, not your application
    })
})
```

### Bad Example - Duplicate Layer Tests
```typescript
// DON'T create these separate files:
// - database-operations.test.ts (tests direct DB calls)
// - schema-validation.test.ts (tests Zod schemas)  
// - orpc-actions.test.ts (tests server actions)
// - api-routes.test.ts (tests HTTP layer)

// DO create one file:
// - walkthrough-management.test.ts (tests the complete feature)
```

## Test Utilities

### CRITICAL: Resource Tracking Pattern
```typescript
// ALWAYS use this pattern for resource tracking
const createdResources = {
    organizations: new Set<string>(),  // Use Sets to avoid duplicates
    servers: new Set<string>(),
    walkthroughs: new Set<string>(),
    // ... etc
}

// When creating resources:
const [server] = await createMcpServerAction({...})
if (server) createdResources.servers.add(server.id)  // ALWAYS TRACK!

// When creating multiple resources:
const results = await Promise.all([...])
results.forEach(([error, data]) => {
    if (data) createdResources.walkthroughs.add(data.id)  // TRACK EACH ONE!
})
```

### Database Cleanup Helper
```typescript
async function cleanupTestResources(resources: Record<string, Set<string>>) {
    // Clean up in reverse order to respect FK constraints
    const cleanupOrder = [
        { table: 'mcpServerWalkthroughs', field: 'mcpServerId', resourceKey: 'servers' },
        { table: 'walkthroughSteps', field: 'id', resourceKey: 'steps' },
        { table: 'walkthroughs', field: 'id', resourceKey: 'walkthroughs' },
        { table: 'mcpServers', field: 'id', resourceKey: 'servers' },
        { table: 'session', field: 'id', resourceKey: 'sessions' },
        { table: 'user', field: 'id', resourceKey: 'users' },
        { table: 'organization', field: 'id', resourceKey: 'organizations' }
    ]
    
    for (const { table, field, resourceKey } of cleanupOrder) {
        const resourceIds = resources[resourceKey] || new Set()
        for (const id of resourceIds) {
            try {
                await db.delete(schema[table])
                    .where(eq(schema[table][field], id))
            } catch (err) {
                // Resource might already be deleted by cascade
            }
        }
    }
}
```

### Creating Test Context
```typescript
async function createTestContext() {
    const org = await db.insert(schema.organization).values({
        id: `org_${nanoid(8)}`,
        name: 'Test Org',
        slug: `test-${nanoid(8)}`,
        createdAt: new Date()
    }).returning()
    
    // Return real IDs that can be used in tests
    return { organizationId: org[0].id }
}
```

## Running Tests

```bash
# Run all tests
bun sst shell -- bun test --timeout 15000

# Run specific test file
bun sst shell -- bun test packages/dashboard/tests/03-interactive-walkthrough/walkthrough-crud.test.ts
```

### Helpful references for bun:test
- [`bun:test`](https://bun.com/docs/cli/test.md): Bun's test runner uses Jest-compatible syntax but runs 100x faster.
- [Writing tests](https://bun.com/docs/test/writing.md): Write your tests using Jest-like expect matchers, plus setup/teardown hooks, snapshot testing, and more
- [Watch mode](https://bun.com/docs/test/hot.md): Reload your tests automatically on change.
- [Lifecycle hooks](https://bun.com/docs/test/lifecycle.md): Add lifecycle hooks to your tests that run before/after each test or test run
- [Mocks](https://bun.com/docs/test/mocks.md): Mocks functions and track method calls
- [Snapshots](https://bun.com/docs/test/snapshots.md): Add lifecycle hooks to your tests that run before/after each test or test run
- [Dates and times](https://bun.com/docs/test/time.md): Control the date & time in your tests for more reliable and deterministic tests
- [Code coverage](https://bun.com/docs/test/coverage.md): Generate code coverage reports with `bun test --coverage`
- [Test reporters](https://bun.com/docs/test/reporters.md): Add a junit reporter to your test runs
- [Test configuration](https://bun.com/docs/test/configuration.md): Configure the test runner with bunfig.toml
- [Runtime behavior](https://bun.com/docs/test/runtime-behavior.md): Learn how the test runner affects Bun's runtime behavior
- [Finding tests](https://bun.com/docs/test/discovery.md): Learn how the test runner discovers tests
- [DOM testing](https://bun.com/docs/test/dom.md): Write headless tests for UI and React/Vue/Svelte/Lit components with happy-dom

### Supported Matchers:
*   `.not`
*   `.toBe()`
*   `.toEqual()`
*   `.toBeNull()`
*   `.toBeUndefined()`
*   `.toBeNaN()`
*   `.toBeDefined()`
*   `.toBeFalsy()`
*   `.toBeTruthy()`
*   `.toContain()`
*   `.toContainAllKeys()`
*   `.toContainValue()`
*   `.toContainValues()`
*   `.toContainAllValues()`
*   `.toContainAnyValues()`
*   `.toStrictEqual()`
*   `.toThrow()`
*   `.toHaveLength()`
*   `.toHaveProperty()`
*   `.extend`
*   `.anything()`
*   `.any()`
*   `.arrayContaining()`
*   `.assertions()`
*   `.closeTo()`
*   `.hasAssertions()`
*   `.objectContaining()`
*   `.stringContaining()`
*   `.stringMatching()`
*   `.resolves()`
*   `.rejects()`
*   `.toHaveBeenCalled()`
*   `.toHaveBeenCalledTimes()`
*   `.toHaveBeenCalledWith()`
*   `.toHaveBeenLastCalledWith()`
*   `.toHaveBeenNthCalledWith()`
*   `.toHaveReturned()`
*   `.toHaveReturnedTimes()`
*   `.toBeCloseTo()`
*   `.toBeGreaterThan()`
*   `.toBeGreaterThanOrEqual()`
*   `.toBeLessThan()`
*   `.toBeLessThanOrEqual()`
*   `.toBeInstanceOf()`
*   `.toContainEqual()`
*   `.toMatch()`
*   `.toMatchObject()`
*   `.toMatchSnapshot()`
*   `.toMatchInlineSnapshot()`
*   `.toThrowErrorMatchingSnapshot()`
*   `.toThrowErrorMatchingInlineSnapshot()`

## Key Reminders

1. **Test what the app actually uses** - If the app calls oRPC actions, test those
2. **Use real database** - No mocking of database operations
3. **TRACK EVERY RESOURCE** - Use Sets to track IDs of all created resources
4. **CLEAN UP ONLY WHAT YOU CREATED** - Never delete entire tables
5. **Clean up in correct order** - Respect foreign key constraints
6. **Test user flows** - Not technical implementation layers
7. **One test file per feature** - Not per technical concern
8. **Verify real state** - Check actual database records, not mocked returns
9. **Test error cases** - But with real errors from real operations
10. **NEVER use beforeEach to delete entire tables** - This is destructive

## Example: Complete Feature Test

```typescript
import { afterAll, beforeAll, describe, expect, test } from 'bun:test'
import { db, schema } from 'database'
import { 
    createWalkthroughAction,
    updateWalkthroughAction,
    deleteWalkthroughAction 
} from '@/lib/orpc/actions'

describe('Walkthrough CRUD Operations', () => {
    // Use Sets for tracking to avoid duplicates
    const createdResources = {
        organizations: new Set<string>(),
        walkthroughs: new Set<string>()
    }
    
    beforeAll(async () => {
        // Create test org if needed
        const [org] = await db.insert(schema.organization).values({
            id: 'test-org-id',
            name: 'Test Organization',
            slug: 'test-org',
            createdAt: new Date()
        }).returning()
        createdResources.organizations.add(org.id)
    })
    
    test('should handle complete walkthrough lifecycle', async () => {
        // Create
        const [createError, walkthrough] = await createWalkthroughAction({
            title: 'API Integration Guide',
            type: 'integration',
            isPublished: false
        })
        
        expect(createError).toBeNull()
        expect(walkthrough).toBeDefined()
        if (walkthrough) {
            expect(walkthrough.title).toBe('API Integration Guide')
            createdResources.walkthroughs.add(walkthrough.id)  // Track it!
        
        // Update
        const [updateError, updated] = await updateWalkthroughAction({
            walkthroughId: walkthrough.id,
            title: 'Updated API Guide',
            isPublished: true
        })
        
        expect(updateError).toBeNull()
        expect(updated.title).toBe('Updated API Guide')
        expect(updated.status).toBe('published')
        
        // Verify in database
        const [dbRecord] = await db.select()
            .from(schema.walkthroughs)
            .where(eq(schema.walkthroughs.id, walkthrough.id))
        
        expect(dbRecord.title).toBe('Updated API Guide')
        expect(dbRecord.status).toBe('published')
        
        // Delete
        const [deleteError] = await deleteWalkthroughAction({
            walkthroughId: walkthrough.id
        })
        
        expect(deleteError).toBeNull()
        
        // Verify deletion
        const deleted = await db.select()
            .from(schema.walkthroughs)
            .where(eq(schema.walkthroughs.id, walkthrough.id))
        
        expect(deleted.length).toBe(0)
    })
    
    afterAll(async () => {
        // Clean up specific resources in correct order
        for (const walkthroughId of createdResources.walkthroughs) {
            await db.delete(schema.walkthroughs)
                .where(eq(schema.walkthroughs.id, walkthroughId))
                .catch(() => {})
        }
        
        for (const orgId of createdResources.organizations) {
            await db.delete(schema.organization)
                .where(eq(schema.organization.id, orgId))
                .catch(() => {})
        }
    })
})
```

Remember: **Test the real thing, not a mock of the thing. Track every resource you create. Clean up only what you created.**


## Test running:
**Important** Requires using the package scripts since we need to use `bun sst shell -- <command>` to run commands so that they can reach redis & postgres; so use `bun run tests` which runs `bun sst shell -- bun test --timeout 150000`:

- Run tests (from repo root): `bun run tests`
- Run a specific file: `bun run tests contextualize-chunks.test.ts`
- Run a specific directory's tests: `bun runt tests retrieval`