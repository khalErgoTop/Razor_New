$(function () {
    $(document).on('scroll', function (ev) {
        let rem2PixelMultiplier = 15.87; // 0.063;
        let ltHeight = $('#M_cphLeittext_PnlLeittext').outerHeight() / rem2PixelMultiplier + 2;
        let facOpenerTop = 14 + ltHeight;
        let controlsTop = 14 + ltHeight;
        let facilitiesListTop = 14 + ltHeight;
        let $facListOpener = $('#M_MC_Standorte');

        //if (ev.currentTarget.scrollingElement.scrollTop > 100 && ev.currentTarget.scrollingElement.scrollTop <= 190) {
        //    facOpenerTop = 0;
        //    controlsTop = 0;
        //    facilitiesListTop = 10;
        //}

        //if (ev.currentTarget.scrollingElement.scrollTop > 190) {
        //    facOpenerTop = 0;
        //    controlsTop = 0;
        //    facilitiesListTop = 0;
        //}

        if (!$('#WrapperHead').hasClass('active')) {
            facOpenerTop = 0 + ltHeight;
            controlsTop = 0 + ltHeight;
            facilitiesListTop = 0 + ltHeight;
        }

        $facListOpener.css('top', facOpenerTop + 'rem');
        $('#M_MC_Standorte').css('top', facilitiesListTop + 'rem');
       
    }); // END Doc Scroll

    $('.jsgrid-header-row').append($('<div class="search opener">Suchen</div>'));
    $('.search.opener').on('click', function () {
        $('.jsgrid-filter-row').toggle();
    });

    
}); // END PageLoad