using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LlmDocuments
{
    /// <summary>
    /// Optional: asks a model to turn free text into JSON that satisfies the template's schema.
    ///
    /// GroupDocs.Assembly never sees the model. It sees a JSON file and a JSON Schema, which is
    /// exactly what makes this design replaceable: any model, any SDK, any local runtime that can
    /// produce JSON will do. The schema is handed to the model as the contract and used again when
    /// the document is assembled, so both sides agree on the same shape.
    /// </summary>
    public static class ModelClient
    {
        public const string DefaultModel = "claude-sonnet-5-5";

        public static async Task<string> ExtractAsync(
            string sourceText,
            string schemaJson,
            string instructions,
            string model,
            CancellationToken cancellationToken)
        {
            string? apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "ANTHROPIC_API_KEY is not set. Run 'generate' with the sample data instead, " +
                    "or point --data at JSON produced elsewhere.");
            }

            string prompt =
                instructions + Environment.NewLine + Environment.NewLine +
                "JSON Schema the answer must satisfy:" + Environment.NewLine + schemaJson + Environment.NewLine + Environment.NewLine +
                "Source text:" + Environment.NewLine + sourceText;

            object payload = new
            {
                model,
                max_tokens = 2048,
                messages = new[] { new { role = "user", content = prompt } }
            };

            using HttpClient http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(2);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"The model call failed with {(int)response.StatusCode}: {body}");
            }

            return ExtractJson(ReadText(body));
        }

        private static string ReadText(string responseBody)
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            StringBuilder text = new StringBuilder();

            if (document.RootElement.TryGetProperty("content", out JsonElement content))
            {
                foreach (JsonElement block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("text", out JsonElement value))
                    {
                        text.Append(value.GetString());
                    }
                }
            }

            return text.ToString();
        }

        /// <summary>Pulls the JSON object out of the answer, tolerating markdown fences and stray prose.</summary>
        public static string ExtractJson(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new InvalidOperationException("The model returned nothing.");
            }

            int start = answer.IndexOf('{');
            int end = answer.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                throw new InvalidOperationException("The model did not return a JSON object:" + Environment.NewLine + answer);
            }

            string json = answer.Substring(start, end - start + 1);

            // Fail here rather than inside the assembler if the answer is not valid JSON.
            using (JsonDocument.Parse(json))
            {
                return json;
            }
        }

        public static string ReadInstructions()
        {
            string path = Path.Combine(RepositoryPaths.Prompts, "extract-prompt.md");
            return File.Exists(path)
                ? File.ReadAllText(path)
                : "Return only a JSON object that satisfies the schema below. No prose, no markdown fences.";
        }
    }
}
