import argparse
import json
import os
import re
import zipfile
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR / "data"
DEFAULT_SOURCE = DATA_DIR / "size_specs.xlsx"
DEFAULT_OUTPUT = DATA_DIR / "size_specs_index.json"
NS = {"a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}


def parse_args():
    parser = argparse.ArgumentParser(
        description="Build a normalized JSON size index from the Temu spec workbook."
    )
    parser.add_argument(
        "--source",
        default=str(DEFAULT_SOURCE),
        help="Path to the source spec workbook.",
    )
    parser.add_argument(
        "--output",
        default=str(DEFAULT_OUTPUT),
        help="Path to the output JSON index.",
    )
    return parser.parse_args()


def read_shared_strings(archive):
    root = ET.fromstring(archive.read("xl/sharedStrings.xml"))
    values = []
    for node in root.findall("a:si", NS):
        values.append("".join((t.text or "") for t in node.iterfind(".//a:t", NS)))
    return values


def read_sheet_rows(archive, shared_strings, sheet_number=1):
    root = ET.fromstring(archive.read(f"xl/worksheets/sheet{sheet_number}.xml"))
    sheet_data = root.find("a:sheetData", NS)
    rows = []
    for row in sheet_data.findall("a:row", NS):
        record = {}
        for cell in row.findall("a:c", NS):
            ref = cell.attrib.get("r", "")
            col_match = re.match(r"[A-Z]+", ref)
            if not col_match:
                continue
            col = col_match.group(0)
            cell_type = cell.attrib.get("t")
            value_node = cell.find("a:v", NS)
            value = "" if value_node is None else value_node.text
            if cell_type == "s" and value not in ("", None):
                value = shared_strings[int(value)]
            record[col] = value
        if record:
            rows.append(record)
    return rows


def read_sheet_names(archive):
    root = ET.fromstring(archive.read("xl/workbook.xml"))
    sheets = root.find("a:sheets", NS)
    return [sheet.attrib.get("name", "") for sheet in sheets]


def parse_length_range(text):
    match = re.fullmatch(r"\s*(\d+)\s*-\s*(\d+)\s*", text or "")
    if not match:
        return None, None
    return int(match.group(1)), int(match.group(2))


def parse_price_range(text):
    match = re.fullmatch(r"\s*(\d+(?:\.\d+)?)\s*-\s*(\d+(?:\.\d+)?)\s*", text or "")
    if not match:
        return None, None
    return float(match.group(1)), float(match.group(2))


def to_number(value):
    if value in ("", None):
        return None
    return float(value)


def build_payload(source_path):
    with zipfile.ZipFile(source_path) as archive:
        shared_strings = read_shared_strings(archive)
        sheet_names = read_sheet_names(archive)
        rows = read_sheet_rows(archive, shared_strings, sheet_number=1)

    headers = rows[0]
    records = []
    for row_number, row in enumerate(rows[1:], start=2):
        length_min, length_max = parse_length_range(row.get("B", ""))
        price_min, price_max = parse_price_range(row.get("G", ""))
        records.append(
            {
                "row_number": row_number,
                "width_cm": int(float(row["A"])) if row.get("A") not in ("", None) else None,
                "length_range_text": row.get("B", ""),
                "length_min_cm": length_min,
                "length_max_cm": length_max,
                "longest_edge_cm": to_number(row.get("C")),
                "second_longest_edge_cm": to_number(row.get("D")),
                "shortest_edge_cm": to_number(row.get("E")),
                "weight_g": to_number(row.get("F")),
                "declared_price_range_text": row.get("G", ""),
                "declared_price_min": price_min,
                "declared_price_max": price_max,
            }
        )

    return {
        "source_file": str(source_path),
        "source_last_modified": datetime.fromtimestamp(
            os.path.getmtime(source_path)
        ).isoformat(timespec="seconds"),
        "generated_at": datetime.now().isoformat(timespec="seconds"),
        "sheet_names": sheet_names,
        "header_map": {
            "A": headers.get("A"),
            "B": headers.get("B"),
            "C": headers.get("C"),
            "D": headers.get("D"),
            "E": headers.get("E"),
            "F": headers.get("F"),
            "G": headers.get("G"),
        },
        "record_count": len(records),
        "records": records,
    }


def main():
    args = parse_args()
    source_path = Path(args.source)
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    payload = build_payload(source_path)
    with output_path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2)
    print(output_path)
    print(f"records={payload['record_count']}")


if __name__ == "__main__":
    main()
