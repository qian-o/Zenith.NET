export default {
    iconLinks: [
        {
            icon: 'github',
            href: 'https://github.com/qian-o/Zenith.NET',
            title: 'GitHub'
        }
    ],
    start: () => {
        const copyText = async (text) => {
            try {
                await navigator.clipboard.writeText(text);
                return;
            } catch {
                const textarea = document.createElement('textarea');
                textarea.value = text;
                textarea.setAttribute('readonly', '');
                textarea.style.position = 'fixed';
                textarea.style.opacity = '0';
                document.body.append(textarea);
                textarea.select();

                const copied = document.execCommand('copy');
                textarea.remove();
                if (!copied) throw new Error('Copy failed');
            }
        };

        const copyResetTimers = new WeakMap();
        const setCopyButtonState = (button, state) => {
            const defaultLabel = button.dataset.copyLabel || button.getAttribute('aria-label') || 'Copy';
            const icon = button.querySelector('i');
            const feedback = {
                idle: { icon: 'bi bi-copy', label: defaultLabel },
                copied: { icon: 'bi bi-check2', label: 'Copied' },
                failed: { icon: 'bi bi-x-lg', label: 'Copy failed' }
            }[state];

            button.dataset.copyLabel = defaultLabel;
            button.classList.toggle('is-copied', state === 'copied');
            button.classList.toggle('is-failed', state === 'failed');
            button.setAttribute('aria-label', feedback.label);
            button.title = feedback.label;
            if (icon) icon.className = feedback.icon;

            const activeTimer = copyResetTimers.get(button);
            if (activeTimer) window.clearTimeout(activeTimer);

            if (state === 'idle') {
                copyResetTimers.delete(button);
            } else {
                copyResetTimers.set(button, window.setTimeout(() => {
                    copyResetTimers.delete(button);
                    setCopyButtonState(button, 'idle');
                }, 2000));
            }
        };

        const copyWithFeedback = async (button, text) => {
            try {
                await copyText(text);
                setCopyButtonState(button, 'copied');
            } catch {
                setCopyButtonState(button, 'failed');
            }
        };

        const navbar = document.getElementById('navbar');
        if (navbar && !navbar.querySelector('.nav-cta')) {
            const rootPath = document.querySelector('meta[name="docfx:rel"]')?.content || '';
            const getStarted = document.createElement('a');
            getStarted.className = 'nav-cta';
            getStarted.href = `${rootPath}tutorials/getting-started/prerequisites.html`;
            getStarted.textContent = 'Get started';
            getStarted.setAttribute('aria-label', 'Get started with Zenith.NET');
            navbar.append(getStarted);
        }

        const installCopy = document.querySelector('[data-copy-command]');
        if (installCopy) {
            setCopyButtonState(installCopy, 'idle');
            installCopy.addEventListener('click', async () => {
                await copyWithFeedback(installCopy, installCopy.dataset.copyCommand);
            });
        }

        const architectureCopy = document.querySelector('.architecture-code-copy');
        const architectureCode = document.querySelector('.architecture-code code');
        if (architectureCopy && architectureCode) {
            setCopyButtonState(architectureCopy, 'idle');
            architectureCopy.addEventListener('click', async () => {
                await copyWithFeedback(architectureCopy, architectureCode.textContent);
            });
        }

        const languageNames = {
            bash: 'Shell',
            csharp: 'C#',
            cs: 'C#',
            console: 'Console',
            json: 'JSON',
            powershell: 'PowerShell',
            shell: 'Shell',
            xml: 'XML',
            yaml: 'YAML',
            yml: 'YAML'
        };

        for (const pre of document.querySelectorAll('body:not([data-layout="landing"]) article pre')) {
            if (pre.closest('.doc-code-frame')) continue;

            const code = pre.querySelector('code');
            if (!code) continue;

            const languageClass = [...code.classList].find(name => name.startsWith('language-') || name.startsWith('lang-'));
            const language = languageClass?.replace(/^language-|^lang-/, '').toLowerCase() || 'text';
            const frame = document.createElement('div');
            const toolbar = document.createElement('div');
            const languageLabel = document.createElement('span');
            const copy = document.createElement('button');

            frame.className = 'doc-code-frame';
            toolbar.className = 'doc-code-toolbar';
            languageLabel.className = 'doc-code-language';
            languageLabel.textContent = languageNames[language] || language.toUpperCase();
            copy.type = 'button';
            copy.className = 'doc-code-copy';
            copy.setAttribute('aria-label', 'Copy code block');
            copy.innerHTML = '<i class="bi bi-copy" aria-hidden="true"></i>';
            setCopyButtonState(copy, 'idle');

            pre.parentNode.insertBefore(frame, pre);
            frame.append(toolbar, pre);
            toolbar.append(languageLabel, copy);
            pre.querySelector(':scope > .code-action')?.remove();

            copy.addEventListener('click', async () => {
                await copyWithFeedback(copy, code.textContent);
            });
        }

        for (const table of document.querySelectorAll('body:not([data-layout="landing"]) article table')) {
            if (table.closest('.doc-table-frame')) continue;

            const frame = document.createElement('div');
            frame.className = 'doc-table-frame';
            const responsive = table.closest('.table-responsive');
            const target = responsive || table;
            target.parentNode.insertBefore(frame, target);
            frame.append(target);
        }

        const initializeAffix = () => {
            const links = [...document.querySelectorAll('#affix a[href^="#"]')];
            const targetGroups = new Map();
            const targetIndexes = new Map();

            for (const link of links) {
                const targetId = decodeURIComponent(link.hash.slice(1));
                if (!targetGroups.has(targetId)) {
                    targetGroups.set(targetId, [...document.querySelectorAll(`[id="${CSS.escape(targetId)}"]`)]);
                }
            }

            const affixLinks = links
                .map(link => {
                    const targetId = decodeURIComponent(link.hash.slice(1));
                    const targetIndex = targetIndexes.get(targetId) || 0;
                    const targets = targetGroups.get(targetId) || [];
                    const target = targets[targetIndex] || targets[0];
                    targetIndexes.set(targetId, targetIndex + 1);

                    if (target && targetIndex > 0) {
                        const uniqueId = `${targetId}--${targetIndex + 1}`;
                        target.id = uniqueId;
                        link.setAttribute('href', `#${uniqueId}`);
                    }

                    return { link, target };
                })
                .filter(item => {
                    const isVisible = item.target?.getClientRects().length;
                    if (!isVisible) item.link.closest('li')?.style.setProperty('display', 'none');
                    return isVisible;
                });
            if (!affixLinks.length) return false;

            let affixFrame;
            const updateAffix = () => {
                if (affixFrame) return;
                affixFrame = window.requestAnimationFrame(() => {
                    const scrollPaddingTop = Number.parseFloat(
                        window.getComputedStyle(document.documentElement).scrollPaddingTop
                    ) || 0;
                    let current = affixLinks[0];

                    for (const item of affixLinks) {
                        const scrollMarginTop = Number.parseFloat(
                            window.getComputedStyle(item.target).scrollMarginTop
                        ) || 0;
                        const targetOffset = scrollPaddingTop + scrollMarginTop + 1;
                        if (item.target.getBoundingClientRect().top <= targetOffset) {
                            current = item;
                        } else {
                            break;
                        }
                    }

                    for (const item of affixLinks) {
                        item.link.classList.toggle('is-active', item === current);
                    }
                    affixFrame = undefined;
                });
            };

            window.addEventListener('scroll', updateAffix, { passive: true });
            updateAffix();
            return true;
        };

        const currentPath = window.location.pathname.replace(/\/index\.html$/, '/');
        for (const link of document.querySelectorAll('#navbar .nav-link')) {
            const linkPath = new URL(link.href, window.location.href).pathname.replace(/\/index\.html$/, '/');
            const isHome = linkPath === '/' && currentPath === '/';
            const isSection = linkPath !== '/' && currentPath.startsWith(linkPath);
            link.classList.toggle('active', isHome || isSection);
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

        if (!initializeAffix()) {
            const affixHost = document.querySelector('main > .affix');
            if (affixHost) {
                const affixObserver = new MutationObserver(() => {
                    if (initializeAffix()) affixObserver.disconnect();
                });
                affixObserver.observe(affixHost, { childList: true, subtree: true });
            }
        }

        // Search results: navigate in same tab with back button support
        document.addEventListener('click', (e) => {
            const link = e.target.closest('#search-results .sr-item a');
            if (!link) return;

            e.preventDefault();
            const query = document.getElementById('search-query')?.value || '';
            history.replaceState({ search: true, query, scrollY: window.scrollY }, '');
            window.location.href = link.href;
        });

        const restoreSearchState = (state) => {
            if (!state?.search) return;

            const input = document.getElementById('search-query');
            if (!input) return;

            input.value = state.query || '';
            input.dispatchEvent(new Event('input', { bubbles: true }));

            if (!Number.isFinite(state.scrollY)) return;

            const restoreScroll = () => {
                if (!document.body.hasAttribute('data-search')) return false;

                const maxScroll = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
                if (maxScroll < state.scrollY) return false;

                window.scrollTo(0, state.scrollY);
                return true;
            };

            if (restoreScroll()) return;

            const observer = new MutationObserver(() => {
                if (restoreScroll()) observer.disconnect();
            });
            observer.observe(document.body, { attributes: true, childList: true, subtree: true });
            window.setTimeout(() => {
                restoreScroll();
                observer.disconnect();
            }, 2000);
        };

        window.addEventListener('popstate', (e) => restoreSearchState(e.state));

        const restoreInitialSearchState = () => {
            if (!history.state?.search) return;

            const input = document.getElementById('search-query');
            if (!input) return;

            if (!input.disabled) {
                restoreSearchState(history.state);
                return;
            }

            const observer = new MutationObserver(() => {
                if (input.disabled) return;

                observer.disconnect();
                restoreSearchState(history.state);
            });
            observer.observe(input, { attributes: true, attributeFilter: ['disabled'] });
        };

        restoreInitialSearchState();
    }
}