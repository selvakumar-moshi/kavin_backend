namespace LearningBackendAPI.Utils
{
    public static class Constants
    {
        public static class Messages
        {
            public const string LoginSuccess = "Login successful";
            public const string LoginFailed = "Invalid email or password";
            public const string RegistrationSuccess = "Registration successful";
            public const string EmailExists = "Email is already registered";
            public const string UserNotFound = "User not found";
            public const string CourseCreated = "Course created successfully";
            public const string CourseUpdated = "Course updated successfully";
            public const string CourseDeleted = "Course deleted successfully";
            public const string CourseNotFound = "Course not found";
            public const string CourseExists = "Course with this name already exists";
            public const string ValidationFailed = "Validation failed";
            public const string Unauthorized = "Unauthorized access";
            public const string Forbidden = "Access denied";
            public const string InternalError = "An unexpected error occurred. Please try again later.";
        }

        public static class Roles
        {
            public const string Admin = "Admin";
            public const string User = "User";
        }

        public static class CollectionNames
        {
            public const string Users = "Users";
            public const string Courses = "Courses";
        }
    }
}