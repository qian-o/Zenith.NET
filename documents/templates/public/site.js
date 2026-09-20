export const isApplePlatform = /Mac|iPhone|iPad/.test(navigator.platform);

export function closeNavigation() {
    document.getElementById('menu-toggle')?.setAttribute('aria-expanded', 'false');
    document.querySelector('.site-header')?.classList.remove('menu-open');
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
    initializeLearnToc();
    labelCodeBlocks();
}

function initializeLearnToc() {
    const toc = document.getElementById('toc');
    if (!toc || document.body.dataset.section !== 'learn') return;
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
    if (flattenGroups()) return;
    const observer = new MutationObserver(() => {
        if (flattenGroups()) observer.disconnect();
    });
    observer.observe(toc, { childList: true, subtree: true });
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
        if (language !== 'mermaid') code.parentElement.dataset.language = labels[language] || language;
    }
}
