using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SQLite;
using SpeckleCore;
using Interop.Gsa_10_0;

namespace SpeckleGSA
{
  /// <summary>
  /// Static class which interfaces with GSA
  /// </summary>
  public static class GSA
  {
    public static ComAuto GSAObject;

    public static bool IsInit;

    public static string Units { get; private set; }

    public static Dictionary<string, string> Senders { get; set; }
    public static List<string> Receivers { get; set; }
    
    public static void Init()
    {
      if (IsInit)
        return;

      Senders = new Dictionary<string, string>();
      Receivers = new List<string>();

      IsInit = true;

      Status.AddMessage("Linked to GSA.");
    }

    #region File Operations
    /// <summary>
    /// Creates a new GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static void NewFile(string emailAddress, string serverAddress)
    {
      if (!IsInit)
        return;

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
      //GSAObject.LogFeatureUsage("api::specklegsa::" +
      //    FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
      //        .ProductVersion + "::GSA " + GSAObject.VersionString()
      //        .Split(new char[] { '\n' })[0]
      //        .Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);
      GSAObject.NewFile();
      GSAObject.DisplayGsaWindow(true);

      GetSpeckleClients(emailAddress, serverAddress);

      Status.AddMessage("Created new file.");
    }

    /// <summary>
    /// Opens an existing GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="path">Absolute path to GSA file</param>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static void OpenFile(string path, string emailAddress, string serverAddress)
    {
      if (!IsInit)
        return;

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
      //GSAObject.LogFeatureUsage("api::specklegsa::" +
      //    FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
      //        .ProductVersion + "::GSA " + GSAObject.VersionString()
      //        .Split(new char[] { '\n' })[0]
      //        .Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);
      GSAObject.Open(path);
      GSAObject.DisplayGsaWindow(true);

      GetSpeckleClients(emailAddress, serverAddress);

      Status.AddMessage("Opened new file.");
    }

    /// <summary>
    /// Close GSA file.
    /// </summary>
    public static void Close()
    {
      if (!IsInit) return;

      try
      {
        GSAObject.Close();
      }
      catch { }
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

      string key = emailAddress + "&" + serverAddress.Replace(':', '&');
      string res = (string)GSAObject.GwaCommand("GET,SID");

      if (res == "")
        return;

      List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
              .Select(m => m.Value.Split(new char[] { ':' }))
              .Where(s => s.Length == 2)
              .ToList();

      string[] senderList = sids.Where(s => s[0] == "SpeckleSender&" + key).FirstOrDefault();
      string[] receiverList = sids.Where(s => s[0] == "SpeckleReceiver&" + key).FirstOrDefault();

      if (senderList != null)
      {
        string[] senders = senderList[1].Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < senders.Length; i += 2)
          Senders[senders[i]] = senders[i + 1];
      }

      if (receiverList != null)
        Receivers = receiverList[1].Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// Writes sender and receiver streams associated with the account.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public static void SetSpeckleClients(string emailAddress, string serverAddress)
    {
      string key = emailAddress + "&" + serverAddress.Replace(':', '&');
      string res = (string)GSAObject.GwaCommand("GET,SID");

      List<string[]> sids = Regex.Matches(res, @"(?<={).*?(?=})").Cast<Match>()
              .Select(m => m.Value.Split(new char[] { ':' }))
              .Where(s => s.Length == 2)
              .ToList();

      sids.RemoveAll(S => S[0] == "SpeckleSender&" + key || S[0] == "SpeckleReceiver&" + key);

      List<string> senderList = new List<string>();
      foreach (KeyValuePair<string, string> kvp in Senders)
      {
        senderList.Add(kvp.Key);
        senderList.Add(kvp.Value);
      }

      sids.Add(new string[] { "SpeckleSender&" + key, string.Join("&", senderList) });
      sids.Add(new string[] { "SpeckleReceiver&" + key, string.Join("&", Receivers) });

      string sidRecord = "";
      foreach (string[] s in sids)
        sidRecord += "{" + s[0] + ":" + s[1] + "}";

      GSAObject.GwaCommand("SET,SID," + sidRecord);
    }
    #endregion

    #region Document Properties
    /// <summary>
    /// Extract the title of the GSA model.
    /// </summary>
    /// <returns>GSA model title</returns>
    public static string Title()
    {
      string res = (string)GSAObject.GwaCommand("GET,TITLE");

      string[] pieces = res.ListSplit(",");

      return pieces[1];
    }

    /// <summary>
    /// Extracts the base properties of the Speckle stream.
    /// </summary>
    /// <returns>Base property dictionary</returns>
    public static Dictionary<string, object> GetBaseProperties()
    {
      Dictionary<string, object> baseProps = new Dictionary<string, object>();

      baseProps["units"] = Units.LongUnitName();
      // TODO: Add other units

      string[] tolerances = ((string)GSAObject.GwaCommand("GET,TOL")).ListSplit(",");

      List<double> lengthTolerances = new List<double>() {
                Convert.ToDouble(tolerances[3]), // edge
                Convert.ToDouble(tolerances[5]), // leg_length
                Convert.ToDouble(tolerances[7])  // memb_cl_dist
            };

      List<double> angleTolerances = new List<double>(){
                Convert.ToDouble(tolerances[4]), // angle
                Convert.ToDouble(tolerances[6]), // meemb_orient
            };

      baseProps["tolerance"] = lengthTolerances.Max().ConvertUnit("m", Units);
      baseProps["angleTolerance"] = angleTolerances.Max().ToRadians();

      return baseProps;
    }

    /// <summary>
    /// Updates the GSA unit stored in SpeckleGSA.
    /// </summary>
    public static void UpdateUnits()
    {
      Units = ((string)GSAObject.GwaCommand("GET,UNIT_DATA.1,LENGTH")).ListSplit(",")[2];
    }
    #endregion

    #region Views
    /// <summary>
    /// Update GSA viewer. This should be called at the end of changes.
    /// </summary>
    public static void UpdateViews()
    {
      GSAObject.UpdateViews();
    }

    /// <summary>
    /// Update GSA case and task links. This should be called at the end of changes.
    /// </summary>
    public static void UpdateCasesAndTasks()
    {
      GSAObject.ReindexCasesAndTasks();
    }
    #endregion
  }
}
