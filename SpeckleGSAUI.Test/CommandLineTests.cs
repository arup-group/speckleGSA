using NUnit.Framework;
using SpeckleCore;
using SpeckleGSA;
using SpeckleGSAProxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpeckleGSAUI.Test
{
  [TestFixture]
  public class CommandLineTests
  {
    private int NumCalled = 0;
    private string sendGsaFileRelativePath = @"..\..\TestData\shear wall system-seismic v10.1.gwb";
    private string email = "test.two@arup.com";
    private string restApi = "https://australia.speckle.arup.com/api";
    private string testRecStreamId = "Rec-02";
    private Dictionary<string, string> testSenBucketStreamIds = new Dictionary<string, string>() { { "model", "Sen-01" }, { "results", "Sen-02" } };

    [SetUp]
    public void SetUpMocks()
    {
      Headless.streamReceiverCreationFn = (url, token) => new TestSpeckleGSAReceiver(url, "mm");
      Headless.streamSenderCreationFn = (url, token) => new TestSpeckleGSASender();
      Headless.loggingProgress = new Progress<MessageEventArgs>();
      Headless.ProgressMessenger = new MockProgressMessenger(Headless.loggingProgress);
      NumCalled = 0;
    }

    [Test]
    public void Receive()
    {
      GSA.App = new MockGSAApp();
      var testObjects = new Dictionary<string, List<SpeckleObject>>();
      var receiversCreated = new List<TestSpeckleGSAReceiver>();
      var tokensUsed = new List<string>();
      Headless.streamReceiverCreationFn = (url, t) =>
      {
        var receiver = new TestSpeckleGSAReceiver(url, "mm");
        receiver.UpdateGlobalTrigger += Receiver_UpdateGlobalTrigger;
        receiver.Token = t;
        receiversCreated.Add(receiver);
        return receiver;
      };

      //Copy file so that it can be deleted later
      var filePath = HelperFunctions.GetFullPath(@".\ReceiveDesignLayerTest.gwb");

      var email = "test@arup.com";
      var token = "TestToken";
      var restApi = "http://australia.speckle.arup.com/api/"; //Note: the real thing needs api on the end, so added here as reminder
      var expectedStreamIds = new List<string>() { "stream-id-a", "stream-id-b", "stream-id-c" };

      //var proxy = new TestPRo
      var headless = new Headless();
      headless.RunCLI("receiver",
        "--server", restApi,
        "--email", email,
        "--token", token,
        "--file", filePath,
        "--streamIDs", string.Join(",", expectedStreamIds),
        "--layer", "design",
        "--nodeAllowance", "0.2");

      //Check there 3 receivers were used with the restApi and token
      Assert.AreEqual(3, receiversCreated.Count());
      Assert.IsTrue(receiversCreated.Select(r => r.StreamId).OrderBy(i => i).SequenceEqual(expectedStreamIds.OrderBy(i => i)));
      Assert.IsTrue(receiversCreated.All(r => r.ServerAddress.Equals(restApi)));
      Assert.IsTrue(receiversCreated.All(r => r.Token.Equals(token)));
      Assert.AreEqual(3, NumCalled); //Number of times a streamRecevier.Trigger has been called
    }

    private void Receiver_UpdateGlobalTrigger(object sender, EventArgs e)
    {
      NumCalled++;
    }

    [Test]
    public void SendAnalysisLayer()
    {
      var token = "TestToken";

      GSA.App = new MockGSAApp(proxy: new GSAProxy());

      var testObjects = new Dictionary<string, List<SpeckleObject>>();
      var sendersCreated = new List<TestSpeckleGSASender>();
      var tokensUsed = new List<string>();
      Headless.streamSenderCreationFn = (url, t) =>
      {
        var sender = new TestSpeckleGSASender() { ServerAddress = url, Token = t };
        sendersCreated.Add(sender);
        return sender;
      };

      var expectedTestCases = new[] { "A1", "C2" };
      var results = new[] { "Nodal Displacements", "1D Element Displacement" };

      //Copy file so that it can be deleted later
      var origFilePath = HelperFunctions.GetFullPath(sendGsaFileRelativePath);
      var copiedFilePath = HelperFunctions.GetFullPath(@".\SendAnalysisLayerTest.gwb");
      File.Copy(origFilePath, copiedFilePath, true);

      var exceptionThrown = false;
      try
      {
        var headless = new Headless();
        headless.RunCLI("sender",
          "--server", restApi,
          "--email", "test.two@arup.com",
          "--token", token,
          "--file", copiedFilePath,
          "--layer", "analysis",
          "--separateStreams",
          "--result", string.Join(",", results.Select(r => "\"" + r + "\"")),
          "--resultCases", string.Join(",", expectedTestCases));
      }
      catch
      {
        exceptionThrown = true;
      }
      finally
      {
        File.Delete(copiedFilePath);
      }
      Assert.IsFalse(exceptionThrown);

      //Check there 3 receivers were used with the restApi and token
      Assert.AreEqual(2, sendersCreated.Count());
      Assert.IsTrue(sendersCreated.All(r => !string.IsNullOrEmpty(r.StreamId)));
      Assert.IsTrue(sendersCreated.All(r => r.ServerAddress.Equals(restApi)));
      Assert.IsTrue(sendersCreated.Select(r => r.StreamId).OrderBy(i => i).SequenceEqual(testSenBucketStreamIds.Values.OrderBy(i => i)));
      Assert.IsTrue(sendersCreated.All(r => r.Token.Equals(token)));
      Assert.IsTrue(sendersCreated.All(s => s.SentObjects.Keys.Count() > 0 && s.SentObjects.Values.Count() > 0)); //Number of times a streamSender.SendObjects has been called

      Assert.IsTrue(GSA.App.LocalSettings.ResultCases.SequenceEqual(expectedTestCases));
      Assert.IsTrue(GSA.App.LocalSettings.SeparateStreams);
      Assert.IsTrue(GSA.App.LocalSettings.NodalResults.ContainsKey(results[0]));
      Assert.IsTrue(GSA.App.LocalSettings.Element1DResults.ContainsKey(results[1]));
    }

    [Test]
    public void SendDesignLayer()
    {
      var token = "TestToken";

      GSA.App = new MockGSAApp(proxy: new GSAProxy());

      var testObjects = new Dictionary<string, List<SpeckleObject>>();
      var sendersCreated = new List<TestSpeckleGSASender>();
      var tokensUsed = new List<string>();
      Headless.streamSenderCreationFn = (url, t) =>
      {
        var sender = new TestSpeckleGSASender() { ServerAddress = url, Token = t };
        sendersCreated.Add(sender);
        return sender;
      };

      //Copy file so that it can be deleted later
      var origFilePath = HelperFunctions.GetFullPath(sendGsaFileRelativePath);
      var copiedFilePath = HelperFunctions.GetFullPath(@".\SendAnalysisLayerTest.gwb");
      File.Copy(origFilePath, copiedFilePath, true);

      var exceptionThrown = false;
      try
      {
        var headless = new Headless();
        headless.RunCLI("sender",
          "--server", restApi,
          "--email", "test.two@arup.com",
          "--token", token,
          "--file", copiedFilePath,
          "--layer", "design"
          );
      }
      catch
      {
        exceptionThrown = true;
      }
      finally
      {
        File.Delete(copiedFilePath);
      }
      Assert.IsFalse(exceptionThrown);

      //Check there 3 receivers were used with the restApi and token
      Assert.AreEqual(1, sendersCreated.Count());
      Assert.IsFalse(string.IsNullOrEmpty(sendersCreated[0].StreamId));
      Assert.AreEqual(restApi, sendersCreated[0].ServerAddress);
      Assert.AreEqual(testSenBucketStreamIds["model"], sendersCreated[0].StreamId);
      Assert.AreEqual(token, sendersCreated[0].Token);
      Assert.IsTrue(sendersCreated[0].SentObjects.Keys.Count() > 0);
      Assert.IsTrue(sendersCreated[0].SentObjects.Values.Count() > 0); //Number of times a streamSender.SendObjects has been called
    }

    [Test]
    public void StreamInfoSidStorage()
    {
      var server = "https://australia.speckle.arup.com/api";
      var filePath = HelperFunctions.GetFullPath(sendGsaFileRelativePath);

      var gsaProxy = new GSAProxy();
      gsaProxy.OpenFile(filePath, true);

      var wroteOne = HelperFunctions.SetSidSpeckleRecords("test.one@arup.com", server, gsaProxy,
        new List<SidSpeckleRecord>()
        {
          new SidSpeckleRecord("Rec-01", null, "Client-01")
        },
        null);
      var wroteTwo = HelperFunctions.SetSidSpeckleRecords(email, server, gsaProxy,
        new List<SidSpeckleRecord>()
        {
          new SidSpeckleRecord(testRecStreamId, null, "Client-02")
        },
        new List<SidSpeckleRecord>()
        {
          new SidSpeckleRecord(testSenBucketStreamIds["model"], "model", "Client-02"), new SidSpeckleRecord(testSenBucketStreamIds["results"], "results", "Client-02")
        });

      //The first record is just for context, to add basic complexity
      var readOne = HelperFunctions.GetSidSpeckleRecords("test.one@arup.com", server, gsaProxy, out var recOne, out var senOne) && recOne.Count() == 1 && senOne.Count() == 0;
      var readTwo = HelperFunctions.GetSidSpeckleRecords(email, server, gsaProxy, out var recTwo, out var senTwo) && recTwo.Count() == 1 && senTwo.Count() == 2;
      if (wroteOne && wroteTwo && readOne && readTwo)
      {
        gsaProxy.SaveAs(filePath);
      }
      gsaProxy.Close();

      Assert.IsTrue(wroteOne);
      Assert.IsTrue(wroteTwo);
      Assert.IsTrue(readOne);
      Assert.IsTrue(readTwo);
    }
  }
}

