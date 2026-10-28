import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const ignoredDirectories = new Set([
  ".git", "node_modules", "bin", "obj", ".vs", "artifacts", "TestResults",
]);
const markdownFiles = [];
let approximateWordCount = 0;
const filePaths = new Set();
const directoryPaths = new Set(["."]);

function walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (entry.isSymbolicLink()) continue;
    const absolutePath = path.join(directory, entry.name);
    const relativePath = path.relative(root, absolutePath).split(path.sep).join("/");
    if (entry.isDirectory()) {
      if (ignoredDirectories.has(entry.name)) continue;
      directoryPaths.add(relativePath);
      walk(absolutePath);
      continue;
    }
    if (!entry.isFile()) continue;
    filePaths.add(relativePath);
    if (path.extname(entry.name).toLowerCase() === ".md") {
      markdownFiles.push({ absolutePath, relativePath });
    }
  }
}

function removeFencedCode(content, relativePath, diagnostics) {
  const lines = content.split(/\r?\n/);
  const result = [];
  let fence = null;
  for (let index = 0; index < lines.length; index += 1) {
    const marker = lines[index].match(/^ {0,3}([\x60]{3,}|~{3,})(.*)$/);
    if (!fence && marker) {
      fence = { character: marker[1][0], length: marker[1].length, line: index + 1 };
      result.push("");
      continue;
    }
    if (fence) {
      const close = lines[index].match(/^ {0,3}([\x60]+|~+)\s*$/);
      if (close && close[1][0] === fence.character && close[1].length >= fence.length) {
        fence = null;
      }
      result.push("");
      continue;
    }
    result.push(lines[index]);
  }
  if (fence) diagnostics.push({ type: "unclosed code fence", path: relativePath, line: fence.line });
  return result.join("\n");
}

function decodeMarkdownPath(value) {
  let result = value.replace(/\\([\\ ()])/g, "$1");
  try {
    result = decodeURIComponent(result);
  } catch {
    // Keep malformed percent-encoding literal so it is reported as a missing path.
  }
  return result.replaceAll("\\", "/");
}

function resolveLocalTarget(fromFile, target) {
  const trimmed = target.trim();
  if (!trimmed || trimmed.startsWith("#") || trimmed.startsWith("//") ||
      /^[a-z][a-z\d+.-]*:/i.test(trimmed)) return null;

  const pathOnly = trimmed.split(/[?#]/, 1)[0];
  if (!pathOnly) return null;
  const decoded = decodeMarkdownPath(pathOnly);
  const joined = decoded.startsWith("/")
    ? decoded.slice(1)
    : path.posix.join(path.posix.dirname(fromFile), decoded);
  const normalized = path.posix.normalize(joined);
  return normalized.length > 1 && normalized.endsWith("/")
    ? normalized.slice(0, -1)
    : normalized;
}

function checkMarkdownLinks(content, relativePath, diagnostics) {
  const withoutFences = removeFencedCode(content, relativePath, diagnostics);
  const prose = withoutFences.replace(/([\x60]+)([\s\S]*?)\1/g, (match) =>
    match.replace(/[^\n]/g, " "));

  const linkPattern =
    /!?\[[^\]\n]*\]\(\s*(<[^>\n]+>|(?:\\.|[^)\s])+)(?:\s+(?:"[^"\n]*"|'[^'\n]*'|\([^) \n]*\)))?\s*\)/g;
  let match;
  while ((match = linkPattern.exec(prose)) !== null) {
    const target = match[1].replace(/^<|>$/g, "");
    const line = prose.slice(0, match.index).split("\n").length;
    const resolved = resolveLocalTarget(relativePath, target);
    if (resolved === null) continue;
    if (filePaths.has(resolved) || directoryPaths.has(resolved)) continue;
    diagnostics.push({
      type: "broken relative link", path: relativePath, line, target, resolved,
    });
  }
}

const diagnostics = [];
walk(root);
markdownFiles.sort((a, b) => a.relativePath.localeCompare(b.relativePath));

for (const file of markdownFiles) {
  const content = fs.readFileSync(file.absolutePath, "utf8");
  if (!content.trim()) {
    diagnostics.push({ type: "empty Markdown file", path: file.relativePath, line: 1 });
    continue;
  }
  approximateWordCount += (content.match(/[\p{L}\p{N}][\p{L}\p{N}’'_-]*/gu) || []).length;
  checkMarkdownLinks(content, file.relativePath, diagnostics);
}

const brokenLinks = diagnostics.filter((item) => item.type === "broken relative link");
const unclosedFences = diagnostics.filter((item) => item.type === "unclosed code fence");
const emptyFiles = diagnostics.filter((item) => item.type === "empty Markdown file");

console.log([
  "Scrobblix documentation validation",
  "Markdown files scanned: " + markdownFiles.length,
  "Approximate Markdown words: " + approximateWordCount,
  "Broken relative links: " + brokenLinks.length,
  "Unclosed fenced code blocks: " + unclosedFences.length,
  "Empty Markdown files: " + emptyFiles.length,
].join("\n"));

if (diagnostics.length > 0) {
  for (const item of diagnostics) {
    const location = item.line ? item.path + ":" + item.line : item.path;
    const detail = item.type === "broken relative link"
      ? ' -> "' + item.target + '" (resolved as "' + item.resolved + '")' : "";
    console.error("ERROR [" + item.type + "] " + location + detail);
  }
  process.exitCode = 1;
} else {
  console.log("Result: PASS (path-only link validation)");
}
