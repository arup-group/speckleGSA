namespace SpeckleGSAInterfaces
{
	public enum MessageLevel
  {
		Debug,
		Information,
		Error,
		Fatal
  }

	public enum MessageIntent
	{
		Display,
		TechnicalLog,
		Telemetry
	}

	public enum GSATargetLayer
	{
		None,
		Design,
		Analysis
	}

	public enum GSAEntity
	{
		NotSet = 0,
		NODE = 1,
		ELEMENT = 2,
		MEMBER = 3,
		LINE = 6,
		AREA = 7,
		REGION = 8
	}

  public enum GwaSetCommandType
  {
    Set,
    SetAt
  }

	public enum StreamContentConfig
	{
		None,
		ModelOnly,
		ModelWithEmbeddedResults,
		ModelWithTabularResults,
		TabularResultsOnly
	}

	public enum ResultCsvGroup
	{
		Unknown = 0,
		Node,
		Element1d,
		Element2d,
		Assembly
	}
	
	public enum ResultType
  {
		NodalDisplacements,
		NodalVelocity,
		NodalAcceleration,
		NodalReaction,
		ConstraintForces,
		Element1dDisplacement,
		Element1dForce,
		Element2dDisplacement,
		Element2dProjectedMoment,
		Element2dProjectedForce,
		Element2dProjectedStressBottom,
		Element2dProjectedStressMiddle,
		Element2dProjectedStressTop,
		AssemblyForcesAndMoments
	}
}
