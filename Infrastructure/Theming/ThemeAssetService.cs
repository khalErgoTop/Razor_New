namespace TOPqwWeb_Core.Infrastructure.Theming
{
    public class ThemeAssetService
    {
        private readonly ThemeResolver _themeResolver;

        public ThemeAssetService(ThemeResolver themeResolver)
        {
            _themeResolver = themeResolver;
        }

        public string ThemeName => _themeResolver.GetThemeName();

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

            return $"/css/{ThemeName}/{legacyFileName}";
        }
    }
}
