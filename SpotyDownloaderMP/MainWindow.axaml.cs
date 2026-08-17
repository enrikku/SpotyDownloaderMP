namespace SpotyDownloaderMP;

public partial class MainWindow : Window
{
    private enum SearchMode
    {
        Artist,
        Track,
        Playlist
    }

    private SearchMode currentMode = SearchMode.Artist;
    private readonly bool isLoaded;

    public ObservableCollection<AlbumResult> Albums { get; } = [];
    public ObservableCollection<SongResult> Songs { get; } = [];
    public ObservableCollection<PlaylistResult> Playlists { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        AlbumsList.ItemsSource = Albums;
        SongsList.ItemsSource = Songs;
        PlaylistsList.ItemsSource = Playlists;
        isLoaded = true;
        _ = ShowVersionInfoAsync();
    }

    private async Task ShowVersionInfoAsync()
    {
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var appVersionText = appVersion is null ? "" : $"v{appVersion.Major}.{appVersion.Minor}.{appVersion.Build}";

        try
        {
            var ytDlpVersion = await Task.Run(DownloadService.VerifyYtDlp);
            VersionText.Text = $"{appVersionText} · yt-dlp {ytDlpVersion}";
        }
        catch (FileNotFoundException)
        {
            VersionText.Text = $"{appVersionText} · yt-dlp no encontrado";
        }
    }

    private async void UpdateYtDlpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        UpdateYtDlpButton.IsEnabled = false;
        StatusText.Text = "Actualizando yt-dlp...";

        try
        {
            var resultado = await DownloadService.UpdateYtDlpAsync();
            StatusText.Text = $"yt-dlp: {resultado}";
            await ShowVersionInfoAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error al actualizar yt-dlp: {ex.Message}";
        }
        finally
        {
            UpdateYtDlpButton.IsEnabled = true;
        }
    }

    private void SearchModeComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded)
            return;

        var tag = (SearchModeComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        currentMode = tag switch
        {
            "track" => SearchMode.Track,
            "playlist" => SearchMode.Playlist,
            _ => SearchMode.Artist
        };

        SearchBox.Watermark = currentMode switch
        {
            SearchMode.Track => "Escribe el nombre de una canción...",
            SearchMode.Playlist => "Escribe el nombre de una playlist...",
            _ => "Escribe el nombre de un artista..."
        };

        ResultsTitle.Text = currentMode switch
        {
            SearchMode.Track => "Canciones",
            SearchMode.Playlist => "Playlists",
            _ => "Álbumes"
        };

        AlbumsScroll.IsVisible = currentMode == SearchMode.Artist;
        SongsScroll.IsVisible = currentMode == SearchMode.Track;
        PlaylistsScroll.IsVisible = currentMode == SearchMode.Playlist;

        Albums.Clear();
        Songs.Clear();
        Playlists.Clear();
        StatusText.Text = "Listo para buscar.";
    }

    private void SearchBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            SearchButton_OnClick(sender, e);
    }

    private async void SearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var query = SearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            StatusText.Text = currentMode switch
            {
                SearchMode.Track => "Escribe el nombre de una canción.",
                SearchMode.Playlist => "Escribe el nombre de una playlist.",
                _ => "Escribe el nombre de un artista."
            };
            return;
        }

        SearchButton.IsEnabled = false;
        Albums.Clear();
        Songs.Clear();
        Playlists.Clear();

        try
        {
            switch (currentMode)
            {
                case SearchMode.Track:
                    await SearchTracksAsync(query);
                    break;

                case SearchMode.Playlist:
                    await SearchPlaylistsAsync(query);
                    break;

                default:
                    await SearchArtistAsync(query);
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private async Task SearchArtistAsync(string query)
    {
        StatusText.Text = "Buscando álbumes...";

        var spotify = await SpotifyClientFactory.GetClientAsync();

        // Buscar el artista
        var searchResult = await spotify.Search.Item(
            new SearchRequest(SearchRequest.Types.Artist, query));
        var artist = searchResult.Artists.Items?.FirstOrDefault();

        if (artist == null)
        {
            StatusText.Text = "No se encontró ningún artista con ese nombre.";
            return;
        }

        // Traer todos los álbumes del artista (incluye singles, se puede filtrar con IncludeGroupsParam)
        var albumsRequest = new ArtistsAlbumsRequest
        {
            IncludeGroupsParam = ArtistsAlbumsRequest.IncludeGroups.Album
                                 | ArtistsAlbumsRequest.IncludeGroups.Single,
            Limit = 50
        };

        var albumsResponse = await spotify.Artists.GetAlbums(artist.Id, albumsRequest);

        // Evitar duplicados (Spotify repite álbumes por cada mercado/edición)
        var vistos = new HashSet<string>();

        if(albumsResponse == null || albumsResponse.Items == null)
        {
            StatusText.Text = "Error al obtener albums";
            return;
        }

        foreach (var album in albumsResponse.Items)
        {
            if (!vistos.Add(album.Name))
                continue;

            Albums.Add(new AlbumResult
            {
                Id = album.Id,
                Name = album.Name,
                ReleaseDate = album.ReleaseDate,
                AlbumType = album.AlbumType,
                TotalTracks = album.TotalTracks,
                ImageUrl = album.Images?.FirstOrDefault()?.Url ?? string.Empty
            });
        }

        StatusText.Text = $"{Albums.Count} álbumes encontrados. Selecciona los que quieras descargar.";
    }

    private async Task SearchTracksAsync(string query)
    {
        StatusText.Text = "Buscando canciones...";

        var spotify = await SpotifyClientFactory.GetClientAsync();
        var searchResult = await spotify.Search.Item(
            new SearchRequest(SearchRequest.Types.Track, query) { Limit = 50 });

        foreach (var track in searchResult.Tracks.Items ?? [])
            Songs.Add(MapTrack(track));

        StatusText.Text = Songs.Count == 0
            ? "No se encontraron canciones con ese nombre."
            : $"{Songs.Count} canciones encontradas. Selecciona las que quieras descargar.";
    }

    private async Task SearchPlaylistsAsync(string query)
    {
        StatusText.Text = "Buscando playlists...";

        var spotify = await SpotifyClientFactory.GetClientAsync();
        var searchResult = await spotify.Search.Item(
            new SearchRequest(SearchRequest.Types.Playlist, query) { Limit = 50 });

        // La búsqueda de playlists de Spotify a veces devuelve elementos nulos
        foreach (var playlist in (searchResult.Playlists.Items ?? []).Where(p => p is not null))
        {
            Playlists.Add(new PlaylistResult
            {
                Id = playlist.Id ?? string.Empty,
                Name = playlist.Name ?? "Sin nombre",
                Owner = playlist.Owner?.DisplayName ?? "Desconocido",
                TotalTracks = playlist.Items?.Total ?? 0,
                ImageUrl = playlist.Images?.FirstOrDefault()?.Url ?? string.Empty
            });
        }

        StatusText.Text = Playlists.Count == 0
            ? "No se encontraron playlists con ese nombre."
            : $"{Playlists.Count} playlists encontradas. Selecciona las que quieras descargar.";
    }

    private static SongResult MapTrack(FullTrack track, string? fallbackAlbumName = null, string? fallbackImageUrl = null) => new()
    {
        Title = track.Name,
        Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
        AlbumName = track.Album?.Name ?? fallbackAlbumName ?? string.Empty,
        ImageUrl = track.Album?.Images?.FirstOrDefault()?.Url ?? fallbackImageUrl ?? string.Empty,
        ReleaseDate = track.Album?.ReleaseDate ?? string.Empty,
        TrackNumber = (uint)track.TrackNumber,
        Duration = TimeSpan.FromMilliseconds(track.DurationMs).ToString(@"mm\:ss")
    };

    private async void DownloadAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var haySeleccion = currentMode switch
        {
            SearchMode.Track => Songs.Any(s => s.IsSelected),
            SearchMode.Playlist => Playlists.Any(p => p.IsSelected),
            _ => Albums.Any(a => a.IsSelected)
        };

        if (!haySeleccion)
        {
            StatusText.Text = currentMode switch
            {
                SearchMode.Track => "No hay canciones seleccionadas.",
                SearchMode.Playlist => "No hay playlists seleccionadas.",
                _ => "No hay álbumes seleccionados."
            };
            return;
        }

        DownloadAllButton.IsEnabled = false;
        SearchButton.IsEnabled = false;
        DownloadProgress.Value = 0;

        try
        {
            DownloadService.VerifyYtDlp();

            var spotify = await SpotifyClientFactory.GetClientAsync();

            // Fase 1: recopilar todas las canciones a descargar según el modo activo
            List<SongResult> canciones = currentMode switch
            {
                SearchMode.Track => [.. Songs.Where(s => s.IsSelected)],
                SearchMode.Playlist => await CollectSongsFromPlaylistsAsync(spotify),
                _ => await CollectSongsFromAlbumsAsync(spotify)
            };

            // Fase 2: descargar cada canción con yt-dlp
            var outputDir = DownloadService.GetMusicDir();
            var audioFormat = (FormatComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "mp3";
            var downloader = new DownloadService();

            int fallos = 0;
            for (int i = 0; i < canciones.Count; i++)
            {
                var cancion = canciones[i];
                StatusText.Text = $"Descargando ({i + 1}/{canciones.Count}): {cancion.Title} — {cancion.Artist}";
                DownloadProgress.Value = i * 100.0 / canciones.Count;

                cancion.Status = "Descargando";
                try
                {
                    await downloader.DownloadAsync(cancion, outputDir, audioFormat);
                    cancion.Status = "Completado";
                }
                catch (Exception ex)
                {
                    cancion.Status = "Error";
                    fallos++;
                    System.Diagnostics.Debug.WriteLine($"[ERROR] {cancion.Title}: {ex.Message}");
                }

                DownloadProgress.Value = (i + 1) * 100.0 / canciones.Count;
            }

            var ok = canciones.Count - fallos;
            StatusText.Text = fallos == 0
                ? $"{canciones.Count} canciones descargadas en: {outputDir}"
                : $"{ok}/{canciones.Count} descargadas, {fallos} fallaron. Carpeta: {outputDir}";
            DownloadProgress.Value = 100;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            DownloadAllButton.IsEnabled = true;
            SearchButton.IsEnabled = true;
        }
    }

    private async Task<List<SongResult>> CollectSongsFromAlbumsAsync(SpotifyClient spotify)
    {
        var seleccionados = Albums.Where(a => a.IsSelected).ToList();
        var canciones = new List<SongResult>();

        for (int i = 0; i < seleccionados.Count; i++)
        {
            var album = seleccionados[i];
            StatusText.Text = $"Obteniendo canciones de \"{album.Name}\" ({i + 1}/{seleccionados.Count})...";

            var tracks = await spotify.Albums.GetTracks(album.Id);
            
            if(tracks == null || tracks.Items == null)
            {
                StatusText.Text = "Error al obtener canciones";
                return canciones;
            }
            
            foreach (var track in tracks.Items)
            {
                canciones.Add(new SongResult
                {
                    Title = track.Name,
                    Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
                    AlbumName = album.Name,
                    ImageUrl = album.ImageUrl,
                    ReleaseDate = album.ReleaseDate,
                    TrackNumber = (uint)track.TrackNumber,
                    Duration = TimeSpan.FromMilliseconds(track.DurationMs).ToString(@"mm\:ss")
                });
            }
        }

        return canciones;
    }

    private async Task<List<SongResult>> CollectSongsFromPlaylistsAsync(SpotifyClient spotify)
    {
        var seleccionadas = Playlists.Where(p => p.IsSelected).ToList();
        var canciones = new List<SongResult>();

        for (int i = 0; i < seleccionadas.Count; i++)
        {
            var playlist = seleccionadas[i];
            StatusText.Text = $"Obteniendo canciones de \"{playlist.Name}\" ({i + 1}/{seleccionadas.Count})...";

            var firstPage = await spotify.Playlists.GetPlaylistItems(playlist.Id);
            var items = await spotify.PaginateAll(firstPage);

            foreach (var item in items)
            {
                if (item.Track is not FullTrack track)
                    continue;

                canciones.Add(MapTrack(track, playlist.Name, playlist.ImageUrl));
            }
        }

        return canciones;
    }

    private void SelectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        switch (currentMode)
        {
            case SearchMode.Track:
                foreach (var song in Songs) song.IsSelected = true;
                break;

            case SearchMode.Playlist:
                foreach (var playlist in Playlists) playlist.IsSelected = true;
                break;

            default:
                foreach (var album in Albums) album.IsSelected = true;
                break;
        }
    }

    private void DeselectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        switch (currentMode)
        {
            case SearchMode.Track:
                foreach (var song in Songs) song.IsSelected = false;
                break;

            case SearchMode.Playlist:
                foreach (var playlist in Playlists) playlist.IsSelected = false;
                break;

            default:
                foreach (var album in Albums) album.IsSelected = false;
                break;
        }
    }
}