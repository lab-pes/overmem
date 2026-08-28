using System.IO;
using System.Linq;
using System.Text;
using Overmem.Extensions.Pes2021.ClubRelations;

namespace Overmem.Extensions.Pes2021.Tests.ClubRelations;

public sealed class Pes2021ClubCatalogLoaderTests
{
    [Fact]
    public void LoadFromFile_ParsesHeaderAndRows()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"catalog-{System.Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(tempFile,
                "team_id,secondary_id,name,short_name,city_or_stadium,address,region_base,region_offset\n" +
                "32784,313,\"SANTOS\",\"SAN\",\"Santos\",0x7FF4DAD60F0C,0x7FF4DA280000,0xAE0F0C\n" +
                "32768,482,\"ATHLETICO PARANAENSE\",\"ATP\",\"Curitiba\",0x7FF4DAD60000,0x7FF4DA280000,0xAE0000\n",
                new UTF8Encoding(false));

            var result = Pes2021ClubCatalogLoader.LoadFromFile(tempFile);

            Assert.Equal(2, result.Rows.Count);
            var santos = result.Rows.Single(r => r.TeamId == 32784);
            Assert.Equal(313, santos.SecondaryId);
            Assert.Equal("SANTOS", santos.Name);
            Assert.Equal(0x7FF4DAD60F0CUL, santos.Address);
            Assert.Equal(0x7FF4DA280000UL, santos.RegionBase);
            Assert.Equal(0xAE0F0CUL, santos.RegionOffset);
            Assert.Equal(64, result.SourceSha256.Length);
            Assert.Empty(result.Warnings);
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
    public void LoadFromFile_WarnsOnInvalidRow()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"catalog-bad-{System.Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(tempFile,
                "team_id,secondary_id,name,short_name,city_or_stadium,address,region_base,region_offset\n" +
                "not-an-int,313,\"SANTOS\",\"SAN\",\"Santos\",0x0,0x0,0x0\n" +
                "32768,482,\"ATHLETICO\",\"ATP\",\"Curitiba\",0x0,0x0,0x0\n",
                new UTF8Encoding(false));

            var result = Pes2021ClubCatalogLoader.LoadFromFile(tempFile);

            Assert.Single(result.Rows);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("team_id"));
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
            Pes2021ClubCatalogLoader.LoadFromFile(Path.Combine(Path.GetTempPath(), "missing.csv")));
    }
}
