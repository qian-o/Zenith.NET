import { pageUrl, siteRoot } from './languages.js';
import { t } from './resources.js';

let navigationPromise;
export function loadNavigation() {
    return navigationPromise ??= fetch(new URL('toc.json', siteRoot), { cache: 'no-cache' }).then(response => {
        if (!response.ok) throw new Error('Navigation unavailable');
        return response.json();
    }).then(model => model.items).catch(error => {
        navigationPromise = undefined;
        throw error;
    });
}

export async function initializeNavigation() {
    const items = await loadNavigation();
    const currentPath = location.pathname.slice(siteRoot.pathname.length).replace(/\/$/, '/index.html');
    const active = items.find(item => (item.topicHref || item.href)?.split('/')[0] === currentPath.split('/')[0]);
    const label = item => item.resourceKey ? t(item.resourceKey) : item.name;
    const linkFor = item => {
        const link = document.createElement('a');
        link.href = pageUrl(item.topicHref || item.href);
        link.textContent = label(item);
        return link;
    };
    const navbar = document.createElement('ul');
    navbar.className = 'navbar-nav';
    for (const item of items) {
        const li = document.createElement('li');
        li.className = 'nav-item';
        const link = linkFor(item);
        link.className = 'nav-link';
        if (item === active) {
            link.classList.add('active');
            link.setAttribute('aria-current', 'true');
        }
        li.append(link); navbar.append(li);
    }
    document.getElementById('primary-navigation').replaceChildren(navbar);
    const toc = document.getElementById('toc');
    if (!toc) return;
    const listFor = (items, level) => {
        const list = document.createElement('ul');
        list.className = `nav level${level}`;
        for (const item of items) {
            const li = document.createElement('li');
            const href = item.topicHref || item.href;
            if (href) {
                const link = linkFor(item);
                if (new URL(link.href).pathname === new URL(currentPath, siteRoot).pathname) {
                    li.classList.add('active');
                    link.setAttribute('aria-current', 'page');
                }
                li.append(link);
            } else {
                li.className = 'expander expanded';
                const title = document.createElement('span');
                title.className = 'toc-group-title'; title.textContent = label(item); li.append(title);
            }
            if (item.items?.length) li.append(listFor(item.items, level + 1));
            list.append(li);
        }
        return list;
    };
    const scroll = document.createElement('div');
    scroll.className = 'overflow-y-auto';
    scroll.append(listFor(active ? [{ ...active, items: [] }, ...(active.items || [])] : [], 1));
    toc.replaceChildren(scroll);
}
