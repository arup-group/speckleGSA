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

        public bool SendDesignLayer { get; set; }
        public bool SendAnalysisLayer { get; set; }
        public string ReceiveNodeTolerance { get; set; }
        public string StreamName { get; set; }
        public ObservableCollection<string> Messages { get; set; }
        
        public UserManager userManager;
        public Sender speckleSender;
        public Receiver speckleReceiver;
        public GSAController gsa;

        private Timer TimerTrigger;

        public MainWindow()
        {
            InitializeComponent();

            //For testing purposes
            ServerAddress.Text = "https://hestia.speckle.works/api/v1";
            EmailAddress.Text = "mishael.nuh@arup.com";
            Password.Password = "temporaryPassword";

            SendDesignLayer = false;
            SendAnalysisLayer = false;
            ReceiveNodeTolerance = "0";
            StreamName = "";
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
        #endregion

        #region GSA
        private void LinkGSA(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                AddError("Please log in");
                return;
            }

            gsa = new GSAController();
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
        private async void SenderOn(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                ToggleSender.IsChecked = false;
                AddError("Login to server first");
                return;
            }

            speckleSender = new Sender(userManager.ServerAddress, userManager.ApiToken);

            AddMessage("Initializing sender");
            await speckleSender.InitializeSender();
            SenderStreamID.Text = speckleSender.StreamID;

            TimerTrigger = new Timer(UPDATE_INTERVAL) { AutoReset = false, Enabled = false };
            TimerTrigger.Elapsed += TriggerSend;
            TimerTrigger.Start();
            AddMessage("Start sending");
        }

        private void SenderOff(object sender, RoutedEventArgs e)
        {
            if (userManager != null && TimerTrigger != null)
            {
                TimerTrigger.Stop();
                AddMessage("Stop sending");
            }
        }

        private void TriggerSend(object sender, ElapsedEventArgs e)
        {
            gsa.SendDesignLayer = SendDesignLayer;
            if (gsa.SendDesignLayer)
                AddMessage("Sending design layer");

            gsa.SendAnalysisLayer = SendAnalysisLayer;
            if (gsa.SendAnalysisLayer)
                AddMessage("Sending analysis layer");

            try
            {
                AddMessage(speckleSender.UpdateData(gsa, StreamName));
            }
            catch (Exception ex)
            {
                AddError(ex.Message);
            }

            TimerTrigger.Stop();
            TimerTrigger.Start();
        }
        #endregion

        #region Receiver
        private async void ReceiverOn(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                ToggleReceiver.IsChecked = false;
                AddError("Login to server first");
                return;
            }

            speckleReceiver = new Receiver(userManager.ServerAddress, userManager.ApiToken);
            AddMessage("Initializing receiver");
            await speckleReceiver.InitializeReceiver(ReceiverStreamID.Text);

            TimerTrigger = new Timer(UPDATE_INTERVAL) { AutoReset = false, Enabled = false };
            TimerTrigger.Elapsed += TriggerReceive;
            TimerTrigger.Start();
            AddMessage("Start receiving");
        }

        private void ReceiverOff(object sender, RoutedEventArgs e)
        {
            if (userManager != null && TimerTrigger != null)
            {
                TimerTrigger.Stop();
                AddMessage("Stop receiving");
            }
        }
        
        private void TriggerReceive(object sender, ElapsedEventArgs e)
        {
            double tolerance = 0;
            if (!double.TryParse(ReceiveNodeTolerance, out tolerance))
            {
                AddError("Could not parse tolerance to number.");
            }
            AddMessage("Using tolerance of " + tolerance.ToString());
            gsa.ReceiveNodeTolerance = tolerance;

            try
            {
                AddMessage(speckleReceiver.UpdateData(gsa));
                StreamName = speckleReceiver.StreamName;
            }
            catch (Exception ex)
            {
                AddError(ex.Message);
            }

            TimerTrigger.Stop();
            TimerTrigger.Start();
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
