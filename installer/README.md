# EcomTool Studio 安装包打包说明

## 方案

安装包使用 Inno Setup 生成，安装时会覆盖旧版本程序文件。

用户数据不放在安装目录中，现有数据库和模板资源默认保存在：

```text
%LocalAppData%\ToolBox\
```

因此重新安装或覆盖安装不会删除用户本地数据。

## 准备

先安装 Inno Setup 6：

```text
https://jrsoftware.org/isinfo.php
```

安装后确认本机能找到：

```text
C:\Program Files (x86)\Inno Setup 6\ISCC.exe
```

## 打包

在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build_installer.ps1
```

脚本会先生成便携包，然后把最新便携包打进安装包。

输出目录：

```text
dist\installer\
```

输出文件示例：

```text
EcomTool_Studio_Setup_2026.07.11.1430.exe
```

## 只用已有便携包打安装包

如果已经有最新便携包，不想重新生成，可以执行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build_installer.ps1 -SkipPortableBuild
```

## 指定 Inno Setup 编译器路径

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build_installer.ps1 -InnoSetupCompiler "D:\Tools\Inno Setup 6\ISCC.exe"
```

## 覆盖安装逻辑

安装包 AppId 固定，所以别人电脑上已经安装过时，再运行新安装包会自动覆盖升级。

安装目录默认：

```text
C:\Program Files\EcomTool Studio\
```

本地数据目录仍保留：

```text
%LocalAppData%\ToolBox\
```
