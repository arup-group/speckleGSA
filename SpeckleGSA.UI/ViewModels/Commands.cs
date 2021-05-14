﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using Microsoft.Win32;
using SpeckleCore;
using SpeckleGSA.UI.Models;
using SpeckleGSA.UI.Utilities;
using SpeckleGSAInterfaces;
using System.IO;

namespace SpeckleGSA.UI.ViewModels
{
  public static class Commands
  {
    public static bool NewFile(TabCoordinator coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      GSA.GsaApp.gsaProxy.NewFile(true);

      coordinator.ReceiverTab.ReceiverSidRecords.Clear();
      coordinator.SenderTab.SenderSidRecords.Clear();

      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Created new file."));

      return true;
    }

    public static async Task<bool> InitialLoad(TabCoordinator coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      coordinator.Init();
      try
      {
        //This will throw an exception if there is no default account
        var account = LocalContext.GetDefaultAccount();
        if (account != null)
        {
          coordinator.Account = new SpeckleAccountForUI(account.RestApi, account.Email, account.Token);
        }
      }
      catch
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "No default account found - press the Login button to login/select an account"));
      }

      return await CompleteLogin(coordinator, loggingProgress);
    }

    public static async Task<bool> CompleteLogin(TabCoordinator coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      try
      {
        var accountName = await SpeckleInterface.SpeckleStreamManager.GetClientName(coordinator.Account.ServerUrl, coordinator.Account.Token);
        if (!string.IsNullOrEmpty(accountName))
        {
          coordinator.Account.Update(accountName);
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
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Logged into account at: " + coordinator.Account.ServerUrl));
        return true;
      }
      else
      {
        return false;
      }
    }

    public static async Task<bool> ReadSavedStreamInfo(TabCoordinator coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      if (coordinator.FileStatus == GsaLoadedFileType.ExistingFile && coordinator.Account != null && coordinator.Account.IsValid)
      {
        var retrieved = coordinator.RetrieveSavedSidStreamRecords();
        if (retrieved)
        {
          if (coordinator.ReceiverTab.ReceiverSidRecords.Count() > 0)
          {
            var invalidSidRecords = new List<SidSpeckleRecord>();
            //Since the buckets are stored in the SID tags, but not the stream names, get the stream names
            foreach (var r in coordinator.ReceiverTab.ReceiverSidRecords)
            {
              
              var basicStreamData = await SpeckleInterface.SpeckleStreamManager.GetStream(coordinator.Account.ServerUrl, coordinator.Account.Token, r.StreamId);
              if (basicStreamData == null)
              {
                invalidSidRecords.Add(r);
              }
              else if (!string.IsNullOrEmpty(basicStreamData.Name))
              {
                r.SetName(basicStreamData.Name);
              }
            }
            invalidSidRecords.ForEach(r => coordinator.ReceiverTab.RemoveSidSpeckleRecord(r));
            coordinator.ReceiverTab.SidRecordsToStreamList();

            loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Found streams from the same server stored in file for receiving: "
               + string.Join(", ", coordinator.ReceiverTab.ReceiverSidRecords.Select(r => r.StreamId))));
          }
          if (coordinator.SenderTab.SenderSidRecords.Count() > 0)
          {
            var invalidSidRecords = new List<SidSpeckleRecord>();
            //Since the buckets are stored in the SID tags, but not the stream names, get the stream names
            foreach (var r in coordinator.SenderTab.SenderSidRecords)
            {
              var basicStreamData = await SpeckleInterface.SpeckleStreamManager.GetStream(coordinator.Account.ServerUrl, coordinator.Account.Token, r.StreamId);
              if (basicStreamData == null)
              {
                invalidSidRecords.Add(r);
              }
              else if (!string.IsNullOrEmpty(basicStreamData.Name))
              {
                r.SetName(basicStreamData.Name);
              }
            }
            invalidSidRecords.ForEach(r => coordinator.SenderTab.RemoveSidSpeckleRecord(r));
            coordinator.SenderTab.SidRecordsToStreamList();

            loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Found streams from the same server stored in file for sending: "
               + string.Join(", ", coordinator.SenderTab.SenderSidRecords.Select(r => r.StreamId))));
          }
        }
        return retrieved;
      }
      return true;
    }

    public static bool OpenFile(TabCoordinator coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      var openFileDialog = new OpenFileDialog();
      if (openFileDialog.ShowDialog() == true)
      {
        try
        {
          GSA.GsaApp.gsaProxy.OpenFile(openFileDialog.FileName, true);
        }
        catch (Exception ex)
        {
          loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "Unable to load " + openFileDialog.FileName + " - refer to logs for more information"));
          loggingProgress.Report(new MessageEventArgs(MessageIntent.TechnicalLog, MessageLevel.Error, ex, "Unable to load file"));
          return false;
        }
        if (!string.IsNullOrEmpty(openFileDialog.FileName))
        {
          coordinator.FilePath = openFileDialog.FileName;
        }

        coordinator.FileStatus = GsaLoadedFileType.ExistingFile;
        return true;
      }
      else
      {
        return false;
      }
    }

    public static async Task<bool> GetStreamList(TabCoordinator coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      return await CompleteLogin(coordinator, loggingProgress);
    }

    public static bool Receive(TabCoordinator coordinator, ReceiverCoordinator gsaReceiverCoordinator, IProgress<SidSpeckleRecord> streamCreationProgress,
      IProgress<MessageEventArgs> loggingProgress, IProgress<string> statusProgress, IProgress<double> percentageProgress)
    {
      GSA.GsaApp.gsaSettings.TargetLayer = coordinator.ReceiverTab.TargetLayer;
      GSA.GsaApp.gsaSettings.Units = UnitEnumToString(coordinator.ReceiverTab.CoincidentNodeUnits);
      GSA.GsaApp.gsaSettings.CoincidentNodeAllowance = coordinator.ReceiverTab.CoincidentNodeAllowance;

      var messenger = new ProgressMessenger(loggingProgress);

      coordinator.ReceiverTab.StreamListToSidRecords();

      Func<string, string, SpeckleInterface.IStreamReceiver> streamReceiverCreationFn = ((url, token) => new SpeckleInterface.StreamReceiver(url, token, messenger));

      gsaReceiverCoordinator.Initialize(coordinator.Account.ServerUrl, coordinator.Account.Token, coordinator.ReceiverTab.ReceiverSidRecords,
        streamReceiverCreationFn, loggingProgress, statusProgress, percentageProgress);

      gsaReceiverCoordinator.Trigger(null, null);

      gsaReceiverCoordinator.Dispose();

      coordinator.WriteStreamInfo();

      return true;
    }

    public static async Task<bool> SendInitial(TabCoordinator coordinator, SenderCoordinator gsaSenderCoordinator, 
      IProgress<SidSpeckleRecord> streamCreationProgress, IProgress<SidSpeckleRecord> streamDeletionProgress, 
      IProgress<MessageEventArgs> loggingProgress, IProgress<string> statusProgress, IProgress<double> percentageProgress)
    {
      GSA.GsaApp.gsaSettings.TargetLayer = coordinator.SenderTab.TargetLayer;
      GSA.GsaApp.gsaSettings.SeparateStreams = (coordinator.SenderTab.StreamContentConfig == StreamContentConfig.ModelWithTabularResults 
        || coordinator.SenderTab.StreamContentConfig == StreamContentConfig.TabularResultsOnly);
      GSA.GsaApp.gsaSettings.SendResults = (coordinator.SenderTab.StreamContentConfig == StreamContentConfig.ModelWithEmbeddedResults 
        || coordinator.SenderTab.StreamContentConfig == StreamContentConfig.ModelWithTabularResults 
        || coordinator.SenderTab.StreamContentConfig == StreamContentConfig.TabularResultsOnly);
      GSA.GsaApp.gsaSettings.SendOnlyResults = (coordinator.SenderTab.StreamContentConfig == StreamContentConfig.TabularResultsOnly);

      UpdateResultSettings(coordinator.SenderTab.ResultSettings.ResultSettingItems.Where(rsi => rsi.Selected).ToList(), 
        coordinator.SenderTab.LoadCaseList);
      coordinator.SenderTab.SetDocumentName(GSA.GsaApp.gsaProxy.GetTitle());

      var messenger = new ProgressMessenger(loggingProgress);

      Func<string, string, SpeckleInterface.IStreamSender> streamSenderCreationFn = ((url, token) => new SpeckleInterface.StreamSender(url, token, messenger));

      gsaSenderCoordinator.Initialize(coordinator.Account.ServerUrl, coordinator.Account.Token, coordinator.SenderTab.SenderSidRecords, 
        streamSenderCreationFn, loggingProgress, statusProgress, percentageProgress, streamCreationProgress, streamDeletionProgress);

      await gsaSenderCoordinator.Trigger();

      coordinator.WriteStreamInfo();

      return true;
    }

    public static async Task<bool> SendTriggered(SenderCoordinator gsaSenderCoordinator)
    {
      await gsaSenderCoordinator.Trigger();

      return true;
    }

    public static bool SaveFile(TabCoordinator coordinator)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      if (openFileDialog.ShowDialog() == true)
      {
        coordinator.WriteStreamInfo();
        GSA.GsaApp.gsaProxy.SaveAs(openFileDialog.FileName);
      }
      return true;
    }

    public static async Task<bool> RenameStream(TabCoordinator coordinator, string streamId, string newStreamName, IProgress<MessageEventArgs> loggingProgress)
    {
      var changed = await SpeckleInterface.SpeckleStreamManager.UpdateStreamName(coordinator.Account.ServerUrl, coordinator.Account.Token, streamId, newStreamName);
      if (changed)
      {
        coordinator.SenderTab.ChangeSidRecordStreamName(streamId, newStreamName);
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Changed name of the stream to " + newStreamName));
        return true;
      }
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Unable to change the name of the stream to " + newStreamName));
      return false;
    }

    public static async Task<bool> CloneStream(TabCoordinator coordinator, string streamId, IProgress<MessageEventArgs> loggingProgress)
    {
      var clonedStreamId = await SpeckleInterface.SpeckleStreamManager.CloneStream(coordinator.Account.ServerUrl, coordinator.Account.Token, streamId);

      if (string.IsNullOrEmpty(clonedStreamId))
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "Unable to clone " + streamId));
        return false;
      }
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Cloned to: " + clonedStreamId));
      return true;
    }

    private static bool UpdateResultSettings(List<ResultSettingItem> resultsToSend, string loadCaseString)
    {
      if (resultsToSend == null || resultsToSend.Count() == 0 || string.IsNullOrEmpty(loadCaseString))
      {
        return false;
      }

      //Prepare the cache for the ability to parse the load case string
      var initialData = GSA.GsaApp.gsaProxy.GetGwaData(GSA.GsaApp.gsaCache.KeywordsForLoadCaseExpansion, false);
      for (int i = 0; i < initialData.Count(); i++)
      {
        var applicationId = (string.IsNullOrEmpty(initialData[i].ApplicationId)) ? null : initialData[i].ApplicationId;
        GSA.GsaApp.gsaCache.Upsert(
          initialData[i].Keyword,
          initialData[i].Index,
          initialData[i].GwaWithoutSet,
          streamId: initialData[i].StreamId,
          applicationId: applicationId,
          gwaSetCommandType: initialData[i].GwaSetType);
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
    
    private static string UnitEnumToString(GsaUnit unit)
    {
      switch(unit)
      {
        case GsaUnit.Inches: return "in";
        case GsaUnit.Metres: return "m";
        default: return "mm";
      }
    }
  }
}
