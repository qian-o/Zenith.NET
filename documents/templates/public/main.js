export default {
    iconLinks: [
        {
            icon: 'github',
            href: 'https://github.com/qian-o/Zenith.NET',
            title: 'GitHub'
        }
    ],
    start: () => {
        // Hide inherited type info sections (inheritance, implements, inheritedMembers, derivedClasses)
        for (const dl of document.querySelectorAll('dl.typelist.inheritance, dl.typelist.implements, dl.typelist.inheritedMembers, dl.typelist.derivedClasses')) {
            dl.style.display = 'none';
        }

        // Hide protected and override members from API pages
        for (const code of document.querySelectorAll('.codewrapper pre code')) {
            const text = code.textContent.trim();
            if (!/^protected\s/.test(text) && !/\boverride\b/.test(text)) continue;

            const wrapper = code.closest('.codewrapper');
            if (!wrapper) continue;

            const toHide = [];

            // Walk backward from codewrapper to collect h3 header and anchor
            let el = wrapper;
            while (el) {
                toHide.push(el);
                if (el.tagName === 'H3') {
                    const prev = el.previousElementSibling;
                    if (prev && prev.tagName === 'A' && prev.dataset.uid) {
                        toHide.push(prev);
                    }
                    break;
                }
                el = el.previousElementSibling;
            }

            // Walk forward from codewrapper to collect parameters, returns, etc.
            let next = wrapper.nextElementSibling;
            while (next && next.tagName !== 'H3' && next.tagName !== 'H2' &&
                !(next.tagName === 'A' && next.dataset.uid)) {
                toHide.push(next);
                next = next.nextElementSibling;
            }

            toHide.forEach(e => e.style.display = 'none');
        }

        // Hide empty section headers (e.g. "Constructors" when all constructors are hidden)
        for (const h2 of document.querySelectorAll('article h2.section')) {
            let next = h2.nextElementSibling;
            let hasVisibleMember = false;
            while (next && next.tagName !== 'H2') {
                if (next.tagName === 'H3' && next.style.display !== 'none') {
                    hasVisibleMember = true;
                    break;
                }
                next = next.nextElementSibling;
            }
            if (!hasVisibleMember) {
                h2.style.display = 'none';
            }
        }
    }
}