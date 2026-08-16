#!/usr/bin/env python3
"""
Batch-generate fixed-color SKU variants from every image in a folder.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import random
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Any

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

DEFAULT_INPUT_DIR = Path(r"D:\temu_auto\review")
DEFAULT_OUTPUT_DIR = Path(r"D:\temu_auto\assert")
DEFAULT_CONCURRENCY = 2
DEFAULT_RETRIES = 6
VALID_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
SOURCE_METADATA_NAME = ".sku-source.json"
LINKED_BUNDLES_MANIFEST_NAME = ".sp-batch-linked-bundles.json"
MAIN_IMAGE_NAME = "1-封面.png"


class SkuColorBatchError(Exception):
    """Raised for expected batch-generation failures."""


@dataclass(frozen=True)
class ColorSpec:
    suffix: str
    label: str
    file_name: str
    hex_code: str


@dataclass(frozen=True)
class RequestOptions:
    input_dir: Path
    output_dir: Path
    image2_script: Path
    material: str
    concurrency: int
    retries: int
    overwrite: bool
    dry_run: bool
    prepare_only: bool
    master_only: bool
    recolor_only: bool
    selected_colors: tuple[str, ...]
    color_count: int | None


@dataclass(frozen=True)
class OutputBundle:
    sp_dir: Path
    main_dir: Path
    sku_dir: Path
    detail_dir: Path
    source_copy_path: Path


@dataclass(frozen=True)
class Job:
    index: int
    image_path: Path
    color: ColorSpec
    output_path: Path
    bundle: OutputBundle


COLORS: tuple[ColorSpec, ...] = (
    ColorSpec("black", "Black", "黑色", "#0A0A0A"),
    ColorSpec("offwhite", "Off-white", "米白色", "#F2EFE5"),
    ColorSpec("darkbrown", "Dark brown", "深棕色", "#261107"),
    ColorSpec("darkgray", "Dark gray", "深灰色", "#7C7C7A"),
    ColorSpec("winered", "Wine red", "酒红色", "#722829"),
    ColorSpec("royalblue", "Royal blue", "宝蓝色", "#2E3EA5"),
)

COLOR_ALIAS_MAP: dict[str, str] = {
    "black": "black",
    "黑色": "black",
    "offwhite": "offwhite",
    "off-white": "offwhite",
    "off white": "offwhite",
    "米白色": "offwhite",
    "darkbrown": "darkbrown",
    "dark-brown": "darkbrown",
    "dark brown": "darkbrown",
    "深棕色": "darkbrown",
    "darkgray": "darkgray",
    "dark-gray": "darkgray",
    "dark gray": "darkgray",
    "darkgrey": "darkgray",
    "dark-grey": "darkgray",
    "dark grey": "darkgray",
    "深灰色": "darkgray",
    "winered": "winered",
    "wine-red": "winered",
    "wine red": "winered",
    "酒红色": "winered",
    "royalblue": "royalblue",
    "royal-blue": "royalblue",
    "royal blue": "royalblue",
    "宝蓝色": "royalblue",
}


def normalize_color_token(text: str) -> str:
    return re.sub(r"[\s_\-#]+", "", text.casefold())


def infer_color_from_path(image_path: Path) -> str | None:
    text = normalize_color_token(image_path.stem)
    for color in COLORS:
        candidates = {
            color.suffix,
            color.label,
            color.file_name,
            color.hex_code,
            color.hex_code.lstrip("#"),
        }
        for candidate in candidates:
            token = normalize_color_token(candidate)
            if token and token in text:
                return color.suffix

    for alias, suffix in COLOR_ALIAS_MAP.items():
        if normalize_color_token(alias) in text:
            return suffix

    return None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Batch-generate fixed-color SKU variants from local reference images.",
    )
    parser.add_argument("--request", required=True, help="Full natural-language user request.")
    parser.add_argument("--input-dir", help="Override input image directory.")
    parser.add_argument("--output-dir", help="Override output image directory.")
    parser.add_argument("--image2-script", required=True, help="Path to the image2 generation script.")
    parser.add_argument("--material", default="lychee_grain", help="Stable material token stored in the SP metadata.")
    parser.add_argument("--color-template", help="Optional JSON color template exported by the desktop app.")
    parser.add_argument("--concurrency", type=int, help="Override worker count.")
    parser.add_argument("--retries", type=int, help="Override retry count per job.")
    parser.add_argument("--overwrite", action="store_true", help="Regenerate outputs even if they already exist.")
    parser.add_argument("--dry-run", action="store_true", help="Print the planned jobs without generating images.")
    parser.add_argument("--prepare-only", action="store_true", help="Create dated SP folders and copy source images only.")
    parser.add_argument("--master-only", action="store_true", help="Generate only the master SKU image.")
    parser.add_argument("--recolor-only", action="store_true", help="Generate color variants from the provided master SKU image.")
    return parser.parse_args()


def load_color_specs(path_text: str | None) -> tuple[ColorSpec, ...]:
    if not path_text:
        return COLORS
    path = Path(path_text).expanduser()
    if not path.exists():
        return COLORS
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return COLORS
    if not isinstance(payload, list):
        return COLORS
    colors: list[ColorSpec] = []
    for index, item in enumerate(payload, start=1):
        if not isinstance(item, dict):
            continue
        name = str(item.get("name") or item.get("Name") or "").strip()
        hex_code = str(item.get("hex") or item.get("HexCode") or "").strip().upper()
        if not name or not re.fullmatch(r"#[0-9A-F]{6}", hex_code):
            continue
        suffix = normalize_color_token(name) or f"color{index}"
        colors.append(ColorSpec(suffix, name, name, hex_code))
    return tuple(colors) or COLORS


def build_color_alias_map(colors: tuple[ColorSpec, ...]) -> dict[str, str]:
    aliases: dict[str, str] = {}
    for color in colors:
        aliases[color.label.casefold()] = color.suffix
        aliases[color.file_name.casefold()] = color.suffix
        aliases[color.hex_code.casefold()] = color.suffix
        aliases[color.hex_code.lstrip("#").casefold()] = color.suffix
    return aliases


def parse_request(text: str) -> dict[str, Any]:
    parsed: dict[str, Any] = {}
    lowered_text = text.lower()

    # Match simple Windows-style paths without swallowing trailing request text.
    path_pattern = r"([A-Za-z]:\\(?:[^\\/:*?\"<>|\r\n\s]+\\)*[^\\/:*?\"<>|\r\n\s]+)"

    input_match = re.search(rf"(?:基于|根据|从)\s*{path_pattern}", text, re.IGNORECASE)
    if input_match:
        parsed["input_dir"] = input_match.group(1).strip()

    output_match = re.search(rf"(?:输出到|保存到|放到)\s*{path_pattern}", text, re.IGNORECASE)
    if output_match:
        parsed["output_dir"] = output_match.group(1).strip()

    concurrency_match = re.search(r"(?:并发|线程)\s*(\d+)", text, re.IGNORECASE)
    if concurrency_match:
        parsed["concurrency"] = int(concurrency_match.group(1))
    elif "多线程" in text:
        parsed["concurrency"] = DEFAULT_CONCURRENCY

    retries_match = re.search(r"重试\s*(\d+)\s*次", text, re.IGNORECASE)
    if retries_match:
        parsed["retries"] = int(retries_match.group(1))
    elif "多尝试几次" in text:
        parsed["retries"] = DEFAULT_RETRIES

    if "覆盖已有" in text or "重新生成全部" in text:
        parsed["overwrite"] = True

    if "只出计划" in text or "只做计划" in text:
        parsed["dry_run"] = True

    if "先不生图片" in text or "只测main" in lowered_text or "只测 main" in lowered_text:
        parsed["prepare_only"] = True

    selected_colors = [suffix for alias, suffix in COLOR_ALIAS_MAP.items() if alias in lowered_text]
    if selected_colors:
        parsed["selected_colors"] = list(dict.fromkeys(selected_colors))

    color_count_match = re.search(r"分别生成\s*(\d+)\s*张", text)
    if color_count_match:
        parsed["color_count"] = int(color_count_match.group(1))

    return parsed


def resolve_options(args: argparse.Namespace) -> RequestOptions:
    parsed = parse_request(args.request)

    input_dir = Path(args.input_dir or parsed.get("input_dir") or DEFAULT_INPUT_DIR).expanduser().resolve()
    output_dir = Path(args.output_dir or parsed.get("output_dir") or DEFAULT_OUTPUT_DIR).expanduser().resolve()
    image2_script = Path(args.image2_script).expanduser().resolve()
    material = normalize_material_token(args.material)
    concurrency = max(1, int(args.concurrency or parsed.get("concurrency") or DEFAULT_CONCURRENCY))
    retries = max(1, int(args.retries or parsed.get("retries") or DEFAULT_RETRIES))
    overwrite = bool(args.overwrite or parsed.get("overwrite", False))
    dry_run = bool(args.dry_run or parsed.get("dry_run", False))
    prepare_only = bool(args.prepare_only or parsed.get("prepare_only", False))
    master_only = bool(args.master_only)
    recolor_only = bool(args.recolor_only)
    color_count = parsed.get("color_count")
    explicit_selected_colors = parsed.get("selected_colors")
    if explicit_selected_colors:
        selected_colors = tuple(explicit_selected_colors)
    elif color_count:
        sample_count = max(1, min(int(color_count), len(COLORS)))
        selected_colors = tuple(color.suffix for color in random.sample(list(COLORS), sample_count))
    else:
        selected_colors = tuple(color.suffix for color in COLORS)

    return RequestOptions(
        input_dir=input_dir,
        output_dir=output_dir,
        image2_script=image2_script,
        material=material,
        concurrency=concurrency,
        retries=retries,
        overwrite=overwrite,
        dry_run=dry_run,
        prepare_only=prepare_only,
        master_only=master_only,
        recolor_only=recolor_only,
        selected_colors=selected_colors,
        color_count=color_count,
    )


def list_input_images(input_dir: Path) -> list[Path]:
    if not input_dir.exists():
        raise SkuColorBatchError(f"Input directory not found: {input_dir}")
    if not input_dir.is_dir():
        raise SkuColorBatchError(f"Input path is not a directory: {input_dir}")

    images = sorted(
        path for path in input_dir.iterdir()
        if path.is_file() and path.suffix.lower() in VALID_EXTENSIONS
    )
    if not images:
        raise SkuColorBatchError(f"No supported images found in: {input_dir}")
    return images


def get_dated_root(output_dir: Path) -> Path:
    return output_dir / str(date.today())


def get_next_sp_index(dated_root: Path) -> int:
    max_index = 0
    if dated_root.exists():
        for child in dated_root.iterdir():
            if not child.is_dir():
                continue
            match = re.fullmatch(r"SP(\d+)", child.name, re.IGNORECASE)
            if match:
                max_index = max(max_index, int(match.group(1)))
    return max_index + 1


def normalize_source_file_name(file_name: str) -> str:
    return Path(file_name).name.casefold()


def load_source_file_name(sp_dir: Path) -> str | None:
    metadata_path = sp_dir / SOURCE_METADATA_NAME
    if not metadata_path.exists():
        return None

    try:
        data = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None

    source_file_name = data.get("source_file_name")
    return source_file_name if isinstance(source_file_name, str) and source_file_name.strip() else None


def build_existing_source_map(dated_root: Path) -> dict[str, Path]:
    source_map: dict[str, Path] = {}
    if not dated_root.exists():
        return source_map

    sp_dirs = [
        child
        for child in dated_root.iterdir()
        if child.is_dir() and re.fullmatch(r"SP\d+", child.name, re.IGNORECASE)
    ]
    sp_dirs.sort(key=lambda path: int(re.fullmatch(r"SP(\d+)", path.name, re.IGNORECASE).group(1)))

    for sp_dir in sp_dirs:
        source_file_name = load_source_file_name(sp_dir)
        if not source_file_name:
            continue
        source_map.setdefault(normalize_source_file_name(source_file_name), sp_dir)

    return source_map


def clear_directory_contents(directory: Path) -> None:
    if not directory.exists():
        return

    for child in directory.iterdir():
        if child.is_dir():
            shutil.rmtree(child)
        else:
            child.unlink()


def normalize_material_token(value: str | None) -> str:
    text = (value or "").strip().casefold()
    if text in {"suede", "麂皮绒"}:
        return "suede"
    return "lychee_grain"


def write_source_metadata(sp_dir: Path, image_path: Path, material: str) -> None:
    metadata = {
        "source_file_name": image_path.name,
        "source_original_path": str(image_path.resolve()),
        "updated_at": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
        "material": normalize_material_token(material),
    }
    (sp_dir / SOURCE_METADATA_NAME).write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def update_source_material(sp_dir: Path, image_path: Path, material: str) -> None:
    metadata_path = sp_dir / SOURCE_METADATA_NAME
    metadata: dict[str, Any] = {}
    if metadata_path.exists():
        try:
            loaded = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
            if isinstance(loaded, dict):
                metadata.update(loaded)
        except (OSError, json.JSONDecodeError):
            pass

    metadata.setdefault("source_file_name", image_path.name)
    metadata.setdefault("source_original_path", str(image_path.resolve()))
    metadata["updated_at"] = time.strftime("%Y-%m-%dT%H:%M:%S%z")
    metadata["material"] = normalize_material_token(material)
    metadata_path.write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def load_linked_bundle_map(input_dir: Path) -> dict[str, dict[str, Any]]:
    manifest_path = input_dir / LINKED_BUNDLES_MANIFEST_NAME
    if not manifest_path.exists():
        return {}

    try:
        data = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return {}

    items = data.get("items")
    if not isinstance(items, list):
        return {}

    result: dict[str, dict[str, Any]] = {}
    for item in items:
        if not isinstance(item, dict):
            continue
        file_name = item.get("FileName") or item.get("file_name")
        card_path = item.get("CardPath") or item.get("card_path") or item.get("sp_dir")
        sku_dir = item.get("SkuDirectory") or item.get("sku_directory") or item.get("sku_dir")
        source_copy_path = item.get("SourceCopyPath") or item.get("source_copy_path")
        if not isinstance(file_name, str) or not isinstance(card_path, str) or not isinstance(sku_dir, str):
            continue
        result[normalize_source_file_name(file_name)] = {
            "card_path": card_path,
            "sku_dir": sku_dir,
            "source_copy_path": source_copy_path if isinstance(source_copy_path, str) else "",
        }
    return result


def ensure_output_bundles(
    images: list[Path],
    output_dir: Path,
    overwrite: bool,
    material: str,
    copy_source_to_sku: bool = False,
    linked_bundles: dict[str, dict[str, Any]] | None = None,
) -> dict[Path, OutputBundle]:
    dated_root = get_dated_root(output_dir)

    bundles: dict[Path, OutputBundle] = {}
    linked_bundles = linked_bundles or {}
    existing_source_map = build_existing_source_map(dated_root)
    next_index = get_next_sp_index(dated_root)
    used_sp_dirs: set[Path] = set()

    for image_path in images:
        source_key = normalize_source_file_name(image_path.name)
        linked_bundle = linked_bundles.get(source_key)
        if linked_bundle:
            sp_dir = Path(str(linked_bundle["card_path"])).expanduser().resolve()
            main_dir = sp_dir / "main"
            sku_dir = Path(str(linked_bundle["sku_dir"])).expanduser().resolve()
            detail_dir = sp_dir / "detail"
            for folder in (main_dir, sku_dir, detail_dir):
                folder.mkdir(parents=True, exist_ok=True)
            update_source_material(sp_dir, image_path, material)
            source_copy_text = str(linked_bundle.get("source_copy_path") or "")
            source_copy_path = Path(source_copy_text).expanduser().resolve() if source_copy_text else image_path
            bundles[image_path] = OutputBundle(
                sp_dir=sp_dir,
                main_dir=main_dir,
                sku_dir=sku_dir,
                detail_dir=detail_dir,
                source_copy_path=source_copy_path,
            )
            continue

        sp_dir = existing_source_map.get(source_key)
        reuse_existing = sp_dir is not None and sp_dir not in used_sp_dirs
        if not reuse_existing:
            sp_dir = dated_root / f"SP{next_index:02d}"
            next_index += 1

        used_sp_dirs.add(sp_dir)
        main_dir = sp_dir / "main"
        sku_dir = sp_dir / "sku"
        detail_dir = sp_dir / "detail"

        if overwrite and reuse_existing:
            for folder in (main_dir, sku_dir, detail_dir):
                clear_directory_contents(folder)

        for folder in (main_dir, sku_dir, detail_dir):
            folder.mkdir(parents=True, exist_ok=True)

        source_copy_path = sku_dir / image_path.name if copy_source_to_sku else main_dir / MAIN_IMAGE_NAME
        if overwrite or not source_copy_path.exists() or source_copy_path.stat().st_size == 0:
            shutil.copy2(image_path, source_copy_path)
        write_source_metadata(sp_dir, image_path, material)

        bundles[image_path] = OutputBundle(
            sp_dir=sp_dir,
            main_dir=main_dir,
            sku_dir=sku_dir,
            detail_dir=detail_dir,
            source_copy_path=source_copy_path,
        )

    return bundles


def build_jobs(
    images: list[Path],
    bundles: dict[Path, OutputBundle],
    selected_colors: tuple[str, ...],
) -> list[Job]:
    jobs: list[Job] = []
    index = 1
    color_map = {color.suffix: color for color in COLORS}
    chosen_colors = [color_map[suffix] for suffix in selected_colors if suffix in color_map]
    if not chosen_colors:
        raise SkuColorBatchError("No valid target colors were selected.")

    for image_path in images:
        bundle = bundles[image_path]
        for color in chosen_colors:
            output_name = f"{color.file_name}.png"
            jobs.append(
                Job(
                    index=index,
                    image_path=image_path,
                    color=color,
                    output_path=bundle.sku_dir / output_name,
                    bundle=bundle,
                )
            )
            index += 1
    return jobs


def build_master_jobs(
    images: list[Path],
    bundles: dict[Path, OutputBundle],
    selected_colors: tuple[str, ...],
) -> list[Job]:
    color_map = {color.suffix: color for color in COLORS}
    master_color = (
        color_map.get("offwhite")
        or next((color for color in COLORS if color.file_name == "米白色" or color.hex_code.upper() == "#F2EFE5"), None)
        or ColorSpec("offwhite", "Off-white", "米白色", "#F2EFE5")
    )
    jobs: list[Job] = []

    for index, image_path in enumerate(images, start=1):
        bundle = bundles[image_path]
        jobs.append(
            Job(
                index=index,
                image_path=image_path,
                color=master_color,
                output_path=bundle.sku_dir / f"{master_color.file_name}.png",
                bundle=bundle,
            )
        )

    return jobs


def build_recolor_jobs(
    images: list[Path],
    bundles: dict[Path, OutputBundle],
    selected_colors: tuple[str, ...],
) -> list[Job]:
    color_map = {color.suffix: color for color in COLORS}
    selected = [color_map[suffix] for suffix in selected_colors if suffix in color_map]
    if not selected:
        selected = list(COLORS)

    jobs: list[Job] = []
    index = 1
    for image_path in images:
        source_color_suffix = infer_color_from_path(image_path)
        colors = [color for color in selected if color.suffix != source_color_suffix]
        if not colors:
            colors = selected

        bundle = bundles[image_path]
        for color in colors:
            jobs.append(
                Job(
                    index=index,
                    image_path=image_path,
                    color=color,
                    output_path=bundle.sku_dir / f"{color.file_name}.png",
                    bundle=bundle,
                )
            )
            index += 1

    return jobs


def build_master_prompt(color: ColorSpec, material: str = "lychee_grain") -> str:
    if normalize_material_token(material) == "suede":
        return build_suede_master_prompt(color)

    return f"""
    Use the uploaded image as lifestyle scene reference.

    Create a premium Amazon/TEMU ecommerce product photo.

    Keep the original lifestyle environment and main subject structure.
    The main furniture/object must remain clearly visible.

    The final image must show:
    ONE main subject,
    ONE PU leather repair roll.

    Remove all extra rolls, duplicate products, samples, labels, and unnecessary objects.

    PRODUCT PLACEMENT:

    The PU leather repair roll must physically touch the main subject.

    Place the roll resting on, leaning against, or attached to the furniture surface.

    Show realistic contact:
    contact shadow,
    occlusion,
    natural weight and physical interaction.

    Do not let the roll float.
    Do not place the roll separately.
    Do not create an isolated product display.


    PRODUCT FORM:

    Show only ONE fully rolled PU leather repair roll.

    Keep a realistic cylindrical roll shape.

    Create a slim elongated leather sheet roll.

    Reduce roll diameter by approximately 50% compared with the reference.

    Keep the roll length extended.

    The roll should look thin, long, lightweight, and elegant.

    Avoid:
    short thick tape roll,
    bulky cylinder,
    compact spool.
    COLOR MATCH:

    Target leather color:
    {color.hex_code}

    The PU leather repair roll and the leather/upholstered area of the main subject MUST use the exact same target color.
    Match:
    hue,
    brightness,
    saturation,
    and color tone.
    Keep furniture shape unchanged.

    PRODUCT PRIORITY:
    The PU leather repair roll is the primary product element.
    Keep the roll clear, sharp, and visually important.
    Furniture and background are supporting elements only.
    Do not enhance furniture texture.
    The main subject only needs realistic shape and natural appearance.

    DUAL LAYER:
    Keep the roll as a real repair material roll.

    Front:
    PU leather layer.

    Back:
    kraft paper release liner.
    The liner must stay attached and aligned.
    No loose paper.
    No exposed edge.

    CAMERA:
    Close-up premium ecommerce photography.
    Keep both:
    the main subject,
    and the repair roll visible.
    The roll should be sharper than the environment.
    Realistic commercial lighting.

    REMOVE:
    text,
    logos,
    icons,
    watermarks,
    labels,
    extra products,
    duplicate objects.


    FINAL STYLE:
    High-end Amazon/TEMU commercial product photography.
    Realistic premium lifestyle presentation.
    Clean modern composition.
    The final image should look like the same real scene,
    with one matching-color PU leather repair roll naturally applied to the main subject.
"""


def build_suede_master_prompt(color: ColorSpec) -> str:
    return f"""
    Use the uploaded image as lifestyle scene reference.

    Create a premium Amazon/TEMU ecommerce product photo.

    Keep the original lifestyle environment and main subject structure.
    The main furniture/object must remain clearly visible.

    HIGHEST PRIORITY COLOR LOCK:
    The target color must control BOTH:
    the suede repair roll,
    and the visible suede/fabric/upholstered surface of the main subject.

    Do not preserve the original upholstery color from the reference image.
    If the reference car interior, furniture, seat, door panel, armrest, or upholstered area is brown, black, gray,
    or any other color, recolor the main suede/fabric/upholstered contact area to the exact target color.

    For target color #F2EFE5, the roll, seat, door panel, armrest, and visible suede/fabric/upholstered surfaces
    must all become warm off-white #F2EFE5.

    Only keep non-upholstery parts unchanged:
    steering wheel,
    dashboard plastic,
    metal trim,
    glass,
    buttons,
    wood,
    floor,
    background,
    and natural shadows.

    The final image must show:
    ONE main subject,
    ONE self-adhesive suede repair wrap roll.

    Remove all extra rolls, duplicate products, samples, labels, and unnecessary objects.

    PRODUCT PLACEMENT:

    Place the suede repair roll naturally within the scene.
    It may rest on a seat, armrest, center console, shelf, floor mat, or other suitable nearby surface,
    or lean naturally beside the main subject.
    The roll does not need to touch or be attached to the main subject.
    Prioritize a believable product-photography composition, realistic gravity, natural contact shadows,
    and clear product visibility.
    Do not let the roll float, stand unnaturally on an overhead surface, or intersect with the main subject.


    PRODUCT FORM:

    Show only ONE fully rolled self-adhesive suede repair wrap roll.

    Keep a realistic cylindrical roll shape.

    Create a slim elongated suede sheet roll.

    Reduce roll diameter by approximately 50% compared with the reference.

    Keep the roll length extended.

    The roll should look thin, long, lightweight, and elegant.

    Avoid:
    short thick tape roll,
    bulky cylinder,
    compact spool.

    COLOR MATCH:

    Target suede color:
    {color.hex_code}

    The suede repair roll and the suede/fabric/upholstered area of the main subject MUST use the exact same target color.
    Match:
    hue,
    brightness,
    saturation,
    and color tone.
    Keep furniture/object shape unchanged.

    MATERIAL:
    The product material must be soft suede / microsuede / alcantara-like fabric.
    Surface texture must be fine, short, dense, velvety nap.
    Keep a soft-touch matte finish with gentle directional nap variation.
    The surface should look smooth, plush, and slightly fuzzy at close distance.
    No litchi grain.
    No pebble grain.
    No oily PU leather gloss.
    No broad mirror specular highlights.
    No plastic, rubber, PVC, or glossy leather finish.

    PRODUCT PRIORITY:
    The suede repair roll is the primary product element.
    Keep the roll clear, sharp, and visually important.
    Furniture and background are supporting elements only.
    Do not enhance furniture texture.
    The main subject only needs realistic shape and natural appearance.

    DUAL LAYER:
    Keep the roll as a real self-adhesive repair material roll.

    Front:
    suede fabric layer.

    Back:
    kraft paper release liner.
    The liner must stay attached and aligned.
    The kraft paper release liner must be tightly bonded to the suede layer.
    It must not protrude beyond the outer roll edges.
    Do not create a paper lip, lifted paper edge, curled edge, or separate exposed paper layer.
    No loose paper.
    No exposed edge.

    CAMERA:
    Close-up premium ecommerce photography.
    Keep both:
    the main subject,
    and the suede repair roll visible.
    The roll should be sharper than the environment.
    Realistic commercial lighting.

    REMOVE:
    text,
    logos,
    icons,
    watermarks,
    labels,
    extra products,
    duplicate objects.


    FINAL STYLE:
    High-end Amazon/TEMU commercial product photography.
    Realistic premium lifestyle presentation.
    Clean modern composition.
    The final image should look like the same real scene,
    with one matching-color suede repair roll naturally applied to the main subject.
"""


def build_recolor_prompt(color: ColorSpec, material: str = "lychee_grain") -> str:
    if normalize_material_token(material) == "suede":
        return build_suede_recolor_prompt(color)

    return (
        "Use the uploaded image as the fixed master SKU composition.\n"
        "Create a color variant of the exact same SKU image.\n"
        f"Target color: {color.label} {color.hex_code}.\n"
        "STRICT LOCK: Do not change the composition, camera angle, perspective, crop, object positions, "
        "main subject size, main subject shape, repair patch size, repair patch position, roll size, roll angle, "
        "roll position, background, props, lighting direction, shadows, depth of field, or layout.\n"
        "The output must look like the same photo and the same scene as the uploaded master image, with only "
        "the SKU color changed.\n"
        "Only recolor leather-repair-related areas to the target color:\n"
        "- the main leather or upholstered surface\n"
        "- the leather repair patch/product material\n"
        "- any repair demonstration leather surface\n"
        "Do not recolor non-leather materials such as wood, marble, metal, glass, walls, floor, plants, curtains, "
        "decorations, paper core, or background objects.\n"
        "STRICT ROLL SPECIFICATION: preserve the exact roll specification established in the master image. Every visible "
        "roll must remain a high-quality PU leather repair roll kept in a fully rolled state. Do not unfold, bend, fold, "
        "deform, or redesign the roll. Keep the overall roll as a standard cylindrical form.\n"
        "Preserve the dual-layer structure exactly as shown in the master image: the front side is the premium PU leather "
        "layer, and the back side is the kraft paper release liner. The release liner must remain tightly attached to the "
        "leather and must not be recolored into a leather surface.\n"
        "Keep clear, shallow, fine, even litchi leather grain on the visible PU leather surface. The grain must stay "
        "low-contrast, while the leather overall still reads as smooth and refined first.\n"
        "Keep the rich oily leather finish, strong natural specular highlights, broad bright specular reflections, premium "
        "commercial product photography sheen, visible light flow across the curved surface, and high surface brightness "
        "without overexposure, whitening, or loss of grain detail.\n"
        "Avoid dark, gray, matte, powdery, dry, rough, low-gloss, frosted, rubber-like, plastic-like, or non-reflective "
        "surfaces. Avoid deep embossing, coarse pebble grain, oversized pores, chalky texture, or patent-leather mirror "
        "reflections.\n"
        "Do not add litchi grain or leather-like gloss to the paper core, release liner, background, props, or any "
        "non-leather object.\n"
        "Do not add or remove objects. Do not add text, icons, logos, watermarks, labels, or overlays. "
        "Do not redraw the scene. Do not move the roll or patch. Do not resize anything.\n"
        "Final result must be a strict color-only SKU variant that matches the master image in every detail "
        "except the target leather color.\n"
    )


def build_suede_recolor_prompt(color: ColorSpec) -> str:
    return (
        "Use the uploaded image as the fixed master SKU composition.\n"
        "Create a color variant of the exact same SKU image.\n"
        f"Target color: {color.label} {color.hex_code}.\n"
        "STRICT LOCK: Do not change the composition, camera angle, perspective, crop, object positions, "
        "main subject size, main subject shape, repair wrap size, repair wrap position, roll size, roll angle, "
        "roll position, background, props, lighting direction, shadows, depth of field, or layout.\n"
        "The output must look like the same photo and the same scene as the uploaded master image, with only "
        "the SKU color changed.\n"
        "Only recolor suede-repair-related areas to the target color:\n"
        "- the main suede, fabric, or upholstered surface\n"
        "- the suede repair wrap/product material\n"
        "- any repair demonstration suede surface\n"
        "Do not recolor non-suede materials such as wood, marble, metal, glass, walls, floor, plants, curtains, "
        "decorations, paper core, release liner, or background objects.\n"
        "STRICT ROLL SPECIFICATION: preserve the exact roll specification established in the master image. Every visible "
        "roll must remain a high-quality self-adhesive suede repair wrap roll kept in a fully rolled state. Do not unfold, "
        "bend, fold, deform, or redesign the roll. Keep the overall roll as a standard slim cylindrical form.\n"
        "Preserve the dual-layer structure exactly as shown in the master image: the front side is the suede fabric layer, "
        "and the back side is the kraft paper release liner. The release liner must remain tightly attached to the suede "
        "and must not be recolored into a suede surface. The kraft paper release liner must be tightly bonded to the suede "
        "layer and must not protrude beyond the outer roll edges. Do not create a paper lip, lifted paper edge, curled edge, "
        "or separate exposed paper layer.\n"
        "Keep the same fine, short, dense, velvety suede nap on every visible suede surface. The material must stay soft, "
        "smooth, plush, and matte with gentle directional nap variation.\n"
        "Avoid litchi grain, pebble grain, oily PU leather gloss, broad mirror specular highlights, plastic texture, PVC "
        "texture, rubber texture, rough fabric, long fur, wool, carpet, or shiny leather.\n"
        "Do not add suede nap or fabric texture to the paper core, release liner, background, props, or any non-suede object.\n"
        "Do not add or remove objects. Do not add text, icons, logos, watermarks, labels, or overlays. "
        "Do not redraw the scene. Do not move the roll or patch. Do not resize anything.\n"
        "Final result must be a strict color-only SKU variant that matches the master image in every detail "
        "except the target suede color.\n"
    )


def decode_output(data: bytes | None) -> str:
    if not data:
        return ""
    for encoding in ("utf-8", "gbk", "cp936"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            continue
    return data.decode("utf-8", errors="replace")


def is_retryable_error(message: str) -> bool:
    lowered = message.lower()
    markers = (
        "temporarily unavailable",
        "timeout",
        "timed out",
        "connection reset",
        "connection aborted",
        "recv failure",
        "curl: (56)",
        "524",
        "502",
        "503",
        "504",
    )
    return any(marker in lowered for marker in markers)


def get_retry_delay_seconds(attempt: int) -> int:
    return min(30 * attempt, 120)


def run_job(
    job: Job,
    image2_script: Path,
    retries: int,
    overwrite: bool,
    input_image_path: Path | None = None,
    prompt: str | None = None,
    stage: str = "generated",
    material: str = "lychee_grain",
) -> dict[str, Any]:
    if not overwrite and job.output_path.exists() and job.output_path.stat().st_size > 0:
        return {
            "index": job.index,
            "source_image": str(job.image_path),
            "source_copy_path": str(job.bundle.source_copy_path),
            "sp_dir": str(job.bundle.sp_dir),
            "color": job.color.suffix,
            "stage": stage,
            "status": "skipped",
            "image_path": str(job.output_path.resolve()),
        }

    job.bundle.sku_dir.mkdir(parents=True, exist_ok=True)
    reference_image_path = input_image_path or job.image_path
    command = [
        sys.executable,
        str(image2_script),
        "--input-image",
        str(reference_image_path),
        "--prompt",
        prompt or build_master_prompt(job.color, material),
        "--output-dir",
        str(job.bundle.sku_dir),
        "--filename",
        job.output_path.name,
    ]
    last_error = ""
    for attempt in range(1, retries + 1):
        completed = subprocess.run(
            command,
            capture_output=True,
            text=False,
            check=False,
        )
        stdout_text = decode_output(completed.stdout)
        stderr_text = decode_output(completed.stderr)
        if completed.returncode == 0:
            lines = [line.strip() for line in stdout_text.splitlines() if line.strip()]
            final_path = lines[-1] if lines else str(job.output_path.resolve())
            return {
                "index": job.index,
                "source_image": str(job.image_path),
                "source_copy_path": str(job.bundle.source_copy_path),
                "sp_dir": str(job.bundle.sp_dir),
                "color": job.color.suffix,
                "stage": stage,
                "reference_image": str(reference_image_path),
                "status": "generated",
                "attempts": attempt,
                "image_path": final_path,
            }

        last_error = (stderr_text or stdout_text).strip()
        if attempt < retries and is_retryable_error(last_error):
            time.sleep(get_retry_delay_seconds(attempt))
            continue
        break

    return {
        "index": job.index,
        "source_image": str(job.image_path),
        "source_copy_path": str(job.bundle.source_copy_path),
        "sp_dir": str(job.bundle.sp_dir),
        "color": job.color.suffix,
        "stage": stage,
        "reference_image": str(reference_image_path),
        "status": "failed",
        "attempts": retries,
        "error": last_error or "Unknown error",
    }


def fail_dependent_job(job: Job, master_result: dict[str, Any]) -> dict[str, Any]:
    return {
        "index": job.index,
        "source_image": str(job.image_path),
        "source_copy_path": str(job.bundle.source_copy_path),
        "sp_dir": str(job.bundle.sp_dir),
        "color": job.color.suffix,
        "stage": "recolor",
        "status": "failed",
        "error": "Master SKU image was not generated, so this color variant was not created.",
        "master_error": master_result.get("error", ""),
    }


def execute_job_group(group_jobs: list[Job], options: RequestOptions) -> list[dict[str, Any]]:
    if not group_jobs:
        return []

    ordered_jobs = sorted(group_jobs, key=lambda item: item.index)
    master_job = ordered_jobs[0]
    master_result = run_job(
        master_job,
        options.image2_script,
        options.retries,
        options.overwrite,
        input_image_path=master_job.image_path,
        prompt=build_master_prompt(master_job.color, options.material),
        stage="master",
    )
    results = [master_result]
    if master_result.get("status") == "failed":
        results.extend(fail_dependent_job(job, master_result) for job in ordered_jobs[1:])
        return results

    master_image_path = Path(str(master_result.get("image_path") or master_job.output_path)).resolve()
    for job in ordered_jobs[1:]:
        results.append(
            run_job(
                job,
                options.image2_script,
                options.retries,
                options.overwrite,
                input_image_path=master_image_path,
                prompt=build_recolor_prompt(job.color, options.material),
                stage="recolor",
            )
        )

    return results


def execute_jobs(jobs: list[Job], options: RequestOptions) -> list[dict[str, Any]]:
    results_by_index: dict[int, dict[str, Any]] = {}
    groups_by_sp_dir: dict[Path, list[Job]] = {}
    for job in jobs:
        groups_by_sp_dir.setdefault(job.bundle.sp_dir, []).append(job)

    with concurrent.futures.ThreadPoolExecutor(max_workers=options.concurrency) as executor:
        future_to_job = {
            executor.submit(
                execute_job_group,
                group_jobs,
                options,
            ): group_jobs
            for group_jobs in groups_by_sp_dir.values()
        }
        for future in concurrent.futures.as_completed(future_to_job):
            group_jobs = future_to_job[future]
            try:
                for result in future.result():
                    results_by_index[int(result["index"])] = result
            except Exception as exc:  # noqa: BLE001
                for job in group_jobs:
                    results_by_index[job.index] = {
                        "index": job.index,
                        "source_image": str(job.image_path),
                        "source_copy_path": str(job.bundle.source_copy_path),
                        "sp_dir": str(job.bundle.sp_dir),
                        "color": job.color.suffix,
                        "status": "failed",
                        "error": str(exc),
                    }

    return [results_by_index[index] for index in sorted(results_by_index)]


def execute_master_jobs(jobs: list[Job], options: RequestOptions) -> list[dict[str, Any]]:
    results_by_index: dict[int, dict[str, Any]] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=options.concurrency) as executor:
        future_to_job = {
            executor.submit(
                run_job,
                job,
                options.image2_script,
                options.retries,
                options.overwrite,
                job.image_path,
                build_master_prompt(job.color, options.material),
                "master",
            ): job
            for job in jobs
        }
        for future in concurrent.futures.as_completed(future_to_job):
            job = future_to_job[future]
            try:
                results_by_index[job.index] = future.result()
            except Exception as exc:  # noqa: BLE001
                results_by_index[job.index] = {
                    "index": job.index,
                    "source_image": str(job.image_path),
                    "source_copy_path": str(job.bundle.source_copy_path),
                    "sp_dir": str(job.bundle.sp_dir),
                    "color": job.color.suffix,
                    "stage": "master",
                    "status": "failed",
                    "error": str(exc),
                }

    return [results_by_index[index] for index in sorted(results_by_index)]


def execute_recolor_jobs(jobs: list[Job], options: RequestOptions) -> list[dict[str, Any]]:
    results_by_index: dict[int, dict[str, Any]] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=options.concurrency) as executor:
        future_to_job = {
            executor.submit(
                run_job,
                job,
                options.image2_script,
                options.retries,
                options.overwrite,
                job.image_path,
                build_recolor_prompt(job.color, options.material),
                "recolor",
            ): job
            for job in jobs
        }
        for future in concurrent.futures.as_completed(future_to_job):
            job = future_to_job[future]
            try:
                results_by_index[job.index] = future.result()
            except Exception as exc:  # noqa: BLE001
                results_by_index[job.index] = {
                    "index": job.index,
                    "source_image": str(job.image_path),
                    "source_copy_path": str(job.bundle.source_copy_path),
                    "sp_dir": str(job.bundle.sp_dir),
                    "color": job.color.suffix,
                    "stage": "recolor",
                    "status": "failed",
                    "error": str(exc),
                }

    return [results_by_index[index] for index in sorted(results_by_index)]


def serialize_bundle(image_path: Path, bundle: OutputBundle) -> dict[str, str]:
    return {
        "source_image": str(image_path),
        "sp_dir": str(bundle.sp_dir),
        "main_dir": str(bundle.main_dir),
        "sku_dir": str(bundle.sku_dir),
        "detail_dir": str(bundle.detail_dir),
        "source_copy_path": str(bundle.source_copy_path),
    }


def main() -> int:
    global COLORS, COLOR_ALIAS_MAP
    args = parse_args()
    try:
        COLORS = load_color_specs(args.color_template)
        COLOR_ALIAS_MAP = build_color_alias_map(COLORS)
        options = resolve_options(args)
        images = list_input_images(options.input_dir)
        linked_bundles = load_linked_bundle_map(options.input_dir)
        bundles = ensure_output_bundles(
            images,
            options.output_dir,
            options.overwrite,
            options.material,
            copy_source_to_sku=options.recolor_only,
            linked_bundles=linked_bundles,
        )
        if options.master_only:
            jobs = build_master_jobs(images, bundles, options.selected_colors)
            mode_name = "master_generated"
        elif options.recolor_only:
            jobs = build_recolor_jobs(images, bundles, options.selected_colors)
            mode_name = "recolor_generated"
        else:
            jobs = build_jobs(images, bundles, options.selected_colors)
            mode_name = "generated"

        if options.dry_run:
            print(
                json.dumps(
                    {
                        "mode": "dry_run",
                        "input_dir": str(options.input_dir),
                        "output_dir": str(options.output_dir),
                        "dated_root": str(get_dated_root(options.output_dir).resolve()),
                        "concurrency": options.concurrency,
                        "retries": options.retries,
                        "overwrite": options.overwrite,
                        "prepare_only": options.prepare_only,
                        "color_count": options.color_count,
                        "selected_colors": list(options.selected_colors),
                        "prepared_bundles": [
                            serialize_bundle(image_path, bundle)
                            for image_path, bundle in bundles.items()
                        ],
                        "job_count": len(jobs),
                        "jobs": [
                            {
                                "index": job.index,
                                "source_image": str(job.image_path),
                                "color": job.color.suffix,
                                "sp_dir": str(job.bundle.sp_dir),
                                "output_path": str(job.output_path),
                            }
                            for job in jobs
                        ],
                    },
                    ensure_ascii=False,
                    indent=2,
                )
            )
            return 0

        if options.prepare_only:
            print(
                json.dumps(
                    {
                        "mode": "prepared",
                        "input_dir": str(options.input_dir),
                        "output_dir": str(options.output_dir),
                        "dated_root": str(get_dated_root(options.output_dir).resolve()),
                        "prepared_bundles": [
                            serialize_bundle(image_path, bundle)
                            for image_path, bundle in bundles.items()
                        ],
                    },
                    ensure_ascii=False,
                    indent=2,
                )
            )
            return 0

        if options.master_only:
            results = execute_master_jobs(jobs, options)
        elif options.recolor_only:
            results = execute_recolor_jobs(jobs, options)
        else:
            results = execute_jobs(jobs, options)
        failed = [result for result in results if result.get("status") == "failed"]
        print(
            json.dumps(
                {
                    "mode": mode_name,
                    "input_dir": str(options.input_dir),
                    "output_dir": str(options.output_dir),
                    "dated_root": str(get_dated_root(options.output_dir).resolve()),
                    "concurrency": options.concurrency,
                    "retries": options.retries,
                    "prepare_only": options.prepare_only,
                    "color_count": options.color_count,
                    "selected_colors": list(options.selected_colors),
                    "results": results,
                },
                ensure_ascii=False,
                indent=2,
            )
        )
        return 1 if failed else 0
    except SkuColorBatchError as exc:
        print(str(exc), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
