using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public class StreamList
  {
    public List<StreamListItem> StreamListItems { get; set; } = new List<StreamListItem>();
    public StreamListItem SeletedStreamListItem { get; set; }
  }
}
