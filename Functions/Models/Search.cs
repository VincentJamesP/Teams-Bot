namespace KTI.PAL.Teams.Functions.Models
{
    public class SearchQuery
    {
        public string type { get; set; }
        public string id { get; set; }
        public string empCode { get; set; }
        public string query { get; set; }
    }

    public class SearchResult
    {
        public string key { get; set; }
        public string content { get; set; }
        public string header { get; set; }
    }
}