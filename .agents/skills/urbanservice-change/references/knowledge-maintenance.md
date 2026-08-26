# Knowledge maintenance

Use this process only for durable knowledge about how future Codex tasks should operate in UrbanService. Ordinary implementation details belong in code/tests, not in the skill.

## Admission criteria

A proposal must satisfy all three:

1. **Non-obvious:** a capable agent could reasonably miss it from a quick code read, and missing it would cause a recurring error or risk.
2. **Reusable:** it applies across future tasks or a stable domain slice, not only to one fix, one temporary branch, or one line.
3. **Evidence-backed:** the current session can point to verified code, tests, migrations, runtime evidence, or an explicit user decision.

Reject stale conclusions, speculative guidance, copied repository-specific rules from another project, facts already obvious in `AGENTS.md`, and historical narrative that does not change a future decision.

## Proposal before writing

1. Search `AGENTS.md`, this skill, its references, and relevant legacy evidence for duplicates or contradictions.
2. Revalidate the candidate against the current worktree.
3. At the end of the task, propose at most 1–3 high-value items. For each, give a short title, evidence, target file/section, and exact concise rule.
4. Ask the user which proposals to approve. Do not edit the skill, `AGENTS.md`, or legacy `skill/` knowledge until approval, unless the user explicitly requested the knowledge update in the current task.
5. After approval, write only the approved content, update in place instead of duplicating, validate links/frontmatter, and report the knowledge files changed.

The native `.agents/skills/urbanservice-change/` tree is the active Codex workflow. Keep `skill/` physically present as legacy/archive material unless the user explicitly requests a separate migration or cleanup.
