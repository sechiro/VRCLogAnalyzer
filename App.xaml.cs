using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Threading;


namespace VRCLogAnalyzer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string GetAppPath()
        {
            string? appPath = System.IO.Path.GetDirectoryName(
                System.AppContext.BaseDirectory);
            if (appPath is null)
            {
                throw new DirectoryNotFoundException("実行ファイルのパス取得失敗");
            }

            return appPath;
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            foreach (string arg in e.Args)
            {
                switch (arg)
                {
                    case "/analyze":
                        var LogAnalyzer = new LogAnalyzer();
                        LogAnalyzer.UpdateDb();
                        //プロセスが残ってしまうことがあるので、そうならなくなりそうな方法をいろいろ実行
                        Current.Shutdown();
                        System.Threading.Thread.Sleep(10000);
                        Environment.Exit(0);
                        return;
                    //break;
                    default:
                        // メイン ウィンドウ表示
                        //MainWindow window = new MainWindow();
                        //window.Show();
                        break;
                }
            }
            // メイン ウィンドウ表示

            MainWindow window = new MainWindow();
            window.Show();
        }
        private void _analyzeLog()
        {
            MessageBox.Show("これはテストです");
            //TODO: ここでログ解析のロジックを呼び出す

        }
    }
}
