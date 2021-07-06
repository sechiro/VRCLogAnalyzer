using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;

using System.IO;

using System.Data;
using SQLite;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using NLog;


namespace VRCLogAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<UserEncounterHistory> _userEnconterHistories = new List<UserEncounterHistory>();
        private List<WorldVisitHistory> _worldVisitHistories = new List<WorldVisitHistory>();
        private ObservableCollection<Dto> _dtos = new ObservableCollection<Dto>();
        private bool _isFirstBoot;
        private string _appConfigPath;
        private string _databasePath;
        private string _databaseName = "VRCLogAnalyzer.db";
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public MainWindow()
        {
            InitializeComponent();

            //デフォルト表示は、直近1週間
            DateTime today = DateTime.Today;
            StartDate.Text = today.AddDays(-7).ToLongDateString();
            EndDate.Text = today.ToLongDateString();

            setDatabasePath();
            updateView();
        }

        public void setDatabasePath()
        {
            //設定ファイル読み込み
            string appPath = App.GetAppPath();
            _appConfigPath = System.IO.Path.Combine(appPath, "VRCLogAnalyzer.config");
            string? dbPathConfig = "AppPath";

            //設定ファイルは必ず同梱し、ファイルがないパターンはいったん想定外にする
            //dbPathConfig = System.Configuration.ConfigurationManager.AppSettings["DbPathChoice"];
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

            _databasePath = System.IO.Path.Combine(folderPath, _databaseName);
        }

        public void updateView()
        {
            //データを再度初期化
            _dtos = new ObservableCollection<Dto>();

            //VRCのログの日付の区切りがドットなので、それに揃える
            string queryStartDate = StartDate.Text.Replace("/", ".");
            string queryEndDate = EndDate.Text.Replace("/", ".") + " 23:59:59";

            //LIKE句で部分一致させる
            //LIKE句でエスケープが必要な特殊文字が含まれていたら、「|」でエスケープ処理する
            bool needLikeEscape = false;
            string inputUsername = QueryUsername.Text;
            string inputWorldName = QueryWorldname.Text;
            if (inputUsername.Contains("%") || inputUsername.Contains("_") ||
                inputWorldName.Contains("%") || inputWorldName.Contains("_"))
            {
                needLikeEscape = true;
                inputUsername = inputUsername.Replace("%", "|%");
                inputUsername = inputUsername.Replace("_", "|_");
                inputWorldName = inputWorldName.Replace("%", "|%");
                inputWorldName = inputWorldName.Replace("_", "|_");
            }

            string queryUsername = '%' + inputUsername + '%';
            string queryWorldname = '%' + inputWorldName + '%';

            CreateTableResult isCreated;
            using (var conn = new SQLiteConnection(_databasePath))
            {
                //初回テーブル作成時はCreated、それ以外はMigratedが返る
                isCreated = conn.CreateTable<UserEncounterHistory>();
                conn.CreateTable<WorldVisitHistory>();

                if (isCreated == CreateTableResult.Created)
                {
                    _isFirstBoot = true;
                }
                //Console.WriteLine(isCreated);
                //Console.WriteLine(queryUsername);
                //Console.WriteLine(queryWorldname);

                string queryString = "SELECT * FROM UserEncounterHistory WHERE Timestamp BETWEEN ? AND ? ";
                queryString += " AND DisplayName LIKE ? ";
                if (needLikeEscape)
                {
                    queryString += " ESCAPE '|' ";
                }
                queryString += " AND WorldName LIKE ? ";
                if (needLikeEscape)
                {
                    queryString += " ESCAPE '|' ";
                }
                queryString += "ORDER BY Timestamp;";
                //Console.WriteLine(queryString);

                _userEnconterHistories = conn.Query<UserEncounterHistory>(
                    queryString,
                    queryStartDate,
                    queryEndDate,
                    queryUsername,
                    queryWorldname
                );
            }

            //TreeView表示内容組み立て
            var worldName = "";
            var worldVisitTimestamp = "";
            var dto = new Dto(worldName);
            foreach (UserEncounterHistory u in _userEnconterHistories)
            {
                if (worldName == "") //1st loop
                {
                    worldName = u.WorldName;
                    worldVisitTimestamp = u.WorldVisitTimestamp;


                    List<WorldVisitHistory> worldinfo;
                    using (var conn = new SQLiteConnection(_databasePath))
                    {
                        worldinfo = conn.Query<WorldVisitHistory>(
                            "SELECT * FROM WorldVisitHistory WHERE WorldName = ? AND WorldVisitTimestamp = ?",
                            worldName,
                            worldVisitTimestamp
                        );
                    }
                    if (worldinfo.Count > 0)
                    {
                        dto = new Dto(worldName, worldVisitTimestamp, worldinfo[0].Description ?? "");
                    }
                    else
                    {
                        dto = new Dto(worldName, worldVisitTimestamp, "");
                    }

                    dto.Dtos.Add(new Dto(u));
                }
                else if (worldName != u.WorldName || worldVisitTimestamp != u.WorldVisitTimestamp)
                {
                    _dtos.Add(dto);
                    worldName = u.WorldName;
                    worldVisitTimestamp = u.WorldVisitTimestamp;
                    List<WorldVisitHistory> worldinfo;
                    using (var conn = new SQLiteConnection(_databasePath))
                    {
                        worldinfo = conn.Query<WorldVisitHistory>(
                            "SELECT * FROM WorldVisitHistory WHERE WorldName = ? AND WorldVisitTimestamp = ?",
                            worldName,
                            worldVisitTimestamp
                        );
                    }
                    dto = new Dto(worldName, worldVisitTimestamp, worldinfo[0].Description ?? ""); dto.Dtos.Add(new Dto(u));
                }
                else
                {
                    dto.Dtos.Add(new Dto(u));
                }
            }

            _dtos.Add(dto);

            HistoryTree.ItemsSource = _dtos;
        }

        public void setDummyData()
        {
            //ダミーデータ
            _userEnconterHistories.Add(new UserEncounterHistory
            {
                Id = 1,
                Timestamp = "2021.06.12 16:55:55",
                DisplayName = "sechiro",
                WorldName = "test world",
                WorldVisitTimestamp = "2021.06.12 16:50:00",
                Bio = "dummy"
            });
            _userEnconterHistories.Add(new UserEncounterHistory
            {
                Id = 2,
                Timestamp = "2021.06.12 16:57:55",
                DisplayName = "sechiro",
                WorldName = "test world",
                WorldVisitTimestamp = "2021.06.12 16:50:00",

            });
            _userEnconterHistories.Add(new UserEncounterHistory
            {
                Id = 3,
                Timestamp = "2021.06.12 16:59:55",
                DisplayName = "sechiro",
                WorldName = "test world2",
                WorldVisitTimestamp = "2021.06.12 16:58:00",
            });

        }

        public sealed class Dto
        {
            public Dto(string name)
            {
                Name = name;
            }
            public Dto(string worldName, string worldVisitTimestamp)
            {
                Name = $"{worldName} (Visited at {worldVisitTimestamp})";
            }
            public Dto(string worldName, string worldVisitTimestamp, string description)
            {
                Name = $"{worldName} (Visited at {worldVisitTimestamp})";
                Desc = $"{description}";
            }
            public Dto(UserEncounterHistory u)
            {
                Name = $"{u.Timestamp}     {u.DisplayName}";
                Desc = $"{u.Bio}";
            }
            public string Name { get; set; }
            public string? Desc { get; set; }
            public List<Dto> Dtos { get; set; } = new List<Dto>();
        }

        /**
        * ボタンクリックに対応したアクション定義
        */
        public async void Button_UpdateDb(object sender, RoutedEventArgs e)
        {
            loadingText.Visibility = Visibility.Visible;

            await RunUpdateDb();
            loadingText.Visibility = Visibility.Collapsed;
        }

        private Task RunUpdateDb()
        {
            var LogAnalyzer = new LogAnalyzer();
            return Task.Run(() => { LogAnalyzer.UpdateDb(); });
        }

        public void Button_UpdateView(object sender, RoutedEventArgs e)
        {
            updateView();
        }
        public void Button_FirstHelp(object sender, RoutedEventArgs e)
        {
            PopupHelpWindow();
        }
        public void Button_Readme(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://github.com/sechiro/VRCLogAnalyzer");
        }
        public void Button_Credit(object sender, RoutedEventArgs e)
        {
            CreditWindow creditWin = new CreditWindow();
            creditWin.Owner = this;
            creditWin.ShowDialog();
        }
        public void Button_Settings(object sender, RoutedEventArgs e)
        {
            SettingWindow setttingWin = new SettingWindow();
            setttingWin.Owner = this;
            setttingWin.ShowDialog();
            setDatabasePath();
        }
        public void Button_ExportUser(object sender, RoutedEventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.FileName = "user-history.csv";
            fileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            fileDialog.Filter = "CSVファイル|*.csv|すべてのファイル|*.*";
            fileDialog.FilterIndex = 0;

            if (fileDialog.ShowDialog() == true)
            {

                using (var conn = new SQLiteConnection(_databasePath))
                {

                    string queryString = "SELECT * FROM UserEncounterHistory ";
                    queryString += "ORDER BY Timestamp;";
                    Console.WriteLine(queryString);

                    _userEnconterHistories = conn.Query<UserEncounterHistory>(
                        queryString
                    );
                }
                StringBuilder sb = new StringBuilder();

                //ヘッダ
                sb.Append("Id").Append(",");
                sb.Append("Timestamp").Append(",");
                sb.Append("\"" + "DisplayName" + "\"").Append(",");
                sb.Append("\"" + "Bio" + "\"").Append(",");
                sb.Append("\"" + "WorldName" + "\"").Append(",");
                sb.Append("\"" + "WorldVisitTimestamp" + "\"").Append(Environment.NewLine);

                foreach (UserEncounterHistory u in _userEnconterHistories)
                {
                    sb.Append(u.Id).Append(",");
                    sb.Append(u.Timestamp).Append(",");
                    sb.Append("\"" + u.DisplayName + "\"").Append(",");
                    sb.Append("\"" + u.Bio + "\"").Append(",");
                    sb.Append("\"" + u.WorldName + "\"").Append(",");
                    sb.Append("\"" + u.WorldVisitTimestamp + "\"").Append(Environment.NewLine);
                }

                Stream st = fileDialog.OpenFile();
                StreamWriter sw = new StreamWriter(st, Encoding.GetEncoding("UTF-8"));
                sw.Write(sb.ToString());
                sw.Close();
                st.Close();
                MessageBox.Show("CSVファイルを出力しました。");
            }

        }
        public void Button_ExportWorld(object sender, RoutedEventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.FileName = "world-history.csv";
            fileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            fileDialog.Filter = "CSVファイル|*.csv|すべてのファイル|*.*";
            fileDialog.FilterIndex = 0;

            if (fileDialog.ShowDialog() == true)
            {

                using (var conn = new SQLiteConnection(_databasePath))
                {

                    string queryString = "SELECT * FROM WorldVisitHistory ";
                    queryString += "ORDER BY WorldVisitTimestamp;";
                    Console.WriteLine(queryString);

                    _worldVisitHistories = conn.Query<WorldVisitHistory>(
                        queryString
                    );
                }
                StringBuilder sb = new StringBuilder();

                //ヘッダ
                sb.Append("Id").Append(",");
                sb.Append("WorldVisitTimestamp").Append(",");
                sb.Append("\"" + "WorldName" + "\"").Append(",");
                sb.Append("\"" + "Description" + "\"").Append(",");
                sb.Append("\"" + "AuthorName" + "\"").Append(",");
                sb.Append("\"" + "Url" + "\"").Append(",");
                sb.Append("\"" + "ImageUrl" + "\"").Append(Environment.NewLine);

                foreach (WorldVisitHistory w in _worldVisitHistories)
                {
                    sb.Append(w.Id).Append(",");
                    sb.Append(w.WorldVisitTimestamp).Append(",");
                    sb.Append("\"" + w.WorldName + "\"").Append(",");
                    sb.Append("\"" + w.Description + "\"").Append(",");
                    sb.Append("\"" + w.AuthorName + "\"").Append(",");
                    sb.Append("\"" + w.Url + "\"").Append(",");
                    sb.Append("\"" + w.ImageUrl + "\"").Append(Environment.NewLine);
                }

                Stream st = fileDialog.OpenFile();
                StreamWriter sw = new StreamWriter(st, Encoding.GetEncoding("UTF-8"));
                sw.Write(sb.ToString());
                sw.Close();
                st.Close();
                MessageBox.Show("CSVファイルを出力しました。");
            }
        }
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            TextBlock parentItem = ((e.Source as MenuItem).Parent as ContextMenu).PlacementTarget as TextBlock;
            string nameText = parentItem.Text;

            /*
            * テキストは以下の形式で入っているので、そこからそれぞれの名前だけ切り出す
            * User:
            *  Name = $"{u.Timestamp}     {u.DisplayName}";
            * World:
            *  Name = $"{worldName} (Visited at {worldVisitTimestamp})";
            */

            Regex reg;
            MatchCollection mc;
            GroupCollection groups;
            string copyName = "";

            // ユーザー名コピー
            reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+)     (?<name>.*)");
            mc = reg.Matches(nameText);

            if (mc.Count > 0)
            {
                Match match = mc[0];
                groups = match.Groups;
                copyName = groups["name"].Value;
                Clipboard.SetData(DataFormats.Text, copyName);
                logger.Info($"Name copied: {copyName}");
                return;
            }

            // ワールド名コピー
            reg = new Regex("(?<name>.*) "+ Regex.Escape("(Visited at ") + "(?<timestamp>[0-9.]+ [0-9:]+)" + Regex.Escape(")") );
            mc = reg.Matches(nameText);

            if (mc.Count > 0)
            {
                Match match = mc[0];
                groups = match.Groups;
                copyName = groups["name"].Value;
                Clipboard.SetData(DataFormats.Text, copyName);
                logger.Info($"Name copied: {copyName}");
                return;
            }

            Console.WriteLine(parentItem.Text);
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            //初回は、データ更新をするよう案内を出す
            if (_isFirstBoot)
            {
                PopupHelpWindow();
            }
        }

        private void PopupHelpWindow()
        {
            HelpWindow helpWin = new HelpWindow();
            helpWin.Owner = this;
            helpWin.ShowDialog();
        }
        public static void OpenBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch
            {

                url = url.Replace("&", "^&");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });

            }
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            Application.Current.Shutdown();
        }
    }
}
