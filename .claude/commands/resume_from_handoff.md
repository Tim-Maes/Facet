# resume_from_handoff.md - Resume work from a handoff document

You are tasked with resuming work from a handoff document through an interactive process. These handoffs contain critical context, learnings, and next steps from previous work sessions that need to be understood and continued.

## Initial Response

When this command is invoked:

1. **Check if parameters were provided**:
   - If a handoff document path was provided as a parameter, skip the default message
   - Immediately read the handoff document FULLY
   - Begin the analysis process by ingesting relevant context from the handoff document, reading additional files it mentions
   - Then propose a course of action to the user and confirm, or ask for clarification on direction.

2. **If no parameters provided**, respond with:
```
I'll help you resume work from a handoff document. Let me find the available handoffs.

Which handoff would you like to resume from?

Tip: You can invoke this command directly with a handoff path: `/resume_from_handoff specification/feature/handoffs/handoff_YYYY-MM-DD_HH-MM-SS_topic.md`
```

Then wait for the user's input.

## Process Steps

### Step 1: Read and Analyze Handoff

1. **Read handoff document completely**:
   - Use the Read tool WITHOUT limit/offset parameters
   - Extract all sections:
     - Task(s) and their statuses
     - Recent changes
     - Learnings
     - Artifacts
     - Action items and next steps
     - Other notes

2. **Spawn focused research tasks**:
   Based on the handoff content, spawn parallel research tasks to verify current state:

   ```
   Task 1 - Verify recent changes:
   Check if the recent changes mentioned in the handoff still exist.
   1. Verify files mentioned in "Recent changes" section
   2. Check if the described changes are still present
   3. Look for any subsequent modifications
   4. Identify any conflicts or regressions
   Use tools: Read, Grep, Glob
   Return: Current state of recent changes with file:line references
   ```

   ```
   Task 2 - Validate current codebase state:
   Verify the current state against what's described in the handoff.
   1. Check files mentioned in "Learnings" section
   2. Verify patterns and implementations still exist
   3. Look for any breaking changes since handoff
   4. Identify new related code added since handoff
   Use tools: Read, Grep, Glob
   Return: Validation results and any discrepancies found
   ```

   ```
   Task 3 - Gather artifact context:
   Read all artifacts mentioned in the handoff.
   1. Read feature documents listed in "Artifacts"
   2. Read implementation plans referenced
   3. Read any research documents mentioned
   4. Extract key requirements and decisions
   Use tools: Read
   Return: Summary of artifact contents and key decisions
   ```

3. **Wait for ALL sub-tasks to complete** before proceeding

4. **Read critical files identified**:
   - Read files from "Learnings" section completely
   - Read files from "Recent changes" to understand modifications
   - Read any new related files discovered during research

### Step 2: Synthesize and Present Analysis

1. **Present comprehensive analysis**:
   ```
   I've analyzed the handoff from [date] by [researcher]. Here's the current situation:

   **Original Tasks:**
   - [Task 1]: [Status from handoff] → [Current verification]
   - [Task 2]: [Status from handoff] → [Current verification]

   **Key Learnings Validated:**
   - [Learning with file:line reference] - [Still valid/Changed]
   - [Pattern discovered] - [Still applicable/Modified]

   **Recent Changes Status:**
   - [Change 1] - [Verified present/Missing/Modified]
   - [Change 2] - [Verified present/Missing/Modified]

   **Artifacts Reviewed:**
   - [Document 1]: [Key takeaway]
   - [Document 2]: [Key takeaway]

   **Recommended Next Actions:**
   Based on the handoff's action items and current state:
   1. [Most logical next step based on handoff]
   2. [Second priority action]
   3. [Additional tasks discovered]

   **Potential Issues Identified:**
   - [Any conflicts or regressions found]
   - [Missing dependencies or broken code]

   Shall I proceed with [recommended action 1], or would you like to adjust the approach?
   ```

2. **Get confirmation** before proceeding

### Step 3: Create Action Plan

1. **Use TodoWrite to create task list**:
   - Convert action items from handoff into todos
   - Add any new tasks discovered during analysis
   - Prioritize based on dependencies and handoff guidance

2. **Present the plan**:
   ```
   I've created a task list based on the handoff and current analysis:

   [Show todo list]

   Ready to begin with the first task: [task description]?
   ```

### Step 4: Begin Implementation

1. **Start with the first approved task**
2. **Reference learnings from handoff** throughout implementation
3. **Apply patterns and approaches documented** in the handoff
4. **Update progress** as tasks are completed

## Guidelines

1. **Be Thorough in Analysis**:
   - Read the entire handoff document first
   - Verify ALL mentioned changes still exist
   - Check for any regressions or conflicts
   - Read all referenced artifacts

2. **Be Interactive**:
   - Present findings before starting work
   - Get buy-in on the approach
   - Allow for course corrections
   - Adapt based on current state vs handoff state

3. **Leverage Handoff Wisdom**:
   - Pay special attention to "Learnings" section
   - Apply documented patterns and approaches
   - Avoid repeating mistakes mentioned
   - Build on discovered solutions

4. **Track Continuity**:
   - Use TodoWrite to maintain task continuity
   - Reference the handoff document in commits
   - Document any deviations from original plan
   - Consider creating a new handoff when done

5. **Validate Before Acting**:
   - Never assume handoff state matches current state
   - Verify all file references still exist
   - Check for breaking changes since handoff
   - Confirm patterns are still valid

## Common Scenarios

### Scenario 1: Clean Continuation
- All changes from handoff are present
- No conflicts or regressions
- Clear next steps in action items
- Proceed with recommended actions

### Scenario 2: Diverged Codebase
- Some changes missing or modified
- New related code added since handoff
- Need to reconcile differences
- Adapt plan based on current state

### Scenario 3: Incomplete Handoff Work
- Tasks marked as "in_progress" in handoff
- Need to complete unfinished work first
- May need to re-understand partial implementations
- Focus on completing before new work

### Scenario 4: Stale Handoff
- Significant time has passed
- Major refactoring has occurred
- Original approach may no longer apply
- Need to re-evaluate strategy

## Example Interaction Flow

```
User: /resume_from_handoff specification/feature/handoffs/handoff-0.md
Assistant: Let me read and analyze that handoff document...

[Reads handoff completely]
[Spawns research tasks]
[Waits for completion]
[Reads identified files]

I've analyzed the handoff from [date]. Here's the current situation...

[Presents analysis]

Shall I proceed with implementing the webhook validation fix, or would you like to adjust the approach?

User: Yes, proceed with the webhook validation
Assistant: [Creates todo list and begins implementation]
```