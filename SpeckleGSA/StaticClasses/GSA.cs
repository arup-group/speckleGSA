using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Interop.Gsa_10_0;
using SQLite;

namespace SpeckleGSA
{
    /// <summary>
    /// Static class which interfaces with GSA
    /// </summary>
    public static class GSA
    {
        public static ComAuto GSAObject;

        public static bool IsInit;

        public static bool TargetAnalysisLayer;
        public static bool TargetDesignLayer;

        private static Dictionary<string, object> PreviousGSAGetCache;
        private static Dictionary<string, object> GSAGetCache;

        private static Dictionary<string, object> PreviousGSASetCache;
        private static Dictionary<string, object> GSASetCache;

        public static string Units { get; private set; }
        public static Dictionary<string, string>    Senders { get; set; }
        public static List<string> Receivers { get; set; }

        public static string DbPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles) + @"\Oasys\GSA 10.0\sectlib.db3";

        public static void Init()
        {
            if (IsInit)
                return;
            
            TargetAnalysisLayer = false;
            TargetDesignLayer = true;

            PreviousGSAGetCache = new Dictionary<string, object>();
            GSAGetCache = new Dictionary<string, object>();
            PreviousGSASetCache = new Dictionary<string, object>();
            GSASetCache = new Dictionary<string, object>();
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

            GSAObject = new ComAuto();
            GSAObject.LogFeatureUsage("api::specklegsa::" +
                FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                    .ProductVersion + "::GSA " + GSAObject.VersionString()
                    .Split(new char[] { '\n' })[0]
                    .Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);
            GSAObject.NewFile();
            GSAObject.DisplayGsaWindow(true);

            ClearCache();
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

            GSAObject = new ComAuto();
            GSAObject.LogFeatureUsage("api::specklegsa::" +
                FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                    .ProductVersion + "::GSA " + GSAObject.VersionString()
                    .Split(new char[] { '\n' })[0]
                    .Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);
            GSAObject.Open(path);
            GSAObject.DisplayGsaWindow(true);
            
            ClearCache();
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
            ClearCache();
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
            string res = (string)RunGWACommand("GET,SID", false);

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

                for (int i = 0; i < senders.Length; i+=2)
                    Senders[senders[i]] = senders[i+1];
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
            string res = (string)RunGWACommand("GET,SID", false);

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

            RunGWACommand("SET,SID," + sidRecord, false);
        }
        #endregion

        #region GWA Command
        /// <summary>
        /// Returns a list of GWA records with the index of the record prepended.
        /// </summary>
        /// <param name="command">GET GWA command</param>
        /// <returns>Array of GWA records</returns>
        public static string[] GetGWARecords(string command)
        {
            if (!command.StartsWith("GET"))
                throw new Exception("GetGWAGetCommands() only takes in GET commands");

            object result = RunGWACommand(command);
            string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();
            return newPieces;
        }

        /// <summary>
        /// Returns a list of new GWA records with the index of the record prepended.
        /// </summary>
        /// <param name="command">GET GWA command</param>
        /// <returns>Array of GWA records</returns>
        public static string[] GetNewGWARecords(string command)
        {
            if (!command.StartsWith("GET"))
                throw new Exception("GetNewGWAGetCommands() only takes in GET commands");

            object result = RunGWACommand(command);

            if (PreviousGSAGetCache.ContainsKey(command))
            {
                if ((result as string) == (PreviousGSAGetCache[command] as string))
                    return new string[0];

                string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s,idx) => idx.ToString() + ":" + s).ToArray();
                string[] prevPieces = ((string)PreviousGSAGetCache[command]).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

                string[] ret = newPieces.Where(n => !prevPieces.Contains(n)).ToArray();
                
                return ret;
            }
            else
            {
                return ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();
            }
        }

        /// <summary>
        /// Returns a list of deleted GWA records with the index of the record prepended.
        /// </summary>
        /// <param name="command">GET GWA command</param>
        /// <returns>Array of GWA records</returns>
        public static string[] GetDeletedGWARecords(string command)
        {
            if (!command.Contains("GET"))
                throw new Exception("GetDeletedGWAGetCommands() only takes in GET commands");

            object result = RunGWACommand(command);

            if (PreviousGSAGetCache.ContainsKey(command))
            {
                if ((result as string) == (PreviousGSAGetCache[command] as string))
                    return new string[0];

                string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();
                string[] prevPieces = ((string)PreviousGSAGetCache[command]).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

                string[] ret = prevPieces.Where(p => !newPieces.Contains(p)).ToArray();
                
                return ret;
            }
            else
                return new string[0];
        }

        /// <summary>
        /// Runs a GWA command with the option to cache GET and SET commands.
        /// </summary>
        /// <param name="command">GWA command</param>
        /// <param name="cache">Use cache</param>
        /// <returns>GWA command return object</returns>
        public static object RunGWACommand(string command, bool cache = true)
        {
            if (!IsInit)
                return "";

            if (cache)
            {
                if (command.StartsWith("GET"))
                {
                    if (!GSAGetCache.ContainsKey(command))
                    {
                        if (command.StartsWith("GET_ALL,MEMB"))
                        {
                            // TODO: Member GET_ALL work around
                            int[] memberRefs = new int[0];
                            GSAObject.EntitiesInList("all", GsaEntity.MEMBER, out memberRefs);

                            if (memberRefs == null || memberRefs.Length == 0)
                                return "";

                            List<string> result = new List<string>();

                            foreach (int r in memberRefs)
                                (result as List<string>).Add((string)RunGWACommand("GET,MEMB," + r.ToString()));

                            GSAGetCache[command] = string.Join("\n", result);
                        }
                        else if (command.StartsWith("GET_ALL,ANAL"))
                        {
                            // TODO: Anal GET_ALL work around
                            int highestRef = (int)RunGWACommand("HIGHEST,ANAL.1");

                            List<string> result = new List<string>();

                            for (int i = 1; i <= highestRef; i++)
                            {
                                string res = (string)RunGWACommand("GET,ANAL," + i.ToString());
                                if (res != null && res != "")
                                    (result as List<string>).Add(res);
                            }

                            GSAGetCache[command] = string.Join("\n", result);
                        }
                        else
                        {
                            GSAGetCache[command] = GSAObject.GwaCommand(command);
                        }
                    }

                    return GSAGetCache[command];
                }

                if (command.StartsWith("SET"))
                {
                    if (PreviousGSASetCache.ContainsKey(command))
                        GSASetCache[command] = PreviousGSASetCache[command];

                    if (!GSASetCache.ContainsKey(command))
                        GSASetCache[command] = GSAObject.GwaCommand(command);

                    return GSASetCache[command];
                }
            }

            return GSAObject.GwaCommand(command);
        }

        /// <summary>
        /// BLANKS all SET GWA records which are in the previous cache, but not the current cache.
        /// </summary>
        public static void BlankDepreciatedGWASetCommands()
        {
            List<string> prevSets = PreviousGSASetCache.Keys.Where(l => l.StartsWith("SET")).ToList();

            for (int i = 0; i < prevSets.Count(); i++)
            {
                string[] split = prevSets[i].ListSplit(",");
                prevSets[i] = split[1] + "," + split[2] + ",";

            }

            prevSets = prevSets.Where(l => !GSASetCache.Keys.Any(x => x.Contains(l))).ToList();

            foreach (string p in prevSets)
            {
                string[] split = p.ListSplit(",");

                if (split[1].IsDigits())
                {
                    // Uses SET
                    if (!Indexer.InBaseline(split[0], Convert.ToInt32(split[1])))
                        RunGWACommand("BLANK," + split[0] + "," + split[1], false);
                }
                else
                {
                    // Uses SET_AT
                    if (!Indexer.InBaseline(split[1], Convert.ToInt32(split[0])))
                    { 
                        RunGWACommand("BLANK," + split[1] + "," + split[0], false);
                        // TODO: Need to DELETE instead of BLANK, but also shift the cache indices
                    }
                }
            }
        }
        #endregion

        #region Document Properties
        /// <summary>
        /// Extract the title of the GSA model.
        /// </summary>
        /// <returns>GSA model title</returns>
        public static string Title()
        {
            string res = GetGWARecords("GET,TITLE").FirstOrDefault();

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

            string[] tolerances = GetGWARecords("GET,TOL").FirstOrDefault().ListSplit(",");

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
            Units = GetGWARecords("GET,UNIT_DATA.1,LENGTH").FirstOrDefault().ListSplit(",")[2];
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
        #endregion

        #region Sections
        /// <summary>
        /// Transforms a GSA category section description into a generic section description.
        /// </summary>
        /// <param name="description"></param>
        /// <returns>Generic section description</returns>
        public static string TransformCategorySection(string description)
        {
            string[] pieces = description.ListSplit("%");

            try
            { 
                using (SQLiteConnection conn = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadOnly))
                {
                    string query_type = "SELECT TYPE_NUM" +
                        " FROM Types" +
                        " WHERE TYPE_ABR = ?";

                    IEnumerable<GSASectionType> type = conn.Query<GSASectionType>(query_type, new object[] { pieces[1] });

                    if (type.Count() == 0)
                        return null;

                    int typeNum = type.ToList()[0].TYPE_NUM;

                    string query_sect = "SELECT SECT_SHAPE, SECT_DEPTH_DIAM, SECT_WIDTH, SECT_WEB_THICK, SECT_FLG_THICK" +
                        " FROM Sect" +
                        " WHERE SECT_TYPE_NUM = ?";

                    IEnumerable<GSASection> sect = conn.Query<GSASection>(query_sect, new object[] { typeNum });

                    if (sect.Count() == 0)
                        return null;

                    GSASection s = sect.ToList()[0];

                    switch((HelperFunctions.GSACAtSectionType)s.SECT_SHAPE)
                    {
                        case HelperFunctions.GSACAtSectionType.I:
                            return "STD%I(m)%" + s.SECT_DEPTH_DIAM + "%" + s.SECT_WIDTH + "%" + s.SECT_WEB_THICK + "%" + s.SECT_FLG_THICK;
                        case HelperFunctions.GSACAtSectionType.CastellatedI:
                            Status.AddError("Castillated sections not supported.");
                            return null;
                        case HelperFunctions.GSACAtSectionType.Channel:
                            return "STD%CH(m)%" + s.SECT_DEPTH_DIAM + "%" + s.SECT_WIDTH + "%" + s.SECT_WEB_THICK + "%" + s.SECT_FLG_THICK;
                        case HelperFunctions.GSACAtSectionType.T:
                            return "STD%T(m)%" + s.SECT_DEPTH_DIAM + "%" + s.SECT_WIDTH + "%" + s.SECT_WEB_THICK + "%" + s.SECT_FLG_THICK;
                        case HelperFunctions.GSACAtSectionType.Angles:
                            return "STD%A(m)%" + s.SECT_DEPTH_DIAM + "%" + s.SECT_WIDTH + "%" + s.SECT_WEB_THICK + "%" + s.SECT_FLG_THICK;
                        case HelperFunctions.GSACAtSectionType.DoubleAngles:
                            Status.AddError("Double angle sections not supported.");
                            return null;
                        case HelperFunctions.GSACAtSectionType.CircularHollow:
                            return "STD%CHS(m)%" + s.SECT_DEPTH_DIAM + "%" + s.SECT_WEB_THICK;
                        case HelperFunctions.GSACAtSectionType.Circular:
                            return "STD%C(m)%" + s.SECT_DEPTH_DIAM;
                        case HelperFunctions.GSACAtSectionType.RectangularHollow:
                            return "STD%RHS(m)%" + s.SECT_DEPTH_DIAM + "%" + s.SECT_WIDTH + "%" + s.SECT_WEB_THICK + "%" + s.SECT_FLG_THICK;
                        case HelperFunctions.GSACAtSectionType.Rectangular:
                            return "STD%R(m)%" + s.SECT_DEPTH_DIAM + "%" + s.SECT_WIDTH;
                        case HelperFunctions.GSACAtSectionType.Oval:
                            return "STD%OVAL(m)%" + s.SECT_DEPTH_DIAM + "%" + s.SECT_WIDTH + "%" + s.SECT_WEB_THICK;
                        case HelperFunctions.GSACAtSectionType.TwoChannelsLaces:
                            Status.AddError("Double channel sections not supported.");
                            return null;
                        default:
                            Status.AddError("Unknown section type.");
                            return null;
                    }
                }
            }
            catch { return null; }
        }
        #endregion

        #region Nodes
        /// <summary>
        /// Create new node at the coordinate. If a node already exists, no new nodes are created. Updates Indexer with the index.
        /// </summary>
        /// <param name="x">X coordinate of the node</param>
        /// <param name="y">Y coordinate of the node</param>
        /// <param name="z">Z coordinate of the node</param>
        /// <param name="structuralID">Structural ID of the node</param>
        /// <returns>Node index</returns>
        public static int NodeAt(double x, double y, double z, string structuralID = null)
        {
            int idx = GSAObject.Gen_NodeAt(x, y, z, Settings.CoincidentNodeAllowance);

            if (structuralID != null)
                Indexer.ReserveIndicesAndMap(typeof(GSANode), new List<int>() { idx }, new List<string>() { structuralID });
            else
                Indexer.ReserveIndices(typeof(GSANode), new List<int>() { idx });

            // Add artificial cache
            string cacheKey = "SET," + typeof(GSANode).GetGSAKeyword() + "," + idx.ToString() + ",";
            if (!GSASetCache.ContainsKey(cacheKey))
                GSASetCache[cacheKey] = 0;

            return idx;
        }
        #endregion

        #region List
        /// <summary>
        /// Converts a GSA list to a list of indices.
        /// </summary>
        /// <param name="list">GSA list</param>
        /// <param name="type">GSA entity type</param>
        /// <returns></returns>
        public static int[] ConvertGSAList(this string list, GsaEntity type)
        {
            if (list == null) return new int[0];

            string[] pieces = list.ListSplit(" ");
            pieces = pieces.Where(s => !string.IsNullOrEmpty(s)).ToArray();

            List<int> items = new List<int>();
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].IsDigits())
                    items.Add(Convert.ToInt32(pieces[i]));
                else if (pieces[i].Contains('"'))
                    items.AddRange(pieces[i].ConvertNamedGSAList(type));
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
                        int[] itemTemp = new int[0];
                        GSA.GSAObject.EntitiesInList(pieces[i], type, out itemTemp);
                        items.AddRange(itemTemp);
                    }
                    catch
                    { }
                }
            }

            return items.ToArray();
        }

        /// <summary>
        /// Converts a named GSA list to a list of indices.
        /// </summary>
        /// <param name="list">GSA list</param>
        /// <param name="type">GSA entity type</param>
        /// <returns></returns>
        public static int[] ConvertNamedGSAList(this string list, GsaEntity type)
        {
            list = list.Trim(new char[] { '"' });

            string res = GSA.GetGWARecords("GET,LIST," + list).FirstOrDefault();

            string[] pieces = res.Split(new char[] { ',' });

            return pieces[pieces.Length - 1].ConvertGSAList(type);
        }

        /// <summary>
        /// Extracts and return the group indicies in the list.
        /// </summary>
        /// <param name="list">List</param>
        /// <returns>Array of group indices</returns>
        public static int[] GetGroupsFromGSAList(this string list)
        {
            string[] pieces = list.ListSplit(" ");

            List<int> groups = new List<int>();

            foreach (string p in pieces)
                if (p.Length > 0 && p[0] == 'G')
                    groups.Add(Convert.ToInt32(p.Substring(1)));

            return groups.ToArray();
        }
        #endregion

        #region Cache
        /// <summary>
        /// Move current cache into previous cache.
        /// </summary>
        public static void ClearCache()
        {
            PreviousGSAGetCache = new Dictionary<string, object>(GSAGetCache);
            GSAGetCache.Clear();
            PreviousGSASetCache = new Dictionary<string,object>(GSASetCache);
            GSASetCache.Clear();
        }

        /// <summary>
        /// Clear current and previous cache.
        /// </summary>
        public static void FullClearCache()
        {
            PreviousGSAGetCache.Clear();
            GSAGetCache.Clear();
            PreviousGSASetCache.Clear();
            GSASetCache.Clear();
        }

        /// <summary>
        /// Blanks all records within the current and previous cache.
        /// </summary>
        public static void DeleteSpeckleObjects()
        {
            GSA.BlankDepreciatedGWASetCommands();
            GSA.ClearCache();
            GSA.BlankDepreciatedGWASetCommands();
            GSA.UpdateViews();
        }
        #endregion
    }

    #region GSA Category Section Helper Classes
    public class GSASection
    {
        public int SECT_SHAPE { get; set; }
        public float SECT_DEPTH_DIAM { get; set; }
        public float SECT_WIDTH { get; set; }
        public float SECT_WEB_THICK { get; set; }
        public float SECT_FLG_THICK { get; set; }
    }

    public class GSASectionType
    {
        public int TYPE_NUM { get; set; }
    }
    #endregion
}
