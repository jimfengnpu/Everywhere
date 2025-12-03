using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Plugins.Web;
using Tavily;
using ZLinq;

namespace Everywhere.Chat.Plugins;

public partial class WebBrowserPlugin
{
    private class TavilyConnector(string apiKey, HttpClient httpClient, Uri? uri, ILoggerFactory? loggerFactory) : IWebSearchEngineConnector
    {
        private readonly TavilyClient _tavilyClient = new(httpClient, uri);
        private readonly ILogger _logger = loggerFactory?.CreateLogger(typeof(TavilyConnector)) ?? NullLogger.Instance;

        public async Task<IEnumerable<T>> SearchAsync<T>(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
        {
            if (count is <= 0 or >= 21)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, $"{nameof(count)} value must be greater than 0 and less than 21.");
            }

            _logger.LogDebug("Sending request");

            var response = await _tavilyClient.SearchAsync(apiKey: apiKey, query, maxResults: count, cancellationToken: cancellationToken);

            _logger.LogDebug("Response received");

            List<T>? returnValues;
            if (typeof(T) == typeof(string))
            {
                returnValues = response.Results
                    .AsValueEnumerable()
                    .Take(count)
                    .Select(x => x.Content)
                    .ToList() as List<T>;
            }
            else if (typeof(T) == typeof(WebPage))
            {
                returnValues = response.Results
                    .AsValueEnumerable()
                    .Take(count)
                    .Select(x => new WebPage
                    {
                        Name = x.Title,
                        Url = x.Url,
                        Snippet = x.Content
                    })
                    .ToList() as List<T>;
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");
            }

            return returnValues ?? [];
        }
    }
}