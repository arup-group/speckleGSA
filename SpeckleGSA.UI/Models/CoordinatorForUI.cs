using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpeckleCore;

namespace SpeckleGSA.UI.Models
{
  public class CoordinatorForUI
  {
    public SpeckleAccountForUI Account { get; set; }
    public GsaLoadedFileType FileStatus { get; set; }
    public string FilePath { get; set; }
    public StreamList ServerStreamList { get; set; } = new StreamList();
    public DisplayLog DisplayLog { get; set; } = new DisplayLog();

    public LoggingMinimumLevel LoggingMinimumLevel { get; set; } = LoggingMinimumLevel.Information;
    public bool VerboseErrorInformation { get; set; } = false;

    public ReceiverCoordinatorForUI ReceiverCoordinatorForUI { get; set; } = new ReceiverCoordinatorForUI();
    public SenderCoordinatorForUI SenderCoordinatorForUI { get; set; } = new SenderCoordinatorForUI();
    public Version RunningVersion { get => getRunningVersion(); }

    #region app_resources

    //The SpeckleStreamManager is also used, but that is a static class so no need to store it as a member here
    public SenderCoordinator gsaSenderCoordinator;
    public ReceiverCoordinator gsaReceiverCoordinator;

    public Timer triggerTimer;

    #endregion

    public void Init()
    {
      GSA.Init(getRunningVersion().ToString());
      SpeckleInitializer.Initialize();
      LocalContext.Init();
      gsaSenderCoordinator = new SenderCoordinator();
      gsaReceiverCoordinator = new ReceiverCoordinator();
    }

    private Version getRunningVersion()
    {
      try
      {
        return ApplicationDeployment.CurrentDeployment.CurrentVersion;
      }
      catch (Exception)
      {
        return Assembly.GetExecutingAssembly().GetName().Version;
      }
    }
  }
}
