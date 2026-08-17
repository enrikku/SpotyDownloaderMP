namespace SpotyDownloaderMP.Clases
{
    public class AlbumResult : INotifyPropertyChanged
    {
        private bool isSelected = true;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string AlbumType { get; set; } = string.Empty; // album, single, compilation
        public int TotalTracks { get; set; }
        public string ImageUrl { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => isSelected;
            set => SetField(ref isSelected, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}