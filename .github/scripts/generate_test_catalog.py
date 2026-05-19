import xml.etree.ElementTree as ET
import os, sys
from collections import defaultdict

trx_file = "./TestResults/test-results.trx"
out_md = "./TestResults/test-catalog.md"

if not os.path.exists(trx_file):
    sys.exit(0)

ns = {"v": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
root = ET.parse(trx_file).getroot()

# Build map: test name -> outcome
outcomes = {
    r.attrib.get("testName", ""): r.attrib.get("outcome", "Unknown")
    for r in root.findall(".//v:UnitTestResult", ns)
}

# Build map: test name -> fully qualified class name
# TestDefinitions contains ALL discovered tests, even if skipped
class_map = {}
for unit_test in root.findall(".//v:UnitTest", ns):
    name = unit_test.attrib.get("name", "")
    method_el = unit_test.find("v:TestMethod", ns)
    if method_el is not None:
        class_map[name] = method_el.attrib.get("className", "Unknown")

# Build tree: class -> [test names]
tree = defaultdict(list)
for test_name, class_name in class_map.items():
    outcome = outcomes.get(test_name, "NotRun")
    icon = {"Passed": "✅", "Failed": "❌", "NotExecuted": "⏭️"}.get(outcome, "❓")
    tree[class_name].append((test_name, icon))

# Sort classes; strip method name from test_name for display
# (MSTest stores the method name as the testName, but qualified names appear
# in className, so the display name is just test_name itself)
lines = [
    "# Test Catalog\n",
    f"_Auto-generated from CI run. {sum(len(v) for v in tree.values())} tests across {len(tree)} classes._\n\n",
]

# Group by top-level namespace segment for collapsible sections
ns_groups = defaultdict(list)
for class_name in sorted(tree.keys()):
    # e.g. "ObfusCal.Tests.Domain.ObfuscationPipelineTests"
    #  -> group by "Domain" (third segment), or "Root" if flat
    parts = class_name.split(".")
    group = parts[2] if len(parts) > 2 else parts[-1]
    ns_groups[group].append(class_name)

for group, classes in sorted(ns_groups.items()):
    total_in_group = sum(len(tree[c]) for c in classes)
    passed_in_group = sum(1 for c in classes for (_, icon) in tree[c] if icon == "✅")
    lines.append(f"## {group} ({passed_in_group}/{total_in_group})\n\n")

    for class_name in sorted(classes):
        short = class_name.split(".")[-1]
        tests = sorted(tree[class_name], key=lambda t: t[0])
        passed = sum(1 for _, icon in tests if icon == "✅")
        lines.append(f"<details>\n<summary><b>{short}</b> - {passed}/{len(tests)}</summary>\n\n")
        lines.append("| Test | Result |\n|------|--------|\n")
        for test_name, icon in tests:
            lines.append(f"| `{test_name}` | {icon} |\n")
        lines.append("\n</details>\n\n")

os.makedirs("./TestResults", exist_ok=True)
with open(out_md, "w", encoding="utf-8") as f:
    f.writelines(lines)

print(f"Test catalog written to {out_md}")
