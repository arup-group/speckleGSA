using NUnit.Framework;
using SpeckleCore;
using SpeckleGSA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpeckleGSAUI.Test
{
  [TestFixture]
  public class CommandLineTests
  {
    private int NumCalled = 0;

    [SetUp]
    public void SetUpMocks()
    {
      Headless.streamReceiverCreationFn = (url, token) => new TestSpeckleGSAReceiver(url, "mm");
      Headless.streamSenderCreatorFn = (url, token) => new TestSpeckleGSASender();
      Headless.loggingProgress = new Progress<MessageEventArgs>();
      Headless.ProgressMessenger = new MockProgressMessenger(Headless.loggingProgress);
      GSA.App = new MockGSAApp();
      NumCalled = 0;
    }

    [Test]
    public void Receive()
    {
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
        "--file", "headless-test.gwa",
        "--streamIDs", string.Join(",", expectedStreamIds),
        "--layer", "design",
        "--nodeAllowance", "0.2");

      //Check there 3 receivers were used with the restApi and token
      Assert.AreEqual(3, receiversCreated.Count());
      Assert.IsTrue(receiversCreated.Select(r => r.StreamId).OrderBy(i => i).SequenceEqual(expectedStreamIds.OrderBy(i => i)));
      Assert.IsTrue(receiversCreated.All(r => r.ServerAddress.Equals(restApi)));
      Assert.IsTrue(receiversCreated.All(r => r.Token.Equals(token)));
      Assert.AreEqual(3, NumCalled); //Number of times a streamRecevier.Trigger has been called

      //Check the stream Ids
      //Check the layer
      //Check the file was saved with the expected file
      Assert.IsTrue(HelperFunctions.GetSidSpeckleRecords(email, restApi, GSA.App.LocalProxy, out var receiverStreamInfo, out var senderStreamInfo));

      //Receive 2 streams into new (blank) file, save GSA file, open and check stream names are saved
      //THEN, open file and receive from new streams; open file again and check if both streams are saved
    }

    private void Receiver_UpdateGlobalTrigger(object sender, EventArgs e)
    {
      NumCalled++;
    }

    [Test]
    public void Send()
    {
      //Open
    }

    [Test]
    public void ReceiveThenSend()
    {
      
    }
  }
}
