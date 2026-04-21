import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdir, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { atlasCatalogByPath, repoRoot, toCanonicalHref } from '../src/data/atlas-data.mjs';

// DOCS: ../../../docs/wiki/README.md#navegacao-rapida-por-tarefa
// DOCS: ../../../docs/handoffs/2026-04-20-mica-atlas-structural-site.md

const siteRoot = resolveHere('..');
const wikiRoot = path.resolve(repoRoot, 'docs/wiki');
const generatedDocsRoot = path.resolve(siteRoot, 'src/content/docs/docs');
const generatedCanonicalRoot = path.resolve(siteRoot, 'public/canonical/wiki');
const atlasDocMetaComponent = path.resolve(siteRoot, 'src/components/AtlasDocMeta.astro');

const githubBaseUrl = normalizeGithubRemoteUrl(runGit('remote get-url origin'));
const githubRef = runGit('rev-parse HEAD');

await rm(generatedDocsRoot, { recursive: true, force: true });
await rm(generatedCanonicalRoot, { recursive: true, force: true });

const markdownFiles = await walkMarkdownFiles(wikiRoot);
for (const absoluteSourceFile of markdownFiles) {
	const repoRelativeSourcePath = toRepoRelativePath(absoluteSourceFile);
	const sourceRelativeToWiki = path.relative(wikiRoot, absoluteSourceFile).replaceAll('\\', '/');
	const targetDocFile = path.resolve(
		generatedDocsRoot,
		sourceRelativeToWiki.replace(/\.md$/u, '.mdx')
	);
	const targetCanonicalFile = path.resolve(generatedCanonicalRoot, sourceRelativeToWiki);
	const rawMarkdown = await readFile(absoluteSourceFile, 'utf8');
	const catalogEntry = atlasCatalogByPath.get(repoRelativeSourcePath);
	const title = catalogEntry?.title ?? extractTitle(rawMarkdown, sourceRelativeToWiki);
	const markdownBody = stripLeadingHeading(rawMarkdown);
	const rewrittenBody = sanitizeForMdx(rewriteLinks(markdownBody, absoluteSourceFile));
	const description = catalogEntry?.summary ?? extractDescription(markdownBody, title);
	const componentImportPath = toPosixPath(
		path.relative(path.dirname(targetDocFile), atlasDocMetaComponent)
	);

	const generatedMdx = [
		'---',
		`title: ${toYamlString(title)}`,
		`description: ${toYamlString(description)}`,
		'template: doc',
		'editUrl: false',
		'prev: false',
		'next: false',
		'sidebar:',
		'  hidden: true',
		'---',
		`import AtlasDocMeta from '${componentImportPath}';`,
		'',
		'<AtlasDocMeta',
		`  capability={${toPropValue(catalogEntry?.capability)}}`,
		`  kind={${toPropValue(catalogEntry?.kind)}}`,
		`  sourceHref={${toPropValue(toCanonicalHref(repoRelativeSourcePath))}}`,
		`  sourcePath={${toPropValue(repoRelativeSourcePath)}}`,
		`  status={${toPropValue(catalogEntry?.status)}}`,
		`  summary={${toPropValue(catalogEntry?.summary)}}`,
		'/>',
		'',
		rewrittenBody.trim(),
		'',
	].join('\n');

	await mkdir(path.dirname(targetDocFile), { recursive: true });
	await mkdir(path.dirname(targetCanonicalFile), { recursive: true });
	await writeFile(targetDocFile, generatedMdx, 'utf8');
	await writeFile(targetCanonicalFile, rawMarkdown, 'utf8');
}

function resolveHere(relativePath) {
	return path.resolve(fileURLToPath(new URL(relativePath, import.meta.url)));
}

function runGit(command) {
	try {
		return execSync(`git ${command}`, {
			cwd: repoRoot,
			stdio: ['ignore', 'pipe', 'ignore'],
		})
			.toString('utf8')
			.trim();
	} catch {
		return '';
	}
}

function normalizeGithubRemoteUrl(remoteUrl) {
	if (!remoteUrl) {
		return '';
	}

	const trimmed = remoteUrl.trim();
	if (trimmed.startsWith('https://github.com/')) {
		return trimmed.replace(/\.git$/u, '');
	}

	const sshMatch = /^git@github\.com:(.+?)(?:\.git)?$/u.exec(trimmed);
	if (sshMatch) {
		return `https://github.com/${sshMatch[1]}`;
	}

	return '';
}

async function walkMarkdownFiles(rootDirectory) {
	const entries = await readdir(rootDirectory, { withFileTypes: true });
	const files = [];

	for (const entry of entries) {
		const absolutePath = path.resolve(rootDirectory, entry.name);
		if (entry.isDirectory()) {
			files.push(...(await walkMarkdownFiles(absolutePath)));
			continue;
		}

		if (entry.isFile() && absolutePath.endsWith('.md')) {
			files.push(absolutePath);
		}
	}

	return files.sort((left, right) => left.localeCompare(right));
}

function toRepoRelativePath(absolutePath) {
	return toPosixPath(path.relative(repoRoot, absolutePath));
}

function toPosixPath(value) {
	return value.replaceAll('\\', '/');
}

function extractTitle(rawMarkdown, fallback) {
	const headingMatch = rawMarkdown.match(/^#\s+(.+)$/mu);
	if (headingMatch) {
		return headingMatch[1].trim();
	}

	return fallback.replace(/\.md$/u, '');
}

function stripLeadingHeading(rawMarkdown) {
	return rawMarkdown.replace(/^\uFEFF?#\s+.+?(?:\r?\n){1,2}/u, '').trim();
}

function extractDescription(markdownBody, fallback) {
	const paragraphs = markdownBody
		.split(/\r?\n\r?\n/u)
		.map((paragraph) => paragraph.trim())
		.filter(Boolean)
		.filter((paragraph) => {
			return (
				!paragraph.startsWith('```') &&
				!paragraph.startsWith('- ') &&
				!paragraph.startsWith('1. ') &&
				!paragraph.startsWith('|') &&
				!paragraph.startsWith('#')
			);
		});

	if (paragraphs.length === 0) {
		return fallback;
	}

	return paragraphs[0].replace(/\s+/gu, ' ').slice(0, 180);
}

function toYamlString(value) {
	return JSON.stringify(value);
}

function toPropValue(value) {
	return value === undefined ? 'null' : JSON.stringify(value);
}

function rewriteLinks(markdownBody, absoluteSourceFile) {
	return markdownBody.replace(/(!?)\[([^\]]+)\]\(([^)\s]+)([^)]*)\)/gu, (match, bang, label, rawTarget, suffix) => {
		if (bang === '!') {
			return match;
		}

		if (!rawTarget || rawTarget.startsWith('#') || /^[a-z]+:/iu.test(rawTarget)) {
			return match;
		}

		const [targetWithoutHash, anchor = ''] = rawTarget.split('#');
		const resolvedTarget = path.resolve(path.dirname(absoluteSourceFile), targetWithoutHash);
		if (!existsSync(resolvedTarget)) {
			return match;
		}

		if (isInside(resolvedTarget, wikiRoot) && resolvedTarget.endsWith('.md')) {
			const wikiRelative = toPosixPath(path.relative(wikiRoot, resolvedTarget)).replace(/\.md$/u, '');
			const rewritten = `/docs/${wikiRelative}/${anchor ? `#${anchor}` : ''}`;
			return `[${label}](${rewritten}${suffix})`;
		}

		if (isInside(resolvedTarget, repoRoot) && githubBaseUrl && githubRef) {
			const repoRelative = toRepoRelativePath(resolvedTarget);
			const rewritten = `${githubBaseUrl}/blob/${githubRef}/${repoRelative}${anchor ? `#${anchor}` : ''}`;
			return `[${label}](${rewritten}${suffix})`;
		}

		return match;
	});
}

function isInside(candidatePath, rootPath) {
	const relative = path.relative(rootPath, candidatePath);
	return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}

function sanitizeForMdx(markdownBody) {
	const lines = markdownBody.split(/\r?\n/u);
	let inFence = false;

	return lines
		.map((line) => {
			if (/^\s*```/u.test(line)) {
				inFence = !inFence;
				return line;
			}

			if (inFence) {
				return line;
			}

			let inInlineCode = false;
			let output = '';
			for (const char of line) {
				if (char === '`') {
					inInlineCode = !inInlineCode;
					output += char;
					continue;
				}

				if (!inInlineCode && char === '<') {
					output += '&lt;';
					continue;
				}

				if (!inInlineCode && char === '>') {
					output += '&gt;';
					continue;
				}

				output += char;
			}

			return output;
		})
		.join('\n');
}
