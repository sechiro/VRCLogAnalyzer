using System.IO; //System.IO.FileInfo, System.IO.StreamReader, System.IO.StreamWriter
using System; //Exception
using System.Text; //Encoding
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SQLite;


namespace VRCLogAnalyzer
{
    public class LogAnalyzer
    {
        public void UpdateDb(string? logDir = null)
        {

            string databaseName = "VRCLogAnalyzer.db";
            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string databasePath = System.IO.Path.Combine(folderPath, databaseName);

            using (var conn = new SQLiteConnection(databasePath))
            {
                //テーブルがない場合は作成
                conn.CreateTable<UserEncounterHistory>();
                conn.CreateTable<WorldVisitHistory>();

                AnalyzeLog(conn, logDir);

            }
        }
        public void AnalyzeLog(SQLiteConnection conn, string? logDir = null)
        {
            if (logDir == null)
            {
                logDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low\\VRChat\\VRChat";
            }
            Console.WriteLine(logDir);
            DirectoryInfo dir = new DirectoryInfo(logDir);
            FileInfo[] info = dir.GetFiles("output_log_*.txt").OrderBy(p => p.LastWriteTime).ToArray();

            //JSONデータを加工時の参照用にキャッシュ
            Dictionary<string, JObject> userCache = new Dictionary<string, JObject>();
            Dictionary<string, JObject> worldCache = new Dictionary<string, JObject>();

            foreach (FileInfo f in info)
            {
                Console.WriteLine(f.Name);
                Console.WriteLine(f.LastWriteTime);
                //shift_jis利用のため
                //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                using (FileStream fs = f.Open(FileMode.Open))
                {
                    var reader = new StreamReader(fs, Encoding.UTF8);

                    string? line; //ファイルを読み終わったらnullになる
                    string worldVisitTimestamp = "";
                    string worldName = "";
                    while ((line = reader.ReadLine()) != null)
                    {
                        //ログを1行ずつ処理。ログにはワールドの情報→ユーザーの情報の順番で情報が登場する想定。
                        //JSON詳細データは、それぞれの情報の前に登場する

                        Regex reg;
                        MatchCollection mc;

                        //JSON
                        reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+"
                                        + Regex.Escape("[API]")
                                        + " {(?<rawjson>{.*})}$"
                                        );
                        mc = reg.Matches(line);
                        if (mc.Count > 0)
                        {
                            //Console.WriteLine(line);
                            foreach (Match match in mc)
                            {
                                GroupCollection groups = match.Groups;
                                JObject json;
                                try
                                {
                                    json = JObject.Parse(groups["rawjson"].Value);
                                }
                                catch (Newtonsoft.Json.JsonReaderException)
                                {
                                    //不正なJSONは無視する
                                    //自分自身の初期化API呼び出しの際のsteamDetailsのフォーマットがおかしい。
                                    //今回は利用しないデータなので、無視して次の行へ
                                    continue;
                                }

                                if (json["authorName"] != null)
                                {
                                    //Console.WriteLine("World!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                                    string name_raw = json["name"].ToString();
                                    //Console.WriteLine(name_raw);
                                    json["raw_json"] = groups["rawjson"].Value;
                                    worldCache[name_raw] = json;
                                }
                                else if (json["displayName"] != null)
                                {
                                    //Console.WriteLine("User!!!!!!!!!!!!!!!!!!!");
                                    string name_raw = json["displayName"].ToString();
                                    //Console.WriteLine(name_raw);
                                    json["raw_json"] = groups["rawjson"].Value;
                                    userCache[name_raw] = json;
                                }
                            }
                        }

                        //ワールド
                        reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+Joining or Creating Room: (?<worldname>.+)");
                        mc = reg.Matches(line);
                        if (mc.Count > 0)
                        {
                            //Console.WriteLine(line);
                            GroupCollection groups;
                            foreach (Match match in mc)
                            {
                                groups = match.Groups;
                                //Console.WriteLine(groups["timestamp"].Value);
                                worldVisitTimestamp = groups["timestamp"].Value;
                                //Console.WriteLine(groups["worldname"].Value);
                                worldName = groups["worldname"].Value;
                            }

                            //worlcCacheに情報があれば取得
                            string worldId = "";
                            string authorName = "";
                            string authorId = "";
                            string description = "";
                            string imageUrl = "";
                            string url = "";
                            string rawJson = "";
                            try
                            {
                                JObject current_cache = worldCache[worldName];
                                worldId = current_cache["id"].ToString();
                                authorName = current_cache["authorName"].ToString();
                                authorId = current_cache["authorId"].ToString();
                                description = current_cache["description"].ToString();
                                imageUrl = current_cache["imageUrl"].ToString();
                                url = "https://vrchat.com/home/world/" + worldId;
                                rawJson = current_cache["raw_json"].ToString();
                            }
                            catch (System.Collections.Generic.KeyNotFoundException)
                            { //情報が取得できなかった場合は空文字のまま
                            }

                            //worldVisitHistory INSERT
                            Console.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                            worldVisitTimestamp, worldName, worldId, authorName, authorId, description, imageUrl, url, rawJson);
                            var ret = conn.Query<WorldVisitHistory>(
                                "SELECT * FROM WorldVisitHistory WHERE WorldName = ? AND WorldVisitTimestamp = ?",
                                worldName,
                                worldVisitTimestamp
                                );
                            if (ret.Count() == 0)
                            {
                                var newRecord = new WorldVisitHistory();
                                newRecord.WorldVisitTimestamp = worldVisitTimestamp;
                                newRecord.WorldName = worldName;
                                newRecord.WorldId = worldId;
                                newRecord.AuthorName = authorName;
                                newRecord.AuthorId = authorId;
                                newRecord.Description = description;
                                newRecord.ImageUrl = imageUrl;
                                newRecord.Url = url;
                                newRecord.RawJson = rawJson;

                                conn.Insert(newRecord);
                            }
                        }

                        //ユーザー
                        reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+"
                                                + Regex.Escape("[Behaviour]")
                                                + " Initialized PlayerAPI \"(?<playername>.+)\" is remote"
                                            );
                        mc = reg.Matches(line);
                        if (mc.Count > 0)
                        {
                            //Console.WriteLine(line);
                            string timestamp = "";
                            string displayName = "";
                            foreach (Match match in mc)
                            {
                                GroupCollection groups = match.Groups;
                                //Console.WriteLine(groups["timestamp"].Value);
                                timestamp = groups["timestamp"].Value;
                                //Console.WriteLine(groups["playername"].Value);
                                displayName = groups["playername"].Value;
                            }

                            //userCacheにbioの情報があれば取得
                            string bio = "";
                            try
                            {
                                JObject current_cache = userCache[displayName];
                                bio = current_cache["bio"].ToString();
                            }
                            catch (System.Collections.Generic.KeyNotFoundException)
                            { //情報が取得できなかった場合は空文字のまま
                            }

                            //userEncounterHistory INSERT
                            Console.WriteLine("{0},{1},{2},{3},{4}",
                            timestamp, displayName, bio, worldVisitTimestamp, worldName);
                            var ret = conn.Query<UserEncounterHistory>(
                                "SELECT * FROM UserEncounterHistory WHERE Timestamp = ? AND DisplayName = ?",
                                timestamp,
                                displayName
                                );
                            if (ret.Count() == 0)
                            {
                                var newRecord = new UserEncounterHistory();
                                newRecord.Timestamp = timestamp;
                                newRecord.DisplayName = displayName;
                                newRecord.Bio = bio;
                                newRecord.WorldName = worldName;
                                newRecord.WorldVisitTimestamp = worldVisitTimestamp;

                                conn.Insert(newRecord);
                            }
                        }

                    }
                }
            }
        }
    }
}