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

    document.querySelectorAll('form').forEach(initialise);
})();