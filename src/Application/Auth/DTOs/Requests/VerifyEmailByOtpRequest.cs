namespace FinFlow.Application.Auth.DTOs.Requests;

public record VerifyEmailByOtpRequest(string Email, string Otp);
