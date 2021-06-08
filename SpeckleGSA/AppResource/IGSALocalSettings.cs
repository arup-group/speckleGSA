using SpeckleGSAInterfaces;

namespace SpeckleGSA
{
  public interface IGSALocalSettings : IGSASettings
  {
    int LoggingMinimumLevel { get; set; }
    bool SendOnlyResults { get; set; }
    bool SendOnlyMeaningfulNodes { get; set; }
    bool SeparateStreams { get; set; }
    string ServerAddress { get; set; }
    bool VerboseErrors { get; set; }
  }
}
