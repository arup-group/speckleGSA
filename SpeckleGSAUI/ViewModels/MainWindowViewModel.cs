﻿using SpeckleGSAUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using SpeckleGSAUI.Utilities;
using System.ComponentModel;
using System.Windows;
using SpeckleGSAInterfaces;
using System.Reflection;
using System.Windows.Threading;
using System.Timers;
using SpeckleGSAProxy;
using System.Diagnostics;
using SpeckleGSA;

namespace SpeckleGSAUI.ViewModels
{
  public class MainWindowViewModel : INotifyPropertyChanged
  {
    //INotifyPropertyChanged aspects
    public event PropertyChangedEventHandler PropertyChanged;
    protected void NotifyPropertyChanged(String info) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));

    public TabCoordinator Coordinator { get; } = new TabCoordinator();
    public StateMachine StateMachine { get; } = new StateMachine();

    public string Title { get => "SpeckleGSA - " + Coordinator.RunningVersion; }

    public SpeckleAccountForUI Account { get => Coordinator.Account; }

    public GSATargetLayer ReceiveLayer
    {
      get => Coordinator.ReceiverTab.TargetLayer;
      set
      {
        Refresh(() => Coordinator.ReceiverTab.TargetLayer = value);
      }
    }
    public GSATargetLayer SendLayer { get => Coordinator.SenderTab.TargetLayer;
      set
      {
        Refresh(() => Coordinator.SenderTab.TargetLayer = value);
      }
    }
    public StreamMethod ReceiveStreamMethod { get => Coordinator.ReceiverTab.StreamMethod; set => Refresh(() => Coordinator.ReceiverTab.StreamMethod = value); }
    public StreamMethod SendStreamMethod { get => Coordinator.SenderTab.StreamMethod; set => Refresh(() => Coordinator.SenderTab.StreamMethod = value); }
    public string CurrentlyOpenFileName
    {
      get => Coordinator.FileStatus == GsaLoadedFileType.None ? "No file is currently open" : Coordinator.FileStatus == GsaLoadedFileType.NewFile ? "New file" : Coordinator.FilePath;
    }

    public bool MainWindowEnabled { get; private set; } = true;

    #region command_properties
    public DelegateCommand<object> InitialLoadCommand { get; set; }
    public DelegateCommand<object> ConnectToServerCommand { get; private set; }
    public DelegateCommand<object> UpdateStreamListCommand { get; private set; }
    public DelegateCommand<object> ReceiveSelectedStreamCommand { get; private set; }
    public DelegateCommand<object> NewFileCommand { get; private set; }
    public DelegateCommand<object> OpenFileCommand { get; private set; }
    public DelegateCommand<object> SaveFileCommand { get; private set; }
    public DelegateCommand<object> ReceiveStopCommand { get; private set; }
    public DelegateCommand<object> SendStopCommand { get; private set; }
    public DelegateCommand<object> ContinuousSendCommand { get; private set; }  //Used by the timer only
    public DelegateCommand<object> PasteClipboardCommand { get; private set; }
    public DelegateCommand<object> ClearReceiveStreamListCommand { get; private set; }
    public DelegateCommand<object> AddCandidateStreamIdCommand { get; private set; }
    public DelegateCommand<object> RenameStreamCommand { get; private set; }
    public DelegateCommand<object> RemoveStreamCommand { get; private set; }
    public DelegateCommand<object> CopyStreamIdCommand { get; set; }
    public DelegateCommand<object> ViewStreamCommand { get; set; }
    public DelegateCommand<object> ViewStreamDataCommand { get; set; }
    public DelegateCommand<object> ViewObjectDataCommand { get; set; }
    public DelegateCommand<object> CloneStreamCommand { get; set; }

    public string ReceiveButtonText { get => (ReceiveStreamMethod == StreamMethod.Continuous && StateMachine.StreamState == StreamState.ReceivingWaiting) ? "Stop" : "Receive"; }
    public string SendButtonText { get => (SendStreamMethod == StreamMethod.Continuous && StateMachine.StreamState == StreamState.SendingWaiting) ? "Stop" : "Send"; }
    #endregion

    #region logging_and_state_members
    public ObservableCollection<string> DisplayLogLines
    {
      get => new ObservableCollection<string>(Coordinator.DisplayLog.DisplayLogItems.Select(i => string.Join(" - ", i.TimeStamp.ToString("dd/MM/yyyy HH:mm:ss"), i.Description)));
    }
    public string StateSummary { get => (string.IsNullOrEmpty(stateSummaryOverride)) ? string.Join(" | ", fileStateDict[StateMachine.FileState], streamStateDict[StateMachine.StreamState]) : stateSummaryOverride; }
    public double ProcessingProgress { get; set; }

    private string stateSummaryOverride = "";  //To allow temporary finer information within a state to override the usual state summary
    #endregion

    #region stream_list_members
    private StreamListItem selectedStream = null;

    //These are for the server tab - the receiver and sender tabs have their own stream list
    public ObservableCollection<StreamListItem> ServerStreamListItems { get => new ObservableCollection<StreamListItem>(Coordinator.ServerStreamList.StreamListItems); }
    public StreamListItem SelectedStreamItem
    {
      get => selectedStream; 
      set
      {
        selectedStream = value;
        ReceiveSelectedStreamCommand.RaiseCanExecuteChanged();
      }
    }

    public StreamListItem SelectedReceiverStreamItem 
    { 
      get => Coordinator.ReceiverTab.StreamList.SeletedStreamListItem; set { Coordinator.ReceiverTab.StreamList.SeletedStreamListItem = value; } 
    }

    public StreamListItem SelectedSenderStreamItem
    {
      get => Coordinator.SenderTab.StreamList.SeletedStreamListItem; set { Coordinator.SenderTab.StreamList.SeletedStreamListItem = value; }
    }
    
    public ObservableCollection<string> SelectedLogLines { get; set; }
    public string SelectedLogLine { get; set; }

    public string CandidateStreamId { get; set; }
    public ObservableCollection<StreamListItem> ReceiverStreamListItems { get => new ObservableCollection<StreamListItem>(Coordinator.ReceiverTab.StreamList.StreamListItems); }

    public ObservableCollection<StreamListItem> SenderStreamListItems { get => new ObservableCollection<StreamListItem>(Coordinator.SenderTab.StreamList.StreamListItems); }
    #endregion

    #region settings_properties
    public ObservableCollection<ResultSettingItem> ResultSettingItems
    {
      get => new ObservableCollection<ResultSettingItem>(Coordinator.SenderTab.ResultSettings.ResultSettingItems.OrderByDescending(i => i.DefaultSelected));
    }

    public StreamContentConfig StreamContentConfig { get => Coordinator.SenderTab.StreamContentConfig;
      set => Refresh(() => Coordinator.SenderTab.StreamContentConfig = value); }

    public bool SendMeaningfulNodes { get; set; } = true;
    public int Additional1DPositions { get => Coordinator.SenderTab.AdditionalPositionsFor1dElements;
      set => Coordinator.SenderTab.AdditionalPositionsFor1dElements = value; }

    public double CoincidentNodeAllowance { get => Coordinator.ReceiverTab.CoincidentNodeAllowance; set { Coordinator.ReceiverTab.CoincidentNodeAllowance = value; } }
    public List<GsaUnit> CoincidentNodeAllowanceUnitOptions { get => new List<GsaUnit> { GsaUnit.Millimetres, GsaUnit.Metres, GsaUnit.Inches }; }
    public GsaUnit CoincidentNodeAllowanceUnit { get => Coordinator.ReceiverTab.CoincidentNodeUnits; set { Coordinator.ReceiverTab.CoincidentNodeUnits = value; } }
    public LoggingMinimumLevel LoggingMinimumLevel { get => Coordinator.LoggingMinimumLevel; set { Coordinator.LoggingMinimumLevel = value; } }
    public List<LoggingMinimumLevel> LoggingMinimumLevelOptions
    {
      get => new List<LoggingMinimumLevel> { LoggingMinimumLevel.Debug, LoggingMinimumLevel.Information, LoggingMinimumLevel.Error, LoggingMinimumLevel.Fatal };
    }
    public string CasesDescription { get => Coordinator.SenderTab.LoadCaseList; set { Coordinator.SenderTab.LoadCaseList = value; } }
    #endregion


    private readonly Dictionary<StreamState, string> streamStateDict = new Dictionary<StreamState, string> {
      { StreamState.NotLoggedIn, "Not logged in"},
      { StreamState.ActiveLoggingIn, "Logging in"},
      { StreamState.ActiveRetrievingStreamList, "Retrieving streams" },
      { StreamState.ReceivingWaiting, "Waiting for next receive event"},
      { StreamState.ActiveReceiving, "Receiving"},
      { StreamState.SendingWaiting, "Waiting for next send event"},
      { StreamState.ActiveSending, "Sending"},
      { StreamState.ActiveRenamingStream, "Renaming stream" },
      { StreamState.Ready, "Ready to stream" } };

    private readonly Dictionary<FileState, string> fileStateDict = new Dictionary<FileState, string> {
      { FileState.None, "No file loaded"},
      { FileState.Loading, "Loading file"},
      { FileState.Loaded, "File loaded" },
      { FileState.Saving, "Saving file"} };

    public int SelectedTabIndex { get; set; }

    private readonly Progress<double> percentageProgress = new Progress<double>();
    private readonly Progress<MessageEventArgs> loggingProgress = new Progress<MessageEventArgs>();
    private readonly Progress<SidSpeckleRecord> streamCreationProgress = new Progress<SidSpeckleRecord>();
    private readonly Progress<SidSpeckleRecord> streamDeletionProgress = new Progress<SidSpeckleRecord>();
    private readonly Progress<string> statusProgress = new Progress<string>();
    private List<DelegateCommandBase> cmds; //Filled in using Reflection and used in bulk updating of CanExecute bindings

    #region continuous_streaming_members
    private Timer TriggerTimer { get; set; } = new Timer();
    private ReceiverCoordinator continuousReceiverCoordinator;
    #endregion

    public MainWindowViewModel()
    {
      percentageProgress.ProgressChanged += ProcessPercentageProgressUpdate;
      //The same methods that handle messages from the kits are being used for messages originating from the commands and the SpeckleGSA library, with the 
      //sender and receiver coordinators
      loggingProgress.ProgressChanged += ProcessLogProgressUpdate;
      loggingProgress.ProgressChanged += GSA.ProcessMessageForLog;
      streamCreationProgress.ProgressChanged += ProcessStreamCreationProgress;
      streamDeletionProgress.ProgressChanged += ProcessStreamDeletionProgress;
      statusProgress.ProgressChanged += ProcessStatusProgressUpdate;
      //This ensures the messages for the display log in the UI, originating from the conversion code in the kits, end up being handled
      GSA.App.LocalMessenger.MessageAdded += ProcessLogProgressUpdate;
      CreateCommands();
    }

    private void CreateCommands()
    {
      InitialLoadCommand = new DelegateCommand<object>(
       async (o) =>
       {
         Refresh(() => StateMachine.StartedLoggingIn());

         var calibrateNodeAtTask = Task.Run(() => GSAProxy.CalibrateNodeAt());
         var initialLoadTask = Task.Run(() => Commands.InitialLoad(Coordinator, loggingProgress));

         var loaded = await initialLoadTask;
         await calibrateNodeAtTask;

         if (loaded)
         {
           var retrievedStreamInfoFromFile = await Task.Run(() => Commands.ReadSavedStreamInfo(Coordinator, loggingProgress));

           Refresh(() => StateMachine.LoggedIn());
         }
         else
         {
           Refresh(() => StateMachine.CancelledLoggingIn());
         }
       });

      ConnectToServerCommand = new DelegateCommand<object>(
        async (o) =>
        {
          Refresh(() => StateMachine.StartedLoggingIn());

          var signInWindow = new SpecklePopup.SignInWindow(true);

          MainWindowEnabled = false;

          signInWindow.ShowDialog();

          MainWindowEnabled = true;

          if (signInWindow.AccountListBox.SelectedIndex != -1)
          {
            var account = signInWindow.accounts[signInWindow.AccountListBox.SelectedIndex];
            var newAccountForUI = new SpeckleAccountForUI(account.RestApi, account.Email, account.Token);

            if (newAccountForUI != null && newAccountForUI.IsValid)
            {
              Refresh(() => StateMachine.StartedUpdatingStreams());

              var completed = await Commands.CompleteLogin(Coordinator, newAccountForUI, loggingProgress);

              if (completed)
              {
                Refresh(() => StateMachine.LoggedIn());

                var retrievedStreamInfoFromFile = await Task.Run(() => Commands.ReadSavedStreamInfo(Coordinator, loggingProgress));
              }
              else
              {
                Refresh(() => StateMachine.CancelledLoggingIn());
                GSA.App.Messenger.Message(MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error, "Failed to log in");
              }

              Refresh(() => StateMachine.StoppedUpdatingStreams());

              return;
            }
          }
          Refresh(() => StateMachine.CancelledLoggingIn());
          GSA.App.Messenger.Message(MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error, "Failed to log in");
        },
        (o) => !StateMachine.StreamIsOccupied);

      UpdateStreamListCommand = new DelegateCommand<object>(
        async (o) =>
        {
          Refresh(() => StateMachine.StartedUpdatingStreams());

          var result = await Task.Run(() => Commands.GetStreamList(Coordinator, Coordinator.Account, loggingProgress));

          Refresh(() => StateMachine.StoppedUpdatingStreams());
        },
        (o) => StateMachine.LoggedInState && !StateMachine.StreamIsOccupied );

      ReceiveSelectedStreamCommand = new DelegateCommand<object>(
        async (o) =>
        {
          SelectedTabIndex = (int)MainTab.Receiver;

          Coordinator.ReceiverTab.StreamList.StreamListItems.Clear();
          Coordinator.ReceiverTab.StreamList.StreamListItems.Add(SelectedStreamItem);

          //Sender coordinator is in the SpeckleGSA library, NOT the SpeckleInterface.  The sender coordinator calls the SpeckleInterface methods
          var gsaReceiverCoordinator = new ReceiverCoordinator();  //Coordinates across multiple streams

          Refresh(() => StateMachine.EnteredReceivingMode(ReceiveStreamMethod));

          var result = await Task.Run(() => Commands.Receive(Coordinator, gsaReceiverCoordinator, streamCreationProgress, loggingProgress, statusProgress, percentageProgress));

          Refresh(() => StateMachine.StoppedReceiving());
        },
        (o) => StateMachine.FileState == FileState.Loaded && StateMachine.StreamState == StreamState.Ready && (SelectedStreamItem != null));

      NewFileCommand = new DelegateCommand<object>(
        async (o) =>
        {
          Refresh(() => StateMachine.StartedOpeningFile());
          var result = await Task.Run(() => Commands.NewFile(Coordinator, loggingProgress));
          if (result)
          {
            Coordinator.FileStatus = GsaLoadedFileType.NewFile;
          }
          Refresh(() => StateMachine.OpenedFile());
        },
        (o) => !StateMachine.StreamFileIsOccupied && !StateMachine.FileIsOccupied);

      OpenFileCommand = new DelegateCommand<object>(
        async (o) =>
        {
          Refresh(() => StateMachine.StartedOpeningFile());
          var opened = await Task.Run(() => Commands.OpenFile(Coordinator, loggingProgress));
          if (opened)
          {
            var retrievedStreamInfoFromFile = await Task.Run(() => Commands.ReadSavedStreamInfo(Coordinator, loggingProgress));
            Refresh(() => StateMachine.OpenedFile());
          }
          else
          {
            Refresh(() => StateMachine.CancelledOpeningFile());
          }
        },
        (o) => !StateMachine.StreamFileIsOccupied && !StateMachine.FileIsOccupied);

      ReceiveStopCommand = new DelegateCommand<object>(
        async (o) =>
        {
          if (StateMachine.StreamState == StreamState.ReceivingWaiting)
          {
            continuousReceiverCoordinator.Dispose();
            Refresh(() => StateMachine.StoppedReceiving());
          }
          else
          {
            //Sender coordinator is in the SpeckleGSA library, NOT the SpeckleInterface.  The sender coordinator calls the SpeckleInterface methods
            continuousReceiverCoordinator = new ReceiverCoordinator();  //Coordinates across multiple streams

            Refresh(() => StateMachine.EnteredReceivingMode(ReceiveStreamMethod));

            var result = await Task.Run(() => Commands.Receive(Coordinator, continuousReceiverCoordinator, streamCreationProgress, loggingProgress, statusProgress, percentageProgress));

            if (ReceiveStreamMethod != StreamMethod.Continuous)
            {
              continuousReceiverCoordinator.Dispose();
            }

            Refresh(() => StateMachine.StoppedReceiving());
          }
        },
        (o) => StateMachine.StreamState == StreamState.ReceivingWaiting || (StateMachine.StreamState == StreamState.Ready && ReceiverStreamListItems.Count() > 0));

      PasteClipboardCommand = new DelegateCommand<object>(
        (o) =>
        {
          var paste = Clipboard.GetText(TextDataFormat.Text).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
          foreach (string p in paste)
          {
            Coordinator.ReceiverTab.StreamList.StreamListItems.Add(new StreamListItem(p, null));
            Coordinator.ReceiverTab.StreamListToSidRecords();
          }
          Refresh();
        },
        (o) => StateMachine.FileState == FileState.Loaded && !StateMachine.StreamFileIsOccupied);

      ClearReceiveStreamListCommand = new DelegateCommand<object>(
        (o) =>
        {
          Coordinator.ReceiverTab.StreamList.StreamListItems.Clear();
          Coordinator.ReceiverTab.StreamListToSidRecords();
          Refresh();
        },
        (o) => !StateMachine.StreamFileIsOccupied && ReceiverStreamListItems.Count() > 0);

      AddCandidateStreamIdCommand = new DelegateCommand<object>(
        (o) =>
        {
          if (!String.IsNullOrEmpty(CandidateStreamId))
          {
            Coordinator.ReceiverTab.StreamList.StreamListItems.Add(new StreamListItem(CandidateStreamId, null));
            CandidateStreamId = "";
          }
          Refresh();
        },
        (o) => StateMachine.FileState == FileState.Loaded && !StateMachine.StreamFileIsOccupied);

      SendStopCommand = new DelegateCommand<object>(
        async (o) =>
        {
          if (StateMachine.StreamState == StreamState.SendingWaiting)
          {
            Refresh(() => StateMachine.StoppedSending());
          }
          else
          {
            //Sender coordinator is in the SpeckleGSA library, NOT the SpeckleInterface.  The sender coordinator calls the SpeckleInterface methods
            var gsaSenderCoordinator = new SenderCoordinator();  //Coordinates across multiple streams

            Refresh(() => StateMachine.EnteredSendingMode(SendStreamMethod));
            var result = await Task.Run(() => Commands.SendInitial(Coordinator, gsaSenderCoordinator, streamCreationProgress, streamDeletionProgress, 
              loggingProgress, statusProgress, percentageProgress));
            Refresh(() => StateMachine.StoppedSending());

            if (SendStreamMethod == StreamMethod.Continuous)
            {
              TriggerTimer = new Timer(Coordinator.SenderTab.PollingRateMilliseconds);
              TriggerTimer.Elapsed += (sender, e) => Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background, new Action(() => ContinuousSendCommand.Execute(gsaSenderCoordinator)));
              TriggerTimer.AutoReset = false;
              TriggerTimer.Start();
            }
            else
            {
              gsaSenderCoordinator.Dispose();
            }
          }
        },
        (o) => (StateMachine.FileState == FileState.Loaded 
              && (StateMachine.StreamState == StreamState.SendingWaiting || StateMachine.StreamState == StreamState.Ready)));

      ContinuousSendCommand = new DelegateCommand<object>(
        async (o) =>
        {
          var gsaSenderCoordinator = (SenderCoordinator)o;
          Refresh(() => StateMachine.StartedTriggeredSending());
          var result = await Task.Run(() => Commands.SendTriggered(gsaSenderCoordinator));
          Refresh(() => StateMachine.StoppedTriggeredSending());

          TriggerTimer.Start();
        }, (o) => true);

      SaveFileCommand = new DelegateCommand<object>(
        async (o) =>
        {
          Refresh(() => StateMachine.StartedSavingFile());
          var result = await Task.Run(() => Commands.SaveFile(Coordinator));
          Refresh(() => StateMachine.StoppedSavingFile());
        },
        (o) => !StateMachine.StreamFileIsOccupied && StateMachine.FileState == FileState.Loaded);

      RemoveStreamCommand = new DelegateCommand<object>(
        (o) =>
        {
          var streamIdToRemove = Coordinator.SenderTab.StreamList.SeletedStreamListItem.StreamId;
          if (!string.IsNullOrEmpty(streamIdToRemove))
          {
            var sidRecordToRemove = Coordinator.SenderTab.SenderSidRecords.FirstOrDefault(r => r.StreamId.Equals(streamIdToRemove));
            if (sidRecordToRemove != null)
            {
              Coordinator.SenderTab.RemoveSidSpeckleRecord(sidRecordToRemove);
              Coordinator.SenderTab.SidRecordsToStreamList();
              Refresh();
            }
          }
        },
        (o) => !StateMachine.StreamIsOccupied);  //There is no visual button linked to this command so the CanExecute condition can be less strict 

      RenameStreamCommand = new DelegateCommand<object>(
        async (o) =>
        {
          Refresh(() => StateMachine.StartedRenamingStream());
          var newStreamName = o.ToString();
          var streamId = Coordinator.SenderTab.StreamList.SeletedStreamListItem.StreamId;
          var result = await Task.Run(() => Commands.RenameStream(Coordinator, streamId, newStreamName, loggingProgress));
          Coordinator.SenderTab.ChangeSidRecordStreamName(streamId, newStreamName);
          Coordinator.SenderTab.SidRecordsToStreamList();
          Refresh(() => StateMachine.StoppedRenamingStream());
        },
        (o) => !StateMachine.StreamIsOccupied);  //There is no visual button linked to this command so the CanExecute condition can be less strict 


      CopyStreamIdCommand = new DelegateCommand<object>(
        (o) =>
        {
          var tab = (string)o;
          string streamId = SelectedStreamId(tab);
          if (!string.IsNullOrEmpty(streamId))
          {
            Clipboard.SetText(streamId);
          }
        },
        (o) => !StateMachine.StreamIsOccupied);

      ViewStreamCommand = new DelegateCommand<object>(
        (o) =>
        {
          var tab = (string)o;
          string streamId = SelectedStreamId(tab);
          if (!string.IsNullOrWhiteSpace(streamId))
          {
            Task.Run(() =>
            {
              string url = Coordinator.Account.ServerUrl.Split(new string[] { "api" }, StringSplitOptions.RemoveEmptyEntries)[0];
              Process.Start(url + @"#/view/" + streamId);
            });
          }
        },
        (o) => !StateMachine.StreamIsOccupied);

      ViewStreamDataCommand = new DelegateCommand<object>(
        (o) =>
        {
          var tab = (string)o;
          string streamId = SelectedStreamId(tab);
          if (!string.IsNullOrWhiteSpace(streamId))
          {
            Task.Run(() =>
            {
              Process.Start(Coordinator.Account.ServerUrl + @"/streams/" + streamId);
            });
          }
        },
        (o) => !StateMachine.StreamIsOccupied);

      ViewObjectDataCommand = new DelegateCommand<object>(
        (o) =>
        {
          var tab = (string)o;
          string streamId = SelectedStreamId(tab);
          if (!string.IsNullOrWhiteSpace(streamId))
          {
            Task.Run(() =>
            {
              Process.Start(Coordinator.Account.ServerUrl + @"/streams/" + streamId + @"/objects?omit=displayVale,base64&limit=500");
            });
          }
        },
        (o) => !StateMachine.StreamIsOccupied);

      CloneStreamCommand = new DelegateCommand<object>(
        (o) =>
        {
          var tab = (string)o;
          string streamId = SelectedStreamId(tab);
          if (!string.IsNullOrWhiteSpace(streamId))
          {
            Task.Run(() => Commands.CloneStream(Coordinator, streamId, loggingProgress));
          }
        },
        (o) => !StateMachine.StreamIsOccupied);

      cmds = new List<DelegateCommandBase>();
      Type myType = this.GetType();
      IList<PropertyInfo> props = new List<PropertyInfo>(myType.GetProperties());

      var baseType = typeof(DelegateCommandBase);
      foreach (PropertyInfo prop in props)
      {
        if (prop.PropertyType.IsSubclassOf(baseType))
        {
          var cmd = (DelegateCommandBase)prop.GetValue(this, null);
          cmds.Add(cmd);
        }
      }
    }

    #region progress_fns

    private void ProcessStreamCreationProgress(object sender, SidSpeckleRecord r)
    {
      //This is only releveant for sending since no streams are created when receiving
      if (!Coordinator.SenderTab.StreamList.StreamListItems.Any(s => s.StreamId.Equals(r.StreamId, StringComparison.InvariantCultureIgnoreCase)))
      {
        Coordinator.SenderTab.SenderSidRecords.Add(r);
        Coordinator.SenderTab.SidRecordsToStreamList();
        NotifyPropertyChanged("SenderStreamListItems");
      }
    }

    private void ProcessStreamDeletionProgress(object sender, SidSpeckleRecord r)
    {
      //This is only releveant for sending since no streams are deleted when receiving
      var matching = Coordinator.SenderTab.StreamList.StreamListItems.Where(sli => sli.StreamId.Equals(r.StreamId, StringComparison.InvariantCultureIgnoreCase)).ToList();
      if (matching != null && matching.Count() > 0)
      {
        Coordinator.SenderTab.RemoveSidSpeckleRecord(r);
        Coordinator.SenderTab.SidRecordsToStreamList();
        NotifyPropertyChanged("SenderStreamListItems");
      }
    }

    private void ProcessLogProgressUpdate(object sender, MessageEventArgs mea)
    {
      if (mea.Intent == SpeckleGSAInterfaces.MessageIntent.Display)
      {
        var displayLine = mea.MessagePortions.First() + ((mea.MessagePortions.Count() == 1) ? "" : ": " + string.Join(" ", mea.MessagePortions.Skip(1)));
        if (mea.Level == MessageLevel.Information)
        {
          Coordinator.DisplayLog.DisplayLogItems.Add(new DisplayLogItem(displayLine));
        }
        else if (mea.Level == MessageLevel.Error || mea.Level == MessageLevel.Fatal)
        {
          Coordinator.DisplayLog.DisplayLogItems.Add(new DisplayLogItem("Error: " + displayLine));
        }
        NotifyPropertyChanged("DisplayLogLines");
      }
    }

    private void ProcessPercentageProgressUpdate(object sender, double n)
    {
      ProcessingProgress = n;
      NotifyPropertyChanged("ProcessingProgress");
    }

    private void ProcessStatusProgressUpdate(object sender, string s)
    {
      stateSummaryOverride = s;
      NotifyPropertyChanged("StateSummary");
    }
    #endregion

    private void Refresh(Action stateUpdateFn = null)
    {
      stateSummaryOverride = "";
      stateUpdateFn?.Invoke();

      cmds.ForEach(cmd => cmd.RaiseCanExecuteChanged());
      if (!StateMachine.StreamFileIsOccupied)
      {
        ProcessingProgress = 0;
      }
      NotifyPropertyChanged(null);
    }

    private string SelectedStreamId(string tab)
    {
      string streamId = null;
      if (tab == "Sender")
      {
        if (Coordinator.SenderTab.StreamList.SeletedStreamListItem != null && !string.IsNullOrEmpty(Coordinator.SenderTab.StreamList.SeletedStreamListItem.StreamId))
        {
          streamId = Coordinator.SenderTab.StreamList.SeletedStreamListItem.StreamId;
        }
      }
      else if (tab == "Receiver")
      {
        if (Coordinator.ReceiverTab.StreamList.SeletedStreamListItem != null && !string.IsNullOrEmpty(Coordinator.ReceiverTab.StreamList.SeletedStreamListItem.StreamId))
        {
          streamId = Coordinator.ReceiverTab.StreamList.SeletedStreamListItem.StreamId;
        }
      }
      else
      {
        if (SelectedStreamItem != null && !string.IsNullOrEmpty(SelectedStreamItem.StreamId))
        {
          streamId = SelectedStreamItem.StreamId;
        }
      }
      return streamId;
    }
  }
}
