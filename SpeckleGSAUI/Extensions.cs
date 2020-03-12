using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SpeckleGSAUI
{
  public static class Extensions
  {

    public static void DoEvents(this Application a)
    {
      a.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
    }
  }
}
