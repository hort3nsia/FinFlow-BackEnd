using FinFlow.Application.Auth.Commands.ChangePassword;
using FinFlow.Application.Auth.Commands.Login;
using FinFlow.Application.Auth.Commands.Logout;
using FinFlow.Application.Auth.Commands.RefreshToken;
using FinFlow.Application.Auth.Commands.Register;
using FinFlow.Application.Auth.Validators;

namespace FinFlow.UnitTests.Application.Auth;

public sealed class AuthCommandValidatorTests
{
    [Fact]
    public void LoginCommandValidator_ReturnsErrors_ForEmptyFields()
    {
        var validator = new LoginCommandValidator();
        var command = new LoginCommand("", "", "");

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(LoginCommand.Email));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(LoginCommand.Password));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(LoginCommand.TenantCode));
    }

    [Fact]
    public void RegisterCommandValidator_ReturnsErrors_ForInvalidFields()
    {
        var validator = new RegisterCommandValidator();
        var command = new RegisterCommand("not-an-email", "", "", "", "");

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterCommand.Email));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterCommand.Password));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterCommand.Name));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterCommand.TenantCode));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterCommand.DepartmentName));
    }

    [Fact]
    public void RefreshTokenAndLogoutValidators_ReturnErrors_ForEmptyRefreshToken()
    {
        var refreshValidator = new RefreshTokenCommandValidator();
        var logoutValidator = new LogoutCommandValidator();

        var refreshResult = refreshValidator.Validate(new RefreshTokenCommand(""));
        var logoutResult = logoutValidator.Validate(new LogoutCommand(""));

        Assert.Contains(refreshResult.Errors, x => x.PropertyName == nameof(RefreshTokenCommand.RefreshToken));
        Assert.Contains(logoutResult.Errors, x => x.PropertyName == nameof(LogoutCommand.RefreshToken));
    }

    [Fact]
    public void ChangePasswordCommandValidator_ReturnsErrors_ForMissingFields()
    {
        var validator = new ChangePasswordCommandValidator();
        var command = new ChangePasswordCommand(Guid.Empty, "", "");

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ChangePasswordCommand.AccountId));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ChangePasswordCommand.CurrentPassword));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }
}
