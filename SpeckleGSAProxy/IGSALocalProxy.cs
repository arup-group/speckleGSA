using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy
{
  public interface IGSALocalProxy : IGSAProxy
  {
    void SendTelemetry(params string[] messagePortions);
    bool SetUnits(string units);
  }
}
