using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Atlas.Core;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Tools
{
    public class ImageSearchTool : IAtlasTool
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ImageSearchTool> _logger;

        public string Name => "ImageSearch";
        public string Description => "Searches the web for an image based on a query and returns the URL.";
        public PermissionLevel RequiredPermission => PermissionLevel.Safe;

        public ImageSearchTool(HttpClient httpClient, ILogger<ImageSearchTool> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ATLAS-AI-Assistant/1.0");
        }

        public async Task<string> ExecuteAsync(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                _logger.LogWarning("[IMAGE SEARCH] Tool invoked with empty parameters.");
                return "Error: Please provide a valid search query, Sir.";
            }

            _logger.LogInformation("[IMAGE SEARCH] Initiating visual database query for: '{Query}'", arguments);

            try
            {
                var url = $"https://en.wikipedia.org/w/api.php?action=query&titles={Uri.EscapeDataString(arguments)}&prop=pageimages&format=json&pithumbsize=800";
                _logger.LogInformation("[IMAGE SEARCH] Accessing endpoint: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                _logger.LogInformation("[IMAGE SEARCH] Received HTTP {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                var pages = doc.RootElement.GetProperty("query").GetProperty("pages");

                foreach (var page in pages.EnumerateObject())
                {
                    if (page.Value.TryGetProperty("thumbnail", out var thumbnail) &&
                        thumbnail.TryGetProperty("source", out var source))
                    {
                        var imageUrl = source.GetString();
                        _logger.LogInformation("[IMAGE SEARCH] Successfully retrieved visual asset: {Src}", imageUrl);
                        return $"IMAGE_FOUND|{imageUrl}|Visual data retrieved for '{arguments}', Sir.";
                    }
                }

                _logger.LogWarning("[IMAGE SEARCH] No visual data found in the Wikipedia response for '{Query}'.", arguments);
                return $"IMAGE_NOT_FOUND|I was unable to retrieve an image for that query, Sir.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[IMAGE SEARCH] Critical subsystem failure during image extraction.");
                return $"IMAGE_ERROR|An error occurred while attempting to access the visual database, Sir.";
            }
        }
    }
}
