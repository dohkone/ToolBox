#!/usr/bin/env python3
"""
Generate images using a skill-private API key and the current Codex provider base_url.
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import subprocess
import sys
import tempfile
import urllib.parse
from pathlib import Path
from typing import Any


DEFAULT_MODEL = "gpt-image-2"
DEFAULT_BASE_URL = "https://api.change2pro.com"
DEFAULT_OUTPUT_DIR = Path.home() / "Downloads" / "image2-generations"
SKILL_DIR = Path(__file__).resolve().parents[1]
KEY_FILE = SKILL_DIR / ".image2_api_key"
STATE_FILE = SKILL_DIR / ".default_model"


class Image2Error(Exception):
    """Raised for expected operational failures."""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate images with the image2 dedicated key.",
    )
    parser.add_argument("--prompt", required=True, help="Prompt used to generate the image.")
    parser.add_argument(
        "--output-dir",
        default=str(DEFAULT_OUTPUT_DIR),
        help="Directory to save generated images. Defaults to ~/Downloads/image2-generations.",
    )
    parser.add_argument(
        "--filename",
        help="Optional output filename. If omitted, a name is derived automatically.",
    )
    parser.add_argument(
        "--model",
        help=f"Image model to use. Defaults to the skill default model, initially {DEFAULT_MODEL}.",
    )
    parser.add_argument(
        "--size",
        default="1024x1024",
        help="Requested image size, for example 1024x1024.",
    )
    parser.add_argument(
        "--quality",
        default="high",
        help="Requested image quality. Defaults to high.",
    )
    parser.add_argument(
        "--n",
        type=int,
        default=1,
        help="Number of images to request. Defaults to 1.",
    )
    parser.add_argument(
        "--config",
        help="Optional path to Codex config.toml. Defaults to ${CODEX_HOME}/config.toml.",
    )
    parser.add_argument(
        "--base-url",
        help=f"Optional image API base URL. Defaults to IMAGE2_BASE_URL or {DEFAULT_BASE_URL}.",
    )
    parser.add_argument(
        "--input-image",
        action="append",
        help="Optional reference image path for edit mode. Repeat to provide multiple input images.",
    )
    parser.add_argument(
        "--mask",
        help="Optional mask image path for edit mode.",
    )
    return parser.parse_args()


def get_codex_home() -> Path:
    codex_home = os.environ.get("CODEX_HOME")
    if codex_home:
        return Path(codex_home).expanduser().resolve()
    return Path.home() / ".codex"


def get_config_path(explicit_path: str | None) -> Path:
    if explicit_path:
        return Path(explicit_path).expanduser().resolve()
    return get_codex_home() / "config.toml"


def load_config(config_path: Path) -> dict[str, Any]:
    if not config_path.exists():
        raise Image2Error(f"Codex config not found: {config_path}")
    try:
        text = config_path.read_text(encoding="utf-8")
    except OSError as exc:
        raise Image2Error(f"Failed to read config: {config_path}: {exc}") from exc
    return parse_minimal_toml(text, config_path)


def parse_minimal_toml(text: str, config_path: Path) -> dict[str, Any]:
    current_section: list[str] = []
    parsed: dict[str, Any] = {}

    for line_number, raw_line in enumerate(text.splitlines(), start=1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if "#" in line:
            line = line.split("#", 1)[0].rstrip()
        if not line:
            continue
        if line.startswith("[") and line.endswith("]"):
            section_name = line[1:-1].strip()
            if not section_name:
                raise Image2Error(f"Invalid empty TOML section in {config_path}:{line_number}")
            current_section = split_toml_section(section_name)
            ensure_nested_table(parsed, current_section)
            continue
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip()
        if not key:
            raise Image2Error(f"Invalid TOML key in {config_path}:{line_number}")
        target = ensure_nested_table(parsed, current_section)
        target[key] = parse_toml_scalar(value)

    return parsed


def split_toml_section(section_name: str) -> list[str]:
    parts: list[str] = []
    current: list[str] = []
    in_quotes = False
    quote_char = ""

    for char in section_name:
        if char in {'"', "'"}:
            if in_quotes and char == quote_char:
                in_quotes = False
                quote_char = ""
            elif not in_quotes:
                in_quotes = True
                quote_char = char
            else:
                current.append(char)
            continue
        if char == "." and not in_quotes:
            part = "".join(current).strip().strip('"').strip("'")
            if part:
                parts.append(part)
            current = []
            continue
        current.append(char)

    part = "".join(current).strip().strip('"').strip("'")
    if part:
        parts.append(part)
    return parts


def ensure_nested_table(root: dict[str, Any], path: list[str]) -> dict[str, Any]:
    current = root
    for segment in path:
        existing = current.get(segment)
        if existing is None:
            existing = {}
            current[segment] = existing
        if not isinstance(existing, dict):
            raise Image2Error(f"Invalid TOML structure near section {'.'.join(path)}")
        current = existing
    return current


def parse_toml_scalar(value: str) -> Any:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {'"', "'"}:
        quote = value[0]
        if quote == "'":
            return value[1:-1]
        try:
            return json.loads(value)
        except json.JSONDecodeError:
            return value[1:-1]
    lowered = value.lower()
    if lowered == "true":
        return True
    if lowered == "false":
        return False
    try:
        if "." in value:
            return float(value)
        return int(value)
    except ValueError:
        return value


def is_local_base_url(base_url: str) -> bool:
    try:
        host = urllib.parse.urlparse(base_url).hostname or ""
    except ValueError:
        return False
    return host.lower() in {"127.0.0.1", "localhost", "::1"}


def resolve_base_url(config: dict[str, Any] | None = None, explicit_base_url: str | None = None) -> str:
    if explicit_base_url and explicit_base_url.strip():
        return explicit_base_url.strip().rstrip("/")

    env_base_url = os.environ.get("IMAGE2_BASE_URL")
    if env_base_url and env_base_url.strip():
        return env_base_url.strip().rstrip("/")

    if config is None:
        return DEFAULT_BASE_URL

    provider_name = config.get("model_provider")
    if not provider_name:
        return DEFAULT_BASE_URL

    providers = config.get("model_providers")
    if not isinstance(providers, dict):
        return DEFAULT_BASE_URL

    provider = providers.get(provider_name)
    if not isinstance(provider, dict):
        return DEFAULT_BASE_URL

    base_url = provider.get("base_url")
    if not isinstance(base_url, str) or not base_url.strip():
        return DEFAULT_BASE_URL

    base_url = base_url.rstrip("/")
    if is_local_base_url(base_url):
        return DEFAULT_BASE_URL

    return base_url


def build_images_endpoint(base_url: str) -> str:
    if base_url.endswith("/v1"):
        return f"{base_url}/images/generations"
    return f"{base_url}/v1/images/generations"


def build_models_endpoint(base_url: str) -> str:
    if base_url.endswith("/v1"):
        return f"{base_url}/models"
    return f"{base_url}/v1/models"


def build_edits_endpoint(base_url: str) -> str:
    if base_url.endswith("/v1"):
        return f"{base_url}/images/edits"
    return f"{base_url}/v1/images/edits"


def read_private_key() -> str:
    if not KEY_FILE.exists():
        raise Image2Error(f"Dedicated key file not found: {KEY_FILE}")
    key = KEY_FILE.read_text(encoding="utf-8").strip()
    if not key:
        raise Image2Error(f"Dedicated key file is empty: {KEY_FILE}")
    return key


def read_default_model() -> str:
    if not STATE_FILE.exists():
        return DEFAULT_MODEL
    value = STATE_FILE.read_text(encoding="utf-8").strip()
    return value or DEFAULT_MODEL


def write_default_model(model: str) -> None:
    STATE_FILE.write_text(model.strip() + "\n", encoding="utf-8")


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
    if not slug:
        slug = "image2-output"
    return slug[:max_length].rstrip("-") or "image2-output"


def detect_extension_from_bytes(data: bytes) -> str:
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return ".png"
    if data.startswith(b"\xff\xd8\xff"):
        return ".jpg"
    if data.startswith(b"RIFF") and data[8:12] == b"WEBP":
        return ".webp"
    return ".png"


def detect_extension_from_url(url: str) -> str:
    parsed = urllib.parse.urlparse(url)
    suffix = Path(parsed.path).suffix.lower()
    if suffix in {".png", ".jpg", ".jpeg", ".webp"}:
        return ".jpg" if suffix == ".jpeg" else suffix
    return ".png"


def normalize_filename(filename: str | None, fallback_stem: str, extension: str) -> str:
    if filename:
        candidate = Path(filename).name
        suffix = Path(candidate).suffix
        if suffix:
            return candidate
        return f"{candidate}{extension}"
    return f"{fallback_stem}{extension}"


def resolve_input_paths(paths: list[str] | None, label: str) -> list[Path]:
    if not paths:
        return []
    resolved: list[Path] = []
    for raw_path in paths:
        path = Path(raw_path).expanduser().resolve()
        if not path.exists():
            raise Image2Error(f"{label} file not found: {path}")
        if not path.is_file():
            raise Image2Error(f"{label} path is not a file: {path}")
        resolved.append(path)
    return resolved


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
            f"Authorization: Bearer {api_key}",
            "--header",
            "Content-Type: application/json",
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
        )
    finally:
        try:
            os.unlink(body_path)
        except OSError:
            pass

    stdout = completed.stdout or ""
    stderr = completed.stderr.strip()
    if completed.returncode != 0:
        message = stderr or stdout.strip() or f"curl failed with exit code {completed.returncode}"
        raise Image2Error(message)

    if "\n" not in stdout:
        raise Image2Error(f"Unexpected curl response format: {stdout!r}")
    body, status_line = stdout.rsplit("\n", 1)
    try:
        status_code = int(status_line.strip())
    except ValueError as exc:
        raise Image2Error(f"Unexpected HTTP status output: {status_line!r}") from exc
    return status_code, body


def run_curl_multipart(
    url: str,
    api_key: str,
    prompt: str,
    model: str,
    size: str,
    quality: str,
    n_value: int,
    input_images: list[Path],
    mask_path: Path | None,
) -> tuple[int, str]:
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
        f"Authorization: Bearer {api_key}",
        "--form",
        f"model={model}",
        "--form",
        f"prompt={prompt}",
        "--form",
        f"size={size}",
        "--form",
        f"quality={quality}",
        "--form",
        f"n={n_value}",
        "--write-out",
        "\n%{http_code}",
    ]

    for image_path in input_images:
        command.extend(["--form", f"image[]=@{image_path}"])

    if mask_path is not None:
        command.extend(["--form", f"mask=@{mask_path}"])

    command.append(url)

    completed = subprocess.run(
        command,
        capture_output=True,
        text=True,
        check=False,
        encoding="utf-8",
    )

    stdout = completed.stdout or ""
    stderr = completed.stderr.strip()
    if completed.returncode != 0:
        message = stderr or stdout.strip() or f"curl failed with exit code {completed.returncode}"
        raise Image2Error(message)

    if "\n" not in stdout:
        raise Image2Error(f"Unexpected curl response format: {stdout!r}")
    body, status_line = stdout.rsplit("\n", 1)
    try:
        status_code = int(status_line.strip())
    except ValueError as exc:
        raise Image2Error(f"Unexpected HTTP status output: {status_line!r}") from exc
    return status_code, body


def run_curl_download(url: str, output_path: Path) -> None:
    command = [
        "curl.exe",
        "--noproxy",
        "*",
        "--silent",
        "--show-error",
        "--location",
        "--output",
        str(output_path),
        url,
    ]
    completed = subprocess.run(
        command,
        capture_output=True,
        text=True,
        check=False,
        encoding="utf-8",
    )
    if completed.returncode != 0:
        message = completed.stderr.strip() or completed.stdout.strip()
        raise Image2Error(message or "Failed to download generated image URL.")


def parse_json_response(body: str) -> dict[str, Any]:
    try:
        payload = json.loads(body)
    except json.JSONDecodeError as exc:
        raise Image2Error(f"Failed to parse JSON response: {exc}: {body[:500]}") from exc
    if not isinstance(payload, dict):
        raise Image2Error("Unexpected JSON response shape.")
    return payload


def extract_error_message(payload: dict[str, Any]) -> str:
    error = payload.get("error")
    if isinstance(error, dict):
        for key in ("message", "code", "type"):
            value = error.get(key)
            if isinstance(value, str) and value.strip():
                return value
    if isinstance(error, str) and error.strip():
        return error
    message = payload.get("message")
    if isinstance(message, str) and message.strip():
        return message
    return json.dumps(payload, ensure_ascii=False)


def list_image_models(models_endpoint: str, api_key: str) -> list[str]:
    command = [
        "curl.exe",
        "--noproxy",
        "*",
        "--silent",
        "--show-error",
        "--location",
        "--header",
        f"Authorization: Bearer {api_key}",
        "--write-out",
        "\n%{http_code}",
        models_endpoint,
    ]
    completed = subprocess.run(
        command,
        capture_output=True,
        text=True,
        check=False,
        encoding="utf-8",
    )
    stdout = completed.stdout or ""
    stderr = completed.stderr.strip()
    if completed.returncode != 0:
        raise Image2Error(stderr or stdout.strip() or "Failed to query /v1/models.")
    if "\n" not in stdout:
        raise Image2Error("Unexpected /v1/models response format.")
    body, status_line = stdout.rsplit("\n", 1)
    status_code = int(status_line.strip())
    payload = parse_json_response(body)
    if status_code == 401 or status_code == 403:
        raise Image2Error("image2 dedicated key was rejected while querying /v1/models.")
    if status_code >= 400:
        raise Image2Error(f"/v1/models failed: {extract_error_message(payload)}")

    data = payload.get("data")
    if not isinstance(data, list):
        raise Image2Error("Unexpected /v1/models payload: missing data array.")

    image_like_models: list[str] = []
    for item in data:
        if not isinstance(item, dict):
            continue
        model_id = item.get("id")
        if not isinstance(model_id, str):
            continue
        lowered = model_id.lower()
        if "image" in lowered or "img" in lowered or "vision" in lowered:
            image_like_models.append(model_id)

    if not image_like_models:
        raise Image2Error("No image-capable models were returned for the image2 dedicated key.")
    return image_like_models


def save_b64_image(item: dict[str, Any], output_dir: Path, filename: str | None, prompt: str) -> Path:
    b64_value = item.get("b64_json")
    if not isinstance(b64_value, str) or not b64_value:
        raise Image2Error("Response item did not contain b64_json.")
    raw = base64.b64decode(b64_value)
    extension = detect_extension_from_bytes(raw)
    final_name = normalize_filename(filename, slugify_filename(prompt), extension)
    output_path = output_dir / final_name
    output_path.write_bytes(raw)
    return output_path.resolve()


def download_url_image(item: dict[str, Any], output_dir: Path, filename: str | None, prompt: str) -> Path:
    url = item.get("url")
    if not isinstance(url, str) or not url:
        raise Image2Error("Response item did not contain a URL.")
    extension = detect_extension_from_url(url)
    final_name = normalize_filename(filename, slugify_filename(prompt), extension)
    output_path = output_dir / final_name
    run_curl_download(url, output_path)
    return output_path.resolve()


def save_images(payload: dict[str, Any], output_dir: Path, filename: str | None, prompt: str) -> list[Path]:
    data = payload.get("data")
    if not isinstance(data, list) or not data:
        raise Image2Error("Image response did not contain any image data.")

    saved_paths: list[Path] = []
    fallback_stem = slugify_filename(prompt)
    for index, item in enumerate(data, start=1):
        if not isinstance(item, dict):
            continue
        current_filename = filename
        if len(data) > 1:
            if filename:
                stem = Path(filename).stem
                suffix = Path(filename).suffix
                current_filename = f"{stem}-{index}{suffix}" if suffix else f"{filename}-{index}"
            else:
                current_filename = f"{fallback_stem}-{index}"
        if "b64_json" in item:
            saved_paths.append(save_b64_image(item, output_dir, current_filename, prompt))
        elif "url" in item:
            saved_paths.append(download_url_image(item, output_dir, current_filename, prompt))
        else:
            raise Image2Error("Image response item contained neither b64_json nor url.")

    if not saved_paths:
        raise Image2Error("No images were saved from the response.")
    return saved_paths


def generate_images(
    endpoint: str,
    models_endpoint: str,
    api_key: str,
    prompt: str,
    model: str,
    size: str,
    quality: str,
    n_value: int,
    output_dir: Path,
    filename: str | None,
) -> tuple[list[Path], str]:
    payload = {
        "model": model,
        "prompt": prompt,
        "size": size,
        "quality": quality,
        "n": n_value,
    }

    status_code, body = run_curl_json(endpoint, payload, api_key)
    parsed = parse_json_response(body)

    if status_code in {401, 403}:
        raise Image2Error("image2 dedicated key was rejected by the image generation endpoint.")

    if status_code >= 400:
        message = extract_error_message(parsed)
        if "No available compatible accounts" in message:
            available_models = list_image_models(models_endpoint, api_key)
            preferred_model = available_models[0]
            if model != preferred_model:
                payload["model"] = preferred_model
                status_code, body = run_curl_json(endpoint, payload, api_key)
                parsed = parse_json_response(body)
                if status_code in {401, 403}:
                    raise Image2Error("image2 dedicated key was rejected by the image generation endpoint.")
                if status_code >= 400:
                    raise Image2Error(extract_error_message(parsed))
                return save_images(parsed, output_dir, filename, prompt), preferred_model
        raise Image2Error(message)

    return save_images(parsed, output_dir, filename, prompt), model


def edit_images(
    endpoint: str,
    models_endpoint: str,
    api_key: str,
    prompt: str,
    model: str,
    size: str,
    quality: str,
    n_value: int,
    output_dir: Path,
    filename: str | None,
    input_images: list[Path],
    mask_path: Path | None,
) -> tuple[list[Path], str]:
    status_code, body = run_curl_multipart(
        url=endpoint,
        api_key=api_key,
        prompt=prompt,
        model=model,
        size=size,
        quality=quality,
        n_value=n_value,
        input_images=input_images,
        mask_path=mask_path,
    )
    parsed = parse_json_response(body)

    if status_code in {401, 403}:
        raise Image2Error("image2 dedicated key was rejected by the image editing endpoint.")

    if status_code >= 400:
        message = extract_error_message(parsed)
        if "No available compatible accounts" in message:
            available_models = list_image_models(models_endpoint, api_key)
            preferred_model = available_models[0]
            if model != preferred_model:
                status_code, body = run_curl_multipart(
                    url=endpoint,
                    api_key=api_key,
                    prompt=prompt,
                    model=preferred_model,
                    size=size,
                    quality=quality,
                    n_value=n_value,
                    input_images=input_images,
                    mask_path=mask_path,
                )
                parsed = parse_json_response(body)
                if status_code in {401, 403}:
                    raise Image2Error("image2 dedicated key was rejected by the image editing endpoint.")
                if status_code >= 400:
                    raise Image2Error(extract_error_message(parsed))
                return save_images(parsed, output_dir, filename, prompt), preferred_model
        raise Image2Error(message)

    return save_images(parsed, output_dir, filename, prompt), model


def main() -> int:
    args = parse_args()
    if args.n < 1:
        print("--n must be >= 1", file=sys.stderr)
        return 1

    try:
        config: dict[str, Any] | None = None
        config_path = get_config_path(args.config)
        if config_path.exists():
            config = load_config(config_path)
        base_url = resolve_base_url(config, args.base_url)
        endpoint = build_images_endpoint(base_url)
        edits_endpoint = build_edits_endpoint(base_url)
        models_endpoint = build_models_endpoint(base_url)
        api_key = read_private_key()
        output_dir = Path(args.output_dir).expanduser().resolve()
        output_dir.mkdir(parents=True, exist_ok=True)
        requested_model = args.model or read_default_model()
        input_images = resolve_input_paths(args.input_image, "input image")
        mask_path = resolve_input_paths([args.mask], "mask")[0] if args.mask else None
        if input_images:
            saved_paths, resolved_model = edit_images(
                endpoint=edits_endpoint,
                models_endpoint=models_endpoint,
                api_key=api_key,
                prompt=args.prompt,
                model=requested_model,
                size=args.size,
                quality=args.quality,
                n_value=args.n,
                output_dir=output_dir,
                filename=args.filename,
                input_images=input_images,
                mask_path=mask_path,
            )
        else:
            saved_paths, resolved_model = generate_images(
                endpoint=endpoint,
                models_endpoint=models_endpoint,
                api_key=api_key,
                prompt=args.prompt,
                model=requested_model,
                size=args.size,
                quality=args.quality,
                n_value=args.n,
                output_dir=output_dir,
                filename=args.filename,
            )
        if resolved_model != requested_model:
            write_default_model(resolved_model)
    except Image2Error as exc:
        print(str(exc), file=sys.stderr)
        return 1

    for path in saved_paths:
        print(str(path))
    if resolved_model != requested_model:
        print(
            f"[model-updated] default model switched to available image model: {resolved_model}",
            file=sys.stderr,
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
