(() => {
    const pageUrl = window.location.pathname;

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

    function initialiseLegacyInteractions() {
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

        const closeDialog = dialog => {
            dialog.hidden = true;
            document.body.classList.remove('legacy-dialog-open');
        };
        document.addEventListener('click', event => {
            const opener = event.target.closest('[data-dialog-target]');
            if (!opener) return;
            const target = opener.dataset.dialogTarget;
            const dialog = document.getElementById(target);
            if (!dialog) {
                console.warn(`Dialogziel nicht gefunden: ${target}`);
                return;
            }
                document.querySelectorAll('[role="dialog"]').forEach(closeDialog);
                dialog.hidden = false;
                document.body.classList.add('legacy-dialog-open');
                dialog.querySelector('input, select, textarea, button')?.focus();
        });
        document.querySelectorAll('.dialog-close').forEach(button => {
            button.addEventListener('click', () => closeDialog(button.closest('[role="dialog"]')));
        });
        document.addEventListener('keydown', event => {
            if (event.key !== 'Escape') return;
            document.querySelectorAll('[role="dialog"]:not([hidden])').forEach(closeDialog);
        });
    }

    document.querySelectorAll('form').forEach(initialise);
    initialiseLegacyInteractions();
})();