using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy;
using SpeckleUtil;

namespace SpeckleGSA
{
	/// <summary>
	/// Static class which interfaces with GSA
	/// </summary>
	public static class GSA
  {
		public static Settings Settings = new Settings();

    public static ISpeckleObjectMerger Merger = new SpeckleObjectMerger();
    public static IGSAProxy gsaProxy = new GSAProxy();
    public static IGSACache gsaCache = new GSACache();
    public static ISpeckleGSAAppUI appUi = new SpeckleAppUI();

    public static bool IsInit;

    public static Dictionary<string, Tuple<string, string>> SenderInfo { get; set; }
    public static List<Tuple<string, string>> ReceiverInfo { get; set; }

		public static Dictionary<Type, List<Type>> WriteTypePrereqs = new Dictionary<Type, List<Type>>();
		public static Dictionary<Type, List<Type>> ReadTypePrereqs = new Dictionary<Type, List<Type>>();

		public static void Init()
    {
      if (IsInit) return;

      SenderInfo = new Dictionary<string, Tuple<string, string>>();
      ReceiverInfo = new List<Tuple<string, string>>();

      IsInit = true;

      Status.AddMessage("Linked to GSA.");

			InitialiseKits(out List<string> statusMessages);

			if (statusMessages.Count() > 0)
			{
				foreach (var msg in statusMessages)
				{
					Status.AddMessage(msg);
				}
			}
    }

		private static void InitialiseKits(out List<string> statusMessages)
		{
			statusMessages = new List<string>();

			var attributeType = typeof(GSAObject);
			var interfaceType = typeof(IGSASpeckleContainer);

			SpeckleInitializer.Initialize();

			// Run initialize receiver method in interfacer
			var assemblies = SpeckleInitializer.GetAssemblies().Where(a => a.GetTypes().Any(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer))));

      var speckleTypes = new List<Type>();
      foreach (var assembly in assemblies)
      {
        var types = assembly.GetTypes();
        foreach (var t in types)
        {
          if (typeof(SpeckleObject).IsAssignableFrom(t))
          {
            speckleTypes.Add(t);
          }
        }
      }

      var mappableTypes = new List<Type>();
      foreach (var ass in assemblies)
			{
				var types = ass.GetTypes();

        Type gsaStatic;
				try
				{
					gsaStatic = types.FirstOrDefault(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer)) && t.GetProperties().Any(p => p.PropertyType == typeof(IGSACacheForKit)));
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
          gsaStatic.GetProperty("Interface").SetValue(null, gsaProxy);
          gsaStatic.GetProperty("Settings").SetValue(null, Settings);
          gsaStatic.GetProperty("Cache").SetValue(null, gsaCache);
          gsaStatic.GetProperty("AppUI").SetValue(null, appUi);
        }
        catch
        {
          Status.AddError($"Unable to fully connect to {ass.GetName().Name}.dll. Please check the versions of the kit you have installed.");
        }


        var objTypes = types.Where(t => interfaceType.IsAssignableFrom(t) && t != interfaceType).ToList();
        objTypes = objTypes.Distinct().ToList();

				foreach (var t in objTypes)
				{
					var prereq = t.GetAttribute("WritePrerequisite");
					WriteTypePrereqs[t] = (prereq != null) ? ((Type[])prereq).ToList() : new List<Type>();

					prereq = t.GetAttribute("ReadPrerequisite");
					ReadTypePrereqs[t] = (prereq != null) ? ((Type[])prereq).ToList() : new List<Type>();
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
      Merger.Initialise(mappableTypes);
    } 

		#region File Operations
		/// <summary>
		/// Creates a new GSA file. Email address and server address is needed for logging purposes.
		/// </summary>
		/// <param name="emailAddress">User email address</param>
		/// <param name="serverAddress">Speckle server address</param>
		public static void NewFile(string emailAddress, string serverAddress, bool showWindow = true)
    {
			if (!IsInit) return;

			gsaProxy.NewFile(showWindow);

      if (emailAddress != null && serverAddress != null)
      {
        GetSpeckleClients(emailAddress, serverAddress);
      }

      Status.AddMessage("Created new file.");
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

			gsaProxy.OpenFile(path, showWindow);
      if (emailAddress != null && serverAddress != null)
      {
        GetSpeckleClients(emailAddress, serverAddress);
      }

			Status.AddMessage("Opened new file.");
    }

    /// <summary>
    /// Close GSA file.
    /// </summary>
    public static void Close()
    {
      if (!IsInit) return;

			gsaProxy.Close();
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
				
        string res = gsaProxy.GetTopLevelSid();

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
            SenderInfo[senders[i]] = new Tuple<string, string>(senders[i + 1], senders[i + 2]);
        }

        if (receiverList != null && !string.IsNullOrEmpty(receiverList[1]))
        {
          string[] receivers = receiverList[1].Split(new char[] { '&' });

          for (int i = 0; i < receivers.Length; i += 2)
            ReceiverInfo.Add(new Tuple<string, string>(receivers[i], receivers[i + 1]));
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
			string res = gsaProxy.GetTopLevelSid();

			List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
              .Select(m => m.Value.Split(new char[] { ':' }))
              .Where(s => s.Length == 2)
              .ToList();

      sids.RemoveAll(S => S[0] == "SpeckleSender&" + key || S[0] == "SpeckleReceiver&" + key);

      List<string> senderList = new List<string>();
      foreach (KeyValuePair<string, Tuple<string, string>> kvp in SenderInfo)
      {
        senderList.Add(kvp.Key);
        senderList.Add(kvp.Value.Item1);
        senderList.Add(kvp.Value.Item2);
      }

      List<string> receiverList = new List<string>();
      foreach (Tuple<string, string> t in ReceiverInfo)
      {
        receiverList.Add(t.Item1);
        receiverList.Add(t.Item2);
      }

      sids.Add(new string[] { "SpeckleSender&" + key, string.Join("&", senderList) });
      sids.Add(new string[] { "SpeckleReceiver&" + key, string.Join("&", receiverList) });

      string sidRecord = "";
      foreach (string[] s in sids)
        sidRecord += "{" + s[0] + ":" + s[1] + "}";

      return gsaProxy.SetTopLevelSid(sidRecord);
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

      baseProps["units"] = Settings.Units.LongUnitName();
      // TODO: Add other units

      var tolerances = gsaProxy.GetTolerances();

			var lengthTolerances = new List<double>() {
								Convert.ToDouble(tolerances[3]), // edge
                Convert.ToDouble(tolerances[5]), // leg_length
                Convert.ToDouble(tolerances[7])  // memb_cl_dist
            };

      var angleTolerances = new List<double>(){
                Convert.ToDouble(tolerances[4]), // angle
                Convert.ToDouble(tolerances[6]), // meemb_orient
            };

      baseProps["tolerance"] = lengthTolerances.Max().ConvertUnit("m", Settings.Units);
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
			gsaProxy.UpdateCasesAndTasks();
    }
    #endregion
  }
}
