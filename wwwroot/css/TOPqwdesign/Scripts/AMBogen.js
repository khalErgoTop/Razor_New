function updatePositionOnScroll() {
  const scrollTop = $(window).scrollTop();
  const isFixed = scrollTop > 160;

  const elements = [
    {
      id: '#M_MC_Standorte', // gleich wie #Facilities
      top: '4rem',
      leftFixed: '0rem',
      leftAbsolute: '-18rem'
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
