import { t } from './resources.js';

// Explicit DocFX references and generated API signatures share this DOM renderer.
const pendingReferences = new WeakMap();

export function collectCodeReferences(el) {
    const references = new Map();
    const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    let offset = 0;
    while (walker.nextNode()) {
        const node = walker.currentNode;
        const anchor = node.parentElement.closest('a[href]');
        if (anchor && el.contains(anchor)) {
            if (!references.has(anchor)) references.set(anchor, { start: offset, end: offset, link: anchor });
            references.get(anchor).end = offset + node.length;
        }
        offset += node.length;
    }
    return [...references.values()];
}

export function preserveCodeReferences({ el }) {
    if (el.dataset.highlighted || !el.matches('article pre > code')) return;
    const references = collectCodeReferences(el);
    if (!references.length) return;
    pendingReferences.set(el, references);
    // The highlighter receives plain code, never authored HTML or link markup.
    el.textContent = el.textContent;
}

export function restoreCodeReferences({ el }) {
    const references = pendingReferences.get(el);
    if (!references) return;
    pendingReferences.delete(el);
    for (const reference of references.reverse()) linkCodeRange(el, reference.start, reference.end, reference.link);
}

export function linkCodeRange(code, start, end, reference) {
    if (start >= end) return;
    const walker = document.createTreeWalker(code, NodeFilter.SHOW_TEXT);
    let offset = 0;
    let first;
    let last;
    while (walker.nextNode()) {
        const node = walker.currentNode;
        const next = offset + node.length;
        if (!first && start < next) first = { node, offset: start - offset };
        if (first && node.parentElement.closest('a')) return;
        if (first && end <= next) {
            last = { node, offset: end - offset };
            break;
        }
        offset = next;
    }
    if (!first || !last) return;
    const range = document.createRange();
    range.setStart(first.node, first.offset);
    range.setEnd(last.node, last.offset);
    const link = document.createElement('a');
    for (const attribute of ['href', 'target', 'rel', 'aria-label']) {
        if (reference.hasAttribute(attribute)) link.setAttribute(attribute, reference.getAttribute(attribute));
    }
    link.className = 'code-reference';
    link.title = reference.getAttribute('title') || t('ui.reference.open', { name: range.toString() });
    link.append(range.extractContents());
    range.insertNode(link);
    range.detach();
}
