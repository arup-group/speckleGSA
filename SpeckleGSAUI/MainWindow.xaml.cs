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

namespace SpeckleGSAUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int UPDATE_INTERVAL = 2000;

        public UserManager userManager;
        public Sender speckleSender;
        public Receiver speckleReceiver;
        public GSAController gsa;

        private Timer TimerTrigger;
        private string StreamName;

        public MainWindow()
        {
            InitializeComponent();

            //For testing purposes
            ServerAddress.Text = "https://hestia.speckle.works/api/v1";
            EmailAddress.Text = "mishael.nuh@arup.com";
            Password.Password = "temporaryPassword";
        }

        private void Login(object sender, RoutedEventArgs e)
        {
            userManager = new UserManager(EmailAddress.Text, Password.Password, ServerAddress.Text);
            userManager.Login();
            MessageBox.Show("Successfuly logged in");
        }

        private void LinkGSA(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                MessageBox.Show("Login first");
                return;
            }

            gsa = new GSAController();
            MessageBox.Show("Successfuly linked to GSA");
        }

        private async void SenderOn(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                MessageBox.Show("Login first");
                ToggleSender.IsChecked = false;
                return;
            }

            speckleSender = new Sender(userManager.ServerAddress, userManager.ApiToken);
            await speckleSender.InitializeSender();
            SenderStreamID.Text = speckleSender.StreamID;

            TimerTrigger = new System.Timers.Timer(UPDATE_INTERVAL) { AutoReset = true, Enabled = false };
            TimerTrigger.Elapsed += TriggerSend;
            TimerTrigger.Start();
        }

        private void SenderOff(object sender, RoutedEventArgs e)
        {
            if (userManager != null)
            {
                TimerTrigger.Stop();
            }
        }

        private void TriggerSend(object sender, ElapsedEventArgs e)
        {
            gsa.GetNodes();
            gsa.GetElements();
            speckleSender.UpdateData(StreamName, gsa);
        }

        private void UpdateStreamName(object sender, TextChangedEventArgs e)
        {
            StreamName = ((TextBox)sender).Text;
        }

        private async void ReceiverOn(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                MessageBox.Show("Login first");
                ToggleReceiver.IsChecked = false;
                return;
            }

            speckleReceiver = new Receiver(userManager.ServerAddress, userManager.ApiToken);
            await speckleReceiver.InitializeReceiver(ReceiverStreamID.Text);
            ReceiverStreamName.Text = speckleReceiver.StreamName;

            TimerTrigger = new System.Timers.Timer(UPDATE_INTERVAL) { AutoReset = true, Enabled = false };
            TimerTrigger.Elapsed += TriggerReceive;
            TimerTrigger.Start();
        }

        private void ReceiverOff(object sender, RoutedEventArgs e)
        {
            if (userManager != null)
            {
                TimerTrigger.Stop();
            }
        }


        private void TriggerReceive(object sender, ElapsedEventArgs e)
        {
            speckleReceiver.UpdateGlobal(gsa);
        }

        private async void TestReceiver(object sender, RoutedEventArgs e)
        {
            if (userManager == null)
            {
                MessageBox.Show("Login first");
                return;
            }

            speckleReceiver = new Receiver(userManager.ServerAddress, userManager.ApiToken);
            await speckleReceiver.InitializeReceiver(ReceiverStreamID.Text);


            speckleReceiver.UpdateGlobal(gsa);
        }
    }
}
