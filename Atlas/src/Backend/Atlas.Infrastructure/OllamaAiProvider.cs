using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Atlas.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Atlas.Infrastructure
{
    public class OllamaAiProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaAiProvider> _logger;
        private readonly string _modelName;
        private readonly string _endpoint;
        private readonly IToolDispatcher _toolDispatcher;

        public OllamaAiProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaAiProvider> logger, IToolDispatcher toolDispatcher)
        {
            _httpClient = httpClient;
            _logger = logger;
            _toolDispatcher = toolDispatcher;
            _modelName = configuration["AiSettings:ModelName"] ?? "llama3.1";
            _endpoint = configuration["AiSettings:OllamaEndpoint"] ?? "http://localhost:11434/api/chat";
        }

        public async Task<string> GenerateResponseAsync(string prompt)
        {
            _logger.LogInformation("[LLM PROVIDER] Submitting context to neural network model '{Model}' at {Endpoint}", _modelName, _endpoint);

            var messages = new List<object>
            {
                new { role = "system", content = "You are A.T.L.A.S., an advanced AI assistant. You speak formal, concise English. If asked to show an image, use the ImageSearch tool. If asked to show a 3D hologram or model, use the GenerateHologram tool." },
                new { role = "user", content = prompt }
            };

            var tools = new List<object>
            {
                new {
                    type = "function",
                    @function = new {
                        name = "ImageSearch",
                        description = "Search the web for an image of an object.",
                        parameters = new {
                            type = "object",
                            properties = new {
                                query = new { type = "string", description = "The exact subject to search for." }
                            },
                            required = new[] { "query" }
                        }
                    }
                },
                new {
                    type = "function",
                    @function = new {
                        name = "GenerateHologram",
                        description = "Generates a 3D hologram model of an object.",
                        parameters = new {
                            type = "object",
                            properties = new {
                                objectName = new { type = "string", description = "The name of the object to generate." }
                            },
                            required = new[] { "objectName" }
                        }
                    }
                },
                new {
                    type = "function",
                    @function = new {
                        name = "StopHologram",
                        description = "Stops and closes the currently active hologram.",
                        parameters = new {
                            type = "object",
                            properties = new { },
                        }
                    }
                }
            };

            var requestBody = new
            {
                model = _modelName,
                messages = messages,
                tools = tools,
                stream = false
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDocument = JsonDocument.Parse(responseString);
                var messageNode = jsonDocument.RootElement.GetProperty("message");

                // Inspect for Tool Calls
                if (messageNode.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
                {
                    var toolCall = toolCalls[0].GetProperty("function");
                    var functionName = toolCall.GetProperty("name").GetString();
                    var arguments = toolCall.GetProperty("arguments").ToString();

                    _logger.LogInformation("[LLM TOOL DISPATCH] Neural network elected to call: {Name} with args: {Args}", functionName, arguments);

                    using var argsDoc = JsonDocument.Parse(arguments);

                    if (functionName == "ImageSearch")
                    {
                        var query = argsDoc.RootElement.GetProperty("query").GetString() ?? "";
                        return $"/tool ImageSearch {query}";
                    }
                    if (functionName == "GenerateHologram")
                    {
                        var objName = argsDoc.RootElement.GetProperty("objectName").GetString() ?? "object";
                        return $"/hologram start {objName}";
                    }
                    if (functionName == "StopHologram")
                    {
                        return $"/hologram stop";
                    }
                }

                // Process standard text response
                if (messageNode.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString() ?? string.Empty;
                }

                return "Error: Unexpected response format from Ollama, Sir.";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[LLM PROVIDER] Connection to Ollama failed. Target endpoint: {Endpoint}", _endpoint);
                return "Error: Unable to connect to the local neural network provider, Sir. Is the Ollama service running on the host OS?";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLM PROVIDER] Critical parsing failure.");
                return "Error: A critical failure occurred while parsing the neural network response, Sir.";
            }
        }
    }
}
