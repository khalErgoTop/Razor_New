(function ($) {
    $(function () {
        const $header = $('#WrapperHead');
        const $modules = $('#M_PnlModules');
        const $contentWrapper = $('.wrapper');
        if (!$header.length || !$modules.length || !$contentWrapper.length) return;

        let lastScrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const delta = 8; // minimaler Scroll-Abstand um Richtung zu erkennen
        let ticking = false;
        let lastToggle = 0;
        const minToggleInterval = 80; // ms, verhindert Flackern

        function applyFixedState(fixed) {
            const moduleHeight = $modules.outerHeight() || 0;
            if (fixed) {
                if (!$modules.hasClass('modules-fixed')) {
                    $modules.addClass('modules-fixed');
                    $contentWrapper.css('padding-top', moduleHeight + 'px');
                }
                if ($header.hasClass('active')) $header.removeClass('active');
            } else {
                if ($modules.hasClass('modules-fixed')) {
                    $modules.removeClass('modules-fixed');
                    $contentWrapper.css('padding-top', '');
                }
                if (!$header.hasClass('active')) $header.addClass('active');
            }
        }

        // Ensure wrapper has enough bottom padding to avoid footer overlap
        function updateFooterPadding() {
            const $footer = $('#Footer, #footer');
            if ($footer.length && $contentWrapper.length) {
                const footerH = $footer.outerHeight() || 0;
                // keep existing padding-bottom if larger
                const current = parseInt($contentWrapper.css('padding-bottom')) || 0;
                const desired = Math.max(current, footerH + 8);
                $contentWrapper.css('padding-bottom', desired + 'px');
            }
        }

        function update() {
            ticking = false;
            const now = Date.now();
            const headerHeight = $header.outerHeight() || 0;
            const moduleHeight = $modules.outerHeight() || 0;
            const mrect = $modules[0].getBoundingClientRect();
            const modulesFixed = $modules.hasClass('modules-fixed');
            const scrollY = window.pageYOffset || document.documentElement.scrollTop;
            const thresholdEnter = Math.max(1, Math.floor(moduleHeight / 2));
            const thresholdExit = thresholdEnter + 12; // Hysterese

            if (now - lastToggle < minToggleInterval) {
                lastScrollTop = scrollY;
                return;
            }

            const scrollingUp = scrollY < lastScrollTop - delta;
            const scrollingDown = scrollY > lastScrollTop + delta;
            const rect = $header[0].getBoundingClientRect();

            if (modulesFixed && scrollingUp) {
                if (rect.bottom > 0 || mrect.top > thresholdExit) {
                    applyFixedState(false);
                    lastToggle = now;
                    lastScrollTop = scrollY;
                    return;
                }
            }

            if (!modulesFixed && mrect.top <= thresholdEnter && scrollingDown) {
                applyFixedState(true);
                lastToggle = now;
                lastScrollTop = scrollY;
                return;
            }

            if (modulesFixed && mrect.top > thresholdExit) {
                applyFixedState(false);
                lastToggle = now;
                lastScrollTop = scrollY;
                return;
            }

            if (scrollY <= Math.max(0, headerHeight - 5)) {
                applyFixedState(false);
                lastToggle = now;
                lastScrollTop = scrollY;
                return;
            }

            lastScrollTop = scrollY;
        }

        function requestTick() {
            if (!ticking) {
                ticking = true;
                requestAnimationFrame(update);
            }
        }

        $(window).on('scroll resize', function () { requestTick(); updateFooterPadding(); });
        $(window).on('load', function () { setTimeout(function () { requestTick(); updateFooterPadding(); }, 50); });
        // initial
        requestTick();
        updateFooterPadding();
    });
})(jQuery);