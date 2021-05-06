﻿using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SpeckleGSAInterfaces;
using SpeckleUtil;
using Serilog;

namespace SpeckleGSA
{

  public static class GSA
  {
    public static List<IGSAKit> kits = new List<IGSAKit>();
    public static GsaAppResources GsaApp = new GsaAppResources();

    public static Dictionary<string, SidSpeckleRecord> SenderInfo { get; set; }  // [ Stream Name, [ StreamId, Client Id ]
    public static List<SidSpeckleRecord> ReceiverInfo { get; set; }

    public static List<IGSASenderDictionary> SenderDictionaries => kits.Select(k => k.GSASenderObjects).ToList();

    public static bool IsInit;

    public static Dictionary<Type, List<Type>> RxTypeDependencies
    {
      get
      {
        var d = new Dictionary<Type, List<Type>>();
        foreach (var k in kits)
        {
          d = d.Concat(k.RxTypeDependencies.Where(x => !d.Keys.Contains(x.Key))).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        return d;
      }
    }

    public static Dictionary<Type, List<Type>> TxTypeDependencies
    {
      get
      {
        var d = new Dictionary<Type, List<Type>>();
        foreach (var k in kits)
        {
          d = d.Concat(k.TxTypeDependencies.Where(x => !d.Keys.Contains(x.Key))).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        return d;
      }
    }

    public static List<string> Keywords
    {
      get
      {
        var keywords = new List<string>();

        foreach (var k in kits)
        {
          foreach (var kw in k.Keywords)
          {
            if (!keywords.Contains(kw))
            {
              keywords.Add(kw);
            }
          }
        }
        return keywords;
      }
    }

    public static Dictionary<Type, string> RxParallelisableTypes => kits.Select(k => k.RxParallelisableTypes).MergeDictionaries();

    public static void Reset()
    {
      IsInit = false;

      kits = new List<IGSAKit>();
      GsaApp = new GsaAppResources();
    }

    public static void Init(string speckleGsaAppVersion)
    {
      if (IsInit) return;

      SenderInfo = new Dictionary<string, SidSpeckleRecord>();
      ReceiverInfo = new List<SidSpeckleRecord>();

      IsInit = true;

      GSA.GsaApp.gsaMessenger.MessageAdded += GSA.ProcessMessageForLog;

      //Avoid sending telemetry when debugging this code
#if !DEBUG
      GSA.GsaApp.gsaMessenger.MessageAdded += GSA.ProcessMessageForTelemetry;
      GSA.GsaApp.gsaProxy.SetAppVersionForTelemetry(speckleGsaAppVersion);
#endif
      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Linked to GSA.");

      InitialiseKits(out List<string> statusMessages);

      if (statusMessages.Count() > 0)
      {
        foreach (var msg in statusMessages)
        {
          GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, msg);
        }
      }
    }

    public static void ProcessMessageForTelemetry(object sender, MessageEventArgs messageEventArgs)
    {
      if (messageEventArgs.Intent == MessageIntent.Telemetry)
      {
        GsaApp.gsaProxy.SendTelemetry(messageEventArgs.MessagePortions);
        //Also log all telemetry transmissions, although the log entries won't have any additional info (prefixes etc) that the proxy has
        //been coded to add
        Log.Debug("Telemetry: " + string.Join(" ", messageEventArgs.MessagePortions));
      }
    }

    public static void ProcessMessageForLog(object sender, MessageEventArgs messageEventArgs)
    {
      if (messageEventArgs.Intent == MessageIntent.TechnicalLog)
      {
        if (messageEventArgs.Exception == null)
        {
          switch (messageEventArgs.Level)
          {
            case MessageLevel.Debug: Log.Debug(string.Join(" ", messageEventArgs.MessagePortions)); break;
            case MessageLevel.Information: Log.Information(string.Join(" ", messageEventArgs.MessagePortions)); break;
            case MessageLevel.Error: Log.Error(string.Join(" ", messageEventArgs.MessagePortions)); break;
            case MessageLevel.Fatal: Log.Fatal(string.Join(" ", messageEventArgs.MessagePortions)); break;
          }
        }
        else
        {
          switch (messageEventArgs.Level)
          {
            case MessageLevel.Debug: Log.Debug(messageEventArgs.Exception, string.Join(" ", messageEventArgs.MessagePortions)); break;
            case MessageLevel.Information: Log.Information(messageEventArgs.Exception, string.Join(" ", messageEventArgs.MessagePortions)); break;
            case MessageLevel.Error: Log.Error(messageEventArgs.Exception, string.Join(" ", messageEventArgs.MessagePortions)); break;
            case MessageLevel.Fatal: Log.Fatal(messageEventArgs.Exception, string.Join(" ", messageEventArgs.MessagePortions)); break;
          }
        }
      }
    }

    private static void InitialiseKits(out List<string> statusMessages)
    {
      statusMessages = new List<string>();

      var attributeType = typeof(GSAObject);
      var interfaceType = typeof(IGSASpeckleContainer);

      SpeckleInitializer.Initialize();

      //Find all structural types
      var speckleTypes = SpeckleInitializer.GetAssemblies().SelectMany(a => a.GetTypes()
        .Where(t => typeof(SpeckleObject).IsAssignableFrom(t) && !t.IsAbstract)).ToList();

      // Run initialize receiver method in interfacer
      var conversionAssemblies = SpeckleInitializer.GetAssemblies().Where(a => a.GetTypes().Any(t => t.GetInterfaces()
        .Contains(typeof(ISpeckleInitializer))));

      var mappableTypes = new List<Type>();
      foreach (var ass in conversionAssemblies)
      {
        var assemblyTypes = ass.GetTypes();

        //These are the interfaces that are required for this app to recognise it as a GSA Speckle kit
        var requiredInterfaces = new List<Type> { typeof(ISpeckleInitializer) };
        var requiredPropertyInterfaces = new List<Type> { typeof(IGSAKit) };

        Type gsaStatic = null;
        try
        {
          foreach (var t in assemblyTypes)
          {
            var interfaces = t.GetInterfaces();

            if (requiredInterfaces.All(ri => interfaces.Contains(ri) 
              && t.GetProperties().Any(p => requiredPropertyInterfaces.Any(pi => pi == p.PropertyType)) ))
            {
              gsaStatic = t;
              break;
            }
          }

          if (gsaStatic == null)
          {
            continue;
          }
        }
        catch
        {
          //The kits could throw an exception due to an app-specific library not being linked in (e.g.: the Revit SDK).  These libraries aren't of the kind that
          //would contain the static properties searched for anyway, so just continue.
          continue;
        }

        try
        {
          var kit = (IGSAKit)gsaStatic.GetProperty("GsaKit").GetValue(null);
          gsaStatic.GetProperty("AppResources").SetValue(null, GSA.GsaApp);
          kits.Add(kit);
        }
        catch
        {
          GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Error, 
            $"Unable to fully connect to {ass.GetName().Name}.dll. Please check the versions of the kit you have installed.");
        }

        foreach (var t in speckleTypes)
        {
          var methods = Helper.GetExtensionMethods(ass, t, "ToNative");
          if (methods != null && methods.Count() > 0 && !mappableTypes.Contains(t))
          {
            mappableTypes.Add(t);

            if (t.BaseType != null && t.BaseType != typeof(SpeckleObject))
            {
              if (!mappableTypes.Contains(t.BaseType))
              {
                mappableTypes.Add(t.BaseType);
              }
            }
          }
        }
      }
      GSA.GsaApp.Merger.Initialise(mappableTypes);
    }

#region kit_resources

    public static void ClearSenderDictionaries()
    {
      foreach (var sd in SenderDictionaries)
      {
        sd.Clear();
      }
    }

    public static List<SpeckleObject> GetSpeckleObjectsFromSenderDictionaries() => GetAllConvertedGsaObjectsByType()
      .SelectMany(sd => sd.Value).Cast<IGSASpeckleContainer>().Select(c => (SpeckleObject)c.SpeckleObject).ToList();

    public static Dictionary<Type, List<IGSASpeckleContainer>> GetAllConvertedGsaObjectsByType()
    {
      var currentObjects = new Dictionary<Type, List<IGSASpeckleContainer>>();

      foreach (var dict in SenderDictionaries)
      {
        try
        {
          var allObjects = dict.GetAll();
          //Ensure alphabetical order here as this has a bearing on the order of the layers when it's sent, and therefore the order of
          //the layers as displayed in GH.  Note the type names here are the GSA ones (e.g. GSAGravityLoading) not the StructuralClasses ones
          var sortedKeys = allObjects.Keys.OrderBy(k => k.Name);
          foreach (var t in sortedKeys)
          {
            var objsToAdd = allObjects.ContainsKey(t) ? allObjects[t].Select(o => (IGSASpeckleContainer)o).ToList() : new List<IGSASpeckleContainer>();
            if (objsToAdd.Count() > 0)
            {
              if (!currentObjects.ContainsKey(t))
              {
                currentObjects.Add(t, new List<IGSASpeckleContainer>());
              }
              currentObjects[t].AddRange(objsToAdd);
            }
          }
        }
        catch
        {

        }
      }
      return currentObjects;
    }

  #endregion

  #region streamInfo
    public static void RemoveUnusedStreamInfo(List<string> streamNames)
    {
      //Remove any streams that will no longer need to be used - if the "Separate sender streams" item has been toggled, for example
      var senderKeysToRemove = GSA.SenderInfo.Keys.Where(k => !streamNames.Any(sn => sn.Equals(k, StringComparison.InvariantCultureIgnoreCase))).ToList();
      foreach (var k in senderKeysToRemove)
      {
        SenderInfo.Remove(k);
      }
    }
#endregion

  #region File Operations
    /// <summary>
    /// Creates a new GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static void NewFile(string emailAddress, string serverAddress, bool showWindow = true)
    {
      if (!IsInit) return;

      GSA.GsaApp.gsaProxy.NewFile(showWindow);

      if (emailAddress != null && serverAddress != null)
      {
        GetSpeckleClients(emailAddress, serverAddress);
      }

      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Created new file.");
    }

    /// <summary>
    /// Opens an existing GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="path">Absolute path to GSA file</param>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static void OpenFile(string path, string emailAddress, string serverAddress, bool showWindow = true)
    {
      if (!IsInit) return;

      GSA.GsaApp.gsaProxy.OpenFile(path, showWindow);
      if (emailAddress != null && serverAddress != null)
      {
        GetSpeckleClients(emailAddress, serverAddress);
      }

      GSA.GsaApp.gsaMessenger.Message(MessageIntent.Display, MessageLevel.Information, "Opened new file.");
    }

    /// <summary>
    /// Close GSA file.
    /// </summary>
    public static void Close()
    {
      if (!IsInit) return;

      GSA.GsaApp.gsaProxy.Close();
      SenderInfo.Clear();
      ReceiverInfo.Clear();
    }
  #endregion

  #region Speckle Client
    /// <summary>
    /// Extracts sender and receiver streams associated with the account.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static bool GetSpeckleClients(string emailAddress, string serverAddress)
    {
      SenderInfo.Clear();
      ReceiverInfo.Clear();

      try
      {
        string key = emailAddress + "&" + serverAddress.Replace(':', '&');

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
            SenderInfo[senders[i]] = new SidSpeckleRecord(senders[i + 1], senders[i], senders[i + 2]);
          }
        }

        if (receiverList != null && !string.IsNullOrEmpty(receiverList[1]))
        {
          string[] receivers = receiverList[1].Split(new char[] { '&' });

          for (int i = 0; i < receivers.Length; i += 2)
          {
            ReceiverInfo.Add(new SidSpeckleRecord(receivers[i], receivers[i + 1]));
          }
        }
        return true;
      }
      catch
      {
        // If fail to read, clear client SIDs
        SenderInfo.Clear();
        ReceiverInfo.Clear();
        return SetSpeckleClients(emailAddress, serverAddress);
      }
    }

    /// <summary>
    /// Writes sender and receiver streams associated with the account.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static bool SetSpeckleClients(string emailAddress, string serverAddress)
    {
      string key = emailAddress + "&" + serverAddress.Replace(':', '&');
      string res = GSA.GsaApp.gsaProxy.GetTopLevelSid();

      List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
              .Select(m => m.Value.Split(new char[] { ':' }))
              .Where(s => s.Length == 2)
              .ToList();

      sids.RemoveAll(S => S[0] == "SpeckleSender&" + key || S[0] == "SpeckleReceiver&" + key || string.IsNullOrEmpty(S[1]));

      List<string> senderList = new List<string>();
      foreach (KeyValuePair<string, SidSpeckleRecord> kvp in SenderInfo)
      {
        senderList.Add(kvp.Key);
        senderList.Add(kvp.Value.StreamId);
        senderList.Add(kvp.Value.ClientId);
      }

      List<string> receiverList = new List<string>();
      foreach (var t in ReceiverInfo)
      {
        receiverList.Add(t.StreamId);
        receiverList.Add(t.StreamName);
      }

      if (senderList.Count() > 0)
      {
        sids.Add(new string[] { "SpeckleSender&" + key, string.Join("&", senderList) });
      }
      if (receiverList.Count() > 0)
      {
        sids.Add(new string[] { "SpeckleReceiver&" + key, string.Join("&", receiverList) });
      }

      string sidRecord = "";
      foreach (string[] s in sids)
      {
        sidRecord += "{" + s[0] + ":" + s[1] + "}";
      }

      return GSA.GsaApp.gsaProxy.SetTopLevelSid(sidRecord);
    }
#endregion

#region Document Properties

    /// <summary>
    /// Extracts the base properties of the Speckle stream.
    /// </summary>
    /// <returns>Base property dictionary</returns>
    public static Dictionary<string, object> GetBaseProperties()
    {
      var baseProps = new Dictionary<string, object>();

      baseProps["units"] = GSA.GsaApp.Settings.Units.LongUnitName();
      // TODO: Add other units

      var tolerances = GSA.GsaApp.gsaProxy.GetTolerances();

      var lengthTolerances = new List<double>() {
                Convert.ToDouble(tolerances[3]), // edge
                Convert.ToDouble(tolerances[5]), // leg_length
                Convert.ToDouble(tolerances[7])  // memb_cl_dist
            };

      var angleTolerances = new List<double>(){
                Convert.ToDouble(tolerances[4]), // angle
                Convert.ToDouble(tolerances[6]), // meemb_orient
            };

      baseProps["tolerance"] = lengthTolerances.Max().ConvertUnit("m", GSA.GsaApp.Settings.Units);
      baseProps["angleTolerance"] = angleTolerances.Max().ToRadians();

      return baseProps;
    }

#endregion

#region Views

    /// <summary>
    /// Update GSA case and task links. This should be called at the end of changes.
    /// </summary>
    public static void UpdateCasesAndTasks()
    {
      GSA.GsaApp.gsaProxy.UpdateCasesAndTasks();
    }
#endregion
  }
}
