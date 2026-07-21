#!/usr/bin/env python3
from __future__ import annotations

import argparse
import concurrent.futures
import json
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

VALID_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp", ".bmp"}
MANIFEST_NAME = ".sku-optimize-manifest.json"
IMAGE2_RETRIES = 5


@dataclass(frozen=True)
class RequestOptions:
    input_dir: Path
    output_dir: Path
    image2_script: Path
    concurrency: int
    length_multiplier: float
    diameter_multiplier: float
    overwrite: bool


@dataclass(frozen=True)
class ColorSpec:
    suffix: str
    label: str
    hex_code: str
    aliases: tuple[str, ...]


@dataclass(frozen=True)
class Job:
    index: int
    source_image: Path
    output_path: Path
    target_color: ColorSpec | None


COLORS: tuple[ColorSpec, ...] = (
    ColorSpec("black", "Black", "#0A0A0A", ("black", "black-cn", "黑色")),
    ColorSpec("offwhite", "Off-white", "#F4F4F2", ("offwhite", "off-white", "offwhite-cn", "米白色")),
    ColorSpec("darkbrown", "Dark brown", "#261107", ("darkbrown", "dark-brown", "darkbrown-cn", "深棕色")),
    ColorSpec("darkgray", "Dark gray", "#C4C8CA", ("darkgray", "dark-gray", "dark-grey", "darkgrey", "darkgray-cn", "深灰色")),
    ColorSpec("winered", "Wine red", "#722829", ("winered", "wine-red", "winered-cn", "酒红色")),
    ColorSpec("royalblue", "Royal blue", "#2E3EA5", ("royalblue", "royal-blue", "royalblue-cn", "宝蓝色")),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Optimize SKU roll geometry using a single master image.")
    parser.add_argument("--input-dir", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--image2-script", required=True)
    parser.add_argument("--concurrency", type=int, default=1)
    parser.add_argument("--length-multiplier", type=float, required=True)
    parser.add_argument("--diameter-multiplier", type=float, required=True)
    parser.add_argument("--overwrite", action="store_true")
    return parser.parse_args()


def resolve_options(args: argparse.Namespace) -> RequestOptions:
    return RequestOptions(
        input_dir=Path(args.input_dir).expanduser().resolve(),
        output_dir=Path(args.output_dir).expanduser().resolve(),
        image2_script=Path(args.image2_script).expanduser().resolve(),
        concurrency=max(1, int(args.concurrency)),
        length_multiplier=float(args.length_multiplier),
        diameter_multiplier=float(args.diameter_multiplier),
        overwrite=bool(args.overwrite),
    )


def list_input_images(input_dir: Path) -> list[Path]:
    if not input_dir.exists() or not input_dir.is_dir():
        raise RuntimeError(f"Input directory not found: {input_dir}")

    images = [
        path
        for path in input_dir.iterdir()
        if path.is_file() and path.suffix.lower() in VALID_EXTENSIONS
    ]
    if not images:
        raise RuntimeError(f"No supported images found in: {input_dir}")
    return sorted(images, key=lambda item: item.name.casefold())


def load_manifest(input_dir: Path) -> list[dict[str, str]] | None:
    manifest_path = input_dir / MANIFEST_NAME
    if not manifest_path.exists():
        return None

    try:
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        raise RuntimeError(f"Failed to read SKU optimize manifest: {exc}") from exc

    entries = payload.get("images")
    if not isinstance(entries, list) or not entries:
        raise RuntimeError("SKU optimize manifest does not contain any images.")

    normalized_entries: list[dict[str, str]] = []
    for entry in entries:
        if not isinstance(entry, dict):
            continue
        staging_name = str(entry.get("staging_name") or "").strip()
        output_name = str(entry.get("output_name") or "").strip()
        if not staging_name or not output_name:
            continue
        normalized_entries.append(
            {
                "staging_name": staging_name,
                "output_name": output_name,
            }
        )

    if not normalized_entries:
        raise RuntimeError("SKU optimize manifest does not contain valid image entries.")

    return normalized_entries


def build_result_root(output_dir: Path) -> Path:
    return output_dir / f"sku-optimize-{datetime.now():%Y%m%d_%H%M%S}"


def normalize_token(text: str) -> str:
    return "".join(ch for ch in text.casefold() if ch.isalnum())


def infer_color_from_path(path: Path) -> ColorSpec | None:
    normalized_stem = normalize_token(path.stem)
    if not normalized_stem:
        return None

    for color in COLORS:
        if any(normalized_stem == normalize_token(alias) for alias in color.aliases):
            return color

    for color in COLORS:
        if any(normalize_token(alias) in normalized_stem for alias in color.aliases):
            return color

    return None


def build_master_prompt(length_multiplier: float, diameter_multiplier: float) -> str:
    if length_multiplier > 0:
        length_instruction = (
            f"Increase the visible roll length until the roll appears approximately {length_multiplier:.4g} times longer than the original while keeping exactly the same diameter."
        )
        length_behavior_instruction = """
The roll must become obviously longer.

Do not make only a slight adjustment.

Extend the roll only along its own longitudinal axis.

Do not extend sideways.

Increase the amount of rolled leather.

The leather must appear naturally rolled, as if the same product were manufactured in a longer specification.

For large changes such as 2x, 3x, or 4x length, fully apply the requested geometry.

Do not preserve the previous short appearance.

The final roll should immediately look much longer than before.

Transform the roll into a longer, slim cylindrical leather repair roll.
""".strip()
    elif length_multiplier < 0:
        target_length_ratio = abs(length_multiplier)
        length_instruction = (
            f"Decrease the visible roll length until the roll appears approximately {target_length_ratio:.4g} times the original length while keeping exactly the same diameter."
        )
        length_behavior_instruction = """
The roll must become obviously shorter.

Do not make only a slight adjustment.

Shorten the roll only along its own longitudinal axis.

Do not compress sideways.

Reduce the amount of rolled leather while keeping the roll naturally manufactured.

The leather must appear naturally rolled, as if the same product were manufactured in a shorter specification.

Fully apply the requested target length ratio.

Do not preserve the previous long appearance.

The final roll should immediately look much shorter than before.

Keep the roll cylindrical and realistic after shortening.
""".strip()
    else:
        length_instruction = "Keep the visible roll length unchanged."
        length_behavior_instruction = """
Do not change the visible roll length.

Do not extend or shorten the roll.

Keep the original amount of rolled leather unchanged.
""".strip()
    diameter_instruction = (
        "Keep the current roll diameter unchanged."
        if diameter_multiplier == 0 or diameter_multiplier == 1
        else f"Adjust the roll diameter until it appears approximately {diameter_multiplier:.4g} times the original diameter."
    )
    diameter_behavior_instruction = (
        "Keep the diameter unchanged."
        if diameter_multiplier == 0 or diameter_multiplier == 1
        else "Apply the requested diameter change, but do not change any other part of the image."
    )
    paper_core_instruction = (
        "Do not increase the roll diameter."
        if diameter_multiplier == 0 or diameter_multiplier == 1
        else "Keep the paper core exactly the same size even when changing the outer roll diameter."
    )
    return f"""
Use the uploaded image as the only reference.

Perform a strict local edit.

This image is the MASTER SKU image and must remain the fixed reference for all future SKU color variants.

========================
MASTER IMAGE LOCK
========================

Keep exactly unchanged:
- subject
- scene
- camera angle
- perspective
- crop
- composition
- background
- furniture
- props
- object positions
- lighting
- shadows
- reflections
- colors
- image quality
- focus
- leather texture
- leather grain
- material appearance

Do NOT:
- redesign the image
- regenerate the scene
- add objects
- remove objects
- move objects
- rotate objects
- resize unrelated objects
- recolor any object

========================
ONLY MODIFY THE ROLL
========================

Only modify the geometry of every visible leather repair roll.

{length_instruction}

{diameter_instruction}

The requested geometry change is mandatory.

{length_behavior_instruction}

Keep the original roll position, angle, orientation, and perspective.

Do not stretch the leather.

Do not stretch the texture.

Do not stretch the leather grain.

Keep the original leather material consistent. STRICT ROLL SPECIFICATION: every visible roll must remain a
high-quality PU leather repair roll in a fully rolled state. Never unfold it, bend it, fold it, distort it, or
deform it. The overall silhouette must remain a standard cylindrical roll.

The roll must clearly preserve a dual-layer structure: the front side is the premium PU leather layer, and the back
side is a kraft paper release liner tightly attached to the leather layer. The two layers must have exactly the same
visible length and must be perfectly flush along every edge. The kraft paper release liner must never extend beyond
the PU leather edge. Do not create any paper rim, paper lip, exposed liner edge, raised liner, lifted liner, curled
liner, protruding liner, or any protruding paper structure.

The outer leather surface should show clear, shallow, fine, even lychee-grain leather texture with low contrast.
The leather should appear smooth and refined first, with the fine grain becoming visible only at close viewing
distance.

The leather must present a rich oily leather finish, strong natural specular highlights, broad bright specular
reflections across the curved surface as if lit by bright natural sunlight or a large softbox, premium commercial
product photography gloss, visible light flow across the surface, and bright high surface brightness without
overexposure. The highlights may be pronounced but must remain natural, clean, even, transparent, and continuous
without washing out the leather texture. The roll must look bright, dimensional, premium, tactile, and glossy like
high-quality PU leather, not ordinary plastic reflection.

SURFACE TEXTURE AND GLOSS ARE TOP PRIORITY: the roll surface must clearly show ultra-fine, shallow, uniform lychee
leather grain and a smooth oily PU leather sheen. The final image must immediately communicate premium PU leather
through visible fine grain, clean broad highlights, rich luster, and natural reflective light flow on the curved roll
surface.

If these qualities are weak, they may be subtly enhanced on the roll surface only, but do not change color, lighting,
subject, background, position, angle, crop, or composition. Avoid dark, gray, matte, powdery, dry, rough, low-gloss,
frosted, rubber-like, plastic-like, or non-reflective surfaces. Avoid deep embossing, coarse pebble grain,
oversized pores, chalky finish, or patent-leather mirror reflections. Do not add leather grain to the paper core,
release liner, props, or any non-leather object.

Keep the roll tightly wound.

Keep the winding density unchanged.

Never loosen or unfold the roll.

Keep the paper core exactly the same size.

Do not increase the paper core length.

{paper_core_instruction}

Only change the rolled leather length unless a diameter change is explicitly requested.

If multiple rolls are visible, apply the same geometry change consistently to every roll.

If a repair patch sheet is visible, keep it unchanged unless it must naturally connect to the modified roll.

{diameter_behavior_instruction}

========================
FINAL REQUIREMENT
========================

Everything except the roll geometry must remain unchanged.

The final image must look like the exact same MASTER SKU image, with the only visible difference being that the leather repair roll geometry follows the requested length and diameter change while keeping a realistic appearance.

Photorealistic.
Commercial product photography.
Ultra realistic.
High detail.
No editing artifacts.
""".strip()


def build_recolor_prompt(target_color: ColorSpec) -> str:
    return f"""
Use the uploaded image as the fixed MASTER SKU image.
Create a strict color-only variant.
Target color: {target_color.label} {target_color.hex_code}.

STRICT LOCK:
Do not change roll length, roll diameter, roll size, roll angle, roll position, subject size, subject position,
camera angle, crop, perspective, composition, lighting, shadows, background, props, furniture, scene depth,
paper core, texture sharpness, or any object placement.
Do not add or remove objects. Do not redraw the image. Do not alter the layout.

Only recolor leather-repair-related areas to the target color:
- the leather repair roll surface
- the repair patch material
- the repair demonstration leather surface
- the main leather or upholstered surface if it is part of the SKU color

Keep the ultra-fine natural litchi grain, shallow micro embossing, soft glossy finish, rich leather luster,
elegant natural sheen, clean specular highlights, and smooth reflective surface on the outer PU leather repair patch
or roll surfaces. The leather should appear smooth first, with the fine grain visible only at close viewing distance.
Do not add lychee grain to the paper core, release liner, props, background, or any non-leather object.

Do not recolor non-leather materials such as wood, marble, metal, glass, walls, flooring, curtains, plants,
decorations, paper core, or background objects.

Final result must look like the exact same photo as the master image, with only the leather-related color changed.
""".strip()


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


def run_image2(
    *,
    script_path: Path,
    input_image: Path,
    prompt: str,
    output_dir: Path,
    filename: str,
) -> tuple[int, str, str]:
    command = [
        sys.executable,
        str(script_path),
        "--input-image",
        str(input_image),
        "--prompt",
        prompt,
        "--output-dir",
        str(output_dir),
        "--filename",
        filename,
    ]
    completed = subprocess.run(command, capture_output=True, text=False, check=False)
    return completed.returncode, decode_output(completed.stdout), decode_output(completed.stderr)


def resolve_final_output_path(stdout_text: str, fallback_path: Path) -> str:
    lines = [line.strip() for line in stdout_text.splitlines() if line.strip()]
    return lines[-1] if lines else str(fallback_path.resolve())


def build_jobs(input_dir: Path, result_root: Path) -> list[Job]:
    manifest_entries = load_manifest(input_dir)
    jobs: list[Job] = []

    if manifest_entries is not None:
        for index, entry in enumerate(manifest_entries, start=1):
            source_image = (input_dir / entry["staging_name"]).resolve()
            if not source_image.exists() or source_image.suffix.lower() not in VALID_EXTENSIONS:
                raise RuntimeError(f"Manifest image not found: {source_image}")

            output_name = entry["output_name"]
            if Path(output_name).suffix:
                final_name = output_name
            else:
                final_name = f"{output_name}.png"

            jobs.append(
                Job(
                    index=index,
                    source_image=source_image,
                    output_path=result_root / final_name,
                    target_color=None,
                )
            )

        return jobs

    images = list_input_images(input_dir)
    return [
        Job(
            index=index,
            source_image=image,
            output_path=result_root / f"{image.stem}.png",
            target_color=None,
        )
        for index, image in enumerate(images, start=1)
    ]


def run_master(master_job: Job, options: RequestOptions) -> dict[str, Any]:
    if not options.overwrite and master_job.output_path.exists() and master_job.output_path.stat().st_size > 0:
        return {
            "index": master_job.index,
            "source_image": str(master_job.source_image),
            "status": "skipped",
            "image_path": str(master_job.output_path.resolve()),
            "attempts": 0,
            "stage": "master",
        }

    last_error = ""
    final_attempt = 0
    for attempt in range(1, IMAGE2_RETRIES + 1):
        final_attempt = attempt
        returncode, stdout_text, stderr_text = run_image2(
            script_path=options.image2_script,
            input_image=master_job.source_image,
            prompt=build_master_prompt(options.length_multiplier, options.diameter_multiplier),
            output_dir=master_job.output_path.parent,
            filename=master_job.output_path.name,
        )

        if returncode == 0:
            return {
                "index": master_job.index,
                "source_image": str(master_job.source_image),
                "status": "generated",
                "image_path": resolve_final_output_path(stdout_text, master_job.output_path),
                "attempts": attempt,
                "stage": "master",
            }

        last_error = (stderr_text or stdout_text).strip()
        if attempt < IMAGE2_RETRIES and is_retryable_error(last_error):
            time.sleep(get_retry_delay_seconds(attempt))
            continue
        break

    return {
        "index": master_job.index,
        "source_image": str(master_job.source_image),
        "status": "failed",
        "image_path": "",
        "error": last_error,
        "attempts": final_attempt,
        "stage": "master",
    }


def run_recolor(job: Job, options: RequestOptions, master_image_path: Path) -> dict[str, Any]:
    if job.target_color is None:
        return {
            "index": job.index,
            "source_image": str(job.source_image),
            "status": "failed",
            "image_path": "",
            "error": "Cannot infer target color from file name. Please use color-named SKU images.",
            "attempts": 0,
            "stage": "recolor",
        }

    if not options.overwrite and job.output_path.exists() and job.output_path.stat().st_size > 0:
        return {
            "index": job.index,
            "source_image": str(job.source_image),
            "status": "skipped",
            "image_path": str(job.output_path.resolve()),
            "attempts": 0,
            "stage": "recolor",
        }

    returncode, stdout_text, stderr_text = run_image2(
        script_path=options.image2_script,
        input_image=master_image_path,
        prompt=build_recolor_prompt(job.target_color),
        output_dir=job.output_path.parent,
        filename=job.output_path.name,
    )

    if returncode == 0:
        return {
            "index": job.index,
            "source_image": str(job.source_image),
            "status": "generated",
            "image_path": resolve_final_output_path(stdout_text, job.output_path),
            "attempts": 1,
            "stage": "recolor",
            "reference_image": str(master_image_path),
            "target_color": job.target_color.suffix,
        }

    return {
        "index": job.index,
        "source_image": str(job.source_image),
        "status": "failed",
        "image_path": "",
        "error": (stderr_text or stdout_text).strip(),
        "attempts": 1,
        "stage": "recolor",
        "reference_image": str(master_image_path),
        "target_color": job.target_color.suffix,
    }


def main() -> None:
    try:
        options = resolve_options(parse_args())
        result_root = build_result_root(options.output_dir)
        result_root.mkdir(parents=True, exist_ok=True)
        jobs = build_jobs(options.input_dir, result_root)

        results: list[dict[str, Any]] = []
        with concurrent.futures.ThreadPoolExecutor(max_workers=options.concurrency) as executor:
            futures = [executor.submit(run_master, job, options) for job in jobs]
            for future in concurrent.futures.as_completed(futures):
                results.append(future.result())

        results.sort(key=lambda item: int(item["index"]))
        payload = {
            "input_dir": str(options.input_dir),
            "output_dir": str(options.output_dir),
            "result_root": str(result_root),
            "concurrency": options.concurrency,
            "length_multiplier": options.length_multiplier,
            "diameter_multiplier": options.diameter_multiplier,
            "results": results,
        }
        print(json.dumps(payload, ensure_ascii=False))
    except Exception as ex:
        payload = {
            "input_dir": "",
            "output_dir": "",
            "result_root": "",
            "concurrency": 0,
            "length_multiplier": 0,
            "diameter_multiplier": 0,
            "results": [],
            "error": str(ex),
        }
        print(json.dumps(payload, ensure_ascii=False))
        sys.exit(1)


if __name__ == "__main__":
    main()
