namespace LearningBackendAPI.Utils
{
    public static class Constants
    {
        public static class Messages
        {
            public const string LoginSuccess = "Login successful";
            public const string LoginFailed = "Invalid email or password";
            public const string DeviceMismatch = "Your account is already registered on another device. Please contact the administrator to reset your device access.";
            public const string DeviceIdRequired = "Device id is required";
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
            public const string PaymentScreenshotRequired = "Payment screenshot file is required";
            public const string PaymentScreenshotInvalidType = "Only JPG, PNG, or WEBP images are allowed";
            public const string PaymentScreenshotTooLarge = "Payment screenshot must not exceed 5 MB";
            public const string PaymentAlreadyVerified = "Payment is already verified for this enrollment";
            public const string InvalidPaymentMethod = "Invalid payment method";
            public const string InvalidEnrollmentStatus = "Invalid enrollment status";
            public const string NoCourseAccess = "You do not have access to this course's content until your payment is verified";
            public const string QuizNotFound = "Quiz not found";
            public const string QuizNotPublished = "This quiz is not currently published";
            public const string QuizExpired = "This quiz has expired and is no longer accepting submissions";
            public const string QuizAlreadyAttempted = "You have already submitted this quiz";
            public const string QuizAttemptsExhausted = "You have used all your attempts for this quiz";
            public const string QuizNotDraft = "Only a draft quiz can be deleted";
            public const string QuizAlreadyPublished = "This quiz has already been published";
            public const string RankListNotForFreeQuiz = "Rank list is not available for free quizzes";
            public const string InvalidQuizTypeFilter = "quizType must be 'competitive', 'school', 'previousYear', 'syllabusWise', 'daily' or 'currentAffairs'";
            public const string RankListQuizTypeMismatch = "The quiz is not of the requested quizType";
            public const string RankListFreeViewOnPaidQuiz = "Unpaid students can only be ranked for free quizzes";
            public const string QuizNotAttempted = "You have not submitted this quiz yet";
            public const string QuizIncomplete = "Every question must have text, options A, B and C (D is optional), and a correct answer selected before publishing";
            public const string QuizExpiryRequired = "Expiry date and time must be a valid date in the future";
            public const string InvalidOption = "Selected option must be A, B, C, or D";
            public const string InvalidMark = "Mark must be a positive number";
            public const string BatchNotFound = "Batch not found";
            public const string BatchDateRangeInvalid = "Batch start date must be before or equal to the end date";
            public const string BatchCourseMismatch = "Selected batch does not belong to the selected course";
            public const string BatchExpired = "Selected batch has already ended";
            public const string EnrollmentDropped = "Your enrollment for this course was dropped - please contact the admin to rejoin";
            public const string CourseAlreadyEnrolled = "You are already enrolled in this course. To join another batch, please contact the Administration.";
            public const string NotificationNotFound = "Notification not found";
            public const string InvalidCourseType = "courseType must be 'Paid', 'Free' or 'All'";
            public const string InvalidMaterialAccess = "MaterialToView must be either 'Paid' or 'Free'";
            public const string TransactionReferenceInvalid = "Enter the UPI transaction ID / UTR (6-35 letters or digits)";
            public const string TransactionReferenceInUse = "This transaction ID has already been used for a payment.";
            public const string UpiNotConfigured = "UPI payments are not set up yet. Please contact the admin.";
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
            public const string Upi = "UPI";

            public static readonly string[] All = { GooglePay, BankTransfer, Cash, Upi };
        }

        // The quiz tabs in the UI: Competitive/Daily, School Book, Previous Year, Syllabus Wise
        public static class QuizTypes
        {
            public const string Competitive = "competitive";
            public const string School = "school";
            public const string PreviousYear = "previousYear";
            public const string SyllabusWise = "syllabusWise";
            public const string Daily = "daily";
            public const string CurrentAffairs = "currentAffairs";

            public static readonly string[] All = { Competitive, School, PreviousYear, SyllabusWise, Daily, CurrentAffairs };

            // A daily quiz is always Free - it never needs a course, batch or enrollment
            public static bool IsAlwaysFree(string? quizType) => quizType == Daily;

            // Quiz types that are filed under a folder (and sub folder) from GET /api/Folder
            public static bool UsesFolders(string? quizType) => quizType == PreviousYear || quizType == SyllabusWise || quizType == School || quizType == CurrentAffairs;

            // Accepts the tab keys or labels in any case/spacing: "school", "School Book", "previous year", "Competitive/Daily" ...
            public static string? Normalize(string? value)
            {
                var key = new string((value ?? "").Where(char.IsLetter).ToArray()).ToLowerInvariant();
                return key switch
                {
                    "competitive" or "competitivedaily" => Competitive,
                    "daily" => Daily,
                    "currentaffairs" or "currentaffair" => CurrentAffairs,
                    "school" or "schoolbook" => School,
                    "previousyear" => PreviousYear,
                    "syllabuswise" or "syllabus" => SyllabusWise,
                    _ => null
                };
            }
        }

        public static class NotificationTypes
        {
            public const string Flash = "Flash Notification";
            public const string Job = "Job Notification";

            // Accepts the dropdown labels in any case/spacing: "Flash Notification", "flash", "JobNotification" ...
            // "Push Notification" is the old name for Flash and is still accepted (and matches notifications saved under it)
            public static string? Normalize(string? value)
            {
                var key = (value ?? "").Replace(" ", "").ToLowerInvariant();
                return key switch
                {
                    "flash" or "flashnotification" or "push" or "pushnotification" => Flash,
                    "job" or "jobnotification" => Job,
                    _ => null
                };
            }
        }

        // Who a notification is for (and, with sendPush, who gets the push)
        public static class NotificationAudiences
        {
            public const string All = "All";
            public const string Course = "Course";
            public const string Batch = "Batch";
            public const string Users = "Users";
            public const string SyllabusPaid = "SyllabusPaid";

            public static readonly string[] AllValues = { All, Course, Batch, Users, SyllabusPaid };

            public static string? Normalize(string? value)
                => AllValues.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static class PushStatuses
        {
            public const string None = "None";
            public const string Scheduled = "Scheduled";
            public const string Sending = "Sending";
            public const string Sent = "Sent";
            public const string PartiallyFailed = "PartiallyFailed";
            public const string Failed = "Failed";
            public const string Cancelled = "Cancelled";
        }

        public static class EnrollmentStatuses
        {
            public const string Pending = "Pending";
            public const string Verified = "Verified";
            public const string Dropped = "Dropped";
            public const string Rejected = "Rejected";

            public static readonly string[] All = { Pending, Verified, Dropped, Rejected };
        }

        public static class PaymentStatuses
        {
            public const string Pending = "Pending";
            public const string Verified = "Verified";
            public const string Rejected = "Rejected";

            public static readonly string[] All = { Pending, Verified, Rejected };

            public static string Normalize(string? value)
            {
                var match = All.FirstOrDefault(s => string.Equals(s, value?.Trim(), StringComparison.OrdinalIgnoreCase));
                return match ?? throw new InvalidOperationException("paymentStatus must be pending, verified or rejected");
            }
        }

        public static class LiveClassStatuses
        {
            public const string NotStarted = "Not Started";
            public const string Online = "Online";
            public const string Expired = "Expired";
        }

        public static class QuizStatuses
        {
            public const string Draft = "Draft";
            public const string Published = "Published";
        }

        // Course list filter. A course priced at 0 is Free, anything above 0 is Paid
        public static class CourseTypes
        {
            public const string Paid = "Paid";
            public const string Free = "Free";
            public const string All = "All";

            public static string Normalize(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return All;
                }
                return new[] { Paid, Free, All }.FirstOrDefault(t => string.Equals(t, value.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(Messages.InvalidCourseType);
            }
        }

        public static class MaterialAccess
        {
            public const string Paid = "Paid";
            public const string Free = "Free";

            public static readonly string[] All = { Paid, Free };

            public static string Normalize(string value)
            {
                var match = All.FirstOrDefault(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    throw new InvalidOperationException(Messages.InvalidMaterialAccess);
                }
                return match;
            }
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