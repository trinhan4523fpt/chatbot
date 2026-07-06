namespace Chatbot.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record CreateUserRequest(string Email, string FullName, string? Password, IReadOnlyList<string>? Roles);

public sealed record AssignRolesRequest(IReadOnlyList<string> Roles);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record SetActiveRequest(bool IsActive);

public sealed record CreateChatSessionRequest(long SubjectId, string? Title);

public sealed record SendMessageRequest(string Content);

public sealed record ConfirmEmailRequest(string Email, string Code);

/// <summary>Dùng trong flow kích hoạt tài khoản: xác thực mã OTP và đặt mật khẩu mới cùng lúc.</summary>
public sealed record ConfirmAndSetupPasswordRequest(string Email, string Code, string NewPassword);
