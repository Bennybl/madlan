---
name: madlan-implementation-step
description: Implement one requested, approved step from the Madlan implementation plan through a reviewable GitHub pull request.
---

# Madlan implementation step

Read `madlan_implementation_plan.md` and identify the step requested by the user. If no step is specified, ask which numbered step to implement.

Before starting, confirm the preceding step's pull request has the user's approval and is merged into the base branch. For step 1 only, create the private GitHub repository before creating its implementation branch.

For the requested step:

1. Create a descriptive branch from the approved base branch.
2. Make only the changes required by that step and its directly necessary documentation.
3. After each meaningful change, review it against both the requested step and the architecture/project as a whole. Correct scope drift, inconsistent conventions and missing documentation before continuing.
4. Run the step's specified verification and any focused tests needed to validate the change.
5. Add a factual entry to `docs/ai-log.md` for every commit. Record material user feedback and the resulting change when the user asks for a correction.
6. Commit the changes with a concise message, push the branch, and open a pull request against the base branch.
7. Report the pull-request URL and wait for the user's approval. Do not merge it or begin the next step.

If a verification failure reveals an implementation error, fix it and record a factual entry in `docs/ai-log.md` when it is a real AI-assisted mistake. Keep the pull request description focused on behavior, validation and known limits.
