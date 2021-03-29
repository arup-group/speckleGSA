using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public class ReceiverCoordinator
  {
    public StreamList StreamList { get; set; }

    public double CoincidentNodeAllowance { get; set; }
    public GsaUnit CoincidentNodeUnits { get; set; }
  }
}
