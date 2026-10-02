#!/usr/bin/env python3
"""
Turns Unity Test Framework results (NUnit 3 XML) into a short Markdown summary for humans and agents (TS-03).

  python3 Tools/ci/summarize_results.py artifacts/editmode-results.xml [more.xml ...]

Prints totals, then every failure with its message and the first stack lines.
Exit code: 0 all passed, 1 failures/errors, 2 no results found.
"""
import sys, glob
import xml.etree.ElementTree as ET


def summarize(paths):
    files = [p for pattern in paths for p in glob.glob(pattern)]
    if not files:
        print("No test result files found for: " + " ".join(paths))
        return 2
    lines, failed_total, total = [], 0, 0
    for path in files:
        root = ET.parse(path).getroot()
        run = root if root.tag == "test-run" else root.find(".//test-run")
        attrs = run.attrib if run is not None else root.attrib
        t, p, f, s = (int(attrs.get(k, 0)) for k in ("total", "passed", "failed", "skipped"))
        total += t
        failed_total += f
        icon = "✅" if f == 0 else "❌"
        lines.append(f"### {icon} {path}: {p}/{t} passed, {f} failed, {s} skipped ({float(attrs.get('duration', 0)):.1f}s)")
        for case in root.iter("test-case"):
            if case.attrib.get("result") not in ("Failed", "Error"):
                continue
            msg = (case.findtext("failure/message") or "").strip()
            stack = (case.findtext("failure/stack-trace") or "").strip().splitlines()[:4]
            lines.append(f"- **{case.attrib.get('fullname')}**")
            if msg:
                lines.append("  ```\n  " + msg.replace("\n", "\n  ") + "\n  ```")
            for s_line in stack:
                lines.append(f"    {s_line.strip()}")
    header = f"## Test summary: {total - failed_total}/{total} passed" + (" ✅" if failed_total == 0 else f", {failed_total} FAILED ❌")
    print(header)
    print()
    print("\n".join(lines))
    return 0 if failed_total == 0 else 1


if __name__ == "__main__":
    sys.exit(summarize(sys.argv[1:] or ["artifacts/*.xml"]))
