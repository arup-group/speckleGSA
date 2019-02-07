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
        
        public static string Units;

        public static void Init()
        {
            if (IsInit)
                return;

            GSAObject = new ComAuto();

            TargetAnalysisLayer = false;
            TargetDesignLayer = true; // TODO

            IsInit = true;

            Status.AddMessage("Linked to GSA.");
        }

        public static void NewFile()
        {
            if (!IsInit)
                return;

            GSAObject.NewFile();
            GSAObject.DisplayGsaWindow(true);
            UpdateUnits();

            Status.AddMessage("Created new file.");
        }

        public static void OpenFile(string path)
        {
            if (!IsInit)
                return;
        
            GSAObject.Close();
            GSAObject.Open(path);
            GSAObject.DisplayGsaWindow(true);

            Status.AddMessage("Opened new file.");
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

        public static void UpdateUnits()
        {
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
    }
}
