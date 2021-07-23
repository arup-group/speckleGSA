using SpeckleGSAInterfaces;

namespace SpeckleGSA
{
  public interface IGSALocalSettings : IGSASettings
  {
    int LoggingMinimumLevel { get; set; }
    bool SendResults { get; }
    bool SendOnlyMeaningfulNodes { get; set; }
    string ServerAddress { get; set; }
    bool VerboseErrors { get; set; }
  }
}
