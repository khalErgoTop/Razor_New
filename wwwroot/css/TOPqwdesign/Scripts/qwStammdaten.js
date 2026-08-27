function updatePositionOnScroll() {
  const scrollTop = $(window).scrollTop();
  const isFixed = scrollTop > 160;

  const elements = [
    {
      id: '#M_MC_FFView_FacCtls',
      top: '5rem',
      leftFixed: '0',
      leftAbsolute: '-5rem'
    },
    {
      id: '#FacilityListOpenener',
      top: '2rem',
      leftFixed: '0',
      leftAbsolute: '-5rem'
    },
    {
      id: '#Facilities',
      top: '2rem',
      leftFixed: '5rem',
      leftAbsolute: '0rem'
    },
    {
      id: '#M_MC_Standorte', // gleich wie #Facilities
      top: '2rem',
      leftFixed: '5rem',
      leftAbsolute: '0rem'
    }
  ];

  elements.forEach(({ id, top, leftFixed, leftAbsolute }) => {
    $(id).css({
      position: isFixed ? 'fixed' : 'absolute',
      top,
      left: isFixed ? leftFixed : leftAbsolute
    });
  });
}

// Scroll- und Resize-Events
$(window).on('scroll resize', updatePositionOnScroll);

// Beim Laden initial aufrufen
$(document).ready(updatePositionOnScroll);
