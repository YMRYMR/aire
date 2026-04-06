#!/usr/bin/env python3
"""
Convert structured help extraction to CSV for easy translation.
Each row represents a translatable string with context.
"""
import csv
import json
from pathlib import Path

INPUT_JSON = Path("help_structured_en.json")
OUTPUT_CSV = Path("help_translation_strings.csv")

def load_json(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)

def extract_strings(item):
    """Yield (field, text, notes, context) tuples."""
    item_id = item["id"]
    tab = item.get("tab", "")
    # Translational fields
    if "title" in item:
        yield (
            item_id,
            tab,
            "title",
            item["title"],
            item.get("translator_notes", {}).get("title", ""),
            "Short heading for this help item."
        )
    if "content" in item:
        yield (
            item_id,
            tab,
            "content",
            item["content"],
            item.get("translator_notes", {}).get("content", ""),
            "Main explanatory text."
        )
    if "imageCaption" in item:
        yield (
            item_id,
            tab,
            "imageCaption",
            item["imageCaption"],
            item.get("translator_notes", {}).get("imageCaption", ""),
            "Caption for an image."
        )
    if "intro" in item:
        yield (
            item_id,
            tab,
            "intro",
            item["intro"],
            item.get("translator_notes", {}).get("intro", ""),
            "Introductory text above a table."
        )
    # Links
    if "links" in item:
        for idx, link in enumerate(item["links"]):
            yield (
                item_id,
                tab,
                f"links[{idx}].label",
                link["label"],
                "Translate the label; keep action unchanged.",
                f"Hyperlink label (action: {link.get('action', '')})"
            )
    # Table columns
    if "cols" in item:
        for idx, col in enumerate(item["cols"]):
            yield (
                item_id,
                tab,
                f"cols[{idx}]",
                col,
                "Table column header.",
                "Keep short and consistent."
            )
    # Table rows (each row is an array of cells)
    if "rows" in item:
        for row_idx, row in enumerate(item["rows"]):
            for cell_idx, cell in enumerate(row):
                yield (
                    item_id,
                    tab,
                    f"rows[{row_idx}][{cell_idx}]",
                    cell,
                    "Table cell content.",
                    "May contain placeholders."
                )

def main():
    data = load_json(INPUT_JSON)
    items = data["items"]
    
    rows = []
    for item in items:
        for row in extract_strings(item):
            rows.append(row)
    
    with open(OUTPUT_CSV, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f)
        writer.writerow(["ID", "Tab", "Field", "Text", "Translator Notes", "Context"])
        writer.writerows(rows)
    
    print(f"CSV exported: {OUTPUT_CSV} ({len(rows)} strings)")

if __name__ == "__main__":
    main()