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

            var token = _jwtService.GenerateToken(user);

            return new LoginResponse
            {
                Token = token,
                User = MapToUserDto(user)
            };
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
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}