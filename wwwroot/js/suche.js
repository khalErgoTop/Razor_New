(() => {
    const form = document.querySelector('.searchform');
    if (!form) return;

    const plz = document.getElementById('Postleitzahl');
    const entfernung = document.getElementById('Entfernung');
    const einrichtungstyp = document.getElementById('Einrichtungstyp');
    const mitFreien = document.getElementById('MitFreienPlaetzen');
    const einrichtungen = document.getElementById('Einrichtungen');
    const leistungserbringer = document.getElementById('Leistungserbringer');
    const submit = document.getElementById('search-submit');

    function updateSearchButton() {
        if (submit && einrichtungen && leistungserbringer) {
            submit.disabled = !einrichtungen.checked && !leistungserbringer.checked;
        }
    }

    // Optional: PLZ nur Zahlen zulassen - wie in deinem alten Script
    if (plz) {
        plz.addEventListener('keydown', (e) => {
            if (e.key.length === 1 && !/[0-9]/.test(e.key) && !e.ctrlKey) {
                if (!['Backspace','Delete','Tab','Enter','ArrowLeft','ArrowRight'].includes(e.key)) {
                    e.preventDefault();
                }
            }
        });
    }

    [einrichtungen, leistungserbringer].forEach(input => {
        if (input) input.addEventListener('change', updateSearchButton);
    });
    updateSearchButton();


    // Reset button is now just a link to /Suche, no JS needed
})();