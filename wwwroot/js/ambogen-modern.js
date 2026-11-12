(() => {
    const page = document.querySelector('[data-am-page]');
    if (!page) return;
    const form = page.querySelector('[data-am-form]');
    if (!form) return;
    let dirty = form.dataset.amUnsaved === 'true';
    form.addEventListener('input', () => { dirty = true; });
    form.addEventListener('change', () => { dirty = true; });
    form.addEventListener('submit', () => { dirty = false; });
    window.addEventListener('beforeunload', event => {
        if (!dirty) return;
        event.preventDefault();
        event.returnValue = '';
    });
})();
