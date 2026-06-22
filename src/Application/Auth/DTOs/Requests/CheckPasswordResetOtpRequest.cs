namespace FinFlow.Application.Auth.DTOs.Requests;

public sealed record CheckPasswordResetOtpRequest(string Email, string Otp);
