import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import "dotenv/config";
import { chromium, Locator, Page } from "playwright";

type ProductJsonItem = Record<string, unknown>;
type MiaoshouConfig = {
  baseUrl?: string;
  browserChannel?: string;
  profileName?: string;
  profilesDir?: string;
  killEdgeBeforeLaunch?: boolean;
  killEdgeOnBusy?: boolean;
  keepBrowserOpen?: boolean;
};

type CliOptions = {
  manifestPath: string;
  resultPath: string;
  configPath: string;
  eventsPath: string;
  logPath: string;
};

const cliOptions = parseCliOptions(process.argv.slice(2));
const miaoshouConfig = readMiaoshouConfig(cliOptions.configPath);
const browserChannel = miaoshouConfig.browserChannel || process.env.BROWSER_CHANNEL || "chromium";
const baseUrl = miaoshouConfig.baseUrl || process.env.BASE_URL || "https://erp.91miaoshou.com/welcome";
const profileName = miaoshouConfig.profileName || process.env.PROFILE_NAME || "miaoshou-edge-main";
const configuredProfilesDir = miaoshouConfig.profilesDir || process.env.MIAOSHOU_PROFILES_DIR || "";
const killEdgeBeforeLaunch = Boolean(miaoshouConfig.killEdgeBeforeLaunch) || process.env.KILL_EDGE_BEFORE_LAUNCH === "1";
const killEdgeOnBusy = (miaoshouConfig.killEdgeOnBusy ?? true) || process.env.KILL_EDGE_ON_BUSY === "1";
const keepBrowserOpen = miaoshouConfig.keepBrowserOpen ?? false;
const collectBoxItemsUrl = "https://erp.91miaoshou.com/pddkj/collect_box/items";
const pictureIndexUrl = "https://erp.91miaoshou.com/picture/index";
const productsJsonPath = cliOptions.manifestPath;

fs.mkdirSync(path.dirname(cliOptions.eventsPath), { recursive: true });
fs.mkdirSync(path.dirname(cliOptions.logPath), { recursive: true });
safeWriteTextFile(cliOptions.eventsPath, "", "events log init");
fs.writeFileSync(cliOptions.logPath, "", "utf8");

const immediateLoginText = "\u7acb\u5373\u767b\u5f55";
const createProductText = "\u521b\u5efa\u4ea7\u54c1";
const fullscreenText = "\u5168\u5c4f";
const shopText = "\u5e97\u94fa";
const checkAllSuccessText = "\u5df2\u70b9\u51fb\u5168\u9009";
const checkAllMissingText = "\u672a\u627e\u5230\u5168\u9009";
const categoryPathText =
  "\u827a\u672f\u54c1\u3001\u5de5\u827a\u54c1\u548c\u7f1d\u7eab\u7528\u54c1/\u5de5\u827a\u5de5\u5177\u548c\u7528\u54c1/\u76ae\u9769\u624b\u5de5\u827a/\u76ae\u9769\u624b\u5de5\u827a\u90e8\u4ef6";
const materialText = "\u6750\u6599";
const materialValueText = "\u81ea\u7c98PU\u9769";
const backingMaterialText = "\u5e95\u5e03\u6750\u8d28";
const backingMaterialValueText = "\u5c3c\u9f99";
const moreAttributesText = "\u66f4\u591a\u5c5e\u6027";
const surfacePatternText = "\u8868\u76ae\u82b1\u7eb9";
const surfacePatternValueText = "\u8354\u679d\u7eb9";
const thicknessText = "\u539a\u5ea6\uff08mm\uff09";
const thicknessValueText = "1.01";
const leatherTypeText = "\u8868\u76ae\u7c7b\u578b";
const leatherTypeValueText = "\u6f06\u76ae";
const productTitleText = "\u4ea7\u54c1\u6807\u9898";
const englishTitleText = "\u82f1\u8bed\u6807\u9898";
const originText = "\u4ea7\u5730";
const originValueText = "\u4e2d\u56fd\u5927\u9646 / \u5e7f\u4e1c\u7701";
const productInfoNavText = "\u4ea7\u54c1\u4fe1\u606f";
const salesAttributesNavText = "\u9500\u552e\u5c5e\u6027";
const packagingInfoNavText = "\u5305\u88c5\u4fe1\u606f";
const outerPackagingShapeText = "\u5916\u5305\u88c5\u5f62\u72b6";
const outerPackagingShapeValueText = "\u5706\u67f1\u4f53";
const outerPackagingTypeText = "\u5916\u5305\u88c5\u7c7b\u578b";
const outerPackagingTypeValueText = "\u8f6f\u5305\u88c5+\u786c\u7269";
const productDescriptionText = "\u4ea7\u54c1\u63cf\u8ff0";
const addDescriptionText = "\u6dfb\u52a0\u63cf\u8ff0";
const batchOperationText = "\u6279\u91cf\u64cd\u4f5c";
const batchImageProcessText = "\u6279\u91cf\u56fe\u7247\u5904\u7406";
const batchImageUploadDialogTitleText = "\u6279\u91cf\u56fe\u7247\u4e0a\u4f20";
const saveText = "\u4fdd\u5b58";
const createPublishText = "\u521b\u5efa\u5e76\u53d1\u5e03";
const releaseProductDialogTitleText = "\u53d1\u5e03\u4ea7\u54c1";
const publishToSelectedShopText = "\u53d1\u5e03\u5230\u9009\u4e2d\u5e97\u94fa";
const promptDialogTitleText = "\u63d0\u793a";
const closeText = "\u5173\u95ed";
const batchDeleteText = "\u6279\u91cf\u5220\u9664";
const deleteCurrentPageText = "\u5220\u9664\u672c\u9875";
const confirmDeleteText = "\u786e\u5b9a\u5220\u9664";
const addSalesSpecText = "\u65b0\u589e\u9500\u552e\u89c4\u683c";
const addOptionText = "\u6dfb\u52a0\u9009\u9879";
const batchText = "\u6279\u91cf";
const batchUploadImageText = "\u6279\u91cf\u4f20\u56fe";
const uploadImageText = "\u4e0a\u4f20\u56fe\u7247";
const localUploadText = "\u672c\u5730\u4e0a\u4f20";
const cancelText = "\u53d6\u6d88";
const supplyPriceText = "\u4f9b\u8d27\u4ef7";
const suggestedPriceText = "\u5efa\u8bae\u552e\u4ef7";
const suggestedPriceBatchValue = "2";
const skuClassificationText = "SKU\u5206\u7c7b";
const singleItemText = "\u5355\u54c1";
const packageSizeText = "\u5c3a\u5bf8(CM)";
const packageSizeDialogTitleText = "\u6279\u91cf\u7f16\u8f91\u5305\u88f9\u5c3a\u5bf8";
const weightText = "\u91cd\u91cf";
const weightDialogTitleText = "\u6279\u91cf\u4fee\u6539\u91cd\u91cf";
const skuColorList = ["\u9ed1\u8272", "\u7c73\u767d\u8272", "\u6df1\u68d5\u8272", "\u6df1\u7070\u8272", "\u9152\u7ea2\u8272", "\u5b9d\u84dd\u8272"] as const;
const supportedImageExtensions = new Set([".jpg", ".jpeg", ".png", ".webp", ".bmp"]);

function parseCliOptions(args: string[]): CliOptions {
  const getValue = (name: string) => {
    const index = args.indexOf(name);
    return index >= 0 ? args[index + 1] : undefined;
  };

  const manifestPath = getValue("--manifest") || process.env.MIAOSHOU_MANIFEST || "";
  const resultPath = getValue("--result") || process.env.MIAOSHOU_RESULT || path.resolve(__dirname, "..", "output", "batch-result.json");
  const configPath = getValue("--config") || process.env.MIAOSHOU_CONFIG || path.resolve(__dirname, "..", "..", "..", "config", "miaoshou.json");
  const eventsPath = getValue("--events") || process.env.MIAOSHOU_EVENTS || path.resolve(__dirname, "..", "output", "events.jsonl");
  const logPath = getValue("--log") || process.env.MIAOSHOU_LOG || path.resolve(__dirname, "..", "output", "publish.log");

  if (!manifestPath) {
    throw new Error("Missing required --manifest path.");
  }

  return {
    manifestPath: path.resolve(manifestPath),
    resultPath: path.resolve(resultPath),
    configPath: path.resolve(configPath),
    eventsPath: path.resolve(eventsPath),
    logPath: path.resolve(logPath),
  };
}

function readMiaoshouConfig(configPath: string): MiaoshouConfig {
  if (!configPath || !fs.existsSync(configPath)) {
    return {};
  }

  const raw = fs.readFileSync(configPath, "utf8");
  return JSON.parse(raw) as MiaoshouConfig;
}

function emitEvent(type: string, payload: Record<string, unknown> = {}) {
  const event = {
    type,
    timestamp: new Date().toISOString(),
    ...payload,
  };
  const line = JSON.stringify(event);
  safeAppendTextFile(cliOptions.eventsPath, `${line}\n`, "events log append");
  console.log(line);
}

function safeWriteTextFile(filePath: string, content: string, action: string) {
  try {
    fs.writeFileSync(filePath, content, "utf8");
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.warn(`Skipped ${action}: ${message}`);
  }
}

function safeAppendTextFile(filePath: string, content: string, action: string) {
  try {
    fs.appendFileSync(filePath, content, "utf8");
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.warn(`Skipped ${action}: ${message}`);
  }
}

function logMessage(message: string, payload: Record<string, unknown> = {}) {
  const textLine = `[${new Date().toISOString()}] ${message}`;
  fs.appendFileSync(cliOptions.logPath, `${textLine}\n`, "utf8");
  emitEvent("step", { message, ...payload });
}

function writeResult(payload: Record<string, unknown>) {
  fs.mkdirSync(path.dirname(cliOptions.resultPath), { recursive: true });
  fs.writeFileSync(cliOptions.resultPath, JSON.stringify(payload, null, 2), "utf8");
}

function readJsonInstance() {
  const raw = fs.readFileSync(productsJsonPath, "utf8").replace(/^\uFEFF/, "");
  const parsed = JSON.parse(raw) as unknown;

  if (!Array.isArray(parsed)) {
    throw new Error(`Expected ${productsJsonPath} to contain a JSON array.`);
  }

  return parsed as ProductJsonItem[];
}

function getExistingProfileLockPaths(userDataDir: string) {
  const lockFileNames = [
    "SingletonLock",
    "SingletonCookie",
    "SingletonSocket",
    "lockfile",
  ];

  return lockFileNames
    .map((fileName) => path.join(userDataDir, fileName))
    .filter((filePath) => fs.existsSync(filePath));
}

function clearProfileLockFiles(userDataDir: string) {
  const lockPaths = getExistingProfileLockPaths(userDataDir);
  for (const lockPath of lockPaths) {
    try {
      fs.rmSync(lockPath, { force: true, recursive: true });
      console.log(`Removed stale profile lock: ${lockPath}`);
    } catch {
      console.log(`Could not remove profile lock: ${lockPath}`);
    }
  }

  return lockPaths.length;
}

function buildProfileBusyMessage(userDataDir: string, originalError?: unknown) {
  const lockPaths = getExistingProfileLockPaths(userDataDir);
  const lockSummary =
    lockPaths.length > 0 ? `Detected profile lock files: ${lockPaths.join(", ")}` : "No lock files were detected.";
  const originalMessage =
    originalError instanceof Error ? originalError.message : typeof originalError === "string" ? originalError : "";

  return [
    `Edge profile is busy: ${userDataDir}`,
    "This error happens before any page HTML selector logic runs.",
    "It usually means this dedicated Edge profile is already in use by another Edge session.",
    lockSummary,
    "Please fully close Edge windows using this profile, then run the script again.",
    "If you want, I can also change the script to use a different profile name.",
    originalMessage ? `Original Playwright error: ${originalMessage}` : "",
  ]
    .filter((line) => line.length > 0)
    .join("\n");
}

function isLikelyProfileBusyError(error: unknown) {
  const message = error instanceof Error ? error.message : typeof error === "string" ? error : "";
  return /profile|user data directory|Singleton|Target page, context or browser has been closed/i.test(message)
    && !/Arguments can not specify page to be opened/i.test(message);
}

function escapePowerShellSingleQuoted(value: string) {
  return value.replace(/'/g, "''");
}

function killEdgeProcessesForProfile(userDataDir: string) {
  try {
    const escapedProfile = escapePowerShellSingleQuoted(userDataDir);
    const output = execFileSync(
      "powershell",
      [
        "-NoProfile",
        "-Command",
        [
          `$profile = [System.IO.Path]::GetFullPath('${escapedProfile}').TrimEnd('\\').ToLowerInvariant()`,
          "$matched = @(Get-CimInstance Win32_Process -Filter \"name = 'msedge.exe'\" | Where-Object {",
          "  if (-not $_.CommandLine) { return $false }",
          "  $commandLine = $_.CommandLine.Replace('/', '\\').ToLowerInvariant()",
          "  $commandLine.Contains($profile)",
          "})",
          "$matched | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }",
          "$matched.Count",
          "Start-Sleep -Milliseconds 1200",
        ].join("; "),
      ],
      { encoding: "utf8" },
    );
    const killedCount = Number.parseInt(String(output).trim(), 10) || 0;
    console.log(`Killed ${killedCount} Edge process(es) using profile: ${userDataDir}`);
    return killedCount;
  } catch {
    console.log(`No Edge processes were terminated for profile: ${userDataDir}`);
    return 0;
  }
}

async function launchPersistentContextForProfile(userDataDir: string) {
  const launchOptions = {
    headless: false,
    args: ["--start-maximized"],
    viewport: null,
    ...(browserChannel === "chromium" ? {} : { channel: browserChannel }),
  };

  return await chromium.launchPersistentContext(userDataDir, {
    ...launchOptions,
  });
}

async function createSingleAutomationPage(context: Awaited<ReturnType<typeof launchPersistentContextForProfile>>) {
  const page = await context.newPage();
  for (const existingPage of context.pages()) {
    if (existingPage !== page && !existingPage.isClosed()) {
      await existingPage.close({ runBeforeUnload: false }).catch(() => {});
    }
  }

  await page.bringToFront().catch(() => {});
  return page;
}

function getDefaultProfilesDir() {
  const localAppData = process.env.LOCALAPPDATA;
  if (localAppData && localAppData.trim().length > 0) {
    return path.join(localAppData, "EcomToolStudio", "miaoshou-playwright", "profiles");
  }

  return path.resolve(__dirname, "..", ".profiles");
}

function resolveProfilesDir() {
  if (configuredProfilesDir.trim().length > 0) {
    return path.resolve(expandWindowsEnvVars(configuredProfilesDir));
  }

  return getDefaultProfilesDir();
}

function expandWindowsEnvVars(value: string) {
  return value.replace(/%([^%]+)%/g, (_, name: string) => process.env[name] || `%${name}%`);
}

function getProductLabel(product: ProductJsonItem, index: number) {
  const candidates = [product.title, product.name, product.product_name, product.spu, product.id];
  const firstString = candidates.find((value) => typeof value === "string" && value.trim().length > 0);
  if (typeof firstString === "string") {
    return firstString;
  }

  const firstNumber = candidates.find((value) => typeof value === "number");
  if (typeof firstNumber === "number") {
    return String(firstNumber);
  }

  return `item-${index + 1}`;
}

function getProductTitle(product: ProductJsonItem) {
  const candidates = [product.title, product.name, product.product_name];
  const firstString = candidates.find((value) => typeof value === "string" && value.trim().length > 0);
  return typeof firstString === "string" ? firstString : "";
}

function getEnglishTitle(product: ProductJsonItem) {
  const candidates = [product.englist_title, product.english_title, product.en_title];
  const firstString = candidates.find((value) => typeof value === "string" && value.trim().length > 0);
  return typeof firstString === "string" ? firstString : "";
}

function getMainFileFolder(product: ProductJsonItem) {
  const folder = product.main_file_folder;
  return typeof folder === "string" && folder.trim().length > 0 ? folder.trim() : "";
}

function getDetailFileFolder(product: ProductJsonItem) {
  const folder = product.detail_file_folder;
  return typeof folder === "string" && folder.trim().length > 0 ? folder.trim() : "";
}

function getSkuSizeValues(product: ProductJsonItem) {
  const rawValue = product.sku_size_list;
  if (!Array.isArray(rawValue)) {
    throw new Error("The current product item does not contain a valid sku_size_list array.");
  }

  const sizes = rawValue
    .map((item) => {
      if (!item || typeof item !== "object") {
        return "";
      }

      const sizeValue = (item as { size?: unknown }).size;
      return typeof sizeValue === "string" ? sizeValue.trim() : "";
    })
    .filter((value) => value.length > 0);

  if (sizes.length === 0) {
    throw new Error("The current product item does not contain any usable size values in sku_size_list.");
  }

  return sizes;
}

function getSkuSizePriceEntries(product: ProductJsonItem) {
  const rawValue = product.sku_size_list;
  if (!Array.isArray(rawValue)) {
    throw new Error("The current product item does not contain a valid sku_size_list array.");
  }

  const entries = rawValue
    .map((item) => {
      if (!item || typeof item !== "object") {
        return null;
      }

      const sizeValue = (item as { size?: unknown }).size;
      const supplyPriceValue = (item as { supply_price?: unknown }).supply_price;
      const size = typeof sizeValue === "string" ? sizeValue.trim() : "";
      const supplyPrice = typeof supplyPriceValue === "string" ? supplyPriceValue.trim() : String(supplyPriceValue ?? "").trim();

      if (!size || !supplyPrice) {
        return null;
      }

      return { size, supplyPrice };
    })
    .filter((entry): entry is { size: string; supplyPrice: string } => Boolean(entry));

  if (entries.length === 0) {
    throw new Error("The current product item does not contain any usable size/supply_price pairs in sku_size_list.");
  }

  return entries;
}

function readRequiredTextField(item: Record<string, unknown>, key: string, index: number) {
  const rawValue = item[key];
  const value = typeof rawValue === "string" ? rawValue.trim() : String(rawValue ?? "").trim();

  if (!value) {
    throw new Error(`sku_size_list[${index}] is missing a usable '${key}' value.`);
  }

  return value;
}

function getSkuSizeDimensionEntries(product: ProductJsonItem) {
  const rawValue = product.sku_size_list;
  if (!Array.isArray(rawValue)) {
    throw new Error("The current product item does not contain a valid sku_size_list array.");
  }

  const entries = rawValue.map((item, index) => {
    if (!item || typeof item !== "object") {
      throw new Error(`sku_size_list[${index}] is not a valid object.`);
    }

    const record = item as Record<string, unknown>;
    return {
      size: readRequiredTextField(record, "size", index),
      length: readRequiredTextField(record, "length", index),
      width: readRequiredTextField(record, "width", index),
      height: readRequiredTextField(record, "height", index),
    };
  });

  if (entries.length === 0) {
    throw new Error("The current product item does not contain any usable dimension entries in sku_size_list.");
  }

  return entries;
}

function getSkuSizeWeightEntries(product: ProductJsonItem) {
  const rawValue = product.sku_size_list;
  if (!Array.isArray(rawValue)) {
    throw new Error("The current product item does not contain a valid sku_size_list array.");
  }

  const entries = rawValue.map((item, index) => {
    if (!item || typeof item !== "object") {
      throw new Error(`sku_size_list[${index}] is not a valid object.`);
    }

    const record = item as Record<string, unknown>;
    return {
      size: readRequiredTextField(record, "size", index),
      weight: readRequiredTextField(record, "weight", index),
    };
  });

  if (entries.length === 0) {
    throw new Error("The current product item does not contain any usable weight entries in sku_size_list.");
  }

  return entries;
}

function getPreviewImageFolder(product: ProductJsonItem) {
  const folder = product.preview_image_folder;
  return typeof folder === "string" && folder.trim().length > 0 ? folder.trim() : "";
}

function getPreviewImagePathByColor(product: ProductJsonItem, skuColor: string) {
  const previewImageFolder = getPreviewImageFolder(product);
  if (!previewImageFolder) {
    throw new Error("The current product item does not contain a valid preview_image_folder.");
  }

  const exactCandidates = [".png", ".jpg", ".jpeg", ".webp", ".bmp"].map((extension) =>
    path.join(previewImageFolder, `${skuColor}${extension}`),
  );
  const exactMatch = exactCandidates.find((candidate) => fs.existsSync(candidate));
  if (exactMatch) {
    return exactMatch;
  }

  const fuzzyMatch = fs
    .readdirSync(previewImageFolder, { withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => path.join(previewImageFolder, entry.name))
    .find((filePath) => {
      const baseName = path.basename(filePath, path.extname(filePath));
      return (
        supportedImageExtensions.has(path.extname(filePath).toLowerCase()) &&
        baseName.includes(skuColor)
      );
    });

  if (!fuzzyMatch) {
    throw new Error(`Could not find a preview image file for color '${skuColor}' in ${previewImageFolder}.`);
  }

  return fuzzyMatch;
}

function getImageFilesFromFolder(folderPath: string, options: { folderLabel?: string; maxCount?: number | null } = {}) {
  const folderLabel = options.folderLabel ?? "main_file_folder";
  const maxCount = options.maxCount === undefined ? 10 : options.maxCount;

  if (!folderPath) {
    throw new Error(`The current product item does not contain a valid ${folderLabel}.`);
  }

  if (!fs.existsSync(folderPath)) {
    throw new Error(`The ${folderLabel} does not exist: ${folderPath}`);
  }

  const imageFiles = fs
    .readdirSync(folderPath, { withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => path.join(folderPath, entry.name))
    .filter((filePath) => supportedImageExtensions.has(path.extname(filePath).toLowerCase()))
    .sort((left, right) => left.localeCompare(right, "en"));

  if (imageFiles.length === 0) {
    throw new Error(`No supported image files were found in ${folderLabel}: ${folderPath}`);
  }

  if (maxCount !== null && imageFiles.length > maxCount) {
    throw new Error(
      `${folderLabel} contains ${imageFiles.length} images, but this upload allows at most ${maxCount}.`,
    );
  }

  return imageFiles;
}

function isSuccessfulUploadResponse(payload: unknown) {
  if (!payload || typeof payload !== "object") {
    return false;
  }

  const result = (payload as { result?: unknown }).result;
  const picturePath = (payload as { picturePath?: unknown }).picturePath;
  const appPictureId = (payload as { appPictureId?: unknown }).appPictureId;

  return (
    result === "success" &&
    typeof picturePath === "string" &&
    picturePath.trim().length > 0 &&
    (typeof appPictureId === "number" || typeof appPictureId === "string")
  );
}

function isUpload502Response(response: { status: () => number; url: () => string }) {
  return response.url().includes("/api/picture/picture/uploadPictureFile") && response.status() === 502;
}

async function hasImmediateLogin(page: Page) {
  const primaryButton = page.getByRole("button", { name: immediateLoginText });
  if ((await primaryButton.count()) > 0) {
    return await primaryButton.first().isVisible().catch(() => false);
  }

  const fallbackButton = page
    .locator("button, a, [role='button'], .el-button")
    .filter({ hasText: immediateLoginText });

  if ((await fallbackButton.count()) > 0) {
    return await fallbackButton.first().isVisible().catch(() => false);
  }

  return false;
}

async function waitForManualLoginToComplete(page: Page, timeoutMs = 10 * 60_000) {
  const startedAt = Date.now();
  let lastHeartbeatAt = 0;

  logMessage("等待手动登录完成，请在已打开的浏览器窗口完成验证码/登录", {
    currentUrl: page.url(),
  });
  emitEvent("login_waiting", { currentUrl: page.url() });

  while (Date.now() - startedAt < timeoutMs) {
    await waitForBlockingLayersToClear(page);

    const stillOnLogin = await hasImmediateLogin(page).catch(() => false);
    if (!stillOnLogin) {
      try {
        await openCollectBoxItemsPage(page);
        logMessage("检测到妙手登录完成，继续执行批量上架。", {
          currentUrl: page.url(),
        });
        emitEvent("login_completed", { currentUrl: page.url() });
        return;
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        logMessage("登录入口已消失，正在等待页面跳转完成。", {
          currentUrl: page.url(),
          detail: message,
        });
      }
    }

    if (Date.now() - lastHeartbeatAt > 10_000) {
      lastHeartbeatAt = Date.now();
      emitEvent("login_waiting", {
        currentUrl: page.url(),
        elapsedSeconds: Math.round((Date.now() - startedAt) / 1000),
      });
    }

    await page.waitForTimeout(2_000);
  }

  throw new Error("登录等待超时，请确认妙手登录已完成后重新执行批量上架。");
}

function formatElapsedTime(milliseconds: number) {
  const totalSeconds = Math.max(0, Math.round(milliseconds / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  if (hours > 0) {
    return `${hours}h ${String(minutes).padStart(2, "0")}m ${String(seconds).padStart(2, "0")}s`;
  }

  if (minutes > 0) {
    return `${minutes}m ${String(seconds).padStart(2, "0")}s`;
  }

  return `${seconds}s`;
}

async function waitForBlockingLayersToClear(page: Page) {
  await page.locator(".jx-loading-mask").waitFor({ state: "hidden", timeout: 15_000 }).catch(() => {});
  await page.waitForTimeout(300);
}

async function setInputValueLowConflict(input: Locator, value: string) {
  await input.evaluate((element) => {
    const target = element as HTMLInputElement | HTMLTextAreaElement;
    target.focus();
    target.value = "";
    target.dispatchEvent(new Event("input", { bubbles: true }));
    target.dispatchEvent(new Event("change", { bubbles: true }));
  });

  await input.pressSequentially(value, { delay: 40 });
}

async function setEditableValueLowConflict(target: Locator, value: string) {
  await target.evaluate((element, nextValue) => {
    const editable = element as HTMLElement;
    editable.focus();

    if (editable instanceof HTMLInputElement || editable instanceof HTMLTextAreaElement) {
      editable.value = "";
      editable.dispatchEvent(new Event("input", { bubbles: true }));
      editable.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }

    editable.textContent = "";
    editable.dispatchEvent(new InputEvent("input", { bubbles: true, data: "" }));
  }, value);

  await target.pressSequentially(value, { delay: 40 });
}

async function clearEditableValueLowConflict(target: Locator) {
  await target.evaluate((element) => {
    const editable = element as HTMLElement;
    editable.focus();

    if (editable instanceof HTMLInputElement || editable instanceof HTMLTextAreaElement) {
      editable.value = "";
      editable.dispatchEvent(new Event("input", { bubbles: true }));
      editable.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }

    editable.textContent = "";
    editable.dispatchEvent(new InputEvent("input", { bubbles: true, data: "" }));
  });
}

async function verifyEditableValue(target: Locator, expectedValue: string) {
  return await target.evaluate((element, expected) => {
    if (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) {
      return (element.value || "").trim() === expected;
    }

    return (element.textContent || "").trim() === expected;
  }, expectedValue);
}

async function waitForEditableValueStable(target: Locator, expectedValue: string, timeoutMs = 5_000) {
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    const matched = await verifyEditableValue(target, expectedValue).catch(() => false);
    if (matched) {
      await new Promise((resolve) => setTimeout(resolve, 300));
      const stillMatched = await verifyEditableValue(target, expectedValue).catch(() => false);
      if (stillMatched) {
        return true;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, 200));
  }

  return false;
}

async function clickLocatorLowConflict(locator: Locator, page: Page) {
  await locator.scrollIntoViewIfNeeded().catch(() => {});
  await waitForBlockingLayersToClear(page);

  try {
    await locator.click({ timeout: 2_000 });
    return;
  } catch {
    await locator.evaluate((element) => {
      const target = element as HTMLElement;
      target.focus?.();
      target.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
      target.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
      target.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
      target.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
      target.click();
    });
  }
}

async function clickFirstVisibleLocator(candidates: Locator[], errorMessage: string) {
  for (const locator of candidates) {
    if ((await locator.count()) === 0) {
      continue;
    }

    await locator.waitFor({ state: "visible", timeout: 30_000 }).catch(() => {});
    if (!(await locator.isVisible().catch(() => false))) {
      continue;
    }

    await locator.click();
    return;
  }

  throw new Error(errorMessage);
}

async function clickVisibleButtonByName(page: Page, name: string) {
  await clickFirstVisibleLocator(
    [
      page.locator("button").filter({ has: page.locator(`span:text-is("${name}")`) }).first(),
      page.getByRole("button", { name }).first(),
      page.locator("button, [role='button']").filter({ hasText: name }).first(),
    ],
    `Could not find a visible button named '${name}'.`,
  );
}

async function waitForVisibleButtonByName(page: Page, name: string, timeoutMs = 30_000) {
  const candidates = [
    page.locator("button").filter({ has: page.locator(`span:text-is("${name}")`) }).first(),
    page.getByRole("button", { name }).first(),
    page.locator("button, [role='button']").filter({ hasText: name }).first(),
  ];

  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    for (const locator of candidates) {
      if ((await locator.count()) === 0) {
        continue;
      }

      await locator.waitFor({ state: "visible", timeout: 1_000 }).catch(() => {});
      if (await locator.isVisible().catch(() => false)) {
        return locator;
      }
    }

    await page.waitForTimeout(200);
  }

  throw new Error(`Timed out waiting for a visible button named '${name}'.`);
}

async function clickVisibleFullscreenButton(page: Page) {
  await clickFirstVisibleLocator(
    [
      page.locator(".shopee-icon-fullscreen-expand.pro-icon").first(),
      page.locator("[class*='shopee-icon-fullscreen-expand'][class*='pro-icon']").first(),
      page.getByRole("button", { name: fullscreenText }).first(),
      page.locator(`[aria-label*='${fullscreenText}'],[title*='${fullscreenText}']`).first(),
      page.locator("button, span, i").filter({ hasText: fullscreenText }).first(),
    ],
    "Could not find a visible fullscreen button in the dialog.",
  );
}

async function clickShopDropdown(page: Page) {
  const candidates = [
    page.locator(".shop-form-item div.jx-cascader__tags").first(),
    page
      .locator("div")
      .filter({
        has: page.locator(`span:text-is("${shopText}")`),
        hasText: shopText,
      })
      .locator("div.jx-cascader__tags")
      .first(),
    page.locator(
      `xpath=//div[.//span[normalize-space(text())='${shopText}']]//div[contains(@class,'jx-cascader__tags')][1]`,
    ),
    page.locator("div.jx-cascader__tags").first(),
  ];

  for (const locator of candidates) {
    if ((await locator.count()) === 0) {
      continue;
    }

    await locator.waitFor({ state: "visible", timeout: 10_000 }).catch(() => {});
    if (!(await locator.isVisible().catch(() => false))) {
      continue;
    }

    await locator.scrollIntoViewIfNeeded().catch(() => {});

    try {
      await locator.click({ timeout: 2_000 });
      return;
    } catch {
      const clicked = await locator.evaluate((element) => {
        const target = element as HTMLElement;
        target.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
        target.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
        target.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
        target.click();
        return true;
      });

      if (clicked) {
        await page.waitForTimeout(300);
        return;
      }
    }
  }

  throw new Error("Could not find the shop dropdown.");
}

async function captureShopDebugState(page: Page, name: string) {
  const artifactDir = path.resolve(__dirname, "..", "output");
  const screenshotPath = path.resolve(artifactDir, name);
  await page.screenshot({ path: screenshotPath, fullPage: true }).catch(() => {});

  const visibleOverlayClasses = await page.locator(".jx-overlay:visible").evaluateAll((nodes) =>
    nodes.map((node) => (node as HTMLElement).className),
  );

  console.log(`Saved debug screenshot: ${screenshotPath}`);
  console.log(`Visible overlays: ${JSON.stringify(visibleOverlayClasses)}`);
}

async function clickShopSelectorCheckAll(page: Page) {
  await page.waitForTimeout(500);

  const result = await page.evaluate(
    ({ missingText, successText }) => {
      const candidates = Array.from(document.querySelectorAll("label.shop-selector-check-all"));
      const checkAll = candidates.find((node) => {
        const element = node as HTMLElement;
        const rect = element.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
      }) as HTMLElement | undefined;

      if (!checkAll) {
        return missingText;
      }

      const beforeClass = checkAll.className;
      const beforeAriaChecked = checkAll.getAttribute("aria-checked");
      const beforeChecked =
        checkAll.classList.contains("is-checked") ||
        checkAll.querySelector(".is-checked") !== null ||
        (checkAll.querySelector("input") as HTMLInputElement | null)?.checked === true;

      checkAll.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
      checkAll.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
      checkAll.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
      checkAll.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
      checkAll.click();

      const afterClass = checkAll.className;
      const afterAriaChecked = checkAll.getAttribute("aria-checked");
      const afterChecked =
        checkAll.classList.contains("is-checked") ||
        checkAll.querySelector(".is-checked") !== null ||
        (checkAll.querySelector("input") as HTMLInputElement | null)?.checked === true;

      const changed =
        beforeClass !== afterClass ||
        beforeAriaChecked !== afterAriaChecked ||
        beforeChecked !== afterChecked;

      return changed ? successText : missingText;
    },
    {
      missingText: checkAllMissingText,
      successText: checkAllSuccessText,
    },
  );

  if (result === checkAllSuccessText) {
    console.log(result);
    return;
  }

  await captureShopDebugState(page, "miaoshou-shop-checkall-missing.png");
  console.log("Did not find label.shop-selector-check-all. Saved a debug screenshot and continued.");
}

async function selectProductCategory(page: Page) {
  const categorySelector = page.locator("div.jx-cascader.category-selector").first();
  await categorySelector.waitFor({ state: "visible", timeout: 30_000 });
  await categorySelector.click();

  const inputCandidates = [
    categorySelector.locator("input").first(),
    page.locator("div.jx-cascader.category-selector input").first(),
    page.locator("input").last(),
  ];

  let categoryInput: Locator | null = null;
  for (const locator of inputCandidates) {
    if ((await locator.count()) > 0) {
      await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
      if (await locator.isVisible().catch(() => false)) {
        categoryInput = locator;
        break;
      }
    }
  }

  if (!categoryInput) {
    throw new Error("Could not find the category input inside div.jx-cascader.category-selector.");
  }

  await categoryInput.click();
  await setInputValueLowConflict(categoryInput, categoryPathText);
  await page.waitForTimeout(500);

  await clickFirstVisibleLocator(
    [
      page.locator("ul").filter({ hasText: categoryPathText }).first(),
      page.locator("li").filter({ hasText: categoryPathText }).first(),
      page.getByText(categoryPathText, { exact: true }).first(),
    ],
    `Could not find a visible category option matching '${categoryPathText}'.`,
  );

  console.log(`Selected category: ${categoryPathText}`);
}

async function waitForMaterialFormItem(page: Page) {
  await waitForFormItemSelectionBox(page, materialText);
}

async function findFormItemIndexByLabel(page: Page, labelText: string) {
  return await page.locator(".jx-form-item, .pro-form-item").evaluateAll((items, exactText) => {
    return items.findIndex((item) => {
      const label = item.querySelector(".jx-form-item__label");
      if (!label) {
        return false;
      }

      const labelSpans = Array.from(label.querySelectorAll("span"));
      return labelSpans.some((span) => (span.textContent || "").trim() === exactText);
    });
  }, labelText);
}

async function waitForFormItemSelectionBox(page: Page, labelText: string) {
  await waitForBlockingLayersToClear(page);

  const start = Date.now();
  while (Date.now() - start < 15_000) {
    const formItemIndex = await findFormItemIndexByLabel(page, labelText);
    if (formItemIndex >= 0) {
      const formItem = page.locator(".jx-form-item, .pro-form-item").nth(formItemIndex);
      const contentBox = formItem.locator(".jx-form-item__content .content-box").first();
      if ((await contentBox.count()) > 0) {
        await contentBox.waitFor({ state: "visible", timeout: 1_000 }).catch(() => {});
        if (await contentBox.isVisible().catch(() => false)) {
          await page.waitForTimeout(800);
          return;
        }
      }
    }

    if (formItemIndex >= 0) {
      await page.waitForTimeout(800);
    }

    await page.waitForTimeout(300);
  }

  await captureShopDebugState(page, `miaoshou-${labelText}-form-item-missing.png`);
  throw new Error(`The '${labelText}' form item did not appear after category selection.`);
}

async function waitForFormItemInput(page: Page, labelText: string) {
  await waitForBlockingLayersToClear(page);

  const start = Date.now();
  while (Date.now() - start < 15_000) {
    const formItemIndex = await findFormItemIndexByLabel(page, labelText);
    if (formItemIndex >= 0) {
      const formItem = page.locator(".jx-form-item, .pro-form-item").nth(formItemIndex);
      const inputCandidates = [
        formItem.locator(".jx-form-item__content input").first(),
        formItem.locator(".jx-form-item__content textarea").first(),
        formItem.locator(".jx-form-item__content .jx-input__wrapper").first(),
        formItem.locator(".jx-form-item__content .el-input__inner").first(),
      ];

      for (const inputLocator of inputCandidates) {
        if ((await inputLocator.count()) === 0) {
          continue;
        }

        await inputLocator.waitFor({ state: "visible", timeout: 1_000 }).catch(() => {});
        if (await inputLocator.isVisible().catch(() => false)) {
          await page.waitForTimeout(500);
          return;
        }
      }
    }

    await page.waitForTimeout(300);
  }

  await captureShopDebugState(page, `miaoshou-${labelText}-input-missing.png`);
  throw new Error(`The '${labelText}' input did not appear after previous steps.`);
}

async function findFormItemByLabel(page: Page, labelText: string) {
  const formItemIndex = await findFormItemIndexByLabel(page, labelText);
  if (formItemIndex < 0) {
    return null;
  }

  const locator = page.locator(".jx-form-item, .pro-form-item").nth(formItemIndex);
  await locator.waitFor({ state: "visible", timeout: 10_000 }).catch(() => {});
  if (await locator.isVisible().catch(() => false)) {
    return locator;
  }

  return null;
}

async function selectFormItemOption(page: Page, labelText: string, optionText: string) {
  await waitForFormItemSelectionBox(page, labelText);

  const formItem = await findFormItemByLabel(page, labelText);
  if (!formItem) {
    throw new Error(`Could not find the form item anchored by '${labelText}'.`);
  }

  const selectionCandidates = [
    formItem.locator(".jx-form-item__content .content-box").first(),
    formItem.locator(".jx-form-item__content .jx-select.jx-select--small.pro-select").first(),
    formItem.locator(".jx-form-item__content .pro-select").first(),
  ];

  let selection: Locator | null = null;
  for (const locator of selectionCandidates) {
    if ((await locator.count()) > 0) {
      await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
      if (await locator.isVisible().catch(() => false)) {
        selection = locator;
        break;
      }
    }
  }

  if (!selection) {
    throw new Error(`Could not find '.content-box' inside the '${labelText}' form item content.`);
  }

  await clickLocatorLowConflict(selection, page);
  await page.waitForTimeout(300);

  const inputCandidates = [
    formItem.locator(".jx-form-item__content .content-box input").first(),
    formItem.locator(".jx-form-item__content .content-box .el-input__inner").first(),
    formItem.locator(".jx-form-item__content input").first(),
    formItem.locator("input").first(),
  ];

  let input: Locator | null = null;
  for (const locator of inputCandidates) {
    if ((await locator.count()) > 0) {
      await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
      if (await locator.isVisible().catch(() => false)) {
        input = locator;
        break;
      }
    }
  }

  if (!input) {
    throw new Error(`Could not find the input under '${labelText}'.`);
  }

  await clickLocatorLowConflict(input, page);

  await input.evaluate((element) => {
    const target = element as HTMLInputElement;
    target.focus();
  });

  await setInputValueLowConflict(input, optionText);
  await page.waitForTimeout(500);

  const optionClicked = await page.evaluate(
    ({ exactText }) => {
      const candidates = Array.from(
        document.querySelectorAll("li, ul li, [role='option'], .jx-select-dropdown__item"),
      );
      const visibleCandidate = candidates.find((node) => {
        const element = node as HTMLElement;
        const text = (element.innerText || element.textContent || "").trim();
        const rect = element.getBoundingClientRect();
        return text === exactText && rect.width > 0 && rect.height > 0;
      }) as HTMLElement | undefined;

      if (!visibleCandidate) {
        return false;
      }

      visibleCandidate.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
      visibleCandidate.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
      visibleCandidate.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
      visibleCandidate.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
      visibleCandidate.click();
      return true;
    },
    { exactText: optionText },
  );

  if (optionClicked) {
    console.log(`Selected ${labelText}: ${optionText}`);
    return true;
  }

  await captureShopDebugState(page, `miaoshou-${labelText}-option-missing.png`);
  console.log(`Did not find a visible option matching '${optionText}' for '${labelText}'. Saved a debug screenshot and continued.`);
  return false;
}

async function selectMaterial(page: Page) {
  return await selectFormItemOption(page, materialText, materialValueText);
}

async function selectBackingMaterial(page: Page) {
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(800);
  return await selectFormItemOption(page, backingMaterialText, backingMaterialValueText);
}

async function clickMoreAttributes(page: Page) {
  await clickVisibleButtonByName(page, moreAttributesText);
  console.log(`Clicked ${moreAttributesText}`);
  await page.waitForTimeout(500);
}

async function selectSurfacePattern(page: Page) {
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(500);
  return await selectFormItemOption(page, surfacePatternText, surfacePatternValueText);
}

async function inputFormItemValue(page: Page, labelText: string, value: string) {
  await waitForFormItemInput(page, labelText);

  const formItem = await findFormItemByLabel(page, labelText);
  if (!formItem) {
    throw new Error(`Could not find the form item anchored by '${labelText}'.`);
  }

  const inputCandidates = [
    formItem.locator(".jx-form-item__content .jx-input__wrapper input").first(),
    formItem.locator(".jx-form-item__content .jx-input__wrapper textarea").first(),
    formItem.locator(".jx-form-item__content .jx-input__wrapper [contenteditable='true']").first(),
    formItem.locator(".jx-form-item__content input").first(),
    formItem.locator(".jx-form-item__content textarea").first(),
    formItem.locator(".jx-form-item__content .el-input__inner").first(),
    formItem.locator(".jx-form-item__content [contenteditable='true']").first(),
  ];

  let input: Locator | null = null;
  for (const locator of inputCandidates) {
    if ((await locator.count()) > 0) {
      await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
      if (await locator.isVisible().catch(() => false)) {
        input = locator;
        break;
      }
    }
  }

  if (!input) {
    throw new Error(`Could not find the input under '${labelText}'.`);
  }

  await clickLocatorLowConflict(input, page);
  await input.evaluate((element) => {
    const target = element as HTMLInputElement | HTMLTextAreaElement;
    target.focus();
  });

  await setEditableValueLowConflict(input, value);
  const verified = await waitForEditableValueStable(input, value);
  if (!verified) {
    await captureShopDebugState(page, `miaoshou-${labelText}-input-not-applied.png`);
    throw new Error(`Input '${labelText}' did not retain the expected value '${value}'.`);
  }

  await page.waitForTimeout(300);
  console.log(`Input ${labelText}: ${value}`);
}

async function inputThickness(page: Page) {
  await inputFormItemValue(page, thicknessText, thicknessValueText);
}

async function selectLeatherType(page: Page) {
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(500);
  return await selectFormItemOption(page, leatherTypeText, leatherTypeValueText);
}

async function inputProductTitle(page: Page, titleValue: string) {
  await inputTitleFieldFromScrollPane(page, productTitleText, titleValue);
}

async function inputEnglishTitle(page: Page, titleValue: string) {
  await inputTitleFieldFromScrollPane(page, englishTitleText, titleValue);
}

async function selectOrigin(page: Page) {
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(500);

  const productInfoPane = await getProductInfoPane(page);
  const originFormItem = await findFormItemByLabelInScope(productInfoPane, originText);
  if (!originFormItem) {
    await captureShopDebugState(page, "miaoshou-origin-form-item-missing.png");
    throw new Error(`Could not find the '${originText}' form item inside the '${productInfoNavText}' pane.`);
  }

  const originBoxCandidates = [
    originFormItem.locator(".jx-form-item__content .country-item-box").first(),
    originFormItem.locator(".jx-form-item__content .product-origin-country-select").first(),
    originFormItem.locator(".jx-form-item__content .jx-cascader").first(),
    originFormItem.locator(".jx-form-item__content .jx-input__wrapper").first(),
  ];

  let originBox: Locator | null = null;
  for (const locator of originBoxCandidates) {
    if ((await locator.count()) > 0) {
      await locator.waitFor({ state: "visible", timeout: 10_000 }).catch(() => {});
      if (await locator.isVisible().catch(() => false)) {
        originBox = locator;
        break;
      }
    }
  }

  if (!originBox) {
    await captureShopDebugState(page, "miaoshou-origin-box-missing.png");
    throw new Error(`Could not find the origin country select box for '${originText}'.`);
  }

  await clickLocatorLowConflict(originBox, page);
  await page.waitForTimeout(300);

  const originInputCandidates = [
    originBox.locator("input.jx-input__inner").first(),
    originFormItem.locator(".jx-form-item__content input.jx-input__inner").first(),
    originFormItem.locator(".jx-form-item__content input").first(),
  ];

  let originInput: Locator | null = null;
  for (const locator of originInputCandidates) {
    if ((await locator.count()) > 0) {
      await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
      if (await locator.isVisible().catch(() => false)) {
        originInput = locator;
        break;
      }
    }
  }

  if (!originInput) {
    await captureShopDebugState(page, "miaoshou-origin-input-missing.png");
    throw new Error(`Could not find the origin input for '${originText}'.`);
  }

  await clickLocatorLowConflict(originInput, page);
  await originInput.evaluate((element) => {
    const target = element as HTMLInputElement;
    target.focus();
  });
  await setInputValueLowConflict(originInput, originValueText);
  await page.waitForTimeout(500);

  const originOptionClicked = await page.evaluate(
    ({ exactText }) => {
      const candidates = Array.from(
        document.querySelectorAll("li, ul li, [role='option'], .jx-cascader__menu-item, .jx-select-dropdown__item"),
      );
      const visibleCandidate = candidates.find((node) => {
        const element = node as HTMLElement;
        const text = (element.innerText || element.textContent || "").trim();
        const rect = element.getBoundingClientRect();
        return text === exactText && rect.width > 0 && rect.height > 0;
      }) as HTMLElement | undefined;

      if (!visibleCandidate) {
        return false;
      }

      visibleCandidate.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
      visibleCandidate.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
      visibleCandidate.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
      visibleCandidate.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
      visibleCandidate.click();
      return true;
    },
    { exactText: originValueText },
  );

  if (originOptionClicked) {
    console.log(`Selected ${originText}: ${originValueText}`);
    return true;
  }

  await captureShopDebugState(page, "miaoshou-origin-option-missing.png");
  console.log(`Did not find a visible option matching '${originValueText}' for '${originText}'. Saved a debug screenshot and continued.`);
  return false;
}

async function clickProductInfoNav(page: Page) {
  await clickFirstVisibleLocator(
    [
      page.locator(".scroll-menu-nav__item").filter({ hasText: productInfoNavText }).first(),
      page.getByText(productInfoNavText, { exact: true }).first(),
      page.locator(".scroll-menu.is-horizontal").getByText(productInfoNavText, { exact: true }).first(),
    ],
    `Could not find the '${productInfoNavText}' navigation item.`,
  );

  console.log(`Clicked ${productInfoNavText}`);
  await page.waitForTimeout(500);
}

async function clickSalesAttributesNav(page: Page) {
  await clickFirstVisibleLocator(
    [
      page.locator(".scroll-menu-nav__item").filter({ hasText: salesAttributesNavText }).first(),
      page.getByText(salesAttributesNavText, { exact: true }).first(),
      page.locator(".scroll-menu.is-horizontal").getByText(salesAttributesNavText, { exact: true }).first(),
    ],
    `Could not find the '${salesAttributesNavText}' navigation item.`,
  );

  console.log(`Clicked ${salesAttributesNavText}`);
  await page.waitForTimeout(500);
}

async function clickPackagingInfoNav(page: Page) {
  await clickFirstVisibleLocator(
    [
      page.locator(".scroll-menu-nav__item").filter({ hasText: packagingInfoNavText }).first(),
      page.getByText(packagingInfoNavText, { exact: true }).first(),
      page.locator(".scroll-menu.is-horizontal").getByText(packagingInfoNavText, { exact: true }).first(),
    ],
    `Could not find the '${packagingInfoNavText}' navigation item.`,
  );

  console.log(`Clicked ${packagingInfoNavText}`);
  await page.waitForTimeout(500);
}

async function getPackagingInfoPane(page: Page) {
  const pane = page
    .locator(".scroll-menu-pane")
    .filter({
      has: page.locator(".scroll-menu-pane__label").filter({ hasText: packagingInfoNavText }),
    })
    .first();

  if ((await pane.count()) === 0) {
    await captureShopDebugState(page, "miaoshou-packaging-info-pane-missing.png");
    throw new Error(`Could not find the '${packagingInfoNavText}' scroll pane.`);
  }

  await pane.waitFor({ state: "visible", timeout: 15_000 }).catch(() => {});
  if (!(await pane.isVisible().catch(() => false))) {
    await captureShopDebugState(page, "miaoshou-packaging-info-pane-hidden.png");
    throw new Error(`The '${packagingInfoNavText}' scroll pane is not visible.`);
  }

  return pane;
}

async function getProductInfoPane(page: Page) {
  const pane = page
    .locator(".scroll-menu-pane")
    .filter({
      has: page.locator(".scroll-menu-pane__label").filter({ hasText: productInfoNavText }),
    })
    .first();

  if ((await pane.count()) === 0) {
    await captureShopDebugState(page, "miaoshou-product-info-pane-missing.png");
    throw new Error(`Could not find the '${productInfoNavText}' scroll pane.`);
  }

  await pane.waitFor({ state: "visible", timeout: 15_000 }).catch(() => {});
  if (!(await pane.isVisible().catch(() => false))) {
    await captureShopDebugState(page, "miaoshou-product-info-pane-hidden.png");
    throw new Error(`The '${productInfoNavText}' scroll pane is not visible.`);
  }

  return pane;
}

async function findFormItemByLabelInScope(scope: Page | Locator, labelText: string) {
  const formItems = scope.locator(".jx-form-item, .pro-form-item");
  const count = await formItems.count();

  for (let index = 0; index < count; index += 1) {
    const formItem = formItems.nth(index);
    const isMatch = await formItem
      .locator(".jx-form-item__label span")
      .evaluateAll(
        (spans, expectedText) =>
          spans.some((span) => (span.textContent || "").trim() === expectedText),
        labelText,
      )
      .catch(() => false);

    if (!isMatch) {
      continue;
    }

    await formItem.waitFor({ state: "visible", timeout: 3_000 }).catch(() => {});
    if (await formItem.isVisible().catch(() => false)) {
      return formItem;
    }
  }

  return null;
}

async function selectFormItemOptionInScope(page: Page, scope: Locator, labelText: string, optionText: string) {
  const formItem = await findFormItemByLabelInScope(scope, labelText);
  if (!formItem) {
    await captureShopDebugState(page, `miaoshou-${labelText}-scoped-form-item-missing.png`);
    throw new Error(`Could not find '${labelText}' inside the current pane.`);
  }

  const selectionCandidates = [
    formItem.locator(".jx-form-item__content .content-box").first(),
    formItem.locator(".jx-form-item__content .jx-select").first(),
    formItem.locator(".jx-form-item__content .pro-select").first(),
    formItem.locator(".jx-form-item__content .jx-input__wrapper").first(),
  ];

  let selection: Locator | null = null;
  for (const locator of selectionCandidates) {
    if ((await locator.count()) === 0) {
      continue;
    }

    await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
    if (await locator.isVisible().catch(() => false)) {
      selection = locator;
      break;
    }
  }

  if (!selection) {
    await captureShopDebugState(page, `miaoshou-${labelText}-selection-missing.png`);
    throw new Error(`Could not find a selection box for '${labelText}'.`);
  }

  await clickLocatorLowConflict(selection, page);
  await page.waitForTimeout(300);

  const input = formItem.locator(".jx-form-item__content input").first();
  if ((await input.count()) > 0 && (await input.isVisible().catch(() => false))) {
    await clickLocatorLowConflict(input, page);
    await setInputValueLowConflict(input, optionText);
    await page.waitForTimeout(300);
  }

  const optionClicked = await page.evaluate((exactText) => {
    const candidates = Array.from(document.querySelectorAll("li, ul li, [role='option'], .jx-select-dropdown__item"));
    const visibleCandidate = candidates.find((node) => {
      const element = node as HTMLElement;
      const text = (element.innerText || element.textContent || "").trim();
      const rect = element.getBoundingClientRect();
      return text === exactText && rect.width > 0 && rect.height > 0;
    }) as HTMLElement | undefined;

    if (!visibleCandidate) {
      return false;
    }

    visibleCandidate.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
    visibleCandidate.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
    visibleCandidate.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
    visibleCandidate.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
    visibleCandidate.click();
    return true;
  }, optionText);

  if (!optionClicked) {
    await captureShopDebugState(page, `miaoshou-${labelText}-option-missing.png`);
    throw new Error(`Could not select option '${optionText}' for '${labelText}'.`);
  }

  console.log(`Selected ${labelText}: ${optionText}`);
  await page.waitForTimeout(400);
}

async function inputTitleFieldFromScrollPane(page: Page, labelText: string, titleValue: string) {
  await waitForBlockingLayersToClear(page);
  const productInfoPane = await getProductInfoPane(page);
  const formItem = await findFormItemByLabelInScope(productInfoPane, labelText);

  if (!formItem) {
    await captureShopDebugState(page, `miaoshou-${labelText}-scroll-pane-missing.png`);
    throw new Error(`Could not find '${labelText}' inside the '${productInfoNavText}' pane.`);
  }

  const wrapperCandidates = [
    formItem.locator(".jx-form-item__content .jx-input__wrapper").first(),
    formItem.locator(".jx-form-item__content .ban-words-wrapper .content .chat-gpt-panel.is-title").first(),
    formItem.locator(".jx-form-item__content .ban-words-wrapper").first(),
    formItem.locator(".jx-form-item__content .pro-input").first(),
    formItem.locator(".jx-form-item__content .jx-input").first(),
    formItem.locator(".jx-form-item__content input.jx-input__inner").first(),
  ];

  let wrapper: Locator | null = null;
  for (const locator of wrapperCandidates) {
    if ((await locator.count()) === 0) {
      continue;
    }

    await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
    if (await locator.isVisible().catch(() => false)) {
      wrapper = locator;
      break;
    }
  }

  if (!wrapper) {
    await captureShopDebugState(page, `miaoshou-${labelText}-wrapper-missing.png`);
    throw new Error(`Could not find the title wrapper for '${labelText}'.`);
  }

  await clickLocatorLowConflict(wrapper, page);
  await page.waitForTimeout(300);

  const inputCandidates = [
    wrapper.locator("input.jx-input__inner").first(),
    wrapper.locator("input").first(),
    wrapper.locator("textarea").first(),
    wrapper.locator("[contenteditable='true']").first(),
    formItem.locator(".jx-form-item__content input.jx-input__inner").first(),
    formItem.locator(".jx-form-item__content input").first(),
    formItem.locator(".jx-form-item__content textarea").first(),
    formItem.locator(".jx-form-item__content [contenteditable='true']").first(),
  ];

  let input: Locator | null = null;
  for (const locator of inputCandidates) {
    if ((await locator.count()) === 0) {
      continue;
    }

    await locator.waitFor({ state: "visible", timeout: 3_000 }).catch(() => {});
    if (await locator.isVisible().catch(() => false)) {
      input = locator;
      break;
    }
  }

  if (!input) {
    await captureShopDebugState(page, `miaoshou-${labelText}-input-missing.png`);
    throw new Error(`Could not find the input node for '${labelText}'.`);
  }

  await clickLocatorLowConflict(input, page);
  await input.evaluate((element) => {
    const target = element as HTMLInputElement | HTMLTextAreaElement | HTMLElement;
    target.focus();
  });

  await setEditableValueLowConflict(input, titleValue);
  const verified = await waitForEditableValueStable(input, titleValue);
  if (!verified) {
    await captureShopDebugState(page, `miaoshou-${labelText}-input-not-applied.png`);
    throw new Error(`Input '${labelText}' did not retain the expected value '${titleValue}'.`);
  }

  await page.waitForTimeout(500);
  console.log(`Input ${labelText}: ${titleValue}`);
}

async function openCollectBoxItemsPage(page: Page) {
  if (page.url().includes("/pddkj/collect_box/items")) {
    await waitForBlockingLayersToClear(page);
    try {
      await waitForVisibleButtonByName(page, createProductText, 3_000);
      await page.waitForTimeout(300);
      console.log(`Already opened ${collectBoxItemsUrl}`);
      return;
    } catch {
      // The URL is correct but the page is not ready, reload it below.
    }
  }

  await page.goto(collectBoxItemsUrl, { waitUntil: "domcontentloaded", timeout: 60_000 });
  await waitForBlockingLayersToClear(page);
  await waitForVisibleButtonByName(page, createProductText, 20_000);
  await page.waitForTimeout(500);

  console.log(`Opened ${collectBoxItemsUrl}`);
}

async function closeTopVisibleDialogByHeaderButton(page: Page) {
  const closed = await page.evaluate(() => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    const visibleDialogs = dialogs.filter((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      return style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
    });

    const topDialog = visibleDialogs[visibleDialogs.length - 1];
    if (!topDialog) {
      return false;
    }

    const closeButton = topDialog.querySelector('button.jx-dialog__headerbtn[aria-label="鍏抽棴姝ゅ璇濇"]') as
      | HTMLButtonElement
      | null;
    if (!closeButton) {
      return false;
    }

    closeButton.click();
    return true;
  });

  if (closed) {
    await page.waitForTimeout(500);
    await waitForBlockingLayersToClear(page);
  }

  return closed;
}

async function closeTopVisibleDialogByHeaderButtonStable(page: Page) {
  const closed = await page.evaluate(() => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    const visibleDialogs = dialogs.filter((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      return style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
    });

    const topDialog = visibleDialogs[visibleDialogs.length - 1];
    if (!topDialog) {
      return false;
    }

    const closeButton = topDialog.querySelector("button.jx-dialog__headerbtn") as HTMLButtonElement | null;
    if (!closeButton) {
      return false;
    }

    closeButton.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
    closeButton.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
    closeButton.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
    closeButton.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
    closeButton.click();
    return true;
  });

  if (closed) {
    await page.waitForTimeout(600);
    await waitForBlockingLayersToClear(page);
  }

  return closed;
}

async function getMainCarouselUploadedImageCount(pictureList: Locator) {
  return await pictureList.evaluate((element) => {
    const items = Array.from(element.querySelectorAll(".picture-draggable-list .product-picture-item"));
    return items.filter((item) => {
      const image = item.querySelector(".jx-image");
      const addButton = item.querySelector(".product-picture-item-add");
      const imageSrc = (item.querySelector("img") as HTMLImageElement | null)?.src || "";
      return image !== null && (addButton === null || imageSrc.length > 0);
    }).length;
  }).catch(() => 0);
}

async function uploadMainCarouselImages(page: Page, productItem: ProductJsonItem) {
  const mainFileFolder = getMainFileFolder(productItem);
  const imageFiles = getImageFilesFromFolder(mainFileFolder);
  const expectedCount = imageFiles.length;

  console.log(`Main carousel source folder: ${mainFileFolder}`);
  console.log(`Main carousel images found: ${expectedCount}`);

  const pictureList = page.locator(".product-picture-list").first();
  await pictureList.waitFor({ state: "visible", timeout: 30_000 });

  const uploadButton = pictureList
    .locator("button")
    .filter({ has: page.locator(`span:text-is("${uploadImageText}")`) })
    .first();

  await uploadButton.waitFor({ state: "visible", timeout: 15_000 });
  await clickLocatorLowConflict(uploadButton, page);
  await page.waitForTimeout(500);

  const localUploadContainer = page.locator(".jx-popper .picture-selector-item.pro-upload").first();
  await localUploadContainer.waitFor({ state: "visible", timeout: 15_000 });

  const localUploadInput = localUploadContainer.locator(
    'input.jx-upload__input[name="uploadImgFile"][type="file"]',
  ).first();
  await localUploadInput.waitFor({ state: "attached", timeout: 15_000 });

  let uploadSucceeded = false;
  let uploadedImageCount = 0;
  let successCount = 0;

  for (let attempt = 1; attempt <= 3; attempt += 1) {
    const uploadResponses: Array<{ ok: boolean; payload: unknown }> = [];
    const responseListener = async (response: { url: () => string; ok: () => boolean; json: () => Promise<unknown>; status: () => number }) => {
      if (!response.url().includes("/api/picture/picture/uploadPictureFile")) {
        return;
      }

      const payload = await response.json().catch(() => null);
      uploadResponses.push({
        ok: response.ok() && isSuccessfulUploadResponse(payload),
        payload,
      });
    };
    page.on("response", responseListener);

    await localUploadInput.setInputFiles(imageFiles);
    await page.waitForTimeout(1_000);

    const start = Date.now();
    while (Date.now() - start < 120_000) {
      uploadedImageCount = await getMainCarouselUploadedImageCount(pictureList);
      successCount = uploadResponses.filter((item) => item.ok).length;
      if (uploadedImageCount >= expectedCount || successCount >= expectedCount) {
        break;
      }

      await page.waitForTimeout(1_000);
    }

    page.off("response", responseListener);

    successCount = uploadResponses.filter((item) => item.ok).length;
    uploadedImageCount = await getMainCarouselUploadedImageCount(pictureList);
    console.log(`Main carousel upload attempt ${attempt}/3 API success count: ${successCount}/${expectedCount}`);
    console.log(`Main carousel upload attempt ${attempt}/3 UI image count: ${uploadedImageCount}/${expectedCount}`);

    if (successCount === expectedCount || uploadedImageCount === expectedCount) {
      uploadSucceeded = true;
      break;
    }

    if (attempt < 3) {
      console.log(`Main carousel upload did not complete on attempt ${attempt}/3. Closing dialog and retrying.`);
      await closeTopVisibleDialogByHeaderButtonStable(page);
      await clickLocatorLowConflict(uploadButton, page);
      await page.waitForTimeout(500);

      const retryLocalUploadContainer = page.locator(".jx-popper .picture-selector-item.pro-upload").first();
      await retryLocalUploadContainer.waitFor({ state: "visible", timeout: 15_000 });

      const retryLocalUploadInput = retryLocalUploadContainer.locator(
        'input.jx-upload__input[name="uploadImgFile"][type="file"]',
      ).first();
      await retryLocalUploadInput.waitFor({ state: "attached", timeout: 15_000 });
      continue;
    }
  }

  console.log(`Main carousel final upload API success count: ${successCount}/${expectedCount}`);
  console.log(`Main carousel final uploaded image count in UI: ${uploadedImageCount}/${expectedCount}`);

  if (!uploadSucceeded) {
    await captureShopDebugState(page, "miaoshou-main-carousel-upload-mismatch.png");
    throw new Error(
      `Main carousel upload mismatch. Found ${expectedCount} local images in '${mainFileFolder}', but only ${successCount} upload API calls returned success and the UI only shows ${uploadedImageCount} uploaded images after retries.`,
    );
  }

  await page.waitForTimeout(2_000);
}

async function uploadOuterPackagingImage(page: Page, productItem: ProductJsonItem) {
  const firstMainImage = getImageFilesFromFolder(getMainFileFolder(productItem))[0];
  const packagingPane = await getPackagingInfoPane(page);

  console.log(`Outer packaging image source: ${firstMainImage}`);

  const uploadButtonCandidates = [
    packagingPane
      .locator("button")
      .filter({ has: page.locator(`span:text-is("${uploadImageText}")`) })
      .first(),
    packagingPane.locator(".product-picture-item-add").first(),
    packagingPane.locator(".picture-uploader, .picture-selector, .upload-box").first(),
  ];

  let uploadButton: Locator | null = null;
  for (const locator of uploadButtonCandidates) {
    if ((await locator.count()) === 0) {
      continue;
    }

    await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
    if (await locator.isVisible().catch(() => false)) {
      uploadButton = locator;
      break;
    }
  }

  if (!uploadButton) {
    await captureShopDebugState(page, "miaoshou-outer-packaging-upload-button-missing.png");
    throw new Error(`Could not find the ${uploadImageText} button inside ${packagingInfoNavText}.`);
  }

  let uploadSucceeded = false;
  for (let attempt = 1; attempt <= 3; attempt += 1) {
    await clickLocatorLowConflict(uploadButton, page);
    await page.waitForTimeout(500);

    const localUploadContainer = page.locator(".jx-popper .picture-selector-item.pro-upload").first();
    await localUploadContainer.waitFor({ state: "visible", timeout: 15_000 });

    const localUploadInput = page.locator('input.jx-upload__input[name="uploadImgFile"][type="file"]').last();
    await localUploadInput.waitFor({ state: "attached", timeout: 15_000 });

    const uploadResponsePromise = page.waitForResponse(
      (response) => response.url().includes("/api/picture/picture/uploadPictureFile"),
      { timeout: 120_000 },
    );

    await localUploadInput.setInputFiles(firstMainImage);
    const uploadResponse = await uploadResponsePromise.catch(() => null);
    const payload = uploadResponse ? await uploadResponse.json().catch(() => null) : null;
    uploadSucceeded = Boolean(uploadResponse?.ok() && isSuccessfulUploadResponse(payload));
    console.log(`Outer packaging image upload attempt ${attempt}/3 success: ${uploadSucceeded}`);

    if (uploadSucceeded) {
      break;
    }

    if (attempt < 3) {
      await closeTopVisibleDialogByHeaderButtonStable(page);
      await page.waitForTimeout(500);
    }
  }

  if (!uploadSucceeded) {
    await captureShopDebugState(page, "miaoshou-outer-packaging-upload-failed.png");
    throw new Error(`Outer packaging image upload failed after retries: ${firstMainImage}`);
  }

  await page.waitForTimeout(1_000);
}

async function getDescriptionEditorDialog(page: Page) {
  const dialog = page.locator(".jx-overlay-dialog").filter({ has: page.locator(".pddkj-description-editor-dialog") }).last();
  await dialog.waitFor({ state: "visible", timeout: 20_000 });
  return dialog;
}

async function getBatchImageUploadDialog(page: Page) {
  return await getVisibleDialogWithTitle(page, batchImageUploadDialogTitleText);
}

async function openDescriptionEditor(page: Page) {
  await clickFirstVisibleLocator(
    [
      page.locator("button").filter({ hasText: addDescriptionText }).first(),
      page.locator("button, span, div").filter({ hasText: `+ ${addDescriptionText}` }).first(),
      page.getByText(addDescriptionText, { exact: true }).first(),
    ],
    `Could not find the '${addDescriptionText}' button.`,
  );
  console.log(`Clicked ${addDescriptionText}`);
  await getDescriptionEditorDialog(page);
  await page.waitForTimeout(500);
}

async function openBatchImageUploadDialogFromDescription(page: Page) {
  const editorDialog = await getDescriptionEditorDialog(page);
  const batchOperationButton = editorDialog.locator("button").filter({ hasText: batchOperationText }).first();
  await batchOperationButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(batchOperationButton, page);
  console.log(`Clicked ${batchOperationText}`);
  await page.waitForTimeout(500);

  await clickFirstVisibleLocator(
    [
      page.locator(".jx-dropdown__popper, .jx-popper").locator("li, [role='menuitem'], .jx-dropdown-menu__item").filter({ hasText: batchImageProcessText }).first(),
      page.locator("li, [role='menuitem'], .jx-dropdown-menu__item").filter({ hasText: batchImageProcessText }).first(),
      page.getByText(batchImageProcessText, { exact: true }).first(),
    ],
    `Could not find the '${batchImageProcessText}' dropdown option.`,
  );
  console.log(`Clicked ${batchImageProcessText}`);
  await getBatchImageUploadDialog(page);
  await page.waitForTimeout(500);
}

async function saveTopDialogByPrimaryButton(page: Page, dialog: Locator, logText: string) {
  const clicked = await dialog.evaluate((element, targetText) => {
    const buttons = Array.from(element.querySelectorAll("button")) as HTMLButtonElement[];
    const visibleButtons = buttons.filter((button) => {
      const style = window.getComputedStyle(button);
      const rect = button.getBoundingClientRect();
      return (
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0 &&
        (button.textContent || "").replace(/\s+/g, "").includes(targetText)
      );
    });

    const targetButton = visibleButtons[visibleButtons.length - 1];
    if (!targetButton) {
      return false;
    }

    targetButton.scrollIntoView({ block: "center", inline: "nearest" });
    targetButton.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
    targetButton.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
    targetButton.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
    targetButton.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
    targetButton.click();
    return true;
  }, saveText);

  if (clicked) {
    console.log(logText);
    await page.waitForTimeout(1_000);
    return;
  }

  const saveButton = dialog.locator("button").filter({ hasText: saveText }).last();
  if ((await saveButton.count()) > 0) {
    await saveButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(saveButton, page);
    console.log(logText);
    await page.waitForTimeout(800);
    return;
  }

  const primaryButton = dialog.locator("button.jx-button--primary").last();
  await primaryButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(primaryButton, page);
  console.log(logText);
  await page.waitForTimeout(800);
}

async function getDescriptionBatchUploadedImageCount(batchDialog: Locator) {
  return await batchDialog.locator(".picture-draggable-list img, .product-picture-item img").count().catch(() => 0);
}

async function waitForDialogTitleToClose(page: Page, titleText: string, timeoutMs = 20_000) {
  await page.waitForFunction((expectedTitle) => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return !dialogs.some((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      const title = (dialog.querySelector(".jx-dialog__title")?.textContent || "").trim();
      return isVisible && title === expectedTitle;
    });
  }, titleText, { timeout: timeoutMs });
}

async function waitForDescriptionEditorDialogToClose(page: Page, timeoutMs = 20_000) {
  await page.waitForFunction(() => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return !dialogs.some((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      return isVisible && Boolean(dialog.querySelector(".pddkj-description-editor-dialog"));
    });
  }, undefined, { timeout: timeoutMs });
}

async function uploadDescriptionDetailImages(page: Page, productItem: ProductJsonItem) {
  const detailFileFolder = getDetailFileFolder(productItem);
  const detailImageFiles = getImageFilesFromFolder(detailFileFolder, {
    folderLabel: "detail_file_folder",
    maxCount: null,
  });
  const expectedCount = detailImageFiles.length;

  console.log(`Detail images source folder: ${detailFileFolder}`);
  console.log(`Detail images found: ${expectedCount}`);

  let uploadSucceeded = false;
  let lastSuccessCount = 0;
  let lastUiCount = 0;

  for (let attempt = 1; attempt <= 3; attempt += 1) {
    const batchDialog = await getBatchImageUploadDialog(page);
    const pictureList = batchDialog.locator(".product-picture-list").first();
    await pictureList.waitFor({ state: "visible", timeout: 20_000 });

    const uploadButtonCandidates = [
      pictureList
        .locator("button")
        .filter({ has: page.locator(`span:text-is("${uploadImageText}")`) })
        .first(),
      pictureList.locator("button").filter({ hasText: uploadImageText }).first(),
      pictureList.locator(".operation-bar button").nth(2),
    ];

    let uploadButton: Locator | null = null;
    for (const locator of uploadButtonCandidates) {
      if ((await locator.count()) === 0) {
        continue;
      }

      await locator.waitFor({ state: "visible", timeout: 5_000 }).catch(() => {});
      if (await locator.isVisible().catch(() => false)) {
        uploadButton = locator;
        break;
      }
    }

    if (!uploadButton) {
      await captureShopDebugState(page, "miaoshou-description-upload-button-missing.png");
      throw new Error(`Could not find the ${uploadImageText} button in the batch image upload dialog.`);
    }

    await clickLocatorLowConflict(uploadButton, page);
    await page.waitForTimeout(500);

    const localUploadContainer = page.locator(".jx-popper .picture-selector-item.pro-upload").first();
    await localUploadContainer.waitFor({ state: "visible", timeout: 15_000 });

    const localUploadInput = localUploadContainer.locator(
      'input.jx-upload__input[name="uploadImgFile"][type="file"]',
    ).first();
    await localUploadInput.waitFor({ state: "attached", timeout: 15_000 });

    const uploadResponses: Array<{ ok: boolean; status: number; payload: unknown }> = [];
    const responseListener = async (response: { url: () => string; ok: () => boolean; json: () => Promise<unknown>; status: () => number }) => {
      if (!response.url().includes("/api/picture/picture/uploadPictureFile")) {
        return;
      }

      const payload = await response.json().catch(() => null);
      uploadResponses.push({
        ok: response.ok() && isSuccessfulUploadResponse(payload),
        status: response.status(),
        payload,
      });
    };
    page.on("response", responseListener);

    await localUploadInput.setInputFiles(detailImageFiles);
    await page.waitForTimeout(1_000);

    const start = Date.now();
    while (Date.now() - start < 120_000) {
      lastSuccessCount = uploadResponses.filter((item) => item.ok).length;
      lastUiCount = await getDescriptionBatchUploadedImageCount(batchDialog);

      if (lastSuccessCount >= expectedCount || lastUiCount >= expectedCount) {
        break;
      }

      await page.waitForTimeout(1_000);
    }

    page.off("response", responseListener);

    lastSuccessCount = uploadResponses.filter((item) => item.ok).length;
    lastUiCount = await getDescriptionBatchUploadedImageCount(batchDialog);
    const hit502 = uploadResponses.some((item) => item.status === 502);

    console.log(`Description detail upload attempt ${attempt}/3 API success count: ${lastSuccessCount}/${expectedCount}`);
    console.log(`Description detail upload attempt ${attempt}/3 UI image count: ${lastUiCount}/${expectedCount}`);

    if ((lastSuccessCount >= expectedCount || lastUiCount >= expectedCount) && !hit502) {
      uploadSucceeded = true;
      break;
    }

    if (attempt < 3) {
      console.log(`Description detail upload retry ${attempt}/3. Hit 502: ${hit502}`);
      await closeTopVisibleDialogByHeaderButtonStable(page);
      await page.waitForTimeout(500);
      await openBatchImageUploadDialogFromDescription(page);
    }
  }

  if (!uploadSucceeded) {
    await captureShopDebugState(page, "miaoshou-description-detail-upload-failed.png");
    throw new Error(
      `Description detail upload mismatch. Found ${expectedCount} local images in '${detailFileFolder}', API success ${lastSuccessCount}, UI count ${lastUiCount}.`,
    );
  }

  const batchDialog = await getBatchImageUploadDialog(page);
  await saveTopDialogByPrimaryButton(page, batchDialog, "Clicked batch image upload dialog save");
  await waitForDialogTitleToClose(page, batchImageUploadDialogTitleText, 8_000).catch(async () => {
    const retryBatchDialog = await getBatchImageUploadDialog(page);
    const closeButton = retryBatchDialog.locator("button.jx-dialog__headerbtn").first();
    await closeButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(closeButton, page);
    console.log("Clicked batch image upload dialog header close after save");
    await waitForDialogTitleToClose(page, batchImageUploadDialogTitleText, 20_000);
  });
  console.log("Batch image upload dialog closed");
  await page.waitForTimeout(800);
}

async function configureProductDescription(page: Page, productItem: ProductJsonItem) {
  await openDescriptionEditor(page);
  await openBatchImageUploadDialogFromDescription(page);
  await uploadDescriptionDetailImages(page, productItem);

  const editorDialog = await getDescriptionEditorDialog(page);
  await saveTopDialogByPrimaryButton(page, editorDialog, "Clicked product description editor save");
  await waitForDescriptionEditorDialogToClose(page, 8_000).catch(async () => {
    const retryEditorDialog = await getDescriptionEditorDialog(page);
    await saveTopDialogByPrimaryButton(page, retryEditorDialog, "Clicked product description editor save retry");
    await waitForDescriptionEditorDialogToClose(page, 20_000);
  });
  console.log("Product description editor dialog closed");
}

async function addSalesOptionsBySkuColorCount(page: Page) {
  await clickSalesAttributesNav(page);
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(500);

  const addSalesSpecButton = page.locator("button.add-sku-property-button").first();
  for (let index = 0; index < 2; index += 1) {
    await addSalesSpecButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(addSalesSpecButton, page);
    console.log(`Clicked ${addSalesSpecText} ${index + 1}/2`);
    await page.waitForTimeout(500);
  }

  const skuPropertyContents = page.locator(".sku-property-list .sku-property__content");
  const propertyCount = await skuPropertyContents.count();
  if (propertyCount < 2) {
    throw new Error(`Expected at least 2 sales spec groups, but only found ${propertyCount}.`);
  }

  const colorSpecContent = skuPropertyContents.nth(0);
  await colorSpecContent.waitFor({ state: "visible", timeout: 20_000 });

  for (let index = 0; index < skuColorList.length; index += 1) {
    const addOptionButton = colorSpecContent
      .locator("button")
      .filter({ has: page.locator(`span:text-is("${addOptionText}")`) })
      .first();

    await addOptionButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(addOptionButton, page);
    console.log(`Clicked ${addOptionText} for color ${index + 1}/${skuColorList.length}`);
    await page.waitForTimeout(300);
  }

  const optionInputs = colorSpecContent.locator('.spec-box-container .spec-item input.jx-input__inner');
  const optionInputCount = await optionInputs.count();
  if (optionInputCount < skuColorList.length) {
    throw new Error(
      `Expected at least ${skuColorList.length} color option inputs, but only found ${optionInputCount}.`,
    );
  }

  for (const [index, colorText] of skuColorList.entries()) {
    const input = optionInputs.nth(index);
    await input.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(input, page);
    await setEditableValueLowConflict(input, colorText);

    const verified = await waitForEditableValueStable(input, colorText);
    if (!verified) {
      await captureShopDebugState(page, `miaoshou-sales-option-${index + 1}-input-not-applied.png`);
      throw new Error(`Sales option ${index + 1} did not retain the expected value '${colorText}'.`);
    }

    console.log(`Input color option ${index + 1}/${skuColorList.length}: ${colorText}`);
    await page.waitForTimeout(200);
  }
}

async function addSizeOptionsFromSkuSizeList(page: Page, productItem: ProductJsonItem) {
  const sizeValues = getSkuSizeValues(productItem);
  const skuPropertyContents = page.locator(".sku-property-list .sku-property__content");
  const propertyCount = await skuPropertyContents.count();

  if (propertyCount < 2) {
    throw new Error(`Expected at least 2 sales spec groups, but only found ${propertyCount}.`);
  }

  const sizeSpecContent = skuPropertyContents.nth(1);
  await sizeSpecContent.waitFor({ state: "visible", timeout: 20_000 });

  for (let index = 0; index < sizeValues.length; index += 1) {
    const addOptionButton = sizeSpecContent
      .locator("button")
      .filter({ has: page.locator(`span:text-is("${addOptionText}")`) })
      .first();

    await addOptionButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(addOptionButton, page);
    console.log(`Clicked ${addOptionText} for size ${index + 1}/${sizeValues.length}`);
    await page.waitForTimeout(300);
  }

  const sizeInputs = sizeSpecContent.locator('.spec-box-container .spec-item input.jx-input__inner[placeholder="请输入选项名称"]');
  const sizeInputCount = await sizeInputs.count();
  if (sizeInputCount < sizeValues.length) {
    throw new Error(`Expected at least ${sizeValues.length} size option inputs, but only found ${sizeInputCount}.`);
  }

  for (const [index, sizeText] of sizeValues.entries()) {
    const input = sizeInputs.nth(index);
    await input.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(input, page);
    await setEditableValueLowConflict(input, sizeText);

    const verified = await waitForEditableValueStable(input, sizeText);
    if (!verified) {
      await captureShopDebugState(page, `miaoshou-size-option-${index + 1}-input-not-applied.png`);
      throw new Error(`Size option ${index + 1} did not retain the expected value '${sizeText}'.`);
    }

    console.log(`Input size option ${index + 1}/${sizeValues.length}: ${sizeText}`);
    await page.waitForTimeout(200);
  }
}

async function clickFirstSkuHeaderCheckbox(page: Page) {
  await page.waitForFunction(
    () => document.querySelectorAll(".pro-virtual-table__header-cell.is-selection-column").length > 1,
    undefined,
    { timeout: 20_000 },
  );

  const readHeaderState = async () => {
    return await page.evaluate(() => {
      const headerCell = document.querySelectorAll(".pro-virtual-table__header-cell.is-selection-column")[1] as
        | HTMLElement
        | undefined;
      const input = headerCell?.querySelector(".jx-checkbox__original") as HTMLInputElement | null;
      const wrapper = headerCell?.querySelector(".jx-checkbox__input") as HTMLElement | null;

      const nodes = Array.from(
        document.querySelectorAll(
          ".pro-virtual-table__body .pro-virtual-table__row-cell.is-selection-column label.pro-virtual-table__checkbox .jx-checkbox__input",
        ),
      ) as HTMLElement[];

      let checked = 0;
      for (const node of nodes) {
        const nativeInput = node.querySelector(".jx-checkbox__original") as HTMLInputElement | null;
        if (node.classList.contains("is-checked") || nativeInput?.checked === true) {
          checked += 1;
        }
      }

      return {
        headerChecked: input?.checked === true || wrapper?.classList.contains("is-checked") === true,
        visibleChecked: checked,
        visibleTotal: nodes.length,
      };
    }).catch(() => ({
      headerChecked: false,
      visibleChecked: 0,
      visibleTotal: 0,
    }));
  };

  for (let attempt = 1; attempt <= 8; attempt += 1) {
    await page
      .locator(".pro-virtual-table__header-cell.is-selection-column")
      .nth(1)
      .locator(".jx-checkbox__inner")
      .click({ force: true });

    await page.waitForTimeout(400);

    const state = await readHeaderState();
    if (state.headerChecked || state.visibleChecked > 0) {
      console.log(
        `Header checkbox click attempt ${attempt} applied. Header checked: ${state.headerChecked}. Visible row checkboxes: ${state.visibleChecked}/${state.visibleTotal}`,
      );
      await page.waitForTimeout(300);
      return;
    }
  }

  const finalState = await readHeaderState();
  console.log(
    `Header checkbox still not checked after exact DOM click retries. Visible row checkboxes: ${finalState.visibleChecked}/${finalState.visibleTotal}`,
  );
  await captureShopDebugState(page, "miaoshou-sku-header-checkbox-not-checked.png");
  console.log("Continuing even though the SKU header checkbox state could not be verified.");
}

async function clickPreviewImageBatchButton(page: Page) {
  const previewImageHeaderCell = page
    .locator(".pro-virtual-table__header .pro-virtual-table__header-cell.is-fixed-left.required")
    .nth(0);

  await previewImageHeaderCell.waitFor({ state: "visible", timeout: 20_000 });

  const batchButton = previewImageHeaderCell.locator(".jx-dropdown.pro-dropdown > button").first();

  await batchButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(batchButton, page);
  await page.waitForTimeout(300);

  const expanded = await batchButton.getAttribute("aria-expanded").catch(() => null);
  const hasVisiblePopover = await page
    .locator(".jx-popper:visible, [role='menu']:visible")
    .count()
    .then((count) => count > 0)
    .catch(() => false);

  if (expanded !== "true" && !hasVisiblePopover) {
    await batchButton.evaluate((element) => {
      const target = element as HTMLButtonElement;
      target.click();
    });
    await page.waitForTimeout(300);
  }

  const verifiedExpanded = await batchButton.getAttribute("aria-expanded").catch(() => null);
  const verifiedPopover = await page
    .locator(".jx-popper:visible, [role='menu']:visible")
    .count()
    .then((count) => count > 0)
    .catch(() => false);

  if (verifiedExpanded !== "true" && !verifiedPopover) {
    await captureShopDebugState(page, "miaoshou-preview-image-batch-not-opened.png");
    throw new Error("The preview image batch dropdown did not open.");
  }

  console.log("Clicked preview image batch button");
}

async function clickBatchUploadImageOption(page: Page) {
  const visibleBatchPopover = page.locator(".jx-popper:visible, [role='menu']:visible").last();
  await visibleBatchPopover.waitFor({ state: "visible", timeout: 20_000 });
  await page.waitForTimeout(300);

  const clicked = await page.evaluate((targetText) => {
    const poppers = Array.from(document.querySelectorAll(".jx-popper, [role='menu']")) as HTMLElement[];
    const visiblePoppers = poppers.filter((popper) => {
      const style = window.getComputedStyle(popper);
      return style.display !== "none" && style.visibility !== "hidden" && popper.offsetParent !== null;
    });

    const latestPopover = visiblePoppers[visiblePoppers.length - 1];
    if (!latestPopover) {
      return false;
    }

    const candidates = Array.from(
      latestPopover.querySelectorAll(".picture-selector-item, li, button, [role='menuitem'], div, span"),
    ) as HTMLElement[];
    const option = candidates.find((element) => {
      const text = (element.textContent || "").replace(/\s+/g, "");
      return text.includes(targetText);
    });

    if (!option) {
      return false;
    }

    option.click();
    return true;
  }, batchUploadImageText).catch(() => false);

  if (!clicked) {
    await captureShopDebugState(page, "miaoshou-batch-upload-option-not-clicked.png");
    throw new Error(`Could not click ${batchUploadImageText} from the visible batch popover.`);
  }

  console.log(`Clicked ${batchUploadImageText}`);
  await page.waitForTimeout(300);
  await captureShopDebugState(page, "miaoshou-after-click-batch-upload-option.png");
}

async function getBatchUploadDialogLegacy(page: Page) {
  await page.waitForFunction(() => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.some((dialog) => {
      const style = window.getComputedStyle(dialog);
      const isVisible = style.display !== "none" && style.visibility !== "hidden" && dialog.offsetParent !== null;
      const title = (dialog.querySelector(".jx-dialog__title")?.textContent || "").trim();
      return isVisible && title === "批量传图";
    });
  }, undefined, { timeout: 20_000 });

  const dialogIndex = await page.evaluate(() => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.findIndex((dialog) => {
      const style = window.getComputedStyle(dialog);
      const isVisible = style.display !== "none" && style.visibility !== "hidden" && dialog.offsetParent !== null;
      const title = (dialog.querySelector(".jx-dialog__title")?.textContent || "").trim();
      return isVisible && title === "批量传图";
    });
  });

  if (dialogIndex < 0) {
    throw new Error("Could not find the batch upload dialog after clicking 批量传图.");
  }

  return page.locator(".jx-overlay-dialog").nth(dialogIndex);
}

async function getBatchUploadDialog(page: Page) {
  await page.waitForFunction((expectedTitle) => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.some((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      const title = (dialog.querySelector(".jx-dialog__title")?.textContent || "").trim();
      return isVisible && title === expectedTitle;
    });
  }, batchUploadImageText, { timeout: 20_000 });

  const dialogIndex = await page.evaluate((expectedTitle) => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.findIndex((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      const title = (dialog.querySelector(".jx-dialog__title")?.textContent || "").trim();
      return isVisible && title === expectedTitle;
    });
  }, batchUploadImageText);

  if (dialogIndex < 0) {
    throw new Error(`Could not find the batch upload dialog after clicking ${batchUploadImageText}.`);
  }

  return page.locator(".jx-overlay-dialog").nth(dialogIndex);
}

async function collectBatchUploadRightItemsDebug(batchUploadDialog: Locator) {
  return await batchUploadDialog.evaluate((dialog) => {
    return Array.from(dialog.querySelectorAll(".content-right .picture-item")).map((item) => {
      const name = (item.querySelector(".picture-name")?.textContent || "").trim();
      const errorText = (item.querySelector(".picture-error")?.textContent || "").trim();
      const imageClass = (item.querySelector(".jx-image") as HTMLElement | null)?.className || "";
      return {
        name,
        errorText,
        imageClass,
      };
    });
  });
}

async function findRightBatchUploadItemByColor(batchUploadDialog: Locator, skuColor: string, timeoutMs = 10_000) {
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    const result = await batchUploadDialog.evaluate((dialog, colorText) => {
      const rightContainer =
        (dialog.querySelector(".content-right .picture-list") as HTMLElement | null) ??
        (dialog.querySelector(".content-right") as HTMLElement | null);

      const rightItems = Array.from(dialog.querySelectorAll(".content-right .picture-item")) as HTMLElement[];
      const names = rightItems.map((item) => (item.querySelector(".picture-name")?.textContent || "").trim());
      const targetIndex = names.findIndex((name) => name.includes(colorText));

      if (targetIndex >= 0) {
        const targetItem = rightItems[targetIndex];
        targetItem.scrollIntoView({ block: "center", inline: "nearest" });
        return {
          found: true,
          targetIndex,
          names,
          scrollTop: rightContainer?.scrollTop || 0,
          scrollHeight: rightContainer?.scrollHeight || 0,
          clientHeight: rightContainer?.clientHeight || 0,
          reachedEnd: false,
        };
      }

      if (!rightContainer) {
        return {
          found: false,
          targetIndex: -1,
          names,
          scrollTop: 0,
          scrollHeight: 0,
          clientHeight: 0,
          reachedEnd: true,
        };
      }

      const previousScrollTop = rightContainer.scrollTop;
      const scrollStep = Math.max(120, Math.floor(rightContainer.clientHeight * 0.8));
      rightContainer.scrollTop = Math.min(previousScrollTop + scrollStep, rightContainer.scrollHeight);
      const reachedEnd = rightContainer.scrollTop === previousScrollTop || rightContainer.scrollTop + rightContainer.clientHeight >= rightContainer.scrollHeight;

      return {
        found: false,
        targetIndex: -1,
        names,
        scrollTop: rightContainer.scrollTop,
        scrollHeight: rightContainer.scrollHeight,
        clientHeight: rightContainer.clientHeight,
        reachedEnd,
      };
    }, skuColor);

    if (result.found) {
      return result;
    }

    if (result.reachedEnd) {
      return result;
    }

    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  return {
    found: false,
    targetIndex: -1,
    names: [] as string[],
    scrollTop: 0,
    scrollHeight: 0,
    clientHeight: 0,
    reachedEnd: true,
  };
}

async function assignUploadedSkuPreviewToRightColor(batchUploadDialog: Locator, skuColor: string) {
  const assignment = await batchUploadDialog.evaluate((dialog, colorText) => {
    const rightItems = Array.from(dialog.querySelectorAll(".content-right .picture-item")) as HTMLElement[];
    const snapshot = rightItems.map((item) => ({
      name: (item.querySelector(".picture-name")?.textContent || "").trim(),
      errorText: (item.querySelector(".picture-error")?.textContent || "").trim(),
      imageClass: (item.querySelector(".jx-image") as HTMLElement | null)?.className || "",
    }));

    const targetItem = rightItems.find((item) => {
      const pictureName = (item.querySelector(".picture-name")?.textContent || "").trim();
      return pictureName.includes(colorText);
    });

    if (!targetItem) {
      return {
        matchedName: "",
        clicked: false,
        beforeErrorText: "",
        afterErrorText: "",
        snapshot,
      };
    }

    const targetImageBox =
      (targetItem.querySelector(".image-box") as HTMLElement | null) ??
      (targetItem.querySelector(".jx-image") as HTMLElement | null) ??
      (targetItem.querySelector(".picture-name") as HTMLElement | null) ??
      targetItem;

    const beforeErrorText = (targetItem.querySelector(".picture-error")?.textContent || "").trim();
    targetImageBox.scrollIntoView({ block: "center", inline: "nearest" });
    targetImageBox.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
    targetImageBox.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
    targetImageBox.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
    targetImageBox.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
    targetImageBox.click();

    const nameElement = targetItem.querySelector(".picture-name") as HTMLElement | null;
    if (nameElement) {
      nameElement.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
      nameElement.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
      nameElement.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
      nameElement.click();
    }

    const afterErrorText = (targetItem.querySelector(".picture-error")?.textContent || "").trim();
    return {
      matchedName: (targetItem.querySelector(".picture-name")?.textContent || "").trim(),
      clicked: true,
      beforeErrorText,
      afterErrorText,
      snapshot,
    };
  }, skuColor);

  return assignment;
}

async function assignUploadedSkuPreviewToAllRightColorItems(batchUploadDialog: Locator, skuColor: string) {
  return await batchUploadDialog.evaluate((dialog, colorText) => {
    const rightItems = Array.from(dialog.querySelectorAll(".content-right .picture-item")) as HTMLElement[];
    const escapedColorText = colorText.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const pattern = new RegExp(`^${escapedColorText}-.+`);
    const matchedItems = rightItems.filter((item) => pattern.test((item.innerText || "").trim()));

    const matchedNames: string[] = [];
    for (const targetItem of matchedItems) {
      const image = targetItem.querySelector(".jx-image") as HTMLElement | null;
      if (!image) {
        continue;
      }

      image.scrollIntoView({ block: "center", inline: "nearest" });
      image.click();

      matchedNames.push((targetItem.querySelector(".picture-name")?.textContent || "").trim());
    }

    return {
      matchedCount: matchedItems.length,
      matchedNames,
    };
  }, skuColor);
}

async function waitForSkuPreviewAssignment(batchUploadDialog: Locator, skuColor: string, timeoutMs = 5_000) {
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    const assignmentState = await batchUploadDialog.evaluate((dialog, colorText) => {
      const rightItems = Array.from(dialog.querySelectorAll(".content-right .picture-item")) as HTMLElement[];
      const targetItems = rightItems.filter((item) => {
        const pictureName = (item.querySelector(".picture-name")?.textContent || "").trim();
        return pictureName.includes(colorText);
      });

      if (targetItems.length === 0) {
        return {
          exists: false,
          allAssigned: false,
          matchedCount: 0,
        };
      }

      const allAssigned = targetItems.every((targetItem) => {
        const errorText = (targetItem.querySelector(".picture-error")?.textContent || "").trim();
        const imageSrc = (targetItem.querySelector("img") as HTMLImageElement | null)?.src || "";
        const imageClass = (targetItem.querySelector(".jx-image") as HTMLElement | null)?.className || "";
        return errorText.length === 0 || imageSrc.length > 0 || imageClass.includes("is-selected");
      });

      return {
        exists: true,
        allAssigned,
        matchedCount: targetItems.length,
      };
    }, skuColor);

    if (assignmentState.exists && assignmentState.allAssigned) {
      return assignmentState;
    }

    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  return null;
}

async function uploadSkuPreviewImagesFromDialog(page: Page, productItem: ProductJsonItem) {
  const batchUploadDialog = await getBatchUploadDialog(page);

  for (const [index, skuColor] of skuColorList.entries()) {
    const previewImagePath = getPreviewImagePathByColor(productItem, skuColor);
    console.log(`Uploading SKU preview image for ${skuColor}: ${previewImagePath}`);

    let uploadSucceeded = false;
    let uploadAttempts = 0;

    while (!uploadSucceeded && uploadAttempts < 3) {
      uploadAttempts += 1;

      const addImageButton = batchUploadDialog.locator(".content-left .picture-selector .picture-selector-add-image").first();
      await addImageButton.waitFor({ state: "visible", timeout: 20_000 });
      await clickLocatorLowConflict(addImageButton, page);

      const localUploadContainer = page.locator(".jx-popper .picture-selector-item.pro-upload").first();
      await localUploadContainer.waitFor({ state: "visible", timeout: 20_000 });

      const localUploadInput = localUploadContainer.locator(
        'input.jx-upload__input[name="uploadImgFile"][type="file"]',
      ).first();
      await localUploadInput.waitFor({ state: "attached", timeout: 20_000 });

      const uploadResponsePromise = page.waitForResponse(
        (response) => response.url().includes("/api/picture/picture/uploadPictureFile"),
        { timeout: 30_000 },
      );

      await localUploadInput.setInputFiles(previewImagePath);

      const response = await uploadResponsePromise.catch(() => null);
      const payload = response ? await response.json().catch(() => null) : null;
      const success = response && response.ok() && isSuccessfulUploadResponse(payload);

      if (success) {
        uploadSucceeded = true;
        break;
      }

      if (response && isUpload502Response(response) && uploadAttempts < 3) {
        console.log(`SKU preview upload hit 502 for ${skuColor} on attempt ${uploadAttempts}/3. Closing dialog and retrying.`);
        await closeTopVisibleDialogByHeaderButtonStable(page);
        await page.waitForTimeout(500);
        continue;
      }

      await captureShopDebugState(page, `miaoshou-batch-preview-upload-${skuColor}-failed.png`);
      throw new Error(`Failed to upload SKU preview image for color '${skuColor}'.`);
    }

    if (!uploadSucceeded) {
      await captureShopDebugState(page, `miaoshou-batch-preview-upload-${skuColor}-failed.png`);
      throw new Error(`Failed to upload SKU preview image for color '${skuColor}' after retries.`);
    }

    const leftPictureItemTarget = batchUploadDialog
      .locator(".content-left .picture-list .picture-item")
      .last();
    await leftPictureItemTarget.waitFor({ state: "visible", timeout: 20_000 });

    if (index > 0) {
      const locatedRightItem = await findRightBatchUploadItemByColor(batchUploadDialog, skuColor, 12_000);
      if (!locatedRightItem.found) {
        console.log(`Right-side batch preview items: ${JSON.stringify(locatedRightItem.names)}`);
        await captureShopDebugState(page, `miaoshou-batch-preview-picture-name-missing-${skuColor}.png`);
        throw new Error(`Could not find a right-side picture-name containing '${skuColor}'.`);
      }

      const assignmentResult = await assignUploadedSkuPreviewToAllRightColorItems(batchUploadDialog, skuColor);

      if (assignmentResult.matchedCount === 0) {
        const debugItems = await collectBatchUploadRightItemsDebug(batchUploadDialog);
        console.log(`Right-side batch preview debug for ${skuColor}: ${JSON.stringify(debugItems)}`);
        await captureShopDebugState(page, `miaoshou-batch-preview-picture-name-missing-${skuColor}.png`);
        throw new Error(`Could not find any right-side picture-name containing '${skuColor}'.`);
      }

      const verifiedAssignment = await waitForSkuPreviewAssignment(batchUploadDialog, skuColor, 6_000);

      if (!verifiedAssignment) {
        const debugItems = await collectBatchUploadRightItemsDebug(batchUploadDialog);
        console.log(`Right-side batch preview debug for ${skuColor}: ${JSON.stringify(debugItems)}`);
        await captureShopDebugState(page, `miaoshou-batch-preview-assignment-failed-${skuColor}.png`);
        throw new Error(`Clicked right-side SKU item for '${skuColor}', but the assignment state did not change.`);
      }

      console.log(`Assigned uploaded SKU preview to ${assignmentResult.matchedCount} right-side items: ${assignmentResult.matchedNames.join(", ")}`);
      await page.waitForTimeout(500);
    } else {
      console.log(`Skipped right-side picture-name click for first SKU color: ${skuColor}`);
    }

    const pictureSelectClicked = await leftPictureItemTarget.evaluate((element) => {
      const button = element.querySelector(".picture-select") as HTMLElement | null;
      if (!button) {
        return false;
      }

      button.style.display = "block";
      button.style.opacity = "1";
      button.style.visibility = "visible";
      button.click();
      return true;
    });

    if (!pictureSelectClicked) {
      await captureShopDebugState(page, `miaoshou-batch-preview-picture-select-missing-${skuColor}.png`);
      throw new Error(`Could not find .picture-select for uploaded left image '${skuColor}'.`);
    }

    console.log(`Clicked left picture-select for ${skuColor}`);
    await page.waitForTimeout(300);

    const deleteClicked = await leftPictureItemTarget.evaluate((element) => {
      const deleteButton = element.querySelector(".shopee-icon-shanchu.pro-icon") as HTMLElement | null;
      if (!deleteButton) {
        return false;
      }

      deleteButton.click();
      return true;
    });

    if (!deleteClicked) {
      await captureShopDebugState(page, `miaoshou-batch-preview-delete-missing-${skuColor}.png`);
      throw new Error(`Could not find delete button for color '${skuColor}'.`);
    }

    console.log(`Clicked left delete icon for ${skuColor}`);
    await page.waitForTimeout(300);
  }
}

async function confirmBatchUploadDialog(page: Page) {
  const batchUploadDialog = await getBatchUploadDialog(page);
  const confirmButton = batchUploadDialog
    .locator("button")
    .filter({ has: page.locator(`span:text-is("确定")`) })
    .first();

  await confirmButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(confirmButton, page);
  console.log("Clicked 批量传图 dialog 确定");
  await page.waitForTimeout(500);
}

async function clickSupplyPriceBatchButton(page: Page) {
  const supplyPriceHeaderCell = page
    .locator(".pro-virtual-table__header .pro-virtual-table__header-cell.required")
    .filter({ hasText: "供货价" })
    .first();

  await supplyPriceHeaderCell.waitFor({ state: "visible", timeout: 20_000 });

  const batchButton = supplyPriceHeaderCell
    .locator("button")
    .filter({ has: page.locator(`span:text-is("${batchText}")`) })
    .first();

  await batchButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(batchButton, page);
  console.log("Clicked 供货价旁边的批量按钮");
  await page.waitForTimeout(300);
}

async function clickHeaderBatchButton(page: Page, headerText: string) {
  const headerCell = page
    .locator(".pro-virtual-table__header .pro-virtual-table__header-cell")
    .filter({ hasText: headerText })
    .first();

  await headerCell.waitFor({ state: "visible", timeout: 20_000 });

  const batchButton = headerCell
    .locator("button")
    .filter({ has: page.locator(`span:text-is("${batchText}")`) })
    .first();

  await batchButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(batchButton, page);
  console.log(`Clicked ${headerText}旁边的${batchText}按钮`);
  await page.waitForTimeout(300);
}

async function getSupplyPriceBatchDialog(page: Page) {
  await page.waitForFunction(() => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.some((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      return isVisible && dialog.querySelector('.sku-filter-editor-container input[value="custom"]');
    });
  }, undefined, { timeout: 20_000 });

  const dialogIndex = await page.evaluate(() => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.findIndex((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      return isVisible && dialog.querySelector('.sku-filter-editor-container input[value="custom"]');
    });
  });

  if (dialogIndex < 0) {
    throw new Error("Could not find the supply price batch dialog.");
  }

  return page.locator(".jx-overlay-dialog").nth(dialogIndex);
}

async function clickCurrentBatchDialogCancelButton(page: Page) {
  const batchDialog = await getSupplyPriceBatchDialog(page);
  const closeButton = batchDialog.locator("button.jx-dialog__headerbtn").first();

  await closeButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(closeButton, page);
  console.log("Clicked current batch dialog header close button");
  await page.waitForTimeout(500);
}

async function clickDialogConfirmButton(page: Page, batchDialog: Locator, logText: string) {
  const confirmButton = batchDialog
    .locator("button")
    .filter({ hasText: "确定" })
    .last();

  if ((await confirmButton.count()) > 0) {
    await confirmButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(confirmButton, page);
    console.log(logText);
    await page.waitForTimeout(700);
    return;
  }

  const primaryButton = batchDialog.locator("button.jx-button--primary").last();
  await primaryButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(primaryButton, page);
  console.log(logText);
  await page.waitForTimeout(700);
}

async function getVisibleDialogWithTitle(page: Page, titleText: string) {
  await page.waitForFunction((expectedTitle) => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.some((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      const title = (dialog.querySelector(".jx-dialog__title")?.textContent || "").trim();
      return isVisible && title === expectedTitle;
    });
  }, titleText, { timeout: 20_000 });

  const dialogIndex = await page.evaluate((expectedTitle) => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.findIndex((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      const title = (dialog.querySelector(".jx-dialog__title")?.textContent || "").trim();
      return isVisible && title === expectedTitle;
    });
  }, titleText);

  if (dialogIndex < 0) {
    throw new Error(`Could not find visible dialog titled '${titleText}'.`);
  }

  return page.locator(".jx-overlay-dialog").nth(dialogIndex);
}

async function configureSkuClassificationBatchDialog(page: Page) {
  const dialogTitle = `\u6279\u91cf\u4fee\u6539${skuClassificationText}`;
  const batchDialog = await getVisibleDialogWithTitle(page, dialogTitle);

  const categorySelect = batchDialog.locator(".sku-classification-select .jx-select__wrapper").first();
  await categorySelect.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(categorySelect, page);
  await page.waitForTimeout(300);

  await page.waitForFunction((targetText) => {
    const candidates = Array.from(
      document.querySelectorAll("li.jx-select-dropdown__item[role='option'], .jx-select-dropdown__item[role='option']"),
    ) as HTMLElement[];

    return candidates.some((element) => {
      const style = window.getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      const isVisible = style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
      return isVisible && (element.textContent || "").replace(/\s+/g, "") === targetText;
    });
  }, singleItemText, { timeout: 10_000 });

  const optionClicked = await page.evaluate((targetText) => {
    const candidates = Array.from(
      document.querySelectorAll("li.jx-select-dropdown__item[role='option'], .jx-select-dropdown__item[role='option']"),
    ) as HTMLElement[];

    const option = candidates.find((element) => {
      const style = window.getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      const isVisible = style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
      return isVisible && (element.textContent || "").replace(/\s+/g, "") === targetText;
    });

    if (!option) {
      return false;
    }

    option.scrollIntoView({ block: "center", inline: "nearest" });
    option.dispatchEvent(new MouseEvent("mouseenter", { bubbles: true, cancelable: true }));
    option.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true }));
    option.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true }));
    option.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
    option.click();
    return true;
  }, singleItemText);

  if (!optionClicked) {
    await captureShopDebugState(page, "miaoshou-sku-classification-option-missing.png");
    throw new Error(`Could not select SKU classification option '${singleItemText}'.`);
  }

  console.log(`Selected ${skuClassificationText}: ${singleItemText}`);
  await page.waitForTimeout(300);

  const quantityInputs = batchDialog.locator(".sku-classification-input-group .number-of-pieces-input input.jx-input__inner");
  await quantityInputs.first().waitFor({ state: "visible", timeout: 20_000 });

  const skuQuantityInput = quantityInputs.first();
  await clickLocatorLowConflict(skuQuantityInput, page);
  await setEditableValueLowConflict(skuQuantityInput, "1");
  console.log("Input SKU classification quantity: 1");
  await page.waitForTimeout(200);

  if ((await quantityInputs.count()) > 1) {
    const containsQuantityInput = quantityInputs.nth(1);
    await containsQuantityInput.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(containsQuantityInput, page);
    await setEditableValueLowConflict(containsQuantityInput, "1");
    console.log("Input SKU classification contains quantity: 1");
  }
  await page.waitForTimeout(300);

  const confirmButton = batchDialog
    .locator("button")
    .filter({ has: page.locator('span:text-is("确定")') })
    .first();
  await confirmButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(confirmButton, page);
  console.log(`Clicked ${skuClassificationText} batch dialog confirm`);
  await page.waitForTimeout(700);

  const closeButton = batchDialog.locator("button.jx-dialog__headerbtn").first();
  if (await closeButton.isVisible().catch(() => false)) {
    await clickLocatorLowConflict(closeButton, page);
    console.log(`Clicked ${skuClassificationText} batch dialog header close`);
    await page.waitForTimeout(500);
  } else {
    console.log(`${skuClassificationText} batch dialog already closed after confirm`);
  }
}

async function configureSupplyPriceBatchDialog(page: Page, productItem: ProductJsonItem) {
  const supplyPriceBatchDialog = await getSupplyPriceBatchDialog(page);
  const sizeValues = getSkuSizeValues(productItem);

  const customRadioLabel = supplyPriceBatchDialog
    .locator(".sku-filter-editor-container label.jx-radio")
    .filter({ hasText: "指定规格的SKU" })
    .first();

  await customRadioLabel.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(customRadioLabel, page);
  console.log("Clicked 指定规格的SKU radio");
  await page.waitForTimeout(500);

  const dialogBody = supplyPriceBatchDialog.locator(".jx-dialog__body").first();
  await dialogBody.waitFor({ state: "visible", timeout: 20_000 });

  for (const [index, sizeText] of sizeValues.entries()) {
    await dialogBody.evaluate((element) => {
      element.scrollTop += 100;
      return null;
    });
    await page.waitForTimeout(300);

    const sizeInput = supplyPriceBatchDialog
      .locator('.sku-filter-editor-container input.jx-input__inner:not([disabled])')
      .last();

    await sizeInput.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(sizeInput, page);
    await setEditableValueLowConflict(sizeInput, sizeText);
    console.log(`Input 指定规格尺码 ${index + 1}/${sizeValues.length}: ${sizeText}`);
    await page.waitForTimeout(300);

    const applyAllButton = supplyPriceBatchDialog
      .locator("button, span, div")
      .filter({ hasText: "全按" })
      .last();

    await applyAllButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(applyAllButton, page);
    console.log(`Clicked 全按 for 尺码 ${index + 1}/${sizeValues.length}: ${sizeText}`);
    await page.waitForTimeout(400);
  }
}

async function configureSupplyPriceBatchDialogFromCompleteHtmlLegacy(page: Page, productItem: ProductJsonItem) {
  const supplyPriceBatchDialog = await getSupplyPriceBatchDialog(page);
  const sizeValues = [...new Set(getSkuSizeValues(productItem))];

  const customRadioLabel = supplyPriceBatchDialog
    .locator(".sku-filter-editor-container label.jx-radio")
    .filter({ hasText: "指定规格的SKU" })
    .first();

  await customRadioLabel.waitFor({ state: "visible", timeout: 20_000 });
  await customRadioLabel.evaluate((element) => {
    (element as HTMLLabelElement).click();
  });
  console.log("Clicked 指定规格的SKU radio");
  await page.waitForTimeout(500);

  const dialogBody = supplyPriceBatchDialog.locator(".jx-dialog__body").first();
  await dialogBody.waitFor({ state: "visible", timeout: 20_000 });
  await dialogBody.evaluate((element) => {
    element.scrollTop += 100;
    return null;
  });
  await page.waitForTimeout(300);

  const checkboxGroups = supplyPriceBatchDialog.locator(".sku-checkbox-group-list > .mb_10");
  await checkboxGroups.nth(1).waitFor({ state: "visible", timeout: 20_000 });
  const sizeGroup = checkboxGroups.nth(1);

  const availableSizeLabels = await sizeGroup.evaluate((element) =>
    Array.from(element.querySelectorAll(".jx-checkbox-group label.jx-checkbox .jx-checkbox__label"))
      .map((label) => (label.textContent || "").replace(/\s+/g, " ").trim())
      .filter((text) => text.length > 0),
  );

  for (const [index, sizeText] of sizeValues.entries()) {
    const sizeCheckbox = sizeGroup
      .locator(".jx-checkbox-group label.jx-checkbox")
      .filter({ hasText: sizeText })
      .first();

    if ((await sizeCheckbox.count()) === 0) {
      await captureShopDebugState(page, `miaoshou-supply-price-size-missing-${sizeText}.png`);
      throw new Error(
        `Could not find size checkbox '${sizeText}' in the supply price batch dialog. Available sizes: ${availableSizeLabels.join(", ")}`,
      );
    }

    await sizeCheckbox.scrollIntoViewIfNeeded().catch(() => {});
    await sizeCheckbox.waitFor({ state: "visible", timeout: 20_000 });

    let isChecked = await sizeCheckbox
      .locator('input.jx-checkbox__original[type="checkbox"]')
      .first()
      .evaluate((element) => (element as HTMLInputElement).checked)
      .catch(() => false);

    if (!isChecked) {
      await sizeCheckbox.evaluate((element) => {
        (element as HTMLLabelElement).click();
      });
      await page.waitForTimeout(250);
      isChecked = await sizeCheckbox
        .locator('input.jx-checkbox__original[type="checkbox"]')
        .first()
        .evaluate((element) => (element as HTMLInputElement).checked)
        .catch(() => false);
    }

    if (!isChecked) {
      await captureShopDebugState(page, `miaoshou-supply-price-size-not-checked-${sizeText}.png`);
      throw new Error(`Clicked size checkbox '${sizeText}', but it did not become checked.`);
    }

    console.log(`Checked 指定规格的SKU 尺码 ${index + 1}/${sizeValues.length}: ${sizeText}`);
    await page.waitForTimeout(400);
  }
}

async function configureSupplyPriceBatchDialogFromCompleteHtml(page: Page, productItem: ProductJsonItem) {
  const sizePriceEntries = getSkuSizePriceEntries(productItem);
  await configureHeaderBatchPriceBySizes(
    page,
    sizePriceEntries.map((entry) => ({ size: entry.size, price: entry.supplyPrice })),
    supplyPriceText,
    { useSizeFilter: false },
  );
}

async function configureHeaderBatchPriceBySizes(
  page: Page,
  entries: Array<{ size: string; price: string }>,
  headerText: string,
  options: { useSizeFilter?: boolean; priceInputLocator?: string; priceMode?: "direct" | "formulaMultiplier" } = {},
) {
  const useSizeFilter = options.useSizeFilter !== false;
  const priceMode = options.priceMode ?? "direct";
  const priceInputLocator = options.priceInputLocator ?? ".currency-input input.jx-input__inner";

  for (const [index, entry] of entries.entries()) {
    const batchDialog = await getSupplyPriceBatchDialog(page);

    const customRadioLabel = batchDialog
      .locator(".sku-filter-editor-container label.jx-radio")
      .filter({ hasText: "指定规格的SKU" })
      .first();

    await customRadioLabel.waitFor({ state: "visible", timeout: 20_000 });
    await customRadioLabel.evaluate((element) => {
      (element as HTMLLabelElement).click();
    });
    console.log(`Clicked 指定规格的SKU radio ${index + 1}/${entries.length} for ${headerText}`);
    await page.waitForTimeout(400);

    const dialogBody = batchDialog.locator(".jx-dialog__body").first();
    await dialogBody.waitFor({ state: "visible", timeout: 20_000 });
    await dialogBody.evaluate((element) => {
      element.scrollTop += 100;
      return null;
    });
    await page.waitForTimeout(250);

    const checkboxGroups = batchDialog.locator(".sku-checkbox-group-list > .mb_10");
    await checkboxGroups.nth(1).waitFor({ state: "visible", timeout: 20_000 });
    const sizeGroup = checkboxGroups.nth(1);

    if (useSizeFilter) {
      const sizeFilterInput = sizeGroup.locator("input.jx-input__inner").first();
      await sizeFilterInput.waitFor({ state: "visible", timeout: 20_000 });
      await clickLocatorLowConflict(sizeFilterInput, page);
      await setEditableValueLowConflict(sizeFilterInput, entry.size);
    console.log(`Input 尺码筛选 ${index + 1}/${entries.length} for ${headerText}: ${entry.size}`);
    await page.waitForTimeout(500);
    }

    const availableSizeLabels = await sizeGroup.evaluate((element) =>
      Array.from(element.querySelectorAll(".jx-checkbox-group label.jx-checkbox .jx-checkbox__label"))
        .map((label) => (label.textContent || "").replace(/\s+/g, " ").trim())
        .filter((text) => text.length > 0),
    );

    const sizeCheckbox = sizeGroup
      .locator(".jx-checkbox-group label.jx-checkbox")
      .filter({ hasText: entry.size })
      .first();

    if ((await sizeCheckbox.count()) === 0) {
      await captureShopDebugState(page, `miaoshou-${headerText}-size-missing-${entry.size}.png`);
      throw new Error(
        `Could not find size checkbox '${entry.size}' in the ${headerText} batch dialog. Available sizes: ${availableSizeLabels.join(", ")}`,
      );
    }

    await sizeCheckbox.scrollIntoViewIfNeeded().catch(() => {});
    await sizeCheckbox.waitFor({ state: "visible", timeout: 20_000 });

    let isChecked = await sizeCheckbox
      .locator('input.jx-checkbox__original[type="checkbox"]')
      .first()
      .evaluate((element) => (element as HTMLInputElement).checked)
      .catch(() => false);

    if (!isChecked) {
      await sizeCheckbox.evaluate((element) => {
        (element as HTMLLabelElement).click();
      });
      await page.waitForTimeout(250);
      isChecked = await sizeCheckbox
        .locator('input.jx-checkbox__original[type="checkbox"]')
        .first()
        .evaluate((element) => (element as HTMLInputElement).checked)
        .catch(() => false);
    }

    if (!isChecked) {
      await captureShopDebugState(page, `miaoshou-${headerText}-size-not-checked-${entry.size}.png`);
      throw new Error(`Clicked size checkbox '${entry.size}', but it did not become checked in ${headerText}.`);
    }
    
    console.log(`Checked 尺码 ${index + 1}/${entries.length} for ${headerText}: ${entry.size}`);
    await page.waitForTimeout(300);

    await sizeGroup.evaluate((element, targetSize) => {
      const labels = Array.from(element.querySelectorAll(".jx-checkbox-group label.jx-checkbox")) as HTMLLabelElement[];

      for (const label of labels) {
        const labelText = (label.querySelector(".jx-checkbox__label")?.textContent || "").replace(/\s+/g, " ").trim();
        const input = label.querySelector('input.jx-checkbox__original[type="checkbox"]') as HTMLInputElement | null;
        const inputWrapper = label.querySelector(".jx-checkbox__input") as HTMLElement | null;
        const isLabelChecked = input?.checked === true || inputWrapper?.classList.contains("is-checked") === true;

        if (labelText !== targetSize && isLabelChecked) {
          label.scrollIntoView({ block: "center", inline: "nearest" });
          label.click();
        }
      }
    }, entry.size);

    let exclusiveSelectionState = {
      checkedLabels: [] as string[],
      targetCheckedOnly: false,
    };

    const exclusiveSelectionStart = Date.now();
    while (Date.now() - exclusiveSelectionStart < 5_000) {
      await page.waitForTimeout(300);

      exclusiveSelectionState = await sizeGroup.evaluate((element, targetSize) => {
        const labels = Array.from(element.querySelectorAll(".jx-checkbox-group label.jx-checkbox")) as HTMLLabelElement[];
        const checkedLabels = labels
          .filter((label) => {
            const input = label.querySelector('input.jx-checkbox__original[type="checkbox"]') as HTMLInputElement | null;
            const inputWrapper = label.querySelector(".jx-checkbox__input") as HTMLElement | null;
            return input?.checked === true || inputWrapper?.classList.contains("is-checked") === true;
          })
          .map((label) => (label.querySelector(".jx-checkbox__label")?.textContent || "").replace(/\s+/g, " ").trim());

        return {
          checkedLabels,
          targetCheckedOnly: checkedLabels.length === 1 && checkedLabels[0] === targetSize,
        };
      }, entry.size);

      if (exclusiveSelectionState.targetCheckedOnly) {
        break;
      }
    }

    if (!exclusiveSelectionState.targetCheckedOnly) {
      await captureShopDebugState(page, `miaoshou-${headerText}-size-selection-not-exclusive-${entry.size}.png`);
      throw new Error(
        `Expected only size '${entry.size}' to be checked in ${headerText}, but checked sizes are: ${exclusiveSelectionState.checkedLabels.join(", ")}`,
      );
    }

    if (priceMode === "formulaMultiplier") {
      const formulaRadioLabel = batchDialog.locator('label.jx-radio:has(input[value="formula"])').first();
      await formulaRadioLabel.waitFor({ state: "visible", timeout: 20_000 });
      await formulaRadioLabel.evaluate((element) => {
        (element as HTMLLabelElement).click();
      });
      await page.waitForTimeout(300);

      const multiplierInput = batchDialog.locator('input.jx-input__inner[placeholder="倍数"]').first();
      await multiplierInput.waitFor({ state: "visible", timeout: 20_000 });
      await clickLocatorLowConflict(multiplierInput, page);
      await setEditableValueLowConflict(multiplierInput, entry.price);
      console.log(`Input ${headerText} formula multiplier ${index + 1}/${entries.length}: ${entry.price}`);
    } else {
      const priceInput = batchDialog.locator(priceInputLocator).first();
      await priceInput.waitFor({ state: "visible", timeout: 20_000 });
      await clickLocatorLowConflict(priceInput, page);
      await setEditableValueLowConflict(priceInput, entry.price);
      console.log(`Input ${headerText} ${index + 1}/${entries.length}: ${entry.price}`);
    }
    await page.waitForTimeout(300);

    const confirmButton = batchDialog
      .locator("button")
      .filter({ has: page.locator('span:text-is("确定")') })
      .first();

    await confirmButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(confirmButton, page);
    console.log(`Clicked ${headerText}批量弹窗确定 ${index + 1}/${entries.length}`);
    await page.waitForTimeout(700);

  }
}

async function configureSuggestedPriceFormulaBatchDialog(page: Page, multiplierValue: string) {
  const batchDialog = await getVisibleDialogWithTitle(page, `批量修改${suggestedPriceText}`);

  const formulaRadioLabel = batchDialog.locator('label.jx-radio:has(input[value="formula"])').first();
  await formulaRadioLabel.waitFor({ state: "visible", timeout: 20_000 });
  await formulaRadioLabel.evaluate((element) => {
    (element as HTMLLabelElement).click();
  });
  await page.waitForTimeout(300);

  let multiplierInput = batchDialog.locator('input.jx-input__inner[placeholder="倍数"]').first();
  if ((await multiplierInput.count()) === 0) {
    multiplierInput = batchDialog.locator('input.jx-input__inner:not([disabled]):not([readonly])').nth(1);
  }
  await multiplierInput.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(multiplierInput, page);
  await setEditableValueLowConflict(multiplierInput, multiplierValue);
  console.log(`Input ${suggestedPriceText} formula multiplier: ${multiplierValue}`);
  await page.waitForTimeout(300);

  const confirmButton = batchDialog
    .locator("button")
    .filter({ has: page.locator('span:text-is("确定")') })
    .first();

  await confirmButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(confirmButton, page);
  console.log(`Clicked ${suggestedPriceText} formula batch dialog confirm`);
  await page.waitForTimeout(700);
}

async function selectOnlySizeInBatchDialog(page: Page, batchDialog: Locator, sizeText: string, debugPrefix: string) {
  const dialogBody = batchDialog.locator(".jx-dialog__body").first();
  await dialogBody.waitFor({ state: "visible", timeout: 20_000 });
  await dialogBody.evaluate((element) => {
    element.scrollTop += 100;
    return null;
  });
  await page.waitForTimeout(250);

  const checkboxGroups = batchDialog.locator(".sku-checkbox-group-list > .mb_10");
  await checkboxGroups.nth(1).waitFor({ state: "visible", timeout: 20_000 });
  const sizeGroup = checkboxGroups.nth(1);

  const availableSizeLabels = await sizeGroup.evaluate((element) =>
    Array.from(element.querySelectorAll(".jx-checkbox-group label.jx-checkbox .jx-checkbox__label"))
      .map((label) => (label.textContent || "").replace(/\s+/g, " ").trim())
      .filter((text) => text.length > 0),
  );

  const sizeCheckbox = sizeGroup
    .locator(".jx-checkbox-group label.jx-checkbox")
    .filter({ hasText: sizeText })
    .first();

  if ((await sizeCheckbox.count()) === 0) {
    await captureShopDebugState(page, `miaoshou-${debugPrefix}-size-missing-${sizeText}.png`);
    throw new Error(`Could not find size checkbox '${sizeText}' in ${debugPrefix}. Available sizes: ${availableSizeLabels.join(", ")}`);
  }

  await sizeGroup.evaluate((element, targetSize) => {
    const labels = Array.from(element.querySelectorAll(".jx-checkbox-group label.jx-checkbox")) as HTMLLabelElement[];

    for (const label of labels) {
      const labelText = (label.querySelector(".jx-checkbox__label")?.textContent || "").replace(/\s+/g, " ").trim();
      const input = label.querySelector('input.jx-checkbox__original[type="checkbox"]') as HTMLInputElement | null;
      const inputWrapper = label.querySelector(".jx-checkbox__input") as HTMLElement | null;
      const isChecked = input?.checked === true || inputWrapper?.classList.contains("is-checked") === true;

      if (labelText !== targetSize && isChecked) {
        label.scrollIntoView({ block: "center", inline: "nearest" });
        label.click();
      }
    }
  }, sizeText);

  await sizeCheckbox.scrollIntoViewIfNeeded().catch(() => {});
  await sizeCheckbox.waitFor({ state: "visible", timeout: 20_000 });

  let isChecked = await sizeCheckbox
    .locator('input.jx-checkbox__original[type="checkbox"]')
    .first()
    .evaluate((element) => (element as HTMLInputElement).checked)
    .catch(() => false);

  if (!isChecked) {
    await sizeCheckbox.evaluate((element) => {
      (element as HTMLLabelElement).click();
    });
    await page.waitForTimeout(350);
  }

  let checkedLabels: string[] = [];
  const selectionStart = Date.now();
  while (Date.now() - selectionStart < 5_000) {
    await page.waitForTimeout(300);
    checkedLabels = await sizeGroup.evaluate((element) => {
      const labels = Array.from(element.querySelectorAll(".jx-checkbox-group label.jx-checkbox")) as HTMLLabelElement[];
      return labels
        .filter((label) => {
          const input = label.querySelector('input.jx-checkbox__original[type="checkbox"]') as HTMLInputElement | null;
          const inputWrapper = label.querySelector(".jx-checkbox__input") as HTMLElement | null;
          return input?.checked === true || inputWrapper?.classList.contains("is-checked") === true;
        })
        .map((label) => (label.querySelector(".jx-checkbox__label")?.textContent || "").replace(/\s+/g, " ").trim());
    });

    if (checkedLabels.length === 1 && checkedLabels[0] === sizeText) {
      return;
    }
  }

  await captureShopDebugState(page, `miaoshou-${debugPrefix}-size-selection-not-exclusive-${sizeText}.png`);
  throw new Error(`Expected only size '${sizeText}' to be checked in ${debugPrefix}, but checked sizes are: ${checkedLabels.join(", ")}`);
}

async function configurePackageSizeBatchDialog(page: Page, productItem: ProductJsonItem) {
  const dimensionEntries = getSkuSizeDimensionEntries(productItem);
  const batchDialog = await getVisibleDialogWithTitle(page, packageSizeDialogTitleText);

  for (const [index, entry] of dimensionEntries.entries()) {
    const customRadioLabel = batchDialog.locator('.sku-filter-editor-container label.jx-radio:has(input[value="custom"])').first();
    await customRadioLabel.waitFor({ state: "visible", timeout: 20_000 });
    await customRadioLabel.evaluate((element) => {
      (element as HTMLLabelElement).click();
    });
    console.log(`Clicked 指定规格的SKU radio ${index + 1}/${dimensionEntries.length} for ${packageSizeText}`);
    await page.waitForTimeout(400);

    await selectOnlySizeInBatchDialog(page, batchDialog, entry.size, packageSizeText);
    console.log(`Checked ${packageSizeText} size ${index + 1}/${dimensionEntries.length}: ${entry.size}`);

    const dimensionInputs = batchDialog.locator("form input.jx-input__inner");
    await dimensionInputs.nth(0).waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(dimensionInputs.nth(0), page);
    await clearEditableValueLowConflict(dimensionInputs.nth(0));
    await setEditableValueLowConflict(dimensionInputs.nth(0), entry.length);
    await clickLocatorLowConflict(dimensionInputs.nth(1), page);
    await clearEditableValueLowConflict(dimensionInputs.nth(1));
    await setEditableValueLowConflict(dimensionInputs.nth(1), entry.width);
    await clickLocatorLowConflict(dimensionInputs.nth(2), page);
    await clearEditableValueLowConflict(dimensionInputs.nth(2));
    await setEditableValueLowConflict(dimensionInputs.nth(2), entry.height);
    console.log(
      `Input ${packageSizeText} ${index + 1}/${dimensionEntries.length}: ${entry.size} => ${entry.length}/${entry.width}/${entry.height}`,
    );
    await page.waitForTimeout(300);

    const confirmButton = batchDialog
      .locator("button")
      .filter({ has: page.locator('span:text-is("确定")') })
      .first();

    await confirmButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(confirmButton, page);
    console.log(`Clicked ${packageSizeText} batch dialog confirm ${index + 1}/${dimensionEntries.length}`);
    await page.waitForTimeout(700);
  }

  const closeButton = batchDialog.locator("button.jx-dialog__headerbtn").first();
  if (await closeButton.isVisible().catch(() => false)) {
    await clickLocatorLowConflict(closeButton, page);
    console.log(`Clicked ${packageSizeText} batch dialog header close`);
    await page.waitForTimeout(500);
  } else {
    console.log(`${packageSizeText} batch dialog already closed`);
  }
}

async function configureWeightBatchDialog(page: Page, productItem: ProductJsonItem) {
  const weightEntries = getSkuSizeWeightEntries(productItem);
  const batchDialog = await getVisibleDialogWithTitle(page, weightDialogTitleText);

  for (const [index, entry] of weightEntries.entries()) {
    const newWeightRadioLabel = batchDialog.locator('form label.jx-radio:has(input[value="newValue"])').first();
    await newWeightRadioLabel.waitFor({ state: "visible", timeout: 20_000 });
    await newWeightRadioLabel.evaluate((element) => {
      (element as HTMLLabelElement).click();
    });
    await page.waitForTimeout(250);

    const customRadioLabel = batchDialog.locator('.sku-filter-editor-container label.jx-radio:has(input[value="custom"])').first();
    await customRadioLabel.waitFor({ state: "visible", timeout: 20_000 });
    await customRadioLabel.evaluate((element) => {
      (element as HTMLLabelElement).click();
    });
    console.log(`Clicked 指定规格的SKU radio ${index + 1}/${weightEntries.length} for ${weightText}`);
    await page.waitForTimeout(400);

    await selectOnlySizeInBatchDialog(page, batchDialog, entry.size, weightText);
    console.log(`Checked ${weightText} size ${index + 1}/${weightEntries.length}: ${entry.size}`);

    const weightInput = batchDialog.locator('form input.jx-input__inner:not([disabled])').first();
    await weightInput.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(weightInput, page);
    await clearEditableValueLowConflict(weightInput);
    await setEditableValueLowConflict(weightInput, entry.weight);
    console.log(`Input ${weightText} ${index + 1}/${weightEntries.length}: ${entry.size} => ${entry.weight}`);
    await page.waitForTimeout(300);

    const confirmButton = batchDialog
      .locator("button")
      .filter({ has: page.locator('span:text-is("确定")') })
      .first();

    await confirmButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(confirmButton, page);
    console.log(`Clicked ${weightText} batch dialog confirm ${index + 1}/${weightEntries.length}`);
    await page.waitForTimeout(700);
  }

  const closeButton = batchDialog.locator("button.jx-dialog__headerbtn").first();
  if (await closeButton.isVisible().catch(() => false)) {
    await clickLocatorLowConflict(closeButton, page);
    console.log(`Clicked ${weightText} batch dialog header close`);
    await page.waitForTimeout(500);
  } else {
    console.log(`${weightText} batch dialog already closed`);
  }
}

async function configurePackagingInfo(page: Page, productItem: ProductJsonItem) {
  await clickPackagingInfoNav(page);
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(800);

  const packagingPane = await getPackagingInfoPane(page);
  await selectFormItemOptionInScope(page, packagingPane, outerPackagingShapeText, outerPackagingShapeValueText);
  await selectFormItemOptionInScope(page, packagingPane, outerPackagingTypeText, outerPackagingTypeValueText);
  await uploadOuterPackagingImage(page, productItem);
}

async function getReleaseProductDialog(page: Page) {
  await page.waitForFunction((expectedTitle) => {
    const dialogs = Array.from(document.querySelectorAll(".jx-overlay-dialog")) as HTMLElement[];
    return dialogs.some((dialog) => {
      const style = window.getComputedStyle(dialog);
      const rect = dialog.getBoundingClientRect();
      const isVisible =
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        rect.width > 0 &&
        rect.height > 0;
      const title = (dialog.querySelector(".jx-dialog__title")?.textContent || "").trim();
      return isVisible && title === expectedTitle && Boolean(dialog.querySelector(".goods-release-dialog"));
    });
  }, releaseProductDialogTitleText, { timeout: 60_000 });

  const dialog = page.locator(".jx-overlay-dialog").filter({ has: page.locator(".goods-release-dialog") }).last();
  await dialog.waitFor({ state: "visible", timeout: 10_000 });
  return dialog;
}

async function clickPromptDialogClose(page: Page) {
  const promptDialog = await getVisibleDialogWithTitle(page, promptDialogTitleText);
  const closeButton = promptDialog.locator("button").filter({ hasText: closeText }).first();
  await closeButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(closeButton, page);
  console.log(`Clicked ${promptDialogTitleText} dialog ${closeText}`);
  await waitForDialogTitleToClose(page, promptDialogTitleText, 20_000).catch(() => {});
}

async function publishCreatedProduct(page: Page) {
  await clickVisibleButtonByName(page, createPublishText);
  console.log(`Clicked ${createPublishText}`);

  const releaseDialog = await getReleaseProductDialog(page);
  console.log(`Opened ${releaseProductDialogTitleText} dialog`);

  const publishButton = releaseDialog.locator("button").filter({ hasText: publishToSelectedShopText }).first();
  await publishButton.waitFor({ state: "visible", timeout: 20_000 });
  await clickLocatorLowConflict(publishButton, page);
  console.log(`Clicked ${publishToSelectedShopText}`);

  await getVisibleDialogWithTitle(page, promptDialogTitleText);
  await clickPromptDialogClose(page);
}

async function clearPictureIndexCurrentPage(page: Page) {
  await page.goto(pictureIndexUrl, { waitUntil: "domcontentloaded", timeout: 60_000 });
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(1_000);
  console.log(`Opened ${pictureIndexUrl}`);

  const batchDeleteButton = page
    .locator("button")
    .filter({ hasText: batchDeleteText })
    .first();

  await batchDeleteButton.waitFor({ state: "visible", timeout: 30_000 });
  await clickLocatorLowConflict(batchDeleteButton, page);
  console.log(`Clicked ${batchDeleteText}`);
  await page.waitForTimeout(500);

  try {
    await clickFirstVisibleLocator(
      [
        page.locator(".jx-dropdown__popper, .jx-popper").locator("li, [role='menuitem'], .jx-dropdown-menu__item, button, div").filter({ hasText: deleteCurrentPageText }).first(),
        page.locator("li, [role='menuitem'], .jx-dropdown-menu__item, button, div").filter({ hasText: deleteCurrentPageText }).first(),
        page.getByText(deleteCurrentPageText, { exact: true }).first(),
      ],
      `Could not find the '${deleteCurrentPageText}' option after clicking '${batchDeleteText}'.`,
    );
    console.log(`Clicked ${deleteCurrentPageText}`);

    const confirmDeleteButton = page.locator("button").filter({ hasText: confirmDeleteText }).first();
    await confirmDeleteButton.waitFor({ state: "visible", timeout: 20_000 });
    await clickLocatorLowConflict(confirmDeleteButton, page);
    console.log(`Clicked ${confirmDeleteText}`);

    await page.waitForTimeout(10_000);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.log(`Skipped picture cleanup because '${deleteCurrentPageText}' was not available: ${message}`);
  }

  await page.goto(collectBoxItemsUrl, { waitUntil: "domcontentloaded", timeout: 60_000 });
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(1_000);
  console.log(`Returned to ${collectBoxItemsUrl}`);
}

async function prepareCreateProductFlow(page: Page, productItem: ProductJsonItem) {
  await clickVisibleButtonByName(page, createProductText);
  console.log(`Clicked ${createProductText}`);

  await clickVisibleFullscreenButton(page);
  console.log("Clicked dialog fullscreen button");

  await clickShopDropdown(page);
  console.log("Clicked shop dropdown");

  await clickShopSelectorCheckAll(page);

  await selectProductCategory(page);
  const materialSelected = await selectMaterial(page);
  if (materialSelected) {
    await selectBackingMaterial(page);
  } else {
    console.log(`Skipped ${backingMaterialText} because ${materialText} was not selected successfully.`);
  }

  await clickMoreAttributes(page);
  await selectSurfacePattern(page);
  await inputThickness(page);
  await selectLeatherType(page);

  const productTitle = getProductTitle(productItem);
  if (productTitle) {
    await inputProductTitle(page, productTitle);
    await waitForBlockingLayersToClear(page);
    await page.waitForTimeout(800);
  } else {
    console.log(`Skipped ${productTitleText} because the current product item has no title.`);
  }

  const englishTitle = getEnglishTitle(productItem);
  if (englishTitle) {
    await inputEnglishTitle(page, englishTitle);
  } else {
    console.log(`Skipped ${englishTitleText} because the current product item has no englist_title.`);
  }

  await selectOrigin(page);
  await clickProductInfoNav(page);
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(800);
  await uploadMainCarouselImages(page, productItem);
  await addSalesOptionsBySkuColorCount(page);
  await addSizeOptionsFromSkuSizeList(page, productItem);
  await clickFirstSkuHeaderCheckbox(page);
  await clickPreviewImageBatchButton(page);
  await clickBatchUploadImageOption(page);
  await uploadSkuPreviewImagesFromDialog(page, productItem);
  await confirmBatchUploadDialog(page);
  await clickSupplyPriceBatchButton(page);
  await configureSupplyPriceBatchDialogFromCompleteHtml(page, productItem);
  await clickCurrentBatchDialogCancelButton(page);
  console.log(`Clicked ${cancelText} after ${supplyPriceText}`);
  await clickHeaderBatchButton(page, suggestedPriceText);
  await configureSuggestedPriceFormulaBatchDialog(page, suggestedPriceBatchValue);
  await clickHeaderBatchButton(page, skuClassificationText);
  await configureSkuClassificationBatchDialog(page);
  await clickHeaderBatchButton(page, packageSizeText);
  await configurePackageSizeBatchDialog(page, productItem);
  await clickHeaderBatchButton(page, weightText);
  await configureWeightBatchDialog(page, productItem);
  await configurePackagingInfo(page, productItem);
  await configureProductDescription(page, productItem);
  await publishCreatedProduct(page);
  await clearPictureIndexCurrentPage(page);
}

async function runCollectBoxFlow(page: Page, jsonInstance: ProductJsonItem[]) {
  const flowStartMs = Date.now();
  await openCollectBoxItemsPage(page);

  if (jsonInstance.length === 0) {
    logMessage(`No products found in ${productsJsonPath}`);
    return [];
  }

  logMessage(`Loaded ${jsonInstance.length} products from ${productsJsonPath}`);
  logMessage(`SKU colors: ${skuColorList.join(", ")}`);
  const results: Array<Record<string, unknown>> = [];
  let shouldResetBeforeNextProduct = false;

  for (const [index, product] of jsonInstance.entries()) {
    const productStartMs = Date.now();
    const label = getProductLabel(product, index);
    const cardPath = typeof product.card_folder_path === "string" ? product.card_folder_path : getMainFileFolder(product);
    emitEvent("product_start", {
      cardPath,
      label,
      index: index + 1,
      total: jsonInstance.length,
    });

    try {
      if (shouldResetBeforeNextProduct) {
        await openCollectBoxItemsPage(page);
        shouldResetBeforeNextProduct = false;
      }
      logMessage(`Preparing product ${index + 1}/${jsonInstance.length}: ${label}`, { cardPath });
      await prepareCreateProductFlow(page, product);
      const elapsed = formatElapsedTime(Date.now() - productStartMs);
      logMessage(`Product ${index + 1}/${jsonInstance.length} elapsed: ${elapsed}`, { cardPath });
      emitEvent("product_success", {
        cardPath,
        label,
        index: index + 1,
        total: jsonInstance.length,
        elapsed,
      });
      results.push({
        cardPath,
        label,
        status: "success",
        elapsed,
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      emitEvent("product_failed", {
        cardPath,
        label,
        index: index + 1,
        total: jsonInstance.length,
        error: message,
      });
      results.push({
        cardPath,
        label,
        status: "failed",
        error: message,
      });
      shouldResetBeforeNextProduct = true;
    }
  }

  logMessage(`Collect box flow elapsed: ${formatElapsedTime(Date.now() - flowStartMs)}`);
  return results;
}

async function main() {
  const automationStartMs = Date.now();
  const artifactDir = path.resolve(__dirname, "..", "output");
  const profilesDir = resolveProfilesDir();
  const userDataDir = path.resolve(profilesDir, profileName);
  const jsonInstance = readJsonInstance();

  fs.mkdirSync(artifactDir, { recursive: true });
  fs.mkdirSync(userDataDir, { recursive: true });
  emitEvent("started", {
    manifestPath: productsJsonPath,
    resultPath: cliOptions.resultPath,
    total: jsonInstance.length,
  });

  if (killEdgeBeforeLaunch) {
    killEdgeProcessesForProfile(userDataDir);
  }

  const existingProfileLocks = getExistingProfileLockPaths(userDataDir);
  if (existingProfileLocks.length > 0) {
    console.log(`Profile lock files found before launch: ${existingProfileLocks.join(", ")}`);
  }

  let context;
  try {
    context = await launchPersistentContextForProfile(userDataDir);
  } catch (error) {
    if (killEdgeOnBusy && isLikelyProfileBusyError(error)) {
      console.log("Initial launch failed. Attempting one recovery retry after killing this profile's Edge processes.");
      killEdgeProcessesForProfile(userDataDir);
      clearProfileLockFiles(userDataDir);

      try {
        context = await launchPersistentContextForProfile(userDataDir);
      } catch (retryError) {
        throw new Error(buildProfileBusyMessage(userDataDir, retryError));
      }
    } else {
      throw error;
    }
  }

  const page = await createSingleAutomationPage(context);
  emitEvent("opening_base_url", { baseUrl });
  await page.goto(baseUrl, { waitUntil: "domcontentloaded", timeout: 60_000 });
  emitEvent("base_url_opened", { baseUrl, currentUrl: page.url() });
  await waitForBlockingLayersToClear(page);
  await page.waitForTimeout(500);

  const needsManualLogin = await hasImmediateLogin(page);
  const title = await page.title().catch(() => "");

  console.log(`Opened ${baseUrl}`);
  console.log(`Browser channel: ${browserChannel}`);
  console.log(`Profile dir: ${userDataDir}`);
  console.log(`Current URL: ${page.url()}`);
  console.log(`Page title: ${title}`);

  if (needsManualLogin) {
    const screenshotPath = path.resolve(artifactDir, "miaoshou-login.png");
    await page.screenshot({ path: screenshotPath, fullPage: true }).catch(() => {});
    emitEvent("login_required", { screenshotPath });
    console.log(`Detected login page with '${immediateLoginText}'.`);
    console.log(`Saved screenshot: ${screenshotPath}`);
    console.log("Please complete login manually in the opened browser window.");
    console.log("If Edge asks to save the password, you can confirm it in this dedicated profile.");
    await waitForManualLoginToComplete(page);
  }

  const results = await runCollectBoxFlow(page, jsonInstance);
  const successCount = results.filter((item) => item.status === "success").length;
  const failedCount = results.filter((item) => item.status === "failed").length;

  const elapsed = formatElapsedTime(Date.now() - automationStartMs);
  writeResult({
    status: failedCount > 0 ? "failed" : "success",
    successCount,
    failedCount,
    total: results.length,
    elapsed,
    results,
  });
  emitEvent("done", {
    success: successCount,
    failed: failedCount,
    total: results.length,
    elapsed,
  });
  console.log(`Automation total elapsed: ${elapsed}`);
  if (!keepBrowserOpen) {
    await context.close();
  }
}

main().catch((error) => {
  console.error(error);
  const message = error instanceof Error ? error.message : String(error);
  emitEvent("error", { error: message });
  writeResult({
    status: "failed",
    error: message,
  });
  process.exitCode = 1;
});
