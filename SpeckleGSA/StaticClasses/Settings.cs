using SpeckleGSAInterfaces;
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
  public class Settings : IGSASettings
	{
    public bool SendOnlyMeaningfulNodes = true;
    public bool SeparateStreams = false;
    public int PollingRate = 2000;

		public string Units { get; set; }
		public bool TargetDesignLayer { get => targetDesignLayer; set { targetDesignLayer = value; } }
		public bool TargetAnalysisLayer { get => targetAnalysisLayer; set { targetAnalysisLayer = value; } }
		public double CoincidentNodeAllowance { get => coincidentNodeAllowance; set { coincidentNodeAllowance = value; } }
		public bool SendOnlyResults { get => sendOnlyResults; set { sendOnlyResults = value; } }

		//Default values
		private bool targetAnalysisLayer = false;
		private bool targetDesignLayer = true;
		private double coincidentNodeAllowance = 0.1;
		private bool sendOnlyResults = false;

		public bool SendResults = false;

    public Dictionary<string, Tuple<int, int, List<string>>> ChosenNodalResult = new Dictionary<string, Tuple<int, int, List<string>>>();
    public Dictionary<string, Tuple<int, int, List<string>>> ChosenElement1DResult = new Dictionary<string, Tuple<int, int, List<string>>>();
    public Dictionary<string, Tuple<int, int, List<string>>> ChosenElement2DResult = new Dictionary<string, Tuple<int, int, List<string>>>();
    public Dictionary<string, Tuple<string, int, int, List<string>>> ChosenMiscResult = new Dictionary<string, Tuple<string, int, int, List<string>>>();

    public bool EmbedResults = true;
    public List<string> ResultCases = new List<string>();
    public bool ResultInLocalAxis = false;
    public int Result1DNumPosition = 3;

		
	}
}
