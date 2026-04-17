Implement the feature described by Azure DevOps work item $ARGUMENTS.

Follow this exact sequence and wait for each step to complete before starting the next.

## Step 1: Plan
Invoke the `feature-planner` agent with work item ID: $ARGUMENTS

After the plan is produced, present it to the user and ask:
"Does this plan look correct? Proceed with implementation? (yes / no / corrections)"

**Do NOT proceed to Step 2 until the user explicitly confirms.**

## Step 2: Implement
Invoke the `backend-expert` agent with this instruction:
"Read the plan at docs/features/{name}/plan.md (for work item $ARGUMENTS) and implement it.
Follow all files-to-create and files-to-modify exactly as listed. Run dotnet build when
complete to verify there are no compilation errors."

## Step 3: Migration (only if plan includes schema changes)
If the plan has a non-empty "Schema Changes" section:
Invoke the `migration-writer` agent:
"Create the EF Core migration for the schema changes described in docs/features/{name}/plan.md.
Determine whether this is a per-tenant (AlixVault) or global migration. Use the mandatory
scaffold-first workflow: run dotnet ef migrations add first, then replace Up() and Down() bodies only."

Skip this step entirely if the plan has no schema changes.

## Step 4: Tests
Invoke the `test-writer` agent:
"Write unit and integration tests for the feature implemented for work item $ARGUMENTS.
Read the production files listed in docs/features/{name}/plan.md to determine what to test.
Cover all happy paths, authorization rules, validation rules, and error branches.
Run dotnet test to verify all tests pass."

## Step 5: Review
Invoke the `code-reviewer` agent:
"Review all code changed for work item $ARGUMENTS. Run the full checklist including
the docs auto-update check. Flag any architectural violations."

Present findings to the user. If there are CRITICAL findings, clearly state that they must
be resolved before creating a PR.

## Step 6: Summary
Report:
- Work item: $ARGUMENTS
- Plan saved at: docs/features/{name}/plan.md
- Build passing: yes/no
- Migration created: yes / no / not needed
- Tests passing: yes/no
- Review: X critical / X warnings / X suggestions
- Ready for PR: yes / no

## Step 7: Post summaries to Azure DevOps

Only proceed if tests pass and there are no CRITICAL review findings.

### 7a — Front-end integration guide (post as Task comment)
Only perform this step if the code-reviewer output or the plan's "Schema Changes" / "Files to
Create" sections include new GraphQL queries, mutations, enums, or DTOs exposed at the API.
Skip silently if there are no front-end-facing changes.

If front-end changes exist, ask the user:
"New GraphQL operations were added. Please provide the Task ID where I should post the
front-end integration guide."

Then use `wit_add_work_item_comment` to post on that Task ID with this structure:

```
## Front-End Integration Guide

### New GraphQL Operations
List each new query or mutation with:
- Exposed field name (camelCase, HotChocolate strips Get prefix)
- Input type fields (name, type, required/optional)
- Return type fields the front-end needs to display
- Example GraphQL operation snippet

### Authorization
Who can call this operation (InternalOrExternal / InternalOnly / specific policy).

### Notes
Any edge cases, validation rules, or behavior that affects the UI.
```

### 7b — PR description
Generate the PR description text and present it to the user (do NOT create the PR automatically):

```
## What changed
[2-4 bullet points of the implemented changes]

## Schema changes
[List of new tables/columns/indexes per migration context (AlixVault/Global), or "None"]

## GraphQL surface
[List of new queries/mutations exposed]

## Test coverage
[Summary of tests added]
```
