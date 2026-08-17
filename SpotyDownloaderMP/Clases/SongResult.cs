namespace SpotyDownloaderMP.Clases;

public class SongResult : INotifyPropertyChanged
{
    private string title = string.Empty;
    private string artist = string.Empty;
    private string albumName = string.Empty;
    private string duration = string.Empty;
    private bool isSelected = true;
    private string status = "Pendiente";

    // Metadatos Spotify para embeber en el MP3
    public string ImageUrl { get; set; } = string.Empty;

    public string ReleaseDate { get; set; } = string.Empty;
    public uint TrackNumber { get; set; }

    public string Title
    {
        get => title;
        set => SetField(ref title, value);
    }

    public string Artist
    {
        get => artist;
        set => SetField(ref artist, value);
    }

    public string AlbumName
    {
        get => albumName;
        set => SetField(ref albumName, value);
    }

    public string Duration
    {
        get => duration;
        set => SetField(ref duration, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetField(ref isSelected, value);
    }

    public string Status
    {
        get => status;
        set => SetField(ref status, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}