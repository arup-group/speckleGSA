using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        
        private static Dictionary<string, object> GSACache;

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


            GSACache = new Dictionary<string, object>();
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
                List<string[]> senders = pPieces[2].ListSplit(" ")
                    .Where(s => !s.Contains("SpeckleSenders") && s.Length > 0)
                    .Select(s => s.Trim(new char[] { '"' })
                    .Split(new char[] { ':' }))
                    .Where(s => s.Length == 2)
                    .ToList();

                foreach (string[] sender in senders)
                    Senders[sender[0]] = sender[1];
            }

            if (receiverList != null)
            {
                string[] pPieces = receiverList.ListSplit(",");
                Receivers = pPieces[2].ListSplit(" ")
                    .Where(s => !s.Contains("SpeckleReceivers") && s.Length > 0)
                    .Select(s => s.Trim(new char[] { '"' }))
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

            string senders = string.Join(" ", Senders.Select(kvp => "\"" + kvp.Key + ":" + kvp.Value + "\""));
            RunGWACommand("SET,LIST," + senderListReference.ToString() + ",SpeckleSenders:" + emailAddress + ":" + serverAddress + " " + senders +",UNDEF, ", false);
            
            string receivers = string.Join(" ", Receivers.Select(r => "\"" + r + "\""));
            RunGWACommand("SET,LIST," + receiverListReference.ToString() + ",SpeckleReceivers:" + emailAddress + ":" + serverAddress + " " + receivers + ",UNDEF, ", false);
        }

        public static string Title()
        {
            string res = (string)RunGWACommand("GET,TITLE");

            string[] pieces = res.ListSplit(",");

            return pieces[1];
        }

        public static object RunGWACommand(string command, bool cache = true)
        {
            if (!IsInit)
                return "";

            if (cache)
            { 
                if (command.Contains("GET") & !command.Contains("HIGHEST"))
                {
                    if (!GSACache.ContainsKey(command))
                        GSACache[command] = GSAObject.GwaCommand(command);

                    return GSACache[command];
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
            GSACache = new Dictionary<string, object>();

            Units = ((string)RunGWACommand("GET,UNIT_DATA.1,LENGTH")).ListSplit(",")[2];
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
            GSACache.Clear();
        }
    }
}
