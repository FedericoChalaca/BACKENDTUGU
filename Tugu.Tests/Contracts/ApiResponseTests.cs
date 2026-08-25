using Tugu.Contracts.Common;

namespace Tugu.Tests.Contracts;

public class ApiResponseTests
{
    [Fact]
    public void Ok_DebeMarcarSuccessYExponerData()
    {
        var response = ApiResponse<string>.Ok("hola");

        Assert.True(response.Success);
        Assert.Equal("hola", response.Data);
        Assert.Null(response.Error);
    }

    [Fact]
    public void Fail_DebeMarcarErrorConCodigoYMensaje()
    {
        var response = ApiResponse<string>.Fail("VALIDATION_ERROR", "Datos inválidos.");

        Assert.False(response.Success);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("VALIDATION_ERROR", response.Error.Code);
        Assert.Equal("Datos inválidos.", response.Error.Message);
    }
}
