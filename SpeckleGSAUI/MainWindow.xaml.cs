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
using System.Collections;

namespace SpeckleGSAUI
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    const string PLAY_BUTTON = "M10,16.5V7.5L16,12M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";
    const string PAUSE_BUTTON = "M15,16H13V8H15M11,16H9V8H11M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";

    public ObservableCollection<string> Messages { get; set; }

    private UIStatus status;
    enum UIStatus
    {
      SENDING, RECEIVING, IDLE, BUSY
    };

    public string EmailAddress;
    public string RestApi;
    public string ApiToken;

    public Sender gsaSender;
    public Receiver gsaReceiver;

    public Timer triggerTimer;

    private int previousTabIndex;

    public MainWindow()
    {
      InitializeComponent();

      DataContext = this;

      Messages = new ObservableCollection<string>();

      gsaSender = new Sender();
      gsaReceiver = new Receiver();

      triggerTimer = new Timer();
      status = UIStatus.IDLE;
      previousTabIndex = 0;

      //Default settings
      SendOnlyMeaningfulNodes.IsChecked = Settings.SendOnlyMeaningfulNodes;
      SeperateStreams.IsChecked = Settings.SeperateStreams;
      PollingRate.Text = Settings.PollingRate.ToString();
      CoincidentNodeAllowance.Text = Settings.CoincidentNodeAllowance.ToString();
      ResultCases.Text = string.Join("\r\n", Settings.ResultCases);
      ResultInLocalAxis.IsChecked = Settings.ResultInLocalAxis;

      //Draw buttons
      SendButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
      SendButtonPath.Fill = Brushes.LightGray;
      ReceiveButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
      ReceiveButtonPath.Fill = Brushes.LightGray;

      GSA.Init();
      Status.Init(this.AddMessage, this.AddError, this.ChangeStatus);
      MessagePane.ItemsSource = Messages;
    }

    #region Speckle Operations
    /// <summary>
    /// Login to a SpeckleServer
    /// </summary>
    private void Login(object sender, RoutedEventArgs e)
    {
      SpecklePopup.MainWindow p = new SpecklePopup.MainWindow(false, true);

      this.IsEnabled = false;
      Brush oldBackground = this.Background;
      this.Background = new SolidColorBrush(Color.FromArgb(255, 81, 140, 255));

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

    /// <summary>
    /// Receive all streams in the account.
    /// </summary>
    private void UpdateStreamList(object sender, RoutedEventArgs e)
    {
      if (RestApi == null && ApiToken == null)
      {
        Status.AddError("Not logged in");
        return;
      }

      Task.Run(() => SpeckleStreamManager.GetStreams(RestApi, ApiToken)).ContinueWith(res =>
      {
        Application.Current.Dispatcher.BeginInvoke(
                  DispatcherPriority.Background,
                  new Action(() =>
                  {
                List<Tuple<string, string>> streams = res.Result;
                if (streams != null)
                {
                  streams.Reverse();
                  StreamList.Items.Clear();
                  foreach (Tuple<string, string> t in streams)
                    StreamList.Items.Add(t);
                }
              }
                  ));
      });
    }
    #endregion

    #region GSA
    /// <summary>
    /// Create new GSA file.
    /// </summary>
    private void NewGSAFile(object sender, RoutedEventArgs e)
    {
      SenderTab.IsEnabled = false;
      ReceiverTab.IsEnabled = false;
      Status.ChangeStatus("Opening New File");
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
                        Status.ChangeStatus("Ready", 0);
                      }
                        ));
            }
            catch
            { Status.ChangeStatus("Failed to create file", 0); }
          });
    }

    /// <summary>
    /// Open a GSA file.
    /// </summary>
    private void OpenGSAFile(object sender, RoutedEventArgs e)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      if (openFileDialog.ShowDialog() == true)
      {
        SenderTab.IsEnabled = false;
        ReceiverTab.IsEnabled = false;
        Status.ChangeStatus("Opening File");
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
                          Status.ChangeStatus("Ready", 0);
                        }
                            ));
              }
              catch
              { Status.ChangeStatus("Failed to open file", 0); }
            });
      }
    }
    #endregion

    #region Sender
    /// <summary>
    /// Start and stop sending.
    /// </summary>
    private async void SendStream(object sender, RoutedEventArgs e)
    {
      if (RestApi == null && ApiToken == null)
      {
        Status.AddError("Not logged in");
        return;
      }

      if (status == UIStatus.IDLE)
      {
        status = UIStatus.BUSY;
        SendButtonPath.Data = Geometry.Parse(PAUSE_BUTTON);
        SendButtonPath.Fill = Brushes.DimGray;

        if (SenderResultToggle.IsChecked.Value && !SenderLayerToggle.IsChecked.Value)
          MessageBox.Show("Results only supported for analysis layer.", "SpeckleGSA", MessageBoxButton.OK, MessageBoxImage.Warning);

        if (SenderResultToggle.IsChecked.Value && !SenderContinuousToggle.IsChecked.Value)
          MessageBox.Show("Results only supported for single send mode.", "SpeckleGSA", MessageBoxButton.OK, MessageBoxImage.Warning);

        if (SenderLayerToggle.IsChecked.Value)
        {
          Settings.TargetAnalysisLayer = true;
          Settings.TargetDesignLayer = false;
          if (SenderContinuousToggle.IsChecked.Value)
            Settings.SendResults = SenderResultToggle.IsChecked.Value;
          else
            Settings.SendResults = false;
        }
        else
        {
          Settings.TargetAnalysisLayer = false;
          Settings.TargetDesignLayer = true;
          Settings.SendResults = false;
        }
        SenderLayerToggle.IsEnabled = false;
        SenderContinuousToggle.IsEnabled = false;
        SenderResultToggle.IsEnabled = false;

        GSA.GetSpeckleClients(EmailAddress, RestApi);
        gsaSender = new Sender();
        await gsaSender.Initialize(RestApi, ApiToken);
        GSA.SetSpeckleClients(EmailAddress, RestApi);

        if (SenderContinuousToggle.IsChecked.Value)
        {
          await Task.Run(() => gsaSender.Trigger())
              .ContinueWith(res =>
              {
                Application.Current.Dispatcher.BeginInvoke(
                              DispatcherPriority.Background,
                              new Action(() =>
                              {
                        UpdateClientLists();
                        status = UIStatus.IDLE;
                        SendButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
                        SendButtonPath.Fill = Brushes.LightGray;

                        SenderLayerToggle.IsEnabled = true;
                        SenderContinuousToggle.IsEnabled = true;
                        SenderResultToggle.IsEnabled = true;
                      })
                          );
              });
        }
        else
        {
          triggerTimer = new Timer(Settings.PollingRate);
          triggerTimer.Elapsed += SenderTimerTrigger;
          triggerTimer.AutoReset = false;
          status = UIStatus.SENDING;
          triggerTimer.Start();

          SendButtonPath.Fill = (SolidColorBrush)(new BrushConverter().ConvertFrom("#0080ff"));
        }
      }
      else if (status == UIStatus.SENDING)
      {
        gsaSender.Dispose();
        status = UIStatus.IDLE;
        SendButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
        SendButtonPath.Fill = Brushes.LightGray;

        SenderLayerToggle.IsEnabled = true;
        SenderContinuousToggle.IsEnabled = true;
        SenderResultToggle.IsEnabled = true;
      }
    }

    /// <summary>
    /// Trigger event for sending stream.
    /// </summary>
    private void SenderTimerTrigger(Object source, ElapsedEventArgs e)
    {
      gsaSender.Trigger();
      Application.Current.Dispatcher.BeginInvoke(
          DispatcherPriority.Background,
          new Action(() => UpdateClientLists()
          )
      );

      if (status == UIStatus.SENDING)
        triggerTimer.Start();
    }
    #endregion

    #region Receiver
    /// <summary>
    /// Add a new receiver.
    /// </summary>
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

    /// <summary>
    /// Add receivers from clipboard.
    /// </summary>
    private void PasteClipboardReceiver(object sender, RoutedEventArgs e)
    {
      string[] paste = Clipboard.GetText(TextDataFormat.Text).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

      foreach (string p in paste)
        GSA.Receivers.Add(p);

      GSA.SetSpeckleClients(EmailAddress, RestApi);
      UpdateClientLists();
    }

    /// <summary>
    /// Clear all receivers.
    /// </summary>
    private void ClearReceiver(object sender, RoutedEventArgs e)
    {
      GSA.Receivers.Clear();
      GSA.SetSpeckleClients(EmailAddress, RestApi);

      UpdateClientLists();
    }

    /// <summary>
    /// Start and stop receiving.
    /// </summary>
    private async void ReceiveStream(object sender, RoutedEventArgs e)
    {
      if (RestApi == null && ApiToken == null)
      {
        Status.AddError("Not logged in");
        return;
      }

      if (status == UIStatus.IDLE)
      {
        status = UIStatus.BUSY;
        ReceiveButtonPath.Data = Geometry.Parse(PAUSE_BUTTON);
        ReceiveButtonPath.Fill = Brushes.DimGray;

        if (ReceiverLayerToggle.IsChecked.Value)
        {
          Settings.TargetAnalysisLayer = true;
          Settings.TargetDesignLayer = false;
        }
        else
        {

          Settings.TargetAnalysisLayer = false;
          Settings.TargetDesignLayer = true;
        }
        ReceiverLayerToggle.IsEnabled = false;
        ReceiverContinuousToggle.IsEnabled = false;
        ReceiverControlPanel.IsEnabled = false;

        GSA.GetSpeckleClients(EmailAddress, RestApi);
        gsaReceiver = new Receiver();
        await gsaReceiver.Initialize(RestApi, ApiToken);
        GSA.SetSpeckleClients(EmailAddress, RestApi);
        status = UIStatus.RECEIVING;
        if (ReceiverContinuousToggle.IsChecked.Value)
        {
          await Task.Run(() => gsaReceiver.Trigger(null, null))
              .ContinueWith(res =>
              {
                Application.Current.Dispatcher.BeginInvoke(
                              DispatcherPriority.Background,
                              new Action(() =>
                              {
                        ReceiveStream(sender, e);
                      })
                          );
              });
        }
        else
        {
          await Task.Run(() => gsaReceiver.Trigger(null, null));
          ReceiveButtonPath.Fill = (SolidColorBrush)(new BrushConverter().ConvertFrom("#0080ff"));
        }
      }
      else if (status == UIStatus.RECEIVING)
      {
        status = UIStatus.IDLE;
        ReceiveButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
        ReceiveButtonPath.Fill = Brushes.LightGray;

        ReceiverLayerToggle.IsEnabled = true;
        ReceiverContinuousToggle.IsEnabled = true;
        ReceiverControlPanel.IsEnabled = true;

        MessageBoxResult result = MessageBox.Show("Bake received objects permanently? ", "SpeckleGSA", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
          gsaReceiver.DeleteSpeckleObjects();

        gsaReceiver.Dispose();
      }
    }
    #endregion

    #region Log
    /// <summary>
    /// Message handler.
    /// </summary>
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

    /// <summary>
    /// Error message handler.
    /// </summary>
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

    /// <summary>
    /// Change status handler.
    /// </summary>
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
    /// <summary>
    /// Control UI tab changes.
    /// </summary>
    private void ChangeTab(object sender, SelectionChangedEventArgs e)
    {
      if (e.OriginalSource == UITabControl)
      {
        switch (status)
        {
          case UIStatus.BUSY:
            e.Handled = true;
            UITabControl.SelectedIndex = previousTabIndex;
            break;
          case UIStatus.SENDING:
            e.Handled = true;
            UITabControl.SelectedIndex = previousTabIndex;
            MessageBox.Show("Unable to switch tabs while sending");
            break;
          case UIStatus.RECEIVING:
            e.Handled = true;
            UITabControl.SelectedIndex = previousTabIndex;
            MessageBox.Show("Unable to switch tabs while receiving");
            break;
          default:
            previousTabIndex = UITabControl.SelectedIndex;
            break;
        }
      }
    }

    /// <summary>
    /// Update data grids with stream IDs from GSA file.
    /// </summary>
    private void UpdateClientLists()
    {
      SenderStreams.Items.Clear();
      ReceiverStreams.Items.Clear();

      if (GSA.Senders != null)
        foreach (KeyValuePair<string, string> sender in GSA.Senders)
          SenderStreams.Items.Add(new Tuple<string, string>(sender.Key, sender.Value));

      if (GSA.Receivers != null)
        foreach (string receiver in GSA.Receivers)
          ReceiverStreams.Items.Add(receiver);
    }

    /// <summary>
    /// Copy selected stream ID
    /// </summary>
    private void CopyStreamList(object sender, DataGridRowClipboardEventArgs e)
    {
      if (e.ClipboardRowContent.Count() > 1)
        e.ClipboardRowContent.RemoveAt(0);
    }

    /// <summary>
    /// Update stream in Settings.cs
    /// </summary>
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

          var fieldInfo = typeof(Settings).GetField(propertyName);
          Type fieldType = fieldInfo.FieldType;

          if (fieldType == typeof(bool))
          {
            fieldInfo.SetValue(null, propertyValue);
          }
        }
        else if (sender is TextBox)
        {
          propertyName = (sender as TextBox).Name;
          propertyValue = (sender as TextBox).Text;

          var fieldInfo = typeof(Settings).GetField(propertyName);
          Type fieldType = fieldInfo.FieldType;

          if (fieldType == typeof(string))
          {
            fieldInfo.SetValue(null, Convert.ChangeType(propertyValue, fieldType));
          }
          else if (typeof(IEnumerable).IsAssignableFrom(fieldType))
          {
            string[] pieces = ((string)propertyValue).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Type subType = fieldType.GetGenericArguments()[0];

            var newList = Activator.CreateInstance(fieldType);
            foreach (string p in pieces)
              newList.GetType().GetMethod("Add").Invoke(newList, new object[] { Convert.ChangeType(p, newList.GetType().GetGenericArguments().Single()) });

            fieldInfo.SetValue(null, newList);
          }
        }
      }
      catch
      { }
    }

    private void StreamList_CopyStreamID(object sender, RoutedEventArgs e)
    {
      var cell = StreamList.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        Clipboard.SetText((cell as Tuple<string, string>).Item2);
      }
    }

    private void StreamList_ViewStream(object sender, RoutedEventArgs e)
    {
      var cell = StreamList.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        string streamID = (cell as Tuple<string, string>).Item2;
        string url = RestApi.Split(new string[] { "api" }, StringSplitOptions.RemoveEmptyEntries)[0];
        System.Diagnostics.Process.Start(url + @"view/?streams=" + streamID);
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

    private void SenderStreams_CopyStreamID(object sender, RoutedEventArgs e)
    {
      var cell = SenderStreams.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        Clipboard.SetText((cell as Tuple<string, string>).Item2);
      }
    }

    private void SenderStreams_ViewStream(object sender, RoutedEventArgs e)
    {
      var cell = SenderStreams.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        string streamID = (cell as Tuple<string, string>).Item2;
        string url = RestApi.Split(new string[] { "api" }, StringSplitOptions.RemoveEmptyEntries)[0];
        System.Diagnostics.Process.Start(url + @"view/?streams=" + streamID);
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

        Task.Run(() => SpeckleStreamManager.CloneStream(RestApi, ApiToken, streamID)).ContinueWith(res =>
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

    private void ReceiverStreams_CopyStreamID(object sender, RoutedEventArgs e)
    {
      var cell = ReceiverStreams.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        Clipboard.SetText((cell as Tuple<string, string>).Item2);
      }
    }

    private void ReceiverStreams_ViewStream(object sender, RoutedEventArgs e)
    {
      var streamID = ReceiverStreams.CurrentCell.Item;

      if (streamID.GetType() == typeof(string))
      {
        string url = RestApi.Split(new string[] { "api" }, StringSplitOptions.RemoveEmptyEntries)[0];
        System.Diagnostics.Process.Start(url + @"view/?streams=" + (string)streamID);
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
