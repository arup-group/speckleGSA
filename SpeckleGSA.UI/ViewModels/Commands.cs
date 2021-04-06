using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpeckleGSA.UI.Models;

namespace SpeckleGSA.UI.ViewModels
{
  public static class Commands
  {
    public static bool NewFile()
    {
      Thread.Sleep(1000);
      return true;
    }

    public static string OpenFile()
    {
      Thread.Sleep(1000);
      return DataAccess.DataAccess.GetFilePath();
    }

    public static SpeckleAccount Login()
    {
      return DataAccess.DataAccess.GetAccount();
    }

    public static StreamList GetStreamList()
    {
      return DataAccess.DataAccess.GetStreamList();
    }

    public static bool Receive(IProgress<int> numProcessingItemsProgress, IProgress<int> overallProgress, IProgress<DisplayLogItem> loggingProgress)
    {
      Thread.Sleep(1000);
      overallProgress.Report(10);
      loggingProgress.Report(new DisplayLogItem("Received first thing"));
      Thread.Sleep(1000);
      overallProgress.Report(50);
      loggingProgress.Report(new DisplayLogItem("Received second thing - BOO YA!"));
      Thread.Sleep(1000);
      overallProgress.Report(70);
      loggingProgress.Report(new DisplayLogItem("Received third thing"));
      return true;
    }

    public static bool Send(IProgress<StreamListItem> streamCreationProgress, IProgress<int> numProcessingItemsProgress, IProgress<int> overallProgress, IProgress<DisplayLogItem> loggingProgress)
    {
      Thread.Sleep(1000);
      streamCreationProgress.Report(new StreamListItem("Z", "Sendy"));
      overallProgress.Report(10);
      loggingProgress.Report(new DisplayLogItem("Sent first thing"));
      Thread.Sleep(1000);
      overallProgress.Report(50);
      loggingProgress.Report(new DisplayLogItem("Sent second thing - BOO YA!"));
      Thread.Sleep(1000);
      overallProgress.Report(70);
      loggingProgress.Report(new DisplayLogItem("Sent third thing"));
      return true;
    }

    public static bool SaveFile()
    {
      Thread.Sleep(1000);
      return true;
    }

    public static bool RenameStream(string streamId, string newStreamName, IProgress<int> overallProgress, IProgress<DisplayLogItem> loggingProgress)
    {
      Thread.Sleep(1000);
      loggingProgress.Report(new DisplayLogItem("Changed name of the stream to " + newStreamName));
      return true;
    }
  }
}
