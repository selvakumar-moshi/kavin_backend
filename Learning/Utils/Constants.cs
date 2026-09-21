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
            public const string EnrollmentNotFound = "Enrollment not found";
            public const string InvalidPaymentMethod = "Invalid payment method";
            public const string InvalidEnrollmentStatus = "Invalid enrollment status";
            public const string NoCourseAccess = "You do not have access to this course's content until your payment is verified";
            public const string QuizNotFound = "Quiz not found";
            public const string QuizNotPublished = "This quiz is not currently published";
            public const string QuizExpired = "This quiz has expired and is no longer accepting submissions";
            public const string QuizAlreadyAttempted = "You have already submitted this quiz";
            public const string QuizNotDraft = "Only a draft quiz can be deleted";
            public const string QuizAlreadyPublished = "This quiz has already been published";
            public const string QuizNotAttempted = "You have not submitted this quiz yet";
            public const string QuizIncomplete = "Every question must have text, all four options, and a correct answer selected before publishing";
            public const string QuizExpiryRequired = "Expiry date and time must be a valid date in the future";
            public const string InvalidOption = "Selected option must be A, B, C, or D";
            public const string BatchNotFound = "Batch not found";
            public const string BatchDateRangeInvalid = "Batch start date must be before or equal to the end date";
            public const string BatchCourseMismatch = "Selected batch does not belong to the selected course";
            public const string BatchExpired = "Selected batch has already ended";
            public const string CourseAlreadyEnrolled = "User is already enrolled in this course";
            public const string NotificationNotFound = "Notification not found";
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
            public const string Enrollments = "Enrollments";
            public const string Quizzes = "Quizzes";
            public const string QuizAttempts = "QuizAttempts";
            public const string Batches = "Batches";
        }

        public static class PaymentMethods
        {
            public const string GooglePay = "GooglePay";
            public const string BankTransfer = "BankTransfer";
            public const string Cash = "Cash";

            public static readonly string[] All = { GooglePay, BankTransfer, Cash };
        }

        public static class EnrollmentStatuses
        {
            public const string Pending = "Pending";
            public const string Verified = "Verified";

            public static readonly string[] All = { Pending, Verified };
        }

        public static class QuizStatuses
        {
            public const string Draft = "Draft";
            public const string Published = "Published";
        }

        public static class QuizOptions
        {
            public const string A = "A";
            public const string B = "B";
            public const string C = "C";
            public const string D = "D";

            public static readonly string[] All = { A, B, C, D };
        }

        public static class ApplicationNumber
        {
            public const string CounterName = "applicationNo";
            public const string Prefix = "RSK-";

            // First generated application number is Prefix + (Offset + 1), e.g. RSK-1000.
            public const long Offset = 999;
        }
    }
}