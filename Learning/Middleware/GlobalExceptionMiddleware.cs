using System.Text.Json;
using LearningBackendAPI.DTOs;
using LearningBackendAPI.Helpers;

namespace LearningBackendAPI.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            _logger.LogError(exception, "An unhandled exception occurred");

            var response = context.Response;
            response.ContentType = "application/json";

            var responseHelper = new ResponseHelper();
            ApiResponse<object> apiResponse;

            switch (exception)
            {
                case UnauthorizedAccessException:
                    response.StatusCode = StatusCodes.Status401Unauthorized;
                    apiResponse = responseHelper.Unauthorized<object>(exception.Message);
                    break;

                case KeyNotFoundException:
                    response.StatusCode = StatusCodes.Status404NotFound;
                    apiResponse = responseHelper.NotFound<object>(exception.Message);
                    break;

                case InvalidOperationException:
                    response.StatusCode = StatusCodes.Status400BadRequest;
                    apiResponse = responseHelper.BadRequest<object>(exception.Message);
                    break;

                case ArgumentException:
                    response.StatusCode = StatusCodes.Status400BadRequest;
                    apiResponse = responseHelper.BadRequest<object>(exception.Message);
                    break;

                default:
                    response.StatusCode = StatusCodes.Status500InternalServerError;
                    apiResponse = responseHelper.Error<object>("An unexpected error occurred. Please try again later.");
                    break;
            }

            var jsonResponse = JsonSerializer.Serialize(apiResponse);
            await response.WriteAsync(jsonResponse);
        }
    }
}