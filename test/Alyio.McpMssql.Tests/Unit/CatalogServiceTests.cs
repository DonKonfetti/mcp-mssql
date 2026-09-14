// MIT License

using Alyio.McpMssql.Services;

namespace Alyio.McpMssql.Tests.Unit;

public class CatalogServiceTests
{
    [Theory]
    [InlineData(229)]
    [InlineData(262)]
    [InlineData(297)]
    [InlineData(300)]
    public void ContainsRowCountPermissionError_Accepts_Permission_Errors(int errorNumber)
    {
        var result = CatalogService.ContainsRowCountPermissionError([errorNumber]);

        Assert.True(result);
    }

    [Fact]
    public void ContainsRowCountPermissionError_Checks_All_Errors()
    {
        var result = CatalogService.ContainsRowCountPermissionError([50000, 297]);

        Assert.True(result);
    }

    [Theory]
    [InlineData(208)]
    [InlineData(18456)]
    public void ContainsRowCountPermissionError_Rejects_Unrelated_Errors(int errorNumber)
    {
        var result = CatalogService.ContainsRowCountPermissionError([errorNumber]);

        Assert.False(result);
    }
}
