---
name: image2-generate
description: 使用这个 skill 在用户说“用 image2 生图”“image2 生成图片”或“image2-generate”时，通过 image2 专用密钥生成图片，并将结果保存到本地。
---

# Image2 Generate

当用户明确要求使用 image2 生图时，使用这个 skill。

触发示例：
- 用 image2 生图
- image2 生成图片
- image2-generate

执行要求：
- 一律通过 `scripts/generate_image.py` 发起请求。
- 不要改动当前 Codex 聊天分组、默认模型、模型供应商或中转配置。
- `generate_image.py` 会从当前 Codex 配置读取 `base_url`，但认证必须强制使用本 skill 私有 key。
- 如果用户没有指定模型，默认优先使用 `gpt-image-2`。
- 输出成功后，读取脚本打印出的绝对路径并回传给用户。

多图生成策略：
- 当用户要求一次生成多张图片时，优先使用小批量分批生成来提高成功率。
- 默认优先按 `2` 张或 `3` 张一批执行，而不是一次请求过多张图片。
- 如果用户要求例如 `5` 张图片，优先采用 `3 + 2` 的方式分两批生成。
- 第一批成功后再继续下一批，优先保证稳定拿到结果，而不是追求单次请求完成全部图片。
- 对于约束很多、带排版、带大量英文文字、海报式、电商主图式的长提示词，请默认更保守地分批生成。
- 如果接口出现超时或类似 `524` 的错误，不要重复同样的大批量请求，优先改成更小批次重试。

文件命名规则：
- 如果只生成 1 张图片，默认按提示词自动生成文件名；如果传入 `--filename`，则使用指定文件名。
- 如果一次生成多张图片且传入了 `--filename`，脚本会自动保存为 `name-1.png`、`name-2.png` 这类编号文件。
- 如果一次生成多张图片且没有传入 `--filename`，脚本会根据提示词自动生成基础文件名，并追加 `-1`、`-2`、`-3` 这类编号，避免重名覆盖。

常用命令示例：

```powershell
python3 C:\Users\Administrator\.codex\skills\image2-generate\scripts\generate_image.py --prompt "一只可爱的猫"
```

```powershell
python3 C:\Users\Administrator\.codex\skills\image2-generate\scripts\generate_image.py --prompt "一只可爱的猫" --size 1024x1024 --quality high --n 1
```

```powershell
python3 C:\Users\Administrator\.codex\skills\image2-generate\scripts\generate_image.py --prompt "一只可爱的猫" --n 3
```

```powershell
python3 C:\Users\Administrator\.codex\skills\image2-generate\scripts\generate_image.py --prompt "一只可爱的猫" --output-dir "C:\Users\Administrator\Documents\Codex\output" --filename "cat.png"
```
