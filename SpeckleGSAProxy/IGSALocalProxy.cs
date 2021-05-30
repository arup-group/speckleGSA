using SpeckleGSAInterfaces;

namespace SpeckleGSAProxy
{
  public interface IGSALocalProxy : IGSAProxy
  {
    void SetAppVersionForTelemetry(string speckleGsaAppVersion);
    void SendTelemetry(params string[] messagePortions);
    bool SetUnits(string units);
  }
}
