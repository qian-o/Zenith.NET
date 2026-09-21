import { t } from './resources.js';
import { closeContents, closeNavigation, isApplePlatform, isEditing } from './site.js';
import { bindDialogKeys, isBackdropClick } from './dialog.js';

// A page-local symbol finder. Every result points to a stable member anchor.
export function initializeMemberFinder() {
    const dialog = document.getElementById('api-member-dialog');
    if (!dialog) return;

    const input = document.getElementById('api-member-query');
    const results = document.getElementById('api-member-results');
    const empty = document.getElementById('api-member-empty');
    const clear = document.getElementById('api-member-clear');
    const launchers = [...document.querySelectorAll('[data-member-find]')];
    const groups = [...document.querySelectorAll('.api-reference > .api-group')].map(section => ({
        name: section.querySelector('.api-group-heading h2')?.textContent.trim(),
        members: [...section.querySelectorAll('.api-member-heading h3[id], .api-enum-value h3[id]')].map(heading => ({
            heading,
            name: heading.textContent.trim(),
            signature: heading.closest('.api-member')?.querySelector(':scope > pre > code')?.textContent || heading.textContent
        }))
    })).filter(group => group.members.length);
    if (!groups.length) empty.textContent = t('ui.members.undeclared');

    let opener;
    let restoreFocus = true;
    const shortcut = isApplePlatform ? '⌥ M' : 'Alt M';
    for (const launcher of launchers) {
        launcher.hidden = false;
        launcher.title = `${t('ui.members.title')} (${shortcut})`;
        const key = launcher.querySelector('[data-member-shortcut]');
        if (key) key.textContent = shortcut;
        launcher.addEventListener('click', () => open(launcher));
    }

    function render() {
        const terms = input.value.toLowerCase().trim().split(/\s+/).filter(Boolean);
        const fragment = document.createDocumentFragment();
        for (const group of groups) {
            const matches = group.members.filter(member => {
                const text = `${group.name} ${member.name} ${member.signature}`.toLowerCase();
                return terms.every(term => text.includes(term));
            });
            if (!matches.length) continue;
            const section = document.createElement('section');
            const label = document.createElement('h3');
            const list = document.createElement('ul');
            label.textContent = group.name;
            for (const member of matches) {
                const item = document.createElement('li');
                const link = document.createElement('a');
                link.href = '#' + encodeURIComponent(member.heading.id);
                link.dataset.memberTarget = member.heading.id;
                link.textContent = member.name;
                item.append(link);
                list.append(item);
            }
            section.append(label, list);
            fragment.append(section);
        }
        results.replaceChildren(fragment);
        empty.hidden = results.childElementCount > 0;
        clear.hidden = !input.value;
        dialog.querySelector('.search-scroll').scrollTop = 0;
    }

    function isVisible(button) {
        if (!button || !button.getClientRects().length || getComputedStyle(button).visibility !== 'visible') return false;
        const drawer = button.closest('#tocOffcanvas');
        if (drawer && window.matchMedia('(max-width: 767.98px)').matches && (!drawer.classList.contains('show') || drawer.classList.contains('hiding'))) return false;
        const bounds = button.getBoundingClientRect();
        return bounds.right > 0 && bounds.left < innerWidth && bounds.bottom > 0 && bounds.top < innerHeight;
    }

    function visibleLauncher() {
        return launchers.find(isVisible);
    }

    function open(button) {
        if (document.querySelector('dialog[open]')) return;
        opener = button || visibleLauncher();
        restoreFocus = true;
        closeContents();
        closeNavigation();
        render();
        dialog.showModal();
        input.focus();
        input.select();
    }

    document.getElementById('api-member-close').addEventListener('click', () => dialog.close());
    dialog.addEventListener('cancel', event => {
        event.preventDefault();
        dialog.close();
    });
    dialog.addEventListener('close', () => {
        if (restoreFocus && !document.querySelector('dialog[open]')) {
            const menu = document.getElementById('menu-toggle');
            const target = isVisible(opener) ? opener : visibleLauncher() || (isVisible(menu) ? menu : null);
            target?.focus({ preventScroll: true });
        }
    });
    dialog.addEventListener('click', event => {
        if (event.target === dialog) {
            if (isBackdropClick(event, dialog)) dialog.close();
            return;
        }
        const link = event.target.closest('[data-member-target]');
        if (!link || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        const target = document.getElementById(link.dataset.memberTarget);
        restoreFocus = false;
        dialog.close();
        target.tabIndex = -1;
        // Keep native anchor navigation/history; move keyboard focus after scrolling.
        requestAnimationFrame(() => {
            target.focus({ preventScroll: true });
            target.scrollIntoView({ block: 'start', behavior: 'auto' });
        });
    });
    input.addEventListener('input', render);
    clear.addEventListener('click', () => {
        input.value = '';
        render();
        input.focus();
    });
    document.getElementById('api-member-form').addEventListener('submit', event => {
        event.preventDefault();
        results.querySelector('a')?.click();
    });
    bindDialogKeys(dialog, { items: '#api-member-results a', close: () => dialog.close() });
    document.addEventListener('keydown', event => {
        if (event.isComposing || !event.altKey || event.ctrlKey || event.metaKey || event.shiftKey || event.repeat || event.code !== 'KeyM' && event.key.toLowerCase() !== 'm') return;
        if (isEditing(event.target)) return;
        event.preventDefault();
        if (dialog.open) dialog.close();
        else open();
    });
    const dismissForNavigation = () => {
        restoreFocus = false;
        dialog.close();
    };
    window.addEventListener('pagehide', dismissForNavigation);
    window.addEventListener('popstate', dismissForNavigation);
}
