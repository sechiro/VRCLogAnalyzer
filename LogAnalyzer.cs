using System.IO; //System.IO.FileInfo, System.IO.StreamReader, System.IO.StreamWriter
using System; //Exception
using System.Text; //Encoding
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SQLite;
using NLog;
using NLog.Config;
using System.Windows.Threading;

namespace VRCLogAnalyzer
{
    public class LogAnalyzer : DispatcherObject
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private string _appConfigPath;

        public void UpdateDb(string? logDir = null)
        {

            //設定ファイル読み込み
            string appPath = App.GetAppPath();
            _appConfigPath = System.IO.Path.Combine(appPath, "VRCLogAnalyzer.config");
            string? dbPathConfig = "AppPath";

            System.Xml.XmlDocument appConfig = new System.Xml.XmlDocument();
            appConfig.Load(_appConfigPath);
            foreach (System.Xml.XmlNode n in appConfig["configuration"]["appSettings"])
            {
                if (n.Name == "add")
                {
                    if (n.Attributes.GetNamedItem("key").Value == "DbPathChoice")
                    {
                        dbPathConfig = n.Attributes.GetNamedItem("value").Value;
                    }

                }
            }
            logger.Info($"dbPathConfig: {dbPathConfig}");

            string databaseName = "VRCLogAnalyzer.db";
            string folderPath = appPath;
            if (dbPathConfig == "MyDocuments")
            {
                folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\VRCLogAnalyzer";
                Directory.CreateDirectory(folderPath);
            }
            else if (dbPathConfig == "AppPath")
            {
                folderPath = appPath;
            }

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
            logger.Info("VRChat Log Dir: {}", logDir);
            DirectoryInfo dir = new DirectoryInfo(logDir);
            FileInfo[] info = dir.GetFiles("output_log_*.txt").OrderBy(p => p.LastWriteTime).ToArray();

            //JSONデータを加工時の参照用にキャッシュ
            Dictionary<string, JObject> userCache = new Dictionary<string, JObject>();
            Dictionary<string, JObject> worldCache = new Dictionary<string, JObject>();

            //進捗表示用
            int fileCount = info.Count();
            int fileProcessed = 0;


            foreach (FileInfo f in info)
            {
                //Console.WriteLine(f.Name);
                //Console.WriteLine(f.LastWriteTime);
                logger.Info("Processing: {}, LastWriteTIme: {}", f.Name, f.LastWriteTime);

                //進捗表示
                try
                {
                    this.Dispatcher.Invoke((Action)(() =>
                    {
                        var mainWindow = (MainWindow)App.Current.MainWindow;
                        mainWindow.loadingText.Text = $"ログデータ解析中です。しばらくお待ちください。(処理済:{fileProcessed}/対象ログ:{fileCount})";
                    }));
                }
                catch
                {
                    //メインウィンドウが表示されていないコマンドライン等の場合はここに
                    logger.Info($"コマンドラインでログデータ解析中です。しばらくお待ちください。(処理済:{fileProcessed}/対象ログ:{fileCount})");
                }


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
                        //詳細をシリアライズ済みJSONとして出力している行の処理
                        reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+"
                                        + Regex.Escape("[API]")
                                        + " {(?<rawjson>{.*})}$"
                                        );
                        mc = reg.Matches(line);
                        if (mc.Count > 0)
                        {
                            //Console.WriteLine(line);
                            logger.Debug(line);
                            foreach (Match match in mc)
                            {
                                GroupCollection groups = match.Groups;
                                JObject json;
                                try
                                {
                                    //steamDetailsが空の場合、JSONのフォーマット違反になるため、もし文字列があれば変換前に削除する
                                    json = JObject.Parse(groups["rawjson"].Value.Replace("\"steamDetails\":{{}},", ""));
                                }
                                catch (Newtonsoft.Json.JsonReaderException)
                                {
                                    //不正なJSONは無視する
                                    //自分自身の初期化API呼び出しの際のsteamDetailsのフォーマットがおかしい。
                                    //今回は利用しないデータなので、無視して次の行へ
                                    logger.Info("不正なJSONデータです。");
                                    logger.Info(line);
                                    continue;
                                }
                                catch (Exception ex)
                                {
                                    //全く解析ができなかった場合は、それを通常のログにその行を出力
                                    logger.Info("その他のエラーです。");
                                    logger.Info(line);
                                    logger.Info($"message: {ex.Message}");
                                    logger.Info($"{ex.StackTrace}");
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
                            continue;
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
                                if (worldCache.ContainsKey(worldName))
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
                            }
                            catch (System.Collections.Generic.KeyNotFoundException)
                            {
                                //情報が取得できなかった場合は空文字のまま
                                logger.Info($"{worldName}の詳細情報はありませんでした。");
                            }
                            catch (Exception ex)
                            {
                                //情報が取得したが、想定外のエラーの場合はその旨だけログに出す
                                logger.Info($"{worldName}の詳細情報取得時に不明なエラーが発生しました。");
                                logger.Info($"message: {ex.Message}");
                                logger.Info($"{ex.StackTrace}");
                            }

                            //worldVisitHistory INSERT
                            logger.Info("WorldVisitHistory: {0},{1},{2},{3},{4},{5},{6},{7},{8}",
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

                                try
                                {
                                    conn.Insert(newRecord);
                                }
                                catch (Exception ex)
                                {
                                    //DB登録失敗全般をログに記録
                                    logger.Info($"データベースの登録に失敗しました。");
                                    logger.Info($"message: {ex.Message}");
                                    logger.Info($"{ex.StackTrace}");
                                }
                            }
                            continue;
                        }

                        //ユーザー
                        reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+"
                                                + Regex.Escape("[Behaviour]")
                                                + " Initialized PlayerAPI \"(?<playername>.+)\" is (remote|local)"
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
                                if (userCache.ContainsKey(displayName))
                                {
                                    JObject current_cache = userCache[displayName];
                                    if (current_cache.ContainsKey("bio"))
                                    {
                                        bio = current_cache["bio"].ToString();
                                    }
                                }
                            }
                            catch (System.Collections.Generic.KeyNotFoundException)
                            {
                                //情報が取得できなかった場合は空文字のまま
                                logger.Info($"{displayName}の詳細情報はありませんでした。");
                            }
                            catch (Exception ex)
                            {
                                //情報が取得したが、想定外のエラーの場合はその旨だけログに出す
                                logger.Info($"{displayName}の詳細情報取得時に不明なエラーが発生しました。");
                                logger.Info($"message: {ex.Message}");
                                logger.Info($"{ex.StackTrace}");
                            }

                            //userEncounterHistory INSERT
                            logger.Info("UserEncounterHistory: {0},{1},{2},{3},{4}",
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

                                try
                                {
                                    conn.Insert(newRecord);
                                }
                                catch (Exception ex)
                                {
                                    //DB登録失敗全般をログに記録
                                    logger.Info($"データベースの登録に失敗しました。");
                                    logger.Info($"message: {ex.Message}");
                                    logger.Info($"{ex.StackTrace}");
                                }
                            }
                        }
                        continue;
                    }
                }

                //ファイル処理済みカウント
                fileProcessed += 1;
                logger.Info($"ログデータ処理状況　処理済:{fileProcessed}/対象ログ:{fileCount})");
                if (fileProcessed == fileCount)
                {
                    logger.Info($"ログデータの処理が完了しました。");
                }
            }
        }
    }
}