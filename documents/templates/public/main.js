export default {
    iconLinks: [
        {
            icon: 'github',
            href: 'https://github.com/qian-o/Zenith.NET',
            title: 'GitHub'
        }
    ],
    start: () => {
        const logo = document.getElementById('logo');
        if (logo) {
            const lightLogo = logo.src;
            const darkLogo = lightLogo.replace('Zenith.NET-Logo.svg', 'Zenith.NET-Logo-Dark.svg');
            const updateLogo = () => {
                logo.src = document.documentElement.dataset.bsTheme === 'dark' ? darkLogo : lightLogo;
            };

            new MutationObserver(updateLogo).observe(document.documentElement, {
                attributes: true,
                attributeFilter: ['data-bs-theme']
            });
            updateLogo();
        }

        const navbar = document.getElementById('navbar');
        if (navbar && !navbar.querySelector('.nav-cta')) {
            const rootPath = document.querySelector('meta[name="docfx:rel"]')?.content || '';
            const getStarted = document.createElement('a');
            getStarted.className = 'nav-cta';
            getStarted.href = `${rootPath}tutorials/getting-started/prerequisites.html`;
            getStarted.innerHTML = 'Get started <i class="bi bi-chevron-right" aria-hidden="true"></i>';
            getStarted.setAttribute('aria-label', 'Get started with Zenith.NET');
            navbar.append(getStarted);
        }

        const installCopy = document.querySelector('[data-copy-command]');
        if (installCopy) {
            installCopy.addEventListener('click', async () => {
                const label = installCopy.querySelector('span');
                const command = installCopy.dataset.copyCommand;

                try {
                    await navigator.clipboard.writeText(command);
                    installCopy.classList.add('is-copied');
                    if (label) label.textContent = 'Copied';

                    window.setTimeout(() => {
                        installCopy.classList.remove('is-copied');
                        if (label) label.textContent = 'Copy';
                    }, 2000);
                } catch {
                    installCopy.classList.remove('is-copied');
                    if (label) label.textContent = 'Copy failed';
                }
            });
        }

        const currentPath = window.location.pathname.replace(/\/index\.html$/, '/');
        for (const link of document.querySelectorAll('#navbar .nav-link')) {
            const linkPath = new URL(link.href, window.location.href).pathname.replace(/\/index\.html$/, '/');
            const isHome = linkPath === '/' && currentPath === '/';
            const isSection = linkPath !== '/' && currentPath.startsWith(linkPath);
            link.classList.toggle('active', isHome || isSection);
        }

        // Prevent short table cells from wrapping (e.g. "DirectX 12", "Vulkan 1.4")
        for (const td of document.querySelectorAll('article td')) {
            if (td.textContent.trim().length <= 20) {
                td.style.whiteSpace = 'nowrap';
            }
        }

        // Hide inherited type info sections (inheritance, implements, inheritedMembers, derivedClasses)
        for (const dl of document.querySelectorAll('dl.typelist.inheritance, dl.typelist.implements, dl.typelist.inheritedMembers, dl.typelist.derivedClasses')) {
            dl.style.display = 'none';
        }

        // Hide protected and override members from API pages
        for (const code of document.querySelectorAll('.codewrapper pre code')) {
            const text = code.textContent.trim();
            if (!/^protected\s/.test(text) && !/\boverride\b/.test(text)) continue;

            const wrapper = code.closest('.codewrapper');
            if (!wrapper) continue;

            const toHide = [];

            // Walk backward from codewrapper to collect h3 header and anchor
            let el = wrapper;
            while (el) {
                toHide.push(el);
                if (el.tagName === 'H3') {
                    const prev = el.previousElementSibling;
                    if (prev && prev.tagName === 'A' && prev.dataset.uid) {
                        toHide.push(prev);
                    }
                    break;
                }
                el = el.previousElementSibling;
            }

            // Walk forward from codewrapper to collect parameters, returns, etc.
            let next = wrapper.nextElementSibling;
            while (next && next.tagName !== 'H3' && next.tagName !== 'H2' &&
                !(next.tagName === 'A' && next.dataset.uid)) {
                toHide.push(next);
                next = next.nextElementSibling;
            }

            toHide.forEach(e => e.style.display = 'none');
        }

        // Hide empty section headers (e.g. "Constructors" when all constructors are hidden)
        for (const h2 of document.querySelectorAll('article h2.section')) {
            let next = h2.nextElementSibling;
            let hasVisibleMember = false;
            while (next && next.tagName !== 'H2') {
                if (next.tagName === 'H3' && next.style.display !== 'none') {
                    hasVisibleMember = true;
                    break;
                }
                next = next.nextElementSibling;
            }
            if (!hasVisibleMember) {
                h2.style.display = 'none';
            }
        }

        // Search results: navigate in same tab with back button support
        document.addEventListener('click', (e) => {
            const link = e.target.closest('#search-results .sr-item a');
            if (!link) return;

            e.preventDefault();
            const query = document.getElementById('search-query')?.value || '';
            history.replaceState({ search: true, query }, '');
            window.location.href = link.href;
        });

        window.addEventListener('popstate', (e) => {
            if (e.state?.search) {
                const input = document.getElementById('search-query');
                if (input) {
                    input.value = e.state.query;
                    input.dispatchEvent(new Event('input', { bubbles: true }));
                }
            }
        });
    }
}