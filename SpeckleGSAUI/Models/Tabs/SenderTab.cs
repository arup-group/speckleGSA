using SpeckleGSA;
using SpeckleGSAInterfaces;
using System.Collections.Generic;

namespace SpeckleGSAUI.Models
{
  public class SenderTab : TabBase
  {
    public StreamContentConfig StreamContentConfig { get; set; } = StreamContentConfig.ModelOnly;

    public string LoadCaseList { get; set; }
    public int AdditionalPositionsFor1dElements { get; set; } = 3;

    public ResultSettings ResultSettings { get; set; } = new ResultSettings();

    public List<SidSpeckleRecord> SenderSidRecords { get => this.sidSpeckleRecords; }

    private string documentTitle = "";

    public SenderTab() : base(GSATargetLayer.Analysis)
    {

    }


    internal void SetDocumentName(string name)
    {
      documentTitle = name;
    }
  }
}
