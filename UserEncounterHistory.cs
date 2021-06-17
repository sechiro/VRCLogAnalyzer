using SQLite;
namespace VRCLogAnalyzer
{
    public class UserEncounterHistory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Timestamp { get; set; }
        public string DisplayName { get; set; }
        public string WorldName { get; set; }
        public string WorldVisitTimestamp { get; set; }
        public string? Bio { get; set; }

        public UserEncounterHistory()
        {
            this.Timestamp = "";
            this.DisplayName = "";
            this.WorldName = "";
            this.WorldVisitTimestamp = "";
        }
        public override string ToString()
        {
            return $"{Timestamp} - {DisplayName}";
        }
    }
}