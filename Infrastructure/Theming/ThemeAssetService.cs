namespace TOPqwWeb_Core.Infrastructure.Theming
{
    public class ThemeAssetService
    {
        private readonly ThemeResolver _themeResolver;
        private readonly IWebHostEnvironment _environment;

        public ThemeAssetService(ThemeResolver themeResolver, IWebHostEnvironment environment)
        {
            _themeResolver = themeResolver;
            _environment = environment;
        }

        public string ThemeName => _themeResolver.GetThemeName();

        public bool UsesKern => string.Equals(ThemeName, "HH", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(ThemeName, "SH", StringComparison.OrdinalIgnoreCase);

        public string? GetExistingLegacyAssetPath(string fileName)
        {
            if (UsesKern)
            {
                return null;
            }

            var path = GetAssetPath(fileName);
            var file = Path.GetFileName(path);
            return File.Exists(Path.Combine(_environment.WebRootPath, "css", ThemeName, file)) ? path : null;
        }

        public string GetAssetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                throw new ArgumentException("Der Theme-Dateiname ist ungültig.", nameof(fileName));
            }

            var legacyFileName = fileName switch
            {
                "theme.css" => "QW_Master.css",
                "menu.css" => "QW_Menu.css",
                "auflistung.css" => "QW_Auflistung.css",
                _ => fileName
            };

            return $"~/css/{ThemeName}/{legacyFileName}";
        }
    }
}
