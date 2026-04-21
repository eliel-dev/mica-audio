import { z } from 'zod';

export const ATLAS_STATUSES = ['implemented', 'partial', 'future', 'checklist'];
export const ATLAS_CAPABILITIES = [
	'firmware-boards',
	'onboarding-pairing',
	'panels-widgets',
	'observability',
	'cloud-future',
];
export const ATLAS_KINDS = ['architecture', 'module', 'guide', 'reference', 'future-implementation'];

export const STATUS_META = {
	implemented: { label: 'Implementado', tone: 'implemented' },
	partial: { label: 'Parcial', tone: 'partial' },
	future: { label: 'Futuro', tone: 'future' },
	checklist: { label: 'Checklist', tone: 'checklist' },
};

export const KIND_META = {
	architecture: { label: 'Arquitetura' },
	module: { label: 'Modulo' },
	guide: { label: 'Guia' },
	reference: { label: 'Referencia' },
	'future-implementation': { label: 'Futura implementacao' },
};

export const CAPABILITY_META = {
	'firmware-boards': {
		label: 'Firmware e Boards',
		shortLabel: 'Boards',
		description:
			'Perfis de firmware, artefatos oficiais, matriz de boards e limites do runtime HUB75.',
		focus: [
			'Firmware oficial por perfil e geometria de painel.',
			'Catalogo de builds previsivel para DevKitC-1 e futuras variantes.',
		],
	},
	'onboarding-pairing': {
		label: 'Onboarding e Pairing',
		shortLabel: 'Pairing',
		description:
			'Setup de novos devices, provisioning, pareamento e reconexao ao control plane.',
		focus: [
			'Separar flash manual, pair code e claim de device.',
			'Manter onboarding previsivel em USB e AP portal.',
		],
	},
	'panels-widgets': {
		label: 'Paineis e Widgets',
		shortLabel: 'Paineis',
		description:
			'Composicao de paineis HUB75 com widgets sobrepostos, playback e pipeline de midia.',
		focus: [
			'Widget como unidade composicional configuravel.',
			'Preview, batches e resize orientados ao painel alvo.',
		],
	},
	observability: {
		label: 'Observabilidade',
		shortLabel: 'Obs',
		description:
			'Telemetria do app e do device, dashboard nativo, diagnostico e instrumentacao de bancada.',
		focus: [
			'Separar observabilidade estruturada de ferramentas auxiliares.',
			'Manter diagnostico local sem contaminar o runtime oficial.',
		],
	},
	'cloud-future': {
		label: 'Cloud Future',
		shortLabel: 'Cloud',
		description:
			'Arquitetura alvo cloud-first, multi-board e multi-panel, com clientes remotos e catalogo curado.',
		focus: [
			'Server + firmware + clients como estado alvo.',
			'Widgets cloud-safe separados do realtime client-owned.',
		],
	},
};

export const atlasCatalogEntrySchema = z.object({
	id: z.string().min(1),
	sourcePath: z.string().startsWith('docs/wiki/').endsWith('.md'),
	title: z.string().min(1),
	kind: z.enum(ATLAS_KINDS),
	capability: z.enum(ATLAS_CAPABILITIES),
	status: z.enum(ATLAS_STATUSES),
	priority: z.number().int().min(1).max(4),
	summary: z.string().min(1),
	tags: z.array(z.string().min(1)).min(1),
	showInV1: z.boolean(),
	gaps: z.array(z.string().min(1)).optional(),
	nextSteps: z.array(z.string().min(1)).optional(),
});

export const atlasCatalogSchema = z.array(atlasCatalogEntrySchema).superRefine((entries, ctx) => {
	const ids = new Set();
	const paths = new Set();

	for (const [index, entry] of entries.entries()) {
		if (ids.has(entry.id)) {
			ctx.addIssue({
				code: z.ZodIssueCode.custom,
				message: `Catalog id duplicado: ${entry.id}`,
				path: [index, 'id'],
			});
		}

		if (paths.has(entry.sourcePath)) {
			ctx.addIssue({
				code: z.ZodIssueCode.custom,
				message: `Catalog sourcePath duplicado: ${entry.sourcePath}`,
				path: [index, 'sourcePath'],
			});
		}

		ids.add(entry.id);
		paths.add(entry.sourcePath);
	}
});
