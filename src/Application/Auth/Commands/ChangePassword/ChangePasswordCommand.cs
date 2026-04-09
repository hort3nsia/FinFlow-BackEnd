using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.ChangePassword;

public record ChangePasswordCommand(Guid AccountId, string CurrentPassword, string NewPassword)
    : ICommand<Result>;
