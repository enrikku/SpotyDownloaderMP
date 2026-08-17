namespace SpotyDownloaderMP.Clases;

public static class AppConfig
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration => _configuration ??= Build();

    private static IConfiguration Build()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "No se encontró 'appsettings.json'. Copia 'appsettings.example.json' como " +
                $"'appsettings.json' en {AppContext.BaseDirectory} y pon tus credenciales de Spotify.",
                path);
        }

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
    }

    public static SpotifyOptions GetSpotifyOptions()
    {
        var options = new SpotifyOptions();
        Configuration.GetSection("Spotify").Bind(options);

        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException(
                "Faltan ClientId o ClientSecret en appsettings.json (sección 'Spotify').");
        }

        return options;
    }
}