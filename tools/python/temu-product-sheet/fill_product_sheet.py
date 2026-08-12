import argparse
import json
import os
import random
import re
import subprocess
import sys
import tempfile
from datetime import date
from math import ceil
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR / "data"
DEFAULT_SOURCE = DATA_DIR / "size_specs.xlsx"
DEFAULT_OUTPUT_DIR = Path("D:/temu_auto/excel")
DEFAULT_ASSERT_DIR = Path("D:/temu_auto/assert")
DEFAULT_TITLE_JSON = DATA_DIR / "title.json"
SUPPORTED_SIZE_IMAGE_EXTENSIONS = {
    ".png",
    ".jpg",
    ".jpeg",
    ".webp",
    ".bmp",
    ".gif",
    ".tif",
    ".tiff",
    ".jfif",
}
SOURCE_METADATA_NAME = ".sku-source.json"


def get_default_cache_dir():
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        return Path(local_app_data) / "ToolBox" / "cache" / "temu-product-sheet"
    return Path.home() / ".toolbox" / "cache" / "temu-product-sheet"


DEFAULT_INDEX = get_default_cache_dir() / "size_specs_index.json"


def parse_args():
    parser = argparse.ArgumentParser(
        description="Generate Miaoshou product JSON from SP folders and SKU sizes."
    )
    parser.add_argument(
        "--sizes",
        nargs="+",
        help='One or more SKU sizes such as "60*168cm".',
    )
    parser.add_argument(
        "--sp-dir",
        default=None,
        help="SPxx folder path. When provided, auto-extract sizes from main\\2-尺寸.png.",
    )
    parser.add_argument(
        "--assert-dir",
        default=str(DEFAULT_ASSERT_DIR),
        help="Root folder containing SPxx directories. Used by default when --sizes and --sp-dir are not provided.",
    )
    parser.add_argument(
        "--product-id",
        default="SP1",
        help="Value to write into 商品编号 for matched rows.",
    )
    parser.add_argument(
        "--template",
        default=None,
        help="Deprecated. Kept for compatibility; workbook output is no longer generated.",
    )
    parser.add_argument(
        "--index",
        default=str(DEFAULT_INDEX),
        help="Path to the JSON size index.",
    )
    parser.add_argument(
        "--source",
        default=str(DEFAULT_SOURCE),
        help="Path to the source spec workbook used when rebuilding the index.",
    )
    parser.add_argument(
        "--title-json",
        default=str(DEFAULT_TITLE_JSON),
        help="Path to the title JSON used to randomize 产品标题 for each SPxx row.",
    )
    parser.add_argument(
        "--title-chinese-only",
        action="store_true",
        help="Only randomize the Chinese 产品标题 and leave 英语标题 empty.",
    )
    parser.add_argument(
        "--output-dir",
        default=str(DEFAULT_OUTPUT_DIR),
        help="Directory for generated product JSON when --products-json is not provided.",
    )
    parser.add_argument(
        "--date",
        default=date.today().isoformat(),
        help="Date string for the output filename, format YYYY-MM-DD.",
    )
    parser.add_argument(
        "--output-name",
        default=None,
        help="Deprecated. Kept for compatibility; workbook output is no longer generated.",
    )
    parser.add_argument(
        "--products-json",
        default=None,
        help="Optional path for the generated Miaoshou product JSON.",
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=None,
        help="Optional random seed for repeatable price generation.",
    )
    return parser.parse_args()


def ensure_index(index_path, source_path):
    if not source_path.exists():
        raise FileNotFoundError(f"Source size spec workbook not found: {source_path}")

    build_script = Path(__file__).with_name("build_size_index.py")
    cmd = [
        sys.executable,
        str(build_script),
        "--source",
        str(source_path),
        "--output",
        str(index_path),
    ]
    subprocess.run(cmd, check=True)


def extract_sizes_from_sp_dir(sp_dir):
    main_dir = Path(sp_dir) / "main"
    candidates = sorted(
        (
            path
            for path in main_dir.iterdir()
            if path.is_file()
            and path.suffix.lower() in SUPPORTED_SIZE_IMAGE_EXTENSIONS
            and is_size_image_name(path)
        ),
        key=lambda path: path.name.casefold(),
    )
    if not candidates:
        raise FileNotFoundError(f"Automatic size image not found under: {main_dir}")

    image_path = candidates[0]
    ocr_texts = [run_windows_ocr(image_path)]
    with tempfile.TemporaryDirectory(prefix="ecomtool_size_ocr_") as temp_dir:
        for variant_path in build_size_ocr_variants(image_path, Path(temp_dir)):
            ocr_texts.append(run_windows_ocr(variant_path))

    return merge_size_items_from_ocr_texts(ocr_texts)


def is_size_image_name(image_path):
    name = image_path.stem.casefold()
    return name.startswith("2-") or "尺寸" in name or "size" in name


def run_windows_ocr(image_path):
    ps_script = f"""
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStream, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType = WindowsRuntime]

function AwaitResult($op, [Type]$resultType) {{
  $method = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {{
    $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1
  }} | Select-Object -First 1
  $generic = $method.MakeGenericMethod($resultType)
  $task = $generic.Invoke($null, @($op))
  $task.Wait()
  $task.Result
}}

$imagePath = '{str(image_path).replace("'", "''")}'
$file = AwaitResult ([Windows.Storage.StorageFile]::GetFileFromPathAsync($imagePath)) ([Windows.Storage.StorageFile])
$stream = AwaitResult ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = AwaitResult ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap = AwaitResult ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
$result = AwaitResult ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
$result.Text
"""
    completed = subprocess.run(
        ["powershell", "-NoProfile", "-Command", "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + ps_script],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return completed.stdout


def build_size_ocr_variants(image_path, temp_dir):
    try:
        from PIL import Image, ImageEnhance, ImageFilter, ImageOps
    except ImportError:
        return []

    image = Image.open(image_path)
    if image.mode == "RGBA":
        background = Image.new("RGB", image.size, "white")
        background.paste(image, mask=image.getchannel("A"))
        image = background
    else:
        image = image.convert("RGB")

    width, height = image.size
    configs = [
        ("bottom_20_scale3.png", 0.80, 0.20, False),
        ("bottom_25_scale3_sharp.png", 0.75, 0.25, True),
        ("bottom_30_scale3.png", 0.70, 0.30, False),
    ]
    variant_paths = []
    for name, top_ratio, height_ratio, sharpen in configs:
        top = int(height * top_ratio)
        bottom = min(height, top + int(height * height_ratio))
        crop = image.crop((0, top, width, bottom))
        resized = crop.resize((crop.width * 3, crop.height * 3), Image.Resampling.LANCZOS)
        if sharpen:
            resized = ImageEnhance.Contrast(resized).enhance(1.35)
            resized = resized.filter(ImageFilter.SHARPEN)

        output_path = temp_dir / name
        resized.save(output_path)
        variant_paths.append(output_path)

    threshold_configs = [
        ("bottom_22_scale3_threshold160.png", 0.78, 0.22, 3, 160),
        ("bottom_22_scale4_threshold180.png", 0.78, 0.22, 4, 180),
        ("bottom_20_scale5_threshold160.png", 0.80, 0.20, 5, 160),
    ]
    for name, top_ratio, height_ratio, scale, threshold in threshold_configs:
        top = int(height * top_ratio)
        bottom = min(height, top + int(height * height_ratio))
        crop = image.crop((0, top, width, bottom))
        gray = ImageOps.grayscale(crop)
        resized = gray.resize((gray.width * scale, gray.height * scale), Image.Resampling.LANCZOS)
        resized = resized.point(lambda pixel: 255 if pixel > threshold else 0)
        resized = ImageEnhance.Contrast(resized).enhance(1.8)
        resized = resized.filter(ImageFilter.SHARPEN)

        output_path = temp_dir / name
        resized.convert("RGB").save(output_path)
        variant_paths.append(output_path)

    strong_configs = [
        ("bottom_18_scale6_threshold170_bold.png", 0.00, 1.00, 0.82, 0.18, 6, 170),
        ("bottom_left_scale7_threshold170_bold.png", 0.02, 0.38, 0.84, 0.14, 7, 170),
        ("bottom_middle_scale7_threshold170_bold.png", 0.30, 0.70, 0.84, 0.14, 7, 170),
        ("bottom_right_scale7_threshold170_bold.png", 0.60, 0.98, 0.84, 0.14, 7, 170),
    ]
    for name, left_ratio, right_ratio, top_ratio, height_ratio, scale, threshold in strong_configs:
        left = max(0, int(width * left_ratio))
        right = min(width, int(width * right_ratio))
        top = int(height * top_ratio)
        bottom = min(height, top + int(height * height_ratio))
        crop = image.crop((left, top, right, bottom))
        gray = ImageOps.grayscale(crop)
        gray = ImageOps.expand(gray, border=max(12, crop.height // 10), fill=255)
        resized = gray.resize((gray.width * scale, gray.height * scale), Image.Resampling.LANCZOS)
        resized = resized.point(lambda pixel: 255 if pixel > threshold else 0)
        # Slightly thicken dark strokes so OCR is less likely to drop the 0 in values such as 50.
        resized = resized.filter(ImageFilter.MinFilter(3))
        resized = ImageEnhance.Contrast(resized).enhance(1.6)

        output_path = temp_dir / name
        resized.convert("RGB").save(output_path)
        variant_paths.append(output_path)

    return variant_paths


def merge_size_items_from_ocr_texts(texts):
    by_size = {}
    for text in texts:
        for item in parse_sizes_from_ocr_text(text):
            size_text = item["size_text"]
            existing = by_size.get(size_text)
            if existing is None or score_display_size(item["display_size_text"]) > score_display_size(existing["display_size_text"]):
                by_size[size_text] = item

    return sorted(by_size.values(), key=lambda item: parse_size_sort_key(item["size_text"]))


def parse_size_sort_key(size_text):
    match = re.search(r"(\d+(?:\.\d+)?)\s*\*\s*(\d+(?:\.\d+)?)\s*cm", size_text, re.I)
    if not match:
        return (9999, 9999)

    return (float(match.group(1)), float(match.group(2)))


def score_display_size(display_size):
    score = len(display_size)
    if "/" in display_size:
        score += 20
    score += display_size.count(".") * 5
    return score


def parse_sizes_from_ocr_text(text):
    normalized = normalize_ocr_size_text(text)
    unique = []
    seen = set()
    matches = list(re.finditer(r"(\d+(?:\.\d+)?)\s*(?:cm)?\s*[*xX]\s*(\d+(?:\.\d+)?)\s*cm", normalized, re.I))
    for index, match in enumerate(matches):
        width_text, length_text = match.groups()
        width_cm = float(width_text)
        length_cm = float(length_text)
        if not is_plausible_sku_size(width_cm, length_cm):
            continue

        size_text = f"{format_cm(width_cm)}*{format_cm(length_cm)}cm"
        if size_text in seen:
            continue

        seen.add(size_text)
        display_size_text = size_text

        next_start = matches[index + 1].start() if index + 1 < len(matches) else len(normalized)
        trailing_text = normalized[match.end():next_start]
        inch_match = re.search(
            r"/?\s*([0-9][^*]{0,24})\s*[*xX]\s*([0-9][^i]{0,24})\s*inch",
            trailing_text,
            re.I,
        )
        if inch_match or re.search(r"in\s*[\(\（]?\s*h|inch", trailing_text, re.I):
            display_size_text = f"{size_text}/{format_inches(width_cm)}*{format_inches(length_cm)}inch"

        unique.append(
            {
                "size_text": size_text,
                "display_size_text": display_size_text,
            }
        )

    fuzzy_items = parse_fuzzy_sizes_from_ocr_text(normalized)
    for item in fuzzy_items:
        if item["size_text"] in seen:
            continue

        seen.add(item["size_text"])
        unique.append(item)

    return unique


def parse_fuzzy_sizes_from_ocr_text(text):
    items = []
    seen = set()
    normalized = normalize_ocr_size_text(text)
    pattern = re.compile(
        r"(?<!\d)(\d{1,3}(?:\.\d+)?)\D{0,8}(\d{2,3}(?:\.\d+)?)\s*(?:cm|c\s*m|\(\s*m)\s*/\s*(.{0,60}?)(?:inch|in\s*\(?\s*h)",
        re.I,
    )
    for match in pattern.finditer(normalized):
        width_token, length_token, inch_text = match.groups()
        width_cm = float(width_token)
        length_cm = float(length_token)

        if not is_plausible_sku_size(width_cm, length_cm):
            continue

        size_text = f"{format_cm(width_cm)}*{format_cm(length_cm)}cm"
        if size_text in seen:
            continue

        seen.add(size_text)
        display_size_text = size_text
        inch_values = extract_inch_values(inch_text)
        if len(inch_values) >= 2:
            display_size_text = f"{size_text}/{format_inches(width_cm)}*{format_inches(length_cm)}inch"

        items.append(
            {
                "size_text": size_text,
                "display_size_text": display_size_text,
            }
        )

    return items


def normalize_ocr_size_text(text):
    normalized = text.replace("\r", "\n")
    normalized = re.sub(r"(?<=\d)[lI|](?=\d)", "1", normalized)
    normalized = normalized.translate(
        str.maketrans(
            {
                "＊": "*",
                "×": "*",
                "✕": "*",
                "﹡": "*",
                "榨": "*",
                "俨": "*",
                "夤": "*",
                "漡": "*",
                "聶": "*",
                "愐": "*",
                "樥": "*",
                "𩂰": "*",
                "敤": "*",
                "彊": "*",
                "蟬": "*",
                "，": ".",
                "．": ".",
                "·": ".",
                "﹞": ".",
                "ㄝ": ".",
            }
        )
    )
    return normalized


def extract_inch_values(text):
    normalized = normalize_ocr_size_text(text)
    values = []
    for match in re.finditer(r"(\d{1,3})\s*\.\s*(\d{1,2})", normalized):
        values.append(float(f"{match.group(1)}.{match.group(2)}"))

    return values


def is_plausible_sku_size(width_cm, length_cm):
    return 30 <= width_cm <= 100 and 100 <= length_cm <= 400


def format_cm(cm_value):
    return f"{cm_value:.2f}".rstrip("0").rstrip(".")


def format_inches(cm_value):
    return f"{cm_value / 2.54:.2f}".rstrip("0").rstrip(".")


def normalize_decimal_token(token):
    cleaned = token.strip()
    if not cleaned:
        return None

    if re.fullmatch(r"\d+(?:\.\d+)?", cleaned):
        return cleaned

    digit_groups = re.findall(r"\d+", cleaned)
    if not digit_groups:
        return None
    if len(digit_groups) == 1:
        return digit_groups[0]
    return f"{digit_groups[0]}.{''.join(digit_groups[1:])}"


def parse_size(size_text):
    match = re.search(r"^\s*(\d+(?:\.\d+)?)\s*(?:cm)?\s*\*\s*(\d+(?:\.\d+)?)\s*cm\b", size_text, re.I)
    if not match:
        raise ValueError(f"Unsupported size format: {size_text}")
    width = float(match.group(1))
    length = float(match.group(2))
    return width, length


def load_records(index_path):
    with index_path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)
    return payload["records"]


def load_titles(title_json_path):
    with Path(title_json_path).open("r", encoding="utf-8") as handle:
        payload = json.load(handle)

    title_values = payload.get("titles")
    if isinstance(title_values, list):
        cn_titles = [str(value).strip() for value in title_values if str(value).strip()]
        english_values = payload.get("english_title", payload.get("english_titles", payload.get("en_titles", [])))
        en_titles = [str(value).strip() for value in english_values if str(value).strip()] if isinstance(english_values, list) else []
        if not cn_titles:
            raise ValueError(f"No usable Chinese titles found in: {title_json_path}")
        return cn_titles, en_titles

    title_groups = payload.get("title_groups")
    if not isinstance(title_groups, list):
        raise ValueError(f"Invalid title JSON format: {title_json_path}")

    cn_titles = []
    en_titles = []
    for item in title_groups:
        if isinstance(item, dict):
            cn_text = str(item.get("cn") or "").strip()
            en_text = str(item.get("en") or "").strip()
            if cn_text:
                cn_titles.append(cn_text)
            if en_text:
                en_titles.append(en_text)

    if not cn_titles:
        raise ValueError(f"No usable Chinese titles found in: {title_json_path}")
    return cn_titles, en_titles


def match_record(records, width, length):
    record = match_record_by_values(records, width, length)
    if record is not None:
        return record

    rounded_width = ceil(width)
    rounded_length = ceil(length)
    if rounded_width == width and rounded_length == length:
        return None
    return match_record_by_values(records, rounded_width, rounded_length)


def match_record_by_values(records, width, length):
    for record in records:
        width_min = record.get("width_min_cm", record.get("width_cm"))
        width_max = record.get("width_max_cm", record.get("width_cm"))
        if width_min is None or width_max is None:
            continue
        if not (width_min <= width <= width_max):
            continue
        if record["length_min_cm"] is None or record["length_max_cm"] is None:
            continue
        if record["length_min_cm"] <= length <= record["length_max_cm"]:
            return record
    return None


def random_price(record):
    price_min = record["declared_price_min"]
    price_max = record["declared_price_max"]
    if price_min is None or price_max is None:
        return None
    min_cents = int(round(price_min * 100))
    max_cents = int(round(price_max * 100))
    if max_cents < min_cents:
        row_number = record.get("row_number", "?")
        raise ValueError(f"Invalid price range at size_specs row {row_number}: {record['declared_price_range_text']}")
    return random.randint(min_cents, max_cents) / 100.0


def clear_sheet_rows(sheet, start_row, end_col):
    max_row = max(sheet.max_row, start_row)
    for row in range(start_row, max_row + 1):
        for col in range(1, end_col + 1):
            sheet.cell(row, col).value = None


def clear_rows(sheet, start_row=2, end_row=500, end_col=7):
    for row in range(start_row, end_row + 1):
        for col in range(1, end_col + 1):
            sheet.cell(row, col).value = None


def apply_template_row_format(sheet, target_row, template_row, start_col, end_col):
    source_height = sheet.row_dimensions[template_row].height
    if source_height is not None:
        sheet.row_dimensions[target_row].height = source_height

    for col in range(start_col, end_col + 1):
        source = sheet.cell(template_row, col)
        target = sheet.cell(target_row, col)
        if source.has_style:
            target._style = copy(source._style)
        if source.number_format:
            target.number_format = source.number_format
        if source.font:
            target.font = copy(source.font)
        if source.fill:
            target.fill = copy(source.fill)
        if source.border:
            target.border = copy(source.border)
        if source.alignment:
            target.alignment = copy(source.alignment)
        if source.protection:
            target.protection = copy(source.protection)


def write_main_rows(sheet, main_rows):
    for row_index, item in enumerate(main_rows, start=2):
        apply_template_row_format(sheet, row_index, 2, 1, 6)
        sheet.cell(row_index, 1).value = item["product_id"]
        sheet.cell(row_index, 2).value = item["title"]
        sheet.cell(row_index, 3).value = item["english_title"]
        sheet.cell(row_index, 4).value = item["main_path"]
        sheet.cell(row_index, 5).value = item["detail_path"]
        sheet.cell(row_index, 6).value = item["sku_path"]


def write_rows(sheet, matched_rows):
    for row_index, item in enumerate(matched_rows, start=2):
        apply_template_row_format(sheet, row_index, 2, 1, 7)
        record = item["record"]
        price = item["price"]
        sheet.cell(row_index, 1).value = item["product_id"]
        sheet.cell(row_index, 2).value = item["display_size_text"]
        sheet.cell(row_index, 3).value = record["longest_edge_cm"]
        sheet.cell(row_index, 4).value = record["second_longest_edge_cm"]
        sheet.cell(row_index, 5).value = record["shortest_edge_cm"]
        sheet.cell(row_index, 6).value = record["weight_g"]
        sheet.cell(row_index, 7).value = price
        sheet.cell(row_index, 7).number_format = "0.00"


def collect_sp_directories(assert_dir):
    assert_root = Path(assert_dir)
    if not assert_root.exists():
        raise FileNotFoundError(f"Assert root not found: {assert_root}")
    if not assert_root.is_dir():
        raise NotADirectoryError(f"Assert root is not a directory: {assert_root}")

    sp_dirs = [
        path for path in sorted(assert_root.iterdir())
        if path.is_dir() and re.fullmatch(r"SP\d+", path.name, re.I)
    ]
    if not sp_dirs:
        raise FileNotFoundError(f"No SPxx folders found under: {assert_root}")
    return sp_dirs


def build_main_row(sp_dir, cn_titles, en_titles, title_chinese_only=False):
    title = random.choice(cn_titles)
    english_title = "" if title_chinese_only or not en_titles else random.choice(en_titles)
    return {
        "product_id": sp_dir.name,
        "material": load_material_from_sp_dir(sp_dir),
        "title": title,
        "english_title": english_title,
        "main_path": str((sp_dir / "main").resolve()),
        "detail_path": str((sp_dir / "detail").resolve()),
        "sku_path": str((sp_dir / "sku").resolve()),
    }


def load_material_from_sp_dir(sp_dir):
    metadata_path = Path(sp_dir) / SOURCE_METADATA_NAME
    try:
        payload = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return "lychee_grain"

    value = str(payload.get("material") or "").strip().casefold()
    return "suede" if value in {"suede", "麂皮绒"} else "lychee_grain"


def process_sp_dir(sp_dir, records):
    size_items = extract_sizes_from_sp_dir(sp_dir)
    if not size_items:
        raise ValueError(f"No sizes were extracted from: {sp_dir}")

    matched_rows = []
    skipped_sizes = []
    size_texts = []
    for size_item in size_items:
        size_text = size_item["size_text"]
        size_texts.append(size_text)
        width, length = parse_size(size_text)
        record = match_record(records, width, length)
        if record is None:
            skipped_sizes.append(size_text)
            continue
        matched_rows.append(
            {
                "product_id": sp_dir.name,
                "size_text": size_text,
                "display_size_text": size_item["display_size_text"],
                "record": record,
                "price": random_price(record),
            }
        )

    return {
        "product_id": sp_dir.name,
        "size_texts": size_texts,
        "matched_rows": matched_rows,
        "skipped_sizes": skipped_sizes,
    }


def output_path_for(args):
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    if args.output_name:
        return output_dir / args.output_name
    return output_dir / "products_new.json"


def products_json_path_for(args, output_path):
    if args.products_json:
        target = Path(args.products_json)
    else:
        target = output_path
    target.parent.mkdir(parents=True, exist_ok=True)
    return target


def build_products_json(main_rows, matched_rows):
    by_product = {}
    for row in main_rows:
        by_product[row["product_id"]] = {
            "card_folder_path": str(Path(row["main_path"]).parent.resolve()),
            "material": row.get("material", "lychee_grain"),
            "title": row["title"],
            "english_title": row["english_title"],
            "main_file_folder": row["main_path"],
            "detail_file_folder": row["detail_path"],
            "preview_image_folder": row["sku_path"],
            "sku_size_list": [],
        }

    for item in matched_rows:
        product = by_product.setdefault(
            item["product_id"],
            {
                "card_folder_path": "",
                "material": "lychee_grain",
                "title": "",
                "english_title": "",
                "main_file_folder": "",
                "detail_file_folder": "",
                "preview_image_folder": "",
                "sku_size_list": [],
            },
        )
        record = item["record"]
        product["sku_size_list"].append(
            {
                "size": item["display_size_text"],
                "supply_price": "" if item["price"] is None else f"{item['price']:.2f}",
                "length": str(record["longest_edge_cm"]),
                "width": str(record["second_longest_edge_cm"]),
                "height": str(record["shortest_edge_cm"]),
                "weight": str(record["weight_g"]),
            }
        )

    return list(by_product.values())


def main():
    args = parse_args()
    if args.seed is not None:
        random.seed(args.seed)

    index_path = Path(args.index)
    source_path = Path(args.source)
    title_json_path = Path(args.title_json)
    ensure_index(index_path, source_path)
    records = load_records(index_path)
    cn_titles, en_titles = load_titles(title_json_path)

    main_rows = []
    matched_rows = []
    summaries = []

    if args.sizes:
        sp_path = Path(args.sp_dir) if args.sp_dir else None
        product_id = sp_path.name if sp_path is not None else args.product_id
        if sp_path is not None:
            main_rows.append(build_main_row(sp_path, cn_titles, en_titles, args.title_chinese_only))

        size_texts = list(args.sizes)
        current_matched_rows = []
        skipped_sizes = []
        for size_text in size_texts:
            width, length = parse_size(size_text)
            record = match_record(records, width, length)
            if record is None:
                skipped_sizes.append(size_text)
                continue
            current_matched_rows.append(
                {
                    "product_id": product_id,
                    "size_text": size_text,
                    "display_size_text": size_text,
                    "record": record,
                    "price": random_price(record),
                }
            )

        matched_rows.extend(current_matched_rows)
        summaries.append(
            {
                "product_id": product_id,
                "size_texts": size_texts,
                "matched_rows": current_matched_rows,
                "skipped_sizes": skipped_sizes,
            }
        )
    elif args.sp_dir:
        sp_path = Path(args.sp_dir)
        main_rows.append(build_main_row(sp_path, cn_titles, en_titles, args.title_chinese_only))
        summary = process_sp_dir(sp_path, records)
        matched_rows.extend(summary["matched_rows"])
        summaries.append(summary)
    else:
        for sp_path in collect_sp_directories(args.assert_dir):
            main_rows.append(build_main_row(sp_path, cn_titles, en_titles, args.title_chinese_only))
            summaries.append(process_sp_dir(sp_path, records))
        for summary in summaries:
            matched_rows.extend(summary["matched_rows"])

    output_path = output_path_for(args)
    products_json_path = products_json_path_for(args, output_path)
    products_json = build_products_json(main_rows, matched_rows)
    products_json_path.write_text(json.dumps(products_json, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"products_json={products_json_path}")
    print(f"products={len(main_rows) if main_rows else (1 if summaries else 0)}")
    print(f"matched={len(matched_rows)}")
    skipped_total = 0
    for summary in summaries:
        skipped_total += len(summary["skipped_sizes"])
        print("product={0};sizes={1};matched={2};skipped={3}".format(
            summary["product_id"],
            ",".join(summary["size_texts"]),
            len(summary["matched_rows"]),
            len(summary["skipped_sizes"]),
        ))
        if summary["skipped_sizes"]:
            print("skipped_sizes[{0}]={1}".format(summary["product_id"], ",".join(summary["skipped_sizes"])))
    print(f"skipped={skipped_total}")


if __name__ == "__main__":
    main()
