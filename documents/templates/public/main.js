import { initializeSite } from './site.js';
import { initializeSearch } from './search.js';
import { initializeHomeScene } from './home.js';
import { initializeMotion } from './motion.js';
import { initializeApi } from './api.js';
import { initializeMemberFinder } from './member-finder.js';
import { initializeOutline } from './outline.js';
import { configureSyntax } from './syntax.js';
import { linkSignatureType } from './signature-links.js';
import { preserveCodeReferences, restoreCodeReferences } from './code-links.js';

// Retain DocFX's generated navigation, reference pages, and code tools.
// The master template sets the fixed dark appearance before the first paint.
export default {
    defaultTheme: 'dark',
    configureHljs: hljs => {
        configureSyntax(hljs);
        hljs.addPlugin({
            'before:highlightElement': preserveCodeReferences,
            'after:highlightElement': event => {
                restoreCodeReferences(event);
                linkSignatureType(event);
            }
        });
    },
    mermaid: {
        theme: 'base',
        themeVariables: {
            darkMode: true,
            background: '#181b21',
            primaryColor: '#262a32',
            primaryTextColor: '#f2f3f5',
            primaryBorderColor: '#626a78',
            secondaryColor: '#181b21',
            secondaryTextColor: '#f2f3f5',
            secondaryBorderColor: '#626a78',
            tertiaryColor: '#343943',
            tertiaryTextColor: '#f2f3f5',
            tertiaryBorderColor: '#626a78',
            lineColor: '#a4abb7',
            textColor: '#f2f3f5',
            edgeLabelBackground: '#181b21',
            fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif'
        }
    },
    start: () => {
        initializeSite();
        initializeOutline();
        initializeSearch();
        initializeHomeScene();
        initializeApi();
        initializeMemberFinder();
        initializeMotion();
    }
};
