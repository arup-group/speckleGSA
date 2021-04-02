using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public enum StreamMethod
  {
    None,
    Continuous,
    Single
  }

  public enum GsaLoadedFileType
  {
    None,
    NewFile,
    ExistingFile
  }

  public enum AppState
  {
    NotLoggedIn = 0,
    ActiveLoggingIn,
    ActiveRetrievingStreamList,
    LoggedInNotLinkedToGsa,  
    ReceivingWaiting,     //time between active reception in continuous mode
    ActiveReceiving,
    SendingWaiting,
    ActiveSending,
    OpeningFile,
    SavingFile,
    Ready,   // when not continuously sending or receiving: between send/receive events, this is the regular state of being
    ActiveRenamingStream
  }

  public enum StreamContentConfig
  {
    None,
    ModelOnly,
    ModelWithEmbeddedResults,
    ModelWithTabularResults,
    TabularResultsOnly
  }

  public enum GsaUnit
  {
    None,
    Millimetres,
    Metres,
    Inches
  }

  public enum LoggingMinimumLevel
  {
    None,
    Debug,
    Information,
    Error,
    Fatal
  }
}
