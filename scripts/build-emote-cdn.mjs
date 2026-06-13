#!/usr/bin/env node
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { access, mkdir, readdir, stat, writeFile } from "node:fs/promises";
import { basename, extname, join } from "node:path";

const DEFAULT_ROOT = "var/kacky-discord-emotes";
const DEFAULT_SOURCE_DIR = "Emojis_The_Kacky_Discord (1)";
const DEFAULT_CDN_BASE = "https://cdn.kacky.gg";
const DEFAULT_RCLONE_REMOTE = "kacky-r2";
const DEFAULT_RCLONE_PATH = "kacky-cdn/emotes";
const EMOTE_PREFIX = "emotes";
const MEDIA_CACHE_CONTROL = "public, max-age=86400";
const MANIFEST_CACHE_CONTROL = "public, max-age=300";

function usage() {
  return `Usage:
  node scripts/build-emote-cdn.mjs
  node scripts/build-emote-cdn.mjs --execute

Options:
  --root <dir>            Archive root. Default: ${DEFAULT_ROOT}
  --source-dir <dir>      Source Discord GIF directory. Default: EMOTE_SOURCE_DIR or ${DEFAULT_SOURCE_DIR}
  --cdn-base <url>        Public CDN base. Default: CDN_BASE_URL or ${DEFAULT_CDN_BASE}
  --rclone-remote <name>  rclone remote. Default: RCLONE_REMOTE or ${DEFAULT_RCLONE_REMOTE}
  --rclone-path <path>    rclone bucket/path. Default: RCLONE_PATH or ${DEFAULT_RCLONE_PATH}
  --execute               Upload with rclone. Without this flag rclone runs with --dry-run.
  --self-test             Run script unit checks
`;
}

function parseArgs(argv) {
  const args = {
    root: DEFAULT_ROOT,
    sourceDir: process.env.EMOTE_SOURCE_DIR || DEFAULT_SOURCE_DIR,
    cdnBase: trimTrailingSlash(process.env.CDN_BASE_URL || DEFAULT_CDN_BASE),
    rcloneRemote: process.env.RCLONE_REMOTE || DEFAULT_RCLONE_REMOTE,
    rclonePath: trimSlashes(process.env.RCLONE_PATH || DEFAULT_RCLONE_PATH),
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
    else if (arg === "--source-dir") args.sourceDir = next();
    else if (arg === "--cdn-base") args.cdnBase = trimTrailingSlash(next());
    else if (arg === "--rclone-remote") args.rcloneRemote = next();
    else if (arg === "--rclone-path") args.rclonePath = trimSlashes(next());
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

function trimSlashes(value) {
  return value.replace(/^\/+|\/+$/g, "");
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

async function runInherited(command, args) {
  return await new Promise((resolve) => {
    const child = spawn(command, args, { stdio: "inherit" });
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

async function scanGifFiles(sourceDir) {
  const files = await readdir(sourceDir);
  return files
    .filter((file) => extname(file).toLowerCase() === ".gif")
    .sort((a, b) => a.localeCompare(b))
    .map((file) => ({
      name: basename(file, extname(file)),
      path: join(sourceDir, file),
      file,
    }));
}

async function pathExists(path) {
  try {
    await access(path);
    return true;
  } catch {
    return false;
  }
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

async function convertGifToVp9Alpha(inputPath, outputPath) {
  return await runCommand("ffmpeg", [
    "-y",
    "-i", inputPath,
    "-c:v", "libvpx-vp9",
    "-crf", "10",
    "-b:v", "0",
    "-pix_fmt", "yuva420p",
    "-auto-alt-ref", "0",
    outputPath,
  ]);
}

async function generateStaticFallback(inputPath, outputPath) {
  return await runCommand("ffmpeg", [
    "-y",
    "-i", inputPath,
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

function conversionCommandText() {
  return commandText("ffmpeg", [
    "-y",
    "-i", "<source.gif>",
    "-c:v", "libvpx-vp9",
    "-crf", "10",
    "-b:v", "0",
    "-pix_fmt", "yuva420p",
    "-auto-alt-ref", "0",
    "<name>.webm",
  ]);
}

async function fileSize(path) {
  return (await stat(path)).size;
}

function formatBytes(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KiB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MiB`;
}

async function uploadPlan(root, emotes, manifestPath) {
  const webmCount = emotes.length;
  const pngCount = emotes.filter((emote) => emote.staticOk).length;
  const totalSize = await dirExtensionSize(join(root, "animated-webm"), ".webm")
    + await dirExtensionSize(join(root, "static"), ".png")
    + await fileSize(manifestPath);
  return { webmCount, pngCount, manifestCount: 1, objectCount: webmCount + pngCount + 1, totalSize };
}

async function dirExtensionSize(dir, extension) {
  const files = await readdir(dir);
  let total = 0;
  for (const file of files.filter((item) => extname(item).toLowerCase() === extension)) {
    total += await fileSize(join(dir, file));
  }
  return total;
}

function rcloneTarget(remote, path) {
  return `${remote}:${trimSlashes(path)}/`;
}

function rcloneCopyArgs(source, target, execute, cacheControl, contentType, extra = []) {
  return [
    "copy",
    source,
    target,
    ...(execute ? [] : ["--dry-run"]),
    "--retries", "1",
    "--no-traverse",
    "--header-upload", `Cache-Control: ${cacheControl}`,
    "--header-upload", `Content-Type: ${contentType}`,
    ...extra,
  ];
}

function rcloneCommands(root, manifestPath, args) {
  const target = rcloneTarget(args.rcloneRemote, args.rclonePath);
  return [
    rcloneCopyArgs(join(root, "animated-webm"), target, args.execute, MEDIA_CACHE_CONTROL, "video/webm", ["--include", "*.webm"]),
    rcloneCopyArgs(join(root, "static"), target, args.execute, MEDIA_CACHE_CONTROL, "image/png", ["--include", "*.png"]),
    rcloneCopyArgs(root, target, args.execute, MANIFEST_CACHE_CONTROL, "application/json", ["--include", "manifest.json"]),
  ];
}

async function hasRcloneRemote(remote) {
  const version = await runCommand("rclone", ["version"]);
  if (!version.ok) {
    return { ok: false, reason: "rclone is not installed or not available on PATH" };
  }

  const remotes = await runCommand("rclone", ["listremotes"]);
  if (!remotes.ok) {
    return { ok: false, reason: `rclone listremotes failed: ${remotes.stderr.trim() || `exit ${remotes.code}`}` };
  }

  const remoteName = `${remote}:`;
  const exists = remotes.stdout.split(/\r?\n/).includes(remoteName);
  if (!exists) return { ok: false, reason: `rclone remote '${remote}' is not configured` };
  return { ok: true, reason: "" };
}

async function uploadOrDryRun(root, emotes, manifestPath, args) {
  const plan = await uploadPlan(root, emotes, manifestPath);
  console.log("");
  console.log(`${args.execute ? "UPLOAD" : "DRY-RUN"} rclone plan: ${plan.objectCount} object(s), ${formatBytes(plan.totalSize)} total`);
  console.log(`  ${plan.webmCount} webm -> ${rcloneTarget(args.rcloneRemote, args.rclonePath)}`);
  console.log(`  ${plan.pngCount} png -> ${rcloneTarget(args.rcloneRemote, args.rclonePath)}`);
  console.log(`  ${plan.manifestCount} manifest -> ${rcloneTarget(args.rcloneRemote, args.rclonePath)}`);

  const availability = await hasRcloneRemote(args.rcloneRemote);
  if (!availability.ok) {
    console.log(`rclone upload skipped: ${availability.reason}.`);
    return { uploaded: false, skipped: true };
  }

  const commands = rcloneCommands(root, manifestPath, args);
  console.log("");
  console.log("rclone commands:");
  for (const command of commands) console.log(`  ${commandText("rclone", command)}`);

  for (const command of commands) {
    const result = await runInherited("rclone", command);
    if (!result.ok) {
      if (!args.execute) {
        console.log(`rclone dry-run failed: ${result.error?.message ?? `exit ${result.code}`}. Upload skipped.`);
        return { uploaded: false, skipped: true };
      }
      throw new Error(`rclone copy failed: ${result.error?.message ?? `exit ${result.code}`}`);
    }
  }

  if (!args.execute) console.log("Dry-run only. Re-run with --execute to upload.");
  return { uploaded: args.execute, skipped: false };
}

async function buildFromGifSources(args, animatedDir, staticDir, logsDir) {
  const gifs = await scanGifFiles(args.sourceDir);
  if (gifs.length === 0) throw new Error(`No .gif files found in ${args.sourceDir}`);
  console.log(`Source GIFs: ${args.sourceDir} (${gifs.length} file(s))`);
  console.log(`VP9 alpha recipe: ${conversionCommandText()}`);
  const entries = [];
  const processed = [];
  const failures = [];

  for (const gif of gifs) {
    const animatedPath = join(animatedDir, `${gif.name}.webm`);
    const staticPath = join(staticDir, `${gif.name}.png`);
    let dimensions;

    try {
      dimensions = await probeDimensions(gif.path);
    } catch (error) {
      console.log(`failed ${gif.name}: ${error.message}`);
      failures.push({ name: gif.name, error: error.message });
      continue;
    }

    const animatedResult = await convertGifToVp9Alpha(gif.path, animatedPath);
    const staticResult = await generateStaticFallback(gif.path, staticPath);
    const animatedOk = animatedResult.ok;
    const staticOk = staticResult.ok;

    if (!animatedOk) {
      const error = animatedResult.stderr.trim() || `ffmpeg webm exited ${animatedResult.code}`;
      console.log(`failed ${gif.name} webm: ${error}`);
      failures.push({ name: gif.name, error: `webm: ${error}` });
    }

    if (!staticOk) {
      const error = staticResult.stderr.trim() || `ffmpeg png exited ${staticResult.code}`;
      console.log(`failed ${gif.name} png: ${error}`);
      failures.push({ name: gif.name, error: `png: ${error}` });
    }

    if (animatedOk && staticOk) {
      console.log(`ok ${gif.name}: ${dimensions.width}x${dimensions.height}`);
      entries.push(manifestEntry(gif.name, args.cdnBase, dimensions.width, dimensions.height));
    }

    processed.push({
      name: gif.name,
      animatedPath,
      staticPath,
      staticOk,
      width: dimensions.width,
      height: dimensions.height,
    });
  }

  await writeFailures(logsDir, failures);
  return { entries, processed, failures };
}

async function buildFromExistingWebms(args, animatedDir, staticDir, logsDir) {
  const files = await scanWebmFiles(animatedDir);
  if (files.length === 0) throw new Error(`No .webm files found in ${animatedDir}`);
  console.log(`Source GIF dir missing; keeping existing WebMs from ${animatedDir}`);
  const entries = [];
  const processed = [];
  const failures = [];

  for (const file of files) {
    const staticPath = join(staticDir, `${file.name}.png`);
    const dimensions = await probeDimensions(file.path);
    const result = await generateStaticFallback(file.path, staticPath);
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
  return { entries, processed, failures };
}

async function run(args) {
  const animatedDir = join(args.root, "animated-webm");
  const staticDir = join(args.root, "static");
  const logsDir = join(args.root, "logs");
  const manifestPath = join(args.root, "manifest.json");
  await mkdir(animatedDir, { recursive: true });
  await mkdir(staticDir, { recursive: true });

  const sourceDirExists = await pathExists(args.sourceDir);
  const { entries, processed, failures } = sourceDirExists
    ? await buildFromGifSources(args, animatedDir, staticDir, logsDir)
    : await buildFromExistingWebms(args, animatedDir, staticDir, logsDir);

  entries.sort((a, b) => a.name.localeCompare(b.name));
  await writeFile(manifestPath, JSON.stringify(entries, null, 2) + "\n");
  console.log(`Wrote ${manifestPath} (${entries.length} entries)`);
  console.log(`Wrote ${join(logsDir, "convert-failures.txt")} (${failures.length} failure(s))`);

  await uploadOrDryRun(args.root, processed, manifestPath, args);

  console.log("");
  console.log(`Processed ${processed.length} emote(s): ${entries.length} static fallback(s), ${failures.length} failure(s)`);
  console.log(`Media upload Cache-Control: ${MEDIA_CACHE_CONTROL}`);
  console.log("After a real --execute upload, purge Cloudflare cache for /emotes/* so edge nodes drop old immutable objects.");
  console.log("WebM has no real loop flag; verify in-game that the client loops the animated quad.");
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
  assert.equal(trimSlashes("/kacky-cdn/emotes/"), "kacky-cdn/emotes");
  assert.equal(rcloneTarget("kacky-r2", "kacky-cdn/emotes"), "kacky-r2:kacky-cdn/emotes/");
  assert.equal(conversionCommandText(), "ffmpeg -y -i '<source.gif>' -c:v libvpx-vp9 -crf 10 -b:v 0 -pix_fmt yuva420p -auto-alt-ref 0 '<name>.webm'");
  assert.deepEqual(rcloneCopyArgs("static", "kacky-r2:kacky-cdn/emotes/", false, MEDIA_CACHE_CONTROL, "image/png", ["--include", "*.png"]), [
    "copy",
    "static",
    "kacky-r2:kacky-cdn/emotes/",
    "--dry-run",
    "--retries",
    "1",
    "--no-traverse",
    "--header-upload",
    "Cache-Control: public, max-age=86400",
    "--header-upload",
    "Content-Type: image/png",
    "--include",
    "*.png",
  ]);
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
