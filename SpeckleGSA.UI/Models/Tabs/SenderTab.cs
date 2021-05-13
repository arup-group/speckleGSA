using SpeckleGSAInterfaces;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSA.UI.Models
{
  public class SenderTab : TabBase
  {
    public StreamContentConfig StreamContentConfig { get; set; } = StreamContentConfig.ModelOnly;

    public string LoadCaseList { get; set; }
    public int AdditionalPositionsFor1dElements { get; set; }

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

    /*
    //[ bucket, stream name ]
    internal Dictionary<string, string> GetDefaultStreamNames()
    {
      var retList = new Dictionary<string, string>();
      if (StreamContentConfig == StreamContentConfig.ModelOnly)
      {
        retList.Add("model", string.IsNullOrEmpty(documentTitle) ? "GSA model" : documentTitle + " (GSA model)");
      }
      else if (StreamContentConfig == StreamContentConfig.ModelWithEmbeddedResults)
      {
        retList.Add("model", string.IsNullOrEmpty(documentTitle) ? "GSA model with embedded results" : documentTitle + " (GSA model with embedded results)");
      }
      else if (StreamContentConfig == StreamContentConfig.ModelWithTabularResults)
      {
        retList.Add("model", string.IsNullOrEmpty(documentTitle) ? "GSA model" : documentTitle + " (GSA model)");
        retList.Add("results", string.IsNullOrEmpty(documentTitle) ? "GSA results" : documentTitle + " (GSA results)");
      }
      else if (StreamContentConfig == StreamContentConfig.TabularResultsOnly)
      {
        retList.Add("model", string.IsNullOrEmpty(documentTitle) ? "GSA results" : documentTitle + " (GSA results)");
      }
      return retList;
    }
    */
  }
}
