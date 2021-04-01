using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public class ReceiverCoordinator
  {
    public GSATargetLayer TargetLayer { get; set; }
    public StreamMethod StreamMethod { get; set; }
    public StreamList StreamList { get; set; } = new StreamList();

    public double CoincidentNodeAllowance { get; set; } = 0.01;
    public GsaUnit CoincidentNodeUnits { get; set; } = GsaUnit.Millimetres;
  }
}
