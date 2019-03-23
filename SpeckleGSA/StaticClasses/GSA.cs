using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Interop.Gsa_10_0;

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

        public static void GetSpeckleClients(string emailAddress, string serverAddress)
        {
            Senders.Clear();
            Receivers.Clear();

            string res = (string)RunGWACommand("GET_ALL,LIST",false);

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            string senderList = pieces.Where(s => s.Contains("SpeckleSenders:" + emailAddress + ":" + serverAddress)).FirstOrDefault();
            string receiverList = pieces.Where(s => s.Contains("SpeckleReceivers:" + emailAddress + ":" + serverAddress)).FirstOrDefault();

            if (senderList != null)
            {
                string[] pPieces = senderList.ListSplit(",");

                List<string[]> senders = Regex.Matches(pPieces[2], @"(?<={).*?(?=})").Cast<Match>()
                    .Select(m => m.Value.Split(new char[] { ':' }))
                    .Where(s => s.Length == 2)
                    .ToList();

                foreach (string[] sender in senders)
                    Senders[sender[0]] = sender[1];
            }

            if (receiverList != null)
            {
                string[] pPieces = receiverList.ListSplit(",");
                Receivers = Regex.Matches(pPieces[2], @"(?<={).*?(?=})").Cast<Match>()
                    .Select(m => m.Value)
                    .ToList();
            }
        }

        public static void SetSpeckleClients(string emailAddress, string serverAddress)
        {
            string res = (string)RunGWACommand("GET_ALL,LIST",false);

            int senderListReference = 1;
            int receiverListReference = 2;

            if (res != "")
            { 
                string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                string senderList = pieces.Where(s => s.Contains("SpeckleSenders:" + emailAddress + ":" + serverAddress)).FirstOrDefault();
                if (senderList == null)
                    senderListReference = (int)GSA.RunGWACommand("HIGHEST,LIST",false) + 1;
                else
                {
                    string[] pPieces = senderList.ListSplit(",");
                    senderListReference = Convert.ToInt32(pPieces[1]);
                }

                string receiverList = pieces.Where(s => s.Contains("SpeckleReceivers:" + emailAddress + ":" + serverAddress)).FirstOrDefault();
                if (receiverList == null)
                    receiverListReference = (int)GSA.RunGWACommand("HIGHEST,LIST", false) + 2;
                else
                {
                    string[] pPieces = receiverList.ListSplit(",");
                    receiverListReference = Convert.ToInt32(pPieces[1]);
                }
            }

            string senders = string.Join(" ", Senders.Select(kvp => '{' + kvp.Key + ":" + kvp.Value + '}'));
            RunGWACommand("SET,LIST," + senderListReference.ToString() + ",SpeckleSenders:" + emailAddress + ":" + serverAddress + " " + senders +",UNDEF, ", false);
            
            string receivers = string.Join(" ", Receivers.Select(r => '{' + r + '}'));
            RunGWACommand("SET,LIST," + receiverListReference.ToString() + ",SpeckleReceivers:" + emailAddress + ":" + serverAddress + " " + receivers + ",UNDEF, ", false);
        }

        public static string Title()
        {
            string res = (string)RunGWACommand("GET,TITLE");

            string[] pieces = res.ListSplit(",");

            return pieces[1];
        }
        
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
        
        public static void BlankDepreciatedGWASetCommands()
        {
            List<string> prevSets = PreviousGSASetCache.Keys.Where(l => l.Contains("SET")).ToList();

            for (int i = 0; i < prevSets.Count(); i++)
            {
                string[] split = prevSets[i].ListSplit(",");
                prevSets[i] = split[1] + "," + split[2];

            }

            prevSets = prevSets.Where(l => !GSASetCache.Keys.Any(x => x.Contains(l))).ToList();
            
            foreach(string p in prevSets)
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

        public static object RunGWACommand(string command, bool cache = true)
        {
            if (!IsInit)
                return "";

            if (cache)
            {
                if (command.Contains("GET") & !command.Contains("HIGHEST"))
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

                if (command.Contains("SET"))
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
        
        public static void UpdateViews()
        {
            GSAObject.UpdateViews();
        }

        public static void UpdateUnits()
        {
            Units = ((string)RunGWACommand("GET,UNIT_DATA.1,LENGTH", false)).ListSplit(",")[2];
        }

        public static int NodeAt(double x, double y, double z)
        {
            int idx = GSAObject.Gen_NodeAt(x, y, z, Settings.CoincidentNodeAllowance);

            // Add to ref counters
            Indexer.ReserveIndices(typeof(GSANode).GetGSAKeyword(), new List<int>() { idx });

            // Add artificial cache
            if (!GSASetCache.ContainsKey("SET,NODE.2," + idx.ToString()))
                GSASetCache["SET,NODE.2," + idx.ToString()] = 0;

            return idx;
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
    }
}
