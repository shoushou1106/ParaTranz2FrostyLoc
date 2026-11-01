using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Windows.Data;
using System.Windows;

namespace ParaTranz2FrostyLoc.Logger
{
    public class Logger : ILogger, INotifyPropertyChanged
    {
        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public string LogText => sb.ToString();
        private StringBuilder sb = new StringBuilder();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter != null)
            {
                sb.AppendLine($"[{logLevel.ToString()}]: {formatter(state, exception)}");
            }
            RaisePropertyChanged("LogText");
        }

        public void AddBinding(UIElement elementToBind, DependencyProperty propertyToBind)
        {
            Binding b = new Binding("LogText")
            {
                Source = this,
                Mode = BindingMode.OneWay
            };

            BindingOperations.SetBinding(elementToBind, propertyToBind, b);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
