const format = require('./public/resource-format.js');
const keyPattern = /^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$/;
const escape = value => String(value).replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));
const json = value => JSON.stringify(value).replace(/</g, '\\u003c');
const setting = (model, key) => model[key] ?? model._metadata?.[key];

exports.dictionary = function (model) {
    const result = {};
    for (const key of Object.keys(model).filter(key => !key.startsWith('_'))) {
        if (!keyPattern.test(key) || typeof model[key] !== 'string' || !model[key].trim()) {
            throw new Error(`Invalid text resource: ${key} (${model._key})`);
        }
        result[key] = model[key];
    }
    return result;
};

exports.source = function (model) {
    const code = setting(model, '_sourceLanguage');
    const source = model.__global._shared[`~/locales/${code}/strings.yml`];
    if (!source) throw new Error(`Missing source dictionary: locales/${code}/strings.yml`);
    return exports.dictionary(source);
};

exports.get = function (dictionary, key) {
    if (!Object.prototype.hasOwnProperty.call(dictionary, key)) throw new Error(`Missing text resource: ${key}`);
    return dictionary[key];
};

exports.validate = function (model) {
    const code = model._key.split('/').at(-2);
    const languages = setting(model, '_languages');
    if (!languages.some(language => language.code === code)) throw new Error(`Unregistered dictionary language: ${code}`);
    const source = exports.source(model);
    const dictionary = exports.dictionary(model);
    for (const key of new Set([...Object.keys(source), ...Object.keys(dictionary)])) {
        const expected = exports.get(source, key);
        const value = exports.get(dictionary, key);
        if (JSON.stringify(format.placeholders(expected)) !== JSON.stringify(format.placeholders(value))) {
            throw new Error(`Resource placeholders differ: ${key} (${model._key})`);
        }
    }
    return dictionary;
};

exports.prepare = function (model) {
    const source = exports.source(model);
    const interfaceEntries = Object.entries(source).filter(([key]) => key.startsWith('ui.') || key.endsWith('.title'));
    model.r = Object.create(null);
    for (const [key, value] of interfaceEntries) {
        const path = key.split('.');
        let scope = model.r;
        for (const segment of path.slice(0, -1)) scope = scope[segment] ||= Object.create(null);
        scope[path.at(-1)] = value;
    }
    for (const field of ['title', 'description']) {
        if (model[field]?.startsWith('@')) {
            model[`_resource${field[0].toUpperCase() + field.slice(1)}`] = model[field].slice(1);
            model[field] = exports.get(source, model[field].slice(1));
        }
    }
    model._languages = model._languages.map(language => ({
        ...language,
        available: Boolean(model.__global._shared[`~/locales/${language.code}/strings.yml`])
    }));
    model._resourceState = json({
        sourceLanguage: model._sourceLanguage,
        languages: model._languages,
        placeholders: Object.fromEntries(Object.entries(source).map(([key, value]) => [key, format.placeholders(value)])),
        strings: Object.fromEntries(interfaceEntries)
    });
    return source;
};

// Only the two authored binding tags are parsed. All other HTML is opaque shared markup.
exports.bind = function (html, dictionary, templates) {
    const root = { children: [] };
    const stack = [root];
    let offset = 0;
    for (const match of html.matchAll(/<\/?(resource|slot)\b[^>]*>/g)) {
        stack.at(-1).children.push(html.slice(offset, match.index));
        if (match[0].startsWith('</')) {
            if (stack.length === 1 || stack.at(-1).type !== match[1]) throw new Error('Unbalanced resource binding tags');
            stack.pop();
        } else {
            const name = /(?:key|name)="([^"]+)"/.exec(match[0])?.[1];
            if (!name) throw new Error(`Missing binding name: ${match[0]}`);
            const node = { type: match[1], name, children: [] };
            stack.at(-1).children.push(node);
            stack.push(node);
        }
        offset = match.index + match[0].length;
    }
    if (stack.length !== 1) throw new Error('Unclosed resource binding tag');
    root.children.push(html.slice(offset));
    const renderChildren = node => node.children.map(render).join('');
    const render = node => {
        if (typeof node === 'string') return node;
        if (!node.type || node.type === 'slot') return renderChildren(node);
        const slots = Object.create(null);
        for (const child of node.children) {
            if (typeof child === 'string' && !child.trim()) continue;
            if (child.type !== 'slot' || child.name in slots) throw new Error(`Invalid slot in ${node.name}`);
            slots[child.name] = renderChildren(child);
        }
        const value = exports.get(dictionary, node.name);
        if (JSON.stringify(Object.keys(slots).sort()) !== JSON.stringify(format.placeholders(value))) {
            throw new Error(`Binding slots differ: ${node.name}`);
        }
        const content = format.parts(value).map(part => part.text !== undefined ? escape(part.text) : slots[part.slot]).join('');
        let reference = '';
        if (Object.keys(slots).length) {
            const id = `resource-slots-${templates.length}`;
            templates.push(`<template id="${id}">${Object.entries(slots).map(([name, content]) => `<span data-slot="${name}">${content}</span>`).join('')}</template>`);
            reference = ` data-resource-slots="${id}"`;
        }
        return `<doc-text data-resource="${node.name}"${reference}>${content}</doc-text>`;
    };
    return renderChildren(root).replace(/(aria-label|title|placeholder|alt)="@([^"]+)"/g, (_, attribute, key) => `${attribute}="${escape(exports.get(dictionary, key))}"`);
};
