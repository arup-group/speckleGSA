using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSA.UI.Models
{
  public class ReceiverTab : TabBase
  {
    public double CoincidentNodeAllowance { get; set; } = 10;
    public GsaUnit CoincidentNodeUnits { get; set; } = GsaUnit.Millimetres;
    public List<SidSpeckleRecord> ReceiverSidRecords { get => this.sidSpeckleRecords; }

    public ReceiverTab() : base(GSATargetLayer.Design)
    {

    }
  }
}
