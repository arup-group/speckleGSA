using SpeckleGSA;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy;
using SpeckleGSAUI.Utilities;
//using SpeckleInterface;
using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SpeckleGSAUI
{
  public class Headless
  {
    public static Func<string, string, SpeckleInterface.IStreamReceiver> streamReceiverCreationFn = ((url, token) => new SpeckleInterface.StreamReceiver(url, token, ProgressMessenger));
    //public static Func<string, string, SpeckleInterface.IStreamSender> streamSenderCreationFn = ((url, token) => new SpeckleInterface.StreamSender(url, token, ProgressMessenger));
    public static Func<string, string, SpeckleInterface.IStreamSender> streamSenderCreationFn;
    public static IProgress<MessageEventArgs> loggingProgress = new Progress<MessageEventArgs>();
    public static SpeckleInterface.ISpeckleAppMessenger ProgressMessenger = new ProgressMessenger(loggingProgress);

    private Dictionary<string, string> arguments = new Dictionary<string, string>();
    private string cliMode = "";

    public string EmailAddress { get; private set; }
    public string RestApi { get; private set; }
    public string ApiToken { get; private set; }

    public bool RunCLI(params string[] args)
    {
      CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

      cliMode = args[0];
      if (cliMode == "-h")
      {
        Console.WriteLine("\n");
        Console.WriteLine("Usage: SpeckleGSAUI.exe <command>\n\n" +
          "where <command> is one of: receiver, sender\n\n");
        Console.Write("SpeckleGSAUI.exe <command> -h\thelp on <command>\n");
        return true;
      }
      if (cliMode != "receiver" && cliMode != "sender")
      {
        Console.WriteLine("Unable to parse command");
        return false;
      }

      for (int index = 1; index < args.Length; index += 2)
      {
        string arg = args[index].Replace("-", "");
        if (args.Length <= index + 1 || args[index + 1].StartsWith("-"))
        {
          arguments.Add(arg, "true");
          index--;
        }
        else
        {
          arguments.Add(arg, args[index + 1].Trim(new char[] { '"' }));
        }
      }

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
        return true;
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
        return true;
      }

      string[] neededArgs = new string[] { "server", "email", "token", "file" };

      foreach (string a in neededArgs)
      {
        if (!arguments.ContainsKey(a))
        {
          Console.WriteLine("Missing -" + a + " argument");
          return false;
        }
      }

      // Login
      EmailAddress = arguments["email"];
      RestApi = arguments["server"];
      ApiToken = arguments["token"];

      //This will create the logger
      GSA.App.LocalSettings.LoggingMinimumLevel = 4;  //Debug
      GSA.App.Settings.TargetLayer = (arguments.ContainsKey("layer") && (arguments["layer"].ToLower() == "analysis")) ? GSATargetLayer.Analysis : GSATargetLayer.Design;

      //This ensures that if multiple CLI calls are made sequentially from any process, that there is no carry over of static variable values
      //from previous calls
      GSA.Reset();

      GSA.Init(getRunningVersion().ToString());
      GSA.App.LocalMessenger.MessageAdded += this.ProcessMessage;
      SpeckleCore.SpeckleInitializer.Initialize();
      
      List<SidSpeckleRecord> receiverStreamInfo;
      List<SidSpeckleRecord> senderStreamInfo;

      var fileArg = arguments["file"];
      var filePath = fileArg.StartsWith(".") ? Path.Combine(AssemblyDirectory, fileArg) : fileArg;

      // GSA File
      if (File.Exists(filePath))
      {
        OpenFile(filePath, EmailAddress, RestApi, out receiverStreamInfo, out senderStreamInfo, false);
      }
      else if (cliMode == "sender")
      {
        Console.WriteLine("Could not locate file: " + filePath);
        //sending needs the file to exist
        return false;
      }
      else
      {
        receiverStreamInfo = new List<SidSpeckleRecord>();
        senderStreamInfo = new List<SidSpeckleRecord>();
        GSA.App.Proxy.NewFile(false);
        
        GSA.App.Messenger.Message(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Information, "Created new file.");

        //Ensure this new file has a file name
        GSA.App.Proxy.SaveAs(filePath);
      }

      var calibrateNodeAtTask = Task.Run(() => GSAProxy.CalibrateNodeAt());
      calibrateNodeAtTask.Wait();

      if (cliMode == "receiver")
      {
        if (!arguments.ContainsKey("streamIDs"))
        {
          Console.WriteLine("Missing -streamIDs argument");
          return false;
        }
        //There seem to be some issues with HTTP requests down the line if this is run on the initial (UI) thread, so this ensures it runs on another thread
        return Task.Run(() => CLIReceiver(receiverStreamInfo)).Result;
      }
      else if (cliMode == "sender")
      {
        //There seem to be some issues with HTTP requests down the line if this is run on the initial (UI) thread, so this ensures it runs on another thread
        return Task.Run(() => CLISender(senderStreamInfo)).Result;
      }
      return true;
    }

    public bool CLIReceiver(List<SidSpeckleRecord> savedReceiverStreamInfo)
    {
      //Ignore the saved receiver stream info for now - review?
      var receiverStreamInfo = new List<SidSpeckleRecord>();

      var streamIds = arguments["streamIDs"].Split(new char[] { ',' });
      foreach (string id in streamIds)
      {
        receiverStreamInfo.Add(new SidSpeckleRecord(id, null));
      }
      HelperFunctions.SetSidSpeckleRecords(EmailAddress, RestApi, GSA.App.Proxy, receiverStreamInfo, null);

      GSA.App.Settings.TargetLayer = ((arguments.ContainsKey("layer")) && (arguments["layer"].ToLower() == "analysis")) ? GSATargetLayer.Analysis : GSATargetLayer.Design;
      if (arguments.ContainsKey("nodeAllowance") && double.TryParse(arguments["nodeAllowance"], out double nodeAllowance))
      {
        GSA.App.Settings.CoincidentNodeAllowance = nodeAllowance;
      }
      //TO DO: enable is as a command line argument
      GSA.App.Settings.Units = "m";

      var gsaReceiverCoordinator = new ReceiverCoordinator();

      var nonBlankReceivers = receiverStreamInfo.Where(r => !string.IsNullOrEmpty(r.StreamId)).ToList();
      foreach (var streamInfo in nonBlankReceivers)
      {
        GSA.App.Messenger.Message(MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Information, "Creating receiver " + streamInfo.StreamId);
        gsaReceiverCoordinator.StreamReceivers[streamInfo.StreamId] = new SpeckleInterface.StreamReceiver(RestApi, ApiToken, ProgressMessenger);
      }

      var messenger = new ProgressMessenger(new Progress<MessageEventArgs>());
      Func<string, string, SpeckleInterface.IStreamReceiver> streamReceiverCreationFn = ((url, token) => new SpeckleInterface.StreamReceiver(url, token, messenger));
      if (!gsaReceiverCoordinator.Initialize(RestApi, ApiToken, receiverStreamInfo, Headless.streamReceiverCreationFn, 
        new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>()))
      {
        Console.WriteLine("Unable to set up connection with the server for the specified streams");
        return false;
      }

      HelperFunctions.SetSidSpeckleRecords(EmailAddress, RestApi, GSA.App.Proxy, receiverStreamInfo, null);

      gsaReceiverCoordinator.Trigger(null, null);
      gsaReceiverCoordinator.Dispose();

      var filePath = arguments["file"];
      if (filePath.StartsWith("."))
      {
        string sCurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        filePath = Path.GetFullPath(Path.Combine(sCurrentDirectory, filePath));
      }

      GSA.App.Proxy.SaveAs(filePath);
      GSA.App.Proxy.Close();

      Console.WriteLine("Receiving complete");

      return true;
    }

    public bool CLISender(List<SidSpeckleRecord> savedSenderStreamInfo)
    {
      if (arguments.ContainsKey("sendAllNodes"))
      {
        GSA.App.LocalSettings.SendOnlyMeaningfulNodes = false;
      }
      if (arguments.ContainsKey("separateStreams"))
      {
        GSA.App.LocalSettings.SeparateStreams = true;
        GSA.App.Settings.EmbedResults = false;
      }
      if (arguments.ContainsKey("resultOnly"))
      {
        GSA.App.LocalSettings.SendOnlyResults = true;
      }
      if (arguments.ContainsKey("resultUnembedded"))
      {
        GSA.App.Settings.EmbedResults = false;
      }
      if (arguments.ContainsKey("resultInLocalAxis"))
      {
        GSA.App.Settings.ResultInLocalAxis = true;
      }
      if (arguments.ContainsKey("result1DNumPosition"))
      {
        try
        {
          GSA.App.Settings.Result1DNumPosition = Convert.ToInt32(arguments["result1DNumPosition"]);
        }
        catch { }
      }

      if (arguments.ContainsKey("result"))
      {
        GSA.App.Settings.SendResults = true;

        var results = arguments["result"].Split(new char[] { ',' }).Select(x => x.Replace("\"", ""));

        foreach (string r in results)
        {
          if (Result.NodalResultMap.ContainsKey(r))
          {
            GSA.GsaApp.Settings.NodalResults[r] = Result.NodalResultMap[r];
          }
          else if (Result.Element1DResultMap.ContainsKey(r))
          {
            GSA.GsaApp.Settings.Element1DResults[r] = Result.Element1DResultMap[r];
          }
          else if (Result.Element2DResultMap.ContainsKey(r))
          {
            GSA.GsaApp.Settings.Element2DResults[r] = Result.Element2DResultMap[r];
          }
          else if (Result.MiscResultMap.ContainsKey(r))
          {
            GSA.GsaApp.Settings.MiscResults[r] = Result.MiscResultMap[r];
          }
        }
      }

      if (arguments.ContainsKey("resultCases"))
      {
        GSA.App.Settings.ResultCases = arguments["resultCases"].Split(new char[] { ',' }).ToList();
      }

      //GSA.GetSpeckleClients(EmailAddress, RestApi);
      var gsaSenderCoordinator = new SenderCoordinator();
      gsaSenderCoordinator.Initialize(RestApi, ApiToken, (savedSenderStreamInfo == null || savedSenderStreamInfo.Count() == 0) ? null : savedSenderStreamInfo,
        streamSenderCreationFn, loggingProgress, new Progress<string>(), new Progress<double>(), new Progress<SidSpeckleRecord>(), new Progress<SidSpeckleRecord>());
      gsaSenderCoordinator.Trigger().Wait();
      //The Trigger method creates the senders, so need to wait until that is finished before saving the sender info to the file
      HelperFunctions.SetSidSpeckleRecords(EmailAddress, RestApi, GSA.App.Proxy, null,
        gsaSenderCoordinator.Senders.Keys.Select(k => new SidSpeckleRecord(gsaSenderCoordinator.Senders[k].StreamId, k, gsaSenderCoordinator.Senders[k].ClientId)).ToList());

      gsaSenderCoordinator.Dispose();

      var filePath = arguments["file"];
      if (filePath.StartsWith("."))
      {
        string sCurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        filePath = Path.GetFullPath(Path.Combine(sCurrentDirectory, filePath));
      }

      GSA.App.Proxy.SaveAs(filePath);
      GSA.App.Proxy.Close();

      Console.WriteLine("Sending complete");
      return true;
    }

    #region Log
    [DllImport("Kernel32.dll")]
    public static extern bool AttachConsole(int processId);

    /// <summary>
    /// Message handler.
    /// </summary>
    private void ProcessMessage(object sender, MessageEventArgs e)
    {
      if (e.Level == SpeckleGSAInterfaces.MessageLevel.Debug || e.Level == SpeckleGSAInterfaces.MessageLevel.Information)
      {
        Console.WriteLine("[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + string.Join(" ", e.MessagePortions.Where(mp => !string.IsNullOrEmpty(mp))));
      }
      else
      {
        Console.WriteLine("[" + DateTime.Now.ToString("h:mm:ss tt") + "] ERROR: " + string.Join(" ", e.MessagePortions.Where(mp => !string.IsNullOrEmpty(mp))));
      }
    }

    /// <summary>
    /// Change status handler.
    /// </summary>
    private void ChangeStatus(object sender, StatusEventArgs e)
    {
      if (e.Percent >= 0 & e.Percent <= 100)
      {
        Console.WriteLine("[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + e.Name + " : " + e.Percent);
      }
      else
      {
        Console.WriteLine("[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + e.Name + "...");
      }
    }
    #endregion

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

    private void OpenFile(string path, string emailAddress, string serverAddress,
      out List<SidSpeckleRecord> receiverStreamInfo, out List<SidSpeckleRecord> senderStreamInfo, bool showWindow = true)
    {
      receiverStreamInfo = new List<SidSpeckleRecord>();
      senderStreamInfo = new List<SidSpeckleRecord>();

      GSA.App.Proxy.OpenFile(path, showWindow);
      if (emailAddress != null && serverAddress != null)
      {
        HelperFunctions.GetSidSpeckleRecords(emailAddress, serverAddress, GSA.App.Proxy, out receiverStreamInfo, out senderStreamInfo);
      }

      GSA.App.Messenger.Message(SpeckleGSAInterfaces.MessageIntent.Display, MessageLevel.Information, "Opened new file.");
    }

    private static string AssemblyDirectory
    {
      get
      {
        string codeBase = Assembly.GetExecutingAssembly().CodeBase;
        UriBuilder uri = new UriBuilder(codeBase);
        string path = Uri.UnescapeDataString(uri.Path);
        return Path.GetDirectoryName(path);
      }
    }
  }
}
