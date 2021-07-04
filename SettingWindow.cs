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
    /// Interaction logic for SettingWindow.xaml
    /// </summary>
    public partial class SettingWindow : Window
    {
        private string _appConfigPath;
        public SettingWindow()
        {
            InitializeComponent();
            string appPath = App.GetAppPath();
            _appConfigPath = System.IO.Path.Combine(appPath, "VRCLogAnalyzer.config");
        }
        public void Button_Click_OK(object sender, RoutedEventArgs e)
        {
            ChangeDbPathConfig();
            this.Close();
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            //設定ファイルは必ず同梱し、ファイルがないパターンはいったん想定外にする
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

            if (dbPathConfig == "MyDocuments")
            {
                DbPathConfigMyDocuments.IsChecked = true;
                DbPathConfigAppPath.IsChecked = false;
            }
            else if (dbPathConfig == "AppPath")
            {
                DbPathConfigMyDocuments.IsChecked = false;
                DbPathConfigAppPath.IsChecked = true;
            }

        }

        public void ChangeDbPathConfig()
        {
            System.Xml.XmlDocument appConfig = new System.Xml.XmlDocument();
            appConfig.Load(_appConfigPath);

            string? dbPathConfig = App.GetAppPath();

            if ((bool)DbPathConfigMyDocuments.IsChecked == true)
            {
                dbPathConfig = "MyDocuments";
            }
            else if ((bool)DbPathConfigAppPath.IsChecked == true)
            {
                dbPathConfig = "AppPath";
            }

            foreach (System.Xml.XmlNode n in appConfig["configuration"]["appSettings"])
            {
                if (n.Name == "add")
                {
                    if (n.Attributes.GetNamedItem("key").Value == "DbPathChoice")
                        n.Attributes.GetNamedItem("value").Value = dbPathConfig;
                }
            }
            appConfig.Save(_appConfigPath);
        }
    }
}
