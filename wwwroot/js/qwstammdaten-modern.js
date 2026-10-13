(() => {
    const pageUrl = window.location.pathname;
    const page = document.querySelector('[data-masterdata-page]');
    let activeDialog = null;
    let previousFocus = null;

    const getJson = async (handler, parameters) => {
        const query = new URLSearchParams({ handler, ...parameters });
        const response = await fetch(`${pageUrl}?${query}`, { headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error('Die Adressdaten konnten nicht geladen werden.');
        return response.json();
    };

    const updateOptions = (list, values) => {
        list.replaceChildren(...values.map(value => {
            const option = document.createElement('option');
            option.value = value;
            return option;
        }));
    };

    const closeDialog = () => {
        if (!activeDialog) return;
        activeDialog.hidden = true;
        activeDialog.closest('[data-dialog-layer]')?.setAttribute('hidden', 'hidden');
        document.body.classList.remove('md-dialog-open');
        previousFocus?.focus();
        activeDialog = null;
        previousFocus = null;
    };

    const openDialog = (dialog, opener) => {
        if (!dialog) return;
        closeDialog();
        const layer = dialog.closest('[data-dialog-layer]');
        previousFocus = opener;
        activeDialog = dialog;
        layer?.removeAttribute('hidden');
        dialog.hidden = false;
        document.body.classList.add('md-dialog-open');
        dialog.querySelector('input:not([type="hidden"]), select, textarea, button')?.focus();
    };

    const initialiseAddressLookup = form => {
        const street = form.querySelector('.street-directory-street');
        const number = form.querySelector('.street-directory-number');
        const validated = form.querySelector('.street-directory-validated');
        if (!street || !number || !validated) return;

        const streets = document.createElement('datalist');
        streets.id = `streets-${Math.random().toString(36).slice(2)}`;
        const numbers = document.createElement('datalist');
        numbers.id = `numbers-${Math.random().toString(36).slice(2)}`;
        document.body.append(streets, numbers);
        street.setAttribute('list', streets.id);
        number.setAttribute('list', numbers.id);

        street.addEventListener('input', async () => {
            validated.value = 'false';
            if (street.value.length < 3) return;
            try { updateOptions(streets, await getJson('StreetNameSuggestions', { prefix: street.value })); } catch { }
        });
        number.addEventListener('input', async () => {
            validated.value = 'false';
            if (street.value.length < 3) return;
            try { updateOptions(numbers, await getJson('HouseNumberSuggestions', { streetName: street.value, prefix: number.value })); } catch { }
        });
        number.addEventListener('change', async () => {
            if (!street.value || !number.value) return;
            try {
                const details = await getJson('StreetDetails', { streetName: street.value, houseNumber: number.value });
                form.querySelector('[name$="HouseNumberAddition"]')?.setAttribute('value', details.houseNumberAddition ?? '');
                const postalCode = form.querySelector('[name$="PostalCode"]');
                const city = form.querySelector('[name$="City"]');
                if (postalCode) postalCode.value = details.postalCode ?? '';
                if (city) city.value = details.city ?? '';
                validated.value = 'true';
            } catch { validated.value = 'false'; }
        });
    };

    page?.querySelectorAll('form').forEach(initialiseAddressLookup);
    document.addEventListener('click', event => {
        const opener = event.target.closest('[data-dialog-target]');
        if (opener) {
            const dialog = document.getElementById(opener.dataset.dialogTarget);
            if (dialog) {
                event.preventDefault();
                openDialog(dialog, opener);
            }
            return;
        }
        if (event.target.closest('[data-dialog-close]')) {
            event.preventDefault();
            closeDialog();
            return;
        }
        if (activeDialog && event.target === activeDialog.closest('[data-dialog-layer]')) closeDialog();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && activeDialog) closeDialog();
    });
})();
