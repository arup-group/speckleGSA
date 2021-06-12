using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using SpeckleGSAInterfaces;
using SpeckleUtil;
using Serilog;

namespace SpeckleGSA
{

  public static class GSA
  {
    public static List<IGSAKit> kits = new List<IGSAKit>();
    public static IGSAAppResources GsaApp { get => App; } 
    public static IGSALocalAppResources App { get; set; } = new GsaAppResources();
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
    }

    public static void Init(string speckleGsaAppVersion)
    {
      if (IsInit) return;

      kits = new List<IGSAKit>();

      IsInit = true;

      GSA.App.LocalMessenger.MessageAdded += GSA.ProcessMessageForLog;

      //Avoid sending telemetry when debugging this code
#if !DEBUG
      GSA.App.LocalMessenger.MessageAdded += GSA.ProcessMessageForTelemetry;
      GSA.App.LocalProxy.SetAppVersionForTelemetry(speckleGsaAppVersion);
#endif

      InitialiseKits(out List<string> statusMessages);

      if (statusMessages.Count() > 0)
      {
        foreach (var msg in statusMessages)
        {
          GSA.App.Messenger.Message(MessageIntent.Display, MessageLevel.Information, msg);
        }
      }
    }

    public static void ProcessMessageForTelemetry(object sender, MessageEventArgs messageEventArgs)
    {
      if (messageEventArgs.Intent == MessageIntent.Telemetry)
      {
        App.LocalProxy.SendTelemetry(messageEventArgs.MessagePortions);
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
            case MessageLevel.Error: 
              Log.Error(messageEventArgs.Exception, string.Join(" ", messageEventArgs.MessagePortions));
              if (messageEventArgs.Exception.InnerException != null)
              {
                Log.Error(messageEventArgs.Exception.InnerException, "Inner exception");
              }
              break;
            case MessageLevel.Fatal: 
              Log.Fatal(messageEventArgs.Exception, string.Join(" ", messageEventArgs.MessagePortions));
              if (messageEventArgs.Exception.InnerException != null)
              {
                Log.Fatal(messageEventArgs.Exception.InnerException, "Inner exception");
              }
              break;
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
          GSA.App.Messenger.Message(MessageIntent.Display, MessageLevel.Error, 
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
      GSA.App.Merger.Initialise(mappableTypes);
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

  
  }
}
