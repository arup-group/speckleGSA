using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  public interface IGSALocalProxy : IGSAProxy
  {
    void SetAppVersionForTelemetry(string speckleGsaAppVersion);
    void SendTelemetry(params string[] messagePortions);
    bool SetUnits(string units);
    //bool Clear(ResultCsvGroup group);
  }
}
