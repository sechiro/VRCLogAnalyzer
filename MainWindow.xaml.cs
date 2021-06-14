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

using System.IO;

using System.Data;
using SQLite;
using System.Collections.ObjectModel;


namespace VRCLogAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<UserEncounterHistory> _userEnconterHistories = new List<UserEncounterHistory>();
        private ObservableCollection<Dto> _dtos = new ObservableCollection<Dto>();

        public MainWindow()
        {
            InitializeComponent();

            //デフォルト表示は、直近1週間
            DateTime today = DateTime.Today;
            StartDate.Text = today.AddDays(-7).ToLongDateString();
            EndDate.Text = today.ToLongDateString();

            updateView();
        }

        public void updateView()
        {
            //データを再度初期化
            _dtos = new ObservableCollection<Dto>();

            //VRCのログの日付の区切りがドットなので、それに揃える
            string queryStartDate = StartDate.Text.Replace("/", ".");
            string queryEndDate = EndDate.Text.Replace("/", ".") + " 23:59:59";

            //LIKE句で部分一致させる
            string queryUsername = '%' + QueryUsername.Text + '%';
            string queryWorldname = '%' + QueryWorldname.Text + '%';

            string databaseName = "VRCLogAnalyzer.db";
            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string databasePath = System.IO.Path.Combine(folderPath, databaseName);
            using (var conn = new SQLiteConnection(databasePath))
            {
                conn.CreateTable<UserEncounterHistory>();
                conn.CreateTable<WorldVisitHistory>();

                //Console.WriteLine(queryUsername);
                //Console.WriteLine(queryWorldname);

                string queryString = "SELECT * FROM UserEncounterHistory WHERE Timestamp BETWEEN ? AND ? ";
                queryString += " AND DisplayName LIKE ? ";
                queryString += " AND WorldName LIKE ? ";
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
                    using (var conn = new SQLiteConnection(databasePath))
                    {
                        worldinfo = conn.Query<WorldVisitHistory>(
                            "SELECT * FROM WorldVisitHistory WHERE WorldName = ? AND WorldVisitTimestamp = ?",
                            worldName,
                            worldVisitTimestamp
                        );
                    }
                    dto = new Dto(worldName, worldVisitTimestamp, worldinfo[0].Description);
                    dto.Dtos.Add(new Dto(u));
                }
                else if (worldName != u.WorldName || worldVisitTimestamp != u.WorldVisitTimestamp)
                {
                    _dtos.Add(dto);
                    worldName = u.WorldName;
                    worldVisitTimestamp = u.WorldVisitTimestamp;
                    List<WorldVisitHistory> worldinfo;
                    using (var conn = new SQLiteConnection(databasePath))
                    {
                        worldinfo = conn.Query<WorldVisitHistory>(
                            "SELECT * FROM WorldVisitHistory WHERE WorldName = ? AND WorldVisitTimestamp = ?",
                            worldName,
                            worldVisitTimestamp
                        );
                    }
                    dto = new Dto(worldName, worldVisitTimestamp, worldinfo[0].Description); dto.Dtos.Add(new Dto(u));
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
            public string Desc { get; set; }
            public List<Dto> Dtos { get; set; } = new List<Dto>();
        }
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
    }
}
