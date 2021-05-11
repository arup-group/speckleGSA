using NUnit.Framework;
using SpeckleCore;
using SpeckleGSA;
using System;
using System.Collections.Generic;
using System.Linq;
using SpeckleGSAInterfaces;
using System.IO;
using System.Diagnostics;
using SpeckleInterface;
using Newtonsoft.Json;

namespace SpeckleGSAProxy.Test
{
  public class TestRes
  {
    public bool success;
    public string message;
  }

  [TestFixture]
  public class ProxyTests
  {
    private List<MessageEventArgs> testMessageCache;

    public static string[] savedJsonFileNames = new[] { "U7ntEJkzdZ.json", "lfsaIEYkR.json", "NaJD7d5kq.json", "UNg87ieJG.json" };

    //This is the number of times some tests in this file are repeated to try to catch any intermittent failures due to parallelisation of
    //some parts of the code
    private int numRepeat = 10;

    private string TestDataDirectory { get => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\"; }

    //The EC_mxfJ2p.json file has these objects (ordered for ease of reading here, this is not the order of the objects in the file):
    /*
      type: "Structural0DLoad",	2
      type: "Structural1DLoad",	2
      type: "Structural2DLoad",	2
      type: "StructuralLoadCase",	21
      type: "StructuralLoadCombo",	4
      type: "StructuralLoadTask",	2
      type: "Plane/StructuralStorey",	1
      type: "StructuralMaterialSteel",	1
      type: "StructuralMaterialConcrete",	1
      type: "Structural1DProperty",	2
      type: "Structural2DProperty",	3
      type: "Point/StructuralNode",	64
      type: "Line/Structural1DElement",	67
      type: "Mesh/Structural2DElement",	27
      type: "Polyline/Structural1DElementPolyline",	23
    */

    [Test]
    public void UnitCalibrationsTest()
    {
      GSAProxy.CalibrateNodeAt();
    }

    private double CalibrateFactorFor(string units, double coincidentNodeTolerance = 1)
    {
      double coordValue = 1000;

      var proxy = new GSAProxy();
      proxy.NewFile(false);
      proxy.SetUnits(units);
      proxy.NodeAt(coordValue, coordValue, coordValue, coincidentNodeTolerance);
      double factor = 1;
      var gwa = proxy.GetGwaForNode(1);
      var pieces = gwa.Split(GSAProxy.GwaDelimiter);
      if (double.TryParse(pieces.Last(), out double z1))
      {
        if (z1 != coordValue)
        {
          var factorCandidate = coordValue / z1;

          proxy.NodeAt(coordValue * factorCandidate, coordValue * factorCandidate, coordValue * factorCandidate, coincidentNodeTolerance * factorCandidate);

          gwa = proxy.GetGwaForNode(2);
          pieces = gwa.Split(GSAProxy.GwaDelimiter);

          if (double.TryParse(pieces.Last(), out double z2) && z2 == 1000)
          {
            //it's confirmed
            factor = factorCandidate;
          }
        }
      }

      proxy.Close();

      return factor;
    }

    private Dictionary<string, double> CalibrateFactorsFor(Dictionary<string, double> unitAndTolerances)
    {
      double coordValue = 1000;
      var retDict = new Dictionary<string, double>();
      var proxy = new GSAProxy();
      proxy.NewFile(false);
      foreach (var u in unitAndTolerances.Keys)
      {
        proxy.SetUnits(u);
        var nodeIndex = proxy.NodeAt(coordValue, coordValue, coordValue, unitAndTolerances[u]);
        double factor = 1;
        var gwa = proxy.GetGwaForNode(nodeIndex);
        var pieces = gwa.Split(GSAProxy.GwaDelimiter);
        if (double.TryParse(pieces.Last(), out double z1))
        {
          if (z1 != coordValue)
          {
            var factorCandidate = coordValue / z1;

            nodeIndex = proxy.NodeAt(coordValue * factorCandidate, coordValue * factorCandidate, coordValue * factorCandidate, 1 * factorCandidate);

            gwa = proxy.GetGwaForNode(nodeIndex);
            pieces = gwa.Split(GSAProxy.GwaDelimiter);

            if (double.TryParse(pieces.Last(), out double z2) && z2 == 1000)
            {
              //it's confirmed
              factor = factorCandidate;
            }
          }
        }
        retDict.Add(u, factor);
      }

      proxy.Close();

      return retDict;
    }

    [TestCase(GSATargetLayer.Design, "EC_mxfJ2p.json", "m")]
    [TestCase(GSATargetLayer.Analysis, "EC_mxfJ2p.json", "m")]
    public void ReceiveTestPreserveOrderContinuousMerge(GSATargetLayer layer, string fileName, string streamUnits = "mm")
    {
      GSA.Reset();
      GSA.GsaApp.gsaProxy = new TestProxy();
      GSA.GsaApp.gsaSettings.TargetLayer = layer;
      GSA.GsaApp.gsaSettings.Units = "m";
      GSA.Init("");

      var streamIds = new[] { fileName }.Select(fn => fn.Split(new[] { '.' }).First()).ToList();
      GSA.ReceiverInfo = streamIds.Select(si => new SidSpeckleRecord(si, null)).ToList();

      //Create receiver with all streams
      var receiverCoordinator = new ReceiverCoordinator() { StreamReceivers = streamIds.ToDictionary(s => s, s => (IStreamReceiver)new TestSpeckleGSAReceiver(s, streamUnits)) };
      SetObjectsAsReceived(receiverCoordinator, fileName, TestDataDirectory);

      GSA.GsaApp.gsaProxy.NewFile(false);

      //This will load data from all streams into the cache
      receiverCoordinator.Initialize(new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>());

      //RECEIVE EVENT #1: first of continuous
      receiverCoordinator.Trigger(null, null);

      //Check cache to see if object have been received
      var records = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records;
      var latestGwaAfter1 = new List<string>(records.Where(r => r.Latest).Select(r => r.Gwa));

      Assert.AreEqual(0, records.Where(r => !r.Latest).Count());

      //For now, have hard-coded keywords that might have a one-to-many ApplicationID relationship (e.g. "blah" -> "blah_X" + "blah_Y" etc)
      var oneToManyAppIdKws = new List<string> { "LOAD_NODE", "LOAD_BEAM" };

      //Exclude these because:
      //1. for Structural1DPolylines, their application ID doesn't make its way to any GSA record, since they create records for each 
      //   "child" application ID they have in their refs
      var excludeSpeckleTypes = new List<string> { "Structural1DElementPolyline", "StructuralNode" };
      //2. for GSA0DElements, they could have been created from StructuralNode records when certain conditions are met, but handling this 
      //   is not important for this test, so the test data is intended to not include it
      var excludeGsaTypes = new List<string> { "GSA0DElement" };

      var speckleKeywordMap = new Dictionary<Type, string>();
      foreach (var gsaType in receiverCoordinator.dummyObjectDict.Keys.Where(k => k != null))
      {
        if (excludeGsaTypes.Any(gt => gsaType.Name.Equals(gt))) continue;

        var kw = GetGSAKeyword(gsaType).Split('.').First();

        if ((layer == GSATargetLayer.Design && !((bool)GetAttribute<GSAObject>(gsaType, "DesignLayer")))
          || (layer == GSATargetLayer.Analysis && !((bool)GetAttribute<GSAObject>(gsaType, "AnalysisLayer")))
          || (string.IsNullOrEmpty(kw))
          //Avoid any one-to-many application ID issues for now - review later
          || (oneToManyAppIdKws.Any(om => om.Equals(kw)))
          ) continue;

        var speckleType = receiverCoordinator.dummyObjectDict[gsaType].SpeckleObject.GetType();

        if (!speckleKeywordMap.ContainsKey(speckleType))
        {
          speckleKeywordMap.Add(speckleType, kw);
        }
      }

      foreach (var streamId in streamIds)
      {
        var serverStreamObjects = receiverCoordinator.StreamReceivers[streamId].GetObjects();
        var serverObjectsByType = serverStreamObjects.GroupBy(o => o.GetType()).ToDictionary(o => o.Key, o => o.ToList());

        foreach (var t in serverObjectsByType.Keys)
        {
          //Exclude these, as they are effectively groups of (what will become) GSA records, under another keyword

          if (speckleKeywordMap.ContainsKey(t) && !excludeSpeckleTypes.Any(est => t.Name.Equals(est)))
          {
            var keyword = speckleKeywordMap[t];
            var serverAppIds = serverObjectsByType[t].Select(o => o.ApplicationId).ToList();
            var cachedAllAppIdsForKeyword = GetKeywordApplicationIds(streamId, keyword);

            var cachedAppIds = cachedAllAppIdsForKeyword.Where(ci => serverAppIds.Any(si => si == ci)).ToList();

            Assert.Greater(cachedAppIds.Count(), 0);

            if (!serverAppIds.SequenceEqual(cachedAppIds))
            {
              Console.WriteLine("Sequence not equal for " + t.Name);
            }
            Assert.IsTrue(serverAppIds.SequenceEqual(cachedAppIds));
          }
        }
      }

      GSA.GsaApp.gsaProxy.Close();
    }


    [Test]
    public void ReceiveTestContinuousMerge()
    {
      for (var n = 0; n < numRepeat; n++)
      {
        GSA.Reset();
        GSA.GsaApp.gsaSettings.Units = "m";
        GSA.GsaApp.gsaSettings.TargetLayer = GSATargetLayer.Design;
        GSA.GsaApp.gsaProxy = new TestProxy();
        GSA.Init("");

        var streamIds = savedJsonFileNames.Select(fn => fn.Split(new[] { '.' }).First()).ToList();
        GSA.ReceiverInfo = streamIds.Select(si => new SidSpeckleRecord(si, null)).ToList();

        //Create receiver with all streams
        var receiverCoordinator = new ReceiverCoordinator() { StreamReceivers = streamIds.ToDictionary(s => s, s => (IStreamReceiver)new TestSpeckleGSAReceiver(s, "mm")) };
        SetObjectsAsReceived(receiverCoordinator, savedJsonFileNames, TestDataDirectory);

        GSA.GsaApp.gsaProxy.NewFile(false);

        //This will load data from all streams into the cache
        receiverCoordinator.Initialize(new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>());

        //RECEIVE EVENT #1: first of continuous
        receiverCoordinator.Trigger(null, null);

        //Check cache to see if object have been received
        var records = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records;
        var latestGwaAfter1 = new List<string>(records.Where(r => r.Latest).Select(r => r.Gwa));
        Assert.AreEqual(99, records.Where(r => r.Latest).Count());
        Assert.AreEqual(0, records.Where(r => string.IsNullOrEmpty(r.StreamId)).Count());
        Assert.IsTrue(records.All(r => r.Gwa.Contains(r.StreamId)));

        //Refresh with new copy of objects so they aren't the same (so the merging code isn't trying to merge each object onto itself)
        SetObjectsAsReceived(receiverCoordinator, savedJsonFileNames, TestDataDirectory);

        //RECEIVE EVENT #2: second of continuous
        receiverCoordinator.Trigger(null, null);

        //Check cache to see if object have been merged correctly and no extraneous calls to GSA is created
        var latestGwaAfter2 = new List<string>(records.Where(r => r.Latest).Select(r => r.Gwa));
        var diff = latestGwaAfter2.Where(a2 => !latestGwaAfter1.Any(a1 => string.Equals(a1, a2, StringComparison.InvariantCultureIgnoreCase))).ToList();
        records = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records;
        Assert.AreEqual(99, records.Where(r => r.Latest).Count());
        Assert.AreEqual(109, records.Count());

        GSA.GsaApp.gsaProxy.Close();
      }
    }
    
    [Ignore("To help with potential future debugging")]
    [Test]
    public void ForgetSIDTest()
    {
      GSA.Reset();
      GSA.GsaApp.gsaProxy = new GSAProxy();
      GSA.GsaApp.gsaSettings.TargetLayer = GSATargetLayer.Design;
      GSA.GsaApp.gsaSettings.Units = "m";
      GSA.Init("");

      GSA.GsaApp.gsaProxy.NewFile(false);

      GSA.GsaApp.gsaProxy.SetGwa("LOAD_2D_THERMAL.2:{testTag:testValue}\tGeneral\tG48 G49 G50 G51 G52\t3\tDZ\t239\t509");
      GSA.GsaApp.gsaProxy.Sync();

      var gwaData = GSA.GsaApp.gsaProxy.GetGwaData(new[] { "LOAD_2D_THERMAL" }, true);

      GSA.GsaApp.gsaProxy.Close();
    }

    [Test]
    public void ReceiveTestStreamSubset()
    {
      for (var n = 0; n < numRepeat; n++)
      {
        GSA.Reset();
        GSA.GsaApp.gsaSettings.Units = "m";
        GSA.GsaApp.gsaSettings.TargetLayer = GSATargetLayer.Design;
        GSA.GsaApp.gsaProxy = new TestProxy();
        GSA.Init("");

        Debug.WriteLine("");
        Debug.WriteLine("Test run number: " + (n + 1));
        Debug.WriteLine("");


        var streamIds = savedJsonFileNames.Select(fn => fn.Split(new[] { '.' }).First()).ToList();
        GSA.ReceiverInfo = streamIds.Select(si => new SidSpeckleRecord(si, null)).ToList();

        //Create receiver with all streams
        var receiverCoordinator = new ReceiverCoordinator() { StreamReceivers = streamIds.ToDictionary(s => s, s => (IStreamReceiver)new TestSpeckleGSAReceiver(s, "mm")) };
        SetObjectsAsReceived(receiverCoordinator, savedJsonFileNames, TestDataDirectory);

        GSA.GsaApp.gsaProxy.NewFile(false);

        //This will load data from all streams into the cache
        receiverCoordinator.Initialize(new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>());

        //RECEIVE EVENT #1: single
        receiverCoordinator.Trigger(null, null);

        //RECEIVE EVENT #2: single with reduced streams

        //Add contents of cache to the test proxy so they can be the source for the renewed hydration of the cache in the Initialize call
        CopyCacheToTestProxy();

        var streamIdsToTest = streamIds.Take(3).ToList();
        GSA.ReceiverInfo = streamIdsToTest.Select(si => new SidSpeckleRecord(si, null)).ToList();

        //Yes the real SpeckleGSA does create a new receiver.  This time, create them with not all streams active
        receiverCoordinator = new ReceiverCoordinator() { StreamReceivers = streamIdsToTest.ToDictionary(s => s, s => (IStreamReceiver)new TestSpeckleGSAReceiver(s, "mm")) };

        var records = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records;
        Assert.AreEqual(3, GSA.ReceiverInfo.Count());
        Assert.AreEqual(3, receiverCoordinator.StreamReceivers.Count());
        Assert.AreEqual(4, records.Select(r => r.StreamId).Distinct().Count());

        receiverCoordinator.Initialize(new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>());

        //Refresh with new copy of objects so they aren't the same (so the merging code isn't trying to merge each object onto itself)
        var streamObjectsTuples = ExtractObjects(savedJsonFileNames.Where(fn => streamIdsToTest.Any(ft => fn.Contains(ft))).ToArray(), TestDataDirectory);
        for (int i = 0; i < streamIdsToTest.Count(); i++)
        {
          ((TestSpeckleGSAReceiver)receiverCoordinator.StreamReceivers[streamIds[i]]).Objects = streamObjectsTuples.Where(t => t.Item1 == streamIds[i]).Select(t => t.Item2).ToList();
        }

        var kwGroupsBefore = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records.Where(r => r.Latest).GroupBy(r => r.Keyword).ToDictionary(g => g, g => g.ToList());

        receiverCoordinator.Trigger(null, null);

        var kwGroupsAfter = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records.Where(r => r.Latest).GroupBy(r => r.Keyword).ToDictionary(g => g, g => g.ToList());

        //Check the other streams aren't affected by only having some active
        records = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records;
        Assert.AreEqual(101, records.Where(r => r.Latest).Count());
        //-------

        GSA.GsaApp.gsaProxy.Close();
      }
    }

    [TestCase("sjc.gwb")]
    public void SendTest(string filename)
    {
      GSA.Reset();
      GSA.GsaApp.gsaProxy = new GSAProxy();
      GSA.GsaApp.gsaSettings.TargetLayer = GSATargetLayer.Design;
      GSA.Init("");

      Status.StatusChanged += (s, e) => Debug.WriteLine("Status: " + e.Name);

      GSA.SenderInfo = new Dictionary<string, SidSpeckleRecord>() { { "testStream", new SidSpeckleRecord("testStreamId", "testStream", "testClientId") } };

      var sender = new SenderCoordinator();

      GSA.GsaApp.gsaProxy.OpenFile(Path.Combine(TestDataDirectory, filename), false);

      var testSender = new TestSpeckleGSASender();

      //This will load data from all streams into the cache
      sender.Initialize("", "", (restApi, apiToken) => testSender, new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>());

      //RECEIVE EVENT #1: first of continuous
      sender.Trigger();

      GSA.GsaApp.gsaProxy.Close();

      var a = new SpeckleObject();
      var sentObjects = testSender.sentObjects.SelectMany(kvp => kvp.Value.Select(v => (SpeckleObject)v)).ToList();

      Assert.AreEqual(1481, sentObjects.Where(so => so.Type.EndsWith("Structural1DElement")).Count());
      Assert.AreEqual(50, sentObjects.Where(so => so.Type.Contains("Structural2DElement")).Count());
      Assert.AreEqual(172, sentObjects.Where(so => so.Type.EndsWith("Structural2DVoid")).Count());
    }


    [TestCase("SET\tMEMB.8:{speckle_app_id:gh/a}\t5\tTheRest", "MEMB", 5, "gh/a", "MEMB.8:{speckle_app_id:gh/a}\t5\tTheRest")]
    [TestCase("MEMB.8:{speckle_app_id:gh/a}\t5\tTheRest", "MEMB", 5, "gh/a", "MEMB.8:{speckle_app_id:gh/a}\t5\tTheRest")]
    [TestCase("SET_AT\t2\tLOAD_2D_THERMAL.2:{speckle_app_id:gh/a}\tTheRest", "LOAD_2D_THERMAL", 2, "gh/a", "LOAD_2D_THERMAL.2:{speckle_app_id:gh/a}\tTheRest")]
    [TestCase("LOAD_2D_THERMAL.2:{speckle_app_id:gh/a}\tTheRest", "LOAD_2D_THERMAL", 0, "gh/a", "LOAD_2D_THERMAL.2:{speckle_app_id:gh/a}\tTheRest")]
    public void ParseGwaCommandTests(string gwa, string expKeyword, int expIndex, string expAppId, string expGwaWithoutSet)
    {
      var gsaProxy = new GSAProxy();
      GSAProxy.ParseGeneralGwa(gwa, out string keyword, out int? foundIndex, out string streamId, out string applicationId, out string gwaWithoutSet, out SpeckleGSAInterfaces.GwaSetCommandType? gwaSetCommandType);
      var index = foundIndex ?? 0;

      Assert.AreEqual(expKeyword, keyword);
      Assert.AreEqual(expIndex, index);
      Assert.AreEqual(expAppId, applicationId);
      Assert.AreEqual(expGwaWithoutSet, gwaWithoutSet);
    }

    [Test]
    public void TestProxyGetDataForCache()
    {
      var proxy = new GSAProxy();
      proxy.OpenFile(Path.Combine(TestDataDirectory, "Structural Demo 191010.gwb"), false);

      var data = proxy.GetGwaData(DesignLayerKeywords, false);

      Assert.AreEqual(188, data.Count());
      proxy.Close();
    }

    [Test]
    public void MessageCacheTest()
    {
      testMessageCache = new List<MessageEventArgs>();

      var messageBus = new GsaMessenger();
      messageBus.MessageAdded += TestMessageHandler;

      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Debug, "display-debug-header", "display-debug-desc1");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Debug, "display-debug-header", "display-debug-desc2");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Debug, "display-debug-single-desc3");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error, "display-error-header", "display-error-desc1");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.Display, SpeckleGSAInterfaces.MessageLevel.Error, "display-error-single-desc2");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.TechnicalLog, SpeckleGSAInterfaces.MessageLevel.Debug, "technicallog-header", "technicallog-debug-desc1");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.TechnicalLog, SpeckleGSAInterfaces.MessageLevel.Debug, "technicallog-header", "technicallog-debug-desc2");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.TechnicalLog, SpeckleGSAInterfaces.MessageLevel.Debug, "technicallog-header", "technicallog-debug-desc3");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.TechnicalLog, SpeckleGSAInterfaces.MessageLevel.Information, "technicallog-header", "technicallog-info-desc1");
      messageBus.CacheMessage(SpeckleGSAInterfaces.MessageIntent.TechnicalLog, SpeckleGSAInterfaces.MessageLevel.Information, "technicallog-header", "technicallog-info-desc2");

      messageBus.ConsolidateCache();
      messageBus.Trigger();

      Assert.AreEqual(9, testMessageCache.Count());
    }

    
    #region private_methods

    private void TestMessageHandler(object sender, MessageEventArgs mea)
    {
      testMessageCache.Add(mea);
    }

    private object GetAttribute<T>(object t, string attribute)
    {
      try
      {
        var attObj = (t is Type) ? Attribute.GetCustomAttribute((Type)t, typeof(T)) : Attribute.GetCustomAttribute(t.GetType(), typeof(T));
        return typeof(T).GetProperty(attribute).GetValue(attObj);
      }
      catch { return null; }
    }

    private string GetGSAKeyword(object t)
    {
      return (string)GetAttribute<GSAObject>(t, "GSAKeyword");
    }

    private List<string> GetKeywordApplicationIds(string streamId, string keyword)
    {
      var records = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records
              .Where(r => r.StreamId == streamId && r.Keyword.Equals(keyword) && !string.IsNullOrEmpty(r.ApplicationId));
      records = records.OrderBy(r => r.Index);
      return records.Select(r => r.ApplicationId).ToList();
    }

    private void SetObjectsAsReceived(ReceiverCoordinator receiver, string savedJsonFileName, string testDataDirectory)
    {
      SetObjectsAsReceived(receiver, new string[] { savedJsonFileName }, testDataDirectory);
    }

    private void SetObjectsAsReceived(ReceiverCoordinator receiver, string[] savedJsonFileNames, string testDataDirectory)
    {
      var streamIds = savedJsonFileNames.Select(fn => fn.Split(new[] { '.' }).First()).ToList();
      var streamObjectsTuples = ExtractObjects(savedJsonFileNames, testDataDirectory);
      for (int i = 0; i < streamIds.Count(); i++)
      {
        ((TestSpeckleGSAReceiver)receiver.StreamReceivers[streamIds[i]]).Objects = streamObjectsTuples.Where(t => t.Item1 == streamIds[i]).Select(t => t.Item2).ToList();
      }
    }

    private void CopyCacheToTestProxy()
    {
      var latestRecords = ((IGSACacheForTesting)GSA.GsaApp.gsaCache).Records.Where(r => r.Latest).ToList();
      latestRecords.ForEach(r => ((TestProxy)GSA.GsaApp.gsaProxy).AddDataLine(r.Keyword, r.Index, r.StreamId, r.ApplicationId, r.Gwa, r.GwaSetCommandType));
    }

    private List<Tuple<string, SpeckleObject>> ExtractObjects(string[] fileNames, string directory)
    {
      var speckleObjects = new List<Tuple<string, SpeckleObject>>();
      foreach (var fileName in fileNames)
      {
        var json = Helper.ReadFile(fileName, directory);
        var streamId = fileName.Split(new[] { '.' }).First();

        var response = ResponseObject.FromJson(json);
        for (int i = 0; i < response.Resources.Count(); i++)
        {
          speckleObjects.Add(new Tuple<string, SpeckleObject>(streamId, response.Resources[i]));
        }
      }
      return speckleObjects;
    }
    #endregion


    public static string[] DesignLayerKeywords = new string[] {
      "LOAD_2D_THERMAL",
      "ALIGN",
      "PATH",
      "USER_VEHICLE",
      "RIGID",
      "ASSEMBLY",
      "LOAD_GRAVITY",
      "PROP_SPR",
      "ANAL",
      "TASK",
      "GEN_REST",
      "ANAL_STAGE",
      "LIST",
      "LOAD_GRID_LINE",
      "POLYLINE",
      "GRID_SURFACE",
      "GRID_PLANE",
      "AXIS",
      "MEMB",
      "NODE",
      "LOAD_GRID_AREA",
      "LOAD_2D_FACE",
      "PROP_2D",
      "MAT_STEEL",
      "MAT_CONCRETE",
      "LOAD_BEAM",
      "LOAD_NODE",
      "COMBINATION",
      "LOAD_TITLE",
      "PROP_SEC",
      "PROP_MASS",
      "GRID_LINE"
    };
  }
}
