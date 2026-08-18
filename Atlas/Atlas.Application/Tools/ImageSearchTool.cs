using System;
using System.Net.Http;
using System.Threading.Tasks;
using Atlas.Core;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;

namespace Atlas.Application.Tools
{
    public class ImageSearchTool : IAtlasTool
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ImageSearchTool> _logger;

        public string Name => "ImageSearch";
        public string Description => "Search the web for an image of an object.";
        public PermissionLevel RequiredPermission => PermissionLevel.Safe;

        public ImageSearchTool(HttpClient httpClient, ILogger<ImageSearchTool> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 A.T.L.A.S.");
        }

        public async Task<string> ExecuteAsync(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                _logger.LogWarning("[LOCAL IMAGE SEARCH] Tool invoked with empty parameters.");
                return "Error: Please provide a valid search query, Sir.";
            }

            _logger.LogInformation("[LOCAL IMAGE SEARCH] Initiating local scraping query for: '{Query}'", arguments);

            try
            {
                // DuckDuckGo Lite image search HTML scraping as a 100% local, API-key-free fallback
                var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(arguments)}+image";
                _logger.LogInformation("[LOCAL IMAGE SEARCH] Accessing endpoint: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                _logger.LogInformation("[LOCAL IMAGE SEARCH] Received HTTP {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // DuckDuckGo Lite stores images in elements with class 'result__icon__img' or specific external sources.
                var imgNodes = doc.DocumentNode.SelectNodes("//img[contains(@class, 'result__icon__img')] | //img[contains(@src, 'external')]");

                if (imgNodes != null && imgNodes.Count > 0)
                {
                    foreach (var node in imgNodes)
                    {
                        var src = node.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src))
                        {
                            if (src.StartsWith("//")) src = "https:" + src;
                            else if (src.StartsWith("/")) src = "https://duckduckgo.com" + src;

                            _logger.LogInformation("[LOCAL IMAGE SEARCH] Successfully retrieved visual asset: {Src}", src);
                            return $"IMAGE_FOUND|{src}|Visual data retrieved for '{arguments}', Sir.";
                        }
                    }
                }

                _logger.LogWarning("[LOCAL IMAGE SEARCH] No visual data found in the response for '{Query}'.", arguments);
                return $"IMAGE_NOT_FOUND|I was unable to retrieve an image for that query, Sir.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LOCAL IMAGE SEARCH] Critical subsystem failure during image extraction.");
                return $"IMAGE_ERROR|An error occurred while attempting to access the visual database, Sir.";
            }
        }
    }
}
