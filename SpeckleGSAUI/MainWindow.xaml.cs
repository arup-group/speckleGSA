using Microsoft.Win32;
using SpeckleGSA;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Deployment.Application;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SpeckleGSAProxy;
using Serilog;
using Serilog.Events;

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

    private bool FileOpened;
    private bool LoggedIn => (!string.IsNullOrEmpty(EmailAddress) && !string.IsNullOrEmpty(RestApi));

    public MainWindow()
    {
      InitializeComponent();

      mainWindow.Title = mainWindow.Title + " - " + getRunningVersion();

      DataContext = this;

      Messages = new ObservableCollection<string>();

      gsaSender = new Sender();
      gsaReceiver = new Receiver();

      triggerTimer = new Timer();
      status = UIStatus.IDLE;
      previousTabIndex = 0;

      //Default settings
      SendOnlyMeaningfulNodes.IsChecked = GSA.Settings.SendOnlyMeaningfulNodes;
      SeparateStreams.IsChecked = GSA.Settings.SeparateStreams;
      PollingRate.Text = GSA.Settings.PollingRate.ToString();
      CoincidentNodeAllowance.Text = GSA.Settings.CoincidentNodeAllowance.ToString();
      SendOnlyResults.IsChecked = GSA.Settings.SendOnlyResults;
      EmbedResults.IsChecked = GSA.Settings.EmbedResults;
      ResultCases.Text = string.Join("\r\n", GSA.Settings.ResultCases);
      ResultInLocalAxis.IsChecked = GSA.Settings.ResultInLocalAxis;
      Result1DNumPosition.Text = GSA.Settings.Result1DNumPosition.ToString();

      //Result List
      foreach (string s in Result.NodalResultMap.Keys)
      {
        CheckBox chk = new CheckBox
        {
          Content = s,
          Tag = Result.NodalResultMap[s]
        };
        chk.Checked += UpdateNodalResult;
        chk.Unchecked += UpdateNodalResult;
        ResultSelection.Children.Add(chk);
      }

      foreach (string s in Result.Element1DResultMap.Keys)
      {
        CheckBox chk = new CheckBox
        {
          Content = s,
          Tag = Result.Element1DResultMap[s]
        };
        chk.Checked += UpdateElement1DResult;
        chk.Unchecked += UpdateElement1DResult;
        ResultSelection.Children.Add(chk);
      }

      foreach (string s in Result.Element2DResultMap.Keys)
      {
        CheckBox chk = new CheckBox
        {
          Content = s,
          Tag = Result.Element2DResultMap[s]
        };
        chk.Checked += UpdateElement2DResult;
        chk.Unchecked += UpdateElement2DResult;
        ResultSelection.Children.Add(chk);
      }

      foreach (string s in Result.MiscResultMap.Keys)
      {
        CheckBox chk = new CheckBox
        {
          Content = s,
          Tag = Result.MiscResultMap[s]
        };
        chk.Checked += UpdateMiscResult;
        chk.Unchecked += UpdateMiscResult;
        ResultSelection.Children.Add(chk);
      }

      //Draw buttons
      SendButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
      SendButtonPath.Fill = (SolidColorBrush)FindResource("PrimaryHueMidBrush");
      ReceiveButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
      ReceiveButtonPath.Fill = (SolidColorBrush)FindResource("PrimaryHueMidBrush");

      //Adds event handling delegates to the status events
      Status.Init(this.AddMessage, this.AddError, this.ChangeStatus);
      GSA.Init();

      //Add further event handling delegates - this time for logging - to the status events
      Status.MessageAdded += (sender, eventArgs) => { Log.Information(eventArgs.Message); };
      Status.ErrorAdded += (sender, eventArgs) => 
      {
        if (eventArgs.Exception == null)
        {
          Log.Error(eventArgs.Message);
        }
        else
        {
          Log.Error(eventArgs.Exception, eventArgs.Message);
          if (eventArgs.Exception.InnerException != null)
          {
            Log.Error(eventArgs.Exception.InnerException, eventArgs.Message);
          }
        }
      };

      MessagePane.ItemsSource = Messages;

      SpeckleCore.SpeckleInitializer.Initialize();
      SpeckleCore.LocalContext.Init();

      try
      {
        //This will throw an exception if there is no default account
        var account = SpeckleCore.LocalContext.GetDefaultAccount();
        if (account != null)
        {
          EmailAddress = account.Email;
          RestApi = account.RestApi;
          ApiToken = account.Token;

          Status.AddMessage("Logged in to default account at: " + RestApi);
        }
      }
      catch
      {
        Status.AddMessage("No default account found - press the Login button to login/select an account");
      }
    }

    #region Speckle Operations
    /// <summary>
    /// Login to a SpeckleServer
    /// </summary>
    private void Login(object sender, RoutedEventArgs e)
    {
      var signInWindow = new SpecklePopup.SignInWindow(true);

      var helper = new System.Windows.Interop.WindowInteropHelper(signInWindow)
      {
        Owner = new System.Windows.Interop.WindowInteropHelper(this).Handle
      };

      this.IsEnabled = false;

      signInWindow.ShowDialog();

      this.IsEnabled = true;

      if (signInWindow.AccountListBox.SelectedIndex != -1)
      {
        var account = signInWindow.accounts[signInWindow.AccountListBox.SelectedIndex];

        EmailAddress = account.Email;
        RestApi = account.RestApi;
        ApiToken = account.Token;
        (SenderTab.Content as Grid).IsEnabled = FileOpened && LoggedIn;
        (ReceiverTab.Content as Grid).IsEnabled = FileOpened && LoggedIn;
        UpdateClientLists();

        Status.AddMessage("Logged in to account at: " + RestApi);
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
      (SenderTab.Content as Grid).IsEnabled = false;
      (ReceiverTab.Content as Grid).IsEnabled = false;
      Status.ChangeStatus("Opening New File");
      Task.Run(() => GSA.NewFile(EmailAddress, RestApi)).ContinueWith(
          delegate
          {
            try
            {
              FileOpened = true;
              Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                          UpdateClientLists();
                          (SenderTab.Content as Grid).IsEnabled = FileOpened && LoggedIn;
                          (ReceiverTab.Content as Grid).IsEnabled = FileOpened && LoggedIn;
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
        (SenderTab.Content as Grid).IsEnabled = false;
        (ReceiverTab.Content as Grid).IsEnabled = false;
        Status.ChangeStatus("Opening File");
        Task.Run(() => GSA.OpenFile(openFileDialog.FileName, EmailAddress, RestApi)).ContinueWith(
            delegate
            {
              try
              {
                FileOpened = true;
                Application.Current.Dispatcher.BeginInvoke(
                  DispatcherPriority.Background,
                  new Action(() =>
                  {
                    UpdateClientLists();
                    (SenderTab.Content as Grid).IsEnabled = FileOpened && LoggedIn;
                    (ReceiverTab.Content as Grid).IsEnabled = FileOpened && LoggedIn;
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
        if (GSA.Settings.NodalResults.Count > 0 || GSA.Settings.Element1DResults.Count > 0
          || GSA.Settings.Element2DResults.Count > 0 || GSA.Settings.MiscResults.Count > 0)
        {
          //SenderLayerToggle is a boolean toggle, where zero is design layer
          if (!SenderLayerToggle.IsChecked.Value)
          {
            var dialogResult = MessageBox.Show("Results only supported for analysis layer.\r\nNo results will be sent.  Do you still wish to proceed?",
              "SpeckleGSA", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (dialogResult == MessageBoxResult.No)
            {
              return;
            }
            GSA.Settings.SendResults = false;
          }
          else if (!SenderContinuousToggle.IsChecked.Value)
          {
            var dialogResult = MessageBox.Show("Results only supported for single send mode.\r\nNo results will be sent.  Do you still wish to proceed?",
              "SpeckleGSA", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (dialogResult == MessageBoxResult.No)
            {
              return;
            }
            GSA.Settings.SendResults = false;
          }
          else
          {
            GSA.Settings.SendResults = true;
          }
        }
        else
        {
          GSA.Settings.SendResults = false;
        }

        Status.AddMessage("Preparing to send ...");
        Application.Current.DoEvents();

        status = UIStatus.BUSY;
        SendButtonPath.Data = Geometry.Parse(PAUSE_BUTTON);
        SendButtonPath.Fill = Brushes.DimGray;

        if (SenderLayerToggle.IsChecked.Value)
        {
          GSA.Settings.TargetLayer = GSATargetLayer.Analysis;
        }
        else
        {
          GSA.Settings.TargetLayer = GSATargetLayer.Design;
        }
        SenderLayerToggle.IsEnabled = false;
        SenderContinuousToggle.IsEnabled = false;

        SenderButton.IsEnabled = false;
        Application.Current.DoEvents();

        try
        {
          if (!GSA.GetSpeckleClients(EmailAddress, RestApi))
          {
            Status.AddError("Error in communicating GSA - please check if the GSA file has been closed down");
            status = UIStatus.SENDING;
            SendStream(sender, e);
            return;
          }
          gsaSender = new Sender();
          var statusMessages = await gsaSender.Initialize(RestApi, ApiToken, (restApi, apiToken) => new SpeckleGSASender(restApi, apiToken));
          GSA.SetSpeckleClients(EmailAddress, RestApi);
          
        }
        catch (Exception ex)
        {
          Status.AddError(ex.Message);
          return;
        }

        var resultCases = GSA.Settings.ResultCases;

        if (GSA.Settings.SendResults && resultCases.Count() > 0)
        {
          var startTime = DateTime.Now;

          var expandedCases = GSA.gsaCache.ExpandLoadCasesAndCombinations(string.Join(" ", resultCases));

          if (!expandedCases.SequenceEqual(resultCases))
          {
            Status.AddMessage("Expanded list of load cases/combinations to be sent: " + string.Join(" ", expandedCases));

            GSA.Settings.ResultCases = expandedCases;

            TimeSpan duration = DateTime.Now - startTime;
            if (duration.Milliseconds > 100)
            {
              Status.AddMessage("Duration of expanding and validating load cases/combinations: " + duration.ToString(@"hh\:mm\:ss"));
            }
          }
        }

        status = UIStatus.SENDING;
        if (SenderContinuousToggle.IsChecked.Value)
        {
          try
          {
            await Task.Run(() => gsaSender.Trigger())
              .ContinueWith(res =>
              {
                Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() =>
                    {
                      UpdateClientLists();
                      SendStream(sender, e);
                    })
                );
              });
          }
          catch (Exception ex)
          {
            Status.AddError(ex.Message);
            //SendStream(sender, e);
          }
        }
        else
        {
          triggerTimer = new Timer(GSA.Settings.PollingRate);
          triggerTimer.Elapsed += SenderTimerTrigger;
          triggerTimer.AutoReset = false;
          triggerTimer.Start();

          SendButtonPath.Fill = (SolidColorBrush)FindResource("SecondaryAccentBrush");// (new BrushConverter().ConvertFrom("#0080ff"));
        }
      }
      else if (status == UIStatus.SENDING)
      {
        gsaSender.Dispose();
        status = UIStatus.IDLE;
        SendButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
        SendButtonPath.Fill = (SolidColorBrush)FindResource("PrimaryHueMidBrush");

        SenderLayerToggle.IsEnabled = true;
        SenderContinuousToggle.IsEnabled = true;
        SenderButton.IsEnabled = true;
      }
    }

    /// <summary>
    /// Trigger event for sending stream.
    /// </summary>
    private void SenderTimerTrigger(Object source, ElapsedEventArgs e)
    {
      try
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
      catch (Exception ex)
      {
        Status.AddError(ex.Message);
        //SendStream(null, null);
      }
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
        var streamId = ReceiverTextbox.Text.Trim();
        GSA.ReceiverInfo.Add(new Tuple<string, string>(streamId, null));
        if (!GSA.SetSpeckleClients(EmailAddress, RestApi))
        {
          Status.AddError("Error in communicating GSA - please check if the GSA file has been closed down");
          return;
        }
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
      {
        var streamId = p.Trim();
        GSA.ReceiverInfo.Add(new Tuple<string, string>(streamId, null));
      }
      if (!GSA.SetSpeckleClients(EmailAddress, RestApi))
      {
        Status.AddError("Error in communicating GSA - please check if the GSA file has been closed down");
        return;
      }
      UpdateClientLists();
    }

    /// <summary>
    /// Clear all receivers.
    /// </summary>
    private void ClearReceiver(object sender, RoutedEventArgs e)
    {
      GSA.ReceiverInfo.Clear();
      if (!GSA.SetSpeckleClients(EmailAddress, RestApi))
      {
        Status.AddError("Error in communicating GSA - please check if the GSA file has been closed down");
        return;
      }

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
        Status.AddMessage("Preparing to receive ...");
        Application.Current.DoEvents();

        status = UIStatus.BUSY;
        ReceiveButtonPath.Data = Geometry.Parse(PAUSE_BUTTON);
        ReceiveButtonPath.Fill = Brushes.DimGray;

        GSA.Settings.TargetLayer = (ReceiverLayerToggle.IsChecked.Value) ? GSATargetLayer.Analysis : GSA.Settings.TargetLayer = GSATargetLayer.Design;

        ReceiverLayerToggle.IsEnabled = false;
        ReceiverContinuousToggle.IsEnabled = false;
        ReceiverControlPanel.IsEnabled = false;
        ReceiveButton.IsEnabled = false;

        Application.Current.DoEvents();

        GSA.GetSpeckleClients(EmailAddress, RestApi);
        if (!GSA.SetSpeckleClients(EmailAddress, RestApi))
        {
          Status.AddError("Error in communicating GSA - please check if the GSA file has been closed down");
          status = UIStatus.RECEIVING;
          ReceiveStream(sender, e);
          return;
        }

        gsaReceiver = new Receiver();
        try
        {
          await Task.Run(() =>
           {
             var nonBlankReceivers = GSA.ReceiverInfo.Where(r => !string.IsNullOrEmpty(r.Item1)).ToList();

             foreach (var streamInfo in nonBlankReceivers)
             {
               Status.AddMessage("Creating receiver " + streamInfo.Item1);
               gsaReceiver.Receivers[streamInfo.Item1] = new SpeckleGSAReceiver(RestApi, ApiToken);
             }
           });
          await gsaReceiver.Initialize(RestApi, ApiToken);
        }
        catch (Exception ex)
        {
          Status.AddError(ex.Message);
          return;
        }

        status = UIStatus.RECEIVING;
        if (ReceiverContinuousToggle.IsChecked.Value)
        {
          try
          {
            await Task.Run(() =>
            {
              gsaReceiver.Trigger(null, null);
              foreach (var m in ((SpeckleAppUI)GSA.appUi).GroupMessages())
              {
                Status.AddMessage(m);
              }
            })
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
          catch (Exception ex)
          {
            Status.AddError(ex.Message);

            ReceiveStream(sender, e);
          }
        }
        else
        {
          try
          {
            await Task.Run(() => gsaReceiver.Trigger(null, null));
          }
          catch (Exception ex)
          {
            Status.AddError(ex.Message);

            ReceiveStream(sender, e);
          }
          ReceiveButtonPath.Fill = (SolidColorBrush)FindResource("SecondaryAccentBrush");// (SolidColorBrush)(new BrushConverter().ConvertFrom("#0080ff"));
        }
      }
      else if (status == UIStatus.RECEIVING)
      {
        status = UIStatus.IDLE;
        ReceiveButtonPath.Data = Geometry.Parse(PLAY_BUTTON);
        ReceiveButtonPath.Fill = (SolidColorBrush)FindResource("PrimaryHueMidBrush");

        ReceiverLayerToggle.IsEnabled = true;
        ReceiverContinuousToggle.IsEnabled = true;
        ReceiverControlPanel.IsEnabled = true;

        if (!ReceiverContinuousToggle.IsChecked.Value)
        {
          MessageBoxResult result = MessageBox.Show("Bake received objects permanently? ", "SpeckleGSA", MessageBoxButton.YesNo, MessageBoxImage.Question);
          if (result != MessageBoxResult.Yes)
          {
            gsaReceiver.DeleteSpeckleObjects();
          }
        }

        gsaReceiver.Dispose();
      }
      ReceiveButton.IsEnabled = true;
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
    private void AddError(object sender, ErrorEventArgs e)
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
            break;
          case UIStatus.RECEIVING:
            e.Handled = true;
            UITabControl.SelectedIndex = previousTabIndex;
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

      if (GSA.SenderInfo != null)
        foreach (KeyValuePair<string, Tuple<string, string>> sender in GSA.SenderInfo)
          SenderStreams.Items.Add(new Tuple<string, string>(sender.Key, sender.Value.Item1));

      if (GSA.ReceiverInfo != null)
        foreach (Tuple<string, string> receiver in GSA.ReceiverInfo)
          ReceiverStreams.Items.Add(receiver.Item1);
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
          var chkBox = (sender as CheckBox);
          propertyName = chkBox.Name;
          propertyValue = chkBox.IsChecked;

          GSA.Settings.SetFieldOrPropValue(propertyName, propertyValue);
        }
        else if (sender is TextBox)
        {
          var txtBox = (sender as TextBox);
          propertyName = txtBox.Name;
          propertyValue = txtBox.Text;

          GSA.Settings.SetFieldOrPropValue(propertyName, propertyValue);
        }
        else if (sender is ComboBox)
        {
          var comboBox = (sender as ComboBox);
          propertyName = comboBox.Name;
          var selectedItem = (comboBox.SelectedItem as ComboBoxItem);
          //For now, use the tag if present, otherwise use the text
          if (selectedItem.Tag != null && int.TryParse(selectedItem.Tag.ToString(), out int comboIntValue))
          {
            propertyValue = comboIntValue;
          }
          else
          {
            propertyValue = comboBox.Text;
          }

          GSA.Settings.SetFieldOrPropValue(propertyName, propertyValue);
        }
      }
      catch (Exception exception)
      { }
    }

    private void UpdateNodalResult(Object sender, RoutedEventArgs e)
    {
      var chk = sender as CheckBox;
      if (chk.IsChecked.Value)
        GSA.Settings.NodalResults[chk.Content as string] = chk.Tag as Tuple<int, int, List<string>>;
      else
        GSA.Settings.NodalResults.Remove(chk.Content as string);
    }

    private void UpdateElement1DResult(Object sender, RoutedEventArgs e)
    {
      var chk = sender as CheckBox;
      if (chk.IsChecked.Value)
        GSA.Settings.Element1DResults[chk.Content as string] = chk.Tag as Tuple<int, int, List<string>>;
      else
        GSA.Settings.Element1DResults.Remove(chk.Content as string);
    }

    private void UpdateElement2DResult(Object sender, RoutedEventArgs e)
    {
      var chk = sender as CheckBox;
      if (chk.IsChecked.Value)
        GSA.Settings.Element2DResults[chk.Content as string] = chk.Tag as Tuple<int, int, List<string>>;
      else
        GSA.Settings.Element2DResults.Remove(chk.Content as string);
    }

    private void UpdateMiscResult(Object sender, RoutedEventArgs e)
    {
      var chk = sender as CheckBox;
      if (chk.IsChecked.Value)
        GSA.Settings.MiscResults[chk.Content as string] = chk.Tag as Tuple<string, int, int, List<string>>;
      else
        GSA.Settings.MiscResults.Remove(chk.Content as string);
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
        Process.Start(url + @"#/view/" + streamID);
      }
    }

    private void StreamList_ViewStreamData(object sender, RoutedEventArgs e)
    {
      var cell = StreamList.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        string streamID = (cell as Tuple<string, string>).Item2;
        Process.Start(RestApi + @"/streams/" + streamID);
      }
    }

    private void StreamList_ViewObjectData(object sender, RoutedEventArgs e)
    {
      var cell = StreamList.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        string streamID = (cell as Tuple<string, string>).Item2;
        Process.Start(RestApi + @"/streams/" + streamID + @"/objects?omit=displayValue,base64");
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
        Process.Start(url + @"#/view/" + streamID);
      }
    }

    private void SenderStreams_ViewStreamData(object sender, RoutedEventArgs e)
    {
      var cell = SenderStreams.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        string streamID = (cell as Tuple<string, string>).Item2;
        Process.Start(RestApi + @"/streams/" + streamID);
      }
    }

    private void SenderStreams_ViewObjectData(object sender, RoutedEventArgs e)
    {
      var cell = SenderStreams.CurrentCell.Item;

      if (cell.GetType() == typeof(Tuple<string, string>))
      {
        string streamID = (cell as Tuple<string, string>).Item2;
        Process.Start(RestApi + @"/streams/" + streamID + @"/objects?omit=displayValue,base64");
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
        Process.Start(url + @"#/view/" + streamID);
      }
    }

    private void ReceiverStreams_ViewStreamData(object sender, RoutedEventArgs e)
    {
      var streamID = ReceiverStreams.CurrentCell.Item;

      if (streamID.GetType() == typeof(string))
      {
        Process.Start(RestApi + @"/streams/" + (string)streamID);
      }
    }

    private void ReceiverStreams_ViewObjectData(object sender, RoutedEventArgs e)
    {
      var streamID = ReceiverStreams.CurrentCell.Item;

      if (streamID.GetType() == typeof(string))
      {
        Process.Start(RestApi + @"/streams/" + (string)streamID + @"/objects?omit=displayValue,base64");
      }
    }

    private void ReceiverStreams_RemoveStream(object sender, RoutedEventArgs e)
    {
      var streamID = ReceiverStreams.CurrentCell.Item;

      if (streamID.GetType() == typeof(string))
      {
        GSA.ReceiverInfo.Remove(GSA.ReceiverInfo.First(x => x.Item1 == (string)streamID));
        if (!GSA.SetSpeckleClients(EmailAddress, RestApi))
        {
          Status.AddError("Error in communicating GSA - please check if the GSA file has been closed down");
          return;
        }
        UpdateClientLists();
      }
    }

    private Version getRunningVersion()
    {
      try
      {
        return ApplicationDeployment.CurrentDeployment.CurrentVersion;
      }
      catch (Exception)
      {
        return Assembly.GetExecutingAssembly().GetName().Version;
      }
    }
    #endregion

    private void MessagePane_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
      if (e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.LeftCtrl) && e.Key == System.Windows.Input.Key.C)
      {
        System.Text.StringBuilder copy_buffer = new System.Text.StringBuilder();
        foreach (object item in MessagePane.SelectedItems)
        {
          copy_buffer.AppendLine(item.ToString());
        }
        if (copy_buffer.Length > 0)
        {
          Clipboard.SetText(copy_buffer.ToString());
        }
      }
    }
  }
}
