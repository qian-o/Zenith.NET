// Extend DocFX's bundled highlighter; coloring never creates navigation links.
// Modes use highlight.js's public grammar API:
// https://highlightjs.readthedocs.io/en/latest/mode-reference.html
const configured = new WeakSet();

function expressionModes(keywords) {
    const reserved = new Set(Object.values(keywords).flatMap(words =>
        (Array.isArray(words) ? words : words.split(/\s+/)).map(word => word.split('|')[0])));
    return [
        {
            scope: 'title.function',
            match: /(?<![\w@])@?[A-Za-z_]\w*(?=\s*(?:<[^;{}()=]+>)?\s*\()/,
            relevance: 0,
            'on:begin': (match, response) => {
                if (reserved.has(match[0])) response.ignoreMatch();
            }
        },
        { scope: 'property', match: /(?<=\.)@?[A-Za-z_]\w*/, relevance: 0 },
        { scope: 'attr', match: /\b[A-Za-z_]\w*(?=\s*:(?!:))/, relevance: 0 },
        // Conventional type identifiers get lexical color, not semantic resolution.
        { scope: 'type', match: /\b[A-Z][A-Za-z0-9_]*\b/, relevance: 0 },
        { scope: 'operator', match: /=>|\?\?=?|\?\.|[=!<>+*/%&|^~?:-]+/, relevance: 0 }
    ];
}

export function configureSyntax(hljs) {
    const csharp = hljs.getLanguage('csharp');
    if (!configured.has(csharp)) {
        configured.add(csharp);
        const expressions = expressionModes(csharp.keywords);
        const visited = new Set();
        const extend = mode => {
            if (!mode || typeof mode !== 'object' || visited.has(mode)) return;
            visited.add(mode);
            for (const child of [...(mode.contains || []), ...(mode.variants || [])]) extend(child);
            // Code contexts include declarations, parameter lists and interpolations.
            // String/comment/attribute modes retain their own lexical boundaries.
            if (mode.keywords === csharp.keywords) mode.contains = [...(mode.contains || []), ...expressions];
        };
        extend(csharp);
    }
    hljs.registerLanguage('slang', slang);
    hljs.registerAliases(['console'], { languageName: 'shell' });
}

function slang(hljs) {
    const keywords = {
        keyword: 'struct class interface extension enum namespace using import public private internal static const uniform groupshared in out inout ref let var typedef typealias generic where __init __subscript if else for while do break continue return discard switch case default true false nullptr this sizeof reinterpret_cast no_diff differentiable',
        type: 'void bool int uint half float double int8_t uint8_t int16_t uint16_t int32_t uint32_t int64_t uint64_t'
    };
    return {
        name: 'Slang',
        disableAutodetect: true,
        keywords,
        contains: [
            hljs.C_LINE_COMMENT_MODE,
            hljs.C_BLOCK_COMMENT_MODE,
            hljs.QUOTE_STRING_MODE,
            hljs.APOS_STRING_MODE,
            { scope: 'meta', begin: /^\s*#/, end: /$/, contains: [hljs.QUOTE_STRING_MODE] },
            hljs.C_NUMBER_MODE,
            { scope: 'meta', match: /(?<=\[)(?:shader|numthreads|unroll|loop|branch|flatten|earlydepthstencil|outputtopology|mesh|domain|partitioning|patchconstantfunc|outputcontrolpoints)\b/ },
            { scope: 'type', match: /\b(?:bool|int|uint|half|float|double)[1-4](?:x[1-4])?\b/ },
            { scope: 'meta', match: /\b(?:SV_\w+|(?:POSITION|NORMAL|TEXCOORD|COLOR|TANGENT|BINORMAL)\d*)\b/ },
            ...expressionModes(keywords)
        ]
    };
}
