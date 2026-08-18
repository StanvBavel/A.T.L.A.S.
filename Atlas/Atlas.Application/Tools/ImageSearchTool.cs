using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Atlas.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Tools
{
    public class ImageSearchTool : IAtlasTool
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ImageSearchTool> _logger;
        private readonly string _unsplashApiKey;

        public string Name => "ImageSearch";
        public string Description => "Search the web for an image of an object.";
        public PermissionLevel RequiredPermission => PermissionLevel.Safe;

        public ImageSearchTool(HttpClient httpClient, IConfiguration configuration, ILogger<ImageSearchTool> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _unsplashApiKey = configuration["UnsplashApiKey"] ?? string.Empty;
        }

        public async Task<string> ExecuteAsync(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                _logger.LogWarning("[IMAGE SEARCH] Tool invoked with empty parameters.");
                return "Error: Please provide a valid search query, Sir.";
            }

            _logger.LogInformation("[IMAGE SEARCH] Initiating visual database query for: '{Query}'", arguments);

            if (string.IsNullOrEmpty(_unsplashApiKey))
            {
                _logger.LogWarning("[IMAGE SEARCH] UnsplashApiKey not configured in appsettings.json.");
                return "IMAGE_ERROR|The visual database API key is not configured, Sir.";
            }

            try
            {
                // Unsplash Search API endpoint
                var url = $"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(arguments)}&per_page=1&client_id={_unsplashApiKey}";
                _logger.LogInformation("[IMAGE SEARCH] Accessing Unsplash API.");

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[IMAGE SEARCH] Unsplash API returned {StatusCode}.", response.StatusCode);
                    return "IMAGE_ERROR|I encountered a communication error with the visual database, Sir.";
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var results = doc.RootElement.GetProperty("results");

                if (results.GetArrayLength() > 0)
                {
                    var firstResult = results[0];
                    var urls = firstResult.GetProperty("urls");
                    var imageUrl = urls.GetProperty("regular").GetString(); // Get a good resolution

                    _logger.LogInformation("[IMAGE SEARCH] Successfully retrieved visual asset: {Src}", imageUrl);
                    return $"IMAGE_FOUND|{imageUrl}|Visual data retrieved for '{arguments}', Sir.";
                }

                _logger.LogInformation("[IMAGE SEARCH] No images found for '{Query}'.", arguments);
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
