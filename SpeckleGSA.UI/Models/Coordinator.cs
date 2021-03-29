using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public class Coordinator
  {
    public SpeckleAccount Account { get; set; }
    public string FilePath { get; set; }
    public StreamList ServerStreamList { get; set; }
    public DisplayLog DisplayLog { get; set; }

    public LoggingMinimumLevel LoggingMinimumLevel { get; set; }
    public bool VerboseErrorInformation { get; set; } = false;

    public AppState AppState { get; set; } = AppState.NotLoggedIn;
  }
}
