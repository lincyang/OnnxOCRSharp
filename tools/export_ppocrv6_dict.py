#!/usr/bin/env python3
"""Export character_dict from PP-OCRv6 rec inference.yml to a txt file."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


def extract_character_dict(yml_path: Path) -> list[str]:
    text = yml_path.read_text(encoding="utf-8")
    in_dict = False
    chars: list[str] = []

    for line in text.splitlines():
        if line.strip() == "character_dict:":
            in_dict = True
            continue

        if not in_dict:
            continue

        match = re.match(r" +- (.+)", line)
        if not match:
            break

        value = match.group(1)
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
        value = value.rstrip("\r\n")
        chars.append(value)

    if not chars:
        raise ValueError(f"No character_dict entries found in {yml_path}")

    return chars


def write_dict(chars: list[str], output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(chars) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inference_yml", type=Path, help="Path to rec inference.yml")
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        default=Path("models/ppocrv6/ppocrv6_tiny_dict.txt"),
        help="Output dictionary txt path",
    )
    args = parser.parse_args()

    chars = extract_character_dict(args.inference_yml)
    write_dict(chars, args.output)
    print(f"Exported {len(chars)} characters to {args.output}")


if __name__ == "__main__":
    main()
