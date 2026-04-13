using FinFlow.Application.Auth.DTOs.Requests;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.VerifyEmailByOtp;

public record VerifyEmailByOtpCommand(VerifyEmailByOtpRequest Request)
    : ICommand<Result>;
