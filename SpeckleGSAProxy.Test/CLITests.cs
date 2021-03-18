using NUnit.Framework;
using SpeckleGSAInterfaces;
using SpeckleGSAUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  [TestFixture]
  public class CLITests
  {
    private const string url = "https://australia.speckle.arup.com/api";
    private const string email = "nic.burgers@arup.com";
    private const string token = "JWT eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJfaWQiOiI1ZGU4NjYxYTlhMThhZDI0MDFiZDU2MWEiLCJpYXQiOjE1NzU1MTE1NzgsImV4cCI6MTYzODYyNjc3OH0.Ku0r_b4BZ3mp4poXjjblrGIMFB06sRMc8KF7yC7ZDnQ";

    [Ignore("Not ready with CLI yet")]
    [TestCase(@"C:\Users\Nic.Burgers\OneDrive - Arup\Issues\Daan Duppen\200602_timberPlint 3d model.gwb", GSATargetLayer.Analysis)]
    public void Test1(string filePath, GSATargetLayer layer)
    {
      var args = new List<string> { "sender", "--server", url, "--email", email, "--token", token, "--file", filePath, "--layer", layer.ToString().ToLower(),
        "--result", "1D Element Force", "--resultCases", "C3" };

      var app = new App();
      app.RunCLI(args.ToArray());
      /*
      var app = new App();
      app.InitializeComponent();
      app.Run();
      */
      //Process.Start(@"C:\Nicolaas\Repo\speckleGSA-github\SpeckleGSAUI\bin\Debug\SpeckleGSAUI.exe", string.Join(" ", args));
    }
  }
}
