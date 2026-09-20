import { closeContents, closeNavigation, isApplePlatform, isEditing } from './site.js';
import { bindDialogKeys, isBackdropClick } from './dialog.js';

// Custom search presentation using DocFX's generated index and search worker.
export function initializeSearch() {
    const dialog = document.getElementById('search-dialog');
    if (!dialog) return;

    const trigger = document.getElementById('search-open');
    if (!trigger) return;
    const input = document.getElementById('site-search-input');
    const status = document.getElementById('search-status');
    const shortcuts = document.getElementById('search-shortcuts');
    const results = document.getElementById('site-search-results');
    const more = document.getElementById('search-more');
    const root = new URL(document.querySelector('meta[name="docfx:rel"]').content || './', location.href);
    const pendingQueries = [];
    let worker;
    let ready = false;
    let hits = [];
    let shown = 0;
    let closeTimer;

    function rememberSearch(open = dialog.open) {
        history.replaceState({
            ...history.state,
            zenithSearch: { open, query: input.value }
        }, '');
    }

    const shortcut = isApplePlatform ? '⌘ K' : 'Ctrl K';
    trigger.title = `Search documentation (${shortcut})`;
    trigger.setAttribute('aria-keyshortcuts', shortcut === '⌘ K' ? 'Meta+K' : 'Control+K');

    function runQuery() {
        const query = input.value.trim();
        results.replaceChildren();
        more.hidden = true;
        shortcuts.hidden = query !== '';
        if (!query) {
            status.textContent = 'Search the docs, concepts, and API.';
            return;
        }
        status.textContent = ready ? 'Searching…' : 'Loading search…';
        if (!ready) return;
        const terms = query.split(/\s+/).map(term => '+' + term.replace(/[+\-:^~*\\]/g, '\\$&')).join(' ');
        pendingQueries.push(query);
        worker.postMessage({ q: terms });
    }

    function appendResults() {
        const fragment = document.createDocumentFragment();
        for (const hit of hits.slice(shown, shown + 30)) {
            const item = document.createElement('li');
            const link = document.createElement('a');
            const heading = document.createElement('span');
            const title = document.createElement('strong');
            const category = document.createElement('small');
            const apiResult = hit.href.startsWith('api/');
            link.href = new URL(hit.href, root).href;
            link.setAttribute('data-search-item', '');
            heading.className = 'search-result-heading';
            title.textContent = hit.title.replace(/\s*\|\s*Zenith\.NET\s*$/, '');
            category.textContent = apiResult ? 'API' : hit.href === 'index.html' ? 'Home' : 'Learn';
            heading.append(title, category);
            link.append(heading);
            if (apiResult || hit.summary) {
                const excerpt = document.createElement('p');
                // API results identify the fully qualified type, without indexing UI labels into the preview.
                let text = apiResult
                    ? hit.href === 'api/index.html' ? 'Browse namespaces and types.' : decodeURIComponent(new URL(hit.href, root).pathname.split('/').pop()).replace(/\.html$/, '')
                    : hit.summary.replace(/\s+/g, ' ').trim();
                if (text.startsWith(title.textContent)) text = text.slice(title.textContent.length).trim();
                if (text) {
                    excerpt.textContent = text.length > 160 ? text.slice(0, 160) + '…' : text;
                    link.append(excerpt);
                }
            }
            item.append(link);
            fragment.append(item);
        }
        shown = Math.min(shown + 30, hits.length);
        results.append(fragment);
        more.hidden = shown === hits.length;
    }

    function openSearch() {
        document.getElementById('api-member-dialog')?.close();
        clearTimeout(closeTimer);
        closeTimer = undefined;
        dialog.classList.remove('is-closing');
        closeNavigation();
        closeContents();
        if (!dialog.open) dialog.showModal();
        input.focus();
        input.select();
        rememberSearch();
        if (worker) return;
        worker = new Worker(new URL('public/search-worker.min.js', root), { type: 'module' });
        worker.addEventListener('message', event => {
            if (event.data.e === 'index-ready') {
                ready = true;
                runQuery();
            } else if (event.data.e === 'query-ready') {
                const query = pendingQueries.shift();
                if (query !== input.value.trim()) return;
                hits = event.data.d;
                shown = 0;
                results.replaceChildren();
                status.textContent = hits.length ? `${hits.length} result${hits.length === 1 ? '' : 's'}` : `No results for “${query}”.`;
                appendResults();
            }
        });
        worker.addEventListener('error', () => {
            status.textContent = 'Search is unavailable. You can still open the pages below.';
            shortcuts.hidden = false;
        });
        worker.postMessage({ init: {} });
    }

    function closeSearch() {
        if (!dialog.open || closeTimer) return;
        rememberSearch(false);
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            dialog.close();
            return;
        }
        dialog.classList.add('is-closing');
        closeTimer = setTimeout(() => dialog.close(), 160);
    }

    trigger.addEventListener('click', openSearch);
    input.addEventListener('input', runQuery);
    document.getElementById('search-close').addEventListener('click', closeSearch);
    dialog.addEventListener('close', () => {
        if (dialog.open) return;
        clearTimeout(closeTimer);
        closeTimer = undefined;
        dialog.classList.remove('is-closing');
        rememberSearch();
        trigger.focus({ preventScroll: true });
    });
    dialog.addEventListener('cancel', event => {
        event.preventDefault();
        closeSearch();
    });
    dialog.addEventListener('click', event => {
        if (event.target.closest('[data-search-item]')) rememberSearch();
        if (isBackdropClick(event, dialog)) closeSearch();
    });
    document.getElementById('site-search-form').addEventListener('submit', event => {
        event.preventDefault();
        results.querySelector('a')?.click();
    });
    more.addEventListener('click', () => {
        const next = shown;
        appendResults();
        results.children[next]?.querySelector('a').focus();
    });
    document.addEventListener('keydown', event => {
        if (event.isComposing || event.repeat) return;
        const command = (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k';
        const slash = event.key === '/' && !event.metaKey && !event.ctrlKey && !event.altKey;
        if (command || slash && !isEditing(event.target)) {
            event.preventDefault();
            if (dialog.open && command && !closeTimer) closeSearch();
            else openSearch();
        }
    });
    bindDialogKeys(dialog, { items: '[data-search-item]', close: closeSearch });

    // Restore the search when returning from a result, including a full reload.
    function restoreSearch() {
        const saved = history.state?.zenithSearch;
        if (!saved?.open) return;
        const queryChanged = input.value !== saved.query;
        input.value = saved.query;
        if (!dialog.open) {
            openSearch();
            runQuery();
        } else {
            input.focus();
            input.select();
            if (queryChanged) runQuery();
        }
    }
    // Run after history traversal restores form controls, including Safari's search input.
    window.addEventListener('pageshow', () => setTimeout(restoreSearch, 0));
    restoreSearch();
}
