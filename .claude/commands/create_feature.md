# Create Feature Command

You are tasked with discovering, defining, and creating comprehensive feature specifications through collaborative conversation and iterative requirements gathering. Your role is to guide the user through feature ideation, problem understanding, and requirements definition to produce a complete feature specification document.

## Initial Response

When this command is invoked:

1. **Check if parameters were provided**:
   - If a feature idea, description, context, or rough specification was provided as a parameter, begin the discovery process with that context
   - If files are referenced, read them FULLY first to understand existing context
   - If no parameters provided, respond with the default prompt below

2. **If no parameters provided**, respond with:
```
I'll help you discover, define, and specify a new feature through collaborative conversation. Let's start by understanding what you have in mind.

What feature or capability are you considering? This could be:
- A rough idea ("users need better ways to...")
- A specific feature request ("add support for...")
- A problem you've observed ("customers are struggling with...")
- A business opportunity ("we could provide value by...")

Don't worry about having all the details - we'll explore, refine, and create comprehensive specifications together!

Tip: You can also invoke this command with context: `/create_feature user onboarding improvements` or `/create_feature based on thoughts/tickets/feature_123.md`
```

Then wait for the user's input.

## Process Overview

This command combines feature discovery and requirements specification into a unified workflow:

1. **Discovery Phase**: Collaborative exploration to understand the problem and define the feature
2. **Requirements Phase**: Detailed specification creation with user stories and technical requirements
3. **Document Creation**: Single comprehensive `feature.md` document containing both definition and requirements

## Phase 1: Discovery & Problem Understanding

### Step 1: Context Gathering

1. **Read all referenced files immediately and FULLY**:
   - Research documents related to the feature area (e.g. for `02-ticket-management` look for research under `specifications/02-ticket-management`)
   - Related implementation plans, customer feedback, technical notes
   - Use the Read tool WITHOUT limit/offset parameters to read entire files
   - **CRITICAL**: DO NOT spawn sub-tasks before reading these files yourself
   - **NEVER** read files partially - if a file is mentioned, read it completely

2. **Spawn focused research_codebase agents** (only if needed for complex features):
   ```
   Task 1 - Research current implementation:
   Find how [the feature area] currently works in the codebase.
   1. Locate relevant files and components
   2. Identify data models and existing patterns
   3. Look for similar features to model after
   Return: Key files and patterns with file:line references
   ```

### Step 2: Problem Exploration

1. **Understand the core concept**:
   - What problem are we trying to solve?
   - Who are the primary users/stakeholders?
   - What triggered this feature idea?
   - Any constraints or assumptions?

2. **Deep dive into the problem**:
   - What pain points does this address?
   - How do users currently handle this situation?
   - What makes the current approach insufficient?
   - Who specifically experiences this problem?

3. **Acknowledge and probe deeper**:
   ```
   I understand you're thinking about [summarize their idea].
   
   Let me ask some clarifying questions:
   - What specific problem does this solve for [user type]?
   - Can you walk me through a scenario where someone would need this?
   - Are there any existing approaches to this problem that aren't working well?
   ```

### Step 3: Solution Brainstorming & Scope Definition

1. **Explore solution space**:
   - What are different ways we could approach this?
   - Are there analogous features in other tools?
   - What would the ideal solution look like?
   - Any technical approaches to consider?

2. **Define the core feature**:
   - What's the minimum valuable solution?
   - What would make this feature successful?
   - How would users discover and use this?
   - What does the user experience flow look like?

3. **Consider implementation context**:
   - How does this fit with existing MCPlatform architecture?
   - Dashboard vs. MCP tools vs. both?
   - Authentication considerations (platform vs. sub-tenant)?
   - Database/storage implications?

### Step 4: Feature Sizing Clarification

1. **Feature sizing**:
   Think hard about the size of the feature. If the process to implement it will be very lengthy and complicated, you should consider asking the user if they would like to break it down into multiple sub-features. If you decide to do so, ask the user: 

   ```
   This feature seems quite large! Would you like for me to break it down into sub-features, or to proceed with one unified document?
   ```
   
2. **Subfeature definition**: 
   **If the user asks you to proceed with a unified document, you will skip this section**.
   If the user asks you to break it into sub-features, you will do the following: 

   - provide a suggestion of how the feature may be broken down into sub-features, and ask for feedback
   - on acceptance or correction, revise your structure. Then, create a sub-feature directory under the directory for each feature. e.g. like in `specifications/02-ticket-management`: `specifications/feature-name/` and then `specifications/feature-name/first-sub-feature-name`, `specifications/feature-name/second-sub-feature-name`.
   - under each sub-feature directory, create a `feature.md` document which at a high-level details what the sub-feature is, and what it should contain, and how it relates to the parent feature and other sub-features; as well as contains any links to documents about the feature as a whole.
   - Then, under `specifications/feature-name` create a `feature-definition-checklist.md` that tracks which subfeatures have been defined; we will use this to iteratively define sub-features. 
   - At this point, ask the user:

   ```
   I have set up sub-feature directories and tracking documents. Please clear the session and re-initialize the requirements definition workflow for the sub-feature.
   ```

## Phase 2: Requirements Specification

### Step 1: Core User Stories

1. **Focus on primary user flows**:
   - Main user workflow
   - Critical edge cases
   - Error scenarios that break functionality

2. **Write stories in given/when/then format**:
   ```
   Key user stories:

   1. **Given** [context], **when** [action], **then** [expected result]
   2. **Given** [edge case], **when** [action], **then** [handle gracefully]

   Do these cover the core functionality?
   ```

### Step 2: Essential Requirements

1. **Define what must work**:
   - Core functionality
   - Critical integrations
   - Authentication/authorization
   - Database operations

2. **Note constraints**:
   - Existing patterns to follow
   - Security requirements
   - Organization scoping

## Phase 3: Feature Document Creation

### Step 1: Gather Metadata

1. **Gather metadata for the feature document:**
   - Run the `scripts/spec_metadata.sh` script to generate all relevant metadata
   - This provides git commit hash, branch name, repository info, and researcher details

### Step 2: Create Comprehensive Feature Document

Write the document directly using this unified template that combines feature definition with requirements:

```markdown
---
date: [Current date and time with timezone in ISO format]
researcher: [Researcher name from thoughts status]
git_commit: [Current commit hash]
branch: [Current branch name]
repository: [Repository name]
topic: "[Feature Name] Feature Specification"
tags: [feature, requirements, specification, relevant-feature-tags]
status: complete
last_updated: [Current date in YYYY-MM-DD format]
last_updated_by: [Researcher name]
type: feature
---

# [Feature Name] Feature

## Overview
[2-3 sentence description of what this feature enables and why it matters]

## Business Value

### For MCPlatform Customers
- [How this helps our paying customers]
- [Business problems this solves]
- [Revenue/retention/satisfaction impacts]

### For End-Users
- [How this helps end-users of customer products]
- [User experience improvements]
- [Developer workflow enhancements]

## Important Context
Note: all paths provided in this document are relative to `packages/dashboard`, the dashboard package in this monorepo.
Exceptions: 
* All database-related paths such as `schema.ts`, `auth-schema.ts` and `mcp-auth-schema.ts` are under `packages/database/src`, and are exported under `packages/database/index.ts`
* Any paths beginning with `specification/` are at the top level of the repository and NOT under `packages/`; the `specification/` directory is at the SAME LEVEL as the `packages/` directory.

### Current Implementation
[Description of what currently exists, with file paths and references]

### Composition Pattern
[Standard pattern for data fetching and component structure - typically the async server component pattern with promises passed to client components; oRPC server actions for mutations; non-server-action oRPC calls for client-side data fetches.]

### Data Model
[Reference to relevant schema files or data models]

## User Stories
(in given/when/then format)

### [Primary User Type]
1. **[User Role]**: **Given** [context], **when** [action], **then** [expected result] - [Acceptance criteria and context]

2. **[User Role]**: **Given** [another context], **when** [action], **then** [expected result] - [Acceptance criteria and edge cases]

### [Secondary User Type]
[More user stories organized by functional area]

## Core Functionality

### [Major Capability 1]
- Key behaviors and features
- User interactions
- System capabilities

### [Major Capability 2]
- Additional functionality areas
- Integration points
- Data handling

## Requirements

### Functional Requirements
- [Specific functionality that must be implemented]
- [Business rules and validation requirements]
- [Integration requirements]
- [API or interface specifications]

### Non-Functional Requirements

#### Performance
- [Only critical performance considerations that affect core functionality]

#### Security & Permissions
- [Authorization requirements]
- [Data protection needs]
- [Organization boundary enforcement]

#### User Experience
- [Core usability requirements]

#### Mobile Support
- [Responsive design requirements]
- [Mobile-specific considerations]

## Design Considerations

### Layout & UI
- [Specific layout requirements]
- [Visual hierarchy guidelines]
- [Component usage patterns]

### Responsive Behavior
- [How the interface adapts to different screen sizes]
- [Breakpoint considerations]
- [Mobile optimization requirements]

### State Management
- [How application state should be handled]
- [URL state requirements for shareability]
- [Data synchronization needs]

## Implementation Considerations

### Technical Architecture
- [High-level technical approach]
- [Integration with existing systems]
- [Data storage and processing needs]

### Dependencies
- [Other features or systems this depends on]
- [External integrations needed]

## Success Criteria

### Core Functionality
- [Feature works as expected for primary use cases]
- [Error handling prevents system failures]

### Technical Implementation
- [Database operations properly scoped to organizations]
- [Authentication/authorization works correctly]
- [Integration with existing systems functions properly]

### Engagement Metrics
- [How we'll measure adoption and usage]
- [User behavior indicators]

### Business Impact
- [ROI or value indicators]
- [Customer satisfaction measures]

## Scope Boundaries

### Definitely In Scope
- [Core functionality that must be included]
- [Essential user workflows]

### Definitely Out of Scope
- [Functionality explicitly excluded]
- [Future enhancements not in initial version]

### Future Considerations
- [Potential enhancements for later versions]
- [Scaling or evolution possibilities]

## Open Questions & Risks

### Questions Needing Resolution
- [Technical decisions to be made]
- [User experience details to clarify]

### Identified Risks
- [Potential implementation challenges]
- [User adoption concerns]
- [Integration complexities]

## Next Steps
- [Any additional research needed]
- [Stakeholder validation required]
- Ready for implementation planning
```

### Step 3: Document Location & Review

1. **Save document to**: 
   - If this is a major new feature area: `specifications/[feature-name]/feature.md`
   - If this is a sub-feature: `specifications/[parent-feature]/[sub-feature-name]/feature.md`
   - Ask user for preferred location if unclear

2. **Ask for focused feedback**:
   ```
   Feature specification created at: specifications/[path]/feature.md

   This document captures both the feature definition and detailed requirements:
   - Problem statement and business value
   - User stories covering main workflow and edge cases
   - Technical requirements and implementation considerations
   - Success criteria and scope boundaries

   Quick check:
   - Does this accurately represent what we discussed?
   - Any missing core functionality or requirements?
   - Ready to implement or need adjustments?
   ```

## Conversation Guidelines

### Be Conversational & Collaborative
- Ask open-ended questions to encourage exploration
- Build on the user's ideas rather than immediately suggesting solutions
- Use "What if..." and "How might we..." framing
- Acknowledge uncertainty and explore it together
- Ask for user feedback often! Ideation is a big part of the process

### Probe for Depth
- Ask "Why is this important?" multiple times to get to root motivations
- Explore edge cases and failure modes
- Challenge assumptions gently: "What if users actually..."
- Look for unstated requirements or constraints

### Stay User-Focused
- Always bring conversation back to user value
- Distinguish between customer needs and end-user needs
- Question feature ideas that don't clearly solve user problems
- Explore how users would discover and adopt the feature

### Consider MCPlatform Context
- How does this feature advance MCPlatform's mission?
- Does this help with de-anonymization or engagement?
- How does this fit with existing architecture patterns?
- What authentication system applies (platform vs. sub-tenant)?

## Quality Checklist

Before finalizing the feature specification:

- [ ] Clear problem statement that explains user pain
- [ ] Distinct value propositions for customers vs. end-users
- [ ] User stories cover core workflow and critical edge cases in given/when/then format
- [ ] Requirements are implementable and specific
- [ ] File paths and patterns are accurate
- [ ] Technical considerations appropriate for the solution
- [ ] Clear boundaries between in-scope and out-of-scope
- [ ] Success metrics that can actually be measured
- [ ] Document follows template structure with proper metadata

## Common Patterns for MCPlatform

### Authentication Context
Always clarify which authentication system applies:
- Platform auth (dashboard users/customers)
- Sub-tenant auth (end-users of customer products)

### Data Fetching Pattern
- Server components fetch data and pass promises to client components
- Client components use `use()` hook with `<Suspense>` and `<ErrorBoundary>`
- State management with `nuqs` or similar for URL state

### Organization Scoping
- All operations must respect organization boundaries
- Include organization-level authorization in requirements
- Consider multi-tenant implications

### UI Component Standards
- Use shadcn/ui components exclusively
- Follow responsive design patterns

## Guidelines for Success

### Focus on Shipping
- Cover core functionality and critical edge cases
- Include error handling that prevents system failures
- Follow existing project patterns
- Get feedback quickly and iterate

### Be Specific
- Write clear user stories in given/when/then format
- Include concrete acceptance criteria
- Reference actual file paths and existing patterns

### Research Efficiently
- Read related files and existing implementations first
- Only spawn research tasks for complex features
- Verify assumptions with actual code