import os
import re

IGNORE_DIRS = {'.git', '.github', '.idea', '.vs', 'bin', 'obj', 'TestResults', 'site', 'StrykerOutput', 'certs', 'Migrations'}
IGNORE_FILES = {'.DS_Store', 'Thumbs.db'}

# This dictionary preserves your helpful inline comments!
COMMENTS = {
    "ObfusCal.Domain": "# Core business rules, domain models, obfuscation transformers",
    "ObfusCal.Application": "# Use cases (CQRS), interfaces, obfuscation pipeline",
    "ObfusCal.Infrastructure": "# Calendar adapters, EF Core persistence, storage implementations",
    "ObfusCal.Api": "# ASP.NET Core entry point, controllers, DI composition root",
    "ObfusCal.Plugins.GoogleCalendar": "# Google Calendar source plugin (built alongside Api, output to plugins/)",
    "ObfusCal.Plugins.ICloudCalendar": "# iCloud CalDAV source plugin (built alongside Api, output to plugins/)",
    "ObfusCal.Tests": "# Unit and integration tests",
    "docs": "# arc42 architecture documentation (served via MkDocs)",
    "plugins": "# Plugin DLL drop folder scanned at startup",
    "certs": "# Local TLS material (gitignored except README)"
}

def generate_tree(dir_path, prefix="", depth=0, max_depth=2):
    if depth >= max_depth:
        return ""
    try:
        entries = sorted(os.listdir(dir_path))
    except FileNotFoundError:
        return f"{prefix}Directory not found.\n"

    dirs = [e for e in entries if os.path.isdir(os.path.join(dir_path, e)) and e not in IGNORE_DIRS]
    files = [e for e in entries if os.path.isfile(os.path.join(dir_path, e)) and e not in IGNORE_FILES]

    valid_entries = dirs + files
    tree_str = ""

    for i, entry in enumerate(valid_entries):
        is_last = (i == len(valid_entries) - 1)
        connector = "└── " if is_last else "├── "

        comment = COMMENTS.get(entry, "")
        display_name = f"{entry}/" if os.path.isdir(os.path.join(dir_path, entry)) else entry
        line = f"{prefix}{connector}{display_name}"

        if comment:
            line = f"{line.ljust(38)} {comment}"

        tree_str += f"{line}\n"

        if os.path.isdir(os.path.join(dir_path, entry)):
            extension = "    " if is_last else "│   "
            tree_str += generate_tree(os.path.join(dir_path, entry), prefix + extension, depth + 1, max_depth)

    return tree_str

def update_markdown_files():
    # Regex looks for: <!-- START_TREE path="X" max_depth="Y" --> ... <!-- END_TREE -->
    tree_pattern = re.compile(
        r'(<!-- START_TREE path="([^"]+)" max_depth="(\d+)" -->\n).*?(<!-- END_TREE -->)',
        re.DOTALL)

    # Regex looks for: <!-- START_SNIPPET path="X" --> ... <!-- END_SNIPPET -->
    snippet_pattern = re.compile(
        r'(<!-- START_SNIPPET path="([^"]+)" -->\n).*?(<!-- END_SNIPPET -->)',
        re.DOTALL)

    for filepath in ['README.md', 'docs/clean-architecture.md']:
        if not os.path.exists(filepath):
            continue

        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()

        def replacer(match):
            start_marker = match.group(1)
            path = match.group(2)
            max_depth = int(match.group(3))
            end_marker = match.group(4)

            tree_content = "```text\n"
            tree_content += f"{os.path.basename(os.path.abspath(path))}/\n" if path != "." else "ObfusCal/\n"
            tree_content += generate_tree(path, max_depth=max_depth)
            tree_content += "```\n"

            return f"{start_marker}{tree_content}{end_marker}"

        def snippet_replacer(match):
            start_marker = match.group(1)
            path = match.group(2)
            end_marker = match.group(3)
            ext = os.path.splitext(path)[1].lstrip('.')
            lang = ext if ext else 'text'
            try:
                with open(path, 'r', encoding='utf-8') as sf:
                    file_content = sf.read().rstrip('\n')
                snippet_body = f"```{lang}\n{file_content}\n```\n"
            except FileNotFoundError:
                snippet_body = f"```{lang}\n// File not found: {path}\n```\n"
            return f"{start_marker}{snippet_body}{end_marker}"

        new_content = tree_pattern.sub(replacer, content)
        new_content = snippet_pattern.sub(snippet_replacer, new_content)

        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)

if __name__ == "__main__":
    update_markdown_files()
