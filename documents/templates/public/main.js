const shikiModuleUrl = 'https://esm.sh/shiki@4.3.1';
const slangGrammarUrl = 'https://raw.githubusercontent.com/shader-slang/slang-vscode-extension/v2.0.10/syntaxes/slang.tmLanguage.json';
const shikiLanguageAliases = {
    bash: 'bash',
    console: 'shellsession',
    cs: 'csharp',
    csharp: 'csharp',
    json: 'json',
    powershell: 'powershell',
    shell: 'bash',
    slang: 'slang',
    text: 'text',
    txt: 'text',
    xml: 'xml',
    yaml: 'yaml',
    yml: 'yaml'
};

let fallbackHighlighter;
let shikiHighlighter;

const loadShiki = () => {
    shikiHighlighter ??= Promise.all([
        import(shikiModuleUrl),
        fetch(slangGrammarUrl, { cache: 'force-cache' }).then(response => {
            if (!response.ok) throw new Error(`Unable to load Slang grammar: HTTP ${response.status}`);
            return response.json();
        })
    ]).then(([{ createHighlighter }, slangGrammar]) => {
        slangGrammar.name = 'slang';

        return createHighlighter({
            themes: ['light-plus', 'dark-plus'],
            langs: ['bash', 'csharp', 'json', 'powershell', 'shellsession', 'xml', 'yaml', slangGrammar]
        });
    });

    return shikiHighlighter;
};

const highlightCode = async (code, language) => {
    const source = code.textContent;
    const pre = code.closest('pre');

    try {
        const highlighter = await loadShiki();
        const highlighted = highlighter.codeToHtml(source, {
            lang: shikiLanguageAliases[language] || 'text',
            themes: { light: 'light-plus', dark: 'dark-plus' },
            defaultColor: false
        });
        const template = document.createElement('template');
        template.innerHTML = highlighted;
        const shikiPre = template.content.querySelector('pre');
        const shikiCode = shikiPre?.querySelector('code');
        if (!pre || !shikiPre || !shikiCode) throw new Error('Shiki returned invalid markup.');

        code.innerHTML = shikiCode.innerHTML;
        code.classList.remove('hljs');
        code.dataset.highlighted = 'yes';
        code.dataset.shiki = 'true';
        pre.classList.add('shiki', 'shiki-themes', 'light-plus', 'dark-plus');

        for (const property of shikiPre.style) {
            if (property.startsWith('--shiki-')) {
                pre.style.setProperty(property, shikiPre.style.getPropertyValue(property));
            }
        }
    } catch (error) {
        console.warn('Shiki highlighting failed; using the DocFX highlighter.', error);
        code.textContent = source;
        code.classList.remove('hljs');
        code.removeAttribute('data-highlighted');

        try {
            fallbackHighlighter?.highlightElement(code);
        } catch {
            code.dataset.highlighted = 'yes';
        }
    }
};

export default {
    configureHljs: hljs => {
        fallbackHighlighter = hljs;
        hljs.registerAliases(['slang'], { languageName: 'cpp' });
    },
    iconLinks: [
        {
            icon: 'github',
            href: 'https://github.com/qian-o/Zenith.NET',
            title: 'GitHub'
        }
    ],
    start: () => {
        const rootPath = document.querySelector('meta[name="docfx:rel"]')?.content || '';
        const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
        const observeUntil = (target, options, initialize, timeout = 5000) => {
            if (initialize() || !target) return;

            let timeoutId;
            const observer = new MutationObserver(() => {
                if (!initialize()) return;
                observer.disconnect();
                window.clearTimeout(timeoutId);
            });
            observer.observe(target, options);
            timeoutId = window.setTimeout(() => observer.disconnect(), timeout);
        };
        document.body.toggleAttribute('data-zenith-api', /\/api(?:\/|$)/i.test(window.location.pathname));

        const initializeThemeCycle = () => {
            const themes = [
                { value: 'light', label: 'Light', icon: 'bi-sun' },
                { value: 'dark', label: 'Dark', icon: 'bi-moon' },
                { value: 'auto', label: 'Auto', icon: 'bi-circle-half' }
            ];
            const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
            const navbar = document.getElementById('navbar');
            if (!navbar) return;

            const enhanceThemeToggle = () => {
                const dropdown = navbar.querySelector('.icons .dropdown');
                const currentToggle = dropdown?.querySelector('.dropdown-toggle');
                if (!dropdown || !currentToggle) return false;

                const themeToggle = document.createElement('button');
                const icon = document.createElement('i');
                let animationTimer;

                themeToggle.type = 'button';
                themeToggle.className = 'btn border-0 zenith-theme-toggle';
                icon.setAttribute('aria-hidden', 'true');
                themeToggle.append(icon);
                dropdown.replaceWith(themeToggle);

                const getTheme = () => {
                    const storedTheme = window.localStorage.getItem('theme');
                    return themes.find(theme => theme.value === storedTheme) || themes[2];
                };

                const getNextTheme = theme => themes[(themes.indexOf(theme) + 1) % themes.length];

                const renderTheme = theme => {
                    const nextTheme = getNextTheme(theme);
                    icon.className = `bi ${theme.icon}`;
                    themeToggle.dataset.theme = theme.value;
                    themeToggle.title = `${theme.label} theme; switch to ${nextTheme.label}`;
                    themeToggle.setAttribute(
                        'aria-label',
                        `${theme.label} theme. Switch to ${nextTheme.label}.`
                    );
                };

                const applyTheme = theme => {
                    const resolvedTheme = theme.value === 'auto'
                        ? (systemTheme.matches ? 'dark' : 'light')
                        : theme.value;
                    window.localStorage.setItem('theme', theme.value);
                    document.documentElement.dataset.bsTheme = resolvedTheme;
                    renderTheme(theme);
                };

                const switchTheme = () => {
                    const nextTheme = getNextTheme(getTheme());
                    window.clearTimeout(animationTimer);

                    if (reducedMotion.matches) {
                        applyTheme(nextTheme);
                        return;
                    }

                    themeToggle.disabled = true;
                    themeToggle.classList.remove('is-theme-entering');
                    themeToggle.classList.add('is-theme-leaving');
                    animationTimer = window.setTimeout(() => {
                        applyTheme(nextTheme);
                        themeToggle.classList.remove('is-theme-leaving');
                        themeToggle.classList.add('is-theme-entering');
                        animationTimer = window.setTimeout(() => {
                            themeToggle.classList.remove('is-theme-entering');
                            themeToggle.disabled = false;
                        }, 260);
                    }, 120);
                };

                themeToggle.addEventListener('click', switchTheme);
                systemTheme.addEventListener('change', () => {
                    const theme = getTheme();
                    if (theme.value === 'auto') applyTheme(theme);
                });
                applyTheme(getTheme());
                return true;
            };

            observeUntil(navbar, { childList: true, subtree: true }, enhanceThemeToggle);
        };

        const initializeExpandableSearch = () => {
            const form = document.querySelector('#navbar form.search');
            const input = form?.querySelector('#search-query');
            const nativeIcon = form?.querySelector(':scope > i');
            if (!form || !input || form.classList.contains('zenith-search')) return;

            const toggle = document.createElement('button');
            const icon = document.createElement('i');
            toggle.type = 'button';
            toggle.className = 'zenith-search-toggle';
            toggle.setAttribute('aria-controls', input.id);
            toggle.setAttribute('aria-expanded', 'false');
            toggle.setAttribute('aria-label', 'Open search');
            toggle.title = 'Search';
            icon.className = 'bi bi-search';
            icon.setAttribute('aria-hidden', 'true');
            toggle.append(icon);
            nativeIcon?.setAttribute('aria-hidden', 'true');
            form.prepend(toggle);
            form.classList.add('zenith-search');
            const searchPlaceholder = input.placeholder || 'Search';

            const updateAvailability = () => {
                const ready = !input.disabled;
                const expanded = form.classList.contains('is-expanded');
                form.classList.toggle('is-loading', !ready);
                form.setAttribute('aria-busy', String(!ready));
                toggle.setAttribute('aria-label', expanded ? 'Close search' : 'Open search');
                toggle.title = ready ? (expanded ? 'Close search' : 'Search') : 'Search is loading';
                icon.className = 'bi bi-search';
                input.placeholder = ready ? searchPlaceholder : 'Search is loading...';
            };
            const availabilityObserver = new MutationObserver(updateAvailability);
            availabilityObserver.observe(input, { attributes: true, attributeFilter: ['disabled'] });
            updateAvailability();
            let pendingFocusObserver;

            const setExpanded = (expanded, focusInput = false) => {
                if (!expanded) {
                    pendingFocusObserver?.disconnect();
                    pendingFocusObserver = undefined;
                }
                form.classList.toggle('is-expanded', expanded);
                toggle.setAttribute('aria-expanded', String(expanded));
                toggle.setAttribute('aria-label', expanded ? 'Close search' : 'Open search');
                toggle.title = input.disabled ? 'Search is loading' : (expanded ? 'Close search' : 'Search');

                if (!focusInput) return;
                const focusWhenReady = () => {
                    if (input.disabled) return false;
                    input.focus({ preventScroll: true });
                    return true;
                };
                if (focusWhenReady()) return;

                pendingFocusObserver?.disconnect();
                pendingFocusObserver = new MutationObserver(() => {
                    if (!form.classList.contains('is-expanded')) {
                        pendingFocusObserver?.disconnect();
                        pendingFocusObserver = undefined;
                        return;
                    }
                    if (!focusWhenReady()) return;
                    pendingFocusObserver?.disconnect();
                    pendingFocusObserver = undefined;
                });
                pendingFocusObserver.observe(input, { attributes: true, attributeFilter: ['disabled'] });
            };

            const clearSearch = () => {
                if (!input.value) return;
                input.value = '';
                input.dispatchEvent(new Event('input', { bubbles: true }));
            };

            toggle.addEventListener('click', () => {
                const expanded = form.classList.contains('is-expanded');
                if (expanded) {
                    clearSearch();
                    setExpanded(false);
                    toggle.focus({ preventScroll: true });
                } else {
                    setExpanded(true, true);
                }
            });

            input.addEventListener('focus', () => setExpanded(true));
            input.addEventListener('keydown', event => {
                if (event.key !== 'Escape') return;
                event.preventDefault();
                clearSearch();
                setExpanded(false);
                toggle.focus({ preventScroll: true });
            });
            document.addEventListener('pointerdown', event => {
                if (!form.classList.contains('is-expanded') || form.contains(event.target)) return;
                if (!input.value && !document.body.hasAttribute('data-search')) setExpanded(false);
            });

            if (input.value || document.body.hasAttribute('data-search')) setExpanded(true);
        };

        const initializeReadingMotion = () => {
            const header = document.querySelector('body > header');
            if (!header) return;

            const progress = document.createElement('div');
            const progressValue = document.createElement('span');
            progress.className = 'zenith-reading-progress';
            progress.setAttribute('aria-hidden', 'true');
            progress.append(progressValue);
            header.append(progress);

            const hasScrollTimeline = window.CSS?.supports?.('animation-timeline: scroll()') === true;

            const updateScrollState = () => {
                const scrollRange = Math.max(1, document.documentElement.scrollHeight - window.innerHeight);
                const value = Math.min(1, Math.max(0, window.scrollY / scrollRange));
                if (!hasScrollTimeline) progressValue.style.transform = `scaleX(${value.toFixed(4)})`;
                header.classList.toggle('is-scrolled', window.scrollY > 8);
            };

            window.addEventListener('scroll', updateScrollState, { passive: true });
            window.addEventListener('resize', updateScrollState, { passive: true });
            updateScrollState();
        };

        const initializeOverlayScrollbars = () => {
            const initialized = new WeakSet();
            const hideTimers = new WeakMap();
            const updates = new WeakMap();

            const attach = (scroller, host) => {
                if (!scroller || !host) return;
                if (initialized.has(scroller)) {
                    updates.get(scroller)?.();
                    return;
                }
                initialized.add(scroller);
                host.classList.add('zenith-scroll-host');

                const track = document.createElement('span');
                const thumb = document.createElement('span');
                track.className = 'zenith-overlay-scrollbar';
                track.setAttribute('role', 'scrollbar');
                track.setAttribute('aria-label', host.id === 'toc'
                    ? 'Table of contents scrollbar'
                    : 'In this article scrollbar');
                track.setAttribute('aria-orientation', 'vertical');
                track.setAttribute('aria-valuemin', '0');
                if (!scroller.id) scroller.id = `${host.id || 'zenith'}-scroll-region`;
                track.setAttribute('aria-controls', scroller.id);
                track.append(thumb);
                host.append(track);

                const update = () => {
                    const hostRect = host.getBoundingClientRect();
                    const scrollerRect = scroller.getBoundingClientRect();
                    const trackStyle = window.getComputedStyle(track);
                    const scrollbarSize = Number.parseFloat(
                        trackStyle.getPropertyValue('--zenith-scrollbar-size')
                    ) || track.offsetWidth;
                    const edgeGap = Number.parseFloat(
                        trackStyle.getPropertyValue('--zenith-scrollbar-edge-gap')
                    ) || (scrollbarSize / 2);
                    const minThumbHeight = Number.parseFloat(
                        trackStyle.getPropertyValue('--zenith-scrollbar-min-thumb')
                    ) || (scrollbarSize * 4.5);
                    const trackHeight = Math.max(0, scroller.clientHeight - (edgeGap * 2));
                    const scrollRange = Math.max(0, scroller.scrollHeight - scroller.clientHeight);
                    const thumbHeight = scrollRange
                        ? Math.max(minThumbHeight, trackHeight * (scroller.clientHeight / scroller.scrollHeight))
                        : trackHeight;
                    const thumbRange = Math.max(0, trackHeight - thumbHeight);
                    const thumbOffset = scrollRange ? (scroller.scrollTop / scrollRange) * thumbRange : 0;

                    track.style.top = `${scrollerRect.top - hostRect.top + edgeGap}px`;
                    track.style.height = `${trackHeight}px`;
                    track.tabIndex = scrollRange > 1 ? 0 : -1;
                    track.setAttribute('aria-valuemax', `${Math.round(scrollRange)}`);
                    track.setAttribute('aria-valuenow', `${Math.round(scroller.scrollTop)}`);
                    thumb.style.height = `${thumbHeight}px`;
                    thumb.style.transform = `translateY(${thumbOffset}px)`;
                    host.classList.toggle('has-scroll-range', scrollRange > 1);
                };
                updates.set(scroller, update);

                const showWhileScrolling = () => {
                    host.classList.add('is-scrolling');
                    window.clearTimeout(hideTimers.get(host));
                    hideTimers.set(host, window.setTimeout(() => {
                        host.classList.remove('is-scrolling');
                    }, 700));
                    update();
                };

                scroller.addEventListener('scroll', showWhileScrolling, { passive: true });
                window.addEventListener('resize', update, { passive: true });
                const resizeObserver = typeof ResizeObserver === 'undefined'
                    ? null
                    : new ResizeObserver(update);
                resizeObserver?.observe(scroller);
                resizeObserver?.observe(host);
                new MutationObserver(update).observe(scroller, { childList: true, subtree: true });

                track.addEventListener('keydown', event => {
                    const lineStep = scroller.clientHeight / 10;
                    const pageStep = scroller.clientHeight;
                    const scrollRange = Math.max(0, scroller.scrollHeight - scroller.clientHeight);
                    const target = {
                        ArrowUp: scroller.scrollTop - lineStep,
                        ArrowDown: scroller.scrollTop + lineStep,
                        PageUp: scroller.scrollTop - pageStep,
                        PageDown: scroller.scrollTop + pageStep,
                        Home: 0,
                        End: scrollRange
                    }[event.key];
                    if (target === undefined) return;

                    event.preventDefault();
                    scroller.scrollTop = Math.min(scrollRange, Math.max(0, target));
                });

                track.addEventListener('pointerdown', event => {
                    if (!host.classList.contains('has-scroll-range')) return;
                    event.preventDefault();

                    const trackRect = track.getBoundingClientRect();
                    const thumbRect = thumb.getBoundingClientRect();
                    const startY = event.clientY;
                    let startScrollTop = scroller.scrollTop;
                    const thumbRange = Math.max(1, trackRect.height - thumbRect.height);
                    const scrollRange = Math.max(0, scroller.scrollHeight - scroller.clientHeight);

                    if (event.target !== thumb) {
                        const targetOffset = Math.min(
                            thumbRange,
                            Math.max(0, event.clientY - trackRect.top - (thumbRect.height / 2))
                        );
                        scroller.scrollTop = (targetOffset / thumbRange) * scrollRange;
                        startScrollTop = scroller.scrollTop;
                    }

                    track.classList.add('is-dragging');
                    track.setPointerCapture(event.pointerId);
                    const move = moveEvent => {
                        scroller.scrollTop = startScrollTop
                            + ((moveEvent.clientY - startY) / thumbRange) * scrollRange;
                    };
                    const stop = () => {
                        track.classList.remove('is-dragging');
                        track.removeEventListener('pointermove', move);
                        track.removeEventListener('pointerup', stop);
                        track.removeEventListener('pointercancel', stop);
                    };
                    track.addEventListener('pointermove', move);
                    track.addEventListener('pointerup', stop);
                    track.addEventListener('pointercancel', stop);
                });
                update();
            };

            const initialize = () => {
                const tocScroller = document.querySelector('#toc > .overflow-y-auto');
                attach(tocScroller, document.querySelector('#toc'));

                const affix = document.querySelector('#affix');
                attach(affix, affix?.parentElement);
            };

            const observer = new MutationObserver(initialize);
            for (const host of document.querySelectorAll('#toc, main > .affix')) {
                observer.observe(host, { childList: true, subtree: true });
            }
            initialize();
        };

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

        const brand = document.querySelector('.navbar-brand');
        if (brand && !brand.hasAttribute('aria-label')) {
            brand.setAttribute('aria-label', 'Zenith.NET home');
        }
        initializeThemeCycle();
        initializeExpandableSearch();

        const languageNames = {
            bash: 'Shell',
            csharp: 'C#',
            cs: 'C#',
            console: 'Console',
            json: 'JSON',
            powershell: 'PowerShell',
            shell: 'Shell',
            slang: 'Slang',
            xml: 'XML',
            yaml: 'YAML',
            yml: 'YAML'
        };

        const createCodeFrame = pre => {
            const code = pre.querySelector('code');
            if (!code) return null;

            const languageClass = [...code.classList].find(name => name.startsWith('language-') || name.startsWith('lang-'));
            const language = languageClass?.replace(/^language-|^lang-/, '').toLowerCase() || 'text';
            const existingFrame = pre.closest('.doc-code-frame');
            if (existingFrame) return { code, language };
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
            toolbar.append(languageLabel);
            toolbar.append(copy);
            pre.querySelector(':scope > .code-action')?.remove();

            copy.addEventListener('click', async () => {
                await copyWithFeedback(copy, code.textContent);
            });

            return { code, language };
        };

        for (const pre of document.querySelectorAll('body:not([data-layout="landing"]) article pre')) {
            const controls = createCodeFrame(pre);
            if (controls) void highlightCode(controls.code, controls.language);
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

        const initializeTableScrollCues = () => {
            const frames = [...document.querySelectorAll('.doc-table-frame')];
            if (!frames.length) return;

            const observedScrollers = new WeakSet();
            const updates = new WeakMap();
            const resizeObserver = typeof ResizeObserver === 'undefined'
                ? null
                : new ResizeObserver(entries => {
                    for (const entry of entries) updates.get(entry.target)?.();
                });

            const initializeFrame = frame => {
                const scroller = frame.querySelector('.table-responsive');
                if (!scroller || observedScrollers.has(scroller)) return false;
                observedScrollers.add(scroller);

                const update = () => {
                    const maxScroll = Math.max(0, scroller.scrollWidth - scroller.clientWidth);
                    frame.classList.toggle('is-scrollable', maxScroll > 1);
                    frame.classList.toggle('is-scroll-end', scroller.scrollLeft >= maxScroll - 1);
                };

                scroller.addEventListener('scroll', update, { passive: true });
                if (!resizeObserver) window.addEventListener('resize', update, { passive: true });
                updates.set(scroller, update);
                resizeObserver?.observe(scroller);

                const table = scroller.querySelector('table');
                if (table) {
                    updates.set(table, update);
                    resizeObserver?.observe(table);
                }
                update();
                return true;
            };

            const pendingFrames = new Set(frames);
            const initializePendingFrames = () => {
                for (const frame of pendingFrames) {
                    if (initializeFrame(frame)) pendingFrames.delete(frame);
                }
                if (!pendingFrames.size) mutationObserver.disconnect();
            };
            const mutationObserver = new MutationObserver(initializePendingFrames);

            for (const frame of pendingFrames) {
                mutationObserver.observe(frame, { childList: true, subtree: true });
            }
            initializePendingFrames();
        };

        const initializeTutorialCarousel = () => {
            const carousel = document.querySelector('[data-tutorial-carousel]');
            if (!carousel) return;

            const slides = [...carousel.querySelectorAll('.render-carousel-slide')];
            const title = carousel.querySelector('[data-carousel-title]');
            const position = carousel.querySelector('[data-carousel-position]');
            const status = carousel.querySelector('[data-carousel-status]');
            const progress = carousel.querySelector('[data-carousel-progress]');
            const openLink = carousel.querySelector('[data-carousel-open]');
            const toggleButton = carousel.querySelector('[data-carousel-toggle]');
            const previousButton = carousel.querySelector('[data-carousel-prev]');
            const nextButton = carousel.querySelector('[data-carousel-next]');
            const mobileViewport = window.matchMedia('(max-width: 767.98px)');
            const autoPlayDuration = 6500;
            let activeIndex = Math.max(0, slides.findIndex(slide => slide.classList.contains('is-active')));
            let autoPlayTimer = 0;
            let autoPlayRemaining = autoPlayDuration;
            let autoPlayStartedAt = 0;
            let isAutoPlayPaused = false;
            let isVisible = true;
            let touchStartX = 0;
            let touchStartY = 0;

            if (slides.length < 2 || !title || !position || !progress || !openLink || !toggleButton || !previousButton || !nextButton) return;

            const prepareSlide = index => {
                const image = slides[index]?.querySelector('img[data-src]');
                if (!image?.dataset.src) return;

                image.src = image.dataset.src;
                image.removeAttribute('data-src');
            };

            const stopAutoPlay = () => {
                window.clearTimeout(autoPlayTimer);
                autoPlayTimer = 0;
                if (autoPlayStartedAt) {
                    autoPlayRemaining = Math.max(0, autoPlayRemaining - (performance.now() - autoPlayStartedAt));
                    autoPlayStartedAt = 0;
                }
                carousel.classList.remove('is-carousel-playing');
            };

            const canAutoPlay = () => !isAutoPlayPaused &&
                !reducedMotion.matches &&
                !mobileViewport.matches &&
                !document.hidden &&
                isVisible &&
                !carousel.matches(':hover') &&
                !carousel.matches(':focus-within');

            const scheduleAutoPlay = () => {
                if (!canAutoPlay()) {
                    stopAutoPlay();
                    return;
                }
                if (autoPlayTimer) return;

                autoPlayStartedAt = performance.now();
                carousel.classList.add('is-carousel-playing');
                autoPlayTimer = window.setTimeout(() => {
                    autoPlayTimer = 0;
                    autoPlayStartedAt = 0;
                    autoPlayRemaining = autoPlayDuration;
                    showSlide(activeIndex + 1);
                }, Math.max(16, autoPlayRemaining));
            };

            const showSlide = (index, announce = false) => {
                stopAutoPlay();
                activeIndex = (index + slides.length) % slides.length;
                prepareSlide(activeIndex);
                prepareSlide((activeIndex + 1) % slides.length);

                for (const [slideIndex, slide] of slides.entries()) {
                    const isActive = slideIndex === activeIndex;
                    slide.classList.toggle('is-active', isActive);
                    slide.setAttribute('aria-hidden', String(!isActive));
                    if (isActive) {
                        slide.setAttribute('aria-current', 'true');
                    } else {
                        slide.removeAttribute('aria-current');
                    }
                    slide.tabIndex = isActive ? 0 : -1;
                }

                const activeSlide = slides[activeIndex];
                const activeTitle = activeSlide.dataset.carouselTitle || `Tutorial ${activeIndex + 1}`;
                title.textContent = activeTitle;
                position.textContent = `${activeIndex + 1} / ${slides.length}`;
                openLink.href = activeSlide.href;
                openLink.setAttribute('aria-label', `Open ${activeTitle} tutorial`);
                if (announce && status) {
                    status.textContent = `${activeTitle}, slide ${activeIndex + 1} of ${slides.length}`;
                }

                autoPlayRemaining = autoPlayDuration;
                progress.classList.remove('is-timing');
                void progress.offsetWidth;
                progress.classList.add('is-timing');
                scheduleAutoPlay();
            };

            const updateToggleButton = () => {
                const label = isAutoPlayPaused ? 'Start automatic slide rotation' : 'Pause automatic slide rotation';
                toggleButton.setAttribute('aria-label', label);
                toggleButton.title = label;
                toggleButton.innerHTML = `<i class="bi bi-${isAutoPlayPaused ? 'play-fill' : 'pause-fill'}" aria-hidden="true"></i>`;
            };

            toggleButton.addEventListener('click', () => {
                isAutoPlayPaused = !isAutoPlayPaused;
                updateToggleButton();
                if (isAutoPlayPaused) {
                    stopAutoPlay();
                } else {
                    autoPlayRemaining = autoPlayDuration;
                    scheduleAutoPlay();
                }
            });
            previousButton.addEventListener('click', () => showSlide(activeIndex - 1, true));
            nextButton.addEventListener('click', () => showSlide(activeIndex + 1, true));

            carousel.addEventListener('keydown', event => {
                if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;

                event.preventDefault();
                showSlide(activeIndex + (event.key === 'ArrowRight' ? 1 : -1), true);
            });
            carousel.addEventListener('mouseenter', stopAutoPlay);
            carousel.addEventListener('mouseleave', scheduleAutoPlay);
            carousel.addEventListener('focusin', stopAutoPlay);
            carousel.addEventListener('focusout', () => window.requestAnimationFrame(scheduleAutoPlay));
            carousel.addEventListener('touchstart', event => {
                const touch = event.changedTouches[0];
                touchStartX = touch.clientX;
                touchStartY = touch.clientY;
                stopAutoPlay();
            }, { passive: true });
            carousel.addEventListener('touchend', event => {
                const touch = event.changedTouches[0];
                const deltaX = touch.clientX - touchStartX;
                const deltaY = touch.clientY - touchStartY;
                if (Math.abs(deltaX) >= 44 && Math.abs(deltaX) > Math.abs(deltaY)) {
                    showSlide(activeIndex + (deltaX < 0 ? 1 : -1), true);
                } else {
                    scheduleAutoPlay();
                }
            }, { passive: true });

            document.addEventListener('visibilitychange', scheduleAutoPlay);
            reducedMotion.addEventListener('change', scheduleAutoPlay);
            mobileViewport.addEventListener('change', scheduleAutoPlay);

            if ('IntersectionObserver' in window) {
                const observer = new IntersectionObserver(entries => {
                    isVisible = entries[0]?.isIntersecting ?? true;
                    scheduleAutoPlay();
                }, { threshold: 0.2 });
                observer.observe(carousel);
            }

            carousel.classList.add('is-carousel-ready');
            showSlide(activeIndex);
        };

        const initializeRevealMotion = () => {
            const selectors = document.body.matches('[data-layout="landing"]')
                ? [
                    '.section-heading',
                    '.feature-card',
                    '.architecture-copy',
                    '.resources-heading',
                    '.resource-card',
                    '.landing-footer-grid > *'
                ]
                : [
                    'article h1:first-of-type',
                    'article h2',
                    'article .alert',
                    'article .doc-code-frame',
                    'article .doc-table-frame',
                    'article .tabGroup'
                ];
            const targets = [...document.querySelectorAll(selectors.join(','))]
                .filter(element => element.getClientRects().length);
            if (!targets.length || reducedMotion.matches) {
                targets.forEach(element => element.classList.add('is-visible'));
                return;
            }

            for (const [index, target] of targets.entries()) {
                target.classList.add('zenith-reveal');
                target.style.setProperty('--zenith-reveal-delay', `${Math.min(index % 4, 3) * 45}ms`);
                if (document.body.matches('[data-layout="landing"]')) {
                    const direction = target.matches('.architecture-grid > .architecture-copy:first-child')
                        ? 'left'
                        : target.matches('.architecture-grid > .architecture-copy:last-child')
                            ? 'right'
                            : 'up';
                    target.dataset.revealFrom = direction;
                }
            }
            document.documentElement.classList.add('zenith-motion-ready');

            const pending = new Set(targets);
            const revealVisibleTargets = () => {
                const revealBoundary = window.innerHeight * 0.92;
                for (const target of pending) {
                    const rect = target.getBoundingClientRect();
                    if (rect.top >= revealBoundary || rect.bottom <= 0) continue;

                    target.classList.add('is-visible');
                    pending.delete(target);
                }

                if (!pending.size) {
                    window.removeEventListener('scroll', revealVisibleTargets);
                    window.removeEventListener('resize', revealVisibleTargets);
                }
            };

            window.addEventListener('scroll', revealVisibleTargets, { passive: true });
            window.addEventListener('resize', revealVisibleTargets, { passive: true });
            revealVisibleTargets();
        };

        initializeReadingMotion();
        initializeOverlayScrollbars();
        initializeTableScrollCues();
        initializeTutorialCarousel();

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

            const updateAffix = () => {
                const scrollPaddingTop = Number.parseFloat(
                    window.getComputedStyle(document.documentElement).scrollPaddingTop
                ) || 0;
                let current = affixLinks[0];
                let start = 0;
                let end = affixLinks.length - 1;

                while (start <= end) {
                    const middle = Math.floor((start + end) / 2);
                    const item = affixLinks[middle];
                    const scrollMarginTop = Number.parseFloat(
                        window.getComputedStyle(item.target).scrollMarginTop
                    ) || 0;
                    const targetOffset = scrollPaddingTop + scrollMarginTop + 1;

                    if (item.target.getBoundingClientRect().top <= targetOffset) {
                        current = item;
                        start = middle + 1;
                    } else {
                        end = middle - 1;
                    }
                }

                for (const item of affixLinks) {
                    item.link.classList.toggle('is-active', item === current);
                }
            };

            window.addEventListener('scroll', updateAffix, { passive: true });
            updateAffix();
            return true;
        };

        const normalizePath = path => path.replace(/\/index\.html$/, '/');
        const siteRootPath = normalizePath(new URL(rootPath || './', window.location.href).pathname);
        const currentPath = normalizePath(window.location.pathname);
        for (const link of document.querySelectorAll('#navbar .nav-link')) {
            const linkPath = normalizePath(new URL(link.href, window.location.href).pathname);
            const isHome = linkPath === siteRootPath && currentPath === siteRootPath;
            const isSection = linkPath !== siteRootPath && currentPath.startsWith(linkPath);
            const isActive = isHome || isSection;
            link.classList.toggle('active', isActive);
            if (isActive) {
                link.setAttribute('aria-current', 'page');
            } else {
                link.removeAttribute('aria-current');
            }
        }

        // Hide inherited type info sections (inheritance, implements, inheritedMembers, derivedClasses)
        for (const dl of document.querySelectorAll('dl.typelist.inheritance, dl.typelist.implements, dl.typelist.inheritedMembers, dl.typelist.derivedClasses')) {
            dl.style.display = 'none';
        }

        // Hide protected members from API pages
        for (const code of document.querySelectorAll('.codewrapper pre code')) {
            const text = code.textContent.trim();
            if (!/^(?:private\s+)?protected\b/.test(text)) continue;

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

        observeUntil(
            document.querySelector('main > .affix'),
            { childList: true, subtree: true },
            initializeAffix
        );

        initializeRevealMotion();

        // Search results: navigate in same tab with back button support
        document.addEventListener('click', event => {
            const link = event.target instanceof Element
                ? event.target.closest('#search-results .sr-item a')
                : null;
            if (!link) return;
            if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey ||
                event.shiftKey || event.altKey || link.hasAttribute('download') ||
                (link.target && link.target !== '_self')) return;

            event.preventDefault();
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

            observeUntil(input, { attributes: true, attributeFilter: ['disabled'] }, () => {
                if (input.disabled) return;
                restoreSearchState(history.state);
                return true;
            });
        };

        restoreInitialSearchState();
    }
}
