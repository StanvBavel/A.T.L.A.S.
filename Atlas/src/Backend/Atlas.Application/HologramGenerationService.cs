using System;
using System.Net.Http;
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
        private readonly string _generateEndpoint;
        private readonly string _statusEndpoint;

        public HologramGenerationService(HttpClient httpClient, IConfiguration configuration, ILogger<HologramGenerationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _generateEndpoint = configuration["Local3DApi:Endpoint"] ?? "http://localhost:5000/generate-3d";
            _statusEndpoint = configuration["Local3DApi:StatusEndpoint"] ?? "http://localhost:5000/tasks/";
        }

        public async Task<string> GenerateHologramAsync(string prompt)
        {
            _logger.LogInformation("[LOCAL HOLOGRAM SERVICE] Initiating local spatial mesh synthesis for: '{Prompt}'", prompt);

            try
            {
                // 1. Submit task to local Docker container (e.g. TripoSR or Shap-E)
                var payload = new { prompt = prompt };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                _logger.LogInformation("[LOCAL HOLOGRAM SERVICE] Dispatching to localized cluster: {Url}", _generateEndpoint);

                var response = await _httpClient.PostAsync(_generateEndpoint, content);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                if (!doc.RootElement.TryGetProperty("task_id", out var taskIdElement))
                {
                     throw new Exception("Local 3D API response did not contain a 'task_id'.");
                }

                var taskId = taskIdElement.GetString() ?? string.Empty;
                _logger.LogInformation("[LOCAL HOLOGRAM SERVICE] Task accepted by local cluster. Task ID: {TaskId}. Commencing polling...", taskId);

                // 2. Poll status from local Docker container
                int retries = 0;
                int maxRetries = 60; // Up to 120 seconds for complex local renders

                while (retries < maxRetries)
                {
                    _logger.LogInformation("[LOCAL HOLOGRAM SERVICE] Polling local cluster... Attempt {Attempt}/{MaxRetries}", retries + 1, maxRetries);

                    var statusResponse = await _httpClient.GetAsync($"{_statusEndpoint}{taskId}");
                    statusResponse.EnsureSuccessStatusCode();

                    var statusJson = await statusResponse.Content.ReadAsStringAsync();
                    using var statusDoc = JsonDocument.Parse(statusJson);

                    if (!statusDoc.RootElement.TryGetProperty("status", out var statusElement))
                    {
                        throw new Exception("Local 3D API status response did not contain a 'status'.");
                    }

                    var status = statusElement.GetString();

                    if (status == "SUCCEEDED")
                    {
                        if (statusDoc.RootElement.TryGetProperty("obj_url", out var glbUrlElement))
                        {
                            var glbUrl = glbUrlElement.GetString();
                            _logger.LogInformation("[LOCAL HOLOGRAM SERVICE] Local mesh synthesis complete. Resolving asset: {Url}", glbUrl);
                            return glbUrl ?? string.Empty;
                        }
                        throw new Exception("Local 3D API succeeded but returned no 'glb_url'.");
                    }
                    else if (status == "FAILED")
                    {
                        _logger.LogError("[LOCAL HOLOGRAM SERVICE] Local generation failed. Status: {Status}", status);
                        throw new Exception($"Generation failed with status: {status}");
                    }

                    await Task.Delay(2000);
                    retries++;
                }

                _logger.LogWarning("[LOCAL HOLOGRAM SERVICE] Polling timeout exceeded for Task ID: {TaskId}", taskId);
                throw new Exception("Hologram generation timed out.");
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "[LOCAL HOLOGRAM SERVICE] Connection to the local 3D cluster failed. Is the Docker container running on port 5000?");
                throw new Exception("Unable to reach the local 3D generation cluster, Sir.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LOCAL HOLOGRAM SERVICE] Critical failure during local 3D mesh synthesis.");
                throw;
            }
        }
    }
}
