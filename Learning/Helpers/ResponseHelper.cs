using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Helpers
{
    public class ResponseHelper
    {
        public ApiResponse<T> Success<T>(T data, string message = "Success")
        {
            return ApiResponse<T>.SuccessResponse(data, message);
        }

        public ApiResponse<T> Error<T>(string message, List<string> errors = null)
        {
            return ApiResponse<T>.ErrorResponse(message, errors);
        }

        public ApiResponse<T> Created<T>(T data, string message = "Resource created successfully")
        {
            return ApiResponse<T>.SuccessResponse(data, message);
        }

        public ApiResponse<T> NotFound<T>(string message = "Resource not found")
        {
            return ApiResponse<T>.ErrorResponse(message);
        }

        public ApiResponse<T> BadRequest<T>(string message, List<string> errors = null)
        {
            return ApiResponse<T>.ErrorResponse(message, errors);
        }

        public ApiResponse<T> Unauthorized<T>(string message = "Unauthorized")
        {
            return ApiResponse<T>.ErrorResponse(message);
        }

        public ApiResponse<T> Forbidden<T>(string message = "Forbidden")
        {
            return ApiResponse<T>.ErrorResponse(message);
        }
    }
}