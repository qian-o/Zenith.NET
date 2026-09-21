import { getStrings, t } from './resources.js';
import { loadNavigation } from './navigation.js';
import { closeContents, closeNavigation, isApplePlatform, isEditing } from './site.js';
import { bindDialogKeys, isBackdropClick } from './dialog.js';
import { edition, sourceLanguage, pageUrl, siteRoot } from './languages.js';

const resultsPerPage = 30;
const excerptLength = 160;

function pageTitles(items) {
    const titles = new Map([['index.html', 'home.title'], ['api/index.html', 'reference.title']]);
    const visit = item => {
        const href = item.topicHref || item.href;
        if (href && item.resourceKey?.endsWith('.title')) titles.set(href, item.resourceKey);
        for (const child of item.items || []) visit(child);
    };
    items.forEach(visit);
    return titles;
}

async function loadPageCatalog() {
    const response = await fetch(new URL('index.json', siteRoot), { cache: 'no-cache' });
    if (!response.ok) throw new Error('Page index unavailable');
    const index = await response.json();
    return Object.keys(index).sort().map(key => index[key]);
}

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
    const pendingQueries = [];
    let worker;
    let ready = false;
    let localEntries = [];
    let loadingCatalog = false;
    let hits = [];
    let shown = 0;
    let closeRequest;
    const normalize = text => String(text || '').normalize('NFKD').replace(/\p{M}/gu, '').toLowerCase();

    function rememberSearch(open = dialog.open) {
        history.replaceState({
            ...history.state,
            zenithSearch: { open, query: input.value }
        }, '');
    }

    const shortcut = isApplePlatform ? '⌘ K' : 'Ctrl K';
    trigger.title = `${t('ui.search.title')} (${shortcut})`;
    trigger.setAttribute('aria-keyshortcuts', shortcut === '⌘ K' ? 'Meta+K' : 'Control+K');

    function runQuery() {
        const query = input.value.trim();
        results.replaceChildren();
        more.hidden = true;
        shortcuts.hidden = query !== '';
        if (!query) {
            status.textContent = t('ui.search.hint');
            return;
        }
        status.textContent = ready ? t('ui.search.pending') : t('ui.search.loading');
        if (!ready) return;
        if (edition.code !== sourceLanguage) {
            const normalizedQuery = normalize(query);
            const terms = normalizedQuery.split(/\s+/).filter(Boolean);
            const matches = localEntries
                .filter(item => terms.every(term => item.body.includes(term)))
                .map(item => ({ page: item.page, score: item.title === normalizedQuery ? terms.length + 1 : terms.filter(term => item.title.includes(term)).length }))
                .sort((a, b) => b.score - a.score)
                .map(item => item.page);
            showResults(query, matches);
            return;
        }
        // Use the same separators as DocFX's index for filenames and qualified API names.
        const terms = query.split(/[\s\-.()]+/).filter(Boolean).map(term => '+' + term.replace(/[+\-:^~*\\]/g, '\\$&')).join(' ');
        if (!terms) return showResults(query, []);
        pendingQueries.push(query);
        worker.postMessage({ q: terms });
    }

    function showResults(query, matches) {
        hits = matches;
        shown = 0;
        results.replaceChildren();
        status.textContent = hits.length ? t('ui.search.resultCount', { count: hits.length }) : t('ui.search.empty', { query });
        appendResults();
    }

    function appendResults() {
        const fragment = document.createDocumentFragment();
        for (const hit of hits.slice(shown, shown + resultsPerPage)) {
            const item = document.createElement('li');
            const link = document.createElement('a');
            const heading = document.createElement('span');
            const title = document.createElement('strong');
            const category = document.createElement('small');
            const path = hit.href;
            const apiResult = path.startsWith('api/');
            link.href = pageUrl(hit.href);
            link.setAttribute('data-search-item', '');
            heading.className = 'search-result-heading';
            title.textContent = hit.title.replace(/\s*\|\s*Zenith\.NET\s*$/, '');
            category.textContent = apiResult ? 'API' : path === 'index.html' ? t('ui.navigation.home') : t('learning.title');
            heading.append(title, category);
            link.append(heading);
            if (apiResult || hit.summary) {
                const excerpt = document.createElement('p');
                // API results identify the fully qualified type, without indexing UI labels into the preview.
                let text = apiResult
                    ? path === 'api/index.html' ? t('ui.api.browse') : decodeURIComponent(new URL(hit.href, siteRoot).pathname.split('/').pop()).replace(/\.html$/, '')
                    : hit.summary.replace(/\s+/g, ' ').trim();
                if (text.startsWith(title.textContent)) text = text.slice(title.textContent.length).trim();
                if (text) {
                    excerpt.textContent = text.length > excerptLength ? text.slice(0, excerptLength) + '…' : text;
                    link.append(excerpt);
                }
            }
            item.append(link);
            fragment.append(item);
        }
        shown = Math.min(shown + resultsPerPage, hits.length);
        results.append(fragment);
        more.hidden = shown === hits.length;
    }

    function openSearch() {
        document.getElementById('api-member-dialog')?.close();
        closeRequest = undefined;
        dialog.classList.remove('is-closing');
        closeNavigation();
        closeContents();
        if (!dialog.open) dialog.showModal();
        input.focus();
        input.select();
        rememberSearch();
        if (edition.code !== sourceLanguage) {
            if (ready) runQuery();
            else if (!loadingCatalog) {
                loadingCatalog = true;
                Promise.all([loadPageCatalog(), loadNavigation()]).then(([index, navigation]) => {
                    const titles = pageTitles(navigation);
                    const strings = getStrings();
                    localEntries = index.map(original => {
                        const key = titles.get(original.href);
                        if (!key) return {
                            page: original,
                            title: normalize(original.title.replace(/\s*\|\s*Zenith\.NET\s*$/, '')),
                            body: normalize(`${original.href} ${original.title} ${original.keywords || ''} ${original.summary || ''}`)
                        };
                        const prefix = key.slice(0, -'.title'.length);
                        const values = Object.entries(strings).filter(([key]) => key.startsWith(prefix + '.')).map(([, value]) => value);
                        const description = strings[prefix + '.description'] || strings[prefix + '.meta.description'];
                        const page = { ...original, title: t(key), summary: description && !description.includes('{') ? description : '' };
                        // Keep shared code, filenames and API identifiers searchable in every language.
                        const body = `${values.join(' ')} ${original.href} ${original.keywords || ''} ${original.summary || ''}`;
                        return { page, title: normalize(page.title), body: normalize(body) };
                    });
                    ready = true;
                    runQuery();
                }).catch(showUnavailable).finally(() => { loadingCatalog = false; });
            }
            return;
        }
        if (worker) return;
        worker = new Worker(new URL('public/search-worker.min.js', siteRoot), { type: 'module' });
        worker.addEventListener('message', event => {
            if (event.data.e === 'index-ready') {
                ready = true;
                runQuery();
            } else if (event.data.e === 'query-ready') {
                const query = pendingQueries.shift();
                if (query !== input.value.trim()) return;
                showResults(query, event.data.d);
            }
        });
        worker.addEventListener('error', () => {
            worker.terminate();
            worker = undefined;
            ready = false;
            pendingQueries.length = 0;
            showUnavailable();
        });
        worker.postMessage({ init: {} });
    }

    function showUnavailable() {
        results.replaceChildren();
        more.hidden = true;
        status.textContent = t('ui.search.unavailable');
        shortcuts.hidden = false;
    }

    function closeSearch() {
        if (!dialog.open || closeRequest) return;
        rememberSearch(false);
        dialog.classList.add('is-closing');
        const request = Promise.allSettled(dialog.getAnimations().map(animation => animation.finished));
        closeRequest = request;
        request.then(() => {
            // Reopening cancels this close, including when animations are disabled.
            if (closeRequest === request) dialog.close();
        });
    }

    trigger.addEventListener('click', openSearch);
    input.addEventListener('input', runQuery);
    document.getElementById('search-close').addEventListener('click', closeSearch);
    dialog.addEventListener('close', () => {
        if (dialog.open) return;
        closeRequest = undefined;
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
            if (dialog.open && command && !closeRequest) closeSearch();
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
