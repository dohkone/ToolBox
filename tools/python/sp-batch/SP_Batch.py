#!/usr/bin/env python3
"""
Batch-generate six fixed-color SKU variants from every image in a folder.
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
DEFAULT_RETRIES = 4
VALID_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
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
    concurrency: int
    retries: int
    overwrite: bool
    dry_run: bool
    prepare_only: bool
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
    ColorSpec("offwhite", "Off-white", "米白色", "#F4F4F2"),
    ColorSpec("darkbrown", "Dark brown", "深棕色", "#634234"),
    ColorSpec("lightgray", "Light gray", "深灰色", "#C4C8CA"),
    ColorSpec("winered", "Wine red", "酒红色", "#722829"),
    ColorSpec("royalblue", "Royal blue", "宝蓝色", "#0B1B6F"),
)

COLOR_ALIAS_MAP: dict[str, str] = {
    "black": "black",
    "offwhite": "offwhite",
    "darkbrown": "darkbrown",
    "lightgray": "lightgray",
    "winered": "winered",
    "royalblue": "royalblue",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Batch-generate fixed-color SKU variants from local reference images.",
    )
    parser.add_argument("--request", required=True, help="Full natural-language user request.")
    parser.add_argument("--input-dir", help="Override input image directory.")
    parser.add_argument("--output-dir", help="Override output image directory.")
    parser.add_argument("--image2-script", required=True, help="Path to the image2 generation script.")
    parser.add_argument("--concurrency", type=int, help="Override worker count.")
    parser.add_argument("--retries", type=int, help="Override retry count per job.")
    parser.add_argument("--overwrite", action="store_true", help="Regenerate outputs even if they already exist.")
    parser.add_argument("--dry-run", action="store_true", help="Print the planned jobs without generating images.")
    parser.add_argument("--prepare-only", action="store_true", help="Create dated SP folders and copy source images only.")
    return parser.parse_args()


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
    concurrency = max(1, int(args.concurrency or parsed.get("concurrency") or DEFAULT_CONCURRENCY))
    retries = max(1, int(args.retries or parsed.get("retries") or DEFAULT_RETRIES))
    overwrite = bool(args.overwrite or parsed.get("overwrite", False))
    dry_run = bool(args.dry_run or parsed.get("dry_run", False))
    prepare_only = bool(args.prepare_only or parsed.get("prepare_only", False))
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
        concurrency=concurrency,
        retries=retries,
        overwrite=overwrite,
        dry_run=dry_run,
        prepare_only=prepare_only,
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


def ensure_output_bundles(images: list[Path], output_dir: Path, overwrite: bool) -> dict[Path, OutputBundle]:
    dated_root = get_dated_root(output_dir)
    dated_root.mkdir(parents=True, exist_ok=True)

    bundles: dict[Path, OutputBundle] = {}
    for idx, image_path in enumerate(images, start=1):
        sp_name = f"SP{idx:02d}"
        sp_dir = dated_root / sp_name
        main_dir = sp_dir / "main"
        sku_dir = sp_dir / "sku"
        detail_dir = sp_dir / "detail"

        for folder in (main_dir, sku_dir, detail_dir):
            folder.mkdir(parents=True, exist_ok=True)

        source_copy_path = main_dir / MAIN_IMAGE_NAME
        if overwrite or not source_copy_path.exists() or source_copy_path.stat().st_size == 0:
            shutil.copy2(image_path, source_copy_path)

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


def build_prompt(color: ColorSpec) -> str:
    return (
        "Use the uploaded image as reference.\n"
        "Create one SKU color variant for ecommerce use.\n"
        f"Target color: {color.label} {color.hex_code}.\n"
        "Create six-color-SKU style consistency, but only output the current target-color version.\n"
        "Keep the overall scene style close to the reference image. Preserve the premium lifestyle "
        "atmosphere, realistic lighting, commercial product photography quality, product scale, "
        "material texture, and clean composition.\n"
        "Prioritize keeping the original image temperament consistent. Small angle optimization, "
        "slight composition refinement, and slight object position adjustment are allowed, but do "
        "not change the scene too much.\n"
        "Remove all text, icons, labels, callout lines, badges, zoom windows, and decorative overlay elements.\n"
        "Remove all extra rolls, color samples, duplicate samples, and duplicate variants.\n"
        "Keep only one repair patch roll in the final image.\n"
        "Only change leather-repair-related color areas:\n"
        "- the repair patch roll color\n"
        "- the leather furniture surface\n"
        "- the upholstered surface\n"
        "- the repair demonstration surface\n"
        "Core rule: the remaining roll color must exactly match the corresponding leather furniture "
        "surface or upholstered repair surface in the same color family, with no visible color "
        "difference.\n"
        "Do not change non-leather material colors such as wood, marble, metal, glass, flooring, "
        "wall, curtains, or decorations.\n"
        "Do not add unrelated objects. Do not remove the main product. Do not change furniture "
        "shape. Do not change material type. No clutter. No people. No watermark. No car logos. "
        "No extra text or icons.\n"
        "The final result should look like a professional cross-border ecommerce SKU image, "
        "photorealistic, clean, unified, and high-end.\n"
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
        "524",
        "502",
        "503",
        "504",
    )
    return any(marker in lowered for marker in markers)


def run_job(job: Job, image2_script: Path, retries: int, overwrite: bool) -> dict[str, Any]:
    if not overwrite and job.output_path.exists() and job.output_path.stat().st_size > 0:
        return {
            "index": job.index,
            "source_image": str(job.image_path),
            "source_copy_path": str(job.bundle.source_copy_path),
            "sp_dir": str(job.bundle.sp_dir),
            "color": job.color.suffix,
            "status": "skipped",
            "image_path": str(job.output_path.resolve()),
        }

    job.bundle.sku_dir.mkdir(parents=True, exist_ok=True)
    command = [
        sys.executable,
        str(image2_script),
        "--input-image",
        str(job.image_path),
        "--prompt",
        build_prompt(job.color),
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
                "status": "generated",
                "attempts": attempt,
                "image_path": final_path,
            }

        last_error = (stderr_text or stdout_text).strip()
        if attempt < retries and is_retryable_error(last_error):
            time.sleep(10 * attempt)
            continue
        break

    return {
        "index": job.index,
        "source_image": str(job.image_path),
        "source_copy_path": str(job.bundle.source_copy_path),
        "sp_dir": str(job.bundle.sp_dir),
        "color": job.color.suffix,
        "status": "failed",
        "attempts": retries,
        "error": last_error or "Unknown error",
    }


def execute_jobs(jobs: list[Job], options: RequestOptions) -> list[dict[str, Any]]:
    results_by_index: dict[int, dict[str, Any]] = {}

    with concurrent.futures.ThreadPoolExecutor(max_workers=options.concurrency) as executor:
        future_to_job = {
            executor.submit(
                run_job,
                job,
                options.image2_script,
                options.retries,
                options.overwrite,
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
    args = parse_args()
    try:
        options = resolve_options(args)
        images = list_input_images(options.input_dir)
        bundles = ensure_output_bundles(images, options.output_dir, options.overwrite)
        jobs = build_jobs(images, bundles, options.selected_colors)

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

        results = execute_jobs(jobs, options)
        failed = [result for result in results if result.get("status") == "failed"]
        print(
            json.dumps(
                {
                    "mode": "generated",
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
