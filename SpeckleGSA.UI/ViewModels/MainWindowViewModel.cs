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

namespace SpeckleGSA.UI.ViewModels
{
  public class MainWindowViewModel : INotifyPropertyChanged
  {
    //INotifyPropertyChanged aspects
    public event PropertyChangedEventHandler PropertyChanged;
    protected void NotifyPropertyChanged(String info) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));

    public Coordinator Coordinator { get; } = new Coordinator();
    public StateMachine StateMachine { get; } = new StateMachine();

    public SpeckleAccount Account { get => Coordinator.Account; }
    public GSATargetLayer Layer { get; set; } = GSATargetLayer.Design;
    public StreamMethod StreamMethod { get => Coordinator.SenderCoordinator.StreamMethod; set { Coordinator.SenderCoordinator.StreamMethod = value; } }
    public string CurrentlyOpenFileName
    {
      get => Coordinator.FileStatus == GsaLoadedFileType.None ? "No file is currently open" : Coordinator.FileStatus == GsaLoadedFileType.NewFile ? "New file" : Coordinator.FilePath;
    }

    #region command_properties
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
    #endregion

    #region logging_and_state_members
    public ObservableCollection<string> DisplayLogLines 
    { 
      get => new ObservableCollection<string>(Coordinator.DisplayLog.DisplayLogItems.Select(i => string.Join(" - ", i.TimeStamp.ToString("dd/MM/yyyy HH:mm:ss"), i.Description))); 
    }
    public string StateSummary { get => appDateDict[StateMachine.State]; }
    public double ProcessingProgress { get; set; }
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
    public ObservableCollection<StreamListItem> ReceiverStreamListItems { get => new ObservableCollection<StreamListItem>(Coordinator.ReceiverCoordinator.StreamList.StreamListItems);  }

    public ObservableCollection<StreamListItem> SenderStreamListItems { get => new ObservableCollection<StreamListItem>(Coordinator.SenderCoordinator.StreamList.StreamListItems); }
    #endregion

    #region settings_properties
    public ObservableCollection<ResultSettingItem> ResultSettingItems 
    { 
      get => new ObservableCollection<ResultSettingItem>(Coordinator.SenderCoordinator.ResultSettings.ResultSettingItems.OrderByDescending(i => i.DefaultSelected)); 
    }

    public StreamContentConfig StreamContentConfig { get => Coordinator.SenderCoordinator.StreamContentConfig; set { Coordinator.SenderCoordinator.StreamContentConfig = value; } }
    public bool SendMeaningfulNodes { get; set; } = true;
    public double PollingRate { get; set; } = (double)2000;
    public double CoincidentNodeAllowance { get => Coordinator.ReceiverCoordinator.CoincidentNodeAllowance; set { Coordinator.ReceiverCoordinator.CoincidentNodeAllowance = value; } }
    public List<GsaUnit> CoincidentNodeAllowanceUnitOptions { get => new List<GsaUnit> { GsaUnit.Millimetres, GsaUnit.Metres, GsaUnit.Inches }; }
    public GsaUnit CoincidentNodeAllowanceUnit { get => Coordinator.ReceiverCoordinator.CoincidentNodeUnits; set { Coordinator.ReceiverCoordinator.CoincidentNodeUnits = value; } }
    public LoggingMinimumLevel LoggingMinimumLevel { get => Coordinator.LoggingMinimumLevel; set { Coordinator.LoggingMinimumLevel = value; } }
    public List<LoggingMinimumLevel> LoggingMinimumLevelOptions 
    { 
      get => new List<LoggingMinimumLevel> { LoggingMinimumLevel.Debug, LoggingMinimumLevel.Information, LoggingMinimumLevel.Error, LoggingMinimumLevel.Fatal };
    }
    #endregion 
    

    private readonly Dictionary<AppState, string> appDateDict = new Dictionary<AppState, string> { 
      { AppState.NotLoggedIn, "Not logged in"},
      { AppState.ActiveLoggingIn, "Logging in"},
      { AppState.ActiveRetrievingStreamList, "Retrieving streams" },
      { AppState.LoggedInNotLinkedToGsa, "Logged in"},
      { AppState.ReceivingWaiting, "Waiting for next receive event"},
      { AppState.ActiveReceiving, "Receiving"},
      { AppState.SendingWaiting, "Waiting for next send event"},
      { AppState.ActiveSending, "Sending"},
      { AppState.OpeningFile, "Opening file" },
      { AppState.SavingFile, "Saving file" },
      { AppState.Ready, "Ready" } };

    private readonly Progress<int> overallProgress = new Progress<int>();
    private readonly Progress<int> numProcessingItemsProgress = new Progress<int>();
    private readonly Progress<DisplayLogItem> loggingProgress =  new Progress<DisplayLogItem>();
    private readonly Progress<StreamListItem> streamCreationProgress = new Progress<StreamListItem>();

    public bool IsSendStreamListEnabled => StateMachine.State != AppState.ActiveRenamingStream;

    public MainWindowViewModel()
    {
      //Coordinator.ServerStreamList = UI.DataAccess.DataAccess.GetStreamList();
      //Coordinator.Account = UI.DataAccess.DataAccess.GetDefaultAccount();
      //Coordinator.DisplayLog = new DisplayLog();

      overallProgress.ProgressChanged += ProcessOverallProgressUpdate;
      loggingProgress.ProgressChanged += ProcessLogProgressUpdate;
      streamCreationProgress.ProgressChanged += ProcessStreamCreationProgress;

      CreateCommands();
    }

    private void CreateCommands()
    {
      ConnectToServerCommand = new DelegateCommand<object>(
        async (o) =>
        {
          StateMachine.StartedLoggingIn(); ;

          Coordinator.Account = await Task.Run(() => Commands.Login());
          NotifyPropertyChanged("Account");

          StateMachine.LoggedIn();
          NotifyPropertyChanged("StateSummary");

          StateMachine.StartedUpdatingStreams();
          NotifyPropertyChanged("StateSummary");

          Coordinator.ServerStreamList = await Task.Run(() => Commands.GetStreamList());
          NotifyPropertyChanged("ServerStreamListItems");

          StateMachine.StoppedUpdatingStreams();
          NotifyPropertyChanged("StateSummary");
        },
        (o) => true);

      UpdateStreamListCommand = new DelegateCommand<object>(
        async (o) =>
        {
          StateMachine.StartedUpdatingStreams();

          Coordinator.ServerStreamList = await Task.Run(() => Commands.GetStreamList());
          NotifyPropertyChanged("ServerStreamListItems");

          StateMachine.StoppedUpdatingStreams();
        },
        (o) => true);

      ReceiveSelectedStreamCommand = new DelegateCommand<object>(
        async (o) =>
        {
          StateMachine.EnteredReceivingMode(StreamMethod);
          var result = await Task.Run(() => Commands.Receive(numProcessingItemsProgress, overallProgress, loggingProgress));
          StateMachine.StoppedReceiving();
        },
        (o) => (SelectedStreamItem != null));

      NewFileCommand = new DelegateCommand<object>(
        async (o) =>
        {
          StateMachine.StartedOpeningFile();
          var result = await Task.Run(() => Commands.NewFile());
          if (result)
          {
            Coordinator.FileStatus = GsaLoadedFileType.NewFile;
            NotifyPropertyChanged("CurrentlyOpenFileName");
          }
          StateMachine.OpenedFile();
        },
        (o) => true);

      OpenFileCommand = new DelegateCommand<object>(
        async (o) =>
        {
          StateMachine.StartedOpeningFile();
          var filePath = await Task.Run(() => Commands.OpenFile());
          if (string.IsNullOrEmpty(filePath))
          {
            StateMachine.CancelledOpeningFile();
          }
          else
          {
            Coordinator.FileStatus = GsaLoadedFileType.ExistingFile;
            Coordinator.FilePath = filePath;
            NotifyPropertyChanged("CurrentlyOpenFileName");

            StateMachine.OpenedFile();
          }
        },
        (o) => true);

      ReceiveCommand = new DelegateCommand<object>(
        async (o) =>
        {
          StateMachine.EnteredReceivingMode(StreamMethod);
          var result = await Task.Run(() => Commands.Receive(numProcessingItemsProgress, overallProgress, loggingProgress));
          StateMachine.StoppedReceiving();
        },
        (o) => true);

      PasteClipboardCommand = new DelegateCommand<object>(
        (o) =>
        {
          var paste = Clipboard.GetText(TextDataFormat.Text).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
          foreach (string p in paste)
          {
            Coordinator.ReceiverCoordinator.StreamList.StreamListItems.Add(new StreamListItem(p, null));
          }
          NotifyPropertyChanged("ReceiverStreamListItems");
        },
        (o) => true);

      ClearReceiveStreamListCommand = new DelegateCommand<object>(
        (o) =>
        {
          Coordinator.ReceiverCoordinator.StreamList.StreamListItems.Clear();
          NotifyPropertyChanged("ReceiverStreamListItems");
        },
        (o) => true);

      AddCandidateStreamIdCommand = new DelegateCommand<object>(
        (o) =>
        {
          if (!String.IsNullOrEmpty(CandidateStreamId))
          {
            Coordinator.ReceiverCoordinator.StreamList.StreamListItems.Add(new StreamListItem(CandidateStreamId, null));
            CandidateStreamId = "";
          }
          NotifyPropertyChanged("CandidateStreamId");
          NotifyPropertyChanged("ReceiverStreamListItems");
        },
        (o) => true);

      SendCommand = new DelegateCommand<object>(
        async (o) =>
        {
          StateMachine.EnteredSendingMode(StreamMethod);
          var result = await Task.Run(() => Commands.Send(streamCreationProgress, numProcessingItemsProgress, overallProgress, loggingProgress));
          StateMachine.StoppedSending();
        },
        (o) => true);

      SaveAndCloseCommand = new DelegateCommand<object>(
        async (o) =>
        {
          StateMachine.StartedSavingFile();
          var result = await Task.Run(() => Commands.SaveFile());
          StateMachine.StoppedSavingFile();
        },
        (o) => true);

      RenameStreamCommand = new DelegateCommand<object>(
        async (o) =>
        {
          var newStreamName = o.ToString();
          StateMachine.StartedRenamingStream();
          var result = await Task.Run(() => Commands.RenameStream(SelectedStreamItem.StreamId, newStreamName));
          SelectedStreamItem.StreamName = newStreamName;
          StateMachine.StoppedRenamingStream();
        },
        (o) => StateMachine.State != AppState.ActiveRenamingStream);
    }

    #region progress_fns

    private void ProcessStreamCreationProgress(object sender, StreamListItem e)
    {
      Coordinator.SenderCoordinator.StreamList.StreamListItems.Add(e);
      NotifyPropertyChanged("SenderStreamListItems");
    }

    private void ProcessLogProgressUpdate(object sender, DisplayLogItem dli)
    {
      Coordinator.DisplayLog.DisplayLogItems.Add(dli);
      NotifyPropertyChanged("DisplayLogLines");
    }

    private void ProcessOverallProgressUpdate(object sender, int e)
    {
      ProcessingProgress = e;
      NotifyPropertyChanged("ProcessingProgress");
    }
    #endregion
  }
}
