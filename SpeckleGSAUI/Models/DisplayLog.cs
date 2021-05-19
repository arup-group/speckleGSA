using System.Collections.Generic;

namespace SpeckleGSAUI.Models
{
  public class DisplayLog
  {
    public List<DisplayLogItem> DisplayLogItems { get; set; } = new List<DisplayLogItem>();
    public List<DisplayLogItem> SelectedDisplayLogItems { get; set; }
  }
}
