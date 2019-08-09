using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy
{
	public class Settings : IGSASettings
	{
		public bool TargetDesignLayer { get; set; }
		public bool TargetAnalysisLayer { get; set; }
		public string Units { get; set; }
		public double CoincidentNodeAllowance { get; set; }
	}
}
