using SpeckleGSAUI.Utilities;
using SpeckleGSAInterfaces;
using SpeckleInterface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Deployment.Application;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using SpeckleGSA;

namespace SpeckleGSAUI
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    private void Application_Startup(object sender, StartupEventArgs e)
    {
      CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

      if (e.Args.Length == 0)
      {
        MainWindow wnd = new MainWindow();
        wnd.Show();
      }
      else
      {
        var headless = new Headless();
        Headless.AttachConsole(-1);

        headless.RunCLI(e.Args);
        Current.Shutdown();
      }
    }
  }
}
