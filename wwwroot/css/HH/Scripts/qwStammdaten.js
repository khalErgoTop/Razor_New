// Version: 1.16 //

$(function () {
    $(document).on('scroll', function (ev) {
        let rem2PixelMultiplier = 15.87; // 0.063;
        let ltHeight = $('#M_cphLeittext_PnlLeittext').outerHeight() / rem2PixelMultiplier + 1;
        let facOpenerTop = 12 + ltHeight;
        let controlsTop = 18 + ltHeight;
        let facilitiesListTop = 15 + ltHeight;
        let $facListOpener = $('#FacilityListOpenener');

        //if (ev.currentTarget.scrollingElement.scrollTop > $('#WrapperHead').outerHeight()) {
        //    facOpenerTop = -2;
        //    controlsTop = 4;
        //    facilitiesListTop = 0;
        //}

        if (!$('#WrapperHead').hasClass('active')) {
            facOpenerTop = -3 + ltHeight ;
            controlsTop = 3 + ltHeight;
            facilitiesListTop = 0 + ltHeight;
        }

        $facListOpener.css('top', facOpenerTop + 'rem');
        $('#M_MC_FFView_FacCtls').css('top', controlsTop + 'rem');
        $('#Facilities').css('top', facilitiesListTop + 'rem');
               
    }); // END Doc Scroll
}); // END PageLoad