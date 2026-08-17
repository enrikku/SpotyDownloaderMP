namespace SpotyDownloaderMP.Clases;

public static class SpotifyClientFactory
{
    private static SpotifyClient? _client;

    public static async Task<SpotifyClient> GetClientAsync()
    {
        if (_client != null)
            return _client;

        var options = AppConfig.GetSpotifyOptions();

        var authenticator = new ClientCredentialsAuthenticator(options.ClientId, options.ClientSecret);
        var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);

        _client = new SpotifyClient(config);
        return _client;
    }
}