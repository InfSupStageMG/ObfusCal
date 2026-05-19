import xml.etree.ElementTree as ET
import os, sys

trx_file = "./TestResults/test-results.trx"
summary_file = os.environ.get("GITHUB_STEP_SUMMARY")

with open(summary_file, "a", encoding="utf-8") as f:
    f.write("### 🧪 Test Results Summary\n\n")

    if not os.path.exists(trx_file):
        f.write("⚠️ **No test results file found.** The build likely failed before tests could run.\n")
        sys.exit(0)

    try:
        tree = ET.parse(trx_file)
        root = tree.getroot()
        ns = {"v": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}

        counters = root.find(".//v:Counters", ns)
        if counters is None:
            f.write("⚠️ Could not find test counters in TRX file.\n")
            sys.exit(0)

        total = counters.attrib.get("total", "0")
        passed = counters.attrib.get("passed", "0")
        failed = int(counters.attrib.get("failed", "0"))

        f.write(f"- **Total Tests:** {total}\n")
        f.write(f"- **Passed:** ✅ {passed}\n")
        f.write(f"- **Failed:** ❌ {failed}\n\n")

        if failed > 0:
            f.write("#### ❌ Failed Tests Details\n\n")
            for result in root.findall(".//v:UnitTestResult", ns):
                if result.attrib.get("outcome") == "Failed":
                    test_name = result.attrib.get("testName", "Unknown Test")
                    msg_el = result.find(".//v:ErrorInfo/v:Message", ns)
                    message = msg_el.text if msg_el is not None else "No error message"
                    f.write(f"<details><summary><b>{test_name}</b></summary>\n\n")
                    f.write("```text\n")
                    f.write(f"{message}\n")
                    f.write("```\n")
                    f.write("</details>\n\n")

    except Exception as e:
        f.write(f"⚠️ Error parsing TRX file: {str(e)}\n")
