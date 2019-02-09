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
        public string RestApi;
        public string ApiToken;
        public Controller controller;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
            
            Messages = new ObservableCollection<string>();
            StreamData = new ObservableCollection<Tuple<string, string>>();

            ModelName = "";
            controller = new Controller();
            
            //Default settings
            SendOnlyMeaningfulNodes.IsChecked = Settings.SendOnlyMeaningfulNodes;
            Merge1DElementsIntoPolyline.IsChecked = Settings.Merge1DElementsIntoPolyline;
            Merge2DElementsIntoMesh.IsChecked = Settings.Merge2DElementsIntoMesh;
            
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

            RestApi = p.restApi;
            ApiToken = p.apitoken;

            if (RestApi != null && ApiToken != null)
                Status.AddMessage("Logged in to " + p.selectedEmail);
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
            }
            );
        }
        #endregion

        #region GSA
        private void LinkGSA(object sender, RoutedEventArgs e)
        {
            GSA.Init();
        }

        private void NewGSAFile(object sender, RoutedEventArgs e)
        {
            Task.Run(() => GSA.NewFile());
        }

        private void OpenGSAFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                Task.Run(() => GSA.OpenFile(openFileDialog.FileName));
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

            Task.Run(() => controller.ExportObjects(RestApi, ApiToken, ModelName)).ContinueWith(
            delegate {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            SenderStreams.Items.Clear();

                            List<Tuple<string, string>> streams = controller.GetSenderStreams();
                            foreach (Tuple<string, string> stream in streams)
                                SenderStreams.Items.Add(stream);
                        }
                        ));
                }
                catch
                { Status.ChangeStatus("Failed to send"); }
            });
        }
        #endregion

        #region Receiver
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

            string streamInput = new TextRange(ReceiverStreams.Document.ContentStart, ReceiverStreams.Document.ContentEnd).Text;
            if (streamInput == null)
                return;

            string[] streams = streamInput.Split(new string[] { "\r", "\n", "," }, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, string> streamDict = new Dictionary<string, string>();

            for (int i = 0; i < streams.Length; i++)
                streamDict.Add(i.ToString(), streams[i]);

            Task.Run(() => controller.ImportObjects(RestApi, ApiToken, streamDict));
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
        #endregion

    }
}
