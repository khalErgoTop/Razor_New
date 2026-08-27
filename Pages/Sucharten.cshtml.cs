using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Public;

namespace TOPqwWeb_Core.Pages
{
    public class SuchartenModel : PageModel
    {
        private readonly SuchartenPageService _suchartenPageService;

        public SuchartenModel(SuchartenPageService suchartenPageService)
        {
            _suchartenPageService = suchartenPageService;
        }

        public string DistrictLabel { get; private set; } = "Kreis";

        public void OnGet()
        {
            DistrictLabel = _suchartenPageService.GetDistrictLabel();
        }
    }
}
