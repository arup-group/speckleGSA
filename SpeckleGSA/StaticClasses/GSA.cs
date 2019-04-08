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
        public static Dictionary<string, string> Senders { get; set; }
        public static List<string> Receivers { get; set; }

        public static string DbPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles) + @"\Oasys\GSA 10.0\sectlib.db3";

        public static void Init()
        {
            if (IsInit)
                return;

            //GSAObject = new ComAuto();
            
            TargetAnalysisLayer = false;
            TargetDesignLayer = true; // TODO

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
        public static string[] GetGWAGetCommands(string command)
        {
            if (!command.Contains("GET"))
                throw new Exception("GetGWAGetCommands() only takes in GET commands");

            object result = RunGWACommand(command);
            string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();
            return newPieces;
        }

        public static string[] GetNewGWAGetCommands(string command)
        {
            if (!command.Contains("GET"))
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

        public static string[] GetDeletedGWAGetCommands(string command)
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

        public static object RunGWACommand(string command, bool cache = true)
        {
            if (!IsInit)
                return "";

            if (cache)
            {
                if (command.StartsWith("GET") & !command.StartsWith("HIGHEST"))
                {
                    if (!GSAGetCache.ContainsKey(command))
                    {
                        if (command.Contains("GET_ALL,MEMB"))
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
                        RunGWACommand("BLANK," + split[1] + "," + split[0], false);
                }
            }
        }
        #endregion

        #region Operations
        public static string Title()
        {
            string res = (string)RunGWACommand("GET,TITLE");

            string[] pieces = res.ListSplit(",");

            return pieces[1];
        }

        public static Dictionary<string, object> GetBaseProperties()
        {
            Dictionary<string, object> baseProps = new Dictionary<string, object>();

            baseProps["units"] = Units.LongUnitName();

            string[] tolerances = ((string)RunGWACommand("GET,TOL")).ListSplit(",");

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

        public static void UpdateViews()
        {
            GSAObject.UpdateViews();
        }

        public static void UpdateUnits()
        {
            Units = ((string)RunGWACommand("GET,UNIT_DATA.1,LENGTH", false)).ListSplit(",")[2];
        }

        public static int NodeAt(double x, double y, double z, string structuralID = null)
        {
            int idx = GSAObject.Gen_NodeAt(x, y, z, Settings.CoincidentNodeAllowance);
            
            if (structuralID != null)
                Indexer.ReserveIndices(typeof(GSANode), new List<int>() { idx }, new List<string>() { structuralID });
            else
                Indexer.ReserveIndices(typeof(GSANode).GetGSAKeyword(), new List<int>() { idx });

            // Add artificial cache
            string cacheKey = "SET," + typeof(GSANode).GetGSAKeyword() + "," + idx.ToString() + ",";
            if (!GSASetCache.ContainsKey(cacheKey))
                GSASetCache[cacheKey] = 0;

            return idx;
        }
        
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

        #region Cache
        public static void ClearCache()
        {
            PreviousGSAGetCache = new Dictionary<string, object>(GSAGetCache);
            GSAGetCache.Clear();
            PreviousGSASetCache = new Dictionary<string,object>(GSASetCache);
            GSASetCache.Clear();
        }

        public static void FullClearCache()
        {
            PreviousGSAGetCache.Clear();
            GSAGetCache.Clear();
            PreviousGSASetCache.Clear();
            GSASetCache.Clear();
        }
        #endregion
    }

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
}
