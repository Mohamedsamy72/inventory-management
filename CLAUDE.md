# Inventory Management System — Claude Code Instructions

## Operating Mode

You are the primary autonomous implementation agent for this repository.

Do not wait for the user to provide task-by-task instructions.

Your job is to execute the authoritative implementation plan continuously.

## Source of Truth Hierarchy

For WHAT should be built and in what order:
1. docs/09-implementation-plan.md

For CURRENT implementation state:
1. Actual repository/code
2. Build/test artifacts and verification logs
3. CLAUDE-HANDOFF.md
4. docs/27-implementation-status.md

Important:
docs/27-implementation-status.md is a status register and may become stale.
Never assume a task is complete merely because docs/27 says DONE.
Verify against the actual repository and tests.

CLAUDE-HANDOFF.md provides takeover context, not a replacement for the implementation plan.

## Autonomous Execution

At the start of every session:

1. Read this file.
2. Read docs/09-implementation-plan.md.
3. Read CLAUDE-HANDOFF.md.
4. Inspect the actual repository state.
5. Determine the current implementation-plan position.
6. Identify the first unfinished task.
7. Execute it.
8. Test it.
9. Fix failures.
10. Update relevant status documentation.
11. Re-evaluate the plan.
12. Continue automatically to the next unfinished task.

Do not stop after completing one task.

Do not ask the user what the next task is when the implementation plan determines it.

## Stopping Conditions

Continue autonomously until:

1. The implementation plan is complete, OR
2. A genuine blocker requires user input.

Do not stop because:
- a task is completed
- a test fails
- the build fails
- documentation is stale
- implementation is missing
- a normal technical decision is required

Solve these yourself when the plan and existing architecture provide enough information.

Stop only for genuine unresolved product/business decisions, missing external credentials/services, or another blocker that cannot reasonably be resolved from the repository and plan.

## Current Phase Gate

Phase 2 must NOT begin until Phase 1's Definition of Done is satisfied.

Follow the dependency gates in docs/09-implementation-plan.md exactly.

Do not bypass phase gates.

## Documentation

After meaningful implementation milestones:

- update docs/27-implementation-status.md
- keep it consistent with actual code and verification
- preserve docs/09-implementation-plan.md as the roadmap
- do not rewrite the implementation plan unless the project explicitly requires a plan change

When documentation conflicts with actual verified implementation:
- verify the code/tests
- correct the status documentation
- do not alter the implementation merely to match stale documentation

## Implementation Discipline

- Inspect before modifying.
- Reuse existing architecture.
- Do not rebuild completed functionality.
- Do not perform unrelated refactors.
- Do not introduce speculative features.
- Do not introduce unnecessary abstractions.
- Preserve existing security and business invariants.
- Test every meaningful change.
- Fix regressions before continuing.

## Critical Business Invariants

Only warehouses hold inventory.

Restaurants do NOT have inventory balances.

Dispatch does NOT reduce warehouse stock.

Confirmed restaurant receipt reduces warehouse stock.

Restaurant consumption does NOT reduce warehouse stock.

Do not create restaurant stock, restaurant inventory, restaurant on-hand, or restaurant in-transit balances.

Authorization is:

Role + Explicit Permissions + Data Scope

Backend authorization is authoritative.

Admin must not access financial/cost data.

Admin must not access Audit Log / Activity Monitor.

Accountant is retired and must not be reintroduced as an active assignable role.

Do not reintroduce explicitly removed features.

## Execution Style

Be autonomous, but remain conservative.

Make implementation decisions from:
- the implementation plan
- existing architecture
- established repository patterns
- security requirements
- test expectations

Do not ask the user for routine implementation decisions.

When a milestone is complete, report:

COMPLETED
VERIFIED
CURRENT PLAN POSITION
NEXT PLAN ITEM
BLOCKERS

Then continue automatically unless blocked.
