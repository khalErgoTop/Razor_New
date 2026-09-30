using Microsoft.Extensions.Options;
using TOPqwWeb_Core.Infrastructure.Configuration;

namespace TOPqwWeb_Core.Services.Public
{
    public class SuchartenPageService
    {
        private readonly ThemeSettings _themeSettings;

        public SuchartenPageService(IOptions<ThemeSettings> themeSettings)
        {
            _themeSettings = themeSettings.Value;
        }

        public string GetDistrictLabel()
        {
            return string.IsNullOrWhiteSpace(_themeSettings.DistrictLabel)
                ? "Kreis"
                : _themeSettings.DistrictLabel;
        }
    }
}
