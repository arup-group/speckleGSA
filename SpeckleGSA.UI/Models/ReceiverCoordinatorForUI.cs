using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public class ReceiverCoordinatorForUI
  {
    public GSATargetLayer TargetLayer { get; set; } = GSATargetLayer.Design;
    public StreamMethod StreamMethod { get; set; } = StreamMethod.Single;
    public StreamList StreamList { get; set; } = new StreamList();

    public double CoincidentNodeAllowance { get; set; } = 10;
    public GsaUnit CoincidentNodeUnits { get; set; } = GsaUnit.Millimetres;
  }
}
