import { collectCodeReferences, linkCodeRange } from './code-links.js';

const escapeRegex = value => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const maskLiterals = text =>
    text.replace(/@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\/\/[^\n]*|\/\*[\s\S]*?\*\//g, match =>
        ' '.repeat(match.length)
    );

// Generated declarations link local reference pages only. Authored xrefs are explicit.
export function isLocalTypeReference(link) {
    const href = link.getAttribute('href');
    if (!href) return false;
    const target = new URL(href, document.baseURI);
    return target.origin === location.origin;
}

// Separate parameter declarations without splitting generic, tuple, or attribute syntax.
function parameterTypeRanges(text, open, parameters) {
    if (open < 0 || !parameters.length) return [];
    const masked = maskLiterals(text);
    const close = masked[open] === '[' ? ']' : ')';
    const stack = [];
    const ranges = [];
    let start = open + 1;
    let defaultStart = -1;
    let index = 0;
    for (let pos = start; pos < masked.length; pos++) {
        const char = masked[pos];
        if (!stack.length && (char === ',' || char === close)) {
            const parameter = parameters[index++];
            if (!parameter) break;
            const declaration = text.slice(start, defaultStart < 0 ? pos : defaultStart);
            const name = new RegExp(`(?<![\\w])@?${escapeRegex(parameter)}\\s*$`).exec(declaration);
            if (name) ranges.push({ start, end: start + name.index, index: index - 1 });
            if (char === close) break;
            start = pos + 1;
            defaultStart = -1;
        } else if (!stack.length && char === '=' && defaultStart < 0) {
            defaultStart = pos;
        } else if ('([{'.includes(char) || (char === '<' && defaultStart < 0)) {
            stack.push({ '(': ')', '[': ']', '{': '}', '<': '>' }[char]);
        } else if (stack.length && char === stack[stack.length - 1]) {
            stack.pop();
        }
    }
    return ranges;
}

// Link each type within its own declaration range, using DocFX's resolved references.
export function linkSignatureType({ el, text }) {
    if (!el.matches('[data-api-symbol]')) return;
    const references = el.parentElement.nextElementSibling;
    if (!references?.matches('.api-signature-references')) return;

    const symbol = el.dataset.apiSymbol.split('(')[0].split('[')[0];
    const masked = maskLiterals(text);
    const declaration = new RegExp(`(?:^|\\s)${escapeRegex(symbol)}(?=\\s*(?:[({\\[;=:]|$))`).exec(masked);
    // The value type precedes the member name; conversion operators put it before "(".
    const end =
        el.dataset.apiKind === 'operator'
            ? text.indexOf('(', declaration ? declaration.index + declaration[0].length : 0)
            : declaration
              ? declaration.index + declaration[0].length - symbol.length
              : -1;
    const valueReferences = references.querySelector('[data-signature-value]');
    if (end >= 0 && valueReferences) linkRange(el, 0, end, valueReferences);

    if (!declaration) return;
    const afterSymbol = declaration.index + declaration[0].length;
    const baseReferences = [...references.querySelectorAll('[data-signature-base]')];
    if (baseReferences.length) {
        const constraint = /\bwhere\b/.exec(masked.slice(afterSymbol));
        const limit = constraint ? afterSymbol + constraint.index : text.length;
        const colon = masked.indexOf(':', afterSymbol);
        if (colon >= 0 && colon < limit) {
            for (const reference of baseReferences) linkRange(el, colon + 1, limit, reference);
        }
    }
    const opening = /[([]/.exec(text.slice(afterSymbol));
    const parameters = [...references.querySelectorAll('[data-signature-parameter]')];
    const ranges = parameterTypeRanges(
        text,
        opening ? afterSymbol + opening.index : -1,
        parameters.map(parameter => parameter.dataset.signatureParameter)
    );
    for (const range of ranges) linkRange(el, range.start, range.end, parameters[range.index]);
}

function linkRange(el, start, end, references) {
    const links = collectCodeReferences(references).filter(reference => isLocalTypeReference(reference.link));
    if (!links.length) return;
    // Match the whole type expression. Equal spellings can refer to different symbols.
    // Nullable annotations may be absent from DocFX's type display, but remain in copied code.
    const tokenize = (text, offset = 0) =>
        [...text.matchAll(/@?[\p{ID_Start}_][\p{ID_Continue}]*|[^\s]/gu)]
            .filter(match => match[0] !== '?')
            .map(match => ({
                value: match[0].replace(/^@/, ''),
                start: offset + match.index,
                end: offset + match.index + match[0].length
            }));
    const expected = tokenize(references.textContent);
    const actual = tokenize(el.textContent.slice(start, end), start);
    if (!expected.length) return;
    let offset = -1;
    for (let index = actual.length - expected.length; index >= 0; index--) {
        if (expected.every((token, part) => token.value === actual[index + part].value)) {
            offset = index;
            break;
        }
    }
    if (offset < 0) return;

    let tupleDepth = 0;
    for (let index = 0; index < expected.length; index++) {
        const token = expected[index];
        if (token.value === '(') tupleDepth++;
        const previous = expected[index - 1]?.value;
        const next = expected[index + 1]?.value;
        // Tuple element names are labels, even when DocFX supplies a member xref for them.
        token.label =
            tupleDepth > 0 &&
            /^[\p{ID_Start}_]/u.test(token.value) &&
            [',', ')'].includes(next) &&
            previous &&
            !['(', ',', '<', '.'].includes(previous);
        if (token.value === ')') tupleDepth--;
    }
    for (const reference of links.reverse()) {
        const positions = expected
            .map((token, index) => ({ token, index }))
            .filter(({ token }) => !token.label && token.start >= reference.start && token.end <= reference.end);
        if (!positions.length) continue;
        const first = actual[offset + positions[0].index];
        const last = actual[offset + positions[positions.length - 1].index];
        linkCodeRange(el, first.start, last.end, reference.link);
    }
}
