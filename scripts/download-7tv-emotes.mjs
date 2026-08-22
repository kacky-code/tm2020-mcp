#!/usr/bin/env node
import assert from "node:assert/strict";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { basename, join } from "node:path";

const DEFAULT_CDN_BASE = "https://cdn.7tv.app/emote";
const DEFAULT_API_BASE = "https://7tv.io/v3";
const DEFAULT_FORMATS = ["webp", "png", "gif"];
const DEFAULT_SIZES = ["3x"];

function usage() {
  return `Usage:
  node scripts/download-7tv-emotes.mjs --input examples/emoji/7tv-emotes.sample.json --out var/7tv-emotes
  node scripts/download-7tv-emotes.mjs --ids catJAM=01F6MQ33FG000FFJ97ZB8MWV52 --dry-run
  node scripts/download-7tv-emotes.mjs --emote-set global --out var/7tv-global

Options:
  --input <file>       JSON array or {"emotes":[...]} with {name,id}
  --ids <items>        Comma-separated name=id or id entries
  --emote-set <id>     7TV emote set id, or "global"
  --out <dir>          Output directory. Default: var/7tv-emotes
  --sizes <list>       Comma-separated sizes. Default: 3x
  --formats <list>     Comma-separated formats. Default: webp,png,gif
  --cdn-base <url>     CDN base. Default: ${DEFAULT_CDN_BASE}
  --api-base <url>     API base. Default: ${DEFAULT_API_BASE}
  --cdn-output <url>   Optional final CDN base for generated manifest animatedUrl
  --dry-run            Write manifest only; do not download assets
  --self-test          Run script unit checks
`;
}

function parseArgs(argv) {
  const args = {
    input: "",
    ids: "",
    emoteSet: "",
    out: "var/7tv-emotes",
    sizes: DEFAULT_SIZES,
    formats: DEFAULT_FORMATS,
    cdnBase: DEFAULT_CDN_BASE,
    apiBase: DEFAULT_API_BASE,
    cdnOutput: "",
    dryRun: false,
    selfTest: false,
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const next = () => {
      if (i + 1 >= argv.length) throw new Error(`Missing value for ${arg}`);
      return argv[++i];
    };

    if (arg === "--input") args.input = next();
    else if (arg === "--ids") args.ids = next();
    else if (arg === "--emote-set") args.emoteSet = next();
    else if (arg === "--out") args.out = next();
    else if (arg === "--sizes") args.sizes = splitList(next());
    else if (arg === "--formats") args.formats = splitList(next()).map((v) => v.replace(/^\./, ""));
    else if (arg === "--cdn-base") args.cdnBase = trimTrailingSlash(next());
    else if (arg === "--api-base") args.apiBase = trimTrailingSlash(next());
    else if (arg === "--cdn-output") args.cdnOutput = trimTrailingSlash(next());
    else if (arg === "--dry-run") args.dryRun = true;
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

function splitList(value) {
  return value.split(",").map((item) => item.trim()).filter(Boolean);
}

function trimTrailingSlash(value) {
  return value.replace(/\/+$/, "");
}

function sanitizeName(value) {
  return value.trim().replace(/[^A-Za-z0-9_.-]+/g, "_").replace(/^_+|_+$/g, "") || "emote";
}

function normalizeEmote(raw) {
  if (typeof raw === "string") {
    return { id: raw, name: raw };
  }

  const id = raw.id ?? raw.emoteId ?? raw.sourceId ?? raw.data?.id;
  const name = raw.name ?? raw.defaultName ?? raw.alias ?? raw.data?.name ?? id;
  const animated = raw.animated ?? raw.data?.animated ?? raw.flags?.animated;

  if (!id) throw new Error(`Emote is missing id: ${JSON.stringify(raw)}`);
  return {
    id: String(id),
    name: String(name ?? id),
    animated: typeof animated === "boolean" ? animated : undefined,
  };
}

function parseIds(value) {
  if (!value) return [];
  return splitList(value).map((item) => {
    const eq = item.indexOf("=");
    if (eq === -1) return normalizeEmote(item);
    return normalizeEmote({ name: item.slice(0, eq), id: item.slice(eq + 1) });
  });
}

function assetUrl(cdnBase, id, size, format) {
  return `${trimTrailingSlash(cdnBase)}/${encodeURIComponent(id)}/${encodeURIComponent(size)}.${format}`;
}

function assetFileName(emote, size, format) {
  return `${sanitizeName(emote.name)}__${emote.id}__${size}.${format}`;
}

function manifestEntry(emote, files, cdnOutput) {
  const webp = files.find((file) => file.size === "3x" && file.format === "webp")
    ?? files.find((file) => file.format === "webp")
    ?? files[0];
  return {
    name: emote.name,
    source: "7tv",
    sourceId: emote.id,
    animated: emote.animated,
    staticUrl: webp?.sourceUrl,
    downloaded: files,
    animatedUrl: cdnOutput ? `${trimTrailingSlash(cdnOutput)}/${emote.id}.webm` : undefined,
    convertOutput: `converted/${emote.id}.webm`,
  };
}

async function loadInputFile(path) {
  if (!path) return [];
  const parsed = JSON.parse(await readFile(path, "utf8"));
  const rawEmotes = Array.isArray(parsed) ? parsed : parsed.emotes;
  if (!Array.isArray(rawEmotes)) {
    throw new Error(`Input JSON must be an array or contain an "emotes" array: ${path}`);
  }
  return rawEmotes.map(normalizeEmote);
}

async function loadEmoteSet(apiBase, emoteSet) {
  if (!emoteSet) return [];
  const path = emoteSet === "global" ? "emote-sets/global" : `emote-sets/${encodeURIComponent(emoteSet)}`;
  const data = await fetchJson(`${trimTrailingSlash(apiBase)}/${path}`);
  if (!Array.isArray(data.emotes)) {
    throw new Error(`7TV emote set response did not contain an emotes array`);
  }
  return data.emotes.map((item) => normalizeEmote({
    id: item.id ?? item.data?.id,
    name: item.name ?? item.data?.name,
    animated: item.data?.animated,
  }));
}

async function fetchJson(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`GET ${url} failed: ${response.status} ${response.statusText}`);
  return await response.json();
}

async function downloadFile(url, path) {
  const response = await fetch(url);
  if (!response.ok) {
    return { ok: false, status: response.status, statusText: response.statusText };
  }

  const bytes = new Uint8Array(await response.arrayBuffer());
  await writeFile(path, bytes);
  return {
    ok: true,
    bytes: bytes.length,
    contentType: response.headers.get("content-type") ?? "",
  };
}

async function collectEmotes(args) {
  const all = [
    ...(await loadInputFile(args.input)),
    ...parseIds(args.ids),
    ...(await loadEmoteSet(args.apiBase, args.emoteSet)),
  ];
  const byId = new Map();
  for (const emote of all) byId.set(emote.id, emote);
  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
}

async function run(args) {
  const emotes = await collectEmotes(args);
  if (emotes.length === 0) {
    throw new Error("No emotes provided. Use --input, --ids, or --emote-set.");
  }

  const assetsDir = join(args.out, "source");
  await mkdir(assetsDir, { recursive: true });
  await mkdir(join(args.out, "converted"), { recursive: true });

  const entries = [];
  for (const emote of emotes) {
    const files = [];
    console.log(`Emote ${emote.name} (${emote.id})`);

    for (const size of args.sizes) {
      for (const format of args.formats) {
        const sourceUrl = assetUrl(args.cdnBase, emote.id, size, format);
        const fileName = assetFileName(emote, size, format);
        const outputPath = join(assetsDir, fileName);
        const record = { size, format, sourceUrl, file: `source/${fileName}` };

        if (args.dryRun) {
          record.status = "dry-run";
          console.log(`  would fetch ${sourceUrl}`);
        } else {
          const result = await downloadFile(sourceUrl, outputPath);
          if (result.ok) {
            record.status = "downloaded";
            record.bytes = result.bytes;
            record.contentType = result.contentType;
            console.log(`  ok ${basename(outputPath)} (${result.bytes} bytes)`);
          } else {
            record.status = "failed";
            record.httpStatus = result.status;
            record.httpStatusText = result.statusText;
            console.log(`  skip ${sourceUrl} (${result.status})`);
          }
        }
        files.push(record);
      }
    }

    entries.push(manifestEntry(emote, files, args.cdnOutput));
  }

  const manifest = {
    generatedAt: new Date().toISOString(),
    source: "7tv",
    sizes: args.sizes,
    formats: args.formats,
    emotes: entries,
  };
  await writeFile(join(args.out, "manifest.json"), JSON.stringify(manifest, null, 2) + "\n");
  await writeFile(join(args.out, "convert-vp8.sh"), conversionScript(entries));
  console.log(`Wrote ${join(args.out, "manifest.json")}`);
  console.log(`Wrote ${join(args.out, "convert-vp8.sh")}`);
}

function conversionScript(entries) {
  const lines = [
    "#!/usr/bin/env bash",
    "set -euo pipefail",
    'root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"',
    'mkdir -p "$root/converted"',
    "",
  ];

  for (const entry of entries) {
    const preferred = preferredConversionInput(entry);
    const input = preferred?.file ?? entry.downloaded[0]?.file;
    if (!input) continue;
    lines.push(`echo "Converting ${entry.name} (${entry.sourceId})"`);
    lines.push("ffmpeg -y \\");
    lines.push(`  -i "$root/${input}" \\`);
    lines.push('  -vf "scale=128:128:force_original_aspect_ratio=decrease,pad=128:128:(ow-iw)/2:(oh-ih)/2:color=black" \\');
    lines.push("  -c:v libvpx \\");
    lines.push("  -pix_fmt yuv420p \\");
    lines.push("  -r 60 \\");
    lines.push("  -an \\");
    lines.push(`  "$root/${entry.convertOutput}"`);
    lines.push("");
  }

  return lines.join("\n");
}

function preferredConversionInput(entry) {
  const usable = entry.downloaded.filter((file) => file.status === "downloaded" || file.status === "dry-run");
  if (entry.animated) {
    return usable.find((file) => file.format === "gif")
      ?? usable.find((file) => file.format === "png")
      ?? usable.find((file) => file.format === "webp")
      ?? usable[0];
  }

  return usable.find((file) => file.format === "png")
    ?? usable.find((file) => file.format === "webp")
    ?? usable.find((file) => file.format === "gif")
    ?? usable[0];
}

function selfTest() {
  const emote = normalizeEmote({ name: "cat JAM", id: "01F6", animated: true });
  assert.deepEqual(emote, { name: "cat JAM", id: "01F6", animated: true });
  assert.equal(assetUrl(DEFAULT_CDN_BASE, "01F6", "3x", "webp"), "https://cdn.7tv.app/emote/01F6/3x.webp");
  assert.equal(assetFileName(emote, "3x", "webp"), "cat_JAM__01F6__3x.webp");
  const entry = manifestEntry(emote, [{
    size: "3x",
    format: "webp",
    sourceUrl: "https://cdn.7tv.app/emote/01F6/3x.webp",
    file: "source/cat_JAM__01F6__3x.webp",
    status: "dry-run",
  }], "https://cdn.kacky.gg/emotes");
  assert.equal(entry.staticUrl, "https://cdn.7tv.app/emote/01F6/3x.webp");
  assert.equal(entry.animatedUrl, "https://cdn.kacky.gg/emotes/01F6.webm");
  assert.equal(parseIds("catJAM=abc,def")[0].name, "catJAM");
  assert.equal(preferredConversionInput({
    animated: true,
    downloaded: [
      { format: "webp", status: "downloaded", file: "source/a.webp" },
      { format: "gif", status: "downloaded", file: "source/a.gif" },
    ],
  }).file, "source/a.gif");
  assert.equal(preferredConversionInput({
    animated: false,
    downloaded: [
      { format: "webp", status: "downloaded", file: "source/a.webp" },
      { format: "png", status: "downloaded", file: "source/a.png" },
    ],
  }).file, "source/a.png");
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
