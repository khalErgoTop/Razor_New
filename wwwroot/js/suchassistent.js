(() => {
    const button = document.getElementById("GeolocationSearchButton");
    const form = document.getElementById("GeolocationForm");
    const latitude = document.getElementById("Latitude");
    const longitude = document.getElementById("Longitude");
    const message = document.getElementById("GeolocationMessage");

    if (!button || !form || !latitude || !longitude || !message) {
        return;
    }

    const setMessage = (text) => {
        message.textContent = text;
    };

    button.addEventListener("click", () => {
        if (!navigator.geolocation) {
            setMessage("Die Standortermittlung wird von diesem Browser nicht unterstützt.");
            return;
        }

        button.disabled = true;
        setMessage("Standort wird ermittelt …");

        navigator.geolocation.getCurrentPosition(
            (position) => {
                latitude.value = position.coords.latitude;
                longitude.value = position.coords.longitude;
                setMessage("Standort wurde ermittelt. Suche wird gestartet …");
                form.submit();
            },
            (error) => {
                button.disabled = false;
                const text = error.code === error.PERMISSION_DENIED
                    ? "Der Zugriff auf den Standort wurde verweigert."
                    : "Der Standort konnte nicht ermittelt werden. Bitte versuchen Sie es erneut.";
                setMessage(text);
            }
        );
    });
})();