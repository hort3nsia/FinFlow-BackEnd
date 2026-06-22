using FinFlow.Application.Auth.DTOs.Requests;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.CheckPasswordResetOtp;

public sealed record CheckPasswordResetOtpCommand(CheckPasswordResetOtpRequest Request) : Common.ICommand<Result>;
