using Microsoft.Extensions.Options;
using TOPqwWeb_Core.Infrastructure.Configuration;

namespace TOPqwWeb_Core.Infrastructure.Theming
{
    public class ThemeResolver
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ThemeSettings _settings;

        public ThemeResolver(IWebHostEnvironment environment, IOptions<ThemeSettings> settings)
        {
            _environment = environment;
            _settings = settings.Value;
        }

        public string GetThemeName()
        {
            if (string.IsNullOrWhiteSpace(_settings.Name) || _settings.Name is "." or ".." ||
                _settings.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                _settings.Name.Contains('/') || _settings.Name.Contains('\\'))
            {
                throw new InvalidOperationException("Die konfigurierte Theme-Bezeichnung ist ungültig.");
            }

            var themePath = Path.Combine(_environment.WebRootPath, "css", _settings.Name);
            if (!Directory.Exists(themePath))
            {
                throw new InvalidOperationException($"Das konfigurierte Theme '{_settings.Name}' wurde nicht gefunden.");
            }

            return _settings.Name;
        }
    }
}
