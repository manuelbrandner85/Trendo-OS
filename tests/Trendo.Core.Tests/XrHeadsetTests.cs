using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>S3: Brillendaten je Zielgerät (Reihenfolge wie XrInspector.Budgets).</summary>
public class XrHeadsetTests
{
    [Fact(DisplayName = "S3 Brillen-Ansicht: Sichtfeld und Augenabstand je Brille, Handy-AR ohne Brille")]
    public void Headsets()
    {
        Assert.Equal(4, XrInspector.Budgets.Count);
        var q3 = XrPro.HeadsetOf(0)!; Assert.Equal((110, 96, 63), (q3.HFovDeg, q3.VFovDeg, q3.IpdMm)); Assert.StartsWith("Meta Quest 3", XrInspector.Budgets[0].Device);
        Assert.Equal(93, XrPro.HeadsetOf(1)!.VFovDeg); Assert.StartsWith("Meta Quest 2", XrInspector.Budgets[1].Device);
        Assert.Null(XrPro.HeadsetOf(2)); Assert.StartsWith("Handy", XrInspector.Budgets[2].Device);
        Assert.NotNull(XrPro.HeadsetOf(3)); Assert.Null(XrPro.HeadsetOf(9));
    }
}
