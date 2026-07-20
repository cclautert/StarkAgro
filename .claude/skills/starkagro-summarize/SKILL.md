---
name: starkagro-summarize
description: |
  Generates a summary of the current StarkAgro branch changes and optionally posts it to a
  GitHub Issue as a comment. Also generates a ready-to-copy PR description.
  Use this skill whenever the user says "summarize this branch", "summarize changes", "post to
  GitHub issue", "generate PR description", or "what changed in this branch" in the StarkAgro
  project context. Also trigger when the user provides an issue number and wants a summary posted.
---

# StarkAgro — Branch Change Summary

Generate a summary of branch changes and optionally post to GitHub Issue $ARGUMENTS.

## Step 1: Analyze changes

```bash
git diff $(git merge-base HEAD main) HEAD --name-only
git diff $(git merge-base HEAD main) HEAD -- StarkAgroAPI/Controllers/
git diff $(git merge-base HEAD main) HEAD -- StarkAgroAPI/Domain/Handlers/
git diff $(git merge-base HEAD main) HEAD -- StarkAgroAPI/Models/Entities/
```

## Step 2: Detect front-end relevance

Include a Front-End Integration Guide only if at least one of the following is true:
- A new controller action was added (new route exposed)
- A new request or response DTO was added under `Domain/Commands/`
- A new entity field was added that needs to be displayed in the UI

## Step 3: Build the comment

### If front-end changes detected:

```
## Summary
[2–4 bullet points describing what changed and why]

## MongoDB Changes
[New collections, fields, or indexes — or "None"]

## Front-End Integration Guide

### New REST Endpoints
For each endpoint:
- Method + route (e.g., GET /api/v1/Pivot/{id})
- Auth required: yes (Bearer JWT) / no
- Request body / query params (name, type, required/optional)
- Response fields the front-end displays
- Example Angular HttpClient snippet (relative URL)

### New DTOs / Types
[New request or response types with their fields]

### Authorization
[Requires Bearer JWT — which Angular route guard applies]

### Notes
[Validation rules, error codes, edge cases affecting the UI]
```

### If no front-end changes:

```
## Summary
[2–4 bullet points describing what changed and why]

## MongoDB Changes
[New collections, fields, or indexes — or "None"]
```

## Step 4: Confirm before posting

Show the generated comment and ask:
> "Would you like me to post this summary to issue #$ARGUMENTS? (yes / no)"

Only call `create_issue_comment(owner="cclautert", repo="StarkAgro", issue_number=NNN)` if the user answers yes.

## Step 5: PR description

Present this for the user to copy (regardless of the posting decision):

```markdown
## What changed
[2–4 bullet points]

## MongoDB changes
[New collections, fields, or indexes — or "None"]

## New API endpoints
[Method + path for each — or "None"]

## Test coverage
[Tests added, or "No new tests"]

Closes #NNN
```
