import { applyResources, resourceState, setStrings } from './resources.js';
import { closeHeaderMenu } from './site.js';

export const siteRoot = new URL(document.querySelector('meta[name="site:root"]').content || './', location.href);
export const sourceLanguage = resourceState.sourceLanguage;
export const edition = { code: sourceLanguage };
const preferenceKey = 'zenith-language:' + siteRoot.pathname;
const available = resourceState.languages.filter(language => language.available);

function savedLanguage() {
    try {
        return localStorage.getItem(preferenceKey);
    } catch {
        return null;
    }
}

function matchLanguage(code) {
    if (!code) return;
    const exact = available.find(language => language.code.toLowerCase() === code.toLowerCase());
    if (exact) return exact.code;
    let locale;
    try {
        locale = new Intl.Locale(code);
    } catch {
        return;
    }
    return (
        locale.language === 'zh'
            ? available.find(language => language.code === (locale.maximize().script === 'Hant' ? 'zh-TW' : 'zh-CN'))
            : available.find(language => language.code.split('-')[0] === locale.language)
    )?.code;
}

function preferredLanguage() {
    const requested = new URL(location.href).searchParams.get('lang');
    if (requested !== null) return matchLanguage(requested) || sourceLanguage;
    return [savedLanguage(), ...navigator.languages].map(matchLanguage).find(Boolean) || sourceLanguage;
}

export function pageUrl(path) {
    const url = new URL(path, siteRoot);
    if (
        url.origin === siteRoot.origin &&
        url.pathname.startsWith(siteRoot.pathname) &&
        (url.pathname.endsWith('.html') || url.pathname.endsWith('/'))
    )
        url.searchParams.set('lang', edition.code);
    return url.href;
}

export async function initializeLanguages() {
    initializeMenu();
    edition.code = preferredLanguage();
    if (edition.code !== sourceLanguage) {
        try {
            const response = await fetch(new URL(`locales/${edition.code}/strings.json`, siteRoot), {
                cache: 'no-cache',
                signal: AbortSignal.timeout(8000)
            });
            if (!response.ok) throw new Error('Language resources unavailable');
            setStrings(await response.json());
        } catch {
            edition.code = sourceLanguage;
        }
    }
    applyResources(edition.code);
    if (location.hash)
        requestAnimationFrame(() => {
            let id;
            try {
                id = decodeURIComponent(location.hash.slice(1));
            } catch {
                return;
            }
            document.getElementById(id)?.scrollIntoView({ block: 'start', behavior: 'auto' });
        });
    const current = new URL(location.href);
    if (current.searchParams.has('lang') || edition.code !== sourceLanguage) {
        current.searchParams.set('lang', edition.code);
        history.replaceState(history.state, '', current);
    }
    updateMenu();
    for (const link of document.querySelectorAll('a[href]:not([href^="#"])')) {
        if (link.closest('#language-menu')) continue;
        link.href = pageUrl(link.href);
        const url = new URL(link.href);
        if (/^https?:$/.test(url.protocol) && url.origin !== location.origin) {
            link.target = '_blank';
            link.rel = 'noopener noreferrer';
        }
    }
}

function updateMenu() {
    for (const link of document.querySelectorAll('#language-menu a[data-language-code]')) {
        const url = new URL(location.href);
        url.searchParams.set('lang', link.dataset.languageCode);
        link.href = url.href;
        if (link.dataset.languageCode === edition.code) link.setAttribute('aria-current', 'true');
        else link.removeAttribute('aria-current');
    }
}

function initializeMenu() {
    const menu = document.getElementById('language-menu');
    updateMenu();
    menu.addEventListener('toggle', () => {
        if (menu.open) {
            closeHeaderMenu();
            updateMenu();
        }
    });
    menu.addEventListener('click', event => {
        const link = event.target.closest('a[data-language-code]');
        if (link) {
            try {
                localStorage.setItem(preferenceKey, link.dataset.languageCode);
            } catch {
                /* Optional preference. */
            }
        }
    });
    document.addEventListener('click', event => {
        if (!menu.contains(event.target)) menu.open = false;
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && menu.open) {
            menu.open = false;
            menu.querySelector('summary').focus();
        }
    });
}
