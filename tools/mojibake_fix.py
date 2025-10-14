#!/usr/bin/env python3
import sys
import json
import re
from pathlib import Path

# Regex for simple C# / XAML string literals (double quotes, allows escaped quotes)
STRING_LITERAL = re.compile(r'"([^"\\]*(?:\\.[^"\\]*)*)"')

# Characters that typically appear in mojibake sequences (Р, С, Ѓ, etc.)
SUSPECT_CHARS = set(
    chr(c) for c in (
        0x0400, 0x0401, 0x0402, 0x0403, 0x0404, 0x0405, 0x0406, 0x0407,
        0x0408, 0x0409, 0x040A, 0x040B, 0x040C, 0x040D, 0x040E, 0x040F,
        0x0410, 0x0411, 0x0412, 0x0413, 0x0414, 0x0415, 0x0416, 0x0417,
        0x0418, 0x0419, 0x041A, 0x041B, 0x041C, 0x041D, 0x041E, 0x041F,
        0x0420, 0x0421, 0x0422, 0x0423, 0x0424, 0x0425, 0x0426, 0x0427,
        0x0428, 0x0429, 0x042A, 0x042B, 0x042C, 0x042D, 0x042E, 0x042F,
        0x0430, 0x0431, 0x0432, 0x0433, 0x0434, 0x0435, 0x0436, 0x0437,
        0x0438, 0x0439, 0x043A, 0x043B, 0x043C, 0x043D, 0x043E, 0x043F,
        0x0440, 0x0441, 0x0442, 0x0443, 0x0444, 0x0445, 0x0446, 0x0447,
        0x0448, 0x0449, 0x044A, 0x044B, 0x044C, 0x044D, 0x044E, 0x044F,
        0x0450, 0x0451, 0x0452, 0x0453, 0x0454, 0x0455, 0x0456, 0x0457,
        0x0458, 0x0459, 0x045A, 0x045B, 0x045C, 0x045D, 0x045E, 0x045F,
        0x0490, 0x0491
    )
)

CP1251 = "cp1251"


def maybe_fix(content: str):
    # Quick reject: skip pure ASCII / digits
    if all(ord(ch) < 128 for ch in content):
        return None
    # Check if there are suspicious repeating patterns like Р?, С?, пї etc.
    if not any(ord(ch) >= 0x0452 or ch in ("\u00af", "\u00bf") for ch in content):
        return None
    try:
        raw = content.encode(CP1251)
        fixed = raw.decode("utf-8")
    except UnicodeError:
        return None
    if fixed == content or "\ufffd" in fixed:
        return None
    # Validate: ensure the fixed string contains Cyrillic or Latin letters in readable range
    if not any("А" <= ch <= "я" or ch in ("ё", "Ё") or ch.isascii() for ch in fixed):
        return None
    # Debug print for tracing
    print(f"[fix] {content!r} -> {fixed!r}")
    return fixed


def process_file(path: Path) -> bool:
    text = path.read_text(encoding="utf-8")
    changed = False

    def repl(match):
        nonlocal changed
        inner = match.group(1)
        fixed = maybe_fix(inner)
        if fixed is None:
            return match.group(0)
        changed = True
        literal = json.dumps(fixed, ensure_ascii=False)
        return literal

    new_text = STRING_LITERAL.sub(repl, text)
    if changed:
        path.write_text(new_text, encoding="utf-8")
    return changed


def main(argv):
    if len(argv) < 2:
        print("Usage: mojibake_fix.py <files...>")
        return 1
    any_changed = False
    for name in argv[1:]:
        path = Path(name)
        if not path.is_file():
            continue
        if process_file(path):
            print(f"Fixed {path}")
            any_changed = True
    return 0 if any_changed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
