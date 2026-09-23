using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _quizRepository;
        private readonly IQuizAttemptRepository _quizAttemptRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IExcelExportService _excelExportService;

        public QuizService(
            IQuizRepository quizRepository,
            IQuizAttemptRepository quizAttemptRepository,
            ICourseRepository courseRepository,
            IEnrollmentRepository enrollmentRepository,
            IUserRepository userRepository,
            IFileStorageService fileStorageService,
            IExcelExportService excelExportService)
        {
            _quizRepository = quizRepository;
            _quizAttemptRepository = quizAttemptRepository;
            _courseRepository = courseRepository;
            _enrollmentRepository = enrollmentRepository;
            _userRepository = userRepository;
            _fileStorageService = fileStorageService;
            _excelExportService = excelExportService;
        }

        public async Task<Quiz> CreateQuizAsync(QuizCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CourseId))
            {
                throw new InvalidOperationException("Course is required");
            }

            var course = await _courseRepository.GetByIdAsync(request.CourseId);
            if (course == null)
            {
                throw new InvalidOperationException("Course not found");
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title is required");
            }

            if (request.Questions == null || request.Questions.Count == 0)
            {
                throw new InvalidOperationException("At least one question is required");
            }

            var quiz = new Quiz
            {
                CourseId = course.Id,
                CourseName = course.CourseName,
                Title = request.Title.Trim(),
                Status = Constants.QuizStatuses.Draft,
                Questions = await MapQuestionsAsync(request.Questions, null),
                CreatedAt = DateTime.UtcNow
            };

            return await _quizRepository.CreateAsync(quiz);
        }

        public async Task<Quiz> UpdateQuizAsync(string id, QuizUpdateRequest request)
        {
            var quiz = await _quizRepository.GetByIdAsync(id);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                quiz.Title = request.Title.Trim();
            }

            if (request.Questions != null)
            {
                quiz.Questions = await MapQuestionsAsync(request.Questions, quiz.Questions);
            }

            if (quiz.Status == Constants.QuizStatuses.Published)
            {
                // Editing a live quiz takes it offline; the admin must explicitly
                // republish (with a fresh expiry) to make the updated version visible again.
                quiz.Status = Constants.QuizStatuses.Draft;
                quiz.PublishedAt = null;
                quiz.ExpiresAt = null;
            }

            quiz.UpdatedAt = DateTime.UtcNow;

            await _quizRepository.UpdateAsync(id, quiz);
            return quiz;
        }

        private async Task<List<QuizQuestionItem>> MapQuestionsAsync(List<QuizQuestionInput> questions, List<QuizQuestionItem>? existing)
        {
            foreach (var q in questions)
            {
                if (!string.IsNullOrWhiteSpace(q.CorrectOption) &&
                    !Constants.QuizOptions.All.Contains(q.CorrectOption.ToUpperInvariant()))
                {
                    throw new InvalidOperationException(Constants.Messages.InvalidOption);
                }
            }

            var items = new List<QuizQuestionItem>();
            for (var index = 0; index < questions.Count; index++)
            {
                var q = questions[index];
                var previous = existing != null && index < existing.Count ? existing[index] : null;

                var item = new QuizQuestionItem
                {
                    QuestionNumber = index + 1,
                    QuestionText = q.QuestionText.Trim(),
                    OptionA = q.OptionA.Trim(),
                    OptionB = q.OptionB.Trim(),
                    OptionC = q.OptionC.Trim(),
                    OptionD = q.OptionD.Trim(),
                    CorrectOption = string.IsNullOrWhiteSpace(q.CorrectOption) ? null : q.CorrectOption.ToUpperInvariant()
                };

                (item.QuestionImageUrl, item.QuestionImageKey) = await ResolveImageAsync(
                    q.QuestionImage, previous?.QuestionImageUrl, previous?.QuestionImageKey);
                (item.OptionAImageUrl, item.OptionAImageKey) = await ResolveImageAsync(
                    q.OptionAImage, previous?.OptionAImageUrl, previous?.OptionAImageKey);
                (item.OptionBImageUrl, item.OptionBImageKey) = await ResolveImageAsync(
                    q.OptionBImage, previous?.OptionBImageUrl, previous?.OptionBImageKey);
                (item.OptionCImageUrl, item.OptionCImageKey) = await ResolveImageAsync(
                    q.OptionCImage, previous?.OptionCImageUrl, previous?.OptionCImageKey);
                (item.OptionDImageUrl, item.OptionDImageKey) = await ResolveImageAsync(
                    q.OptionDImage, previous?.OptionDImageUrl, previous?.OptionDImageKey);

                items.Add(item);
            }

            return items;
        }

        private const string QuestionImageFolder = "coaching/Question";

        private async Task<(string? Url, string? Key)> ResolveImageAsync(
            IFormFile? file, string? previousUrl, string? previousKey)
        {
            if (file == null || file.Length == 0)
            {
                return (previousUrl, previousKey);
            }

            if (!string.IsNullOrWhiteSpace(previousKey))
            {
                await _fileStorageService.DeleteAsync(previousKey);
            }

            var uniqueFileName = $"{Path.GetFileName(file.FileName)}";
            var (url, key) = await _fileStorageService.UploadImageAsync(file, QuestionImageFolder, uniqueFileName);
            return (url, key);
        }

        public async Task<Quiz> PublishQuizAsync(string id, DateTime expiresAt)
        {
            var quiz = await _quizRepository.GetByIdAsync(id);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (quiz.Status != Constants.QuizStatuses.Draft)
            {
                throw new InvalidOperationException(Constants.Messages.QuizAlreadyPublished);
            }

            if (quiz.Questions.Count == 0)
            {
                throw new InvalidOperationException(Constants.Messages.QuizIncomplete);
            }

            var incomplete = quiz.Questions.Any(q =>
                string.IsNullOrWhiteSpace(q.QuestionText) ||
                string.IsNullOrWhiteSpace(q.OptionA) ||
                string.IsNullOrWhiteSpace(q.OptionB) ||
                string.IsNullOrWhiteSpace(q.OptionC) ||
                string.IsNullOrWhiteSpace(q.OptionD) ||
                string.IsNullOrWhiteSpace(q.CorrectOption));

            if (incomplete)
            {
                throw new InvalidOperationException(Constants.Messages.QuizIncomplete);
            }

            if (expiresAt <= DateTime.UtcNow)
            {
                throw new InvalidOperationException(Constants.Messages.QuizExpiryRequired);
            }

            quiz.Status = Constants.QuizStatuses.Published;
            quiz.PublishedAt = DateTime.UtcNow;
            quiz.ExpiresAt = expiresAt;
            quiz.PublishVersion += 1;

            await _quizRepository.UpdateAsync(id, quiz);
            return quiz;
        }

        public async Task<bool> DeleteQuizAsync(string id)
        {
            var quiz = await _quizRepository.GetByIdAsync(id);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (quiz.Status != Constants.QuizStatuses.Draft)
            {
                throw new InvalidOperationException(Constants.Messages.QuizNotDraft);
            }

            var deleted = await _quizRepository.DeleteAsync(id);
            if (deleted)
            {
                foreach (var q in quiz.Questions)
                {
                    await _fileStorageService.DeleteAsync(q.QuestionImageKey);
                    await _fileStorageService.DeleteAsync(q.OptionAImageKey);
                    await _fileStorageService.DeleteAsync(q.OptionBImageKey);
                    await _fileStorageService.DeleteAsync(q.OptionCImageKey);
                    await _fileStorageService.DeleteAsync(q.OptionDImageKey);
                }
            }

            return deleted;
        }

        private static readonly Dictionary<string, Func<Quiz, string?>> QuizSearchFields = new()
        {
            ["title"] = q => q.Title,
            ["coursename"] = q => q.CourseName
        };
        private static readonly string[] DefaultQuizSearchFields = { "title", "courseName" };

        public async Task<PagedResult<Quiz>> GetAllQuizzesForAdminAsync(string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            var quizzes = await _quizRepository.GetAllAsync();
            var filtered = TextSearchHelper.ApplyFilter(quizzes, searchTerm, globalFilter, QuizSearchFields, DefaultQuizSearchFields);
            return PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
        }

        public async Task<Quiz> GetQuizByIdForAdminAsync(string id)
        {
            var quiz = await _quizRepository.GetByIdAsync(id);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }
            return quiz;
        }

        public async Task<PagedResult<QuizStudentResponse>> GetAccessibleQuizzesForStudentAsync(string userId, string? courseId, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize)
        {
            List<Quiz> quizzes;

            if (!string.IsNullOrWhiteSpace(courseId))
            {
                if (!await _enrollmentRepository.HasVerifiedEnrollmentAsync(userId, courseId))
                {
                    throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
                }
                quizzes = await _quizRepository.GetByCourseIdsAsync(new List<string> { courseId });
            }
            else
            {
                var verifiedCourseIds = await _enrollmentRepository.GetVerifiedCourseIdsAsync(userId);
                quizzes = verifiedCourseIds.Count == 0
                    ? new List<Quiz>()
                    : await _quizRepository.GetByCourseIdsAsync(verifiedCourseIds);
            }

            var published = quizzes
                .Where(q => q.Status == Constants.QuizStatuses.Published && !q.IsExpired)
                .ToList();

            var filtered = TextSearchHelper.ApplyFilter(published, searchTerm, globalFilter, QuizSearchFields, DefaultQuizSearchFields);
            var accessible = filtered.Select(ToStudentResponse).ToList();

            return PagingHelper.ToPagedResult(accessible, pageNumber, pageSize);
        }

        public async Task<QuizStudentResponse> GetQuizByIdForStudentAsync(string id, string userId)
        {
            var quiz = await _quizRepository.GetByIdAsync(id);
            if (quiz == null || quiz.Status != Constants.QuizStatuses.Published || quiz.IsExpired)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (!await _enrollmentRepository.HasVerifiedEnrollmentAsync(userId, quiz.CourseId))
            {
                throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
            }

            return ToStudentResponse(quiz);
        }

        public async Task<QuizResultResponse> SubmitQuizAsync(string quizId, string userId, QuizSubmitRequest request)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (!await _enrollmentRepository.HasVerifiedEnrollmentAsync(userId, quiz.CourseId))
            {
                throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
            }

            if (quiz.Status != Constants.QuizStatuses.Published)
            {
                throw new InvalidOperationException(Constants.Messages.QuizNotPublished);
            }

            if (quiz.IsExpired)
            {
                throw new InvalidOperationException(Constants.Messages.QuizExpired);
            }

            var existingAttempt = await _quizAttemptRepository.GetByQuizUserAndVersionAsync(quizId, userId, quiz.PublishVersion);
            if (existingAttempt != null)
            {
                throw new InvalidOperationException(Constants.Messages.QuizAlreadyAttempted);
            }

            var answerLookup = request.Answers
                .Where(a => !string.IsNullOrWhiteSpace(a.SelectedOption))
                .ToDictionary(a => a.QuestionNumber, a => a.SelectedOption!.Trim().ToUpperInvariant());

            var correctCount = 0;
            var answers = new List<QuizAnswerItem>();

            foreach (var question in quiz.Questions)
            {
                answerLookup.TryGetValue(question.QuestionNumber, out var selected);
                var isValidOption = selected != null && Constants.QuizOptions.All.Contains(selected);
                var normalizedSelected = isValidOption ? selected : null;

                if (normalizedSelected != null && normalizedSelected == question.CorrectOption)
                {
                    correctCount++;
                }

                answers.Add(new QuizAnswerItem
                {
                    QuestionNumber = question.QuestionNumber,
                    SelectedOption = normalizedSelected
                });
            }

            var totalQuestions = quiz.Questions.Count;
            var attempt = new QuizAttempt
            {
                QuizId = quizId,
                QuizVersion = quiz.PublishVersion,
                QuestionsSnapshot = quiz.Questions,
                UserId = userId,
                Answers = answers,
                TotalQuestions = totalQuestions,
                CorrectCount = correctCount,
                WrongCount = totalQuestions - correctCount
            };

            await _quizAttemptRepository.CreateAsync(attempt);

            return ToResultResponse(attempt);
        }

        public async Task<QuizResultResponse> GetMyResultAsync(string quizId, string userId)
        {
            var attempt = await _quizAttemptRepository.GetByQuizAndUserAsync(quizId, userId);
            if (attempt == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotAttempted);
            }

            return ToResultResponse(attempt);
        }

        public async Task<List<RankListEntryDto>> GetRankListAsync(string quizId, string userId, bool isAdmin)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (!isAdmin && !await _enrollmentRepository.HasVerifiedEnrollmentAsync(userId, quiz.CourseId))
            {
                throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
            }

            var attempts = await _quizAttemptRepository.GetByQuizIdAsync(quizId);
            var ordered = attempts
                .Where(a => a.QuizVersion == quiz.PublishVersion)
                .OrderByDescending(a => a.CorrectCount)
                .ThenBy(a => a.SubmittedAt)
                .ToList();

            var rankList = new List<RankListEntryDto>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var attempt = ordered[i];
                var user = await _userRepository.GetByIdAsync(attempt.UserId);

                rankList.Add(new RankListEntryDto
                {
                    Rank = i + 1,
                    UserId = attempt.UserId,
                    FirstName = user?.FirstName ?? "Unknown",
                    LastName = user?.LastName ?? "",
                    District = user?.District,
                    ProfileImage = user?.ProfileImage ?? "",
                    CorrectCount = attempt.CorrectCount,
                    TotalQuestions = attempt.TotalQuestions,
                    Score = $"{attempt.CorrectCount}/{attempt.TotalQuestions}",
                    SubmittedAt = attempt.SubmittedAt
                });
            }

            return rankList;
        }

        public async Task<(byte[] Content, string FileName)> ExportRankListAsync(string quizId, string userId, bool isAdmin)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            var rankList = await GetRankListAsync(quizId, userId, isAdmin);
            var content = _excelExportService.GenerateRankListExcel(quiz.Title, rankList);

            var invalidChars = Path.GetInvalidFileNameChars();
            var safeCourseName = new string(quiz.CourseName.Select(c => invalidChars.Contains(c) || c == ' ' ? '_' : c).ToArray());
            var fileName = $"quiz-rank-list-{safeCourseName}.xlsx";

            return (content, fileName);
        }

        private static QuizStudentResponse ToStudentResponse(Quiz quiz)
        {
            return new QuizStudentResponse
            {
                Id = quiz.Id,
                CourseId = quiz.CourseId,
                CourseName = quiz.CourseName,
                Title = quiz.Title,
                PublishedAt = quiz.PublishedAt,
                ExpiresAt = quiz.ExpiresAt,
                IsExpired = quiz.IsExpired,
                TimeLeftSeconds = quiz.TimeLeftSeconds,
                Questions = quiz.Questions.Select(q => new QuizStudentQuestionDto
                {
                    QuestionNumber = q.QuestionNumber,
                    QuestionText = q.QuestionText,
                    QuestionImageUrl = q.QuestionImageUrl,
                    OptionA = q.OptionA,
                    OptionAImageUrl = q.OptionAImageUrl,
                    OptionB = q.OptionB,
                    OptionBImageUrl = q.OptionBImageUrl,
                    OptionC = q.OptionC,
                    OptionCImageUrl = q.OptionCImageUrl,
                    OptionD = q.OptionD,
                    OptionDImageUrl = q.OptionDImageUrl
                }).ToList()
            };
        }

        private static QuizResultResponse ToResultResponse(QuizAttempt attempt)
        {
            var answerByQuestion = attempt.Answers.ToDictionary(a => a.QuestionNumber, a => a.SelectedOption);

            var questions = attempt.QuestionsSnapshot
                .OrderBy(q => q.QuestionNumber)
                .Select(q =>
                {
                    answerByQuestion.TryGetValue(q.QuestionNumber, out var selected);
                    return new QuizResultQuestionDto
                    {
                        QuestionNumber = q.QuestionNumber,
                        QuestionText = q.QuestionText,
                        QuestionImageUrl = q.QuestionImageUrl,
                        OptionA = q.OptionA,
                        OptionAImageUrl = q.OptionAImageUrl,
                        OptionB = q.OptionB,
                        OptionBImageUrl = q.OptionBImageUrl,
                        OptionC = q.OptionC,
                        OptionCImageUrl = q.OptionCImageUrl,
                        OptionD = q.OptionD,
                        OptionDImageUrl = q.OptionDImageUrl,
                        SelectedOption = selected,
                        CorrectOption = q.CorrectOption,
                        IsCorrect = selected != null && selected == q.CorrectOption
                    };
                })
                .ToList();

            return new QuizResultResponse
            {
                QuizId = attempt.QuizId,
                TotalQuestions = attempt.TotalQuestions,
                CorrectAnswers = attempt.CorrectCount,
                WrongAnswers = attempt.WrongCount,
                Score = $"{attempt.CorrectCount}/{attempt.TotalQuestions}",
                SubmittedAt = attempt.SubmittedAt,
                Questions = questions
            };
        }
    }
}
