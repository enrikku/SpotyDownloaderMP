namespace SpotyDownloaderMP.Services;

public class DownloadService
{
    private static readonly char[] InvalidPathChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0'];
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly Dictionary<string, byte[]> CoverCache = new();

    public static string GetMusicDir()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (!string.IsNullOrEmpty(dir)) return dir;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music");
    }

    // Comprueba si yt-dlp está disponible como comando antes de empezar y devuelve su versión
    public static string VerifyYtDlp()
    {
        try
        {
            var psi = new ProcessStartInfo("yt-dlp", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var version = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit();
            return string.IsNullOrEmpty(version) ? "desconocida" : version;
        }
        catch
        {
            throw new FileNotFoundException(
                "No se encontró 'yt-dlp'. Instálalo y asegúrate de que esté en el PATH.");
        }
    }

    // Ejecuta 'yt-dlp --update' y devuelve la salida del proceso
    public static async Task<string> UpdateYtDlpAsync(CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("yt-dlp", "--update")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("No se pudo iniciar yt-dlp.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrEmpty(stderr)
                    ? $"yt-dlp --update terminó con código {process.ExitCode}."
                    : stderr);
        }

        return string.IsNullOrEmpty(stdout) ? "yt-dlp actualizado." : stdout;
    }

    public static readonly string[] SupportedAudioFormats = ["mp3", "flac", "wav", "m4a", "opus"];

    public async Task DownloadAsync(SongResult song, string baseOutputDir, string audioFormat = "mp3", CancellationToken ct = default)
    {
        if (!SupportedAudioFormats.Contains(audioFormat))
            throw new ArgumentException($"Formato de audio no soportado: {audioFormat}", nameof(audioFormat));

        var primaryArtist = song.Artist.Split(',')[0].Trim();
        var artistDir = Path.Combine(baseOutputDir, Sanitize(primaryArtist));
        var songDir = Path.Combine(artistDir, Sanitize(song.AlbumName));
        var logPath = Path.Combine(artistDir, "download.log");

        Directory.CreateDirectory(songDir);

        try
        {
            var safeName = Sanitize(song.Title);
            var outputTemplate = Path.Combine(songDir, $"{safeName}.%(ext)s");

            var psi = new ProcessStartInfo("yt-dlp")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add($"ytsearch1:{song.Title} {song.Artist}");
            psi.ArgumentList.Add("-x");
            psi.ArgumentList.Add("--audio-format");
            psi.ArgumentList.Add(audioFormat);
            psi.ArgumentList.Add("--audio-quality");
            psi.ArgumentList.Add("0");
            psi.ArgumentList.Add("--no-playlist");
            psi.ArgumentList.Add("--no-overwrites");
            psi.ArgumentList.Add("--extractor-args");
            psi.ArgumentList.Add("youtube:player_client=android");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(outputTemplate);

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("No se pudo iniciar yt-dlp.");

            var stderr = new StringBuilder();
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var detail = stderr.ToString().Trim();
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(detail)
                        ? $"yt-dlp terminó con código {process.ExitCode}."
                        : $"yt-dlp (código {process.ExitCode}): {detail}");
            }

            var outputPath = Path.Combine(songDir, $"{safeName}.{audioFormat}");
            if (System.IO.File.Exists(outputPath))
            {
                try
                {
                    await ApplyTagsAsync(outputPath, song, ct);
                }
                catch (Exception ex)
                {
                    // El archivo se descargó correctamente aunque no se hayan podido escribir las etiquetas
                    Debug.WriteLine($"[WARN] No se pudieron escribir etiquetas en {outputPath}: {ex.Message}");
                }
            }

            WriteLog(logPath, song, error: null);
        }
        catch (Exception ex)
        {
            WriteLog(logPath, song, error: ex.Message);
            throw;
        }
    }

    private static void WriteLog(string logPath, SongResult song, string? error)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var sb = new StringBuilder();

        if (error is null)
        {
            sb.AppendLine($"[{timestamp}] OK    | {song.Title} | {song.AlbumName} | pista {song.TrackNumber}");
        }
        else
        {
            sb.AppendLine($"[{timestamp}] ERROR | {song.Title} | {song.AlbumName} | pista {song.TrackNumber}");
            // Indentamos el detalle del error para que sea fácil de leer
            foreach (var line in error.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                sb.AppendLine($"              {line.Trim()}");
        }

        System.IO.File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
    }

    private static async Task ApplyTagsAsync(string filePath, SongResult song, CancellationToken ct)
    {
        using var tagFile = TagLib.File.Create(filePath);

        tagFile.Tag.Title = song.Title;
        tagFile.Tag.Album = song.AlbumName;
        tagFile.Tag.Performers = song.Artist.Split(',').Select(a => a.Trim()).ToArray();
        tagFile.Tag.AlbumArtists = [song.Artist.Split(',')[0].Trim()];
        tagFile.Tag.Track = song.TrackNumber;

        if (uint.TryParse(song.ReleaseDate.Split('-')[0], out var year))
            tagFile.Tag.Year = year;

        if (!string.IsNullOrEmpty(song.ImageUrl))
        {
            if (!CoverCache.TryGetValue(song.ImageUrl, out var imageBytes))
            {
                imageBytes = await Http.GetByteArrayAsync(song.ImageUrl, ct);
                CoverCache[song.ImageUrl] = imageBytes;
            }

            tagFile.Tag.Pictures =
            [
                new TagLib.Picture(new TagLib.ByteVector(imageBytes))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = "image/jpeg"
                }
            ];
        }

        tagFile.Save();
    }

    private static string Sanitize(string name)
    {
        var sanitized = string.Concat(name.Select(c => InvalidPathChars.Contains(c) ? '_' : c)).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
    }
}