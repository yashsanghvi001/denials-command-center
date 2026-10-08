namespace DenialsCommandCenter.Tests;

public class TestDataTests
{
    [Fact]
    public void Data_pack_is_present()
    {
        Assert.True(File.Exists(TestData.PathOf("claims_export.csv")));
        Assert.True(File.Exists(TestData.PathOf("remits", "era_2026Q1.835")));
    }
}
