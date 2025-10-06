using OpenSearch.Client;

namespace FCG.Games.Infra.Search;

public static class OpenSearchClientFactory
{
    public static IOpenSearchClient Create(string url, string defaultIndex)
    {
        var settings = new ConnectionSettings(new Uri(url))
            .DisableDirectStreaming()
            .PrettyJson()
            .DefaultIndex(defaultIndex);
        return new OpenSearchClient(settings);
    }
}
