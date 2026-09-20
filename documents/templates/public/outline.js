// Article navigation owns its markup and active state; links keep native hashes/history.
export function initializeOutline() {
    const nav = document.getElementById('article-outline');
    if (!nav) return;
    const headings = [...document.querySelectorAll('article h2[id], article h3[id]')];
    if (!headings.length) {
        nav.parentElement.hidden = true;
        return;
    }

    const label = document.createElement('div');
    label.className = 'outline-title';
    label.textContent = 'On this page';
    const list = document.createElement('ul');
    list.className = 'outline-list';
    let group;
    const entries = headings.map(heading => {
        const item = document.createElement('li');
        const link = document.createElement('a');
        link.className = 'outline-link';
        link.href = '#' + encodeURIComponent(heading.id);
        link.textContent = heading.textContent;
        item.append(link);
        if (heading.tagName === 'H2' || !group) {
            list.append(item);
            group = item;
        } else {
            let children = group.querySelector('ul');
            if (!children) {
                children = document.createElement('ul');
                group.append(children);
            }
            children.append(item);
        }
        // Make the native fragment destination focusable for keyboard navigation.
        heading.tabIndex = -1;
        heading.classList.add('outline-target');
        return { heading, link, group };
    });
    nav.append(label, list);

    const desktop = window.matchMedia('(min-width: 1200px)');
    let active;
    let frame;
    let reveal = false;
    const update = () => {
        frame = undefined;
        const forceReveal = reveal;
        reveal = false;
        if (!desktop.matches) return;
        const threshold = document.querySelector('.site-header').getBoundingClientRect().bottom + 36;
        let current = entries[0];
        for (const entry of entries) {
            if (entry.heading.getBoundingClientRect().top > threshold) break;
            current = entry;
        }
        if (active === current && !forceReveal) return;
        active?.link.removeAttribute('aria-current');
        active?.group.classList.remove('outline-section-current');
        current.link.setAttribute('aria-current', 'location');
        current.group.classList.add('outline-section-current');
        active = current;

        // Follow reading progress inside the outline, without moving the article
        // or interrupting someone browsing the outline with a pointer/keyboard.
        if (forceReveal || !nav.matches(':hover, :focus-within')) {
            const bounds = list.getBoundingClientRect();
            const target = current.link.getBoundingClientRect();
            if (target.top < bounds.top + 8) list.scrollTop += target.top - bounds.top - 8;
            else if (target.bottom > bounds.bottom - 8) list.scrollTop += target.bottom - bounds.bottom + 8;
        }
    };
    const schedule = (force = false) => {
        if (force === true) reveal = true;
        frame ??= requestAnimationFrame(update);
    };
    window.addEventListener('scroll', schedule, { passive: true });
    window.addEventListener('resize', () => schedule(true));
    window.addEventListener('hashchange', () => schedule(true));
    window.addEventListener('pageshow', () => schedule(true));
    window.addEventListener('pagehide', () => {
        cancelAnimationFrame(frame);
        frame = undefined;
    });
    new ResizeObserver(schedule).observe(document.querySelector('article'));
    schedule();
}
