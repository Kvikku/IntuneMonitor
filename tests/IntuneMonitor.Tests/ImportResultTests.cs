using System.Net;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for the ImportResult model and ImportErrorCategory classification.
/// </summary>
public class ImportResultTests
{
    [Fact]
    public void Succeeded_ReturnsSuccessResult()
    {
        var result = ImportResult.Succeeded("TestPolicy");
        Assert.True(result.Success);
        Assert.Equal("TestPolicy", result.PolicyName);
        Assert.Null(result.StatusCode);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ErrorBody);
        Assert.Equal(ImportErrorCategory.None, result.ErrorCategory);
    }

    [Fact]
    public void Succeeded_NullName_ReturnsSuccessResult()
    {
        var result = ImportResult.Succeeded(null);
        Assert.True(result.Success);
        Assert.Null(result.PolicyName);
    }

    [Fact]
    public void Failed_BadRequest_ReturnsValidationError()
    {
        var result = ImportResult.Failed("TestPolicy", HttpStatusCode.BadRequest, "Invalid payload");
        Assert.False(result.Success);
        Assert.Equal("TestPolicy", result.PolicyName);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ImportErrorCategory.ValidationError, result.ErrorCategory);
        Assert.Contains("Validation error", result.ErrorMessage);
        Assert.Contains("TestPolicy", result.ErrorMessage);
        Assert.Equal("Invalid payload", result.ErrorBody);
    }

    [Fact]
    public void Failed_Conflict_ReturnsConflictCategory()
    {
        var result = ImportResult.Failed("ConflictPolicy", HttpStatusCode.Conflict, "Already exists");
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal(ImportErrorCategory.Conflict, result.ErrorCategory);
        Assert.Contains("Conflict", result.ErrorMessage);
        Assert.Contains("already exists", result.ErrorMessage);
    }

    [Fact]
    public void Failed_NotFound_ReturnsNotFoundCategory()
    {
        var result = ImportResult.Failed("MissingPolicy", HttpStatusCode.NotFound, "Not found");
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(ImportErrorCategory.NotFound, result.ErrorCategory);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Failed_Unauthorized_ReturnsAuthError()
    {
        var result = ImportResult.Failed("AuthPolicy", HttpStatusCode.Unauthorized, "Token expired");
        Assert.False(result.Success);
        Assert.Equal(ImportErrorCategory.AuthenticationError, result.ErrorCategory);
        Assert.Contains("Authorization failed", result.ErrorMessage);
    }

    [Fact]
    public void Failed_Forbidden_ReturnsAuthError()
    {
        var result = ImportResult.Failed("ForbiddenPolicy", HttpStatusCode.Forbidden, "Insufficient permissions");
        Assert.False(result.Success);
        Assert.Equal(ImportErrorCategory.AuthenticationError, result.ErrorCategory);
    }

    [Fact]
    public void Failed_TooManyRequests_ReturnsThrottled()
    {
        var result = ImportResult.Failed("ThrottledPolicy", HttpStatusCode.TooManyRequests, "Rate limited");
        Assert.False(result.Success);
        Assert.Equal(ImportErrorCategory.Throttled, result.ErrorCategory);
        Assert.Contains("Throttled", result.ErrorMessage);
    }

    [Fact]
    public void Failed_InternalServerError_ReturnsServerError()
    {
        var result = ImportResult.Failed("ServerPolicy", HttpStatusCode.InternalServerError, "Internal error");
        Assert.False(result.Success);
        Assert.Equal(ImportErrorCategory.ServerError, result.ErrorCategory);
    }

    [Fact]
    public void Failed_ServiceUnavailable_ReturnsServerError()
    {
        var result = ImportResult.Failed("UnavailablePolicy", HttpStatusCode.ServiceUnavailable, "Down");
        Assert.False(result.Success);
        Assert.Equal(ImportErrorCategory.ServerError, result.ErrorCategory);
    }

    [Fact]
    public void Failed_UnknownStatusCode_ReturnsUnknown()
    {
        var result = ImportResult.Failed("OtherPolicy", (HttpStatusCode)418, "I'm a teapot");
        Assert.False(result.Success);
        Assert.Equal(ImportErrorCategory.Unknown, result.ErrorCategory);
    }

    [Fact]
    public void Failed_NullErrorBody_HandlesGracefully()
    {
        var result = ImportResult.Failed("NullBodyPolicy", HttpStatusCode.BadRequest, null);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Null(result.ErrorBody);
    }

    [Fact]
    public void Failed_LongErrorBody_IsTruncated()
    {
        var longBody = new string('x', 600);
        var result = ImportResult.Failed("LongPolicy", HttpStatusCode.BadRequest, longBody);
        Assert.True(result.ErrorMessage!.Length < longBody.Length + 200);
    }

    [Fact]
    public void FailedWithException_ReturnsExceptionResult()
    {
        var ex = new HttpRequestException("Connection refused");
        var result = ImportResult.FailedWithException("ExceptionPolicy", ex);
        Assert.False(result.Success);
        Assert.Equal("ExceptionPolicy", result.PolicyName);
        Assert.Equal(ImportErrorCategory.Unknown, result.ErrorCategory);
        Assert.Contains("Connection refused", result.ErrorMessage);
        Assert.Null(result.StatusCode);
    }

    [Fact]
    public void ImportErrorCategory_HasAllExpectedValues()
    {
        var values = Enum.GetValues<ImportErrorCategory>();
        Assert.Equal(8, values.Length);
        Assert.Contains(ImportErrorCategory.None, values);
        Assert.Contains(ImportErrorCategory.ValidationError, values);
        Assert.Contains(ImportErrorCategory.AuthenticationError, values);
        Assert.Contains(ImportErrorCategory.NotFound, values);
        Assert.Contains(ImportErrorCategory.Conflict, values);
        Assert.Contains(ImportErrorCategory.Throttled, values);
        Assert.Contains(ImportErrorCategory.ServerError, values);
        Assert.Contains(ImportErrorCategory.Unknown, values);
    }
}
