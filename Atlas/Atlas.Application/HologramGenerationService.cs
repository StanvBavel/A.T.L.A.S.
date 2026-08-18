using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Application
{
    public interface IHologramGenerationService
    {
        Task<string> GenerateHologramAsync(string prompt);
    }

    public class HologramGenerationService : IHologramGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HologramGenerationService> _logger;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.meshy.ai/v2/text-to-3d";

        public HologramGenerationService(HttpClient httpClient, IConfiguration configuration, ILogger<HologramGenerationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["MeshyApiKey"] ?? string.Empty;

            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public async Task<string> GenerateHologramAsync(string prompt)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("[HOLOGRAM SERVICE] MeshyApiKey not configured. Returning fallback model.");
                return "https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Box/glTF-Binary/Box.glb";
            }

            _logger.LogInformation("[HOLOGRAM SERVICE] Initiating Meshy.ai text-to-3d synthesis for: '{Prompt}'", prompt);

            try
            {
                // 1. Submit task to Meshy.ai
                var payload = new
                {
                    mode = "preview",
                    prompt = prompt,
                    art_style = "realistic",
                    should_remesh = true
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(BaseUrl, content);

                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                if (!doc.RootElement.TryGetProperty("result", out var resultProp))
                {
                    throw new Exception("Meshy API response did not contain a 'result' Task ID.");
                }

                var taskId = resultProp.GetString();
                _logger.LogInformation("[HOLOGRAM SERVICE] Task accepted by Meshy.ai. Task ID: {TaskId}. Commencing polling...", taskId);

                // 2. Poll status
                int retries = 0;
                int maxRetries = 60; // 60 * 2 = 120 seconds max wait for preview model

                while (retries < maxRetries)
                {
                    _logger.LogInformation("[HOLOGRAM SERVICE] Polling status... Attempt {Attempt}/{MaxRetries}", retries + 1, maxRetries);

                    var statusResponse = await _httpClient.GetAsync($"{BaseUrl}/{taskId}");
                    statusResponse.EnsureSuccessStatusCode();

                    var statusJson = await statusResponse.Content.ReadAsStringAsync();
                    using var statusDoc = JsonDocument.Parse(statusJson);
                    var root = statusDoc.RootElement;

                    var status = root.GetProperty("status").GetString();

                    if (status == "SUCCEEDED")
                    {
                        var modelUrls = root.GetProperty("model_urls");
                        if (modelUrls.TryGetProperty("glb", out var glbUrlProp))
                        {
                            var glbUrl = glbUrlProp.GetString();
                            _logger.LogInformation("[HOLOGRAM SERVICE] Mesh synthesis complete. URL: {Url}", glbUrl);
                            return glbUrl;
                        }
                    }
                    else if (status == "FAILED" || status == "EXPIRED")
                    {
                        _logger.LogError("[HOLOGRAM SERVICE] Meshy generation failed. Status: {Status}", status);
                        throw new Exception($"Generation failed with status: {status}");
                    }

                    await Task.Delay(2000);
                    retries++;
                }

                _logger.LogWarning("[HOLOGRAM SERVICE] Polling timeout exceeded for Task ID: {TaskId}", taskId);
                throw new Exception("Hologram generation timed out.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HOLOGRAM SERVICE] Critical failure during 3D mesh synthesis.");
                throw;
            }
        }
    }
}
