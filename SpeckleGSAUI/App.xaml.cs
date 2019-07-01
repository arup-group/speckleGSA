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
          System.Windows.Application.Current.Shutdown();
          return;
        }
        if (cliMode != "receiver" && cliMode != "sender")
        {
          Console.WriteLine("Unable to parse command");
          System.Windows.Application.Current.Shutdown();
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

        System.Windows.Application.Current.Shutdown();

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
        Console.Write("-server <server>\t\tAddress of Speckle server\n");
        Console.Write("-email <email>\t\t\tEmail of account\n");
        Console.Write("-password <password>\t\tAccount password\n");
        Console.Write("-file <path>\t\t\tFile to save to. If file does not exist, a new one will be created\n");
        Console.Write("-streamIDs <streamIDs>\t\tComma-delimited ID of streams to be received\n");
        Console.WriteLine("\n");
        Console.Write("Optional arguments:\n");
        Console.Write("-layer [analysis|design]\tSet which layer to write to. Default is design layer\n");
        Console.Write("-nodeAllowance <distance>\tMax distance before nodes are not merged\n");
        return;
      }
      else if (cliMode == "sender" && arguments.ContainsKey("h"))
      {
        Console.WriteLine("\n");
        Console.WriteLine("Usage: SpeckleGSAUI.exe sender\n");
        Console.WriteLine("\n");
        Console.Write("Required arguments:\n");
        Console.Write("-server <server>\t\tAddress of Speckle server\n");
        Console.Write("-email <email>\t\t\tEmail of account\n");
        Console.Write("-password <password>\t\tAccount password\n");
        Console.Write("-file <path>\t\t\tFile to open. If file does not exist, a new one will be created\n");
        Console.WriteLine("\n");
        Console.Write("Optional arguments:\n");
        Console.Write("-layer [analysis|design]\tSet which layer to write to. Default is design layer\n");
        Console.Write("-sendAllNodes\t\t\tSend all nodes in model. Default is to send only 'meaningful' nodes\n");
        Console.Write("-separateStreams\t\tSeparate model into different streams\n");
        Console.Write("-result <options>\t\tType of result to send. Each input should be in quotation marks. Comma-delimited\n");
        Console.Write("-resultCases <cases>\t\tCases to extract results from. Comma-delimited\n");
        Console.Write("-resultOnly\t\t\tSend only results\n");
        Console.Write("-resultUnembedded\t\tSend results as separate objects\n");
        Console.Write("-resultInLocalAxis\t\tSend results calculated at the local axis. Default is global\n");
        Console.Write("-result1DNumPosition <num>\tNumber of additional result points within 1D elements\n");
        return;
      }

      string[] neededArgs = new string[] { "server", "email", "password", "file" };

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

      var myUser = new SpeckleCore.User()
      {
        Email = EmailAddress,
        Password = arguments["password"],
      };

      var spkClient = new SpeckleCore.SpeckleApiClient() { BaseUrl = RestApi };

      var response = spkClient.UserLoginAsync(myUser).Result;
      if (response.Success == true)
        ApiToken = response.Resource.Apitoken;
      else
      {
        Console.WriteLine("Failed to login");
        return;
      }

      // GSA File
      if (File.Exists(arguments["file"]))
        GSA.OpenFile(arguments["file"], EmailAddress, RestApi);
      else
      {
        GSA.NewFile(EmailAddress, RestApi);
        GSA.GSAObject.SaveAs(arguments["file"]);
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
        GSA.Receivers.Add(id);
      GSA.SetSpeckleClients(EmailAddress, RestApi);

      if (arguments.ContainsKey("layer"))
        if (arguments["layer"].ToLower() == "analysis")
        {
          Settings.TargetAnalysisLayer = true;
          Settings.TargetDesignLayer = false;
        }
      
      if (arguments.ContainsKey("nodeAllowance"))
      {
        try
        {
          Settings.CoincidentNodeAllowance = Convert.ToDouble(arguments["nodeAllowance"]);
        }
        catch { }
      }

      GSA.GetSpeckleClients(EmailAddress, RestApi);
      var gsaReceiver = new Receiver();
      Task.Run(() => gsaReceiver.Initialize(RestApi, ApiToken)).Wait();
      GSA.SetSpeckleClients(EmailAddress, RestApi);
      gsaReceiver.Trigger(null, null);
      gsaReceiver.Dispose();

      GSA.GSAObject.SaveAs(arguments["file"]);
      GSA.Close();

      Console.WriteLine("Receiving complete");
    }

    public void CLISender()
    {
      if (arguments.ContainsKey("layer"))
        if (arguments["layer"].ToLower() == "analysis")
        {
          Settings.TargetAnalysisLayer = true;
          Settings.TargetDesignLayer = false;
        }

      if (arguments.ContainsKey("sendAllNodes"))
        Settings.SendOnlyMeaningfulNodes = false;

      if (arguments.ContainsKey("separateStreams"))
        Settings.SeparateStreams = true;

      if (arguments.ContainsKey("resultOnly"))
        Settings.SendOnlyResults = true;

      if (arguments.ContainsKey("resultUnembedded"))
        Settings.EmbedResults = false;

      if (arguments.ContainsKey("resultInLocalAxis"))
        Settings.ResultInLocalAxis = true;

      if (arguments.ContainsKey("result1DNumPosition"))
      {
        try
        {
          Settings.Result1DNumPosition = Convert.ToInt32(arguments["result1DNumPosition"]);
        }
        catch { }
      }

      if (arguments.ContainsKey("result"))
      {
        Settings.SendResults = true;

        var results = arguments["result"].Split(new char[] { ',' }).Select(x => x.Replace("\"", ""));

        foreach (string r in results)
        {
          if (Result.NodalResultMap.ContainsKey(r))
            Settings.ChosenNodalResult[r] = Result.NodalResultMap[r];
          else if (Result.Element1DResultMap.ContainsKey(r))
            Settings.ChosenElement1DResult[r] = Result.Element1DResultMap[r];
          else if (Result.Element2DResultMap.ContainsKey(r))
            Settings.ChosenElement2DResult[r] = Result.Element2DResultMap[r];
          else if (Result.MiscResultMap.ContainsKey(r))
            Settings.ChosenMiscResult[r] = Result.MiscResultMap[r];
        }
      }

      if (arguments.ContainsKey("resultCases"))
        Settings.ResultCases = arguments["resultCases"].Split(new char[] { ',' }).ToList();
      
      GSA.GetSpeckleClients(EmailAddress, RestApi);
      var gsaSender = new Sender();
      Task.Run(() => gsaSender.Initialize(RestApi, ApiToken)).Wait();
      GSA.SetSpeckleClients(EmailAddress, RestApi);
      gsaSender.Trigger();
      gsaSender.Dispose();

      GSA.GSAObject.SaveAs(arguments["file"]);
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
