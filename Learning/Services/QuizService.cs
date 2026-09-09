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

        public QuizService(
            IQuizRepository quizRepository,
            IQuizAttemptRepository quizAttemptRepository,
            ICourseRepository courseRepository,
            IEnrollmentRepository enrollmentRepository,
            IUserRepository userRepository)
        {
            _quizRepository = quizRepository;
            _quizAttemptRepository = quizAttemptRepository;
            _courseRepository = courseRepository;
            _enrollmentRepository = enrollmentRepository;
            _userRepository = userRepository;
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

            var quiz = new Quiz
            {
                CourseId = course.Id,
                CourseName = course.CourseName,
                Title = request.Title.Trim(),
                Status = Constants.QuizStatuses.Draft,
                Questions = MapQuestions(request.Questions),
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

            if (quiz.Status != Constants.QuizStatuses.Draft)
            {
                throw new InvalidOperationException(Constants.Messages.QuizNotDraft);
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                quiz.Title = request.Title.Trim();
            }

            if (request.Questions != null)
            {
                quiz.Questions = MapQuestions(request.Questions);
            }

            await _quizRepository.UpdateAsync(id, quiz);
            return quiz;
        }

        private static List<QuizQuestionItem> MapQuestions(List<QuizQuestionInput> questions)
        {
            foreach (var q in questions)
            {
                if (!string.IsNullOrWhiteSpace(q.CorrectOption) &&
                    !Constants.QuizOptions.All.Contains(q.CorrectOption.ToUpperInvariant()))
                {
                    throw new InvalidOperationException(Constants.Messages.InvalidOption);
                }
            }

            return questions.Select((q, index) => new QuizQuestionItem
            {
                QuestionNumber = index + 1,
                QuestionText = q.QuestionText.Trim(),
                OptionA = q.OptionA.Trim(),
                OptionB = q.OptionB.Trim(),
                OptionC = q.OptionC.Trim(),
                OptionD = q.OptionD.Trim(),
                CorrectOption = string.IsNullOrWhiteSpace(q.CorrectOption) ? null : q.CorrectOption.ToUpperInvariant()
            }).ToList();
        }

        public async Task<Quiz> PublishQuizAsync(string id)
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

            quiz.Status = Constants.QuizStatuses.Published;
            quiz.PublishedAt = DateTime.UtcNow;

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

            return await _quizRepository.DeleteAsync(id);
        }

        public async Task<PagedResult<Quiz>> GetAllQuizzesForAdminAsync(int pageNumber, int pageSize)
        {
            var quizzes = await _quizRepository.GetAllAsync();
            return PagingHelper.ToPagedResult(quizzes, pageNumber, pageSize);
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

        public async Task<PagedResult<QuizStudentResponse>> GetAccessibleQuizzesForStudentAsync(string userId, string? courseId, int pageNumber, int pageSize)
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

            var accessible = quizzes
                .Where(q => q.Status == Constants.QuizStatuses.Published && !q.IsExpired)
                .Select(ToStudentResponse)
                .ToList();

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

            var existingAttempt = await _quizAttemptRepository.GetByQuizAndUserAsync(quizId, userId);
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
                UserId = userId,
                Answers = answers,
                TotalQuestions = totalQuestions,
                CorrectCount = correctCount,
                WrongCount = totalQuestions - correctCount
            };

            await _quizAttemptRepository.CreateAsync(attempt);

            return ToResultResponse(attempt, quiz);
        }

        public async Task<QuizResultResponse> GetMyResultAsync(string quizId, string userId)
        {
            var attempt = await _quizAttemptRepository.GetByQuizAndUserAsync(quizId, userId);
            if (attempt == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotAttempted);
            }

            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            return ToResultResponse(attempt, quiz);
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
                    ProfileImage = user?.ProfileImage ?? "",
                    CorrectCount = attempt.CorrectCount,
                    TotalQuestions = attempt.TotalQuestions,
                    Score = $"{attempt.CorrectCount}/{attempt.TotalQuestions}",
                    SubmittedAt = attempt.SubmittedAt
                });
            }

            return rankList;
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
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD
                }).ToList()
            };
        }

        private static QuizResultResponse ToResultResponse(QuizAttempt attempt, Quiz quiz)
        {
            var answerByQuestion = attempt.Answers.ToDictionary(a => a.QuestionNumber, a => a.SelectedOption);

            var questions = quiz.Questions
                .OrderBy(q => q.QuestionNumber)
                .Select(q =>
                {
                    answerByQuestion.TryGetValue(q.QuestionNumber, out var selected);
                    return new QuizResultQuestionDto
                    {
                        QuestionNumber = q.QuestionNumber,
                        QuestionText = q.QuestionText,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
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
