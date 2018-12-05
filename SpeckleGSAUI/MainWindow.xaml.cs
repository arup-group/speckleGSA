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
        }

        #region Server
        private void Login(object sender, RoutedEventArgs e)
        {
            userManager = new UserManager(EmailAddress.Text, Password.Password, ServerAddress.Text);
            if (userManager.Login()==0)
                AddMessage("Successfully logged in");
            else
                AddError("Failed to login");
        }

        private void UpdateStreamList(object sender, RoutedEventArgs e)
        {
            StreamManager manager = new StreamManager(userManager.ServerAddress, userManager.ApiToken);
            List<Tuple<string, string>> streamData = manager.GetStreams();
            streamData.Reverse();

            StreamData.Clear();
            foreach (Tuple<string, string> t in streamData)
                StreamData.Add(t);
        }
        #endregion

        #region GSA
        private void LinkGSA(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                AddError("Please log in");
                return;
            }

            gsa = new GSAController(userManager);
            AddMessage("Linked to GSA");
        }

        private void NewGSAFile(object sender, RoutedEventArgs e)
        {
            if (gsa == null)
            {
                AddError("GSA link not found");
                return;
            }
            gsa.NewFile();
            AddMessage("New GSA file created");
        }

        private void OpenGSAFile(object sender, RoutedEventArgs e)
        {
            if (gsa == null)
            {
                AddError("GSA link not found");
                return;
            }
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                gsa.OpenFile(openFileDialog.FileName);
                AddMessage("Opened " + openFileDialog.FileName);
            }
            else
                AddMessage("Cancel");
        }
        #endregion

        #region Sender
        private async void SendStream(object sender, RoutedEventArgs e)
        {
            if (gsa == null)
            {
                AddError("Link to GSA first");
                return;
            }

            AddMessage("Initializing sender");
            await gsa.ExportObjects(ProjectName);
            AddMessage("Finished sending");

            SenderNodeStreamID.Text = gsa.SenderNodeStreamID;
            SenderSectionStreamID.Text = gsa.SenderSectionStreamID;
            SenderElementStreamID.Text = gsa.SenderElementStreamID;
        }
        #endregion

        #region Receiver
        private async void ReceiveStream(object sender, RoutedEventArgs e)
        {
            if (gsa == null)
            {
                AddError("Link to GSA first");
                return;
            }
            
            AddMessage("Initializing receiver");
            await gsa.ImportObjects(new Dictionary<string, string>()
            {
                { "Nodes", ReceiverNodeStreamID },
                { "Elements", ReceiverElementStreamID },
            });
            AddMessage("Finished receiving");
        }
        #endregion

        #region Log
        private void AddMessage(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => 
                    Messages.Add("[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + message)));

            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => MessagePane.ScrollIntoView(MessagePane.Items[MessagePane.Items.Count - 1])));
        }

        private void AddError(string error)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => Messages.Add("[" + DateTime.Now.ToString("h:mm:ss tt") + "] ERROR: " + error)));

            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => MessagePane.ScrollIntoView(MessagePane.Items[MessagePane.Items.Count - 1])));
        }
        #endregion
    }
}
