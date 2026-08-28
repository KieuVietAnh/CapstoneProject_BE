#!/usr/bin/env python3
"""Minimal BMAD skill renderer.

This repository was missing the helper expected by installed BMAD skills.
The script validates the requested skill directory and prints the absolute
workflow.md path for the caller to read and follow.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys


def main() -> int:
    parser = argparse.ArgumentParser(description="Render a BMAD skill workflow path.")
    parser.add_argument("--project-root", required=True, help="Absolute path to the project root.")
    parser.add_argument("--skill", required=True, help="Absolute path to the skill directory.")
    args = parser.parse_args()

    project_root = Path(args.project_root).expanduser().resolve()
    skill_root = Path(args.skill).expanduser().resolve()
    workflow_path = skill_root / "workflow.md"

    if not project_root.exists():
        print(f"Project root does not exist: {project_root}", file=sys.stderr)
        return 1

    if not skill_root.exists() or not skill_root.is_dir():
        print(f"Skill directory does not exist: {skill_root}", file=sys.stderr)
        return 1

    if not workflow_path.exists() or not workflow_path.is_file():
        print(f"workflow.md not found in skill directory: {workflow_path}", file=sys.stderr)
        return 1

    print(str(workflow_path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())