// Reveal content once as it enters the viewport; navigation itself stays immediate.
export function initializeMotion() {
    const preference = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (preference.matches) return;

    const selector = document.querySelector('.home-page')
        ? '.render-copy > *, .geometry-stage, .paths-intro, .path-grid'
        : document.querySelector('.learn-hub')
            ? '.learn-hub > h1, .page-intro, .learning-entries, .hub-section-title, .section-description, .concept-list > a'
            : document.querySelector('.api-reference')
                ? '.api-heading, .api-declaration, .api-start-grid, .api-group-heading'
                : 'article > h1, article > p, article > section';
    const observer = new IntersectionObserver(entries => {
        let order = 0;
        for (const { target, isIntersecting } of entries) {
            if (!isIntersecting) continue;
            observer.unobserve(target);
            target.style.setProperty('--entrance-delay', `${Math.min(order++, 3) * 55}ms`);
            target.classList.add('motion-enter');
            target.addEventListener('animationend', event => {
                if (event.target === target && event.animationName === 'zenith-reveal') {
                    target.classList.remove('motion-enter');
                    target.style.removeProperty('--entrance-delay');
                }
            });
        }
    }, { threshold: 0 });
    document.querySelectorAll(selector).forEach(element => observer.observe(element));

    const finish = () => {
        observer.disconnect();
        for (const element of document.querySelectorAll('.motion-enter')) {
            element.classList.remove('motion-enter');
            element.style.removeProperty('--entrance-delay');
        }
    };
    // Cached pages return in their completed state, without replaying an entrance.
    window.addEventListener('pagehide', finish);
    preference.addEventListener('change', event => {
        if (event.matches) finish();
    });
}
