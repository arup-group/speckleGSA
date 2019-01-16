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
        public ObservableCollection<string> Messages { get; set; }
        public ObservableCollection<Tuple<string, string>> StreamData { get; set; }

        public string ModelName { get; set; }

        public GSAController gsa;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            Messages = new ObservableCollection<string>();
            StreamData = new ObservableCollection<Tuple<string, string>>();

            ModelName = "";

            gsa = new GSAController();

            //For testing purposes
            ServerAddress.Text = "https://hestia.speckle.works/api/v1";
            EmailAddress.Text = "mishael.nuh@arup.com";
            Password.Password = "temporaryPassword";

            MessagePane.ItemsSource = Messages;

            SpeckleGSA.Status.Init(this.AddMessage, this.AddError, this.ChangeStatus);
        }

        #region Speckle Operations
        private void Login(object sender, RoutedEventArgs e)
        {
            string email = EmailAddress.Text;
            string password = Password.Password;
            string server = ServerAddress.Text;

            Task.Run(() => gsa.Login(email, password, server));
        }

        private void UpdateStreamList(object sender, RoutedEventArgs e)
        {
            Task.Run(() => gsa.GetStreamList()).ContinueWith(res =>
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

        private void CloneModelStreams(object sender, RoutedEventArgs e)
        {
            Task.Run(() => gsa.CloneModelStreams());
        }
        #endregion

        #region GSA
        private void LinkGSA(object sender, RoutedEventArgs e)
        {
            Task.Run(() => gsa.Link());
        }

        private void NewGSAFile(object sender, RoutedEventArgs e)
        {
            Task.Run(() => gsa.NewFile());
        }

        private void OpenGSAFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                Task.Run(() => gsa.OpenFile(openFileDialog.FileName));
        }
        #endregion

        #region Sender
        private void SendStream(object sender, RoutedEventArgs e)
        {
            Task.Run(() => gsa.ExportObjects(ModelName)).ContinueWith(
            delegate {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            SenderStreams.Items.Clear();

                            List<Tuple<string, string>> streams = gsa.GetSenderStreams();
                            foreach (Tuple<string, string> stream in streams)
                                SenderStreams.Items.Add(stream);
                        }
                        ));
                }
                catch
                { }
            });
        }
        #endregion

        #region Receiver
        private void ReceiveStream(object sender, RoutedEventArgs e)
        {
            string streamInput = new TextRange(ReceiverStreams.Document.ContentStart, ReceiverStreams.Document.ContentEnd).Text;
            if (streamInput == null)
                return;

            string[] streams = streamInput.Split(new string[] { "\r", "\n", "," }, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, string> streamDict = new Dictionary<string, string>();

            for (int i = 0; i < streams.Length; i++)
                streamDict.Add(i.ToString(), streams[i]);

            Task.Run(() => gsa.ImportObjects(streamDict));
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

        private void ChangeStatus(object sender, StatusEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    StatusText.Content = e.Name;
                    if (e.Percent >= 0 & e.Percent <= 100)
                    {
                        ProgressBar.IsIndeterminate = false;
                        ProgressBar.Value = e.Percent;
                    }
                    else
                    {
                        ProgressBar.IsIndeterminate = true;
                        ProgressBar.Value = 0;
                    }
                }
                )
            );
        }
        #endregion

        #region UI
        private void CopyStreamList(object sender, DataGridRowClipboardEventArgs e)
        {
            e.ClipboardRowContent.RemoveAt(0);
        }
        #endregion
    }
}
