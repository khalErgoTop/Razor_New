/**
 * @file kern-kopfzeile.js
 * @author Darko Pervan, darko.pervan@dataport.de / KERN-Team
 * @author Tom Marienfeld, tom.marienfeld@dataport.de / KERN-Team
 * @date 17.06.2025
 * @modified 27.06.2025
 * @brief Web Component für die Kopfzeile.
 *
 * Die `kern-kopfzeile` Web Component implementiert eine anpassbare Kopfzeile
 * mit einem Flaggen-Icon und akzeptiert ihren Textinhalt direkt als Kind-Element.
 * Sie bietet flexible Layout-Optionen für den inneren Inhaltscontainer,
 * der entweder fluid sein oder feste Breiten basierend auf definierten Breakpoints annehmen kann.
 *
 * Standard-Breakpoints sind vordefiniert und können bei Bedarf durch
 * benutzerspezifische Breakpoints vollständig überschrieben werden.
 *
 * Die Komponente setzt intern `role="banner"` auf ihr Hauptelement im Shadow DOM,
 * um die Barrierefreiheit als primären Header der Seite zu kennzeichnen.
 *
 * @element kern-kopfzeile
 *
 * @slot - Der Textinhalt, der in der Kopfzeile angezeigt wird. Dies ist der unbenannte (default) Slot.
 * Wenn kein Text als Kind-Element übergeben wird, wird der Standardtext "Offizielle Website – Bundesrepublik Deutschland" angezeigt.
 * @attr {boolean} fluid - Wenn dieses Attribut vorhanden ist, nimmt der innere
 * Container die volle Breite (100%) ein. Breakpoints
 * werden in diesem Modus ignoriert.
 * @attr {string} breakpoints - Eine durch Kommas getrennte Liste von Breakpoint-Paaren
 * im Format "min-width:container-width" (z.B. "768px:720px, 1200px:1140px").
 * Diese Breakpoints überschreiben die Standard-Breakpoints
 * vollständig und werden in richtiger CSS-Reihenfolge generiert. Wenn das Attribut nicht gesetzt oder leer ist,
 * werden die Standard-Breakpoints verwendet. Dieses Attribut
 * wird im `fluid`-Modus ignoriert.
 *
 * @example
 * <kern-kopfzeile></kern-kopfzeile>
 * 
 * @example
 * <kern-kopfzeile>Meine Webseite</kern-kopfzeile>
 *
 * @example
 * <kern-kopfzeile fluid>Volle Breite Anzeige</kern-kopfzeile>
 *
 * @example
 * <kern-kopfzeile breakpoints="480px:400px, 900px:850px">Benutzerdefinierte Breakpoints</kern-kopfzeile>
 */


// Definieren Sie die Standard-Breakpoints hier
// Format: { 'min-width-value': 'container-max-width-value' }
// KERN Default Breakpoints
const DEFAULT_BREAKPOINTS = {
    '576px': '96%',
    '768px': '96%',
    '992px': '96%',
    '1200px': '96%',
    '1600px': '1536px'
};

class KernKopfzeile extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: 'open' });

        this._containerElement = null;
        this._breakpointStyleElement = null;

        this.shadowRoot.innerHTML = `
            <style>
            :host {
                --kern-color-layout-layer-surface-contextual: #F3F4F7;
                --kern-component-kopfzeile-padding: 5px;
                --kern-metric-space-none: 0px;
                --kern-metric-space-default: 16px;
                --kern-1: 1px;
                --kern-metric-space-small: 8px;
                --kern-metric-dimension-default: 24px;
                --kern-metric-dimension-x-small: 16px;
                --kern-color-layout-text-default-contextual: #131525;
                --kern-component-kopfzeile-font-family: "Fira Sans";
                --kern-component-kopfzeile-font-size: 16px;
                --kern-component-kopfzeile-line-height: 20px;
                --kern-18: 18px;
                --kern-12: 12px;
                --kern-14: 14px;
                --kern-16: 16px;
            }

            @media (prefers-color-scheme: dark) {
                :host(:not([theme="light"])) {
                    --kern-color-layout-text-default-contextual: #f3f4f7; /* PLACEHOLDER: Dark Text Color */
                    --kern-color-layout-layer-surface-contextual: #131525; /* PLACEHOLDER: Dark Surface Color */
                }
            }

            :host([theme="dark"]) {
                --kern-color-layout-text-default-contextual: #f3f4f7; /* PLACEHOLDER: Dark Text Color */
                --kern-color-layout-layer-surface-contextual: #131525; /* PLACEHOLDER: Dark Surface Color */
            }

            :host([theme="light"]) {
                --kern-color-layout-text-default-contextual: #131525;
                --kern-color-layout-layer-surface-contextual: #F3F4F7;
            }

            /* Gescopetes CSS für die Web Component */
            .kern-kopfzeile {
                background: var(--kern-color-layout-layer-surface-contextual, #F3F4F7);
                padding: var(--kern-component-kopfzeile-padding, 5px) var(--kern-metric-space-none, 0px);
            }

            /* Basis-Stil für beide Container-Varianten */
            .kern-container, .kern-container-fluid {
                width: 100%;
                margin-left: auto;
                margin-right: auto;
                padding-left: var(--kern-metric-space-default, 16px);
                padding-right: var(--kern-metric-space-default, 16px);
                box-sizing: border-box;
            }

            /* Fluid-Container-Stil */
            .kern-container-fluid {
                max-width: 100%; /* Volle Breite für den fluiden Zustand */
            }

            .kern-container {
                /* Eine Fallback-Breite, wenn keine Breakpoints aktiv sind und nicht fluid. */
            }

            .kern-kopfzeile__content {
                display: flex;
                padding: var(--kern-1, 1px) var(--kern-metric-space-none, 0px);
                align-items: center;
                gap: var(--kern-metric-space-small, 8px);
            }

            .kern-kopfzeile__flagge {
                display: flex;
                width: var(--kern-metric-dimension-default, 24px);
                height: var(--kern-metric-dimension-x-small, 16px);
                flex-shrink: 0;
            }

            .kern-kopfzeile__flagge svg {
                vertical-align: baseline;
                outline: var(--kern-1, 1px) solid #FFFFFF;
            }

            .kern-kopfzeile__label {
                color: var(--kern-color-layout-text-default-contextual, #131525);
                font-family: var(--kern-component-kopfzeile-font-family, "Fira Sans");
                font-style: normal;
                font-weight: 400;
                font-size: var(--kern-component-kopfzeile-font-size, 16px);
                line-height: var(--kern-component-kopfzeile-line-height, 20px);
            }

            @media only screen and (max-width: 768px) {
                .kern-kopfzeile__flagge {
                    width: var(--kern-18, 18px);
                    height: var(--kern-12, 12px);
                }
                .kern-kopfzeile__label {
                    font-size: var(--kern-14, 14px);
                    line-height: var(--kern-16, 16px);
                }
                .kern-kopfzeile .kern-container,
                .kern-kopfzeile .kern-container-fluid {
                    padding-left: var(--kern-metric-space-small, 8px);
                    padding-right: var(--kern-metric-space-small, 8px);
                }
            }

            #breakpoint-styles {
                /* Dieser Block wird durch JS befüllt */
            }
            </style>

            <div class="kern-kopfzeile" role="banner">
                <div class="kern-container">
                    <div class="kern-kopfzeile__content">
                        <span class="kern-kopfzeile__flagge" aria-hidden="true">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 16" style="max-width:24px">
                                <path fill="#000" d="M0 .5h24v5.333H0z" />
                                <path fill="red" d="M0 5.833h24v5.333H0z" />
                                <path fill="#FACA2C" d="M0 11.167h24V16.5H0z" />
                            </svg>
                        </span>
                        <span class="kern-kopfzeile__label">
                            <slot>Offizielle Website – Bundesrepublik Deutschland</slot>
                        </span>
                    </div>
                </div>
            </div>
            <style id="breakpoint-styles"></style>
        `;
    }

    get theme() {
        return this.getAttribute('theme');
    }

    set theme(val) {
        if (val) {
            this.setAttribute('theme', val);
        } else {
            this.removeAttribute('theme');
        }
    }

    static get observedAttributes() {
        return ['fluid', 'breakpoints', 'theme'];
    }

    attributeChangedCallback(name, oldValue, newValue) {
        if (!this._containerElement || !this._breakpointStyleElement) {
            this._cacheElements();
        }

        const isFluid = this.hasAttribute('fluid') && (this.getAttribute('fluid') === '' || this.getAttribute('fluid') === 'true');

        switch (name) {
            case 'fluid':
                if (this._containerElement) {
                    this._containerElement.className = isFluid ? 'kern-container-fluid' : 'kern-container';
                    if (isFluid) {
                        this._breakpointStyleElement.textContent = '';
                    } else {
                        this._applyBreakpoints();
                    }
                }
                break;
            case 'breakpoints':
                if (!isFluid) {
                    this._applyBreakpoints();
                } else {
                    this._breakpointStyleElement.textContent = '';
                }
                break;
            case 'theme':
                // Handled dynamically via CSS selectors
                break;
        }
    }

    connectedCallback() {
        this._cacheElements();
        this.attributeChangedCallback('fluid', null, this.getAttribute('fluid'));
        this.attributeChangedCallback('breakpoints', null, this.getAttribute('breakpoints'));
        this.attributeChangedCallback('theme', null, this.getAttribute('theme'));
    }

    /**
     * Speichert Referenzen auf wichtige DOM-Elemente im Shadow DOM,
     * um wiederholte `querySelector`-Aufrufe zu vermeiden und die Performance zu verbessern.
     */
    _cacheElements() {
        this._containerElement = this.shadowRoot.querySelector('.kern-kopfzeile > div:first-child');
        this._breakpointStyleElement = this.shadowRoot.getElementById('breakpoint-styles');
        // Das Element mit role="banner" cachen, um es ggf. zu manipulieren (falls nötig)
        this._bannerElement = this.shadowRoot.querySelector('.kern-kopfzeile[role="banner"]');
    }

    _applyBreakpoints() {
        const breakpointAttribute = this.getAttribute('breakpoints');
        const isFluid = this.hasAttribute('fluid') && (this.getAttribute('fluid') === '' || this.getAttribute('fluid') === 'true');

        if (isFluid) {
            this._breakpointStyleElement.textContent = '';
            return;
        }

        let breakpointsToApply;

        if (breakpointAttribute && breakpointAttribute.trim() !== '') {
            const userBreakpoints = {};
            breakpointAttribute.split(',').forEach(bpString => {
                const parts = bpString.trim().split(':');
                if (parts.length === 2) {
                    const bpWidth = parts[0].trim();
                    const containerWidth = parts[1].trim();
                    userBreakpoints[bpWidth] = containerWidth;
                }
            });
            breakpointsToApply = userBreakpoints;
        } else {
            breakpointsToApply = DEFAULT_BREAKPOINTS;
        }

        // Wenn nach der Auswahl immer noch keine Breakpoints zum Anwenden da sind (z.B.
        // wenn DEFAULT_BREAKPOINTS leer ist und keine benutzerdefinierten angegeben wurden),
        // wird der Style-Block geleert und die Funktion beendet.
        if (Object.keys(breakpointsToApply).length === 0) {
            this._breakpointStyleElement.textContent = '';
            return;
        }

        let styleString = '';
        const sortedBreakpointKeys = Object.keys(breakpointsToApply).sort((a, b) => {
            const numA = parseFloat(a);
            const numB = parseFloat(b);
            return numA - numB;
        });

        // Generiert für jeden Breakpoint eine `@media (min-width: ...)`-Regel.
        sortedBreakpointKeys.forEach(minWidth => {
            const containerWidth = breakpointsToApply[minWidth];
            styleString += `
            @media (min-width: ${minWidth}) {
                .kern-container {
                    max-width: ${containerWidth} !important;
                }
            }
            `;
        });

        this._breakpointStyleElement.textContent = styleString;
    }
}

customElements.define('kern-kopfzeile', KernKopfzeile);