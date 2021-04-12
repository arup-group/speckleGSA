using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SpeckleCore;
using SpeckleGSA.UI.Models;
using SpeckleGSA.UI.Utilities;
using SpeckleGSAInterfaces;

namespace SpeckleGSA.UI.ViewModels
{
  public static class Commands
  {
    public static bool NewFile(CoordinatorForUI coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      GSA.GsaApp.gsaProxy.NewFile(true);

      if (coordinator.Account.EmailAddress != null && coordinator.Account.ServerUrl != null)
      {
        GSA.GetSpeckleClients(coordinator.Account.EmailAddress, coordinator.Account.ServerUrl);
      }

      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Created new file."));

      return true;
    }

    public static async Task<bool> InitialLoadAsync(CoordinatorForUI coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      coordinator.Init();
      try
      {
        //This will throw an exception if there is no default account
        var account = LocalContext.GetDefaultAccount();
        if (account != null)
        {
          coordinator.Account = new SpeckleAccountForUI("", account.RestApi, account.Email, account.Token);
        }
      }
      catch
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "No default account found - press the Login button to login/select an account"));
      }

      try
      {
        var accountName = await SpeckleInterface.SpeckleStreamManager.GetClientName(coordinator.Account.ServerUrl, coordinator.Account.Token);
        if (!string.IsNullOrEmpty(accountName))
        {
          coordinator.Account.ClientName = accountName;
        }
      }
      catch
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "Unable to get name of account"));
      }

      if (coordinator.Account != null && coordinator.Account.IsValid)
      {
        var streamData = await SpeckleInterface.SpeckleStreamManager.GetStreams(coordinator.Account.ServerUrl, coordinator.Account.Token);
        coordinator.ServerStreamList.StreamListItems.Clear();
        foreach (var sd in streamData)
        {
          coordinator.ServerStreamList.StreamListItems.Add(new StreamListItem(sd.StreamId, sd.Name));
        }
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Logged in to default account at: " + coordinator.Account.ServerUrl));
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
        if (!string.IsNullOrEmpty(openFileDialog.FileName))
        {
          coordinator.FilePath = openFileDialog.FileName;
        }
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
          foreach (var receiver in GSA.ReceiverInfo)
          {
            coordinator.ReceiverCoordinatorForUI.StreamList.StreamListItems.Add(new StreamListItem(receiver.StreamId));
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

    public static bool Receive(SpeckleAccountForUI account, List<StreamListItem> streamsToReceive, GSATargetLayer layer, IProgress<StreamListItem> streamCreationProgress,
      IProgress<MessageEventArgs> loggingProgress, IProgress<string> statusProgress, IProgress<double> percentageProgress)
    {
      GSA.GsaApp.gsaSettings.TargetLayer = layer;

      var gsaReceiverCoordinator = new ReceiverCoordinator();
      var messenger = new ProgressMessenger(loggingProgress);

      GSA.GetSpeckleClients(account.EmailAddress, account.ServerUrl);
      if (!GSA.SetSpeckleClients(account.EmailAddress, account.ServerUrl))
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "Error in communicating GSA - please check if the GSA file has been closed down"));
        return false;
      }

      foreach (var str in streamsToReceive)
      {
        GSA.ReceiverInfo.Add(new SidSpeckleRecord(str.StreamId, null));
        gsaReceiverCoordinator.StreamReceivers.Add(str.StreamId, new SpeckleInterface.StreamReceiver(account.ServerUrl, account.Token, messenger));
      }

      gsaReceiverCoordinator.Initialize(loggingProgress, statusProgress, percentageProgress);

      gsaReceiverCoordinator.Trigger(null, null);

      gsaReceiverCoordinator.Dispose();

      return true;
    }

    public static bool Send(SpeckleAccountForUI account, GSATargetLayer layer, StreamContentConfig streamContentConfig, List<ResultSettingItem> resultsToSend, string loadCaseString, 
      IProgress<StreamListItem> streamCreationProgress, IProgress<MessageEventArgs> loggingProgress, IProgress<string> statusProgress, IProgress<double> percentageProgress)
    {
      GSA.GsaApp.gsaSettings.TargetLayer = layer;
      GSA.GsaApp.gsaSettings.SeparateStreams = (streamContentConfig == StreamContentConfig.ModelWithTabularResults || streamContentConfig == StreamContentConfig.TabularResultsOnly);
      GSA.GsaApp.gsaSettings.SendResults = (streamContentConfig == StreamContentConfig.ModelWithEmbeddedResults || streamContentConfig == StreamContentConfig.ModelWithTabularResults 
        || streamContentConfig == StreamContentConfig.TabularResultsOnly);
      GSA.GsaApp.gsaSettings.SendOnlyResults = (streamContentConfig == StreamContentConfig.TabularResultsOnly);
      
      //Sender coordinator is in the SpeckleGSA library, NOT the SpeckleInterface.  The sender coordinator calls the SpeckleInterface methods
      var gsaSenderCoordinator = new SenderCoordinator();  //Coordinates across multiple streams
      
      var messenger = new ProgressMessenger(loggingProgress);


      Func<string, string, SpeckleInterface.IStreamSender> streamSenderCreationFn = (restApi, apiToken) => new SpeckleInterface.StreamSender(restApi, apiToken, messenger);

      gsaSenderCoordinator.Initialize(account.ServerUrl, account.Token, streamSenderCreationFn, loggingProgress, statusProgress, percentageProgress);

      //This needs the cache to be populated, which the above line of code does
      UpdateResultSettings(resultsToSend, loadCaseString);

      GSA.SetSpeckleClients(account.EmailAddress, account.ServerUrl);

      gsaSenderCoordinator.Trigger();

      foreach (KeyValuePair<string, SidSpeckleRecord> sender in GSA.SenderInfo)
      {
        streamCreationProgress.Report(new StreamListItem(sender.Value.StreamId, sender.Key));
      }

      gsaSenderCoordinator.Dispose();

      return true;
    }

    public static bool SaveFile()
    {
      Thread.Sleep(1000);
      return true;
    }

    public static bool RenameStream(string streamId, string newStreamName, IProgress<MessageEventArgs> loggingProgress)
    {
      Thread.Sleep(1000);
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Changed name of the stream to " + newStreamName));
      return true;
    }

    private static bool UpdateResultSettings(List<ResultSettingItem> resultsToSend, string loadCaseString)
    {
      if (resultsToSend == null || resultsToSend.Count() == 0 || string.IsNullOrEmpty(loadCaseString))
      {
        return false;
      }
      var resultCases = GSA.GsaApp.gsaCache.ExpandLoadCasesAndCombinations(loadCaseString);
      if (resultCases == null || resultCases.Count() == 0)
      {
        return false;
      }

      GSA.GsaApp.gsaSettings.ResultCases = resultCases;

      var selectedResultNames = resultsToSend.Select(rts => rts.Name).ToList();
      GSA.GsaApp.gsaSettings.NodalResults = ExtractResultParams(ref Result.NodalResultMap, selectedResultNames);
      GSA.GsaApp.gsaSettings.Element1DResults = ExtractResultParams(ref Result.Element1DResultMap, selectedResultNames);
      GSA.GsaApp.gsaSettings.Element2DResults = ExtractResultParams(ref Result.Element2DResultMap, selectedResultNames);
      GSA.GsaApp.gsaSettings.MiscResults = ExtractResultParams(ref Result.MiscResultMap, selectedResultNames);

      return true;
    }

    private static Dictionary<string, IGSAResultParams> ExtractResultParams(ref Dictionary<string, GsaResultParams> map, List<string> names)
    {
      var resultParams = new Dictionary<string, IGSAResultParams>();
      
      foreach (var n in names.Select(n => n.Trim()))
      {
        var matchingMapName = map.Keys.Select(kn => kn.Trim()).FirstOrDefault(kn => kn.Equals(n, StringComparison.InvariantCultureIgnoreCase));
        if (matchingMapName != null && !string.IsNullOrEmpty(matchingMapName))
        {
          resultParams.Add(matchingMapName, map[matchingMapName]);
        }
      }

      return resultParams;
    }
  }
}
