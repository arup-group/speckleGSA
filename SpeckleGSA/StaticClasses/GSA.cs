using Interop.Gsa_10_0;
using SpeckleCore;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy;

namespace SpeckleGSA
{
  /// <summary>
  /// Static class which interfaces with GSA
  /// </summary>
  public static class GSA
  {
		public static IGSASettings Settings = new Settings();
		public static IGSAInterfacer Interfacer = new GSAInterfacer
		{
			Indexer = new Indexer()
		};

		//public static ComAuto GSAObject;

    //public static string FilePath;

    public static bool IsInit;

    //public static string Units { get; private set; }

    public static Dictionary<string, Tuple<string, string>> Senders { get; set; }
    public static List<Tuple<string, string>> Receivers { get; set; }

    public static void Init()
    {
      if (IsInit)
        return;

      Senders = new Dictionary<string, Tuple<string, string>>();
      Receivers = new List<Tuple<string, string>>();

      IsInit = true;

      Status.AddMessage("Linked to GSA.");
    }

    #region File Operations
    /// <summary>
    /// Creates a new GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static void NewFile(string emailAddress, string serverAddress, bool showWindow = true)
    {
      if (!IsInit)
        return;
			/*
      if (GSAObject != null)
      {
        try
        {
          GSAObject.Close();
        }
        catch { }
        GSAObject = null;
      }

			GSAObject = new ComAuto();

      GSAObject.LogFeatureUsage("api::specklegsa::" +
          FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
              .ProductVersion + "::GSA " + GSAObject.VersionString()
              .Split(new char[] { '\n' })[0]
              .Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);

      GSAObject.NewFile();
      GSAObject.SetLocale(Locale.LOC_EN_GB);

			*/
			((GSAInterfacer)Interfacer).NewFile(emailAddress, serverAddress, showWindow);

			GetSpeckleClients(emailAddress, serverAddress);

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
      if (!IsInit)
        return;

			/*
      if (GSAObject != null)
      {
        try
        {
          GSAObject.Close();
        }
        catch { }
        GSAObject = null;
      }

      GSAObject = new ComAuto();

      GSAObject.LogFeatureUsage("api::specklegsa::" +
        FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
          .ProductVersion + "::GSA " + GSAObject.VersionString()
          .Split(new char[] { '\n' })[0]
          .Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);

      GSAObject.Open(path);
      FilePath = path;
      GSAObject.SetLocale(Locale.LOC_EN_GB);

			*/
			((GSAInterfacer)Interfacer).OpenFile(path, emailAddress, serverAddress, showWindow);
			GetSpeckleClients(emailAddress, serverAddress);

			Status.AddMessage("Opened new file.");
    }

    /// <summary>
    /// Close GSA file.
    /// </summary>
    public static void Close()
    {
      if (!IsInit) return;
			/*
      try
      {
        GSAObject.Close();
      }
      catch { }
			*/
			((GSAInterfacer)Interfacer).Close();
			Senders.Clear();
      Receivers.Clear();
    }
    #endregion

    #region Speckle Client
    /// <summary>
    /// Extracts sender and receiver streams associated with the account.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static void GetSpeckleClients(string emailAddress, string serverAddress)
    {
      Senders.Clear();
      Receivers.Clear();

      try
      { 
        string key = emailAddress + "&" + serverAddress.Replace(':', '&');
				
        string res = ((GSAInterfacer)Interfacer).GetSID();

        if (res == "")
          return;

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
            Senders[senders[i]] = new Tuple<string, string>(senders[i + 1], senders[i + 2]);
        }

        if (receiverList != null && !string.IsNullOrEmpty(receiverList[1]))
        {
          string[] receivers = receiverList[1].Split(new char[] { '&' });

          for (int i = 0; i < receivers.Length; i += 2)
            Receivers.Add(new Tuple<string, string>(receivers[i], receivers[i + 1]));
        }
      }
      catch
      {
        // If fail to read, clear client SIDs
        Senders.Clear();
        Receivers.Clear();
        SetSpeckleClients(emailAddress, serverAddress);
      }
    }

    /// <summary>
    /// Writes sender and receiver streams associated with the account.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static void SetSpeckleClients(string emailAddress, string serverAddress)
    {
      string key = emailAddress + "&" + serverAddress.Replace(':', '&');
			string res = ((GSAInterfacer)Interfacer).GetSID();

			List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
              .Select(m => m.Value.Split(new char[] { ':' }))
              .Where(s => s.Length == 2)
              .ToList();

      sids.RemoveAll(S => S[0] == "SpeckleSender&" + key || S[0] == "SpeckleReceiver&" + key);

      List<string> senderList = new List<string>();
      foreach (KeyValuePair<string, Tuple<string, string>> kvp in Senders)
      {
        senderList.Add(kvp.Key);
        senderList.Add(kvp.Value.Item1);
        senderList.Add(kvp.Value.Item2);
      }

      List<string> receiverList = new List<string>();
      foreach (Tuple<string, string> t in Receivers)
      {
        receiverList.Add(t.Item1);
        receiverList.Add(t.Item2);
      }

      sids.Add(new string[] { "SpeckleSender&" + key, string.Join("&", senderList) });
      sids.Add(new string[] { "SpeckleReceiver&" + key, string.Join("&", receiverList) });

      string sidRecord = "";
      foreach (string[] s in sids)
        sidRecord += "{" + s[0] + ":" + s[1] + "}";

			((GSAInterfacer)GSA.Interfacer).SetSID(sidRecord);
    }
    #endregion

    #region Document Properties
		/*
    /// <summary>
    /// Extract the title of the GSA model.
    /// </summary>
    /// <returns>GSA model title</returns>
    public static string Title()
    {
      string res = ((GSAInterfacer)GSA.Interfacer).GetTitle();
    }
		*/

    /// <summary>
    /// Extracts the base properties of the Speckle stream.
    /// </summary>
    /// <returns>Base property dictionary</returns>
    public static Dictionary<string, object> GetBaseProperties()
    {
      var baseProps = new Dictionary<string, object>();

      baseProps["units"] = Settings.Units.LongUnitName();
      // TODO: Add other units

      var tolerances = ((GSAInterfacer)GSA.Interfacer).GetTolerances();

      List<double> lengthTolerances = new List<double>() {
                Convert.ToDouble(tolerances[3]), // edge
                Convert.ToDouble(tolerances[5]), // leg_length
                Convert.ToDouble(tolerances[7])  // memb_cl_dist
            };

      List<double> angleTolerances = new List<double>(){
                Convert.ToDouble(tolerances[4]), // angle
                Convert.ToDouble(tolerances[6]), // meemb_orient
            };

      baseProps["tolerance"] = lengthTolerances.Max().ConvertUnit("m", Settings.Units);
      baseProps["angleTolerance"] = angleTolerances.Max().ToRadians();

      return baseProps;
    }

    /// <summary>
    /// Updates the GSA unit stored in SpeckleGSA.
    /// </summary>
    public static void UpdateUnits()
    {
			Settings.Units = ((GSAInterfacer)Interfacer).GetUnits();
    }
    #endregion

    #region Views

    /// <summary>
    /// Update GSA case and task links. This should be called at the end of changes.
    /// </summary>
    public static void UpdateCasesAndTasks()
    {
			((GSAInterfacer)GSA.Interfacer).UpdateCasesAndTasks();
    }
    #endregion
  }
}
