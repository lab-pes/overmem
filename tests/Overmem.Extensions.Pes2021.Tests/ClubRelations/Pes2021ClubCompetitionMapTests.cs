using System.IO;
using System.Linq;
using System.Text;
using Overmem.Extensions.Pes2021.ClubRelations;

namespace Overmem.Extensions.Pes2021.Tests.ClubRelations;

public sealed class Pes2021ClubCompetitionMapTests
{
    [Fact]
    public void LoadFromFile_ParsesIdEqualsName()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"map-{System.Guid.NewGuid():N}.cfg");
        try
        {
            File.WriteAllText(tempFile,
                "# header\n" +
                "17=BRASILEIRÃO BETANO\n" +
                "18=LIGA PROFESSIONAL\n",
                new UTF8Encoding(false));

            var map = Pes2021ClubCompetitionMap.LoadFromFile(tempFile);

            Assert.Equal("BRASILEIRÃO BETANO", map[17]);
            Assert.Equal("LIGA PROFESSIONAL", map[18]);
            Assert.Equal(2, map.Count);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void LoadFromFile_IgnoresCommentsAndBlankLines()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"map-{System.Guid.NewGuid():N}.cfg");
        try
        {
            File.WriteAllText(tempFile,
                "\n" +
                "# only comment\n" +
                "20=LIGA PRO SERIE A\n" +
                "#21=IGNORED COMMENT\n",
                new UTF8Encoding(false));

            var map = Pes2021ClubCompetitionMap.LoadFromFile(tempFile);

            Assert.Single(map);
            Assert.Equal("LIGA PRO SERIE A", map[20]);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void LoadFromFile_ThrowsWhenFileMissing()
    {
        Assert.Throws<FileNotFoundException>(() =>
            Pes2021ClubCompetitionMap.LoadFromFile(Path.Combine(Path.GetTempPath(), "missing.cfg")));
    }
}
