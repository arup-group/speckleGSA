using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Interop.Gsa_10_1;
using SpeckleGSAInterfaces;

namespace SpeckleGSAProxy
{
  public class GSAProxy : IGSAProxy
  {
    //Hardwired values for interacting with the GSA instance
    //----
    private static readonly string SID_APPID_TAG = "speckle_app_id";
    private static readonly string SID_STRID_TAG = "speckle_stream_id";

    public static readonly char GwaDelimiter = '\t';

    //These are the exceptions to the rule that, in GSA, all records that relate to each table (i.e. the set with mutually-exclusive indices) have the same keyword
    public static Dictionary<string, string[]> IrregularKeywordGroups = new Dictionary<string, string[]> { 
      { "LOAD_BEAM", new string[] { "LOAD_BEAM_POINT", "LOAD_BEAM_UDL", "LOAD_BEAM_LINE", "LOAD_BEAM_PATCH", "LOAD_BEAM_TRILIN" } } 
    };

    //Note that When a GET_ALL is called for LOAD_BEAM, it returns LOAD_BEAM_UDL, LOAD_BEAM_LINE, LOAD_BEAM_PATCH and LOAD_BEAM_TRILIN
    public static string[] SetAtKeywords = new string[] { "LOAD_NODE", "LOAD_BEAM", "LOAD_GRID_POINT", "LOAD_GRID_LINE", "LOAD_2D_FACE", 
      "LOAD_GRID_AREA", "LOAD_2D_THERMAL", "LOAD_GRAVITY", "INF_BEAM", "INF_NODE", "RIGID", "GEN_REST" };
    //----

    //These are accessed via a lock
    private IComAuto GSAObject;
    private readonly List<string> batchSetGwa = new List<string>();
    private readonly List<string> batchBlankGwa = new List<string>();

    public string FilePath { get; set; }

    char IGSAProxy.GwaDelimiter => GSAProxy.GwaDelimiter;

    private string SpeckleGsaVersion;
    private string units = "m";

    #region nodeAt_factors
    public static bool NodeAtCalibrated = false;
    //Set to defaults, which will be updated at calibration
    private static readonly Dictionary<string, double> UnitNodeAtFactors = new Dictionary<string, double>();

    public static void CalibrateNodeAt()
    {
      double coordValue = 1000;
      var unitCoincidentDict = new Dictionary<string, double>() { { "mm", 20 }, { "cm", 1 }, { "in", 1 }, { "m", 0.1 } };
      var units = new[] { "m", "cm", "mm", "in" };

      var proxy = new GSAProxy();
      proxy.NewFile(false);
      foreach (var u in units)
      {
        proxy.SetUnits(u);
        var nodeIndex = proxy.NodeAt(coordValue, coordValue, coordValue, unitCoincidentDict[u]);
        double factor = 1;
        var gwa = proxy.GetGwaForNode(nodeIndex);
        var pieces = gwa.Split(GSAProxy.GwaDelimiter);
        if (double.TryParse(pieces.Last(), out double z1))
        {
          if (z1 != coordValue)
          {
            var factorCandidate = coordValue / z1;

            nodeIndex = proxy.NodeAt(coordValue * factorCandidate, coordValue * factorCandidate, coordValue * factorCandidate, 1 * factorCandidate);

            gwa = proxy.GetGwaForNode(nodeIndex);
            pieces = gwa.Split(GSAProxy.GwaDelimiter);

            if (double.TryParse(pieces.Last(), out double z2) && z2 == 1000)
            {
              //it's confirmed
              factor = factorCandidate;
            }
          }
        }
        if (UnitNodeAtFactors.ContainsKey(u))
        {
          UnitNodeAtFactors[u] = factor;
        }
        else
        {
          UnitNodeAtFactors.Add(u, factor);
        }
      }

      proxy.Close();

      NodeAtCalibrated = true;
    }
    #endregion

    public void SetAppVersionForTelemetry(string speckleGsaAppVersion)
    {
      SpeckleGsaVersion = speckleGsaAppVersion;
    }

    #region telemetry
    public void SendTelemetry(params string[] messagePortions)
    {
      var finalMessagePortions = new List<string> { "SpeckleGSA", SpeckleGsaVersion, GSAObject.VersionString() };
      finalMessagePortions.AddRange(messagePortions);
      var message = string.Join("::", finalMessagePortions);
      GSAObject.LogFeatureUsage(message);
    }
    #endregion

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
                .Split(new char[] { GwaDelimiter }, StringSplitOptions.RemoveEmptyEntries)[1]);

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
            .Split(new char[] { GwaDelimiter }, StringSplitOptions.RemoveEmptyEntries)[1]);

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

    public static void ParseGeneralGwa(string fullGwa, out string keyword, out int? index, out string streamId, out string applicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, bool includeKwVersion = false)
    {
      var pieces = fullGwa.ListSplit(GSAProxy.GwaDelimiter).ToList();
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

      if (!includeKwVersion)
      {
        keyword = keyword.Split('.').First();
      }

      gwaWithoutSet = string.Join(GSAProxy.GwaDelimiter.ToString(), pieces);
      return;
    }

    //Tuple: keyword | index | Application ID | GWA command | Set or Set At
    public List<ProxyGwaLine> GetGwaData(IEnumerable<string> keywords, bool nodeApplicationIdFilter, IProgress<int> incrementProgress = null)
    {
      var dataLock = new object();
      var data = new List<ProxyGwaLine>();
      var setKeywords = new List<string>();
      var setAtKeywords = new List<string>();
      var tempKeywordIndexCache = new Dictionary<string, List<int>>();

      var versionRemovedKeywords = keywords.Select(kw => kw.Split('.').First()).Where(kw => !string.IsNullOrEmpty(kw)).ToList();

      foreach (var keyword in versionRemovedKeywords)
      {
        if (SetAtKeywords.Any(b => keyword.Equals(b, StringComparison.InvariantCultureIgnoreCase)))
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
        var newCommand = "GET_ALL" + GSAProxy.GwaDelimiter + setKeywords[i];
        var isNode = setKeywords[i].Contains("NODE");
        var isElement = setKeywords[i].StartsWith("EL");

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
          ParseGeneralGwa(gwa, out string keywordWithVersion, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, true);
          var index = foundIndex ?? 0;
          var originalSid = "";
          var keyword = keywordWithVersion.Split('.').First();

          //For some GET_ALL calls, records with other keywords are returned, too.  Example: GET_ALL TASK returns TASK, TASK_TAG and ANAL records
          if (keyword.Equals(setKeywords[i], StringComparison.InvariantCultureIgnoreCase))
          {
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
            if (!string.IsNullOrEmpty(newSid))
            {
              if (string.IsNullOrEmpty(originalSid))
              {
                gwaWithoutSet = gwaWithoutSet.Replace(keywordWithVersion, keywordWithVersion + ":" + newSid);
              }
              else
              {
                gwaWithoutSet = gwaWithoutSet.Replace(originalSid, newSid);
              }
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
                if (!tempKeywordIndexCache.ContainsKey(keyword))
                {
                  tempKeywordIndexCache.Add(keyword, new List<int>());
                }
                if (!tempKeywordIndexCache[keyword].Contains(index))
                {
                  data.Add(line);
                  tempKeywordIndexCache[keyword].Add(index);
                }
              }
            }
          }
        });

        if (incrementProgress != null)
        {
          incrementProgress.Report(1);
        }
      }

      for (int i = 0; i < setAtKeywords.Count(); i++)
      {
        var highestIndex = ExecuteWithLock(() => GSAObject.GwaCommand("HIGHEST" + GSAProxy.GwaDelimiter + setAtKeywords[i]));

        for (int j = 1; j <= highestIndex; j++)
        {
          var newCommand = string.Join(GwaDelimiter.ToString(), new[] { "GET", setAtKeywords[i], j.ToString() });

          var gwaRecord = "";
          try
          {
            gwaRecord = (string)ExecuteWithLock(() => GSAObject.GwaCommand(newCommand));
          }
          catch { }

          if (gwaRecord != "")
          {
            ParseGeneralGwa(gwaRecord, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType);

            if (keyword.ContainsCaseInsensitive(setAtKeywords[i]))
            {
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
                if (!tempKeywordIndexCache.ContainsKey(setAtKeywords[i]))
                {
                  tempKeywordIndexCache.Add(setAtKeywords[i], new List<int>());
                }
                if (!tempKeywordIndexCache[setAtKeywords[i]].Contains(j))
                {
                  data.Add(line);
                  tempKeywordIndexCache[setAtKeywords[i]].Add(j);
                }
              }
            }
          }
        }
        if (incrementProgress != null)
        {
          incrementProgress.Report(1);
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
      var pieces = gwaRecord.Split(GwaDelimiter);
      return (int.TryParse(pieces[1], out int index)) ? index : 0;
    }


    //Assumed to be the full SET or SET_AT command
    public void SetGwa(string gwaCommand) => ExecuteWithLock(() => batchSetGwa.Add(gwaCommand));

    public void Sync()
    {
      if (batchBlankGwa.Count() > 0)
      {
        var batchBlankCommand = ExecuteWithLock(() => string.Join("\r\n", batchBlankGwa));
        var blankCommandResult = ExecuteWithLock(() => GSAObject.GwaCommand(batchBlankCommand));
        ExecuteWithLock(() => batchBlankGwa.Clear());
      }

      if (batchSetGwa.Count() > 0)
      {
        var batchSetCommand = ExecuteWithLock(() => string.Join("\r\n", batchSetGwa));
        var setCommandResult = ExecuteWithLock(() => GSAObject.GwaCommand(batchSetCommand));
        ExecuteWithLock(() => batchSetGwa.Clear());
      }
    }

    public void GetGSATotal2DElementOffset(int index, double insertionPointOffset, out double offset, out string offsetRec)
    {
      double materialInsertionPointOffset = 0;
      double zMaterialOffset = 0;

      object result = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GSAProxy.GwaDelimiter.ToString(), new[] { "GET", "PROP_2D", index.ToString() })));
      string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

      string res = newPieces.FirstOrDefault();

      if (res == null || res == "")
      {
        offset = insertionPointOffset;
        offsetRec = res;
        return;
      }

      string[] pieces = res.ListSplit(GSAProxy.GwaDelimiter);

      offsetRec = res;

      if (pieces.Length >= 13)
      {
        zMaterialOffset = -Convert.ToDouble(pieces[12]);
        offset = insertionPointOffset + zMaterialOffset + materialInsertionPointOffset;
      }
      else
      {
        offset = 0;
      }
      return;
    }

    public int NodeAt(double x, double y, double z, double coincidenceTol)
    {
      double factor = (UnitNodeAtFactors != null && UnitNodeAtFactors.ContainsKey(units)) ? UnitNodeAtFactors[units] : 1;
      //Note: the outcome of this might need to be added to the caches!
      var index = ExecuteWithLock(() => GSAObject.Gen_NodeAt(x * factor, y * factor, z * factor, coincidenceTol * factor));
      return index;
    }

    public string GetGwaForNode(int index)
    {
      var gwaCommand = string.Join(GwaDelimiter.ToString(), new[] { "GET", "NODE.3", index.ToString() });
      return (string)ExecuteWithLock(() => GSAObject.GwaCommand(gwaCommand));
    }

    public string SetSid(string gwa, string streamId, string applicationId)
    {
      ParseGeneralGwa(gwa, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, true);

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
        object result = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "GET", "LIST", list })));
        string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

        string res = newPieces.FirstOrDefault();

        string[] pieces = res.Split(GSAProxy.GwaDelimiter);

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
      var command = string.Join(GwaDelimiter.ToString(), new[] { (gwaSetCommandType == GwaSetCommandType.Set) ? "BLANK" : "DELETE", keyword, index.ToString() });
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
        ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "SET", "SID", sidRecord })));
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
      string res = (string)ExecuteWithLock(() => GSAObject.GwaCommand("GET" + GSAProxy.GwaDelimiter + "TITLE"));

      string[] pieces = res.ListSplit(GSAProxy.GwaDelimiter);

      return pieces.Length > 1 ? pieces[1] : "My GSA Model";
    }

    public string[] GetTolerances()
    {
      return ((string)ExecuteWithLock(() => GSAObject.GwaCommand("GET" + GSAProxy.GwaDelimiter + "TOL"))).ListSplit(GSAProxy.GwaDelimiter);
    }

    /// <summary>
    /// Updates the GSA unit stored in SpeckleGSA.
    /// </summary>
    public string GetUnits()
    {
      var retrievedUnits = ((string)ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "GET", "UNIT_DATA.1", "LENGTH" })))).ListSplit(GwaDelimiter)[2];
      this.units = retrievedUnits;
      return retrievedUnits;
    }

    public bool SetUnits(string units)
    {
      this.units = units;
      var retCode = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "SET", "UNIT_DATA", "LENGTH", units })));
      retCode = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "SET", "UNIT_DATA", "DISP", units })));
      retCode = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "SET", "UNIT_DATA", "SECTION", units })));
      //Apparently 1 seems to be the code for success, from observation
      return (retCode == 1);
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
        sid = (string)ExecuteWithLock(() => GSAObject.GwaCommand("GET" + GwaDelimiter + "SID"));
      }
      catch
      {
        //File doesn't have SID
      }
      return sid;
    }

    //Created as part of functionality needed to convert a load case specification in the UI into an itemised list of load cases 
    //(including combinations)
    //Since EntitiesInList doesn't offer load cases/combinations as a GsaEntity type, a dummy GSA proxy (and therefore GSA instance) 
    //is created by the GSA cache and that calls the method below - even though it deals with nodes - as a roundabout way of
    //converting a list specification into valid load cases or combinations.   This method is called separately for load cases and combinations. 
    public List<int> GetNodeEntitiesInList(string spec)
    {
      var listType = GsaEntity.NODE;

      //Check that this indeed a list - the EntitiesInList call will function differently if given a single item
      var pieces = spec.Trim().Split(new[] { ' ' });
      if (pieces.Count() == 1)
      {
        spec = pieces[0] + " " + pieces[0];
      }

      var result = GSAObject.EntitiesInList(spec, ref listType, out int[] entities);
      return (entities != null && entities.Count() > 0)
        ? entities.ToList()
        : new List<int>();
    }

    #endregion
  }
}
