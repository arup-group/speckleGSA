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
        
        public string ReceiveNodeTolerance { get; set; }
        public string StreamName { get; set; }
        public ObservableCollection<string> Messages { get; set; }
        
        public UserManager userManager;
        public Sender speckleSender;
        public Receiver speckleReceiver;
        public GSAController gsa;

      public MainWindow()
        {
            InitializeComponent();

            //For testing purposes
            ServerAddress.Text = "https://hestia.speckle.works/api/v1";
            EmailAddress.Text = "mishael.nuh@arup.com";
            Password.Password = "temporaryPassword";
            
            ReceiveNodeTolerance = "0";
            StreamName = "";
            Messages = new ObservableCollection<string>();

            DataContext = this;
            
            this.Topmost = true;
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
        private async void SendNewStream(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                AddError("Login to server first");
                return;
            }

            speckleSender = new Sender(userManager.ServerAddress, userManager.ApiToken);

            AddMessage("Initializing sender");
            await speckleSender.InitializeSender();
            SenderStreamID.Text = speckleSender.StreamID;

            SendUpdateStream(sender, e);
        }

        private void SendUpdateStream(object sender, RoutedEventArgs e)
        {
            if (speckleSender==null)
            {
                AddError("Create new stream first");
                return;
            }
            
            //try
            //{
                AddMessage(speckleSender.UpdateData(gsa, StreamName));
            //}
            //catch (Exception ex)
            //{
            //    AddError(ex.Message);
            //}
        }
        #endregion

        #region Receiver
        private async void ReceiveNewStream(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                AddError("Login to server first");
                return;
            }

            speckleReceiver = new Receiver(userManager.ServerAddress, userManager.ApiToken);
            AddMessage("Initializing receiver");
            await speckleReceiver.InitializeReceiver(ReceiverStreamID.Text);

            ReceiveUpdateStream(sender, e);
        }

        private void ReceiveUpdateStream(object sender, RoutedEventArgs e)
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
