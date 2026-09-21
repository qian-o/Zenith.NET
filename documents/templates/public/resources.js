import './resource-format.js';

export const resourceState = JSON.parse(document.getElementById('resource-state').textContent);
let strings = resourceState.strings;
export const getStrings = () => strings;
const has = (dictionary, key) => Object.prototype.hasOwnProperty.call(dictionary, key);
const format = globalThis.zenithResourceFormat;
const slotTemplate = element => element.dataset.resourceSlots ? document.getElementById(element.dataset.resourceSlots) : null;

// Validate before changing the DOM: an old or incomplete download must not mix languages.
export function setStrings(value) {
    if (!value || Array.isArray(value) || typeof value !== 'object' || Object.values(value).some(text => typeof text !== 'string' || !text.trim())) {
        throw new Error('Invalid resource dictionary');
    }
    if (Object.keys(value).length !== Object.keys(resourceState.placeholders).length) {
        throw new Error('Resource key set differs');
    }
    const requireKey = (key, slots) => {
        if (!has(value, key)) throw new Error(`Missing text resource: ${key}`);
        if (slots && JSON.stringify(format.placeholders(value[key])) !== JSON.stringify([...slots].sort())) {
            throw new Error(`Resource placeholders differ: ${key}`);
        }
    };
    for (const [key, slots] of Object.entries(resourceState.placeholders)) requireKey(key, slots);
    for (const element of document.querySelectorAll('[data-resource]')) {
        const slots = slotTemplate(element)?.content.children || [];
        requireKey(element.dataset.resource, [...slots].map(slot => slot.dataset.slot));
    }
    for (const attribute of ['label', 'title', 'placeholder', 'alt']) {
        for (const element of document.querySelectorAll(`[data-resource-${attribute}]`)) requireKey(element.getAttribute(`data-resource-${attribute}`), []);
    }
    for (const meta of document.querySelectorAll('meta[name="resource:title"], meta[name="resource:description"]')) requireKey(meta.content, []);
    strings = value;
}

export function t(key, values = {}) {
    if (!has(strings, key)) throw new Error(`Missing text resource: ${key}`);
    return format.format(strings[key], values);
}

function bind(element) {
    const key = element.dataset.resource;
    if (!has(strings, key)) return;
    const template = slotTemplate(element);
    const slots = new Map();
    for (const slot of template?.content.children || []) {
        const content = document.createDocumentFragment();
        content.append(...[...slot.childNodes].map(child => child.cloneNode(true)));
        for (const child of content.querySelectorAll('[data-resource]')) {
            if (content.contains(child)) bind(child);
        }
        slots.set(slot.dataset.slot, content);
    }
    const content = document.createDocumentFragment();
    for (const part of format.parts(strings[key])) {
        if (part.text !== undefined) content.append(document.createTextNode(part.text));
        else {
            if (!slots.has(part.slot)) throw new Error(`Missing binding slot: ${key}.${part.slot}`);
            content.append(slots.get(part.slot).cloneNode(true));
        }
    }
    element.replaceChildren(content);
}

export function applyResources(code) {
    document.documentElement.lang = code;
    for (const element of document.querySelectorAll('[data-resource]')) {
        if (element.isConnected) bind(element);
    }
    for (const [attribute, target] of [['label', 'aria-label'], ['title', 'title'], ['placeholder', 'placeholder'], ['alt', 'alt']]) {
        for (const element of document.querySelectorAll(`[data-resource-${attribute}]`)) {
            const key = element.getAttribute(`data-resource-${attribute}`);
            if (has(strings, key)) element.setAttribute(target, t(key));
        }
    }
    const title = document.querySelector('meta[name="resource:title"]')?.content;
    if (title) document.title = `${t(title)} | Zenith.NET`;
    const description = document.querySelector('meta[name="resource:description"]')?.content;
    if (description && has(strings, description)) document.querySelector('meta[name="description"]').content = t(description);
    document.querySelector('meta[name="loc:copy"]').content = t('ui.action.copy');
    for (const copy of document.querySelectorAll('.code-action[title]')) copy.title = t('ui.action.copy');
}
