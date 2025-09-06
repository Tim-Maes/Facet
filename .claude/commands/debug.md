# Debug

You are tasked with helping debug issues during manual testing or implementation. This command allows you to investigate problems by examining logs, database state, and git history without editing files. Think of this as a way to bootstrap a debugging session without using the primary window's context.

## Initial Response

When invoked WITH a plan/ticket file:
```
I'll help debug issues with [file name]. Let me understand the current state.

What specific problem are you encountering?
- What were you trying to test/implement?
- What went wrong?
- Any error messages?

I'll investigate the logs, database, and git state to help figure out what's happening.
```

When invoked WITHOUT parameters:
```
I'll help debug your current issue.

Please describe what's going wrong:
- What are you working on?
- What specific problem occurred?
- When did it last work?

I can investigate logs, database state, and recent changes to help identify the issue.
```

## Environment Information

You have access to these key locations and tools:

**Logs** (Next.js application logs):
- Next.js dev server output (running on port 3000)
- SST logs (when deployed): `sst logs`
- Browser console logs for client-side errors

**Database**:
- Development: Local PostgreSQL or SQLite database
- Production: PostgreSQL via Drizzle ORM
- Tables: `organization`, `user`, `session`, `mcp_servers`, `mcp_oauth_application`, `mcp_oauth_user`, `support_requests`
- Query with: Drizzle queries in the codebase or direct SQL

**Git State**:
- Check current branch, recent commits, uncommitted changes
- Main branch: `master`

**Service Status**:
- Check if Next.js dev server is running: `ps aux | grep "next dev"`
- Dev server URL: `http://localhost:3000`
- Check if database is accessible
- For production: SST deployment status

## Process Steps

### Step 1: Understand the Problem

After the user describes the issue:

1. **Read any provided context** (plan or ticket file):
   - Understand what they're implementing/testing
   - Note which phase or step they're on
   - Identify expected vs actual behavior

2. **Quick state check**:
   - Current git branch and recent commits
   - Any uncommitted changes
   - When the issue started occurring

### Step 2: Investigate the Issue

Spawn parallel Task agents for efficient investigation:

```
Task 1 - Check Application State:
Analyze the running application and logs:
1. Use Puppeteer to check UI state at http://localhost:3000
2. Check Next.js console output for errors
3. Look for API route errors in terminal
4. Check browser console for client-side errors
5. Look for hydration mismatches or React errors
Return: Key errors/warnings with context
```

```
Task 2 - Database State:
Check the current database state:
1. Query relevant tables based on the issue:
   - Organizations: SELECT * FROM organization;
   - MCP Servers: SELECT * FROM mcp_servers;
   - Auth sessions: SELECT * FROM session ORDER BY created_at DESC LIMIT 5;
   - Support requests: SELECT * FROM support_requests ORDER BY created_at DESC;
2. Check for data integrity issues
3. Verify foreign key relationships
4. Look for stuck states or anomalies
Return: Relevant database findings
```

```
Task 3 - Git and File State:
Understand what changed recently:
1. Check git status and current branch
2. Look at recent commits: git log --oneline -10
3. Check uncommitted changes: git diff
4. Verify Next.js route files exist in src/app/
5. Check for TypeScript errors: bun run typecheck (if available)
Return: Git state and any file issues
```

### Step 3: Present Findings

Based on the investigation, present a focused debug report:

```markdown
## Debug Report

### What's Wrong
[Clear statement of the issue based on evidence]

### Evidence Found

**From Application State**:
- [Error/warning from Next.js console]
- [Client-side errors or hydration issues]
- [API route failures]

**From Database**:
```sql
-- Relevant query and result
[Finding from database]
```

**From Git/Files**:
- [Recent changes that might be related]
- [TypeScript or build errors]

### Root Cause
[Most likely explanation based on evidence]

### Next Steps

1. **Try This First**:
   ```bash
   [Specific command or action]
   ```

2. **If That Doesn't Work**:
   - Check the dev server is running on port 3000
   - Clear Next.js cache: `rm -rf .next`
   - Check for TypeScript errors: `bun run typecheck`
   - Verify database migrations are up to date
   - Try logging in at `/login-for-claude` if auth issues

### Can't Access?
Some issues might be outside my reach:
- Browser console errors (F12 in browser)
- SST deployment logs (production)
- External service integrations

Would you like me to investigate something specific further?
```

## Important Notes

- **Focus on manual testing scenarios** - This is for debugging during implementation
- **Always require problem description** - Can't debug without knowing what's wrong
- **Read files completely** - No limit/offset when reading context
- **Understand the dual auth system** - Platform auth vs sub-tenant auth
- **Check vhost routing** - MCP servers use subdomain-based routing
- **No file editing** - Pure investigation only
- **Dev server is always running** - Never run `bun run dev` or `bun run build`

## Quick Reference

**Check Application State**:
```bash
# Use Puppeteer to check UI at http://localhost:3000
# Navigate to /login-for-claude if auth needed
```

**Database Queries** (adjust for your DB setup):
```sql
-- Check organizations
SELECT * FROM organization;

-- Check MCP servers and routing
SELECT slug, domain FROM mcp_servers;

-- Check auth sessions
SELECT * FROM session ORDER BY created_at DESC LIMIT 5;

-- Check sub-tenant users
SELECT * FROM mcp_oauth_user ORDER BY created_at DESC LIMIT 5;
```

**Service Check**:
```bash
ps aux | grep "next dev"     # Is Next.js running?
lsof -i :3000               # What's on port 3000?
```

**Git State**:
```bash
git status
git log --oneline -10
git diff
```

**Common Issues**:
- Auth problems: Check both auth systems (platform vs sub-tenant)
- Routing issues: Verify vhost subdomain matches MCP server slug
- Hydration errors: Check for server/client data mismatches
- TypeScript errors: Run `bun run typecheck` if available

Remember: This command helps you investigate without burning the primary window's context. Perfect for when you hit an issue during manual testing and need to dig into logs, database, or application state.