(() => {
    const form = document.querySelector('[data-qm-form]');
    const sendForm = document.querySelector('[data-qm-send-form]');
    const status = document.querySelector('[data-qm-dirty-status]');
    if (!form) return;

    let dirty = false;
    const markDirty = () => {
        dirty = true;
        if (status) status.textContent = 'Sie haben ungespeicherte Änderungen.';
    };
    form.addEventListener('input', markDirty);
    form.addEventListener('change', markDirty);
    form.addEventListener('submit', () => { dirty = false; });
    window.addEventListener('beforeunload', event => {
        if (!dirty) return;
        event.preventDefault();
        event.returnValue = '';
    });
    sendForm?.addEventListener('submit', event => {
        if (dirty) {
            event.preventDefault();
            if (status) status.textContent = 'Bitte speichern Sie Ihre Änderungen vor dem Versand.';
            form.querySelector('button[type="submit"]')?.focus();
        }
    });
})();
