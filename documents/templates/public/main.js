import { initializeSearch } from './search.js';
import { initializeHomeScene } from './home.js';
import { initializeMotion } from './motion.js';
import { initializeApi } from './api.js';
import { initializeMemberFinder } from './member-finder.js';

// Retain DocFX's generated navigation, reference pages, and code tools.
// The master template sets the fixed dark appearance before the first paint.
export default {
    defaultTheme: 'dark',
    configureHljs: hljs => {
        hljs.registerAliases(['slang'], { languageName: 'cpp' });
        hljs.registerAliases(['console'], { languageName: 'shell' });
    },
    mermaid: {
        theme: 'base',
        themeVariables: {
            darkMode: true,
            background: '#181b21',
            primaryColor: '#262a32',
            primaryTextColor: '#f2f3f5',
            primaryBorderColor: '#626a78',
            secondaryColor: '#181b21',
            secondaryTextColor: '#f2f3f5',
            secondaryBorderColor: '#626a78',
            tertiaryColor: '#343943',
            tertiaryTextColor: '#f2f3f5',
            tertiaryBorderColor: '#626a78',
            lineColor: '#a4abb7',
            textColor: '#f2f3f5',
            edgeLabelBackground: '#181b21',
            fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
        }
    },
    start: () => {
        // Keep heading IDs for deep links and the outline, without AnchorJS controls.
        for (const heading of document.querySelectorAll('article h2, article h3, article h4')) {
            heading.classList.add('no-anchor');
        }
        // Learn has one small, always-visible group. Keep API trees interactive.
        const toc = document.getElementById('toc');
        if (toc && document.body.dataset.section === 'learn') {
            const flattenGroups = () => {
                if (!toc.querySelector(':scope > .overflow-y-auto')) return false;
                for (const group of toc.querySelectorAll('li.expander')) {
                    group.classList.add('expanded');
                    group.querySelector(':scope > .expand-stub')?.remove();
                    const link = group.querySelector(':scope > a[href="#"]');
                    if (link) {
                        const heading = document.createElement('span');
                        heading.className = 'toc-group-title';
                        heading.textContent = link.textContent;
                        group.replaceChild(heading, link);
                    }
                }
                return true;
            };
            if (!flattenGroups()) {
                const observer = new MutationObserver(() => {
                    if (flattenGroups()) observer.disconnect();
                });
                observer.observe(toc, { childList: true, subtree: true });
            }
        }

        initializeSearch();
        initializeHomeScene();
        initializeApi();
        initializeMemberFinder();
        initializeMotion();

        const menu = document.getElementById('menu-toggle');
        const header = document.querySelector('.site-header');
        const closeMenu = () => {
            menu.setAttribute('aria-expanded', 'false');
            header.classList.remove('menu-open');
        };
        menu?.addEventListener('click', () => {
            const open = menu.getAttribute('aria-expanded') !== 'true';
            menu.setAttribute('aria-expanded', String(open));
            header.classList.toggle('menu-open', open);
        });
        document.addEventListener('click', event => {
            if (header?.classList.contains('menu-open') && !header.contains(event.target)) closeMenu();
        });
        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && header?.classList.contains('menu-open')) {
                closeMenu();
                menu.focus();
            }
        });
        window.matchMedia('(min-width: 768px)').addEventListener('change', event => {
            if (event.matches && menu) closeMenu();
        });
        if (menu) window.addEventListener('pagehide', closeMenu);

        const languages = {
            bash: 'Shell',
            console: 'Console',
            cs: 'C#',
            csharp: 'C#',
            json: 'JSON',
            powershell: 'PowerShell',
            shell: 'Shell',
            slang: 'Slang',
            text: 'Text',
            xml: 'XML',
            yaml: 'YAML',
            yml: 'YAML'
        };

        for (const code of document.querySelectorAll('article pre > code')) {
            const language = [...code.classList]
                .find(name => /^(?:lang|language)-/.test(name))
                ?.replace(/^(?:lang|language)-/, '') || 'text';

            if (language !== 'mermaid') {
                code.parentElement.dataset.language = languages[language] || language;
            }
        }
    }
};
