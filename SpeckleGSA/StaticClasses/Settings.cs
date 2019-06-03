using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    /// <summary>
    /// Static class to store settings.
    /// </summary>
    public class Settings
    {
        public static bool SendOnlyMeaningfulNodes = true;
        public static bool SeperateStreams = false;
        public static int PollingRate = 2000;
        public static double CoincidentNodeAllowance = 0.1;
        public static List<string> ResultCases = new List<string>();
        public static bool ResultInLocalAxis = false;

        public static bool TargetAnalysisLayer = false;
        public static bool TargetDesignLayer = true;
        public static bool SendResults = false;
    }
}
