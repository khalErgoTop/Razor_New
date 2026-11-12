using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using TOPqwWeb_Core.Infrastructure.Theming;

namespace TOPqwWeb_Core.Services.Public
{
    public class IndexPageService
    {
        private static readonly Regex BodyContentExpression = new("<body[^>]*>(?<content>.*)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private readonly IWebHostEnvironment _environment;
        private readonly ThemeResolver _themeResolver;
        private readonly ThemeAssetService _themeAssets;

        public IndexPageService(IWebHostEnvironment environment, ThemeResolver themeResolver, ThemeAssetService themeAssets)
        {
            _environment = environment;
            _themeResolver = themeResolver;
            _themeAssets = themeAssets;
        }

        public async Task<string> GetLandingContentAsync(PathString pathBase)
        {
            var themeName = _themeResolver.GetThemeName();
            var indexPath = _themeAssets.UsesKern
                ? Path.Combine(_environment.WebRootPath, "themes", themeName, "index.html")
                : Path.Combine(_environment.WebRootPath, "css", themeName, "index.htms");

            if (!File.Exists(indexPath))
            {
                return string.Empty;
            }

            var html = await File.ReadAllTextAsync(indexPath);
            var bodyMatch = BodyContentExpression.Match(html);
            html = bodyMatch.Success ? bodyMatch.Groups["content"].Value : html;

            if (_themeAssets.UsesKern && string.IsNullOrWhiteSpace(html))
            {
                var legacyIndex = Path.Combine(_environment.WebRootPath, "css", themeName, "index.htms");
                html = File.Exists(legacyIndex) ? await File.ReadAllTextAsync(legacyIndex) : string.Empty;
            }

            var baseUrl = HtmlEncoder.Default.Encode(pathBase.ToUriComponent());
            html = html.Replace("\"\"", "\"");
            foreach (var page in new[] { "Suche", "Auflistung", "SuchAssistent", "Hilfestellung", "Hilfe", "FAQ", "Impressum", "Datenschutz" })
            {
                html = Regex.Replace(html, $"(?<=href=\")[ ]*{page}\\.aspx(?=[?\"#])", _ => $"{baseUrl}/{page}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            html = html.Replace("src=\"Css/", $"src=\"{baseUrl}/css/", StringComparison.OrdinalIgnoreCase);
            html = html.Replace("<link rel=\"stylesheet\" href=\"/lib/kern/kern.css\" />", "", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(themeName, "HH", StringComparison.OrdinalIgnoreCase))
            {
                html = html.Replace("url(Images/speech.svg)", $"url({baseUrl}/css/HH/speech.svg)", StringComparison.OrdinalIgnoreCase)
                           .Replace("url(Images/nospeech.svg)", $"url({baseUrl}/css/HH/nospeech.svg)", StringComparison.OrdinalIgnoreCase);
            }

            return html;
        }
    }
}
