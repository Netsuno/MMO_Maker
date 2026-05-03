using Frog.Core.Constants;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class WireHelloTests
{
    [Fact]
    public void Build_And_Parse_Roundtrip()
    {
        var bytes = WireHello.BuildPayload();
        Assert.True(WireHello.TryParse(bytes, out var msg, out var ver));
        Assert.Equal(WireHello.DefaultMessage, msg);
        Assert.Equal(FrogWireProtocol.Version, ver);
    }
}
