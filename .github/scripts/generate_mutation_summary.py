import glob
import json
import os
import sys
from collections import Counter

SUMMARY_FILE = os.environ.get("GITHUB_STEP_SUMMARY")
RUN_URL = os.environ.get("GITHUB_RUN_URL")
ARTIFACT_NAME = os.environ.get("MUTATION_ARTIFACT_NAME", "mutation-reports")


def _latest_report_path() -> str | None:
    candidates = glob.glob("./StrykerOutput/**/reports/mutation-report.json", recursive=True)
    if not candidates:
        return None
    return max(candidates, key=os.path.getmtime)


def _find_html_report() -> str | None:
    candidates = glob.glob("./StrykerOutput/**/reports/mutation-report.html", recursive=True)
    if not candidates:
        return None
    return max(candidates, key=os.path.getmtime)


def _collect_mutant_statuses(report: dict) -> Counter:
    if not isinstance(files, dict):
        return statuses
    for file_info in files.values():
        mutants = file_info.get("mutants") if isinstance(file_info, dict) else None
        if not isinstance(mutants, list):
            continue
        for mutant in mutants:
            if not isinstance(mutant, dict):
                continue
            status = mutant.get("status")
            if isinstance(status, str) and status:
                statuses[status] += 1
    return statuses


def _read_score(report: dict, statuses: Counter) -> float | None:
    for key in ("mutationScore", "mutationScoreResult"):
        value = report.get(key)
        if isinstance(value, (int, float)):
            return float(value)

    total = sum(statuses.values())
    if total == 0:
        return None

    killed_like = statuses.get("Killed", 0) + statuses.get("Timeout", 0)
    return round((killed_like / total) * 100.0, 2)


def main() -> int:
    if not SUMMARY_FILE:
        return 0

    with open(SUMMARY_FILE, "a", encoding="utf-8") as summary:
        summary.write("### Mutation Testing Summary\n\n")

        report_path = _latest_report_path()
        if not report_path:
            summary.write("- WARNING: No Stryker JSON report found at `StrykerOutput/**/reports/mutation-report.json`.\n")
            if RUN_URL:
                summary.write(f"- Full logs and artifacts: [Workflow run]({RUN_URL})\n")
            return 0

        try:
            with open(report_path, "r", encoding="utf-8-sig") as report_file:
                report = json.load(report_file)
        except Exception as ex:  # noqa: BLE001 - CI summary path should not fail the workflow
            summary.write(f"- WARNING: Failed to parse mutation report: `{ex}`\n")
            return 0

        statuses = _collect_mutant_statuses(report)
        score = _read_score(report, statuses)

        summary.write(f"- Report file: `{report_path}`\n")
        if score is None:
            summary.write("- Mutation score: `N/A`\n")
        else:
            summary.write(f"- Mutation score: **{score:.2f}%**\n")

        summary.write(f"- Killed: `{statuses.get('Killed', 0)}`\n")
        summary.write(f"- Survived: `{statuses.get('Survived', 0)}`\n")
        summary.write(f"- Timeout: `{statuses.get('Timeout', 0)}`\n")
        summary.write(f"- NoCoverage: `{statuses.get('NoCoverage', 0)}`\n")
        summary.write(f"- CompileError: `{statuses.get('CompileError', 0)}`\n")
        summary.write(f"- RuntimeError: `{statuses.get('RuntimeError', 0)}`\n")

        html_path = _find_html_report()
        if html_path:
            summary.write(f"\n- HTML report is uploaded in artifact **{ARTIFACT_NAME}** as `{html_path}`.\n")
        else:
            summary.write(f"\n- HTML report was not found locally; check artifact **{ARTIFACT_NAME}** if generated.\n")

        if RUN_URL:
            summary.write(f"- Open artifacts from this run: [Workflow run]({RUN_URL})\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())


