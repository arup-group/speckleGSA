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
using Microsoft.Win32;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using SpeckleGSA;

namespace SpeckleGSAUI
{
    /// <summary>
    /// Interaction logic for ControlPanel.xaml
    /// </summary>
    public partial class ControlPanel : UserControl
    {
        public ObservableCollection<Tuple<string, string>> StreamData { get; set; }

        public string ModelName { get; set; }
        public string ReceiverNodesStreamID { get; set; }
        public string ReceiverPropertiesStreamID { get; set; }
        public string ReceiverElementsStreamID { get; set; }

        public GSAController gsa;

        public ControlPanel()
        {
            InitializeComponent();

            //For testing purposes
            ServerAddress.Text = "https://hestia.speckle.works/api/v1";
            EmailAddress.Text = "mishael.nuh@arup.com";
            Password.Password = "temporaryPassword";

            ModelName = "";
            ReceiverNodesStreamID = "";
            ReceiverPropertiesStreamID = "";
            ReceiverElementsStreamID = "";
            StreamData = new ObservableCollection<Tuple<string, string>>();

            gsa = new GSAController();

            DataContext = this;
        }

        #region Speckle Operations
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

        private async void CloneModelStreams(object sender, RoutedEventArgs e)
        {
            await gsa.CloneModelStreams();
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
            gsa.AttachStatusHandler(ChangeSenderProgress);

            Task.Run(() => gsa.ExportObjects(ModelName)).ContinueWith(
            delegate {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            SenderNodesStreamID.Text = gsa.SenderNodesStreamID;
                            SenderPropertiesStreamID.Text = gsa.SenderPropertiesStreamID;
                            SenderElementsStreamID.Text = gsa.SenderElementsStreamID;
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
            gsa.AttachStatusHandler(ChangeReceiverProgress);

            Task.Run(() => gsa.ImportObjects(new Dictionary<string, string>()
            {
                { "properties", ReceiverPropertiesStreamID },
                { "nodes", ReceiverNodesStreamID },
                { "elements", ReceiverElementsStreamID },
            }));
        }
        #endregion

        #region UI
        private void CopyStreamList(object sender, DataGridRowClipboardEventArgs e)
        {
            e.ClipboardRowContent.RemoveAt(0);
        }

        private void ChangeSenderProgress(object sender, StatusEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    SenderStatus.Content = e.Name;
                    if (e.Percent >= 0 & e.Percent <= 100)
                    {
                        SenderProgressBar.IsIndeterminate = false;
                        SenderProgressBar.Value = e.Percent;
                    }
                    else
                    {
                        SenderProgressBar.IsIndeterminate = true;
                        SenderProgressBar.Value = 0;
                    }
                }
                )
            );
        }

        private void ChangeReceiverProgress(object sender, StatusEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    ReceiverStatus.Content = e.Name;
                    if (e.Percent >= 0 & e.Percent <= 100)
                    {
                        ReceiverProgressBar.IsIndeterminate = false;
                        ReceiverProgressBar.Value = e.Percent;
                    }
                    else
                    {
                        ReceiverProgressBar.IsIndeterminate = true;
                        ReceiverProgressBar.Value = 0;
                    }
                }
                )
            );
        }
        #endregion
    }
}
