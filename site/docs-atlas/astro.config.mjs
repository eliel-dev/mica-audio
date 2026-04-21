// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// DOCS: ../../docs/wiki/README.md#navegacao-rapida-por-tarefa
// DOCS: ../../docs/handoffs/2026-04-20-mica-atlas-structural-site.md
export default defineConfig({
	integrations: [
		starlight({
			title: 'Mica Atlas',
			description: 'Portal local-first para organizar a wiki tecnica do Mica Audio por capacidade e status.',
			head: [
				{
					tag: 'meta',
					attrs: {
						name: 'theme-color',
						content: '#0a0f14',
					},
				},
			],
			customCss: [
				'@fontsource/ibm-plex-sans/400.css',
				'@fontsource/ibm-plex-sans/500.css',
				'@fontsource/ibm-plex-sans/600.css',
				'@fontsource/ibm-plex-mono/400.css',
				'@fontsource/silkscreen/400.css',
				'/src/styles/atlas.css',
			],
			components: {
				Head: './src/components/AtlasHead.astro',
				ThemeSelect: './src/components/Empty.astro',
			},
			lastUpdated: false,
			pagination: false,
			credits: false,
			social: [],
			sidebar: [
				{
					label: 'Panorama',
					items: [
						{ label: 'Home', link: '/' },
						{ label: 'Futuro', link: '/future/' },
					],
				},
				{
					label: 'Status',
					items: [
						{ label: 'Implementado', link: '/status/implemented/' },
						{ label: 'Parcial', link: '/status/partial/' },
						{ label: 'Futuro', link: '/status/future/' },
						{ label: 'Checklist', link: '/status/checklist/' },
					],
				},
				{
					label: 'Capacidades',
					items: [
						{ label: 'Firmware e Boards', link: '/capabilities/firmware-boards/' },
						{ label: 'Onboarding e Pairing', link: '/capabilities/onboarding-pairing/' },
						{ label: 'Paineis e Widgets', link: '/capabilities/panels-widgets/' },
						{ label: 'Observabilidade', link: '/capabilities/observability/' },
						{ label: 'Cloud Future', link: '/capabilities/cloud-future/' },
					],
				},
				{
					label: 'Leitura canonica',
					items: [
						{ label: 'Indice da wiki', link: '/docs/README/' },
						{ label: 'Arquitetura futura', link: '/docs/architecture/07-cloud-first-multi-panel-future-architecture/' },
						{ label: 'Futuras implementacoes', link: '/docs/future-implementations/README/' },
					],
				},
			],
		}),
	],
});
