using SmileTrack_MVC.Models.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace SmileTrack_MVC.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task<AuthResponse> RecoverPasswordAsync(RecoverPasswordRequest request, CancellationToken ct = default);
        Task<AuthResponse> VerifyRecoveryCodeAsync(VerifyRecoveryCodeRequest request, CancellationToken ct = default);
        Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
        Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    }
}
