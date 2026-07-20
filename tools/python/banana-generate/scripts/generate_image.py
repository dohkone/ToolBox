#!/usr/bin/env python3
"""
Generate images with the Change2Pro Banana/Gemini image endpoint.
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import re
import subprocess
import sys
import tempfile
from math import gcd
from pathlib import Path
from typing import Any


DEFAULT_MODEL = "gemini-3.1-flash-image"
DEFAULT_BASE_URL = "https://api.change2pro.com"
DEFAULT_OUTPUT_DIR = Path.home() / "Downloads" / "banana-generations"
HTTP_TIMEOUT_SECONDS = 900
SKILL_DIR = Path(__file__).resolve().parents[1]
KEY_FILE = SKILL_DIR / ".banana_api_key"
VALID_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}


class BananaImageError(Exception):
    """Raised for expected Banana image generation failures."""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate images with the Banana image endpoint.")
    parser.add_argument("--prompt", required=True, help="Prompt used to generate the image.")
    parser.add_argument("--output-dir", default=str(DEFAULT_OUTPUT_DIR), help="Directory to save generated images.")
    parser.add_argument("--filename", help="Optional output filename.")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"Model to use. Defaults to {DEFAULT_MODEL}.")
    parser.add_argument("--size", default="1024x1024", help="Requested image size or ratio, for example 1024x1024 or 16:9.")
    parser.add_argument("--quality", default="medium", help="Quality hint. high maps to 4K, medium to 2K, otherwise 1K.")
    parser.add_argument("--n", type=int, default=1, help="Number of images to request. Defaults to 1.")
    parser.add_argument("--base-url", help=f"Optional API base URL. Defaults to BANANA_BASE_URL or {DEFAULT_BASE_URL}.")
    parser.add_argument("--input-image", action="append", help="Optional reference image path. Repeat to provide multiple images.")
    parser.add_argument("--mask", help="Accepted for compatibility; Banana generateContent does not use masks.")
    return parser.parse_args()


def get_user_key_file() -> Path:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data and local_app_data.strip():
        return Path(local_app_data) / "ToolBox" / "banana_api_key"
    return Path.home() / ".toolbox" / "banana_api_key"


def read_private_key() -> str:
    user_key_file = get_user_key_file()
    key_file = user_key_file if user_key_file.exists() else KEY_FILE
    if not key_file.exists():
        raise BananaImageError(f"Banana key file not found: {key_file}")
    key = key_file.read_text(encoding="utf-8-sig").strip().lstrip("\ufeff")
    if not key:
        raise BananaImageError(f"Banana key file is empty: {key_file}")
    return key


def resolve_base_url(explicit_base_url: str | None) -> str:
    if explicit_base_url and explicit_base_url.strip():
        return explicit_base_url.strip().rstrip("/")
    env_base_url = os.environ.get("BANANA_BASE_URL")
    if env_base_url and env_base_url.strip():
        return env_base_url.strip().rstrip("/")
    return DEFAULT_BASE_URL


def build_generate_endpoint(base_url: str, model: str, *, stream: bool = False) -> str:
    normalized_model = model.strip()
    if normalized_model.startswith("models/"):
        normalized_model = normalized_model.removeprefix("models/")
    action = "streamGenerateContent?alt=sse" if stream else "generateContent"
    return f"{base_url}/v1beta/models/{normalized_model}:{action}"


def get_mime_type(path: Path) -> str:
    suffix = path.suffix.lower()
    if suffix in {".jpg", ".jpeg"}:
        return "image/jpeg"
    if suffix == ".webp":
        return "image/webp"
    return "image/png"


def resolve_input_paths(paths: list[str] | None) -> list[Path]:
    if not paths:
        return []
    resolved: list[Path] = []
    for raw_path in paths:
        path = Path(raw_path).expanduser().resolve()
        if not path.exists():
            raise BananaImageError(f"input image file not found: {path}")
        if not path.is_file():
            raise BananaImageError(f"input image path is not a file: {path}")
        if path.suffix.lower() not in VALID_EXTENSIONS:
            raise BananaImageError(f"unsupported input image type: {path}")
        resolved.append(path)
    return resolved


def image_to_inline_data(path: Path) -> dict[str, Any]:
    return {
        "inlineData": {
            "mimeType": get_mime_type(path),
            "data": base64.b64encode(path.read_bytes()).decode("utf-8"),
        }
    }


def parse_aspect_ratio(size_text: str) -> str | None:
    normalized = size_text.strip().lower().replace("*", "x")
    ratio_match = re.fullmatch(r"(\d+)\s*:\s*(\d+)", normalized)
    if ratio_match:
        return f"{int(ratio_match.group(1))}:{int(ratio_match.group(2))}"

    size_match = re.fullmatch(r"(\d+)\s*x\s*(\d+)", normalized)
    if not size_match:
        return None

    width = int(size_match.group(1))
    height = int(size_match.group(2))
    if width <= 0 or height <= 0:
        return None
    divisor = gcd(width, height)
    return f"{width // divisor}:{height // divisor}"


def resolve_image_size(size_text: str, quality: str) -> str:
    _ = size_text, quality
    return "4K"


def build_payload(prompt: str, input_images: list[Path], size: str, quality: str) -> dict[str, Any]:
    parts: list[dict[str, Any]] = [{"text": prompt}]
    parts.extend(image_to_inline_data(path) for path in input_images)

    image_config: dict[str, str] = {
        "imageSize": resolve_image_size(size, quality),
    }
    aspect_ratio = parse_aspect_ratio(size)
    if aspect_ratio:
        image_config["aspectRatio"] = aspect_ratio

    return {
        "contents": [
            {
                "role": "user",
                "parts": parts,
            }
        ],
        "generationConfig": {
            "imageConfig": image_config,
        },
    }


def run_curl_json(url: str, payload: dict[str, Any], api_key: str) -> tuple[int, str]:
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, suffix=".json") as handle:
        json.dump(payload, handle, ensure_ascii=False)
        body_path = handle.name

    try:
        command = [
            "curl.exe",
            "--noproxy",
            "*",
            "--silent",
            "--show-error",
            "--location",
            "--request",
            "POST",
            "--header",
            "Content-Type: application/json",
            "--header",
            f"x-goog-api-key: {api_key}",
            "--data",
            f"@{body_path}",
            "--write-out",
            "\n%{http_code}",
            url,
        ]
        completed = subprocess.run(
            command,
            capture_output=True,
            text=True,
            check=False,
            encoding="utf-8",
            timeout=HTTP_TIMEOUT_SECONDS + 30,
        )
    except subprocess.TimeoutExpired as exc:
        raise BananaImageError(f"Banana API request timed out after {HTTP_TIMEOUT_SECONDS} seconds.") from exc
    finally:
        try:
            os.unlink(body_path)
        except OSError:
            pass

    stdout = completed.stdout or ""
    stderr = completed.stderr.strip()
    if completed.returncode != 0:
        raise BananaImageError(stderr or stdout.strip() or f"curl failed with exit code {completed.returncode}")

    if "\n" not in stdout:
        raise BananaImageError(f"Unexpected Banana API response format: {stdout[:500]!r}")

    response_body, status_line = stdout.rsplit("\n", 1)
    try:
        status_code = int(status_line.strip())
    except ValueError as exc:
        raise BananaImageError(f"Unexpected Banana HTTP status output: {status_line!r}") from exc

    return status_code, response_body


def run_http_json(url: str, payload: dict[str, Any], api_key: str) -> dict[str, Any]:
    status_code, response_body = run_curl_json(url, payload, api_key)
    if status_code >= 400:
        raise BananaImageError(f"Banana API failed: HTTP {status_code}: {extract_error_message_from_text(response_body)}")
    try:
        parsed = json.loads(response_body)
    except json.JSONDecodeError as exc:
        raise BananaImageError(f"Failed to parse Banana JSON response: {exc}: {response_body[:500]}") from exc
    if not isinstance(parsed, dict):
        raise BananaImageError("Unexpected Banana JSON response shape.")
    return parsed


def run_http_stream(url: str, payload: dict[str, Any], api_key: str) -> list[dict[str, Any]]:
    status_code, response_body = run_curl_json(url, payload, api_key)
    if status_code >= 400:
        raise BananaImageError(f"Banana API failed: HTTP {status_code}: {extract_error_message_from_text(response_body)}")

    chunks: list[dict[str, Any]] = []
    for raw_line in response_body.splitlines():
        line = raw_line.strip()
        if not line.startswith("data:"):
            continue
        data = line.removeprefix("data:").strip()
        if not data or data == "[DONE]":
            continue
        try:
            parsed = json.loads(data)
        except json.JSONDecodeError:
            continue
        if isinstance(parsed, dict):
            chunks.append(parsed)

    if chunks:
        return chunks

    # Some gateways return a normal JSON body even on the stream URL.
    try:
        parsed = json.loads(response_body)
    except json.JSONDecodeError as exc:
        raise BananaImageError(f"Failed to parse Banana stream response: {exc}: {response_body[:500]}") from exc
    if isinstance(parsed, dict):
        return [parsed]
    raise BananaImageError("Unexpected Banana stream response shape.")


def extract_error_message_from_text(text: str) -> str:
    try:
        payload = json.loads(text)
    except json.JSONDecodeError:
        return text.strip()[:500] or "unknown error"
    if isinstance(payload, dict):
        error = payload.get("error")
        if isinstance(error, dict):
            message = error.get("message")
            if isinstance(message, str) and message.strip():
                return message
        if isinstance(error, str) and error.strip():
            return error
        message = payload.get("message")
        if isinstance(message, str) and message.strip():
            return message
    return json.dumps(payload, ensure_ascii=False)[:500]


def collect_images(payload: dict[str, Any]) -> list[dict[str, str]]:
    images: list[dict[str, str]] = []
    candidates = payload.get("candidates")
    if not isinstance(candidates, list):
        return images

    for candidate in candidates:
        if not isinstance(candidate, dict):
            continue
        content = candidate.get("content")
        if not isinstance(content, dict):
            continue
        parts = content.get("parts")
        if not isinstance(parts, list):
            continue
        for part in parts:
            if not isinstance(part, dict):
                continue
            inline_data = part.get("inlineData") or part.get("inline_data")
            if not isinstance(inline_data, dict):
                continue
            data = inline_data.get("data")
            if not isinstance(data, str) or not data:
                continue
            mime_type = inline_data.get("mimeType") or inline_data.get("mime_type") or "image/png"
            images.append({"mime_type": str(mime_type), "base64": data})
    return images


def detect_extension_from_mime(mime_type: str) -> str:
    lowered = mime_type.casefold()
    if "jpeg" in lowered or "jpg" in lowered:
        return ".jpg"
    if "webp" in lowered:
        return ".webp"
    return ".png"


def slugify_filename(prompt: str, max_length: int = 60) -> str:
    cleaned = []
    for char in prompt.strip():
        if char.isalnum():
            cleaned.append(char.lower())
        elif char in {" ", "-", "_"}:
            cleaned.append("-")
    slug = "".join(cleaned).strip("-")
    while "--" in slug:
        slug = slug.replace("--", "-")
    return (slug[:max_length].rstrip("-") or "banana-output")


def normalize_filename(filename: str | None, fallback_stem: str, extension: str) -> str:
    if filename:
        candidate = Path(filename).name
        if Path(candidate).suffix:
            return candidate
        return f"{candidate}{extension}"
    return f"{fallback_stem}{extension}"


def save_images(images: list[dict[str, str]], output_dir: Path, filename: str | None, prompt: str) -> list[Path]:
    if not images:
        raise BananaImageError("Banana response did not contain any inline image data.")

    output_dir.mkdir(parents=True, exist_ok=True)
    saved_paths: list[Path] = []
    fallback_stem = slugify_filename(prompt)
    for index, item in enumerate(images, start=1):
        raw = base64.b64decode(item["base64"])
        extension = detect_extension_from_mime(item.get("mime_type", "image/png"))
        current_filename = filename
        if len(images) > 1:
            if filename:
                stem = Path(filename).stem
                suffix = Path(filename).suffix or extension
                current_filename = f"{stem}-{index}{suffix}"
            else:
                current_filename = f"{fallback_stem}-{index}{extension}"
        final_name = normalize_filename(current_filename, fallback_stem, extension)
        output_path = output_dir / final_name
        output_path.write_bytes(raw)
        saved_paths.append(output_path.resolve())
    return saved_paths


def main() -> int:
    args = parse_args()
    if args.n < 1:
        print("--n must be >= 1", file=sys.stderr)
        return 1

    try:
        api_key = read_private_key()
        input_images = resolve_input_paths(args.input_image)
        base_url = resolve_base_url(args.base_url)
        endpoint = build_generate_endpoint(base_url, args.model, stream=True)
        output_dir = Path(args.output_dir).expanduser().resolve()
        saved_paths: list[Path] = []
        for index in range(1, args.n + 1):
            current_filename = args.filename
            if args.n > 1 and args.filename:
                stem = Path(args.filename).stem
                suffix = Path(args.filename).suffix
                current_filename = f"{stem}-{index}{suffix}" if suffix else f"{args.filename}-{index}"
            payload = build_payload(args.prompt, input_images, args.size, args.quality)
            response_payloads = run_http_stream(endpoint, payload, api_key)
            images: list[dict[str, str]] = []
            for response_payload in response_payloads:
                images.extend(collect_images(response_payload))
            saved_paths.extend(save_images(images, output_dir, current_filename, args.prompt))
    except BananaImageError as exc:
        print(str(exc), file=sys.stderr)
        return 1

    for path in saved_paths:
        print(str(path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
