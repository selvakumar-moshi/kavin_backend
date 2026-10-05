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
        private readonly IBatchRepository _batchRepository;

        public QuizService(
            IQuizRepository quizRepository,
            IQuizAttemptRepository quizAttemptRepository,
            ICourseRepository courseRepository,
            IEnrollmentRepository enrollmentRepository,
            IUserRepository userRepository,
            IFileStorageService fileStorageService,
            IExcelExportService excelExportService,
            IBatchRepository batchRepository)
        {
            _batchRepository = batchRepository;
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

            if (request.File != null && request.File.Length > 0)
            {
                if (request.Questions != null && request.Questions.Count > 0)
                {
                    throw new InvalidOperationException("Pass either questions or a file, not both");
                }

                var parsed = ParseDocx(request.File);
                if (parsed.Questions.Count == 0)
                {
                    var first = parsed.Skipped.First();
                    throw new InvalidOperationException($"None of the {parsed.TotalFound} questions could be read. Question {first.DocumentNumber}: {first.Reason}");
                }

                request.Questions = parsed.Questions.Select(q => new QuizQuestionInput
                {
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectOption = q.CorrectOption,
                    Mark = 1
                }).ToList();

                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    request.Title = Path.GetFileNameWithoutExtension(request.File.FileName);
                }
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title is required");
            }

            if (request.Questions == null || request.Questions.Count == 0)
            {
                throw new InvalidOperationException("At least one question is required");
            }

            Batch? batch = null;
            if (!string.IsNullOrWhiteSpace(request.BatchId))
            {
                batch = await _batchRepository.GetByIdAsync(request.BatchId);
                if (batch == null)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                }
                if (batch.CourseId != course.Id)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
                }
            }

            var quiz = new Quiz
            {
                CourseId = course.Id,
                CourseName = course.CourseName,
                BatchId = batch?.Id,
                BatchTitle = batch?.Title,
                Title = request.Title.Trim(),
                QuizToView = string.IsNullOrWhiteSpace(request.QuizToView)
                    ? Constants.MaterialAccess.Paid
                    : Constants.MaterialAccess.Normalize(request.QuizToView),
                Status = Constants.QuizStatuses.Draft,
                Questions = await MapQuestionsAsync(request.Questions, null),
                CreatedAt = DateTime.UtcNow
            };

            return await _quizRepository.CreateAsync(quiz);
        }

        // Reads the questions out of an uploaded Word file. Saves nothing.
        private static DocxQuizParseResult ParseDocx(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException("A .docx file is required");
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only .docx (Word) files are supported - re-save older .doc files as .docx");
            }

            DocxQuizParseResult parsed;
            try
            {
                using var stream = file.OpenReadStream();
                parsed = DocxQuizParser.Parse(stream);
            }
            catch (InvalidDataException)
            {
                throw new InvalidOperationException("This is not a valid .docx file");
            }

            if (parsed.TotalFound == 0)
            {
                throw new InvalidOperationException("No questions found. Each question must start on a new line with its number, e.g. \"1. Question text\", followed by A) B) C) D) options with the correct one marked ✔");
            }

            return parsed;
        }

        public Task<QuizImportPreviewResponse> PreviewDocxQuizAsync(IFormFile? file)
        {
            var parsed = ParseDocx(file);

            return Task.FromResult(new QuizImportPreviewResponse
            {
                TotalFoundInDocument = parsed.TotalFound,
                Readable = parsed.Questions.Count,
                Questions = parsed.Questions.Select(q => new QuizImportQuestionDto
                {
                    DocumentNumber = q.DocumentNumber,
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectOption = q.CorrectOption
                }).ToList(),
                Skipped = parsed.Skipped.Select(i => new QuizImportIssueDto { DocumentNumber = i.DocumentNumber, Reason = i.Reason }).ToList(),
                Warnings = parsed.Warnings.Select(i => new QuizImportIssueDto { DocumentNumber = i.DocumentNumber, Reason = i.Reason }).ToList()
            });
        }

        public async Task<Quiz> CopyQuizAsync(QuizCopyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.QuizId))
            {
                throw new InvalidOperationException("Quiz is required");
            }

            if (string.IsNullOrWhiteSpace(request.BatchId))
            {
                throw new InvalidOperationException("Batch is required");
            }

            var source = await _quizRepository.GetByIdAsync(request.QuizId);
            if (source == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            var batch = await _batchRepository.GetByIdAsync(request.BatchId);
            if (batch == null)
            {
                throw new InvalidOperationException(Constants.Messages.BatchNotFound);
            }
            if (batch.CourseId != source.CourseId)
            {
                throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
            }

            // Each question image is copied in S3 so the two quizzes never share a file -
            // editing or deleting one quiz's image must not break the other.
            var questions = new List<QuizQuestionItem>();
            foreach (var q in source.Questions)
            {
                var copy = new QuizQuestionItem
                {
                    QuestionNumber = q.QuestionNumber,
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectOption = q.CorrectOption,
                    Mark = q.Mark
                };

                (copy.QuestionImageUrl, copy.QuestionImageKey) = await CopyImageAsync(q.QuestionImageKey);
                (copy.OptionAImageUrl, copy.OptionAImageKey) = await CopyImageAsync(q.OptionAImageKey);
                (copy.OptionBImageUrl, copy.OptionBImageKey) = await CopyImageAsync(q.OptionBImageKey);
                (copy.OptionCImageUrl, copy.OptionCImageKey) = await CopyImageAsync(q.OptionCImageKey);
                (copy.OptionDImageUrl, copy.OptionDImageKey) = await CopyImageAsync(q.OptionDImageKey);

                questions.Add(copy);
            }

            var quiz = new Quiz
            {
                CourseId = source.CourseId,
                CourseName = source.CourseName,
                BatchId = batch.Id,
                BatchTitle = batch.Title,
                Title = string.IsNullOrWhiteSpace(request.Title) ? source.Title : request.Title.Trim(),
                QuizToView = source.QuizToView,
                Status = Constants.QuizStatuses.Draft,
                Questions = questions,
                CreatedAt = DateTime.UtcNow
            };

            return await _quizRepository.CreateAsync(quiz);
        }

        private async Task<(string? Url, string? Key)> CopyImageAsync(string? sourceKey)
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                return (null, null);
            }

            return await _fileStorageService.CopyAsync(sourceKey, QuestionImageFolder);
        }

        public async Task<Quiz> UpdateQuizAsync(string id, QuizUpdateRequest request)
        {
            var quiz = await _quizRepository.GetByIdAsync(id);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (!string.IsNullOrWhiteSpace(request.BatchId))
            {
                var batch = await _batchRepository.GetByIdAsync(request.BatchId);
                if (batch == null)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                }
                if (batch.CourseId != quiz.CourseId)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
                }
                quiz.BatchId = batch.Id;
                quiz.BatchTitle = batch.Title;
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                quiz.Title = request.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.QuizToView))
            {
                quiz.QuizToView = Constants.MaterialAccess.Normalize(request.QuizToView);
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

                if (q.Mark.HasValue && q.Mark.Value <= 0)
                {
                    throw new InvalidOperationException(Constants.Messages.InvalidMark);
                }

                if (string.Equals(q.CorrectOption, "D", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(q.OptionD) && q.OptionDImage == null)
                {
                    throw new InvalidOperationException("Option D cannot be the correct answer when it is empty");
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
                    OptionD = (q.OptionD ?? "").Trim(),
                    CorrectOption = string.IsNullOrWhiteSpace(q.CorrectOption) ? null : q.CorrectOption.ToUpperInvariant(),
                    Mark = q.Mark ?? 1
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

        public async Task<Quiz> PublishQuizAsync(string id, DateTime expiresAt, bool shuffleQuestions = true)
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
                string.IsNullOrWhiteSpace(q.CorrectOption) ||
                // Option D is optional, but it can't be the correct answer if it's empty
                (q.CorrectOption == "D" && string.IsNullOrWhiteSpace(q.OptionD) && string.IsNullOrWhiteSpace(q.OptionDImageUrl)));

            if (incomplete)
            {
                throw new InvalidOperationException(Constants.Messages.QuizIncomplete);
            }

            quiz.Status = Constants.QuizStatuses.Published;
            quiz.PublishedAt = DateTime.UtcNow;
            quiz.ExpiresAt = expiresAt;
            quiz.PublishVersion += 1;
            quiz.ShuffleQuestions = shuffleQuestions;

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

            // Students' submitted attempts are kept (their saved results still show the question
            // images), so the images are only removed from S3 when nobody has attempted the quiz.
            var hasAttempts = (await _quizAttemptRepository.GetByQuizIdAsync(id)).Count > 0;

            var deleted = await _quizRepository.DeleteAsync(id);
            if (deleted && !hasAttempts)
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
            ["coursename"] = q => q.CourseName,
            ["status"] = q => q.Status
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
            var quizzes = string.IsNullOrWhiteSpace(courseId)
                ? await _quizRepository.GetAllAsync()
                : await _quizRepository.GetByCourseIdsAsync(new List<string> { courseId });

            var verifiedEnrollments = await GetVerifiedEnrollmentsAsync(userId);

            // A Free quiz is visible to every student regardless of course purchase; a Paid quiz
            // requires a verified enrollment in that course (and in that batch, if the quiz has one).
            var published = quizzes
                .Where(q => q.Status == Constants.QuizStatuses.Published && !q.IsExpired)
                .Where(q => CanAccess(q, verifiedEnrollments))
                .ToList();

            var filtered = TextSearchHelper.ApplyFilter(published, searchTerm, globalFilter, QuizSearchFields, DefaultQuizSearchFields);
            var accessible = filtered.Select(q => ToStudentResponse(q, userId)).ToList();

            return PagingHelper.ToPagedResult(accessible, pageNumber, pageSize);
        }

        public async Task<QuizStudentResponse> GetQuizByIdForStudentAsync(string id, string userId)
        {
            var quiz = await _quizRepository.GetByIdAsync(id);
            if (quiz == null || quiz.Status != Constants.QuizStatuses.Published || quiz.IsExpired)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (!await CanAccessAsync(quiz, userId))
            {
                throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
            }

            return ToStudentResponse(quiz, userId);
        }

        public async Task<QuizResultResponse> SubmitQuizAsync(string quizId, string userId, QuizSubmitRequest request)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (!await CanAccessAsync(quiz, userId))
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
            var totalMarks = 0m;
            var scoredMarks = 0m;
            var answers = new List<QuizAnswerItem>();

            foreach (var question in quiz.Questions)
            {
                totalMarks += question.Mark;

                answerLookup.TryGetValue(question.QuestionNumber, out var selected);
                var isValidOption = selected != null && Constants.QuizOptions.All.Contains(selected);
                var normalizedSelected = isValidOption ? selected : null;

                if (normalizedSelected != null && normalizedSelected == question.CorrectOption)
                {
                    correctCount++;
                    scoredMarks += question.Mark;
                }

                answers.Add(new QuizAnswerItem
                {
                    QuestionNumber = question.QuestionNumber,
                    SelectedOption = normalizedSelected
                });
            }

            // Remember which batch the student attempted from (paid quizzes only)
            Enrollment? attemptEnrollment = null;
            if (quiz.QuizToView != Constants.MaterialAccess.Free)
            {
                attemptEnrollment = (await GetVerifiedEnrollmentsAsync(userId)).FirstOrDefault(e =>
                    e.CourseId == quiz.CourseId &&
                    (string.IsNullOrWhiteSpace(quiz.BatchId) || quiz.BatchId == e.BatchId));
            }

            var totalQuestions = quiz.Questions.Count;
            var attempt = new QuizAttempt
            {
                QuizId = quizId,
                BatchId = attemptEnrollment?.BatchId,
                BatchTitle = attemptEnrollment?.BatchTitle,
                QuizVersion = quiz.PublishVersion,
                QuestionsSnapshot = quiz.Questions,
                UserId = userId,
                Answers = answers,
                TotalQuestions = totalQuestions,
                CorrectCount = correctCount,
                WrongCount = totalQuestions - correctCount,
                TotalMarks = totalMarks,
                ScoredMarks = scoredMarks
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

        public async Task<List<RankListEntryDto>> GetRankListAsync(string quizId, string userId, bool isAdmin, string? batchId = null)
        {
            var (_, rankList, _) = await BuildRankListAsync(quizId, userId, isAdmin, batchId);
            return rankList;
        }

        // Rank lists are kept per batch: each attempt remembers the batch the student was in when
        // they submitted, so moving a quiz to another batch doesn't mix or hide earlier results.
        // Admin: shows the quiz's own batch by default, any other batch via batchId, or every batch
        // (each ranked separately) with batchId=all.
        // Student: always their own batch.
        private async Task<(Quiz Quiz, List<RankListEntryDto> RankList, string? BatchTitle)> BuildRankListAsync(
            string quizId, string userId, bool isAdmin, string? batchId)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            if (quiz.QuizToView == Constants.MaterialAccess.Free)
            {
                throw new InvalidOperationException(Constants.Messages.RankListNotForFreeQuiz);
            }

            string? targetBatchId;
            if (isAdmin)
            {
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    // Default: the quiz's own batch (a quiz with no batch covers the whole course)
                    targetBatchId = quiz.BatchId;
                }
                else
                {
                    targetBatchId = batchId.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : batchId;
                }
            }
            else
            {
                var mine = await GetVerifiedEnrollmentsAsync(userId);
                var own = mine.FirstOrDefault(e => e.CourseId == quiz.CourseId &&
                    (string.IsNullOrWhiteSpace(quiz.BatchId) || e.BatchId == quiz.BatchId));
                if (own == null)
                {
                    throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
                }
                targetBatchId = own.BatchId;
            }

            // Only students who currently hold a verified (paid) enrollment in the batch they
            // attempted from are ranked.
            var candidates = new List<(QuizAttempt Attempt, string? BatchId, string? BatchTitle)>();
            foreach (var attempt in await _quizAttemptRepository.GetByQuizIdAsync(quizId))
            {
                var enrollments = (await GetVerifiedEnrollmentsAsync(attempt.UserId))
                    .Where(e => e.CourseId == quiz.CourseId)
                    .ToList();

                // Attempts saved before batches were recorded fall back to the student's enrollment
                var enrollment = attempt.BatchId != null
                    ? enrollments.FirstOrDefault(e => e.BatchId == attempt.BatchId)
                    : enrollments.FirstOrDefault();
                if (enrollment == null)
                {
                    continue;
                }

                var attemptBatchId = attempt.BatchId ?? enrollment.BatchId;
                if (targetBatchId != null && attemptBatchId != targetBatchId)
                {
                    continue;
                }

                candidates.Add((attempt, attemptBatchId, attempt.BatchTitle ?? enrollment.BatchTitle));
            }

            var rankList = new List<RankListEntryDto>();
            foreach (var group in candidates.GroupBy(c => c.BatchId).OrderBy(g => g.First().BatchTitle))
            {
                // If the quiz was republished while this batch had it, rank only the latest version
                var latestVersion = group.Max(c => c.Attempt.QuizVersion);
                var ordered = group
                    .Where(c => c.Attempt.QuizVersion == latestVersion)
                    .OrderByDescending(c => c.Attempt.ScoredMarks)
                    .ThenBy(c => c.Attempt.SubmittedAt)
                    .ToList();

                for (var i = 0; i < ordered.Count; i++)
                {
                    var (attempt, attemptBatchId, attemptBatchTitle) = ordered[i];
                    var user = await _userRepository.GetByIdAsync(attempt.UserId);

                    rankList.Add(new RankListEntryDto
                    {
                        Rank = i + 1,
                        UserId = attempt.UserId,
                        FirstName = user?.FirstName ?? "Unknown",
                        LastName = user?.LastName ?? "",
                        District = user?.District,
                        BatchId = attemptBatchId,
                        BatchTitle = attemptBatchTitle,
                        ProfileImage = user?.ProfileImage ?? "",
                        CorrectCount = attempt.CorrectCount,
                        TotalQuestions = attempt.TotalQuestions,
                        ScoredMarks = attempt.ScoredMarks,
                        TotalMarks = attempt.TotalMarks,
                        Score = $"{attempt.ScoredMarks}/{attempt.TotalMarks}",
                        SubmittedAt = attempt.SubmittedAt
                    });
                }
            }

            string? batchTitle = null;
            if (targetBatchId != null)
            {
                batchTitle = rankList.FirstOrDefault()?.BatchTitle
                    ?? (await _batchRepository.GetByIdAsync(targetBatchId))?.Title;
            }

            return (quiz, rankList, batchTitle);
        }

        public async Task<(byte[] Content, string FileName)> ExportRankListAsync(string quizId, string userId, bool isAdmin, string? batchId = null)
        {
            var (quiz, rankList, batchTitle) = await BuildRankListAsync(quizId, userId, isAdmin, batchId);
            var content = _excelExportService.GenerateRankListExcel(quiz.Title, batchTitle, rankList);

            var invalidChars = Path.GetInvalidFileNameChars();
            string Safe(string value) => new string(value.Select(c => invalidChars.Contains(c) || c == ' ' ? '_' : c).ToArray());
            var fileName = string.IsNullOrWhiteSpace(batchTitle)
                ? $"quiz-rank-list-{Safe(quiz.CourseName)}.xlsx"
                : $"quiz-rank-list-{Safe(quiz.CourseName)}-{Safe(batchTitle)}.xlsx";

            return (content, fileName);
        }

        private async Task<List<Enrollment>> GetVerifiedEnrollmentsAsync(string userId)
        {
            var enrollments = await _enrollmentRepository.GetByUserIdAsync(userId);
            return enrollments.Where(e => e.Status == Constants.EnrollmentStatuses.Verified).ToList();
        }

        private async Task<bool> CanAccessAsync(Quiz quiz, string userId)
        {
            if (quiz.QuizToView == Constants.MaterialAccess.Free)
            {
                return true;
            }
            return CanAccess(quiz, await GetVerifiedEnrollmentsAsync(userId));
        }

        // Free quizzes are open to everyone. Paid quizzes need a verified enrollment in the quiz's
        // course, and when the quiz is tied to a batch, in that same batch.
        private static bool CanAccess(Quiz quiz, List<Enrollment> verifiedEnrollments)
        {
            if (quiz.QuizToView == Constants.MaterialAccess.Free)
            {
                return true;
            }
            return verifiedEnrollments.Any(e =>
                e.CourseId == quiz.CourseId &&
                (string.IsNullOrWhiteSpace(quiz.BatchId) || quiz.BatchId == e.BatchId));
        }

        // A quiz published with shuffling shows every student the same questions in their own order.
        // The order is derived from (quiz, publish version, student), so it never changes between
        // requests for the same student, but differs between students. Answers are still submitted by
        // the question's fixed QuestionNumber, so scoring and rank lists are unaffected.
        private static List<QuizQuestionItem> OrderForStudent(Quiz quiz, string userId)
        {
            var questions = quiz.Questions.OrderBy(q => q.QuestionNumber).ToList();
            if (!quiz.ShuffleQuestions || questions.Count < 2)
            {
                return questions;
            }

            var hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{quiz.Id}|{quiz.PublishVersion}|{userId}"));
            var random = new Random(BitConverter.ToInt32(hash, 0));

            for (var i = questions.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (questions[i], questions[j]) = (questions[j], questions[i]);
            }

            return questions;
        }

        private static QuizStudentResponse ToStudentResponse(Quiz quiz, string userId)
        {
            return new QuizStudentResponse
            {
                Id = quiz.Id,
                CourseId = quiz.CourseId,
                CourseName = quiz.CourseName,
                Title = quiz.Title,
                QuizToView = quiz.QuizToView,
                PublishedAt = quiz.PublishedAt,
                ExpiresAt = quiz.ExpiresAt,
                IsExpired = quiz.IsExpired,
                TimeLeftSeconds = quiz.TimeLeftSeconds,
                Questions = OrderForStudent(quiz, userId).Select((q, position) => new QuizStudentQuestionDto
                {
                    DisplayNumber = position + 1,
                    QuestionNumber = q.QuestionNumber,
                    QuestionText = q.QuestionText,
                    QuestionImageUrl = q.QuestionImageUrl,
                    OptionA = q.OptionA,
                    OptionAImageUrl = q.OptionAImageUrl,
                    OptionB = q.OptionB,
                    OptionBImageUrl = q.OptionBImageUrl,
                    OptionC = q.OptionC,
                    OptionCImageUrl = q.OptionCImageUrl,
                    OptionD = string.IsNullOrWhiteSpace(q.OptionD) ? null : q.OptionD,
                    OptionDImageUrl = q.OptionDImageUrl,
                    Mark = q.Mark
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
                        OptionD = string.IsNullOrWhiteSpace(q.OptionD) ? null : q.OptionD,
                        OptionDImageUrl = q.OptionDImageUrl,
                        Mark = q.Mark,
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
                TotalMarks = attempt.TotalMarks,
                ScoredMarks = attempt.ScoredMarks,
                Score = $"{attempt.ScoredMarks}/{attempt.TotalMarks}",
                SubmittedAt = attempt.SubmittedAt,
                Questions = questions
            };
        }
    }
}
