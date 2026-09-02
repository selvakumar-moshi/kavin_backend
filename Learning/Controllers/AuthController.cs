using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;
using LearningBackendAPI.Services;
using LearningBackendAPI.Validators;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ResponseHelper _responseHelper;
        private readonly RegisterValidator _registerValidator;

        public AuthController(
            IAuthService authService,
            ResponseHelper responseHelper,
            RegisterValidator registerValidator)
        {
            _authService = authService;
            _responseHelper = responseHelper;
            _registerValidator = registerValidator;
        }

        /// <summary>
        /// Admin Login
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(_responseHelper.Success(result, "Login successful"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(_responseHelper.Unauthorized<object>(ex.Message));
            }
        }

        /// <summary>
        /// User Registration
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Validate request
            var validationResult = await _registerValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(_responseHelper.BadRequest<object>("Validation failed", errors));
            }

            try
            {
                var result = await _authService.RegisterAsync(request);
                return Ok(_responseHelper.Success(result, "Registration successful"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }
    }
}