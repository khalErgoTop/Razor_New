using System.Text.RegularExpressions;
using TOPqwWeb_Core.Infrastructure.Theming;

namespace TOPqwWeb_Core.Services.Public
{
    public class IndexPageService
    {
        private static readonly Regex BodyContentExpression = new("<body[^>]*>(?<content>.*)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private readonly IWebHostEnvironment _environment;
        private readonly ThemeResolver _themeResolver;

        public IndexPageService(IWebHostEnvironment environment, ThemeResolver themeResolver)
        {
            _environment = environment;
            _themeResolver = themeResolver;
        }

        public async Task<string> GetLandingContentAsync()
        {
            var themeName = _themeResolver.GetThemeName();
            var indexPath = Path.Combine(_environment.WebRootPath, "themes", themeName, "index.html");

            if (!File.Exists(indexPath))
            {
                return string.Empty;
            }

            var html = await File.ReadAllTextAsync(indexPath);
            var bodyMatch = BodyContentExpression.Match(html);

            return bodyMatch.Success ? bodyMatch.Groups["content"].Value : html;
        }
    }
}
