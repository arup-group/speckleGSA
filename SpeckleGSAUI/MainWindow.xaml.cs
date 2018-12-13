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
using SpeckleGSA;
using System.Timers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using Dragablz;

namespace SpeckleGSAUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> Messages { get; set; }
        public ObservableCollection<TabItem> Tabs;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            Messages = new ObservableCollection<string>();
            Tabs = new ObservableCollection<TabItem>();

            ControlPanelContainer.ItemsSource = Tabs;
            MessagePane.ItemsSource = Messages;
        }

        private void AddControlPanel(object sender, RoutedEventArgs e)
        {
            TabItem tab = new TabItem();
            tab.Content = new ControlPanel(AddMessage, AddError);
            tab.Header = "TEST";

            Tabs.Add(tab);

            ControlPanelContainer.SelectedIndex  = Tabs.Count() - 1;
        }

        #region Log
        private void AddMessage(object sender, MessageEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    Messages.Add("[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + e.Message);
                    MessagePane.ScrollIntoView(MessagePane.Items[MessagePane.Items.Count - 1]);
                }
                )
            );
        }

        private void AddError(object sender, MessageEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    Messages.Add("[" + DateTime.Now.ToString("h:mm:ss tt") + "] ERROR: " + e.Message);
                    MessagePane.ScrollIntoView(MessagePane.Items[MessagePane.Items.Count - 1]);
                }
                )
            );
        }
        #endregion
    }
}
