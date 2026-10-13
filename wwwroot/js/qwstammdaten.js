(() => {
    const pageUrl = window.location.pathname;
    let activeDialog = null;
    let lastFocusedElement = null;

    function addOptions(list, values) {
        list.replaceChildren(...values.map(value => {
            const option = document.createElement('option');
            option.value = value;
            return option;
        }));
    }

    async function getJson(handler, parameters) {
        const query = new URLSearchParams({ handler, ...parameters });
        const response = await fetch(`${pageUrl}?${query}`, { headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error('Straßenverzeichnis konnte nicht geladen werden.');
        return response.json();
    }

    function initialise(form) {
        const street = form.querySelector('.street-directory-street');
        const number = form.querySelector('.street-directory-number');
        const validated = form.querySelector('.street-directory-validated');
        if (!street || !number || !validated) return;

        const streetList = document.getElementById('street-directory-streets') ?? document.body.appendChild(Object.assign(document.createElement('datalist'), { id: 'street-directory-streets' }));
        const numberList = document.getElementById('street-directory-numbers') ?? document.body.appendChild(Object.assign(document.createElement('datalist'), { id: 'street-directory-numbers' }));

        street.addEventListener('input', async () => {
            validated.value = 'false';
            if (street.value.length < 3) return;
            try { addOptions(streetList, await getJson('StreetNameSuggestions', { prefix: street.value })); } catch { }
        });
        document.querySelectorAll('#M_MC_FFView_SecLocs_UlSecLocs li').forEach(location => {
            location.addEventListener('click', event => {
                if (event.target.closest('a, button, input')) return;
                document.querySelectorAll('#M_MC_FFView_SecLocs_UlSecLocs li[selected="selected"]').forEach(item => item.removeAttribute('selected'));
                location.setAttribute('selected', 'selected');
            });
        });
        number.addEventListener('input', async () => {
            validated.value = 'false';
            if (street.value.length <= 2) return;
            try { addOptions(numberList, await getJson('HouseNumberSuggestions', { streetName: street.value, prefix: number.value })); } catch { }
        });
        number.addEventListener('change', async () => {
            if (!street.value || !number.value) return;
            try {
                const details = await getJson('StreetDetails', { streetName: street.value, houseNumber: number.value });
                const addition = form.querySelector('[name$="HouseNumberAddition"]');
                const postalCode = form.querySelector('[name$="PostalCode"]');
                const city = form.querySelector('[name$="City"]');
                if (addition) addition.value = details.houseNumberAddition ?? '';
                if (postalCode) postalCode.value = details.postalCode ?? '';
                if (city) city.value = details.city ?? '';
                validated.value = 'true';
            } catch { validated.value = 'false'; }
        });
    }

    function closeDialog(dialog) {
        if (!dialog) return;
        dialog.hidden = true;
        dialog.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('legacy-dialog-open');
        if (lastFocusedElement instanceof HTMLElement) lastFocusedElement.focus();
        activeDialog = null;
    }

    function openDialog(dialog, opener) {
        if (!dialog) return;
        document.querySelectorAll('[role="dialog"]:not([hidden])').forEach(openDialogElement => closeDialog(openDialogElement));
        lastFocusedElement = opener;
        activeDialog = dialog;
        dialog.hidden = false;
        dialog.setAttribute('aria-hidden', 'false');
        document.body.classList.add('legacy-dialog-open');
        dialog.querySelector('input, select, textarea, button')?.focus();
    }

    function initialiseLegacyInteractions() {
        const facilityListOpener = document.getElementById('FacilityListOpenener');
        const facilities = document.getElementById('Facilities');
        if (facilityListOpener && facilities) {
            facilityListOpener.addEventListener('click', () => {
                const expanded = facilityListOpener.getAttribute('aria-expanded') === 'true';
                facilityListOpener.setAttribute('aria-expanded', String(!expanded));
                facilities.hidden = expanded;
            });
        }

        document.querySelectorAll('a[href^="tel:"]').forEach(link => {
            link.classList.add('mobile-phone-link');
        });

        document.querySelectorAll('.pnlAgreementHead, .pnlInstitutionHeadLine').forEach(header => {
            header.addEventListener('click', () => {
                const panel = header.nextElementSibling;
                if (!panel) return;
                panel.hidden = !panel.hidden;
            });
        });

        document.querySelectorAll('.pnlInstitutionFacility').forEach(panel => {
            panel.hidden = true;
        });

        document.addEventListener('click', event => {
            const opener = event.target.closest('[data-dialog-target]');
            if (!opener) return;
            const target = opener.dataset.dialogTarget;
            const dialog = document.getElementById(target);
            if (!dialog) {
                console.warn(`Dialogziel nicht gefunden: ${target}`);
                return;
            }
            event.preventDefault();
            openDialog(dialog, opener);
        });
        document.querySelectorAll('.dialog-close').forEach(button => {
            button.addEventListener('click', () => closeDialog(button.closest('[role="dialog"]')));
        });
        document.addEventListener('keydown', event => {
            if (event.key !== 'Escape') return;
            document.querySelectorAll('[role="dialog"]:not([hidden])').forEach(closeDialog);
        });
        document.addEventListener('click', event => {
            if (!activeDialog || event.target === activeDialog || activeDialog.contains(event.target)) return;
            if (!event.target.closest('[data-dialog-target]')) closeDialog(activeDialog);
        });
    }

    document.querySelectorAll('form').forEach(initialise);
    initialiseLegacyInteractions();
})();