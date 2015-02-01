
namespace MediaBrowser.ApiInteraction.Data
{
    public class LocalItemQuery
    {
        public string ServerId { get; set; }
        public string AlbumArtist { get; set; }
        public string AlbumId { get; set; }
        public string Type { get; set; }
        public string MediaType { get; set; }
        public string[] ExcludeTypes { get; set; }

        public LocalItemQuery()
        {
            ExcludeTypes = new string[] { };
        }
    }
}
