# Verification

Choose checks in proportion to the changed slice, but move from narrow evidence to broad confidence.

## Sequence

1. Run the smallest test that reproduces or protects the behavior. Use an xUnit filter when the relevant class/method is known.
2. Run neighboring tests for the same service, controller, constraint, or integration.
3. Run the affected test project:

```powershell
dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj
```

4. Build the solution:

```powershell
dotnet build UrbanService.sln
```

5. Inspect only the intended diff and finish with:

```powershell
git diff --check
git diff --stat
git status --short
```

For documentation/skill-only work, validate the artifact and links instead of running unrelated application tests, then run the repository diff/worktree checks.

## Focus areas

- API: route aliases, authorization attributes, model binding, response shape, status/error behavior.
- BLL: validation, ownership, transition matrix, transaction behavior, cancellation, concurrency outcome.
- DAL: server-translatable query, constraint/index mapping, migration and snapshot agreement.
- Incident/Feedback: links, candidate lifecycle, merge, subscriptions, events, compatibility fields, public projections.
- Messenger/Zalo: disabled paths, signature rejection, idempotency, isolation, queue/worker recovery, no real external calls.

## Dirty-worktree verification

Compare the final status with the recorded baseline. Name pre-existing dirty files separately from files changed for the current task. Do not use broad staging, checkout, reset, restore, clean, or repository-wide formatting as a verification shortcut.

## Honest gaps

Report each command exactly as run and its pass/fail/skipped result. If a test cannot run because of tooling, credentials, network, database, another dirty change, or time, state what remains unverified and why. A build pass does not prove authorization, database constraints, or webhook idempotency unless the relevant behavior was exercised.
