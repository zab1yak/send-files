using SendFiles.Core.Qr;

namespace SendFiles.Tests.Qr;

public class QrMatrixTests
{
    [Fact]
    public void Matrix_has_finder_patterns_and_no_quiet_zone()
    {
        var qr = QrMatrix.Create("http://192.168.1.42:47130/pair#t=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        // Version N has 17 + 4N modules per side.
        Assert.Equal(0, (qr.Size - 17) % 4);
        Assert.True(qr[0, 0]);
        Assert.True(qr[0, qr.Size - 1]);
        Assert.True(qr[qr.Size - 1, 0]);
        // The finder's inner ring is light.
        Assert.False(qr[1, 1]);
    }
}
