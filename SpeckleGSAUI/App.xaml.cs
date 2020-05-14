using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using SpeckleGSA;
using System.IO;
using System.Globalization;
using SpeckleGSAProxy;
using SpeckleGSAInterfaces;

namespace SpeckleGSAUI
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    private Dictionary<string, string> arguments = new Dictionary<string, string>();
    private string cliMode = "";

    public string EmailAddress;
    public string RestApi;
    public string ApiToken;

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
        AttachConsole(-1);
        cliMode = e.Args[0];
        if (cliMode == "-h")
        {
          Console.WriteLine("\n");
          Console.WriteLine("Usage: SpeckleGSAUI.exe <command>\n\n" +
            "where <command> is one of: receiver, sender\n\n");
          Console.Write("SpeckleGSAUI.exe <command> -h\thelp on <command>\n");
					Current.Shutdown();
          return;
        }
        if (cliMode != "receiver" && cliMode != "sender")
        {
          Console.WriteLine("Unable to parse command");
					Current.Shutdown();
          return;
        }

        for (int index = 1; index < e.Args.Length; index += 2)
        {
          string arg = e.Args[index].Replace("-", "");
          if (e.Args.Length <= index + 1 || e.Args[index + 1].StartsWith("-"))
          {
            arguments.Add(arg, "true");
            index--;
          }
          else
            arguments.Add(arg, e.Args[index + 1].Trim(new char[] { '"' }));
        }

        GSA.Init();
        Status.Init(this.AddMessage, this.AddError, this.ChangeStatus);
        SpeckleCore.SpeckleInitializer.Initialize();

        RunCLI();

				Current.Shutdown();

        return;
      }
    }

    private void RunCLI()
    {
      if (cliMode == "receiver" && arguments.ContainsKey("h"))
      {
        Console.WriteLine("\n");
        Console.WriteLine("Usage: SpeckleGSAUI.exe receiver\n");
        Console.WriteLine("\n");
        Console.Write("Required arguments:\n");
        Console.Write("--server <server>\t\tAddress of Speckle server\n");
        Console.Write("--email <email>\t\t\tEmail of account\n");
        Console.Write("--token <token>\t\tJWT token\n");
        Console.Write("--file <path>\t\t\tFile to save to. If file does not exist, a new one will be created\n");
        Console.Write("--streamIDs <streamIDs>\t\tComma-delimited ID of streams to be received\n");
        Console.WriteLine("\n");
        Console.Write("Optional arguments:\n");
        Console.Write("--layer [analysis|design]\tSet which layer to write to. Default is design layer\n");
        Console.Write("--nodeAllowance <distance>\tMax distance before nodes are not merged\n");
        return;
      }
      else if (cliMode == "sender" && arguments.ContainsKey("h"))
      {
        Console.WriteLine("\n");
        Console.WriteLine("Usage: SpeckleGSAUI.exe sender\n");
        Console.WriteLine("\n");
        Console.Write("Required arguments:\n");
        Console.Write("--server <server>\t\tAddress of Speckle server\n");
        Console.Write("--email <email>\t\t\tEmail of account\n");
        Console.Write("--token <token>\t\tJWT token\n");
        Console.Write("--file <path>\t\t\tFile to open. If file does not exist, a new one will be created\n");
        Console.WriteLine("\n");
        Console.Write("Optional arguments:\n");
        Console.Write("--layer [analysis|design]\tSet which layer to write to. Default is design layer\n");
        Console.Write("--sendAllNodes\t\t\tSend all nodes in model. Default is to send only 'meaningful' nodes\n");
        Console.Write("--separateStreams\t\tSeparate model into different streams\n");
        Console.Write("--result <options>\t\tType of result to send. Each input should be in quotation marks. Comma-delimited\n");
        Console.Write("--resultCases <cases>\t\tCases to extract results from. Comma-delimited\n");
        Console.Write("--resultOnly\t\t\tSend only results\n");
        Console.Write("--resultUnembedded\t\tSend results as separate objects\n");
        Console.Write("--resultInLocalAxis\t\tSend results calculated at the local axis. Default is global\n");
        Console.Write("--result1DNumPosition <num>\tNumber of additional result points within 1D elements\n");
        return;
      }

      string[] neededArgs = new string[] { "server", "email", "token", "file" };

      foreach (string a in neededArgs)
      {
        if (!arguments.ContainsKey(a))
        {
          Console.WriteLine("Missing -" + a + " argument");
          return;
        }
      }

      // Login
      EmailAddress = arguments["email"];
      RestApi = arguments["server"];
      ApiToken = arguments["token"];

      // GSA File
      if (File.Exists(arguments["file"]))
        GSA.OpenFile(arguments["file"], EmailAddress, RestApi, false);
      else
      {
        GSA.NewFile(EmailAddress, RestApi, false);
				GSA.gsaProxy.SaveAs(arguments["file"]);
      }

      // We will receive all the things!
      if (cliMode == "receiver")
        CLIReceiver();
      else if (cliMode == "sender")
      {
        CLISender();
      }
    }

    public void CLIReceiver()
    {
      if (!arguments.ContainsKey("streamIDs"))
      {
        Console.WriteLine("Missing -streamIDs argument");
        return;
      }

      var streamIds = arguments["streamIDs"].Split(new char[] { ',' });
      foreach (string id in streamIds)
        GSA.ReceiverInfo.Add(new Tuple<string, string>(id, null));
      GSA.SetSpeckleClients(EmailAddress, RestApi);

      if (arguments.ContainsKey("layer"))
        if (arguments["layer"].ToLower() == "analysis")
        {
					GSA.Settings.TargetLayer = GSATargetLayer.Analysis;
        }
      
      if (arguments.ContainsKey("nodeAllowance"))
      {
        try
        {
					GSA.Settings.CoincidentNodeAllowance = Convert.ToDouble(arguments["nodeAllowance"]);
        }
        catch { }
      }

      GSA.GetSpeckleClients(EmailAddress, RestApi);
      var gsaReceiver = new Receiver();
      Task.Run(() =>
      {
        var nonBlankReceivers = GSA.ReceiverInfo.Where(r => !string.IsNullOrEmpty(r.Item1)).ToList();

        foreach (var streamInfo in nonBlankReceivers)
        {
          Status.AddMessage("Creating receiver " + streamInfo.Item1);
          gsaReceiver.Receivers[streamInfo.Item1] = new SpeckleGSAReceiver(RestApi, ApiToken);
        }
      });
      Task.Run(() => gsaReceiver.Initialize(RestApi, ApiToken)).Wait();
      GSA.SetSpeckleClients(EmailAddress, RestApi);
      gsaReceiver.Trigger(null, null);
      gsaReceiver.Dispose();

			GSA.gsaProxy.SaveAs(arguments["file"]);
			GSA.Close();

      Console.WriteLine("Receiving complete");
    }

    public void CLISender()
    {
      if (arguments.ContainsKey("layer"))
        if (arguments["layer"].ToLower() == "analysis")
        {
					GSA.Settings.TargetLayer = GSATargetLayer.Analysis;
				}

      if (arguments.ContainsKey("sendAllNodes"))
				GSA.Settings.SendOnlyMeaningfulNodes = false;

      if (arguments.ContainsKey("separateStreams"))
				GSA.Settings.SeparateStreams = true;

      if (arguments.ContainsKey("resultOnly"))
				GSA.Settings.SendOnlyResults = true;

      if (arguments.ContainsKey("resultUnembedded"))
				GSA.Settings.EmbedResults = false;

      if (arguments.ContainsKey("resultInLocalAxis"))
				GSA.Settings.ResultInLocalAxis = true;

      if (arguments.ContainsKey("result1DNumPosition"))
      {
        try
        {
					GSA.Settings.Result1DNumPosition = Convert.ToInt32(arguments["result1DNumPosition"]);
        }
        catch { }
      }

      if (arguments.ContainsKey("result"))
      {
				GSA.Settings.SendResults = true;

        var results = arguments["result"].Split(new char[] { ',' }).Select(x => x.Replace("\"", ""));

        foreach (string r in results)
        {
          if (Result.NodalResultMap.ContainsKey(r))
						GSA.Settings.NodalResults[r] = Result.NodalResultMap[r];
          else if (Result.Element1DResultMap.ContainsKey(r))
						GSA.Settings.Element1DResults[r] = Result.Element1DResultMap[r];
          else if (Result.Element2DResultMap.ContainsKey(r))
						GSA.Settings.Element2DResults[r] = Result.Element2DResultMap[r];
          else if (Result.MiscResultMap.ContainsKey(r))
						GSA.Settings.MiscResults[r] = Result.MiscResultMap[r];
        }
      }

      if (arguments.ContainsKey("resultCases"))
				GSA.Settings.ResultCases = arguments["resultCases"].Split(new char[] { ',' }).ToList();
      
      GSA.GetSpeckleClients(EmailAddress, RestApi);
      var gsaSender = new Sender();
      Task.Run(() => gsaSender.Initialize(RestApi, ApiToken, (restApi, apiToken) => new SpeckleGSASender(restApi, apiToken))).Wait();
      GSA.SetSpeckleClients(EmailAddress, RestApi);
      gsaSender.Trigger();
      gsaSender.Dispose();

			GSA.gsaProxy.SaveAs(arguments["file"]);
			GSA.Close();

      Console.WriteLine("Sending complete");
    }

    #region Log
    [DllImport("Kernel32.dll")]
    public static extern bool AttachConsole(int processId);
    
    /// <summary>
    /// Message handler.
    /// </summary>
    private void AddMessage(object sender, MessageEventArgs e)
    {
      Console.WriteLine("[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + e.Message);
    }

    /// <summary>
    /// Error message handler.
    /// </summary>
    private void AddError(object sender, MessageEventArgs e)
    {
      Console.WriteLine("[" + DateTime.Now.ToString("h:mm:ss tt") + "] ERROR: " + e.Message);
    }

    /// <summary>
    /// Change status handler.
    /// </summary>
    private void ChangeStatus(object sender, StatusEventArgs e)
    {
      if (e.Percent >= 0 & e.Percent <= 100)
        Console.WriteLine("[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + e.Name + " : " + e.Percent);
      else
        Console.WriteLine("[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + e.Name + "...");
    }
    #endregion
  }
}
