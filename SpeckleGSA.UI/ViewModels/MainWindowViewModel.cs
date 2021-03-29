using SpeckleGSA.UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeckleGSA.UI.DataAccess;
using System.Collections.ObjectModel;
using SpeckleGSA.UI.Utilities;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;

namespace SpeckleGSA.UI.ViewModels
{
  public class MainWindowViewModel : INotifyPropertyChanged
  {
    //INotifyPropertyChanged aspects
    public event PropertyChangedEventHandler PropertyChanged;
    protected void NotifyPropertyChanged(String info) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));

    public Coordinator Coordinator { get; set; }

    public double ProcessingProgress { get; set; }

    public DelegateCommand<object> ConnectToServerCommand { get; }
    public DelegateCommand<object> ReceiveSelectedStreamCommand { get; }
    public DelegateCommand<object> NewFileCommand { get; }
    public DelegateCommand<object> OpenFileCommand { get; }
    public DelegateCommand<object> SaveAndCloseCommand { get; }

    public string CurrentlyOpenFileName { get => Coordinator.FilePath; }

    public List<string> DisplayLogLines { get => Coordinator.DisplayLog.DisplayLogItems.Select(i => string.Join(" - ", i.TimeStamp.ToString("dd/MM/yyyy HH:mm:ss"), i.Description)).ToList(); }

    public List<StreamListItem> ServerStreamListItems { get => Coordinator.ServerStreamList.StreamListItems; }
    public SpeckleAccount Account { get => Coordinator.Account; }

    public MainWindowViewModel()
    {
      Coordinator = new Coordinator
      {
        ServerStreamList = UI.DataAccess.DataAccess.GetStreamList(),
        Account = UI.DataAccess.DataAccess.GetAccount(),
        DisplayLog = new DisplayLog()
      };

      ConnectToServerCommand = new DelegateCommand<object>(
          async (o) =>
          {
            var overallProgress = new Progress<int>();
            var loggingProgress = new Progress<string>();
            overallProgress.ProgressChanged += ProcessOverallProgressUpdate;
            loggingProgress.ProgressChanged += ProcessLogProgressUpdate;
            bool result = await Task.Run(() =>
            {
              Thread.Sleep(1000);
              ((IProgress<int>)overallProgress).Report(10);
              ((IProgress<string>)loggingProgress).Report("Done first thing");
              Thread.Sleep(1000);
              ((IProgress<int>)overallProgress).Report(50);
              ((IProgress<string>)loggingProgress).Report("Done second thing - BOO YA!");
              Thread.Sleep(1000);
              ((IProgress<int>)overallProgress).Report(70);
              ((IProgress<string>)loggingProgress).Report("Done third thing");
              return true;
            });

            Coordinator.ServerStreamList = UI.DataAccess.DataAccess.GetStreamList();
            Coordinator.Account = UI.DataAccess.DataAccess.GetAccount();
            NotifyPropertyChanged("ServerStreamListItems");
            NotifyPropertyChanged("Account");
          },
          (o) => true);
    }

    private void ProcessLogProgressUpdate(object sender, string e)
    {
      Coordinator.DisplayLog.DisplayLogItems.Add(new DisplayLogItem() { Description = e, TimeStamp = DateTime.Now });
      NotifyPropertyChanged("DisplayLogLines");
    }

    private void ProcessOverallProgressUpdate(object sender, int e)
    {
      ProcessingProgress = e;
      NotifyPropertyChanged("ProcessingProgress");
    }
  }
}
