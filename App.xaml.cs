using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Windows;

namespace ParaTranz2FrostyLoc
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public ObservableCollection<string> Logs { get; set; } = new ObservableCollection<string>();
        public static ParaTranz2FrostyLoc.Logger.Logger Logger { get; set; } = new();
        public static ParaTranz2FrostyLoc.Database Database { get; set; } = new();

        protected override void OnStartup(StartupEventArgs e)
        {
        }
    }

}
