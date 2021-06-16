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
    /// Interaction logic forHelpWindow.xaml
    /// </summary>
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }
        public void Button_Click_OK(object sender, RoutedEventArgs e){
            this.Close();
        }
    }
}