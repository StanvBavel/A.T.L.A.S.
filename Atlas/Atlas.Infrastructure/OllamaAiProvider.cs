using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Atlas.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure
{
    public class OllamaAiProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaAiProvider> _logger;
        private readonly string _modelName;
        private readonly string _endpoint;

        public OllamaAiProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaAiProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _modelName = configuration["AiSettings:ModelName"] ?? "llama3.2";
            _endpoint = configuration["AiSettings:OllamaEndpoint"] ?? "http://localhost:11434/api/generate";
        }

        public async Task<string> GenerateResponseAsync(string prompt)
        {
            var requestBody = new
            {
                model = _modelName,
                prompt = prompt,
                stream = false
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(responseString);

                if (jsonDocument.RootElement.TryGetProperty("response", out var responseProperty))
                {
                    return responseProperty.GetString() ?? string.Empty;
                }

                return "Error: Unexpected response format from Ollama.";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to Ollama at {Endpoint}", _endpoint);
                return "Error: Unable to connect to the local AI provider. Is Ollama running?";
            }
        }
    }
}
