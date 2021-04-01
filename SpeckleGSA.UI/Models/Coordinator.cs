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
    public GsaFileStatus FileStatus { get; set; }
    public string FilePath { get; set; }
    public StreamList ServerStreamList { get; set; }
    public DisplayLog DisplayLog { get; set; }

    public LoggingMinimumLevel LoggingMinimumLevel { get; set; } = LoggingMinimumLevel.Information;
    public bool VerboseErrorInformation { get; set; } = false;

    public AppState AppState { get; set; } = AppState.NotLoggedIn;

    public ReceiverCoordinator ReceiverCoordinator { get; set; } = new ReceiverCoordinator();
    public SenderCoordinator SenderCoordinator { get; set; } = new SenderCoordinator();
  }
}
