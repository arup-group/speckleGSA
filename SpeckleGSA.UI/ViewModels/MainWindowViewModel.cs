using SpeckleGSA.UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using SpeckleGSA.UI.Utilities;
using System.ComponentModel;
using System.Windows;
using SpeckleGSAInterfaces;
using System.Reflection;
using System.Windows.Input;
using SpeckleInterface;
using SpeckleCore;

namespace SpeckleGSA.UI.ViewModels
{
  public class MainWindowViewModel : INotifyPropertyChanged
  {
    //INotifyPropertyChanged aspects
    public event PropertyChangedEventHandler PropertyChanged;
    protected void NotifyPropertyChanged(String info) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));

    public CoordinatorForUI Coordinator { get; } = new CoordinatorForUI();
    public StateMachine StateMachine { get; } = new StateMachine();

    public string Title { get => "SpeckleGSA - " + Coordinator.RunningVersion; }

    public SpeckleAccountForUI Account { get => Coordinator.Account; }
    public GSATargetLayer ReceiveLayer
    {
      get => Coordinator.ReceiverCoordinatorForUI.TargetLayer; 
      set
      {
        Refresh(() => Coordinator.ReceiverCoordinatorForUI.TargetLayer = value);
      }
    }
    public GSATargetLayer SendLayer { get => Coordinator.SenderCoordinatorForUI.TargetLayer; 
      set 
      {
        Refresh(() => Coordinator.SenderCoordinatorForUI.TargetLayer = value);
      } 
    }
    public StreamMethod ReceiveStreamMethod { get => Coordinator.ReceiverCoordinatorForUI.StreamMethod; set => Refresh(() => Coordinator.ReceiverCoordinatorForUI.StreamMethod = value); }
    public StreamMethod SendStreamMethod { get => Coordinator.SenderCoordinatorForUI.StreamMethod; set => Refresh(() => Coordinator.SenderCoordinatorForUI.StreamMethod = value); }
    public string CurrentlyOpenFileName
    {
      get => Coordinator.FileStatus == GsaLoadedFileType.None ? "No file is currently open" : Coordinator.FileStatus == GsaLoadedFileType.NewFile ? "New file" : Coordinator.FilePath;
    }

    #region command_properties
    public DelegateCommand<object> InitialLoadCommand { get; set; }
    public DelegateCommand<object> ConnectToServerCommand { get; private set; }
    public DelegateCommand<object> UpdateStreamListCommand { get; private set; }
    public DelegateCommand<object> ReceiveSelectedStreamCommand { get; private set; }
    public DelegateCommand<object> NewFileCommand { get; private set; }
    public DelegateCommand<object> OpenFileCommand { get; private set; }
    public DelegateCommand<object> SaveAndCloseCommand { get; private set; }
    public DelegateCommand<object> ReceiveCommand { get; private set; }
    public DelegateCommand<object> SendCommand { get; private set; }
    public DelegateCommand<object> PasteClipboardCommand { get; private set; }
    public DelegateCommand<object> ClearReceiveStreamListCommand { get; private set; }
    public DelegateCommand<object> AddCandidateStreamIdCommand { get; private set; }
    public DelegateCommand<object> RenameStreamCommand { get; private set; }

    public string ReceiveButtonText { get => (ReceiveStreamMethod == StreamMethod.Continuous && StateMachine.State == AppState.ReceivingWaiting) ? "Stop" : "Receive"; }
    public string SendButtonText { get => (SendStreamMethod == StreamMethod.Continuous && StateMachine.State == AppState.SendingWaiting) ? "Stop" : "Send"; }
    #endregion

    #region logging_and_state_members
    public ObservableCollection<string> DisplayLogLines
    {
      get => new ObservableCollection<string>(Coordinator.DisplayLog.DisplayLogItems.Select(i => string.Join(" - ", i.TimeStamp.ToString("dd/MM/yyyy HH:mm:ss"), i.Description)));
    }
    public string StateSummary { get => (string.IsNullOrEmpty(stateSummaryOverride)) ? appDateDict[StateMachine.State] : stateSummaryOverride; }
    public double ProcessingProgress { get; set; }

    private string stateSummaryOverride = "";  //To allow temporary finer information within a state to override the usual state summary
    #endregion

    #region stream_list_members
    public ObservableCollection<StreamListItem> ServerStreamListItems { get => new ObservableCollection<StreamListItem>(Coordinator.ServerStreamList.StreamListItems); }
    public StreamListItem SelectedStreamItem
    {
      get => selectedStream; set
      {
        selectedStream = value;
        ReceiveSelectedStreamCommand.RaiseCanExecuteChanged();
      }
    }
    private StreamListItem selectedStream = null;

    public string CandidateStreamId { get; set; }
    public ObservableCollection<StreamListItem> ReceiverStreamListItems { get => new ObservableCollection<StreamListItem>(Coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems); }

    public ObservableCollection<StreamListItem> SenderStreamListItems { get => new ObservableCollection<StreamListItem>(Coordinator.SenderCoordinatorForUI.StreamList.StreamListItems); }
    #endregion

    #region settings_properties
    public ObservableCollection<ResultSettingItem> ResultSettingItems
    {
      get => new ObservableCollection<ResultSettingItem>(Coordinator.SenderCoordinatorForUI.ResultSettings.ResultSettingItems.OrderByDescending(i => i.DefaultSelected));
    }

    public StreamContentConfig StreamContentConfig { get => Coordinator.SenderCoordinatorForUI.StreamContentConfig; 
      set => Refresh(() => Coordinator.SenderCoordinatorForUI.StreamContentConfig = value); }

    public bool SendMeaningfulNodes { get; set; } = true;
    public double PollingRate { get; set; } = (double)2000;
    public double CoincidentNodeAllowance { get => Coordinator.ReceiverCoordinatorForUI.CoincidentNodeAllowance; set { Coordinator.ReceiverCoordinatorForUI.CoincidentNodeAllowance = value; } }
    public List<GsaUnit> CoincidentNodeAllowanceUnitOptions { get => new List<GsaUnit> { GsaUnit.Millimetres, GsaUnit.Metres, GsaUnit.Inches }; }
    public GsaUnit CoincidentNodeAllowanceUnit { get => Coordinator.ReceiverCoordinatorForUI.CoincidentNodeUnits; set { Coordinator.ReceiverCoordinatorForUI.CoincidentNodeUnits = value; } }
    public LoggingMinimumLevel LoggingMinimumLevel { get => Coordinator.LoggingMinimumLevel; set { Coordinator.LoggingMinimumLevel = value; } }
    public List<LoggingMinimumLevel> LoggingMinimumLevelOptions
    {
      get => new List<LoggingMinimumLevel> { LoggingMinimumLevel.Debug, LoggingMinimumLevel.Information, LoggingMinimumLevel.Error, LoggingMinimumLevel.Fatal };
    }
    public string CasesDescription { get => Coordinator.SenderCoordinatorForUI.LoadCaseList; set { Coordinator.SenderCoordinatorForUI.LoadCaseList = value; } }
    #endregion


    private readonly Dictionary<AppState, string> appDateDict = new Dictionary<AppState, string> {
      { AppState.NotLoggedIn, "Not logged in and no link to active GSA file"},
      { AppState.NotLoggedInLinkedToGsa, "Not logged in but linked to active GSA file"},
      { AppState.ActiveLoggingIn, "Logging in"},
      { AppState.ActiveRetrievingStreamList, "Retrieving streams" },
      { AppState.LoggedInNotLinkedToGsa, "Logged in but not linked to active GSA file"},
      { AppState.ReceivingWaiting, "Waiting for next receive event"},
      { AppState.ActiveReceiving, "Receiving"},
      { AppState.SendingWaiting, "Waiting for next send event"},
      { AppState.ActiveSending, "Sending"},
      { AppState.OpeningFile, "Opening file" },
      { AppState.SavingFile, "Saving file" },
      { AppState.ActiveRenamingStream, "Renaming stream" },
      { AppState.Ready, "Ready" } };

    private readonly Dictionary<int, MainTab> TabData = new Dictionary<int, MainTab>
    { 
      { 0, MainTab.Server },
      { 1, MainTab.GSA },
      { 2, MainTab.Sender },
      { 3, MainTab.Receiver },
      { 4, MainTab.Settings }
    };
    
    public int SelectedTabIndex { get; set; }

    private readonly Progress<double> percentageProgress = new Progress<double>();
    private readonly Progress<MessageEventArgs> loggingProgress = new Progress<MessageEventArgs>();
    private readonly Progress<StreamListItem> streamCreationProgress = new Progress<StreamListItem>();
    private readonly Progress<string> statusProgress = new Progress<string>();
    private List<DelegateCommandBase> cmds; //Filled in using Reflection and used in bulk updating of CanExecute bindings

    public bool IsSendStreamListEnabled => StateMachine.State != AppState.ActiveRenamingStream;

    public MainWindowViewModel()
    {
      percentageProgress.ProgressChanged += ProcessPercentageProgressUpdate;
      loggingProgress.ProgressChanged += ProcessLogProgressUpdate;
      streamCreationProgress.ProgressChanged += ProcessStreamCreationProgress;
      statusProgress.ProgressChanged += ProcessStatusProgressUpdate;
      CreateCommands();
    }

    private void CreateCommands()
    {
      InitialLoadCommand = new DelegateCommand<object>(
       async (o) =>
       {
         Refresh(() => StateMachine.StartedLoggingIn());

         var loaded = await Task.Run(() => Commands.InitialLoadAsync(Coordinator, loggingProgress));

         if (loaded)
         {
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

          Coordinator.Account = await Task.Run(() => Commands.Login());

          Refresh(() => StateMachine.LoggedIn());

          Refresh(() => StateMachine.StartedUpdatingStreams());

          Coordinator.ServerStreamList = await Task.Run(() => Commands.GetStreamList());

          Refresh(() => StateMachine.StoppedUpdatingStreams());
        },
        (o) => !StateMachine.StreamFileIsOccupied);

      UpdateStreamListCommand = new DelegateCommand<object>(
        async (o) =>
        {
          Refresh(() => StateMachine.StartedUpdatingStreams());

          Coordinator.ServerStreamList = await Task.Run(() => Commands.GetStreamList());

          Refresh(() => StateMachine.StoppedUpdatingStreams());
        },
        (o) => !StateMachine.StreamFileIsOccupied && StateMachine.State != AppState.NotLoggedIn);

      ReceiveSelectedStreamCommand = new DelegateCommand<object>(
        async (o) =>
        {
          SelectedTabIndex = (int)MainTab.Receiver;

          Coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems.Clear();
          Coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems.Add(SelectedStreamItem);

          Refresh(() => StateMachine.EnteredReceivingMode(ReceiveStreamMethod));

          var result = await Task.Run(() => Commands.Receive(Coordinator.Account, Coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems, 
            Coordinator.ReceiverCoordinatorForUI.TargetLayer, streamCreationProgress, loggingProgress, statusProgress, percentageProgress));

          Refresh(() => StateMachine.StoppedReceiving());
        },
        (o) => StateMachine.State == AppState.Ready && (SelectedStreamItem != null));

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
          var opened = await Task.Run(() => Commands.OpenFile(Coordinator));
          if (opened)
          {
            Refresh(() => StateMachine.OpenedFile());
          }
          else
          {
            Refresh(() => StateMachine.CancelledOpeningFile());
          }
        },
        (o) => !StateMachine.StreamFileIsOccupied && !StateMachine.FileIsOccupied);

      ReceiveCommand = new DelegateCommand<object>(
        async (o) =>
        {
          if (StateMachine.State == AppState.ReceivingWaiting)
          {
            Refresh(() => StateMachine.StoppedReceiving());
          }
          else
          {
            Refresh(() => StateMachine.EnteredReceivingMode(ReceiveStreamMethod));
            var result = await Task.Run(() => Commands.Receive(Coordinator.Account, Coordinator.ServerStreamList.StreamListItems, Coordinator.ReceiverCoordinatorForUI.TargetLayer, 
              streamCreationProgress, loggingProgress, statusProgress, percentageProgress));
            Refresh(() => StateMachine.StoppedReceiving());
          }
        },
        (o) => StateMachine.State == AppState.ReceivingWaiting || (ReceiverStreamListItems.Count() > 0 && StateMachine.State == AppState.Ready));

      PasteClipboardCommand = new DelegateCommand<object>(
        (o) =>
        {
          var paste = Clipboard.GetText(TextDataFormat.Text).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
          foreach (string p in paste)
          {
            Coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems.Add(new StreamListItem(p, null));
          }
          Refresh();
        },
        (o) => !StateMachine.StreamFileIsOccupied && StateMachine.State == AppState.Ready);

      ClearReceiveStreamListCommand = new DelegateCommand<object>(
        (o) =>
        {
          Coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems.Clear();
          Refresh();
        },
        (o) => !StateMachine.StreamFileIsOccupied && ReceiverStreamListItems.Count() > 0);

      AddCandidateStreamIdCommand = new DelegateCommand<object>(
        (o) =>
        {
          if (!String.IsNullOrEmpty(CandidateStreamId))
          {
            Coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems.Add(new StreamListItem(CandidateStreamId, null));
            CandidateStreamId = "";
          }
          Refresh();
        },
        (o) => !StateMachine.StreamFileIsOccupied && StateMachine.State == AppState.Ready);

      SendCommand = new DelegateCommand<object>(
        async (o) =>
        {
          if (StateMachine.State == AppState.SendingWaiting)
          {
            Refresh(() => StateMachine.StoppedSending());
          }
          else
          {
            Refresh(() => StateMachine.EnteredSendingMode(SendStreamMethod));
            var result = await Task.Run(() => Commands.Send(Coordinator.Account, Coordinator.SenderCoordinatorForUI.TargetLayer, Coordinator.SenderCoordinatorForUI.StreamContentConfig, 
              Coordinator.SenderCoordinatorForUI.ResultSettings.ResultSettingItems.Where(rsi => rsi.Selected).ToList(), Coordinator.SenderCoordinatorForUI.LoadCaseList,
              streamCreationProgress, loggingProgress, statusProgress, percentageProgress));
            Refresh(() => StateMachine.StoppedSending());
          }
        },
        (o) => StateMachine.State == AppState.SendingWaiting || StateMachine.State == AppState.Ready);

      SaveAndCloseCommand = new DelegateCommand<object>(
        async (o) =>
        {
          Refresh(() => StateMachine.StartedSavingFile());
          var result = await Task.Run(() => Commands.SaveFile());
          Refresh(() => StateMachine.StoppedSavingFile());
        },
        (o) => !StateMachine.StreamFileIsOccupied && StateMachine.FileState == FileState.Loaded);

      RenameStreamCommand = new DelegateCommand<object>(
        async (o) =>
        {
          var newStreamName = o.ToString();
          Refresh(() => StateMachine.StartedRenamingStream());
          var result = await Task.Run(() => Commands.RenameStream(SelectedStreamItem.StreamId, newStreamName, loggingProgress));
          SelectedStreamItem.StreamName = newStreamName;
          Refresh(() => StateMachine.StoppedRenamingStream());
        },
        (o) => !StateMachine.StreamFileIsOccupied && !StateMachine.StreamIsOccupied);  //There is no visual button linked to this command so the CanExecute condition can be less strict 

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

    private void ProcessStreamCreationProgress(object sender, StreamListItem e)
    {
      Coordinator.SenderCoordinatorForUI.StreamList.StreamListItems.Add(e);
      NotifyPropertyChanged("SenderStreamListItems");
    }

    private void ProcessLogProgressUpdate(object sender, MessageEventArgs mea)
    {
      if (mea.Intent == SpeckleGSAInterfaces.MessageIntent.Display)
      {
        Coordinator.DisplayLog.DisplayLogItems.Add(new DisplayLogItem(mea.MessagePortions.First()));
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
  }
}
