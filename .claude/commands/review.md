Review all code changes on the current branch before creating a PR.

Invoke the `code-reviewer` agent with this instruction:

"Review all changed files on this branch relative to dev. Run the full checklist
(all items including the docs auto-update check). Flag any violations of multi-tenant
patterns, missing authorization, or improper DbContext usage.

$ARGUMENTS"

After the review, summarize:
- Total findings: X critical / X warnings / X suggestions
- Branch ready to merge: yes / no (blocked by criticals)
- Pending documentation updates (if any)
