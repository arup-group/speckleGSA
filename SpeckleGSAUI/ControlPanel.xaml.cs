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
        public string ReceiverNodeStreamID { get; set; }
        public string ReceiverSectionStreamID { get; set; }
        public string ReceiverElementStreamID { get; set; }

        public GSAController gsa;

        public ControlPanel(EventHandler<MessageEventArgs> AddMessage, EventHandler<MessageEventArgs> AddError)
        {
            InitializeComponent();

            //For testing purposes
            ServerAddress.Text = "https://hestia.speckle.works/api/v1";
            EmailAddress.Text = "mishael.nuh@arup.com";
            Password.Password = "temporaryPassword";

            ModelName = "";
            ReceiverNodeStreamID = "";
            ReceiverSectionStreamID = "";
            ReceiverElementStreamID = "";
            StreamData = new ObservableCollection<Tuple<string, string>>();

            DataContext = this;

            gsa = new GSAController();
            gsa.Messages.MessageAdded += AddMessage;
            gsa.Messages.ErrorAdded += AddError;
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
        private void SendStream(object sender, RoutedEventArgs e)
        {
            gsa.ExportObjects(ModelName);//.ContinueWith(
            //    delegate
            //    {
            //        Application.Current.Dispatcher.BeginInvoke(
            //            DispatcherPriority.Background,
            //            new Action(() =>
            //            {
            //                SenderNodeStreamID.Text = gsa.SenderNodeStreamID;
            //                SenderSectionStreamID.Text = gsa.SenderSectionStreamID;
            //                SenderElementStreamID.Text = gsa.SenderElementStreamID;
            //            }
            //            ));
            //    }
            //);
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

        #region UI
        private void CopyStreamList(object sender, DataGridRowClipboardEventArgs e)
        {
            e.ClipboardRowContent.RemoveAt(0);
        }
        #endregion
    }
}
