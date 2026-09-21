export const isApplePlatform = /Mac|iPhone|iPad/.test(navigator.platform);

export function closeHeaderMenu() {
    document.getElementById('menu-toggle')?.setAttribute('aria-expanded', 'false');
    document.querySelector('.site-header')?.classList.remove('menu-open');
}

export function closeNavigation() {
    document.getElementById('language-menu')?.removeAttribute('open');
    closeHeaderMenu();
}

export function closeContents() {
    document.querySelector('#tocOffcanvas.show .contents-close')?.click();
}

export function isEditing(target) {
    return Boolean(target.closest?.('input, textarea, select, [contenteditable]:not([contenteditable="false"])'));
}

export function initializeSite() {
    const menu = document.getElementById('menu-toggle');
    const header = document.querySelector('.site-header');
    if (menu && header) {
        menu.addEventListener('click', () => {
            document.getElementById('language-menu')?.removeAttribute('open');
            const open = menu.getAttribute('aria-expanded') !== 'true';
            menu.setAttribute('aria-expanded', String(open));
            header.classList.toggle('menu-open', open);
        });
        document.addEventListener('click', event => {
            if (header.classList.contains('menu-open') && !header.contains(event.target)) closeNavigation();
        });
        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && header.classList.contains('menu-open')) {
                closeNavigation();
                menu.focus();
            }
        });
        window.matchMedia('(min-width: 768px)').addEventListener('change', event => {
            if (event.matches) closeNavigation();
        });
        window.addEventListener('pagehide', closeNavigation);
    }

    // Preserve deep-link IDs while disabling DocFX's decorative heading anchors.
    for (const heading of document.querySelectorAll('article h2, article h3, article h4')) heading.classList.add('no-anchor');
    labelCodeBlocks();
    // DocFX adds scrolling wrappers after this hook; the table retains its focus target.
    for (const table of document.querySelectorAll('article table')) table.tabIndex = 0;
}

function labelCodeBlocks() {
    const labels = {
        bash: 'Shell', console: 'Console', cs: 'C#', csharp: 'C#', json: 'JSON',
        powershell: 'PowerShell', sh: 'Shell', shell: 'Shell', slang: 'Slang', text: 'Text',
        xml: 'XML', yaml: 'YAML', yml: 'YAML'
    };
    for (const code of document.querySelectorAll('article pre > code')) {
        const language = [...code.classList].find(name => /^(?:lang|language)-/.test(name))
            ?.replace(/^(?:lang|language)-/, '') || 'text';
        if (language === 'mermaid') continue;
        code.parentElement.dataset.language = labels[language] || language;
        // Native keyboard scrolling for examples; API declarations already wrap.
        if (!code.hasAttribute('data-api-symbol')) code.tabIndex = 0;
    }
}
