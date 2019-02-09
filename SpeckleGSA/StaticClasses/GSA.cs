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

            GSAObject = new ComAuto();
            
            TargetAnalysisLayer = false;
            TargetDesignLayer = true; // TODO

            IsInit = true;

            GSACache = new Dictionary<string, object>();

            Senders = new Dictionary<string, string>();
            Receivers = new List<string>();

            Status.AddMessage("Linked to GSA.");
        }

        public static void NewFile()
        {
            if (!IsInit)
                return;

            GSAObject.NewFile();
            GSAObject.DisplayGsaWindow(true);

            ClearCache();
            GetSpeckleClients();

            Status.AddMessage("Created new file.");
        }

        public static void OpenFile(string path)
        {
            if (!IsInit)
                return;
        
            try
            { 
                GSAObject.Close();
                GSAObject.Open(path);
                GSAObject.DisplayGsaWindow(true);
            }
            catch
            {
                GSAObject = new ComAuto();
                GSAObject.Open(path);
                GSAObject.DisplayGsaWindow(true);
            }
            
            ClearCache();
            GetSpeckleClients();

            Status.AddMessage("Opened new file.");
        }

        public static void GetSpeckleClients()
        {
            Senders.Clear();
            Receivers.Clear();

            string res = (string)RunGWACommand("GET_ALL,LIST");

            if (res == "")
                return;

            string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            string senderList = pieces.Where(s => s.Contains("SpeckleSenders")).FirstOrDefault();
            string receiverList = pieces.Where(s => s.Contains("SpeckleReceivers")).FirstOrDefault();

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

        public static void SetSpeckleClients()
        {
            string res = (string)RunGWACommand("GET_ALL,LIST");

            int senderListReference = 1;
            int receiverListReference = 2;

            if (res != "")
            { 
                string[] pieces = res.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                string senderList = pieces.Where(s => s.Contains("SpeckleSenders")).FirstOrDefault();
                if (senderList == null)
                    senderListReference = (int)GSA.RunGWACommand("HIGHEST,LIST") + 1;
                else
                {
                    string[] pPieces = senderList.ListSplit(",");
                    senderListReference = Convert.ToInt32(pPieces[1]);
                }

                string receiverList = pieces.Where(s => s.Contains("SpeckleReceivers")).FirstOrDefault();
                if (receiverList == null)
                    receiverListReference = (int)GSA.RunGWACommand("HIGHEST,LIST") + 2;
                else
                {
                    string[] pPieces = receiverList.ListSplit(",");
                    receiverListReference = Convert.ToInt32(pPieces[1]);
                }
            }

            string senders = string.Join(" ", Senders.Select(kvp => "\"" + kvp.Key + ":" + kvp.Value + "\""));
            RunGWACommand("SET,LIST," + senderListReference.ToString() + ",SpeckleSenders " + senders +",UNDEF, ");
            
            string receivers = string.Join(" ", Receivers.Select(r => "\"" + r + "\""));
            RunGWACommand("SET,LIST," + receiverListReference.ToString() + ",SpeckleReceivers " + receivers + ",UNDEF, ");
        }

        public static string Title()
        {
            string res = (string)RunGWACommand("GET,TITLE");

            string[] pieces = res.ListSplit(",");

            return pieces[1];
        }

        public static object RunGWACommand(string command)
        {
            if (!IsInit)
                return "";

            if (!command.Contains("HIGHEST"))
            {
                if (!GSACache.ContainsKey(command))
                    GSACache[command] = GSAObject.GwaCommand(command);

                return GSACache[command];
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
