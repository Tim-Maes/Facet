# Validate Plan

You are tasked with validating that an implementation plan was correctly executed, verifying all success criteria and identifying any deviations or issues.

## Initial Setup

When invoked:
1. **Determine context** - Are you in an existing conversation or starting fresh?
   - If existing: Review what was implemented in this session
   - If fresh: Need to discover what was done through git and codebase analysis

2. **Locate the plan**:
   - If plan path provided, use it
   - Otherwise, search recent commits for plan references or ask user
   - Look in `specifications/*/implementation-plan.md` or `specifications/*/*/implementation-plan.md`

3. **Gather implementation evidence**:
   ```bash
   # Check recent commits
   git log --oneline -n 20
   git diff HEAD~N..HEAD  # Where N covers implementation commits

   # Run MCPlatform checks
   cd packages/dashboard && bun lint
   cd packages/dashboard && bun run build
   ```
   IMPORTANT: make sure to ask the user to stop the dev server BEFORE doing this!!!

## Validation Process

### Step 1: Context Discovery

If starting fresh or need more context:

1. **Read the implementation plan** completely using Read tool (WITHOUT limit/offset)
2. **Identify what should have changed**:
   - List all files that should be modified
   - Note all success criteria (automated and manual)
   - Identify key functionality to verify
   - Check if database migrations were specified

3. **Spawn parallel research tasks** to discover implementation:
   ```
   Task 1 - Verify database changes:
   Research if migrations were added and schema changes match plan.
   Check: packages/database/src/schema.ts, migration files in packages/database/migrations/
   Look for: new tables, columns, indexes, foreign keys as specified in plan
   Return: What was implemented vs what plan specified; (ask user to run migrations if desired)

   Task 2 - Verify oRPC actions:
   Find all oRPC actions that should have been added/modified.
   Check: packages/dashboard/src/lib/orpc/actions.ts and router.ts
   Look for: new actions, proper input validation, revalidatePath calls, error handling
   Return: Action implementation vs plan specifications

   Task 3 - Verify UI components:
   Check if shadcn/ui components were added/modified as specified.
   Look in: packages/dashboard/src/components/ and src/app/
   Check: server components, client components, proper use() hook usage, Suspense boundaries
   Return: Component implementation vs plan requirements

   Task 4 - Verify React 19 patterns:
   Check if async server components and promise patterns were implemented correctly.
   Look for: page.tsx with async functions, promises passed to client components
   Return: Architecture conformance to MCPlatform patterns
   ```

### Step 2: Systematic Validation

For each phase in the implementation plan document

1. **Check completion status**:
   - Look for checkmarks in the plan (- [x])
   - Verify the actual code matches claimed completion

2. **Run automated verification**:
   - Execute each command from "Automated Verification" section
   - Use MCPlatform specific tools:
     ```bash
     bun run tests
     ```
   - Document pass/fail status
   - If failures, investigate root cause

3. **Assess manual criteria**:
   - List what needs manual testing (especially UI with Puppeteer)
   - Check if organization scoping is properly implemented
   - Verify authentication patterns (platform vs sub-tenant)
   - use playwright MCP to test user interface features & interactions.

4. **Think deeply about MCPlatform patterns**:
   - Are oRPC actions properly wrapped with .actionable({})?
   - Do server components, server actions and RPCs use requireSession() for auth?
   - Are all database queries scoped to organizations?
   - Do they use organization IDs directly from the _session_ for data fetching, rather than a passed parameter?
   - Does the UI follow shadcn/ui component patterns?
   - Is error handling robust with proper user feedback ( e.g. toast messages, fallback components for `<ErrorBoundary>` components)?

### Step 3: Generate Validation Report

Create comprehensive validation summary:

```markdown
## Validation Report: [Plan Name]

### Implementation Status
✅ Phase 1: [Name] - Fully implemented
✅ Phase 2: [Name] - Fully implemented  
⚠️ Phase 3: [Name] - Partially implemented (see issues)

### Automated Verification Results
✅ Biome check passes: `bun lint`
✅ Build succeeds: `cd packages/dashboard && bun run build`
❌ Database tests fail: `cd packages/database && bun run tests` (2 failures)

### Code Review Findings

#### Matches Plan:
- Database migration correctly adds [table] with proper foreign keys
- oRPC actions implement specified methods with proper validation
- UI components follow React 19 async patterns
- Organization scoping implemented correctly

#### Deviations from Plan:
- Used different component name in [file:line] (cosmetic)
- Added extra validation in [file:line] (improvement)
- Skipped intermediate step in favor of more direct approach

#### MCPlatform Pattern Compliance:
✅ Server components are async and use requireSession()
✅ Client components use 'use client' and use() hook properly
✅ oRPC actions include revalidatePath calls
✅ Database queries respect organization boundaries
❌ Missing Suspense boundary in [component:line]

#### Potential Issues:
- Missing index on foreign key could impact performance
- Error boundary not implemented for [component]
- No loading state for async operation in [file]

### Manual Testing Required:
1. **Puppeteer UI Testing**:
   - [ ] Navigate to localhost:3000/login-for-claude for auto-login
   - [ ] Verify [feature] appears correctly at 1920x1080 resolution
   - [ ] Test error states with invalid input
   - [ ] Check responsive behavior on mobile viewport

2. **Integration Testing**:
   - [ ] Confirm works with existing [component]
   - [ ] Test organization isolation (create test data in different orgs)
   - [ ] Verify authentication flows work correctly

3. **Performance Testing**:
   - [ ] Check load times with large datasets
   - [ ] Verify no memory leaks in React components

### Database Verification:
- [ ] Migration [number] applied successfully
- [ ] New tables/columns match schema definitions
- [ ] Indexes created as specified
- [ ] Foreign key constraints working

### Security Review:
- [ ] All operations respect organization boundaries
- [ ] No sensitive data exposed in client components
- [ ] Authentication checks in place
- [ ] Input validation prevents injection attacks

### Recommendations:
- Address Biome warnings before merge
- Add missing Suspense boundaries for better UX
- Consider adding integration tests for [scenario]
- Update CLAUDE.md if new patterns were established
```

## Working with Existing Context

If you were part of the implementation:
- Review the conversation history for what was actually done
- Check your todo list (if any) for completed items
- Focus validation on work done in this session
- Be honest about any shortcuts or incomplete items
- Note any decisions that deviated from the plan and why

## MCPlatform-Specific Checks

Always verify MCPlatform patterns:

### Database Layer (Drizzle ORM)
- [ ] Schema changes in `packages/database/src/schema.ts`
- [ ] Migrations generated with `bun run db:generate`
- [ ] Foreign keys and indexes properly defined
- [ ] Organization scoping in all queries

### API Layer (oRPC)
- [ ] Actions in `packages/dashboard/src/lib/orpc/actions.ts`
- [ ] Proper input validation with Zod schemas
- [ ] `revalidatePath()` called after mutations
- [ ] Error handling with defined error types
- [ ] Actions wrapped with `.actionable({})`

### UI Layer (React 19 + shadcn/ui)
- [ ] Server components are async functions
- [ ] Client components start with `'use client'`
- [ ] Promises passed from server to client components
- [ ] `use()` hook used to unwrap promises
- [ ] Suspense boundaries around async content
- [ ] Only shadcn/ui components used (no Radix direct)

### Authentication & Authorization
- [ ] `requireSession()` used in server components
- [ ] Organization boundaries respected
- [ ] Proper auth system used (platform vs sub-tenant)

## Important Guidelines

1. **Be thorough but practical** - Focus on what matters for shipping
2. **Run all automated checks** - Don't skip Biome/build verification
3. **Document everything** - Both successes and issues
4. **Think critically** - Question if implementation truly solves the problem
5. **Consider maintenance** - Will this be maintainable long-term?
6. **Respect the dev server** - Never run `bun run dev` or `bun run build`

## Validation Checklist

Always verify:
- [ ] All phases marked complete are actually done
- [ ] Biome checks pass without errors
- [ ] Build succeeds (dry-run only)
- [ ] Code follows MCPlatform patterns
- [ ] No regressions in existing functionality
- [ ] Organization isolation maintained
- [ ] Error handling is robust
- [ ] Manual test steps are clear
- [ ] Database changes are properly migrated

## Common Issues to Check

### Database Issues
- Missing organization scoping in queries
- Foreign key constraints not properly defined
- Indexes missing on commonly queried columns
- Migration not applied or generated incorrectly

### oRPC Issues
- Actions missing `.actionable({})` wrapper
- No `revalidatePath()` calls after mutations
- Input validation incomplete or missing
- Error handling not following project patterns

### React Component Issues
- Server components not async
- Client components missing `'use client'`
- Promises not properly passed/unwrapped
- Missing Suspense boundaries
- Direct Radix usage instead of shadcn/ui

### Authentication Issues
- Missing `requireSession()` in server components
- Organization boundaries not enforced
- Wrong auth system used for context

## Addenda:
Remember: Good validation catches issues before they reach users. Be constructive but thorough in identifying gaps or improvements that align with MCPlatform's patterns and quality standards.