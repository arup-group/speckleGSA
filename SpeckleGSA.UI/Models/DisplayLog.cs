using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public class DisplayLog
  {
    public List<DisplayLogItem> DisplayLogItems { get; set; } = new List<DisplayLogItem>();
    public List<DisplayLogItem> SelectedDisplayLogItems { get; set; }
  }
}
