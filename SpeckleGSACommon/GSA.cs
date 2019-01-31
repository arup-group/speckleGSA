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

        public static void Init()
        {
            if (IsInit)
                return;

            GSAObject = new ComAuto();

            TargetAnalysisLayer = false;
            TargetDesignLayer = true; // TODO

            IsInit = true;
        }

        public static void NewFile()
        {
            if (!IsInit)
                return;

            GSAObject.NewFile();
            GSAObject.DisplayGsaWindow(true);
        }

        public static void OpenFile(string path)
        {
            if (!IsInit)
                return;

            GSAObject.Open(path);
            GSAObject.DisplayGsaWindow(true);
        }

        public static object RunGWACommand(string command)
        {
            if (!IsInit)
                return "";

            return GSAObject.GwaCommand(command);
        }
        
        public static void UpdateViews()
        {
            GSAObject.UpdateViews();
        }
    }
}
