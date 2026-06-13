#!/usr/bin/env node
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdir, readdir, stat, writeFile } from "node:fs/promises";
import { basename, extname, join } from "node:path";

const DEFAULT_ROOT = "var/kacky-discord-emotes";
const DEFAULT_CDN_BASE = "https://cdn.kacky.gg";
const EMOTE_PREFIX = "emotes";
const MEDIA_CACHE_CONTROL = "public, max-age=31536000, immutable";
const MANIFEST_CACHE_CONTROL = "public, max-age=300";

function usage() {
  return `Usage:
  node scripts/build-emote-cdn.mjs
  node scripts/build-emote-cdn.mjs --execute

Options:
  --root <dir>       Archive root. Default: ${DEFAULT_ROOT}
  --cdn-base <url>   Public CDN base. Default: CDN_BASE_URL or ${DEFAULT_CDN_BASE}
  --execute          Upload to R2. Without this flag the script only prints the upload plan.
  --self-test        Run script unit checks
`;
}

function parseArgs(argv) {
  const args = {
    root: DEFAULT_ROOT,
    cdnBase: trimTrailingSlash(process.env.CDN_BASE_URL || DEFAULT_CDN_BASE),
    execute: false,
    selfTest: false,
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const next = () => {
      if (i + 1 >= argv.length) throw new Error(`Missing value for ${arg}`);
      return argv[++i];
    };

    if (arg === "--root") args.root = next();
    else if (arg === "--cdn-base") args.cdnBase = trimTrailingSlash(next());
    else if (arg === "--execute") args.execute = true;
    else if (arg === "--self-test") args.selfTest = true;
    else if (arg === "--help" || arg === "-h") {
      console.log(usage());
      process.exit(0);
    } else {
      throw new Error(`Unknown option: ${arg}`);
    }
  }

  return args;
}

function trimTrailingSlash(value) {
  return value.replace(/\/+$/, "");
}

function emoteUrl(cdnBase, name, extension) {
  return `${trimTrailingSlash(cdnBase)}/${EMOTE_PREFIX}/${encodeURIComponent(name)}.${extension}`;
}

function commandText(command, args) {
  return [command, ...args.map(shellQuote)].join(" ");
}

function shellQuote(value) {
  if (/^[A-Za-z0-9_./:=@%+-]+$/.test(value)) return value;
  return `'${value.replaceAll("'", "'\\''")}'`;
}

async function runCommand(command, args) {
  return await new Promise((resolve) => {
    const child = spawn(command, args, { stdio: ["ignore", "pipe", "pipe"] });
    let stdout = "";
    let stderr = "";

    child.stdout.on("data", (chunk) => {
      stdout += chunk;
    });
    child.stderr.on("data", (chunk) => {
      stderr += chunk;
    });
    child.on("error", (error) => {
      resolve({ ok: false, code: -1, stdout, stderr: error.message });
    });
    child.on("close", (code) => {
      resolve({ ok: code === 0, code, stdout, stderr });
    });
  });
}

async function runAws(args, env) {
  return await new Promise((resolve) => {
    const child = spawn("aws", args, {
      stdio: "inherit",
      env: {
        ...process.env,
        AWS_ACCESS_KEY_ID: env.R2_ACCESS_KEY_ID,
        AWS_SECRET_ACCESS_KEY: env.R2_SECRET_ACCESS_KEY,
      },
    });
    child.on("error", (error) => {
      resolve({ ok: false, code: -1, error });
    });
    child.on("close", (code) => {
      resolve({ ok: code === 0, code });
    });
  });
}

async function scanWebmFiles(animatedDir) {
  const files = await readdir(animatedDir);
  return files
    .filter((file) => extname(file).toLowerCase() === ".webm")
    .sort((a, b) => a.localeCompare(b))
    .map((file) => ({
      name: basename(file, extname(file)),
      path: join(animatedDir, file),
      file,
    }));
}

async function probeDimensions(path) {
  const result = await runCommand("ffprobe", [
    "-v", "error",
    "-select_streams", "v:0",
    "-show_entries", "stream=width,height",
    "-of", "json",
    path,
  ]);
  if (!result.ok) {
    throw new Error(`ffprobe failed for ${path}: ${result.stderr.trim() || `exit ${result.code}`}`);
  }

  const parsed = JSON.parse(result.stdout);
  const stream = parsed.streams?.[0];
  const width = Number(stream?.width);
  const height = Number(stream?.height);
  if (!Number.isInteger(width) || !Number.isInteger(height) || width <= 0 || height <= 0) {
    throw new Error(`ffprobe did not return valid dimensions for ${path}`);
  }
  return { width, height };
}

async function generateStaticFallback(inputPath, outputPath, width, height) {
  const filter = `scale=${width}:${height}:force_original_aspect_ratio=decrease,pad=${width}:${height}:(ow-iw)/2:(oh-ih)/2:color=black`;
  return await runCommand("ffmpeg", [
    "-y",
    "-i", inputPath,
    "-vf", filter,
    "-frames:v", "1",
    outputPath,
  ]);
}

function manifestEntry(name, cdnBase, width, height) {
  return {
    name,
    aliases: [],
    animated: true,
    staticUrl: emoteUrl(cdnBase, name, "png"),
    animatedUrl: emoteUrl(cdnBase, name, "webm"),
    width,
    height,
  };
}

async function writeFailures(logsDir, failures) {
  await mkdir(logsDir, { recursive: true });
  const text = failures.length === 0
    ? ""
    : failures.map((failure) => `${failure.name}: ${failure.error}`).join("\n") + "\n";
  await writeFile(join(logsDir, "convert-failures.txt"), text);
}

async function fileSize(path) {
  return (await stat(path)).size;
}

function formatBytes(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KiB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MiB`;
}

async function buildUploadItems(root, emotes, manifestPath) {
  const items = [];
  for (const emote of emotes) {
    items.push({
      path: emote.animatedPath,
      key: `${EMOTE_PREFIX}/${emote.name}.webm`,
      contentType: "video/webm",
      cacheControl: MEDIA_CACHE_CONTROL,
      size: await fileSize(emote.animatedPath),
    });
    if (emote.staticOk) {
      items.push({
        path: emote.staticPath,
        key: `${EMOTE_PREFIX}/${emote.name}.png`,
        contentType: "image/png",
        cacheControl: MEDIA_CACHE_CONTROL,
        size: await fileSize(emote.staticPath),
      });
    }
  }

  items.push({
    path: manifestPath,
    key: `${EMOTE_PREFIX}/manifest.json`,
    contentType: "application/json",
    cacheControl: MANIFEST_CACHE_CONTROL,
    size: await fileSize(manifestPath),
  });

  return items;
}

function r2ConfigFromEnv(env) {
  return {
    R2_ACCOUNT_ID: env.R2_ACCOUNT_ID || "",
    R2_ACCESS_KEY_ID: env.R2_ACCESS_KEY_ID || "",
    R2_SECRET_ACCESS_KEY: env.R2_SECRET_ACCESS_KEY || "",
    R2_BUCKET: env.R2_BUCKET || "",
  };
}

function missingR2Config(config) {
  return Object.entries(config)
    .filter(([, value]) => !value)
    .map(([key]) => key);
}

function endpointUrl(config) {
  return `https://${config.R2_ACCOUNT_ID}.r2.cloudflarestorage.com`;
}

function awsCpArgs(item, config) {
  return [
    "s3", "cp",
    item.path,
    `s3://${config.R2_BUCKET}/${item.key}`,
    "--endpoint-url", endpointUrl(config),
    "--content-type", item.contentType,
    "--cache-control", item.cacheControl,
  ];
}

async function uploadOrDryRun(items, execute) {
  const totalSize = items.reduce((sum, item) => sum + item.size, 0);
  console.log("");
  console.log(`${execute ? "UPLOAD" : "DRY-RUN"} R2 plan: ${items.length} object(s), ${formatBytes(totalSize)} total`);
  for (const item of items) {
    console.log(`  ${item.path} -> ${item.key} (${item.contentType}, ${formatBytes(item.size)})`);
  }

  const config = r2ConfigFromEnv(process.env);
  const missing = missingR2Config(config);
  if (missing.length > 0) {
    console.log(`R2 upload skipped: missing ${missing.join(", ")}.`);
    return { uploaded: false, missing };
  }

  const preview = items.map((item) => commandText("aws", awsCpArgs(item, config)));
  console.log("");
  console.log("aws-cli commands:");
  for (const line of preview) console.log(`  ${line}`);

  if (!execute) {
    console.log("Dry-run only. Re-run with --execute to upload.");
    return { uploaded: false, missing: [] };
  }

  for (const item of items) {
    const result = await runAws(awsCpArgs(item, config), config);
    if (!result.ok) {
      throw new Error(`aws s3 cp failed for ${item.key}: ${result.error?.message ?? `exit ${result.code}`}`);
    }
  }
  return { uploaded: true, missing: [] };
}

async function run(args) {
  const animatedDir = join(args.root, "animated-webm");
  const staticDir = join(args.root, "static");
  const logsDir = join(args.root, "logs");
  const manifestPath = join(args.root, "manifest.json");
  await mkdir(staticDir, { recursive: true });

  const files = await scanWebmFiles(animatedDir);
  if (files.length === 0) throw new Error(`No .webm files found in ${animatedDir}`);

  const entries = [];
  const processed = [];
  const failures = [];

  for (const file of files) {
    const staticPath = join(staticDir, `${file.name}.png`);
    const dimensions = await probeDimensions(file.path);
    const result = await generateStaticFallback(file.path, staticPath, dimensions.width, dimensions.height);
    const staticOk = result.ok;

    if (staticOk) {
      console.log(`ok ${file.name}: ${dimensions.width}x${dimensions.height}`);
      entries.push(manifestEntry(file.name, args.cdnBase, dimensions.width, dimensions.height));
    } else {
      const error = result.stderr.trim() || `ffmpeg exited ${result.code}`;
      console.log(`failed ${file.name}: ${error}`);
      failures.push({ name: file.name, error });
    }

    processed.push({
      name: file.name,
      animatedPath: file.path,
      staticPath,
      staticOk,
      width: dimensions.width,
      height: dimensions.height,
    });
  }

  await writeFailures(logsDir, failures);
  entries.sort((a, b) => a.name.localeCompare(b.name));
  await writeFile(manifestPath, JSON.stringify(entries, null, 2) + "\n");
  console.log(`Wrote ${manifestPath} (${entries.length} entries)`);
  console.log(`Wrote ${join(logsDir, "convert-failures.txt")} (${failures.length} failure(s))`);

  const items = await buildUploadItems(args.root, processed, manifestPath);
  await uploadOrDryRun(items, args.execute);

  console.log("");
  console.log(`Processed ${processed.length} emote(s): ${entries.length} static fallback(s), ${failures.length} failure(s)`);
}

function selfTest() {
  assert.equal(trimTrailingSlash("https://cdn.kacky.gg/"), "https://cdn.kacky.gg");
  assert.equal(emoteUrl("https://cdn.kacky.gg", "peepoRun", "webm"), "https://cdn.kacky.gg/emotes/peepoRun.webm");
  assert.deepEqual(manifestEntry("MLADY", "https://cdn.kacky.gg", 128, 128), {
    name: "MLADY",
    aliases: [],
    animated: true,
    staticUrl: "https://cdn.kacky.gg/emotes/MLADY.png",
    animatedUrl: "https://cdn.kacky.gg/emotes/MLADY.webm",
    width: 128,
    height: 128,
  });
  assert.equal(formatBytes(1024), "1.0 KiB");
  assert.deepEqual(missingR2Config({
    R2_ACCOUNT_ID: "account",
    R2_ACCESS_KEY_ID: "",
    R2_SECRET_ACCESS_KEY: "secret",
    R2_BUCKET: "",
  }), ["R2_ACCESS_KEY_ID", "R2_BUCKET"]);
  console.log("Self-test passed");
}

try {
  const args = parseArgs(process.argv.slice(2));
  if (args.selfTest) {
    selfTest();
  } else {
    await run(args);
  }
} catch (error) {
  console.error(error.message);
  console.error("");
  console.error(usage());
  process.exit(1);
}
