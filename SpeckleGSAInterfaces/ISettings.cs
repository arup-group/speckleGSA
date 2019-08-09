namespace SpeckleGSAInterfaces
{
	public interface IGSASettings
	{
		bool TargetDesignLayer { get; set; }
		bool TargetAnalysisLayer { get; set; }
		string Units { get; set; }
		double CoincidentNodeAllowance { get; set; }
	}
}
