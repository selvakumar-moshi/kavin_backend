using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Services;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly ICounterRepository _counterRepository;
        private readonly IJwtService _jwtService;

        public AuthService(
            IUserRepository userRepository,
            ICounterRepository counterRepository,
            IJwtService jwtService)
        {
            _userRepository = userRepository;
            _counterRepository = counterRepository;
            _jwtService = jwtService;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);

            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Single-device lock for students only (an admin must be able to sign in anywhere).
            // Checked after the password so the device state isn't revealed to someone who doesn't know it.
            if (user.Role != Constants.Roles.Admin)
            {
                await EnforceSingleDeviceAsync(user, request.DeviceId);
            }

            var now = DateTime.UtcNow;
            await _userRepository.SetLastLoginAsync(user.Id, now);
            user.LastLoginDate = now;

            var token = _jwtService.GenerateToken(user);

            return new LoginResponse
            {
                Token = token,
                User = MapToUserDto(user),
                MustChangePassword = user.MustChangePassword
            };
        }

        private async Task EnforceSingleDeviceAsync(User user, string? deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new InvalidOperationException(Constants.Messages.DeviceIdRequired);
            }

            var incoming = HashDeviceId(deviceId);

            if (string.IsNullOrEmpty(user.SavedDeviceId))
            {
                // First login: bind this device. The write is conditional, so if two logins race, only one wins.
                if (await _userRepository.TryRegisterDeviceAsync(user.Id, incoming))
                {
                    return;
                }
                user = await _userRepository.GetByIdAsync(user.Id);
            }

            var saved = user.SavedDeviceId ?? "";
            var matches = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(saved),
                System.Text.Encoding.UTF8.GetBytes(incoming));
            if (!matches)
            {
                throw new DeviceMismatchException();
            }
        }

        // Only a hash is stored, so a database leak doesn't expose the devices' real ids
        private static string HashDeviceId(string deviceId)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(deviceId.Trim()));
            return Convert.ToHexString(bytes);
        }

        // Used after an admin reset: the user proves they hold the temporary (current) password, then sets their own
        public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            if (string.IsNullOrEmpty(request.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new InvalidOperationException("Current password is incorrect");
            }

            var errors = new List<string>();
            var password = request.NewPassword ?? "";
            if (password.Length < 6) errors.Add("Password must be at least 6 characters");
            if (!password.Any(char.IsUpper)) errors.Add("Password must contain at least one uppercase letter");
            if (!password.Any(char.IsLower)) errors.Add("Password must contain at least one lowercase letter");
            if (!password.Any(char.IsDigit)) errors.Add("Password must contain at least one number");
            if (!password.Any(c => "@$!%*?&".Contains(c))) errors.Add("Password must contain at least one special character");
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("; ", errors));
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                throw new InvalidOperationException("Passwords do not match");
            }
            if (request.NewPassword == request.CurrentPassword)
            {
                throw new InvalidOperationException("New password must be different from the current password");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.MustChangePassword = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(userId, user);
        }

        public async Task<UserDto> RegisterAsync(RegisterRequest request)
        {
            // Check if email already exists
            if (await _userRepository.EmailExistsAsync(request.Email))
            {
                throw new InvalidOperationException("Email is already registered");
            }

            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var applicationNo = await GenerateApplicationNoAsync();

            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email.ToLower(),
                PhoneNumber = request.PhoneNumber,
                District = request.District.Trim(),
                PasswordHash = passwordHash,
                ApplicationNo = applicationNo,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            var createdUser = await _userRepository.CreateAsync(user);

            return MapToUserDto(createdUser);
        }

        private async Task<string> GenerateApplicationNoAsync()
        {
            var sequence = await _counterRepository.GetNextSequenceAsync(Constants.ApplicationNumber.CounterName);
            return $"{Constants.ApplicationNumber.Prefix}{Constants.ApplicationNumber.Offset + sequence}";
        }

        private UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                ApplicationNo = user.ApplicationNo,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                District = user.District,
                Role = user.Role,
                ProfileImage = user.ProfileImage ?? "",
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                MustChangePassword = user.MustChangePassword
            };
        }
    }
}