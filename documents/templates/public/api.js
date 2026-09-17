// Use DocFX's generated TOC as the source of truth for the custom API browser.
export async function initializeApi() {
    const browser = document.getElementById('api-browser');
    if (!browser) return;

    const picker = document.getElementById('api-namespace');
    const filter = document.getElementById('api-type-filter');
    const list = document.getElementById('api-type-list');
    const status = document.getElementById('api-browser-status');
    const scope = document.getElementById('api-type-scope');
    filter.disabled = true;

    function writeNamespace(element, name) {
        name.split('.').forEach((part, index) => {
            if (index) element.append('.', document.createElement('wbr'));
            element.append(part);
        });
    }

    try {
        const tocUrl = new URL(document.querySelector('meta[name="docfx:tocrel"]').content, location.href);
        const response = await fetch(tocUrl);
        if (!response.ok) throw new Error('API navigation could not be loaded.');
        const documentToc = new DOMParser().parseFromString(await response.text(), 'text/html');
        const readLink = link => ({ name: link.textContent.trim(), href: new URL(link.getAttribute('href'), tocUrl).href });
        const namespaces = [...documentToc.querySelectorAll('.nav.level1 > li')].map(item => {
            const link = item.querySelector(':scope > a');
            return { ...readLink(link), types: [...item.querySelectorAll(':scope > ul > li > a')].map(readLink) };
        }).sort((a, b) => {
            const rank = name => name === 'Zenith.NET' ? 0 : name.startsWith('Zenith.NET.') ? 1 : 2;
            return rank(a.name) - rank(b.name) || a.name.localeCompare(b.name);
        });
        const current = href => new URL(href).pathname === location.pathname;
        const activeNamespace = namespaces.find(item => current(item.href) || item.types.some(type => current(type.href))) || namespaces[0];
        picker.replaceChildren(...namespaces.map(item => {
            const option = document.createElement('option');
            option.value = item.href;
            option.textContent = item.name;
            return option;
        }));
        picker.disabled = false;
        filter.disabled = false;
        picker.value = activeNamespace.href;
        picker.title = activeNamespace.name;
        const selectedName = document.getElementById('api-namespace-name');
        selectedName.replaceChildren();
        writeNamespace(selectedName, activeNamespace.name);
        picker.addEventListener('change', () => location.assign(picker.value));

        function renderTypes() {
            const query = filter.value.trim().toLowerCase();
            const entries = query
                ? namespaces.flatMap(namespace => namespace.types.map(type => ({ ...type, namespace: namespace.name })))
                    .filter(type => `${type.name} ${type.namespace}`.toLowerCase().includes(query))
                : activeNamespace.types;
            const fragment = document.createDocumentFragment();
            for (const type of entries) {
                const item = document.createElement('li');
                const link = document.createElement('a');
                const name = document.createElement('span');
                // Keep identifiers intact while allowing breaks between their words.
                type.name.split(/(?<=[a-z])(?=[A-Z])|(?<=[0-9A-Z])(?=[A-Z][a-z])|(?<=\.)/).forEach((part, index) => {
                    if (index) name.append(document.createElement('wbr'));
                    name.append(part);
                });
                link.href = type.href;
                link.title = type.name;
                link.append(name);
                if (current(type.href)) link.setAttribute('aria-current', 'page');
                if (query) {
                    const namespace = document.createElement('small');
                    writeNamespace(namespace, type.namespace);
                    link.append(namespace);
                }
                item.append(link);
                fragment.append(item);
            }
            list.replaceChildren(fragment);
            scope.textContent = query ? 'Matching types' : 'Types';
            status.hidden = entries.length > 0;
            status.textContent = query ? 'No matching types.' : 'No types in this namespace.';
        }
        filter.addEventListener('input', renderTypes);
        filter.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                filter.value = '';
                renderTypes();
            } else if (event.key === 'ArrowDown') {
                event.preventDefault();
                list.querySelector('a')?.focus();
            }
        });
        renderTypes();
        const active = list.querySelector('[aria-current="page"]');
        if (active) browser.querySelector('.api-type-scroll').scrollTop = active.offsetTop - 160;
        window.addEventListener('pageshow', () => setTimeout(() => {
            picker.value = activeNamespace.href;
            renderTypes();
        }, 0));

        const grid = document.getElementById('api-namespace-grid');
        if (grid) {
            for (const namespace of namespaces) {
                const link = document.createElement('a');
                link.href = namespace.href;
                link.className = 'api-namespace-card';
                const name = document.createElement('strong');
                writeNamespace(name, namespace.name);
                link.append(name);
                grid.append(link);
            }
        }
    } catch {
        status.hidden = false;
        status.textContent = 'Could not load types. Reload this page to try again.';
    }
}
