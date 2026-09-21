export function isBackdropClick(event, dialog) {
    if (event.target !== dialog) return false;
    const bounds = dialog.getBoundingClientRect();
    return (
        event.clientX < bounds.left ||
        event.clientX > bounds.right ||
        event.clientY < bounds.top ||
        event.clientY > bounds.bottom
    );
}

// Escape closes once, including Safari search inputs; arrows stay within visible results.
export function bindDialogKeys(dialog, { items, close }) {
    dialog.addEventListener(
        'keydown',
        event => {
            if (event.isComposing) return;
            if (event.key === 'Escape') {
                event.preventDefault();
                event.stopPropagation();
                close();
                return;
            }
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
            const links = [...dialog.querySelectorAll(items)].filter(link => link.getClientRects().length);
            if (!links.length) return;
            event.preventDefault();
            const current = links.indexOf(document.activeElement);
            const next =
                event.key === 'ArrowDown'
                    ? (current + 1) % links.length
                    : current <= 0
                      ? links.length - 1
                      : current - 1;
            links[next].focus();
        },
        { capture: true }
    );
}
