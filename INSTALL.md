# ImageKeeper 便携版使用说明

## 目录结构

- `EcomTool Studio.exe`
- `runtime\python\`
- `runtime\node\`
- `tools\python\`
- `tools\node\`
- `config\miaoshou.json`
- `data\workspace\review`
- `data\workspace\backup`
- `data\workspace\excel`
- `data\workspace\assert`
- `data\workspace\temp`

## 首次使用

1. 解压整个目录到本地硬盘，例如 `D:\EcomTool_Studio_Portable`
2. 进入 `data\workspace\temp`
   把模板库 `文生图模板库_Codex.xlsx` 放在这里
3. 如果要使用图片生成
   检查 `tools\python\image2-generate\.image2_api_key`
   确认密钥可用
4. 如果要使用妙手批量上架
   检查 `config\miaoshou.json`
   确认妙手地址、浏览器通道和 profile 名称正确
5. 双击 `EcomTool Studio.exe`

## 目标机器需要的环境

- Windows 10 或 Windows 11
- Edge 浏览器
- 可正常登录妙手 ERP 的账号

## 不再需要单独安装的环境

安装包内已经自带：

- `.NET` 运行时
- `Python`
- `Node.js`

所以正常情况下，不需要另外安装这三项。

## 默认工作目录

- 筛图目录：`data\workspace\review`
- 备份目录：`data\workspace\backup`
- 商品表输出：`data\workspace\excel`
- SP 批处理输出：`data\workspace\assert`
- 模板库目录：`data\workspace\temp`

## 自动上架说明

点击“自动上架”后，程序会：

1. 生成商品信息表
2. 生成妙手批量上架 JSON
3. 启动内置 Playwright 妙手批量上架流程

如果失败，优先检查：

- `config\miaoshou.json`
- `tools\python\temu-product-sheet\data\`
- `tools\python\image2-generate\.image2_api_key`
- 当前 SP 目录下是否存在 `main\2-*.png`
- `output\miaoshou\` 下的 `publish.log`、`events.jsonl` 和 `batch-result.json`

## 打包命令

在开发机器执行：

```powershell
powershell -ExecutionPolicy Bypass -File D:\new_project\scripts\build_portable_package.ps1
```

生成目录：

```text
D:\new_project\dist\EcomTool_Studio_Portable
```
