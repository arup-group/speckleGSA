using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Models
{
  public enum AppState
  {
    NotLoggedIn,
    LoggedIn,  //The most common state when not continuously sending or receiving: between send/receive events, this is the regular state of being
    ReceivingWaiting,  //time between active reception in continuous mode
    ActiveReceiving,
    SendingWaiting,
    ActiveSending
  }

  public enum StreamContentConfig
  {
    ModelOnly,
    ModelWithEmbeddedResults,
    ModelWithTabularResults,
    TabularResultsOnly
  }

  public enum GsaUnit
  {
    Millimetres,
    Metres,
    Inches
  }

  public enum LoggingMinimumLevel
  {
    Debug,
    Information,
    Error,
    Fatal
  }
}
