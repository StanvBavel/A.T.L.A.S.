using System;
using System.Net.Http;
using System.Text;
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
        private readonly string _generateEndpoint;

        public string Name => "ImageSearch";
        public string Description => "Generate or search for an image based on a query.";
        public PermissionLevel RequiredPermission => PermissionLevel.Safe;

        public ImageSearchTool(HttpClient httpClient, IConfiguration configuration, ILogger<ImageSearchTool> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _generateEndpoint = configuration["LocalImageApi:Endpoint"] ?? "http://localhost:8000/generate-image";
        }

        public async Task<string> ExecuteAsync(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                _logger.LogWarning("[LOCAL IMAGE SERVICE] Tool invoked with empty parameters.");
                return "Error: Please provide a valid generation prompt, Sir.";
            }

            _logger.LogInformation("[LOCAL IMAGE SERVICE] Initiating local image generation for: '{Query}'", arguments);

            try
            {
                var payload = new { prompt = arguments };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                _logger.LogInformation("[LOCAL IMAGE SERVICE] Accessing generation endpoint: {Url}", _generateEndpoint);

                var response = await _httpClient.PostAsync(_generateEndpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[LOCAL IMAGE SERVICE] Generation API returned {StatusCode}.", response.StatusCode);
                    return "IMAGE_ERROR|I encountered a communication error with the local imaging cluster, Sir.";
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "SUCCEEDED" &&
                    root.TryGetProperty("image_url", out var imageUrlProp))
                {
                    var imageUrl = imageUrlProp.GetString();
                    _logger.LogInformation("[LOCAL IMAGE SERVICE] Successfully synthesized visual asset.");
                    return $"IMAGE_FOUND|{imageUrl}|Visual data synthesized for '{arguments}', Sir.";
                }

                _logger.LogWarning("[LOCAL IMAGE SERVICE] Invalid response format from image cluster.");
                return $"IMAGE_NOT_FOUND|I was unable to retrieve an image for that query, Sir.";
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "[LOCAL IMAGE SERVICE] Network protocol failure. Is the Python service running on port 8000?");
                return $"IMAGE_ERROR|The local image generation cluster is currently offline, Sir.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LOCAL IMAGE SERVICE] Critical subsystem failure during image synthesis.");
                return $"IMAGE_ERROR|An error occurred while attempting to access the visual generation system, Sir.";
            }
        }
    }
}
