using Moq;
using SpeckleGSA;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy;
using SpeckleUtil;
using System;

namespace SpeckleGSAUI.Test
{
  public class MockGSAApp : IGSALocalAppResources
  {
    public IGSALocalProxy LocalProxy { get; set; }
    public IGSACache LocalCache { get; set; }

    public IGSALocalSettings LocalSettings { get; set; }

    public IGSALocalMessenger LocalMessenger { get; set; }

    public ISpeckleObjectMerger Merger { get; set; } = new SpeckleObjectMerger();

    public IGSASettings Settings { get => LocalSettings; }

    public IGSAProxy Proxy { get => LocalProxy; }

    public IGSACacheForKit Cache { get; set; }

    public IGSAMessenger Messenger { get => LocalMessenger; }


    //Default test implementations
    public MockGSAApp(IGSALocalSettings settings = null, IGSALocalProxy proxy = null, IGSACache cache = null, IGSALocalMessenger messenger = null)
    {
      LocalCache = cache ?? new GSACache();
      LocalSettings = settings ?? new MockSettings();
      if (proxy == null)
      {
        var mockProxy = new Mock<IGSALocalProxy>();

        mockProxy.Setup(x => x.NodeAt(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
          .Returns(new Func<double, double, double, double, int>(MockGSAProxyMethods.NodeAt));
        mockProxy.Setup(x => x.FormatApplicationIdSidTag(It.IsAny<string>()))
          .Returns(new Func<string, string>(MockGSAProxyMethods.FormatApplicationIdSidTag));
        mockProxy.Setup(x => x.FormatSidTags(It.IsAny<string>(), It.IsAny<string>()))
          .Returns(new Func<string, string, string>(MockGSAProxyMethods.FormatSidTags));
        mockProxy.Setup(x => x.ConvertGSAList(It.IsAny<string>(), It.IsAny<GSAEntity>()))
          .Returns(new Func<string, GSAEntity, int[]>(MockGSAProxyMethods.ConvertGSAList));
        mockProxy.SetupGet(x => x.GwaDelimiter).Returns(GSAProxy.GwaDelimiter);
        mockProxy.Setup(x => x.GetUnits()).Returns("m");
        mockProxy.Setup(x => x.GetTopLevelSid()).Returns("");
        mockProxy.Setup(x => x.SetTopLevelSid(It.IsAny<string>())).Returns(true);

        LocalProxy = mockProxy.Object;
      }
      else
      {
        LocalProxy = proxy;
      }
      LocalMessenger = messenger ?? new MockGSAMessenger();
    }
  }
}
