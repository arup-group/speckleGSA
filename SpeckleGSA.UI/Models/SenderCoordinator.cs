using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public class SenderCoordinator
  {
    public GSATargetLayer TargetLayer { get; set; }
    public StreamMethod StreamMethod { get; set; } = StreamMethod.Single;
    public StreamList StreamList { get; set; } = new StreamList();

    public double PollingRateMilliseconds { get; set; } = 2000;

    public StreamContentConfig StreamContentConfig { get; set; } = StreamContentConfig.ModelOnly;

    public string LoadCaseList { get; set; }
    public int AdditionalPositionsFor1dElements { get; set; }

    public ResultSettings ResultSettings { get; set; } = new ResultSettings();
  }
}
