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

namespace SpeckleGSAUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int UPDATE_INTERVAL = 2000;

        public ObservableCollection<Tuple<string, string>> StreamData { get; set; }
        public ObservableCollection<string> Messages { get; set; }

        public string ProjectName { get; set; }
        public string ReceiverNodeStreamID { get; set; }
        public string ReceiverSectionStreamID { get; set; }
        public string ReceiverElementStreamID { get; set; }

        public UserManager userManager;
        public GSAController gsa;

      public MainWindow()
        {
            InitializeComponent();

            //For testing purposes
            ServerAddress.Text = "https://hestia.speckle.works/api/v1";
            EmailAddress.Text = "mishael.nuh@arup.com";
            Password.Password = "temporaryPassword";

            ProjectName = "";
            ReceiverNodeStreamID = "";
            ReceiverSectionStreamID = "";
            ReceiverElementStreamID = "";
            StreamData = new ObservableCollection<Tuple<string, string>>();
            Messages = new ObservableCollection<string>();

            DataContext = this;

            gsa = new GSAController();
            gsa.Messages.MessageAdded += AddMessage;
            gsa.Messages.ErrorAdded += AddError;
        }

        #region Server
        private async void Login(object sender, RoutedEventArgs e)
        {
            await gsa.Login(EmailAddress.Text, Password.Password, ServerAddress.Text);
        }

        private async void UpdateStreamList(object sender, RoutedEventArgs e)
        {
            await gsa.GetStreamList().ContinueWith(res =>
            {
                Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            List<Tuple<string, string>> streams = res.Result;
                            if (streams != null)
                            {
                                streams.Reverse();
                                StreamData.Clear();
                                foreach (Tuple<string, string> t in streams)
                                    StreamData.Add(t);
                            }
                        }
                        ));
            }
            );
        }
        #endregion

        #region GSA
        private async void LinkGSA(object sender, RoutedEventArgs e)
        {
            await gsa.Link();
        }

        private async void NewGSAFile(object sender, RoutedEventArgs e)
        {
            await gsa.NewFile();
        }

        private async void OpenGSAFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                await gsa.OpenFile(openFileDialog.FileName);
        }
        #endregion

        #region Sender
        private async void SendStream(object sender, RoutedEventArgs e)
        {
            await gsa.ExportObjects(ProjectName).ContinueWith(
                delegate
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            SenderNodeStreamID.Text = gsa.SenderNodeStreamID;
                            SenderSectionStreamID.Text = gsa.SenderSectionStreamID;
                            SenderElementStreamID.Text = gsa.SenderElementStreamID;
                        }
                        ));
                }
            );
        }
        #endregion

        #region Receiver
        private async void ReceiveStream(object sender, RoutedEventArgs e)
        {
            await gsa.ImportObjects(new Dictionary<string, string>()
            {
                { "Nodes", ReceiverNodeStreamID },
                { "Elements", ReceiverElementStreamID },
            });
        }
        #endregion

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
