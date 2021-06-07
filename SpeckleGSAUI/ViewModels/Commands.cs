﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using SpeckleCore;
using SpeckleGSAUI.Models;
using SpeckleGSAUI.Utilities;
using SpeckleGSAInterfaces;
using SpeckleGSA;

namespace SpeckleGSAUI.ViewModels
{
  public static class Commands
  {
    public static bool NewFile(TabCoordinator coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      GSA.App.Proxy.NewFile(true);

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
        return await CompleteLogin(coordinator, loggingProgress);
      }
      catch
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "No default account found - press the Login button to login/select an account"));
        return false;
      }
    }

    public static async Task<bool> CompleteLogin(TabCoordinator coordinator, IProgress<MessageEventArgs> loggingProgress)
    {
      var messenger = new ProgressMessenger(loggingProgress);

      var accountName = await SpeckleInterface.SpeckleStreamManager.GetClientName(coordinator.Account.ServerUrl, coordinator.Account.Token, messenger);
      if (!string.IsNullOrEmpty(accountName))
      {
        coordinator.Account.Update(accountName);
      }

      if (coordinator.Account != null && coordinator.Account.IsValid)
      {

        var streamData = await SpeckleInterface.SpeckleStreamManager.GetStreams(coordinator.Account.ServerUrl, coordinator.Account.Token, messenger);
        coordinator.ServerStreamList.StreamListItems.Clear();
        foreach (var sd in streamData)
        {
          coordinator.ServerStreamList.StreamListItems.Add(new StreamListItem(sd.StreamId, sd.Name));
        }

        //This is used to generate URLs for objects and add them to the error context stored in error messages (generated by kits) in the log
        GSA.App.LocalSettings.ServerAddress = coordinator.Account.ServerUrl;

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
            var messenger = new ProgressMessenger(loggingProgress);

            var invalidSidRecords = new List<SidSpeckleRecord>();
            //Since the buckets are stored in the SID tags, but not the stream names, get the stream names
            foreach (var r in coordinator.ReceiverTab.ReceiverSidRecords)
            {
              
              var basicStreamData = await SpeckleInterface.SpeckleStreamManager.GetStream(coordinator.Account.ServerUrl, coordinator.Account.Token, 
                r.StreamId, messenger);

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
            var messenger = new ProgressMessenger(loggingProgress);

            var invalidSidRecords = new List<SidSpeckleRecord>();
            //Since the buckets are stored in the SID tags, but not the stream names, get the stream names
            foreach (var r in coordinator.SenderTab.SenderSidRecords)
            {
              var basicStreamData = await SpeckleInterface.SpeckleStreamManager.GetStream(coordinator.Account.ServerUrl, coordinator.Account.Token, 
                r.StreamId, messenger);
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
          GSA.App.Proxy.OpenFile(openFileDialog.FileName, true);
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
      GSA.App.Settings.TargetLayer = coordinator.ReceiverTab.TargetLayer;
      GSA.App.Settings.Units = UnitEnumToString(coordinator.ReceiverTab.CoincidentNodeUnits);
      GSA.App.Settings.CoincidentNodeAllowance = coordinator.ReceiverTab.CoincidentNodeAllowance;

      var messenger = new ProgressMessenger(loggingProgress);

      coordinator.ReceiverTab.StreamListToSidRecords();

      Func<string, string, SpeckleInterface.IStreamReceiver> streamReceiverCreationFn = ((url, token) => new SpeckleInterface.StreamReceiver(url, token, messenger));

      if (!gsaReceiverCoordinator.Initialize(coordinator.Account.ServerUrl, coordinator.Account.Token, coordinator.ReceiverTab.ReceiverSidRecords,
        streamReceiverCreationFn, loggingProgress, statusProgress, percentageProgress))
      {
        return false;
      }

      gsaReceiverCoordinator.Trigger(null, null);

      //Unlike for sending, the command itself doesn't dispose of the (receiver) coordinator here, as in the case of continuous mode it needs to persist as it needs
      //a constant web socket connection

      coordinator.WriteStreamInfo();

      return true;
    }

    public static async Task<bool> SendInitial(TabCoordinator coordinator, SenderCoordinator gsaSenderCoordinator, 
      IProgress<SidSpeckleRecord> streamCreationProgress, IProgress<SidSpeckleRecord> streamDeletionProgress, 
      IProgress<MessageEventArgs> loggingProgress, IProgress<string> statusProgress, IProgress<double> percentageProgress)
    {
      GSA.App.Settings.TargetLayer = coordinator.SenderTab.TargetLayer;
      GSA.App.LocalSettings.SeparateStreams = (coordinator.SenderTab.StreamContentConfig == StreamContentConfig.ModelWithTabularResults 
        || coordinator.SenderTab.StreamContentConfig == StreamContentConfig.TabularResultsOnly);
      GSA.App.Settings.SendResults = (coordinator.SenderTab.StreamContentConfig == StreamContentConfig.ModelWithEmbeddedResults 
        || coordinator.SenderTab.StreamContentConfig == StreamContentConfig.ModelWithTabularResults 
        || coordinator.SenderTab.StreamContentConfig == StreamContentConfig.TabularResultsOnly);
      GSA.App.LocalSettings.SendOnlyResults = (coordinator.SenderTab.StreamContentConfig == StreamContentConfig.TabularResultsOnly);

      UpdateResultSettings(coordinator.SenderTab.ResultSettings.ResultSettingItems.Where(rsi => rsi.Selected).ToList(), 
        coordinator.SenderTab.LoadCaseList);
      coordinator.SenderTab.SetDocumentName(GSA.App.Proxy.GetTitle());

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
      if (coordinator.FileStatus == GsaLoadedFileType.NewFile)
      {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        if (openFileDialog.ShowDialog() == true)
        {
          GSA.App.Proxy.SaveAs(openFileDialog.FileName);
        }
      }
      else if (coordinator.FileStatus == GsaLoadedFileType.ExistingFile)
      {
        GSA.App.Proxy.SaveAs(coordinator.FilePath);
      }
      return true;
    }

    public static async Task<bool> RenameStream(TabCoordinator coordinator, string streamId, string newStreamName, IProgress<MessageEventArgs> loggingProgress)
    {
      var messenger = new ProgressMessenger(loggingProgress);

      var changed = await SpeckleInterface.SpeckleStreamManager.UpdateStreamName(coordinator.Account.ServerUrl, coordinator.Account.Token, streamId, newStreamName, messenger);

      return changed;
      /*
      if (changed)
      {
        coordinator.SenderTab.ChangeSidRecordStreamName(streamId, newStreamName);
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Changed name of the stream to " + newStreamName));
        return true;
      }
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Unable to change the name of the stream to " + newStreamName));
      return false;
      */
    }

    public static async Task<bool> CloneStream(TabCoordinator coordinator, string streamId, IProgress<MessageEventArgs> loggingProgress)
    {
      var messenger = new ProgressMessenger(loggingProgress);

      var clonedStreamId = await SpeckleInterface.SpeckleStreamManager.CloneStream(coordinator.Account.ServerUrl, coordinator.Account.Token, streamId, messenger);

      return (!string.IsNullOrEmpty(clonedStreamId));
      /*
      if (string.IsNullOrEmpty(clonedStreamId))
      {
        loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Error, "Unable to clone " + streamId));
        return false;
      }
      loggingProgress.Report(new MessageEventArgs(MessageIntent.Display, MessageLevel.Information, "Cloned to: " + clonedStreamId));
      return true;
      */
    }

    private static bool UpdateResultSettings(List<ResultSettingItem> resultsToSend, string loadCaseString)
    {
      if (resultsToSend == null || resultsToSend.Count() == 0 || string.IsNullOrEmpty(loadCaseString))
      {
        return false;
      }

      //Prepare the cache for the ability to parse the load case string
      var initialData = GSA.App.Proxy.GetGwaData(GSA.App.LocalCache.KeywordsForLoadCaseExpansion, false);
      for (int i = 0; i < initialData.Count(); i++)
      {
        var applicationId = (string.IsNullOrEmpty(initialData[i].ApplicationId)) ? null : initialData[i].ApplicationId;
        GSA.App.Cache.Upsert(
          initialData[i].Keyword,
          initialData[i].Index,
          initialData[i].GwaWithoutSet,
          streamId: initialData[i].StreamId,
          applicationId: applicationId,
          gwaSetCommandType: initialData[i].GwaSetType);
      }

      var resultCases = GSA.App.LocalCache.ExpandLoadCasesAndCombinations(loadCaseString);
      if (resultCases == null || resultCases.Count() == 0)
      {
        return false;
      }

      GSA.App.Settings.ResultCases = resultCases;

      var selectedResultNames = resultsToSend.Select(rts => rts.Name).ToList();
      GSA.App.Settings.NodalResults = ExtractResultParams(ref Result.NodalResultMap, selectedResultNames);
      GSA.App.Settings.Element1DResults = ExtractResultParams(ref Result.Element1DResultMap, selectedResultNames);
      GSA.App.Settings.Element2DResults = ExtractResultParams(ref Result.Element2DResultMap, selectedResultNames);
      GSA.App.Settings.MiscResults = ExtractResultParams(ref Result.MiscResultMap, selectedResultNames);

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