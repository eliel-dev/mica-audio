import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import atlasCatalogSource from './atlas-catalog-source.mjs';
import {
	ATLAS_STATUSES,
	ATLAS_CAPABILITIES,
	CAPABILITY_META,
	STATUS_META,
	KIND_META,
	atlasCatalogSchema,
} from './atlas-schema.mjs';

// DOCS: ../../../../docs/wiki/README.md#indice
// DOCS: ../../../../docs/handoffs/2026-04-20-mica-atlas-structural-site.md

export const repoRoot = resolve(process.cwd(), '../..');
export const wikiRoot = resolve(repoRoot, 'docs/wiki');

export const atlasCatalog = atlasCatalogSchema.parse(atlasCatalogSource).map((entry) => {
	const absoluteSourcePath = resolve(repoRoot, entry.sourcePath);
	if (!existsSync(absoluteSourcePath)) {
		throw new Error(`Atlas catalog aponta para um arquivo inexistente: ${entry.sourcePath}`);
	}

	return entry;
});

export const atlasCatalogByPath = new Map(atlasCatalog.map((entry) => [entry.sourcePath, entry]));

export function toDocSlug(sourcePath) {
	return sourcePath.replace(/^docs\/wiki\//, '').replace(/\.md$/u, '');
}

export function toDocHref(sourcePath) {
	return `/docs/${toDocSlug(sourcePath)}/`;
}

export function toCanonicalHref(sourcePath) {
	return `/canonical/wiki/${sourcePath.replace(/^docs\/wiki\//, '')}`;
}

export function sortCatalogEntries(entries) {
	return [...entries].sort((left, right) => {
		if (left.priority !== right.priority) {
			return left.priority - right.priority;
		}

		return left.title.localeCompare(right.title, 'pt-BR');
	});
}

export function getCapabilityEntries(capability) {
	return sortCatalogEntries(atlasCatalog.filter((entry) => entry.capability === capability));
}

export function getStatusEntries(status, { v1Only = true } = {}) {
	const source = v1Only ? v1Catalog : atlasCatalog;
	return sortCatalogEntries(source.filter((entry) => entry.status === status));
}

export const capabilityCards = ATLAS_CAPABILITIES.map((slug) => {
	const entries = getCapabilityEntries(slug);
	const counts = Object.fromEntries(
		Object.keys(STATUS_META).map((status) => [
			status,
			entries.filter((entry) => entry.status === status).length,
		])
	);

	return {
		slug,
		...CAPABILITY_META[slug],
		entries,
		counts,
	};
});

export const v1Catalog = sortCatalogEntries(atlasCatalog.filter((entry) => entry.showInV1));
export const futureCatalog = sortCatalogEntries(
	atlasCatalog.filter((entry) => entry.status === 'future' || entry.status === 'checklist')
);

export const statusCounts = Object.fromEntries(
	Object.keys(STATUS_META).map((status) => [
		status,
		v1Catalog.filter((entry) => entry.status === status).length,
	])
);

export const statusCards = ATLAS_STATUSES.map((status) => ({
	slug: status,
	href: `/status/${status}/`,
	...STATUS_META[status],
	count: statusCounts[status],
	entries: getStatusEntries(status),
}));

export { CAPABILITY_META, STATUS_META, KIND_META };
