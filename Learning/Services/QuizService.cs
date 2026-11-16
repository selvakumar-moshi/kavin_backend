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
        private readonly IFolderService _folderService;
        private readonly ISyllabusWiseService _syllabusWiseService;

        public QuizService(
            IQuizRepository quizRepository,
            IQuizAttemptRepository quizAttemptRepository,
            ICourseRepository courseRepository,
            IEnrollmentRepository enrollmentRepository,
            IUserRepository userRepository,
            IFileStorageService fileStorageService,
            IExcelExportService excelExportService,
            IBatchRepository batchRepository,
            IFolderService folderService,
            ISyllabusWiseService syllabusWiseService)
        {
            _syllabusWiseService = syllabusWiseService;
            _folderService = folderService;
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
            var quizType = Constants.QuizTypes.Normalize(request.QuizType)
                ?? throw new InvalidOperationException("Quiz type is required: competitive, school, previousYear, syllabusWise, daily or currentAffairs");
            var quizAttempt = ResolveAttemptLimit(request.QuizAttempt, 1);

            // A daily quiz is always Free, whatever quizToView says
            var quizToView = Constants.QuizTypes.IsAlwaysFree(quizType)
                ? Constants.MaterialAccess.Free
                : string.IsNullOrWhiteSpace(request.QuizToView)
                    ? Constants.MaterialAccess.Paid
                    : Constants.MaterialAccess.Normalize(request.QuizToView);
            var isFree = quizToView == Constants.MaterialAccess.Free;

            // Course and batch are mandatory for a Paid quiz (they decide who can attempt it) and
            // optional for a Free quiz (open to every student). If a Free quiz still gets them, they're validated.
            // A Paid syllabusWise quiz needs neither: it is unlocked by the student's syllabus wise payment.
            var needsCourse = !isFree && quizType != Constants.QuizTypes.SyllabusWise;
            Course? course = null;
            if (!string.IsNullOrWhiteSpace(request.CourseId))
            {
                course = await _courseRepository.GetByIdAsync(request.CourseId);
                if (course == null)
                {
                    throw new InvalidOperationException("Course not found");
                }
            }
            else if (needsCourse)
            {
                throw new InvalidOperationException("Course is required for a Paid quiz");
            }

            if (needsCourse && string.IsNullOrWhiteSpace(request.BatchId))
            {
                throw new InvalidOperationException("Batch is required for a Paid quiz");
            }

            if (!string.IsNullOrWhiteSpace(request.BatchId) && course == null)
            {
                throw new InvalidOperationException("Course is required when a batch is selected");
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

            async Task<Batch?> LoadBatchAsync()
            {
                if (string.IsNullOrWhiteSpace(request.BatchId))
                {
                    return null;
                }
                var found = await _batchRepository.GetByIdAsync(request.BatchId)
                    ?? throw new InvalidOperationException(Constants.Messages.BatchNotFound);
                if (found.CourseId != course!.Id)
                {
                    throw new InvalidOperationException(Constants.Messages.BatchCourseMismatch);
                }
                return found;
            }

            // The Word file the questions came from goes to S3 while the batch and folder lookups run, instead of after them.
            // If anything is rejected, the upload is removed again so a failed request never leaves a file behind.
            var sourceFile = request.SourceFile is { Length: > 0 } ? request.SourceFile : request.File;
            var uploadTask = UploadSourceFileAsync(sourceFile);

            Batch? batch;
            FolderSelection folder;
            (string Name, string Url, string Key)? source;
            try
            {
                var batchTask = LoadBatchAsync();
                var folderTask = ValidateFolderForTypeAsync(quizType, request.FolderId, request.SubFolderId);
                await Task.WhenAll(batchTask, folderTask, uploadTask);
                batch = batchTask.Result;
                folder = folderTask.Result;
                source = uploadTask.Result;
            }
            catch
            {
                try
                {
                    var uploaded = await uploadTask;
                    if (uploaded != null)
                    {
                        await _fileStorageService.DeleteAsync(uploaded.Value.Key);
                    }
                }
                catch
                {
                    // The upload itself failed, so there is nothing to clean up
                }
                throw;
            }

            var quiz = new Quiz
            {
                CourseId = course?.Id,
                CourseName = course?.CourseName,
                BatchId = batch?.Id,
                BatchTitle = batch?.Title,
                QuizType = quizType,
                FolderId = folder.FolderId,
                FolderName = folder.FolderName,
                SubFolderId = folder.SubFolderId,
                SubFolderName = folder.SubFolderName,
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                SourceFileName = source?.Name,
                SourceFileUrl = source?.Url,
                SourceFileKey = source?.Key,
                QuizAttempt = quizAttempt,
                QuizToView = quizToView,
                Status = Constants.QuizStatuses.Draft,
                Questions = await MapQuestionsAsync(request.Questions, null),
                CreatedAt = DateTime.UtcNow
            };

            return await _quizRepository.CreateAsync(quiz);
        }

        private const string SourceDocumentFolder = "coaching/question/document";

        // Saves an imported Word file in S3. No file (null or empty) means nothing to keep. The stored name gets a unique prefix
        // so two quizzes imported from the same "TNPSC.docx" never overwrite each other; SourceFileName keeps the original name.
        private async Task<(string Name, string Url, string Key)?> UploadSourceFileAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var name = Path.GetFileName(file.FileName);
            if (!string.Equals(Path.GetExtension(name), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only .docx (Word) files are supported - re-save older .doc files as .docx");
            }

            var (url, key) = await _fileStorageService.UploadDocxAsync(file, SourceDocumentFolder, $"{name}");
            return (name, url, key);
        }

        // quizAttempt must be 1 or more; when it isn't passed the fallback applies
        private static int ResolveAttemptLimit(int? requested, int fallback)
        {
            if (requested == null)
            {
                return fallback;
            }
            if (requested < 1)
            {
                throw new InvalidOperationException("quizAttempt must be at least 1");
            }
            return requested.Value;
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
                FileName = Path.GetFileName(file!.FileName),
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

            var source = await _quizRepository.GetByIdAsync(request.QuizId);
            if (source == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            // The copy can be Paid or Free (defaults to the original's type):
            //  - Paid: a batch is required (and a course - the original's, or request.CourseId if it has none)
            //  - Free: no batch needed; one is still validated and kept if it is passed
            var quizToView = Constants.QuizTypes.IsAlwaysFree(source.QuizType)
                ? Constants.MaterialAccess.Free
                : string.IsNullOrWhiteSpace(request.QuizToView)
                    ? source.QuizToView
                    : Constants.MaterialAccess.Normalize(request.QuizToView);
            var isFree = quizToView == Constants.MaterialAccess.Free;
            var quizAttempt = ResolveAttemptLimit(request.QuizAttempt, source.QuizAttempt ?? 1);

            Course? course = null;
            var courseId = !string.IsNullOrWhiteSpace(source.CourseId) ? source.CourseId : request.CourseId;
            if (!string.IsNullOrWhiteSpace(courseId))
            {
                course = await _courseRepository.GetByIdAsync(courseId);
                if (course == null)
                {
                    throw new InvalidOperationException(Constants.Messages.CourseNotFound);
                }
            }
            else if (!isFree)
            {
                throw new InvalidOperationException("The original quiz has no course - pass courseId to copy it as a Paid quiz");
            }

            Batch? batch = null;
            if (!string.IsNullOrWhiteSpace(request.BatchId))
            {
                if (course == null)
                {
                    throw new InvalidOperationException("Course is required when a batch is selected");
                }

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
            else if (!isFree)
            {
                throw new InvalidOperationException("Batch is required for a Paid quiz");
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

            // The Word file is copied too, so deleting or replacing it on one quiz never affects the other
            var (sourceFileUrl, sourceFileKey) = await CopyFileAsync(source.SourceFileKey, SourceDocumentFolder);

            var quiz = new Quiz
            {
                CourseId = course?.Id,
                CourseName = course?.CourseName,
                BatchId = batch?.Id,
                BatchTitle = batch?.Title,
                QuizType = source.QuizType,
                FolderId = source.FolderId,
                FolderName = source.FolderName,
                SubFolderId = source.SubFolderId,
                SubFolderName = source.SubFolderName,
                Title = string.IsNullOrWhiteSpace(request.Title) ? source.Title : request.Title.Trim(),
                Description = source.Description,
                SourceFileName = source.SourceFileName,
                SourceFileUrl = sourceFileUrl,
                SourceFileKey = sourceFileKey,
                QuizAttempt = quizAttempt,
                QuizToView = quizToView,
                Status = Constants.QuizStatuses.Draft,
                Questions = questions,
                CreatedAt = DateTime.UtcNow
            };

            return await _quizRepository.CreateAsync(quiz);
        }

        private Task<(string? Url, string? Key)> CopyImageAsync(string? sourceKey)
            => CopyFileAsync(sourceKey, QuestionImageFolder);

        private async Task<(string? Url, string? Key)> CopyFileAsync(string? sourceKey, string folder)
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                return (null, null);
            }

            return await _fileStorageService.CopyAsync(sourceKey, folder);
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

            if (!string.IsNullOrWhiteSpace(request.QuizType))
            {
                quiz.QuizType = Constants.QuizTypes.Normalize(request.QuizType)
                    ?? throw new InvalidOperationException("Quiz type must be competitive, school, previousYear, syllabusWise, daily or currentAffairs");
            }

            // Folder: only touched when the request changes the folder or the quiz type, so editing e.g. just the
            // title of an older previousYear quiz that has no folder still works
            if (request.FolderId != null || !string.IsNullOrWhiteSpace(request.QuizType))
            {
                var keepFolder = request.FolderId == null && Constants.QuizTypes.UsesFolders(quiz.QuizType) && quiz.FolderId != null;
                var folder = keepFolder
                    ? new FolderSelection { FolderId = quiz.FolderId, FolderName = quiz.FolderName, SubFolderId = quiz.SubFolderId, SubFolderName = quiz.SubFolderName }
                    : await ValidateFolderForTypeAsync(quiz.QuizType, request.FolderId, request.SubFolderId);
                quiz.FolderId = folder.FolderId;
                quiz.FolderName = folder.FolderName;
                quiz.SubFolderId = folder.SubFolderId;
                quiz.SubFolderName = folder.SubFolderName;
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                quiz.Title = request.Title.Trim();
            }

            if (request.Description != null)
            {
                quiz.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.QuizToView))
            {
                quiz.QuizToView = Constants.MaterialAccess.Normalize(request.QuizToView);
            }

            // A daily quiz is always Free
            if (Constants.QuizTypes.IsAlwaysFree(quiz.QuizType))
            {
                quiz.QuizToView = Constants.MaterialAccess.Free;
            }

            if (request.QuizAttempt != null)
            {
                quiz.QuizAttempt = ResolveAttemptLimit(request.QuizAttempt, 1);
            }

            if (quiz.QuizToView != Constants.MaterialAccess.Free && string.IsNullOrWhiteSpace(quiz.CourseId)
                && quiz.QuizType != Constants.QuizTypes.SyllabusWise)
            {
                throw new InvalidOperationException("This quiz has no course, so it can only be Free - create a new Paid quiz with a course and batch instead");
            }

            if (request.Questions != null)
            {
                quiz.Questions = await MapQuestionsAsync(request.Questions, quiz.Questions);
            }

            // Word file: a new one replaces the saved one; removeSourceFile clears it. The old S3 object is deleted
            // only after the quiz is saved, so a failed update never loses the file it still points to.
            string? oldSourceKey = null;
            var newSource = await UploadSourceFileAsync(request.SourceFile);
            if (newSource != null)
            {
                oldSourceKey = quiz.SourceFileKey;
                (quiz.SourceFileName, quiz.SourceFileUrl, quiz.SourceFileKey) = newSource.Value;
            }
            else if (request.RemoveSourceFile == true)
            {
                oldSourceKey = quiz.SourceFileKey;
                quiz.SourceFileName = null;
                quiz.SourceFileUrl = null;
                quiz.SourceFileKey = null;
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
            if (!string.IsNullOrWhiteSpace(oldSourceKey))
            {
                await _fileStorageService.DeleteAsync(oldSourceKey);
            }
            return quiz;
        }

        private async Task<List<QuizQuestionItem>> MapQuestionsAsync(List<QuizQuestionInput> questions, List<QuizQuestionItem>? existing)
        {
            for (var i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                var prev = existing != null && i < existing.Count ? existing[i] : null;

                // Text OR an image is enough (an image already saved on the question counts too)
                RequireTextOrImage(i + 1, "the question", q.QuestionText, q.QuestionImage, q.RemoveQuestionImage == true ? null : prev?.QuestionImageUrl);
                RequireTextOrImage(i + 1, "option A", q.OptionA, q.OptionAImage, q.RemoveOptionAImage == true ? null : prev?.OptionAImageUrl);
                RequireTextOrImage(i + 1, "option B", q.OptionB, q.OptionBImage, q.RemoveOptionBImage == true ? null : prev?.OptionBImageUrl);
                RequireTextOrImage(i + 1, "option C", q.OptionC, q.OptionCImage, q.RemoveOptionCImage == true ? null : prev?.OptionCImageUrl);

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
                    QuestionText = (q.QuestionText ?? "").Trim(),
                    OptionA = (q.OptionA ?? "").Trim(),
                    OptionB = (q.OptionB ?? "").Trim(),
                    OptionC = (q.OptionC ?? "").Trim(),
                    OptionD = (q.OptionD ?? "").Trim(),
                    CorrectOption = string.IsNullOrWhiteSpace(q.CorrectOption) ? null : q.CorrectOption.ToUpperInvariant(),
                    Mark = q.Mark ?? 1
                };

                (item.QuestionImageUrl, item.QuestionImageKey) = await ResolveImageAsync(
                    q.QuestionImage, previous?.QuestionImageUrl, previous?.QuestionImageKey, q.RemoveQuestionImage == true);
                (item.OptionAImageUrl, item.OptionAImageKey) = await ResolveImageAsync(
                    q.OptionAImage, previous?.OptionAImageUrl, previous?.OptionAImageKey, q.RemoveOptionAImage == true);
                (item.OptionBImageUrl, item.OptionBImageKey) = await ResolveImageAsync(
                    q.OptionBImage, previous?.OptionBImageUrl, previous?.OptionBImageKey, q.RemoveOptionBImage == true);
                (item.OptionCImageUrl, item.OptionCImageKey) = await ResolveImageAsync(
                    q.OptionCImage, previous?.OptionCImageUrl, previous?.OptionCImageKey, q.RemoveOptionCImage == true);
                (item.OptionDImageUrl, item.OptionDImageKey) = await ResolveImageAsync(
                    q.OptionDImage, previous?.OptionDImageUrl, previous?.OptionDImageKey, q.RemoveOptionDImage == true);

                items.Add(item);
            }

            return items;
        }

        private static void RequireTextOrImage(int questionNumber, string part, string? text, IFormFile? newImage, string? savedImageUrl)
        {
            if (string.IsNullOrWhiteSpace(text) && (newImage == null || newImage.Length == 0) && string.IsNullOrWhiteSpace(savedImageUrl))
            {
                throw new InvalidOperationException($"Question {questionNumber}: provide text or an image for {part}");
            }
        }

        private const string QuestionImageFolder = "coaching/Question";

        private async Task<(string? Url, string? Key)> ResolveImageAsync(
            IFormFile? file, string? previousUrl, string? previousKey, bool remove = false)
        {
            if (file == null || file.Length == 0)
            {
                if (!remove)
                {
                    return (previousUrl, previousKey);
                }

                if (!string.IsNullOrWhiteSpace(previousKey))
                {
                    await _fileStorageService.DeleteAsync(previousKey);
                }
                return (null, null);
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
                (string.IsNullOrWhiteSpace(q.QuestionText) && string.IsNullOrWhiteSpace(q.QuestionImageUrl)) ||
                (string.IsNullOrWhiteSpace(q.OptionA) && string.IsNullOrWhiteSpace(q.OptionAImageUrl)) ||
                (string.IsNullOrWhiteSpace(q.OptionB) && string.IsNullOrWhiteSpace(q.OptionBImageUrl)) ||
                (string.IsNullOrWhiteSpace(q.OptionC) && string.IsNullOrWhiteSpace(q.OptionCImageUrl)) ||
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
            if (deleted)
            {
                // Attempts keep their own question snapshot, so the imported Word file isn't needed any more
                await _fileStorageService.DeleteAsync(quiz.SourceFileKey ?? "");
            }
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

        public async Task<PagedResult<Quiz>> GetAllQuizzesForAdminAsync(string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize, QuizClassification? classification = null)
        {
            await ExpandSubFolderAsync(classification);
            var quizzes = FilterByClassification(await _quizRepository.GetAllAsync(), classification);
            var filtered = TextSearchHelper.ApplyFilter(quizzes, searchTerm, globalFilter, QuizSearchFields, DefaultQuizSearchFields);
            var page = PagingHelper.ToPagedResult(filtered, pageNumber, pageSize);
            var paths = await _folderService.GetSubFolderPathsAsync(page.Items.Select(q => q.SubFolderId));
            foreach (var quiz in page.Items)
            {
                quiz.SubFolderPath = PathOf(paths, quiz.SubFolderId);
            }
            return page;
        }

        public async Task<Quiz> GetQuizByIdForAdminAsync(string id)
        {
            var quiz = await _quizRepository.GetByIdAsync(id);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }
            quiz.SubFolderPath = PathOf(await _folderService.GetSubFolderPathsAsync(new[] { quiz.SubFolderId }), quiz.SubFolderId);
            return quiz;
        }

        private static string? PathOf(Dictionary<string, string> paths, string? subFolderId)
            => subFolderId != null && paths.TryGetValue(subFolderId, out var path) ? path : null;

        public async Task<PagedResult<QuizStudentResponse>> GetAccessibleQuizzesForStudentAsync(string userId, string? courseId, string? searchTerm, Dictionary<string, string>? globalFilter, int pageNumber, int pageSize, QuizClassification? classification = null)
        {
            var quizzes = await _quizRepository.GetAllAsync();
            if (!string.IsNullOrWhiteSpace(courseId))
            {
                // Free quizzes created without a course belong to every course
                quizzes = quizzes.Where(q => q.CourseId == courseId || string.IsNullOrWhiteSpace(q.CourseId)).ToList();
            }

            var verifiedEnrollments = await GetVerifiedEnrollmentsAsync(userId);
            var syllabusAccess = await GetSyllabusAccessAsync(userId);

            // A Free quiz is visible to every student regardless of course purchase; a Paid quiz
            // requires a verified enrollment in that course (and in that batch, if the quiz has one),
            // or - for a syllabusWise quiz - a verified payment for a plan covering the quiz's folder.
            var published = quizzes
                .Where(q => q.Status == Constants.QuizStatuses.Published && !q.IsExpired)
                .Where(q => CanAccess(q, verifiedEnrollments, syllabusAccess))
                .ToList();
            await ExpandSubFolderAsync(classification);
            published = FilterByClassification(published, classification);

            var filtered = TextSearchHelper.ApplyFilter(published, searchTerm, globalFilter, QuizSearchFields, DefaultQuizSearchFields);
            var myAttempts = await _quizAttemptRepository.GetAttemptKeysByUserAsync(userId);
            var accessible = filtered
                .Select(q => ToStudentResponse(q, userId, myAttempts.Count(a => a.QuizId == q.Id && a.QuizVersion == q.PublishVersion)))
                .ToList();

            var page = PagingHelper.ToPagedResult(accessible, pageNumber, pageSize);
            var paths = await _folderService.GetSubFolderPathsAsync(page.Items.Select(q => q.SubFolderId));
            foreach (var quiz in page.Items)
            {
                quiz.SubFolderPath = PathOf(paths, quiz.SubFolderId);
            }
            return page;
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

            var used = (int)await _quizAttemptRepository.CountByQuizUserAndVersionAsync(id, userId, quiz.PublishVersion);
            var response = ToStudentResponse(quiz, userId, used);
            response.SubFolderPath = PathOf(await _folderService.GetSubFolderPathsAsync(new[] { quiz.SubFolderId }), quiz.SubFolderId);
            return response;
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

            var attemptLimit = quiz.QuizAttempt ?? 1;
            var attemptsUsed = await _quizAttemptRepository.CountByQuizUserAndVersionAsync(quizId, userId, quiz.PublishVersion);
            if (attemptsUsed >= attemptLimit)
            {
                throw new InvalidOperationException(attemptLimit == 1
                    ? Constants.Messages.QuizAlreadyAttempted
                    : Constants.Messages.QuizAttemptsExhausted);
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

            var response = ToResultResponse(attempt);

            // Rank list rules (free quiz, no enrollment, ...) can reject a student: the result itself must still load
            try
            {
                var (_, rankList, _) = await BuildRankListAsync(quizId, userId, false, null, allowFreeForStudent: true);
                var mine = rankList.FirstOrDefault(r => r.UserId == userId);
                response.Leaderboard = rankList;
                response.TotalParticipants = rankList.Count;
                response.Rank = mine?.Rank;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is KeyNotFoundException)
            {
                response.LeaderboardUnavailableReason = ex.Message;
            }

            return response;
        }

        public async Task<List<RankListEntryDto>> GetRankListAsync(string quizId, string userId, bool isAdmin, RankListRequest? request = null)
        {
            var (_, rankList, _) = await BuildRankListAsync(quizId, userId, isAdmin, request);
            return rankList;
        }

        // Rank lists are kept per batch: each attempt remembers the batch the student was in when
        // they submitted, so moving a quiz to another batch doesn't mix or hide earlier results.
        // Admin: shows the quiz's own batch by default, any other batch via batchId, or every batch
        // (each ranked separately) with batchId=all.
        // Student: always their own batch.
        // Admin filters (RankListRequest): quizType must match the quiz; quizToView=Paid ranks only students
        // with a verified paid enrollment (optionally in batchId); on a free quiz, quizToView=Free ranks everyone.
        private async Task<(Quiz Quiz, List<RankListEntryDto> RankList, string? BatchTitle)> BuildRankListAsync(
            string quizId, string userId, bool isAdmin, RankListRequest? request, bool allowFreeForStudent = false)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new KeyNotFoundException(Constants.Messages.QuizNotFound);
            }

            var isFree = quiz.QuizToView == Constants.MaterialAccess.Free;
            if (isFree && !isAdmin && !allowFreeForStudent)
            {
                throw new InvalidOperationException(Constants.Messages.RankListNotForFreeQuiz);
            }

            var batchId = request?.BatchId;

            // Which students to rank on a free quiz: null = everyone (no filter given)
            string? studentView = null;
            if (isAdmin)
            {
                if (!string.IsNullOrWhiteSpace(request?.QuizType))
                {
                    var requestedType = Constants.QuizTypes.Normalize(request.QuizType)
                        ?? throw new InvalidOperationException(Constants.Messages.InvalidQuizTypeFilter);
                    if (!string.Equals(quiz.QuizType, requestedType, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(Constants.Messages.RankListQuizTypeMismatch);
                    }
                }

                if (!string.IsNullOrWhiteSpace(request?.QuizToView))
                {
                    studentView = Constants.MaterialAccess.Normalize(request.QuizToView);
                    if (!isFree && studentView == Constants.MaterialAccess.Free)
                    {
                        throw new InvalidOperationException(Constants.Messages.RankListFreeViewOnPaidQuiz);
                    }

                    // quizToView=Free on a free quiz just names the quiz's own access type: rank everyone who
                    // attempted it (paid students included), otherwise enrolled students vanish from the list
                    if (isFree && studentView == Constants.MaterialAccess.Free)
                    {
                        studentView = null;
                    }
                }
            }

            var syllabusOnly = !isFree && quiz.QuizType == Constants.QuizTypes.SyllabusWise && string.IsNullOrWhiteSpace(quiz.CourseId);

            string? targetBatchId = null;
            if (isFree)
            {
                // Free quizzes have no batch of their own: batchId only narrows the paid students
                if (studentView == Constants.MaterialAccess.Paid && !string.IsNullOrWhiteSpace(batchId) &&
                    !batchId.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    targetBatchId = batchId;
                }
            }
            else if (syllabusOnly)
            {
                // A Paid syllabusWise quiz without a course has no batches: one list of the students who paid
                if (!isAdmin && !(await GetSyllabusAccessAsync(userId)).Covers(quiz.FolderId, quiz.SubFolderId))
                {
                    throw new UnauthorizedAccessException(Constants.Messages.NoCourseAccess);
                }
            }
            else if (isAdmin)
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
            var syllabusByUser = new Dictionary<string, SyllabusAccess>();
            foreach (var attempt in await _quizAttemptRepository.GetByQuizIdAsync(quizId))
            {
                if (isFree)
                {
                    if (studentView == null)
                    {
                        // Anyone can attempt a free quiz, paid or not, so everyone is ranked in one list
                        candidates.Add((attempt, null, null));
                        continue;
                    }

                    var verified = await GetVerifiedEnrollmentsAsync(attempt.UserId);
                    var paid = targetBatchId == null
                        ? verified.FirstOrDefault()
                        : verified.FirstOrDefault(e => e.BatchId == targetBatchId);
                    if (paid != null)
                    {
                        candidates.Add((attempt, paid.BatchId, paid.BatchTitle));
                    }
                    continue;
                }

                if (syllabusOnly)
                {
                    if (!syllabusByUser.TryGetValue(attempt.UserId, out var access))
                    {
                        access = syllabusByUser[attempt.UserId] = await GetSyllabusAccessAsync(attempt.UserId);
                    }
                    if (access.Covers(quiz.FolderId, quiz.SubFolderId))
                    {
                        candidates.Add((attempt, null, null));
                    }
                    continue;
                }

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
                // A student may have several attempts: only their best one is ranked (ties go to the earlier submission)
                var ordered = group
                    .Where(c => c.Attempt.QuizVersion == latestVersion)
                    .GroupBy(c => c.Attempt.UserId)
                    .Select(g => g.OrderByDescending(c => c.Attempt.ScoredMarks).ThenBy(c => c.Attempt.SubmittedAt).First())
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

        public async Task<(byte[] Content, string FileName)> ExportRankListAsync(string quizId, string userId, bool isAdmin, RankListRequest? request = null)
        {
            var (quiz, rankList, batchTitle) = await BuildRankListAsync(quizId, userId, isAdmin, request);
            var content = _excelExportService.GenerateRankListExcel(quiz.Title, batchTitle, rankList);

            var invalidChars = Path.GetInvalidFileNameChars();
            string Safe(string value) => new string(value.Select(c => invalidChars.Contains(c) || c == ' ' ? '_' : c).ToArray());
            var fileName = string.IsNullOrWhiteSpace(batchTitle)
                ? $"quiz-rank-list-{Safe(string.IsNullOrWhiteSpace(quiz.CourseName) ? quiz.Title : quiz.CourseName)}.xlsx"
                : $"quiz-rank-list-{Safe(quiz.CourseName ?? "")}-{Safe(batchTitle)}.xlsx";

            return (content, fileName);
        }

        // A sub folder filter also matches quizzes filed in any sub folder below it
        private async Task ExpandSubFolderAsync(QuizClassification? filter)
        {
            if (filter?.SubFolderId != null)
            {
                filter.SubFolderIds = await _folderService.GetSubtreeIdsAsync(filter.SubFolderId);
            }
        }

        // Optional quizType / quizToView / folder filters (only the ones that are passed are applied)
        private static List<Quiz> FilterByClassification(List<Quiz> quizzes, QuizClassification? filter)
        {
            if (filter == null || filter.IsEmpty)
            {
                return quizzes;
            }

            return quizzes.Where(q =>
                (filter.QuizType == null || string.Equals(q.QuizType, filter.QuizType, StringComparison.OrdinalIgnoreCase)) &&
                (filter.QuizToView == null || string.Equals(q.QuizToView, filter.QuizToView, StringComparison.OrdinalIgnoreCase)) &&
                (filter.FolderId == null || q.FolderId == filter.FolderId) &&
                (filter.SubFolderId == null || (q.SubFolderId != null && (filter.SubFolderIds ?? new HashSet<string> { filter.SubFolderId }).Contains(q.SubFolderId))))
                .ToList();
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
            // The payment lookup only happens for a Paid syllabusWise quiz - every other quiz skips it
            var syllabusAccess = quiz.QuizType == Constants.QuizTypes.SyllabusWise ? await GetSyllabusAccessAsync(userId) : null;
            return CanAccess(quiz, await GetVerifiedEnrollmentsAsync(userId), syllabusAccess);
        }

        private Task<SyllabusAccess> GetSyllabusAccessAsync(string userId) => _syllabusWiseService.GetAccessAsync(userId);

        // Free quizzes are open to everyone. Paid quizzes need a verified enrollment in the quiz's
        // course, and when the quiz is tied to a batch, in that same batch. A Paid syllabusWise quiz is
        // also open to a student whose verified payment covers the quiz's folder (syllabus).
        private static bool CanAccess(Quiz quiz, List<Enrollment> verifiedEnrollments, SyllabusAccess? syllabus)
        {
            if (quiz.QuizToView == Constants.MaterialAccess.Free)
            {
                return true;
            }
            if (quiz.QuizType == Constants.QuizTypes.SyllabusWise && syllabus?.Covers(quiz.FolderId, quiz.SubFolderId) == true)
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

        // A previousYear, syllabusWise, school or currentAffairs quiz must be filed in a folder (and sub folder, if the folder has any); other types can't have one
        private async Task<FolderSelection> ValidateFolderForTypeAsync(string? quizType, string? folderId, string? subFolderId)
        {
            if (Constants.QuizTypes.UsesFolders(quizType))
            {
                if (string.IsNullOrWhiteSpace(folderId))
                {
                    throw new InvalidOperationException($"Folder is required for a {quizType} quiz - pick one from GET /api/Folder");
                }
                return await _folderService.ValidateAsync(folderId, subFolderId);
            }

            if (!string.IsNullOrWhiteSpace(folderId) || !string.IsNullOrWhiteSpace(subFolderId))
            {
                throw new InvalidOperationException("Folder and sub folder can only be set on a previousYear, syllabusWise, school or currentAffairs quiz");
            }
            return new FolderSelection();
        }

        private static QuizStudentResponse ToStudentResponse(Quiz quiz, string userId, int attemptsUsed)
        {
            var attemptLimit = quiz.QuizAttempt ?? 1;
            return new QuizStudentResponse
            {
                QuizAttempt = attemptLimit,
                AttemptsUsed = attemptsUsed,
                AttemptsRemaining = Math.Max(0, attemptLimit - attemptsUsed),
                Id = quiz.Id,
                CourseId = quiz.CourseId,
                CourseName = quiz.CourseName,
                Title = quiz.Title,
                Description = quiz.Description,
                QuizToView = quiz.QuizToView,
                QuizType = quiz.QuizType,
                FolderId = quiz.FolderId,
                FolderName = quiz.FolderName,
                SubFolderId = quiz.SubFolderId,
                SubFolderName = quiz.SubFolderName,
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
