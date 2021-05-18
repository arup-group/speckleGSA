using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SpeckleCore;

namespace SpeckleGSA.UI.Models
{
  public class TabCoordinator
  {
    public SpeckleAccountForUI Account { get; set; }
    public GsaLoadedFileType FileStatus { get; set; }
    public string FilePath { get; set; }
    public StreamList ServerStreamList { get; set; } = new StreamList();
    public DisplayLog DisplayLog { get; set; } = new DisplayLog();

    public LoggingMinimumLevel LoggingMinimumLevel { get; set; } = LoggingMinimumLevel.Information;
    public bool VerboseErrorInformation { get; set; } = false;

    public ReceiverTab ReceiverTab { get; set; } = new ReceiverTab();
    public SenderTab SenderTab { get; set; } = new SenderTab();
    public Version RunningVersion { get => getRunningVersion(); }

    #region app_resources

    //The SpeckleStreamManager is also used, but that is a static class so no need to store it as a member here
    public SenderCoordinator gsaSenderCoordinator;
    public ReceiverCoordinator gsaReceiverCoordinator;

    public Timer triggerTimer;

    #endregion

    public void Init()
    {
      GSA.Init(getRunningVersion().ToString());
      SpeckleInitializer.Initialize();
      LocalContext.Init();

      //This will create the logger
      GSA.GsaApp.gsaSettings.LoggingMinimumLevel = 4;  //Debug

      gsaSenderCoordinator = new SenderCoordinator();
      gsaReceiverCoordinator = new ReceiverCoordinator();
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

    internal bool RetrieveSavedSidStreamRecords()
    {
      ReceiverTab.ReceiverSidRecords.Clear();
      SenderTab.SenderSidRecords.Clear();

      try
      {
        string key = Account.EmailAddress + "&" + Account.ServerUrl.Replace(':', '&');

        string res = GSA.GsaApp.gsaProxy.GetTopLevelSid();

        if (res == "")
        {
          return true;
        }

        List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
                .Select(m => m.Value.Split(new char[] { ':' }))
                .Where(s => s.Length == 2)
                .ToList();

        string[] senderList = sids.Where(s => s[0] == "SpeckleSender&" + key).FirstOrDefault();
        string[] receiverList = sids.Where(s => s[0] == "SpeckleReceiver&" + key).FirstOrDefault();

        if (senderList != null && !string.IsNullOrEmpty(senderList[1]))
        {
          string[] senders = senderList[1].Split(new char[] { '&' });

          for (int i = 0; i < senders.Length; i += 3)
          {
            SenderTab.SenderSidRecords.Add(new SidSpeckleRecord(senders[i + 1], senders[i], senders[i + 2]));
          }
        }

        if (receiverList != null && !string.IsNullOrEmpty(receiverList[1]))
        {
          string[] receivers = receiverList[1].Split(new char[] { '&' });

          for (int i = 0; i < receivers.Length; i += 2)
          {
            ReceiverTab.ReceiverSidRecords.Add(new SidSpeckleRecord(receivers[i], receivers[i + 1]));
          }
        }

        ReceiverTab.SidRecordsToStreamList();
        SenderTab.SidRecordsToStreamList();
        return true;
      }
      catch
      {
        return false;
      }
      throw new NotImplementedException();
    }

    internal bool WriteStreamInfo()
    {
      string key = Account.EmailAddress + "&" + Account.ServerUrl.Replace(':', '&');
      string res = GSA.GsaApp.gsaProxy.GetTopLevelSid();

      List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
              .Select(m => m.Value.Split(new char[] { ':' }))
              .Where(s => s.Length == 2)
              .ToList();

      sids.RemoveAll(S => S[0] == "SpeckleSender&" + key || S[0] == "SpeckleReceiver&" + key || string.IsNullOrEmpty(S[1]));

      if (SenderTab.SenderSidRecords != null)
      {
        var senderList = new List<string>();
        foreach (var si in SenderTab.SenderSidRecords)
        {
          senderList.AddRange(new[] { si.Bucket, si.StreamId, si.ClientId });
        }
        if (senderList.Count() > 0)
        {
          sids.Add(new string[] { "SpeckleSender&" + key, string.Join("&", senderList) });
        }
      }

      if (ReceiverTab.ReceiverSidRecords != null)
      {
        var receiverList = new List<string>();
        foreach (var si in ReceiverTab.ReceiverSidRecords)
        {
          receiverList.AddRange(new[] { si.StreamId, si.Bucket });
        }
        if (receiverList.Count() > 0)
        {
          sids.Add(new string[] { "SpeckleReceiver&" + key, string.Join("&", receiverList) });
        }
      }

      string sidRecord = "";
      foreach (string[] s in sids)
      {
        sidRecord += "{" + s[0] + ":" + s[1] + "}";
      }

      return GSA.GsaApp.gsaProxy.SetTopLevelSid(sidRecord);
    }
  }
}
