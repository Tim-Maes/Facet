# Create Implementation Strategy Document

You are tasked with creating detailed implementation strategy documents through an interactive, iterative process. These documents bridge the gap between requirements and actual code implementation. You should be thorough, technically focused, and work collaboratively to produce actionable implementation plans.

## Initial Response

When this command is invoked:

1. **Check if parameters were provided**:
   - If a requirements document path was provided as a parameter, skip the default message
   - Immediately read the requirements document FULLY
   - Begin the analysis process

2. **If no parameters provided**, respond with:
```
I'll help you create a detailed implementation strategy. Let me start by understanding the requirements.

Please provide:
1. The requirements document path (e.g., specifications/feature-name/feature.md)
2. Any existing research or related implementations
3. Any technical constraints or preferences

I'll analyze the requirements and work with you to create a comprehensive implementation strategy.

Tip: You can invoke this command with requirements: `/create_implementation_strategy specifications/feature-name/feature.md`
```

Then wait for the user's input.

## Process Steps

### Step 1: Requirements Analysis & Research

1. **Read requirements document completely**:
   should be at `specifications/feature-name/feature.md` or similar.
   - Use the Read tool WITHOUT limit/offset parameters
   - Understand all user stories and functional requirements
   - Note any design considerations and constraints
   - Identify success criteria

2. **Spawn focused research tasks**:
   Before asking questions, spawn parallel research tasks to understand the implementation context:

   ```
   Task 1 - Research current implementation:
   Research how the areas mentioned in the requirements currently work.
   1. Find existing components, pages, and data models related to [feature area]
   2. Identify current patterns and conventions being used
   3. Look for similar features that can be used as templates
   4. Note any existing infrastructure that can be leveraged
   Use tools: Grep, Glob, LS, Read
   Return: Current implementation details with file:line references;
   ```

   ```
   Task 2 - Identify technical patterns:
   Research the technical patterns needed for this implementation.
   1. Find oRPC action examples and patterns in the codebase
   2. Identify database schema patterns and migration examples
   3. Look for UI component patterns using shadcn/ui
   4. Find authentication and organization scoping examples
   Return: Technical patterns and examples with file references
   ```

   ```
   Task 3 - Research dependencies and integration points:
   1. Find where [feature] would need to integrate
   2. Check for any existing interfaces we need to implement
   3. Look for configuration or feature flags
   4. Identify potential breaking changes
   5. Find related documentation or comments
   Return: Integration requirements and constraints
   ```
3. **Wait for ALL sub-tasks to complete** before proceeding

4. **Read all files identified by research**:
   - Read relevant files completely into main context including requirements.
   - Understand existing patterns and conventions
   - Identify reusable components and utilities

5. **Present analysis and ask clarifications**:
   ```
   Based on the requirements and my research, I understand we need to implement [summary].

   **Current State:**
   - [Key discovery about existing code]
   - [Pattern or convention to follow]
   - [Technical constraints discovered]

   Questions to finalize the strategy:
   - [Critical technical decision]
   - [Implementation approach preference]
   - [Any missing context from requirements]

   **Design Options:**
   1. [Option A] - [pros/cons]
   2. [Option B] - [pros/cons]

   **Open Questions:**
   - [Technical uncertainty]
   - [Design decision needed]

   Which approach aligns best with your vision?

   ```

### Step 2: Strategy Development

1. **Design the implementation approach**:
   - Determine phase breakdown strategy
   - Identify dependencies and sequencing
   - Choose technical patterns to follow
   - Plan data flow and component hierarchy

2. **Present strategy outline**:
   ```
   Here's my proposed implementation strategy:

   ## Overview
   [1-2 sentence summary of approach]

   ## Phases:
   1. [Phase name] - [what it establishes]
   2. [Phase name] - [what it builds on phase 1]
   3. [Phase name] - [what it completes]

   Does this phasing approach make sense? Any adjustments needed?
   ```

3. **Get feedback on strategy** before writing detailed plan

### Step 3: Gather Metadata and Write Implementation Plan

1. **Gather metadata for the implementation strategy document:**
   - Run the `scripts/spec_metadata.sh` script to generate all relevant metadata
   - Filename: should be under the `specifications` directory for the feature that you're currently working on, e.g. `specifications/01-better-session-support/implementation-plan.md`, or under `specifications/general` if you don't have information about which feature you're working on. Name the file `implementation-plan.md`

2. **Generate implementation strategy document:**
   - Use the metadata gathered in step 1
   - Structure the document with YAML frontmatter followed by content:
     ```markdown
     ---
     date: [Current date and time with timezone in ISO format]
     researcher: [Researcher name from thoughts status]
     git_commit: [Current commit hash]
     branch: [Current branch name]
     repository: [Repository name]
     topic: "[Feature/Task Name] Implementation Strategy"
     tags: [implementation, strategy, relevant-component-names]
     status: complete
     last_updated: [Current date in YYYY-MM-DD format]
     last_updated_by: [Researcher name]
     type: implementation_strategy
     ---

# [Feature/Task Name] Implementation Plan

## Overview

[Brief description of what we're implementing and why]

## Current State Analysis

[What exists now, what's missing, key constraints discovered]

### Key Discoveries:
- [Important finding with file:line reference]
- [Pattern to follow]
- [Constraint to work within]

## What We're NOT Doing

[Explicitly list out-of-scope items to prevent scope creep]

## Implementation Approach

[High-level strategy and reasoning]

## Phase 1: [Descriptive Name]

### Overview
[What this phase accomplishes]

### Changes Required:

#### 1. [Component/File Group]
**File**: `path/to/file.ext`
**Changes**: [Summary of changes]

**Implementation Requirements:**
- [High-level description of what needs to be implemented]
- [Key functionality and behavior requirements] 
- [Integration points and dependencies]
- [UI/UX specifications without code]
- [Data flow and state management approach]
- [Error handling and edge cases to consider]

[Add additional phases as necessary]

### Success Criteria:

**Automated verification**
- [ ] no linter errors

**Manual Verification**
- [ ] feature works as expected when tested in UI by the user 
- [ ] feature works as epxected when testing in UI by you with puppeteer
- [ ] edge case handling is verified manually
- [ ] no regressions in related features

## Phase 2: [Descriptive Name]
[similar structure with both automated and manual success criteria...]

## Performance Considerations
[Any performance implications or optimizations needed]

## Migration Notes
[If applicable, how to handle existing data/systems]

## References 
* Original ticket: `specifications/feature-name/feature.md`
* related research: `specifications/feature-name/artifacts/something.md`, `specifications/artifacts/something-else.md`
* similar implementation: `/path/to/file.ext:line`

```

### Step 4: Review & Refinement

1. **Save document to**: `specifications/[feature-name]/implementation-plan.md` or `specifications/[feature-name]/[sub-feature-name]/implementation-plan.md`
   NOTE: if the feature is large, ask the user:


2. **Present for review**:
   ```
   Implementation strategy created at: specifications/[feature-area]/implementation-plan.md

   Please review:
   - Do the phases make sense and build on each other?
   - Are the tasks specific enough to be actionable?
   - Any technical decisions that need adjustment?
   - Ready to begin implementation?
   ```

3. **Iterate based on feedback**:
   - Adjust phase sequencing
   - Add missing technical details
   - Clarify task descriptions
   - Update architectural decisions

## Guidelines
1. **Be Skeptical**:
- Question vague requirements
- Identify potential issues early
- Ask "why" and "what about"
- Don't assume - verify with code

2. **Be Interactive**:
- Don't write the full plan in one shot
- Get buy-in at each major step
- Allow course corrections
- Work collaboratively

3. **Be Thorough**:
- Read all context files COMPLETELY before planning
- Research actual code patterns using parallel sub-tasks
- Include specific file paths and line numbers
- Write measurable success criteria with clear automated vs manual distinction
- automated steps should use `make` whenever possible - for example `make -C humanlayer-wui check` instead of `cd humanalyer-wui && bun run fmt`

4. **Be Practical**:
- Focus on incremental, testable changes
- Consider migration and rollback
- Think about edge cases
- Include "what we're NOT doing"

5. **Track Progress**:
- Use TodoWrite to track planning tasks
- Update todos as you complete research
- Mark planning tasks complete when done

6. **No Open Questions in Final Plan**:
- If you encounter open questions during planning, STOP
- Research or ask for clarification immediately
- Do NOT write the plan with unresolved questions
- The implementation plan must be complete and actionable
- Every decision must be made before finalizing the plan

7. **Avoid Code Snippets in Implementation Plans**:
- Implementation plans should contain instructions and requirements, NOT actual code
- Use high-level descriptions of what needs to be implemented instead of code examples
- Focus on architectural decisions, patterns to follow, and integration requirements
- Include references to existing code patterns and examples by file:line, but don't copy code
- Code snippets make documents harder to maintain and quickly become outdated
- Reserve actual code for the implementation phase, not the planning phase

### Focus on Implementation Details
- Break down requirements into specific, actionable tasks
- Include exact file paths and component names where possible
- Reference existing patterns and examples by file:line location
- Plan for incremental development and testing
- Describe WHAT needs to be implemented and HOW it should work, not the actual code
- Provide sufficient detail for implementation without including code snippets

### Follow Project Patterns
- Use established oRPC patterns for data operations
- Follow React 19 server/client component patterns
- Use shadcn/ui components exclusively
- Maintain organization scoping throughout

### Be Thorough but Actionable
- Every task should be specific enough to implement
- Include technical context and file references
- Plan for both happy path and error scenarios
- Consider testing strategy from the beginning

### Research-Driven Approach
- Always research existing implementations first
- Identify reusable patterns and components
- Understand current conventions and follow them
- Verify technical assumptions with actual code

### Interactive Planning
- Get feedback on strategy before detailed tasks
- Collaborate on technical approach decisions
- Allow for course corrections during planning
- Ask clarifying questions about requirements

## Quality Checklist

Before finalizing:

- [ ] Requirements are clearly understood and addressed
- [ ] Tasks are specific and actionable
- [ ] Technical patterns follow existing conventions
- [ ] Phases build logically on each other
- [ ] File paths and references are accurate
- [ ] Success criteria match requirements

## Common MCPlatform Patterns

### Database Operations
- Always use Drizzle ORM TypeScript schemas
- Include proper organization scoping in all queries
- Plan migrations separately from application logic
- Use proper foreign key relationships

### oRPC Actions
- Create actions for all data mutations
- Use proper input validation with Zod schemas
- Include revalidatePath for UI updates
- Handle errors gracefully with proper user feedback

### React Architecture
- Server components fetch data and pass promises
- Client components use `use()` hook with Suspense
- State management with nuqs for URL state
- Proper error boundaries throughout

### UI Components
- Use shadcn/ui components exclusively
- Plan responsive behavior from the start
- Consider mobile experience
- Include loading states and error handling

## Common Patterns

### For Database Changes:
- Start with schema/migration
- Add store methods
- Update business logic
- Expose via API
- Update clients

### For New Features:
- Research existing patterns first
- Start with data model
- Build backend logic
- Add API endpoints
- Implement UI last

### For Refactoring:
- Document current behavior
- Plan incremental changes
- Maintain backwards compatibility
- Include migration strategy

## Sub-task Spawning Best Practices

When spawning research sub-tasks:

1. **Spawn multiple tasks in parallel** for efficiency
2. **Each task should be focused** on a specific area
3. **Provide detailed instructions** including:
   - Exactly what to search for
   - Which directories to focus on
   - What information to extract
   - Expected output format
4. **Be EXTREMELY specific about directories**:
   - If the ticket mentions "WUI", specify `humanlayer-wui/` directory
   - If it mentions "daemon", specify `hld/` directory
   - Never use generic terms like "UI" when you mean "WUI"
   - Include the full path context in your prompts
5. **Specify read-only tools** to use
6. **Request specific file:line references** in responses
7. **Wait for all tasks to complete** before synthesizing
8. **Verify sub-task results**:
   - If a sub-task returns unexpected results, spawn follow-up tasks
   - Cross-check findings against the actual codebase
   - Don't accept results that seem incorrect

Example of spawning multiple tasks:
```python
# Spawn these tasks concurrently:
tasks = [
    Task("Research database schema", db_research_prompt),
    Task("Find API patterns", api_research_prompt),
    Task("Investigate UI components", ui_research_prompt),
    Task("Check test patterns", test_research_prompt)
]
```

## Example Interaction Flow

```
User: /implementation_plan
Assistant: I'll help you create a detailed implementation plan...

User: We need to add parent-child tracking for Claude sub-tasks. See thoughts/allison/tickets/eng_1478.md
Assistant: Let me read that ticket file completely first...

[Reads file fully]

Based on the ticket, I understand we need to track parent-child relationships for Claude sub-task events in the hld daemon. Before I start planning, I have some questions...

[Interactive process continues...]
```