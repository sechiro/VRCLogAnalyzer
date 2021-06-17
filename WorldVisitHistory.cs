using SQLite;
namespace VRCLogAnalyzer
{
    public class WorldVisitHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string WorldName { get; set; }
        public string WorldVisitTimestamp { get; set; }
        public string WorldId { get; set; }
        public string? AuthorName { get; set; }
        public string? AuthorId { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? Url { get; set; }
        public string? RawJson { get; set; }

        public WorldVisitHistory()
        {
            //Null非許容のフィールドを空文字で初期化
            this.WorldName = "";
            this.WorldVisitTimestamp = "";
            this.WorldId = "";
        }

        public override string ToString()
        {
            return $"{WorldVisitTimestamp} - {WorldName}";
        }
    }
}