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

        public string EmailAddress;
        public string RestApi;
        public string ApiToken;
        public Controller controller;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
            
            Messages = new ObservableCollection<string>();
            StreamData = new ObservableCollection<Tuple<string, string>>();
            
            controller = new Controller();
            
            //Default settings
            SendOnlyMeaningfulNodes.IsChecked = Settings.SendOnlyMeaningfulNodes;
            Merge1DElementsIntoPolyline.IsChecked = Settings.Merge1DElementsIntoPolyline;
            Merge2DElementsIntoMesh.IsChecked = Settings.Merge2DElementsIntoMesh;

            GSA.Init();
            Status.Init(this.AddMessage, this.AddError, this.ChangeStatus);
            MessagePane.ItemsSource = Messages;
        }

        #region Speckle Operations
        private void Login(object sender, RoutedEventArgs e)
        {
            SpecklePopup.MainWindow p = new SpecklePopup.MainWindow(false, true);
            this.IsEnabled = false;
            Brush oldBackground = this.Background;
            this.Background = new SolidColorBrush(Color.FromArgb(255,81,140,255));
            p.ShowDialog();
            this.IsEnabled = true;
            this.Background = oldBackground;

            if (p.restApi != null && p.apitoken != null)
            {
                Status.AddMessage("Logged in to " + p.selectedEmail);

                GSA.Close();
                SenderTab.IsEnabled = false;
                ReceiverTab.IsEnabled = false;
                EmailAddress = p.selectedEmail;
                RestApi = p.restApi;
                ApiToken = p.apitoken;
                UpdateClientLists();
            }
            else
                Status.AddError("Failed to log in");
        }

        private void UpdateStreamList(object sender, RoutedEventArgs e)
        {
            if (RestApi == null && ApiToken == null)
            {
                Status.AddError("Not logged in");
                return;
            }

            Task.Run(() => StreamManager.GetStreams(RestApi, ApiToken)).ContinueWith(res =>
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
            });
        }
        #endregion

        #region GSA
        private void NewGSAFile(object sender, RoutedEventArgs e)
        {
            SenderTab.IsEnabled = false;
            ReceiverTab.IsEnabled = false;
            Task.Run(() => GSA.NewFile(EmailAddress, RestApi)).ContinueWith(
                delegate
                {
                    try
                    {
                        Application.Current.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                            new Action(() =>
                            {
                                UpdateClientLists();
                                SenderTab.IsEnabled = true;
                                ReceiverTab.IsEnabled = true;
                            }
                            ));
                    }
                    catch
                    { Status.ChangeStatus("Failed to send"); }
                });
        }

        private void OpenGSAFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                SenderTab.IsEnabled = false;
                ReceiverTab.IsEnabled = false;
                Task.Run(() => GSA.OpenFile(openFileDialog.FileName, EmailAddress, RestApi)).ContinueWith(
                    delegate
                    {
                        try
                        {
                            Application.Current.Dispatcher.BeginInvoke(
                                DispatcherPriority.Background,
                                new Action(() =>
                                {
                                    UpdateClientLists();
                                    SenderTab.IsEnabled = true;
                                    ReceiverTab.IsEnabled = true;
                                }
                                ));
                        }
                        catch
                        { Status.ChangeStatus("Failed to send"); }
                    });
            }
        }
        #endregion

        #region Sender
        private void SendAnalysisLayer(object sender, RoutedEventArgs e)
        {
            GSA.TargetAnalysisLayer = true;
            GSA.TargetDesignLayer = false;

            SendStream(sender, e);
        }

        private void SendDesignLayer(object sender, RoutedEventArgs e)
        {
            GSA.TargetAnalysisLayer = false;
            GSA.TargetDesignLayer = true;

            SendStream(sender, e);
        }

        private void SendStream(object sender, RoutedEventArgs e)
        {
            if (RestApi == null && ApiToken == null)
            {
                Status.AddError("Not logged in");
                return;
            }

            GSA.GetSpeckleClients(EmailAddress, RestApi);
            UpdateClientLists();

            Task.Run(() => controller.ExportObjects(RestApi, ApiToken)).ContinueWith(
            delegate {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            GSA.SetSpeckleClients(EmailAddress, RestApi);

                            UpdateClientLists();
                        }
                        ));
                }
                catch
                { Status.ChangeStatus("Failed to send"); }
            });
        }
        #endregion

        #region Receiver
        private void AddReceiver(object sender, RoutedEventArgs e)
        {
            if (ReceiverTextbox.Text != "")
            { 
                GSA.Receivers.Add(ReceiverTextbox.Text);
                GSA.SetSpeckleClients(EmailAddress, RestApi);
                UpdateClientLists();

                ReceiverTextbox.Clear();
            }
        }

        private void ReceiveAnalysisLayer(object sender, RoutedEventArgs e)
        {
            GSA.TargetAnalysisLayer = true;
            GSA.TargetDesignLayer = false;

            ReceiveStream(sender, e);
        }

        private void ReceiveDesignLayer(object sender, RoutedEventArgs e)
        {
            GSA.TargetAnalysisLayer = false;
            GSA.TargetDesignLayer = true;

            ReceiveStream(sender, e);
        }

        private void ReceiveStream(object sender, RoutedEventArgs e)
        {
            if (RestApi == null && ApiToken == null)
            {
                Status.AddError("Not logged in");
                return;
            }

            GSA.GetSpeckleClients(EmailAddress, RestApi);
            UpdateClientLists();

            Task.Run(() => controller.ImportObjects(RestApi, ApiToken)).ContinueWith(
            delegate {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            UpdateClientLists();
                        }
                        ));
                }
                catch
                { Status.ChangeStatus("Failed to receive"); }
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
        private void UpdateClientLists()
        {
            SenderStreams.Items.Clear();
            ReceiverStreams.Items.Clear();

            if (GSA.Senders != null)
                foreach (KeyValuePair<string, string> sender in GSA.Senders)
                    SenderStreams.Items.Add(new Tuple<string,string>(sender.Key, sender.Value));

            if (GSA.Receivers != null)
                foreach (string receiver in GSA.Receivers)
                    ReceiverStreams.Items.Add(receiver);
        }

        private void CopyStreamList(object sender, DataGridRowClipboardEventArgs e)
        {
            if (e.ClipboardRowContent.Count() > 1)
                e.ClipboardRowContent.RemoveAt(0);
        }

        private void UpdateSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                string propertyName = "";
                object propertyValue = null;

                if (sender is CheckBox)
                {
                    propertyName = (sender as CheckBox).Name;
                    propertyValue = (sender as CheckBox).IsChecked;
                    typeof(Settings).GetField(propertyName).SetValue(null, propertyValue);
                }
            }
            catch { }
        }

        private void StreamList_ViewStream(object sender, RoutedEventArgs e)
        {
            var cell = StreamList.CurrentCell.Item;

            if (cell.GetType() == typeof(Tuple<string,string>))
            {
                string streamID = (cell as Tuple<string, string>).Item2;
                System.Diagnostics.Process.Start(RestApi.Replace("api/v1", "view") + @"/?streams=" + streamID);
            }
        }

        private void StreamList_ViewStreamData(object sender, RoutedEventArgs e)
        {
            var cell = StreamList.CurrentCell.Item;

            if (cell.GetType() == typeof(Tuple<string, string>))
            {
                string streamID = (cell as Tuple<string, string>).Item2;
                System.Diagnostics.Process.Start(RestApi + @"/streams/" + streamID);
            }
        }
    
        private void StreamList_ViewObjectData(object sender, RoutedEventArgs e)
        {
            var cell = StreamList.CurrentCell.Item;

            if (cell.GetType() == typeof(Tuple<string, string>))
            {
                string streamID = (cell as Tuple<string, string>).Item2;
                System.Diagnostics.Process.Start(RestApi + @"/streams/" + streamID + @"/objects?omit=displayValue,base64");
            }
        }

        private void SenderStreams_ViewStream(object sender, RoutedEventArgs e)
        {
            var cell = SenderStreams.CurrentCell.Item;

            if (cell.GetType() == typeof(Tuple<string, string>))
            {
                string streamID = (cell as Tuple<string, string>).Item2;
                System.Diagnostics.Process.Start(RestApi.Replace("api/v1", "view") + @"/?streams=" + streamID);
            }
        }

        private void SenderStreams_ViewStreamData(object sender, RoutedEventArgs e)
        {
            var cell = SenderStreams.CurrentCell.Item;

            if (cell.GetType() == typeof(Tuple<string, string>))
            {
                string streamID = (cell as Tuple<string, string>).Item2;
                System.Diagnostics.Process.Start(RestApi + @"/streams/" + streamID);
            }
        }

        private void SenderStreams_ViewObjectData(object sender, RoutedEventArgs e)
        {
            var cell = SenderStreams.CurrentCell.Item;

            if (cell.GetType() == typeof(Tuple<string, string>))
            {
                string streamID = (cell as Tuple<string, string>).Item2;
                System.Diagnostics.Process.Start(RestApi + @"/streams/" + streamID + @"/objects?omit=displayValue,base64");
            }
        }

        private void SenderStreams_CloneStreams(object sender, RoutedEventArgs e)
        {
            if (RestApi == null && ApiToken == null)
            {
                Status.AddError("Not logged in");
                return;
            }

            var cell = SenderStreams.CurrentCell.Item;

            if (cell.GetType() == typeof(Tuple<string, string>))
            {
                string streamID = (cell as Tuple<string, string>).Item2;
                
                Task.Run(() => StreamManager.CloneStream(RestApi, ApiToken, streamID)).ContinueWith(res =>
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            try
                            {
                                Status.AddMessage("Cloned to: " + res.Result);
                            }
                            catch { Status.AddError("Could not clone " + streamID); }
                        }
                        ));
                });
            }
        }

        private void ReceiverStreams_ViewStream(object sender, RoutedEventArgs e)
        {
            var streamID = ReceiverStreams.CurrentCell.Item;

            if (streamID.GetType() == typeof(string))
            {
                System.Diagnostics.Process.Start(RestApi.Replace("api/v1", "view") + @"/?streams=" + (string)streamID);
            }
        }

        private void ReceiverStreams_ViewStreamData(object sender, RoutedEventArgs e)
        {
            var streamID = ReceiverStreams.CurrentCell.Item;

            if (streamID.GetType() == typeof(string))
            {
                System.Diagnostics.Process.Start(RestApi + @"/streams/" + (string)streamID);
            }
        }

        private void ReceiverStreams_ViewObjectData(object sender, RoutedEventArgs e)
        {
            var streamID = ReceiverStreams.CurrentCell.Item;

            if (streamID.GetType() == typeof(string))
            {
                System.Diagnostics.Process.Start(RestApi + @"/streams/" + (string)streamID + @"/objects?omit=displayValue,base64");
            }
        }

        private void ReceiverStreams_RemoveStream(object sender, RoutedEventArgs e)
        {
            var streamID = ReceiverStreams.CurrentCell.Item;

            if (streamID.GetType() == typeof(string))
            {
                GSA.Receivers.Remove((string)streamID);
                GSA.SetSpeckleClients(EmailAddress, RestApi);
                UpdateClientLists();
            }
        }
        #endregion

    }
}
