using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SpeckleGSAInterfaces;

namespace SpeckleGSAProxy
{
  public class GSAProxy : IGSAProxy
  {
    //Hardwired values for interacting with the GSA instance
    //----
    private static string SID_APPID_TAG = "speckle_app_id";
    private static string SID_STRID_TAG = "speckle_stream_id";

    //These are the exceptions to the rule that, in GSA, all records that relate to each table (i.e. the set with mutually-exclusive indices) have the same keyword
    public static Dictionary<string, string[]> IrregularKeywordGroups = new Dictionary<string, string[]> { 
      { "LOAD_BEAM", new string[] { "LOAD_BEAM_POINT", "LOAD_BEAM_UDL", "LOAD_BEAM_LINE", "LOAD_BEAM_PATCH", "LOAD_BEAM_TRILIN" } } 
    };

    //These don't need to be the entire keywords - e.g. LOAD_BEAM covers LOAD_BEAM_UDL, LOAD_BEAM_LINE, LOAD_BEAM_PATCH and LOAD_BEAM_TRILIN
    public static string[] SetAtKeywordBeginnings = new string[] { "LOAD_NODE", "LOAD_BEAM", "LOAD_GRID_POINT", "LOAD_GRID_LINE", "LOAD_2D_FACE", "LOAD_GRID_AREA", "LOAD_2D_THERMAL", "LOAD_GRAVITY", "INF_BEAM", "INF_NODE", "RIGID", "GEN_REST" };
    //----

    //These are accessed via a lock
    private IComAuto GSAObject;
    private readonly List<string> batchSetGwa = new List<string>();
    private readonly List<string> batchBlankGwa = new List<string>();

    public string FilePath { get; set; }

    #region lock-related
    private readonly object syncLock = new object();
    protected T ExecuteWithLock<T>(Func<T> f)
    {
      lock (syncLock)
      {
        return f();
      }
    }

    protected void ExecuteWithLock(Action a)
    {
      lock (syncLock)
      {
        a();
      }
    }
    #endregion

    #region File Operations
    /// <summary>
    /// Creates a new GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public void NewFile(bool showWindow = true, object gsaInstance = null)
    {
      ExecuteWithLock(() =>
      {
        if (GSAObject != null)
        {
          try
          {
            GSAObject.Close();
          }
          catch { }
          GSAObject = null;
        }

        GSAObject = (IComAuto)gsaInstance ?? new ComAuto();

        GSAObject.LogFeatureUsage("api::specklegsa::" +
            FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                .ProductVersion + "::GSA " + GSAObject.VersionString()
                .Split(new char[] { '\n' })[0]
                .Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);

        GSAObject.NewFile();
        GSAObject.SetLocale(Locale.LOC_EN_GB);
        if (showWindow)
        {
          GSAObject.DisplayGsaWindow(true);
        }
      });
    }

    /// <summary>
    /// Opens an existing GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="path">Absolute path to GSA file</param>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public void OpenFile(string path, bool showWindow = true, object gsaInstance = null)
    {
      ExecuteWithLock(() =>
      {
        if (GSAObject != null)
        {
          try
          {
            GSAObject.Close();
          }
          catch { }
          GSAObject = null;
        }

        GSAObject = (IComAuto)gsaInstance ?? new ComAuto();

        GSAObject.LogFeatureUsage("api::specklegsa::" +
          FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
            .ProductVersion + "::GSA " + GSAObject.VersionString()
            .Split(new char[] { '\n' })[0]
            .Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);

        GSAObject.Open(path);
        FilePath = path;
        GSAObject.SetLocale(Locale.LOC_EN_GB);

        if (showWindow)
        {
          GSAObject.DisplayGsaWindow(true);
        }
      });
    }

    public int SaveAs(string filePath) => ExecuteWithLock(() => GSAObject.SaveAs(filePath));

    /// <summary>
    /// Close GSA file.
    /// </summary>
    public void Close()
    {
      ExecuteWithLock(() =>
      {
        try
        {
          GSAObject.Close();
        }
        catch { }
      });
    }
    #endregion

    public string FormatApplicationIdSidTag(string value)
    {
      return (string.IsNullOrEmpty(value) ? "" : "{" + SID_APPID_TAG + ":" + value.Replace(" ","") + "}");
    }

    public string FormatStreamIdSidTag(string value)
    {
      return (string.IsNullOrEmpty(value) ? "" : "{" + SID_STRID_TAG + ":" + value.Replace(" ", "") + "}");
    }

    public string FormatSidTags(string streamId = "", string applicationId = "")
    {
      return FormatStreamIdSidTag(streamId) + FormatApplicationIdSidTag(applicationId);
    }

    public void ParseGeneralGwa(string fullGwa, out string keyword, out int? index, out string streamId, out string applicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType)
    {
      var pieces = fullGwa.ListSplit("\t").ToList();
      keyword = "";
      streamId = "";
      applicationId = "";
      index = null;
      gwaWithoutSet = fullGwa;
      gwaSetCommandType = null;

      if (pieces.Count() < 2)
      {
        return;
      }

      //Remove the Set for the purpose of this method
      if (pieces[0].StartsWith("set", StringComparison.InvariantCultureIgnoreCase))
      {
        if (pieces[0].StartsWith("set_at", StringComparison.InvariantCultureIgnoreCase))
        {
          gwaSetCommandType = GwaSetCommandType.SetAt;

          if (int.TryParse(pieces[1], out int foundIndex))
          {
            index = foundIndex;
          }

          //For SET_ATs the format is SET_AT <index> <keyword> .., so remove the first two
          pieces.Remove(pieces[1]);
          pieces.Remove(pieces[0]);
        }
        else
        {
          gwaSetCommandType = GwaSetCommandType.Set;
          if (int.TryParse(pieces[2], out int foundIndex))
          {
            index = foundIndex;
          }

          pieces.Remove(pieces[0]);
        }
      }
      else
      {
        if (int.TryParse(pieces[1], out int foundIndex))
        {
          index = foundIndex;
        }
      }

      var delimIndex = pieces[0].IndexOf(':');
      if (delimIndex > 0)
      {
        //An SID has been found
        keyword = pieces[0].Substring(0, delimIndex);
        var sidTags = pieces[0].Substring(delimIndex);
        var match = Regex.Match(sidTags, "(?<={" + SID_STRID_TAG + ":).*?(?=})");
        streamId = (!string.IsNullOrEmpty(match.Value)) ? match.Value : "";
        match = Regex.Match(sidTags, "(?<={" + SID_APPID_TAG + ":).*?(?=})");
        applicationId = (!string.IsNullOrEmpty(match.Value)) ? match.Value : "";
      }
      else
      {
        keyword = pieces[0];
      }

      foreach (var groupKeyword in IrregularKeywordGroups.Keys)
      {
        if (IrregularKeywordGroups[groupKeyword].Contains(keyword))
        {
          keyword = groupKeyword;
          break;
        }
      }

      gwaWithoutSet = string.Join("\t", pieces);
      return;
    }

    //Tuple: keyword | index | Application ID | GWA command | Set or Set At
    public List<ProxyGwaLine> GetGwaData(IEnumerable<string> keywords, bool nodeApplicationIdFilter)
    {
      var dataLock = new object();
      var data = new List<ProxyGwaLine>();
      var setKeywords = new List<string>();
      var setAtKeywords = new List<string>();

      foreach (var keyword in keywords)
      {
        if (SetAtKeywordBeginnings.Any(b => keyword.StartsWith(b)))
        {
          setAtKeywords.Add(keyword);
        }
        else
        {
          setKeywords.Add(keyword);
        }
      }

      for (int i = 0; i < setKeywords.Count(); i++)
      {
        var newCommand = "GET_ALL\t" + setKeywords[i];
        var isNode = setKeywords[i].Contains("NODE");
        var isElement = setKeywords[i].StartsWith("EL.");

        string[] gwaRecords;

        try
        {
          gwaRecords = ExecuteWithLock(() => ((string)GSAObject.GwaCommand(newCommand)).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries));
        }
        catch
        {
          gwaRecords = new string[0];
        }

        Parallel.ForEach(gwaRecords, gwa =>
        {
          ParseGeneralGwa(gwa, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType);
          var index = foundIndex ?? 0;
          var originalSid = "";

          if (string.IsNullOrEmpty(foundStreamId))
          {
            //Slight hardcoding for optimisation here: the biggest source of GetSidTagValue calls would be from nodes, and knowing
            //(at least in GSA v10 build 63) that GET_ALL NODE does return SID tags, the call is avoided for NODE keyword
            if (!isNode && !isElement)
            {
              try
              {
                foundStreamId = ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, index, SID_STRID_TAG));
              }
              catch { }
            }
          }
          else
          {
            originalSid += FormatStreamIdSidTag(foundStreamId);
          }

          if (string.IsNullOrEmpty(foundApplicationId))
          {
            //Again, the same optimisation as explained above
            if (!isNode && !isElement)
            {
              try
              {
                foundApplicationId = ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, index, SID_APPID_TAG));
              }
              catch { }
            }
          }
          else
          {
            originalSid += FormatStreamIdSidTag(foundApplicationId);
          }

          var newSid = FormatStreamIdSidTag(foundStreamId) + FormatApplicationIdSidTag(foundApplicationId);
          if (!string.IsNullOrEmpty(originalSid) && !string.IsNullOrEmpty(newSid))
          {
            gwaWithoutSet.Replace(originalSid, newSid);
          }

          if (!(nodeApplicationIdFilter == true && isNode && string.IsNullOrEmpty(foundApplicationId)))
          {
            var line = new ProxyGwaLine()
            {
              Keyword = keyword,
              Index = index,
              StreamId = foundStreamId,
              ApplicationId = foundApplicationId,
              GwaWithoutSet = gwaWithoutSet,
              GwaSetType = GwaSetCommandType.Set
            };

            lock (dataLock)
            {
              data.Add(line);
            }
          }
        }
        );
      }

      for (int i = 0; i < setAtKeywords.Count(); i++)
      {
        var highestIndex = ExecuteWithLock(() => GSAObject.GwaCommand("HIGHEST\t" + setAtKeywords[i]));

        for (int j = 1; j <= highestIndex; j++)
        {
          var newCommand = string.Join("\t", new[] { "GET", setAtKeywords[i], j.ToString() });

          var gwaRecord = "";
          try
          {
            gwaRecord = (string)ExecuteWithLock(() => GSAObject.GwaCommand(newCommand));
          }
          catch { }

          if (gwaRecord != "")
          {
            ParseGeneralGwa(gwaRecord, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType);

            var originalSid = "";
            if (string.IsNullOrEmpty(foundStreamId))
            {
              try
              {
                foundStreamId = ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, j, SID_STRID_TAG));
              }
              catch { }
            }
            else
            {
              originalSid += FormatStreamIdSidTag(foundStreamId);
            }
            if (string.IsNullOrEmpty(foundApplicationId))
            {
              foundApplicationId = ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, j, SID_APPID_TAG));
            }
            else
            {
              originalSid += FormatStreamIdSidTag(foundApplicationId);
            }

            var newSid = FormatStreamIdSidTag(foundStreamId) + FormatApplicationIdSidTag(foundApplicationId);
            if (!string.IsNullOrEmpty(originalSid) && !string.IsNullOrEmpty(newSid))
            {
              gwaWithoutSet.Replace(originalSid, newSid);
            }

            var line = new ProxyGwaLine()
            {
              Keyword = setAtKeywords[i],
              Index = j,
              StreamId = foundStreamId,
              ApplicationId = foundApplicationId,
              GwaWithoutSet = gwaWithoutSet,
              GwaSetType = GwaSetCommandType.SetAt
            };

            lock (dataLock)
            {
              data.Add(line);
            }
          }
        }
      }

      return data;
    }

    private string FormatApplicationId(string keyword, int index, string applicationId)
    {
      //It has been observed that sometimes GET commands don't include the SID despite there being one.  For some (but not all)
      //of these instances, the SID is available through an explicit call for the SID, so try that next
      return (string.IsNullOrEmpty(applicationId)) ? ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, index, SID_APPID_TAG)) : applicationId;
    }

    private int ExtractGwaIndex(string gwaRecord)
    {
      var pieces = gwaRecord.Split(new[] { '\t' });
      return (int.TryParse(pieces[1], out int index)) ? index : 0;
    }


    //Assumed to be the full SET or SET_AT command
    public void SetGwa(string gwaCommand) => ExecuteWithLock(() => batchSetGwa.Add(gwaCommand));

    public void Sync()
    {
      var batchBlankCommand = ExecuteWithLock(() => string.Join("\r\n", batchBlankGwa));
      var blankCommandResult = ExecuteWithLock(() => GSAObject.GwaCommand(batchBlankCommand));
      ExecuteWithLock(() => batchBlankGwa.Clear());

      var batchSetCommand = ExecuteWithLock(() => string.Join("\r\n", batchSetGwa));
      var setCommandResult = ExecuteWithLock(() => GSAObject.GwaCommand(batchSetCommand));
      ExecuteWithLock(() => batchSetGwa.Clear());      
    }

    public void GetGSATotal2DElementOffset(int index, double insertionPointOffset, out double offset, out string offsetRec)
    {
      double materialInsertionPointOffset = 0;
      double zMaterialOffset = 0;

      object result = ExecuteWithLock(() => GSAObject.GwaCommand("GET\tPROP_2D\t" + index.ToString()));
      string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

      string res = newPieces.FirstOrDefault();

      if (res == null || res == "")
      {
        offset = insertionPointOffset;
        offsetRec = res;
        return;
      }

      string[] pieces = res.ListSplit("\t");

      zMaterialOffset = -Convert.ToDouble(pieces[12]);
      offset = insertionPointOffset + zMaterialOffset + materialInsertionPointOffset;
      offsetRec = res;
      return;
    }

    public int NodeAt(double x, double y, double z, double coincidenceTol)
    {
      //Note: the outcome of this might need to be added to the caches!
      var index = ExecuteWithLock(() => GSAObject.Gen_NodeAt(x, y, z, coincidenceTol));
      return index;
    }

    public string GetGwaForNode(int index)
    {
      var gwaCommand = "GET\tNODE.2\t" + index.ToString();
      return (string)ExecuteWithLock(() => GSAObject.GwaCommand(gwaCommand));
    }

    public string SetSid(string gwa, string streamId, string applicationId)
    {
      ParseGeneralGwa(gwa, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType);

      var streamIdToWrite = (string.IsNullOrEmpty(streamId) ? foundStreamId : streamId);
      var applicationIdToWrite = (string.IsNullOrEmpty(applicationId) ? foundApplicationId : applicationId);

      if (!string.IsNullOrEmpty(streamIdToWrite))
      {
        ExecuteWithLock(() => GSAObject.WriteSidTagValue(keyword, foundIndex.Value, SID_STRID_TAG, streamIdToWrite));
      }
      if (!string.IsNullOrEmpty(applicationIdToWrite))
      {
        ExecuteWithLock(() => GSAObject.WriteSidTagValue(keyword, foundIndex.Value, SID_APPID_TAG, applicationIdToWrite));
      }

      var newSid = FormatStreamIdSidTag(streamIdToWrite) + FormatApplicationIdSidTag(applicationIdToWrite);
      if (!string.IsNullOrEmpty(foundStreamId) || !string.IsNullOrEmpty(foundApplicationId))
      {
        var originalSid = FormatStreamIdSidTag(foundStreamId) + FormatApplicationIdSidTag(foundApplicationId);
        gwa = gwa.Replace(originalSid, newSid);
      }
      else
      {
        gwa = gwa.Replace(keyword, keyword + ":" + newSid);
      }
      return gwa;
    }

    public int[] ConvertGSAList(string list, GSAEntity type)
    {
      if (list == null) return new int[0];

      string[] pieces = list.ListSplit(" ");
      pieces = pieces.Where(s => !string.IsNullOrEmpty(s)).ToArray();

      List<int> items = new List<int>();
      for (int i = 0; i < pieces.Length; i++)
      {
        if (pieces[i].IsDigits())
        {
          items.Add(Convert.ToInt32(pieces[i]));
        }
        else if (pieces[i].Contains('"'))
        {
          items.AddRange(ConvertNamedGSAList(pieces[i], type));
        }
        else if (pieces[i] == "to")
        {
          int lowerRange = Convert.ToInt32(pieces[i - 1]);
          int upperRange = Convert.ToInt32(pieces[i + 1]);

          for (int j = lowerRange + 1; j <= upperRange; j++)
            items.Add(j);

          i++;
        }
        else
        {
          try
          {
            var item = ExecuteWithLock(() =>
            {
              GSAObject.EntitiesInList(pieces[i], (GsaEntity)type, out int[] itemTemp);

              if (itemTemp == null)
              {
                GSAObject.EntitiesInList("\"" + list + "\"", (GsaEntity)type, out itemTemp);
              }
              return itemTemp;
            });

            if (item != null)
            {
              items.AddRange((int[])item);
            }
          }
          catch
          { }
        }
      }

      return items.ToArray();
    }

    public Dictionary<string, object> GetGSAResult(int id, int resHeader, int flags, List<string> keys, string loadCase, string axis = "local", int num1DPoints = 2)
    {
      var ret = new Dictionary<string, object>();
      GsaResults[] res = null;
      bool exists = false;

      int returnCode = -1;

      try
      {
        return ExecuteWithLock(() =>
        {
          int num;

          // The 2nd condition here is a special case for assemblies
          if (Enum.IsDefined(typeof(ResHeader), resHeader) || resHeader == 18002000)
          {
            returnCode = GSAObject.Output_Init_Arr(flags, axis, loadCase, (ResHeader)resHeader, num1DPoints);

            try
            {
              var existsResult = GSAObject.Output_DataExist(id);
              exists = (existsResult == 1);
            }
            catch (Exception e)
            {
              return null;
            }

            if (exists)
            {
              var extracted = false;
              try
              {
                returnCode = GSAObject.Output_Extract_Arr(id, out var outputExtractResults, out num);
                res = (GsaResults[])outputExtractResults;
                extracted = true;
              }
              catch { }

              if (!extracted)
              {
                // Try individual extract
                for (var i = 1; i <= keys.Count; i++)
                {
                  var indivResHeader = resHeader + i;

                  try
                  {
                    GSAObject.Output_Init(flags, axis, loadCase, indivResHeader, num1DPoints);
                  }
                  catch (Exception e)
                  {
                    return null;
                  }

                  var numPos = 1;

                  try
                  {
                    numPos = GSAObject.Output_NumElemPos(id);
                  }
                  catch { }

                  if (i == 1)
                  {
                    res = new GsaResults[numPos];
                    for (var j = 0; j < res.Length; j++)
                    {
                      res[j] = new GsaResults() { dynaResults = new double[keys.Count] };
                    }
                  }

                  for (var j = 0; j < numPos; j++)
                  {
                    res[j].dynaResults[i - 1] = (double)GSAObject.Output_Extract(id, j);
                  }
                }
              }

            }
            else
            {
              return null;
            }
          }
          else
          {
            returnCode = GSAObject.Output_Init(flags, axis, loadCase, resHeader, num1DPoints);

            try
            {
              var existsResult = GSAObject.Output_DataExist(id);
              exists = (existsResult == 1);
            }
            catch
            {
              return null;
            }

            if (exists)
            {
              var numPos = GSAObject.Output_NumElemPos(id);
              res = new GsaResults[numPos];

              try
              {
                for (var i = 0; i < numPos; i++)
                {
                  res[i] = new GsaResults() { dynaResults = new double[] { (double)GSAObject.Output_Extract(id, i) } };
                }
              }
              catch
              {
                return null;
              }
            }
            else
            {
              return null;
            }
          }

          var numColumns = res[0].dynaResults.Count();

          for (var i = 0; i < numColumns; i++)
          {
            ret[keys[i]] = res.Select(x => (double)x.dynaResults.GetValue(i)).ToList();
          }

          return ret;
        });
      }
      catch
      {
        return null;
      }
    }

    public bool CaseExist(string loadCase)
    {
      try
      {
        string[] pieces = loadCase.Split(new char[] { 'p' }, StringSplitOptions.RemoveEmptyEntries);

        if (pieces.Length == 1)
        {
          return ExecuteWithLock(() => GSAObject.CaseExist(loadCase[0].ToString(), Convert.ToInt32(loadCase.Substring(1))) == 1);
        }
        else if (pieces.Length == 2)
        {
          return ExecuteWithLock(() => GSAObject.CaseExist(loadCase[0].ToString(), Convert.ToInt32(pieces[0].Substring(1))) == 1);
        }
        else
        {
          return false;
        }
      }
      catch { return false; }
    }

    #region private_methods
    private int[] ConvertNamedGSAList(string list, GSAEntity type)
    {
      list = list.Trim(new char[] { '"', ' ' });

      try
      {
        object result = ExecuteWithLock(() => GSAObject.GwaCommand("GET\tLIST\t" + list));
        string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

        string res = newPieces.FirstOrDefault();

        string[] pieces = res.Split(new char[] { '\t' });

        return ConvertGSAList(pieces[pieces.Length - 1], type);
      }
      catch
      {
        try
        {
          return ExecuteWithLock(() =>
          {
            GSAObject.EntitiesInList("\"" + list + "\"", (GsaEntity)type, out int[] itemTemp);
            return (itemTemp == null) ? new int[0] : (int[])itemTemp;
          });
        }
        catch { return new int[0]; }
      }
    }
    #endregion

    //
    public void DeleteGWA(string keyword, int index, GwaSetCommandType gwaSetCommandType)
    {
      var command = string.Join("\t", new[] { (gwaSetCommandType == GwaSetCommandType.Set) ? "BLANK" : "DELETE", keyword, index.ToString() });
      ExecuteWithLock(() =>
      { 
        if (gwaSetCommandType == GwaSetCommandType.Set)
        {
          //For synchronising later
          batchBlankGwa.Add(command);
        }
        else
        {
          GSAObject.GwaCommand(command);
        }
      });
    }

    //----
    #region Speckle Client
    /// <summary>
    /// Writes sender and receiver streams associated with the account.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public bool SetTopLevelSid(string sidRecord)
    {
      try
      {
        ExecuteWithLock(() => GSAObject.GwaCommand("SET\tSID\t" + sidRecord));
        return true;
      }
      catch
      {
        return false;
      }
    }
    #endregion

    #region Document Properties
    /// <summary>
    /// Extract the title of the GSA model.
    /// </summary>
    /// <returns>GSA model title</returns>
    public string GetTitle()
    {
      string res = (string)ExecuteWithLock(() => GSAObject.GwaCommand("GET\tTITLE"));

      string[] pieces = res.ListSplit("\t");

      return pieces.Length > 1 ? pieces[1] : "My GSA Model";
    }

    public string[] GetTolerances()
    {
      return ((string)ExecuteWithLock(() => GSAObject.GwaCommand("GET\tTOL"))).ListSplit("\t");
    }

    /// <summary>
    /// Updates the GSA unit stored in SpeckleGSA.
    /// </summary>
    public string GetUnits()
    {
      return ((string)ExecuteWithLock(() => GSAObject.GwaCommand("GET\tUNIT_DATA.1\tLENGTH"))).ListSplit("\t")[2];
    }
    #endregion

    #region Views
    /// <summary>
    /// Update GSA viewer. This should be called at the end of changes.
    /// </summary>
    public bool UpdateViews()
    {
      try
      {
        ExecuteWithLock(() => GSAObject.UpdateViews());
        return true;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Update GSA case and task links. This should be called at the end of changes.
    /// </summary>
    public bool UpdateCasesAndTasks()
    {
      try
      {
        ExecuteWithLock(() => GSAObject.ReindexCasesAndTasks());
        return true;
      }
      catch
      {
        return false;
      }
    }

    public string GetTopLevelSid()
    {
      string sid = "";
      try
      {
        sid = (string)ExecuteWithLock(() => GSAObject.GwaCommand("GET\tSID"));
      }
      catch
      {
        //File doesn't have SID
      }
      return sid;
    }
   
    #endregion
  }
}
