Generate a summary of the current branch changes and optionally post it to Azure DevOps task $ARGUMENTS.

## Step 1: Analyze changes
Run the following to identify what changed on this branch:
- `git diff $(git merge-base HEAD dev) HEAD --name-only` — list of changed files
- `git diff $(git merge-base HEAD dev) HEAD -- AP.AlixVault.API/AlixVault.Application/GraphQL/Queries/Query.cs` — detect new queries
- `git diff $(git merge-base HEAD dev) HEAD -- AP.AlixVault.API/AlixVault.Application/GraphQL/Mutations/Mutation.cs` — detect new mutations

## Step 2: Detect front-end relevance
The summary includes a Front-End Integration Guide ONLY if at least one of the following is true:
- A new method was added to `Query.cs` or `Mutation.cs`
- A new enum was added under `Domain/Clients/Enums/` or `Domain/Global/`
- A new DTO or GraphQL type was added that is part of a query/mutation return type

## Step 3: Build the task comment

### If front-end changes were detected:

```
## Summary

[2-4 bullet points describing what changed and why]

## Schema Changes
[List of new tables/columns/indexes per context (AlixVault per-tenant / Global), or "None"]

## Front-End Integration Guide

### New GraphQL Operations
For each new query or mutation:
- Exposed field name (camelCase — HotChocolate strips Get prefix)
- Input fields: name, type, required/optional
- Return fields relevant to the UI
- Example GraphQL snippet

### New Enums / Types
[Any new enums or DTOs the front-end needs, with their values]

### Authorization
[Policy used — InternalOrExternal / InternalOnly / specific tenant policy]

### Notes
[Edge cases, validation rules, or behavior that affects the UI]
```

### If no front-end changes:

```
## Summary

[2-4 bullet points describing what changed and why]

## Schema Changes
[List of new tables/columns/indexes per context, or "None"]
```

## Step 4: Ask before publishing
Show the generated comment to the user and ask:
"Would you like me to post this summary to task #$ARGUMENTS? (yes / no)"

Only call `wit_add_work_item_comment` if the user answers yes.

## Step 5: PR description
After the publish decision (regardless of yes/no), present the PR description text for the user to copy:

```
## What changed
[2-4 bullet points]

## Schema changes
[List per migration context or "None"]

## GraphQL surface
[New queries/mutations, or "None"]

## Test coverage
[Tests added, or "No new tests"]
```
