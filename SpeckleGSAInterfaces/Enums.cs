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
}
