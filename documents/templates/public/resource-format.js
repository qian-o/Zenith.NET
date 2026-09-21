// Shared by DocFX's template host and the browser. Values are text, never HTML.
(function (api) {
    api.parts = function (value) {
        const parts = [];
        let offset = 0;
        for (const match of value.matchAll(/\{([a-zA-Z][a-zA-Z0-9]*)\}/g)) {
            if (match.index > offset) parts.push({ text: value.slice(offset, match.index) });
            parts.push({ slot: match[1] });
            offset = match.index + match[0].length;
        }
        if (offset < value.length) parts.push({ text: value.slice(offset) });
        return parts;
    };
    api.placeholders = value =>
        [
            ...new Set(
                api
                    .parts(value)
                    .filter(part => part.slot)
                    .map(part => part.slot)
            )
        ].sort();
    api.format = (value, slots) =>
        api
            .parts(value)
            .map(part => {
                if (part.text !== undefined) return part.text;
                if (!Object.prototype.hasOwnProperty.call(slots, part.slot))
                    throw new Error(`Missing resource placeholder: ${part.slot}`);
                return slots[part.slot];
            })
            .join('');
})(typeof exports === 'object' ? exports : (globalThis.zenithResourceFormat = {}));
