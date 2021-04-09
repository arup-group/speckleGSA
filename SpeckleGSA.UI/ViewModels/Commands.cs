using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SpeckleCore;
using SpeckleGSA.UI.Models;
using SpeckleGSAInterfaces;
using SpeckleInterface;

namespace SpeckleGSA.UI.ViewModels
{
  public static class Commands
  {
    public static bool NewFile()
    {
      Thread.Sleep(1000);
      return true;
    }

    public static async Task<bool> InitialLoadAsync(CoordinatorForUI coordinator, IProgress<DisplayLogItem> loggingProgress)
    {
      coordinator.Init();
      try
      {
        //This will throw an exception if there is no default account
        var account = LocalContext.GetDefaultAccount();
        if (account != null)
        {
          coordinator.Account = new SpeckleAccountForUI("", account.RestApi, account.Email, account.Token);

          //loggingProgress.Report(new DisplayLogItem("Logged in to default account at: " + account.RestApi));
          GSA.GsaApp.gsaMessenger.Message(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Information, "Logged in to default account at: " + account.RestApi);
        }
      }
      catch
      {
        GSA.GsaApp.gsaMessenger.Message(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error, "No default account found - press the Login button to login/select an account");
      }

      try
      {
        var accountName = await SpeckleStreamManager.GetClientName(coordinator.Account.ServerUrl, coordinator.Account.Token);
        if (!string.IsNullOrEmpty(accountName))
        {
          coordinator.Account.ClientName = accountName;
        }
      }
      catch
      {
        GSA.GsaApp.gsaMessenger.Message(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error, "Unable to get name of account");
      }

      if (coordinator.Account != null && coordinator.Account.IsValid)
      {
        var streamData = await SpeckleStreamManager.GetStreams(coordinator.Account.ServerUrl, coordinator.Account.Token);
        coordinator.ServerStreamList.StreamListItems.Clear();
        foreach (var sd in streamData)
        {
          coordinator.ServerStreamList.StreamListItems.Add(new StreamListItem(sd.StreamId, sd.Name));
        }
        return true;
      }
      else
      {
        return false;
      }
    }

    public static bool OpenFile(CoordinatorForUI coordinator)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      if (openFileDialog.ShowDialog() == true)
      {
        GSA.GsaApp.gsaProxy.OpenFile(openFileDialog.FileName, true);
        if (coordinator.Account.EmailAddress != null && coordinator.Account.ServerUrl != null)
        {
          GSA.GetSpeckleClients(coordinator.Account.EmailAddress, coordinator.Account.ServerUrl);
        }

        if (GSA.SenderInfo != null)
        {
          coordinator.SenderCoordinatorForUI.StreamList.SeletedStreamListItem = null;
          foreach (KeyValuePair<string, SidSpeckleRecord> sender in GSA.SenderInfo)
          {
            coordinator.SenderCoordinatorForUI.StreamList.StreamListItems.Add(new StreamListItem(sender.Value.StreamId, sender.Value.StreamName));
          }
        }
        if (GSA.ReceiverInfo != null)
        {
          foreach (Tuple<string, string> receiver in GSA.ReceiverInfo)
          {
            coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems.Add(new StreamListItem(receiver.Item1));
          }
        }

        coordinator.FileStatus = GsaLoadedFileType.ExistingFile;
        return true;
      }
      else
      {
        return false;
      }
    }

    public static SpeckleAccountForUI Login()
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

    public static async Task<bool> SendAsync(SpeckleAccountForUI account, GSATargetLayer layer, IProgress<StreamListItem> streamCreationProgress, IProgress<int> numProcessingItemsProgress, IProgress<int> overallProgress, IProgress<DisplayLogItem> loggingProgress)
    {
      GSA.GsaApp.gsaSettings.TargetLayer = layer;
      //Sender coordinator is in the SpeckleGSA library, NOT the SpeckleInterface.  The sender coordinator calls the SpeckleInterface methods
      var gsaSenderCoordinator = new SenderCoordinator();
      var statusMessages = await gsaSenderCoordinator.Initialize(account.ServerUrl, account.Token, (restApi, apiToken) => new StreamSender(restApi, apiToken, GSA.GsaApp.gsaMessenger));
      GSA.SetSpeckleClients(account.EmailAddress, account.ServerUrl);

      gsaSenderCoordinator.Trigger();

      foreach (KeyValuePair<string, SidSpeckleRecord> sender in GSA.SenderInfo)
      {
        streamCreationProgress.Report(new StreamListItem(sender.Value.StreamId, sender.Key));
      }

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
