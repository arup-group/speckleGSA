using SpeckleGSA;
using SpeckleGSAInterfaces;
using System.Collections.Generic;

namespace SpeckleGSAUI.Models
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
