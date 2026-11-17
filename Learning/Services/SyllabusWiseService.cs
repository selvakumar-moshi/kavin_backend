using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class SyllabusWiseService : ISyllabusWiseService
    {
        private readonly ISyllabusWiseRepository _repository;
        private readonly IFolderService _folderService;
        private readonly IUserRepository _userRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IEnrollmentRepository _enrollmentRepository;

        private const long MaxScreenshotSizeInBytes = 5 * 1024 * 1024; // 5 MB
        // Kept apart from the course payment screenshots (coaching/payment) so the two never overwrite each other
        private const string PaymentFolder = "coaching/payment/syllabus";

        public SyllabusWiseService(
            ISyllabusWiseRepository repository,
            IFolderService folderService,
            IUserRepository userRepository,
            IFileStorageService fileStorageService,
            IEnrollmentRepository enrollmentRepository)
        {
            _enrollmentRepository = enrollmentRepository;
            _repository = repository;
            _folderService = folderService;
            _userRepository = userRepository;
            _fileStorageService = fileStorageService;
        }

        // ---------- Plans (admin) ----------

        public async Task<List<SyllabusWisePlan>> GetPlansAsync()
        {
            var plans = await _repository.GetPlansAsync();
            var names = await _folderService.GetNodeNamesAsync(plans.Select(p => p.FolderId));
            foreach (var plan in plans)
            {
                plan.FolderName = plan.FolderId != null && names.TryGetValue(plan.FolderId, out var name) ? name : null;
            }
            return plans;
        }

        public async Task<SyllabusWisePlan> GetPlanAsync(string id)
        {
            var plan = await _repository.GetPlanByIdAsync(id) ?? throw new KeyNotFoundException("Syllabus wise plan not found");
            await FillFolderNameAsync(plan);
            return plan;
        }

        private async Task FillFolderNameAsync(SyllabusWisePlan plan)
        {
            var names = await _folderService.GetNodeNamesAsync(new[] { plan.FolderId });
            plan.FolderName = plan.FolderId != null && names.TryGetValue(plan.FolderId, out var name) ? name : null;
        }

        // Blank = no folder (covers every syllabusWise quiz); otherwise it must be a folder or sub folder that exists
        private async Task<string?> ResolveFolderIdAsync(string? folderId)
        {
            if (string.IsNullOrWhiteSpace(folderId))
            {
                return null;
            }
            var id = folderId.Trim();
            if (!(await _folderService.GetNodeNamesAsync(new[] { id })).ContainsKey(id))
            {
                throw new InvalidOperationException("Folder not found");
            }
            return id;
        }

        public async Task<SyllabusWisePlan> CreatePlanAsync(SyllabusWisePlanRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title is required");
            }
            if (request.Amount == null || request.Amount < 0)
            {
                throw new InvalidOperationException("Amount is required and can't be negative");
            }

            var plan = await _repository.CreatePlanAsync(new SyllabusWisePlan
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Amount = request.Amount.Value,
                FolderId = await ResolveFolderIdAsync(request.FolderId)
            });
            await FillFolderNameAsync(plan);
            return plan;
        }

        public async Task<SyllabusWisePlan> UpdatePlanAsync(string id, SyllabusWisePlanRequest request)
        {
            var plan = await GetPlanAsync(id);

            if (request.Title != null)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    throw new InvalidOperationException("Title can't be empty");
                }
                plan.Title = request.Title.Trim();
            }
            if (request.Description != null)
            {
                plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            }
            if (request.Amount != null)
            {
                if (request.Amount < 0)
                {
                    throw new InvalidOperationException("Amount can't be negative");
                }
                plan.Amount = request.Amount.Value;
            }
            // Only changes what students pay for from now on; payments already made keep the folder they covered
            if (request.FolderId != null)
            {
                plan.FolderId = await ResolveFolderIdAsync(request.FolderId);
            }

            await _repository.UpdatePlanAsync(plan);
            await FillFolderNameAsync(plan);
            return plan;
        }

        public async Task DeletePlanAsync(string id)
        {
            await GetPlanAsync(id);
            await _repository.DeletePlanAsync(id);
        }

        // ---------- Payments ----------

        // Same manual flow as a course payment: the student uploads a screenshot, the payment is Pending, and only the
        // admin can set it to Verified or Rejected. A Rejected payment can be resubmitted with a new screenshot (back to Pending).
        public async Task<SyllabusWisePaymentDto> SubmitPaymentAsync(string userId, SyllabusWisePaymentRequest request)
        {
            var file = request.File;
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException(Constants.Messages.PaymentScreenshotRequired);
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension))
            {
                throw new InvalidOperationException(Constants.Messages.PaymentScreenshotInvalidType);
            }

            if (file.Length > MaxScreenshotSizeInBytes)
            {
                throw new InvalidOperationException(Constants.Messages.PaymentScreenshotTooLarge);
            }

            var plan = await ResolvePlanAsync(request.PlanId);

            var payment = await _repository.GetPaymentAsync(userId, plan.Id);
            if (payment?.PaymentStatus == Constants.PaymentStatuses.Verified)
            {
                // Already verified: nothing to pay again, and a late upload never takes access away
                throw new InvalidOperationException("Payment is already verified for this plan");
            }

            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException(Constants.Messages.UserNotFound);

            // A transaction ID that is sent must not belong to any other payment (course or syllabus wise)
            var reference = string.IsNullOrWhiteSpace(request.TransactionReference) ? null : request.TransactionReference.Trim().ToUpperInvariant();
            if (reference != null)
            {
                var courseOwner = await _enrollmentRepository.GetByTransactionReferenceAsync(reference);
                var syllabusOwner = await _repository.GetPaymentByTransactionReferenceAsync(reference);
                if (courseOwner != null || (syllabusOwner != null && syllabusOwner.Id != payment?.Id))
                {
                    throw new InvalidOperationException(Constants.Messages.TransactionReferenceInUse);
                }
            }

            // Named after the student (plus application no.), e.g. Ajith_kumar_RSK-1004_<planId>.png
            var rawName = $"{user.FirstName}_{user.LastName}_{user.ApplicationNo}_{plan.Id}".Trim('_');
            var safeName = new string(rawName.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
            var oldKey = payment?.PaymentScreenshotKey;
            var (url, key) = await _fileStorageService.UploadImageAsync(file, PaymentFolder, $"{safeName}{extension}");

            var isNew = payment == null;
            payment ??= new SyllabusWisePayment { UserId = userId, PlanId = plan.Id };
            // The plan's current title, amount and folder are copied now, so later plan edits don't rewrite what was paid
            payment.PlanTitle = plan.Title;
            payment.Amount = plan.Amount;
            payment.FolderId = plan.FolderId;
            payment.PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? payment.PaymentMethod : request.PaymentMethod.Trim();
            payment.TransactionReference = reference ?? payment.TransactionReference;
            payment.PaymentScreenshot = url;
            payment.PaymentScreenshotKey = key;
            // New, or re-submitted after a rejection: goes to the admin's review queue
            payment.PaymentStatus = Constants.PaymentStatuses.Pending;
            payment.VerifiedAt = null;
            payment.VerifiedByAdminId = null;

            if (isNew)
            {
                await _repository.CreatePaymentAsync(payment);
            }
            else
            {
                await _repository.UpdatePaymentAsync(payment);
            }

            // Same name means the upload already overwrote the old file - deleting it would remove the new one
            if (!string.IsNullOrWhiteSpace(oldKey) && oldKey != key)
            {
                await _fileStorageService.DeleteAsync(oldKey);
            }
            return ToDto(payment);
        }

        public async Task<SyllabusAccess> GetAccessAsync(string userId)
        {
            var access = new SyllabusAccess();
            foreach (var payment in await _repository.GetVerifiedPaymentsAsync(userId))
            {
                if (string.IsNullOrWhiteSpace(payment.FolderId))
                {
                    access.All = true;
                    continue;
                }
                access.FolderIds.Add(payment.FolderId);
                // The id may be a top-level folder or a sub folder: this returns the sub folders below it (and itself, if it is one)
                access.SubFolderIds.UnionWith(await _folderService.GetSubtreeIdsAsync(payment.FolderId));
            }
            return access;
        }

        public async Task<List<SyllabusWisePaymentDto>> GetPaymentsAsync(string? userId)
            => (await _repository.GetPaymentsAsync(userId)).Select(ToDto).ToList();

        public async Task<SyllabusWisePaymentDto> UpdatePaymentStatusAsync(string id, SyllabusWisePaymentStatusRequest request, string? adminUserId = null)
        {
            var payment = await _repository.GetPaymentByIdAsync(id)
                ?? throw new KeyNotFoundException("Payment not found");

            payment.PaymentStatus = Constants.PaymentStatuses.Normalize(request.PaymentStatus);
            var verified = payment.PaymentStatus == Constants.PaymentStatuses.Verified;
            payment.VerifiedAt = verified ? DateTime.UtcNow : null;
            payment.VerifiedByAdminId = verified ? adminUserId : null;
            await _repository.UpdatePaymentAsync(payment);
            return ToDto(payment);
        }

        // The plan the payment is for; planId can be left out while there is only one plan
        private async Task<SyllabusWisePlan> ResolvePlanAsync(string? planId)
        {
            if (!string.IsNullOrWhiteSpace(planId))
            {
                return await _repository.GetPlanByIdAsync(planId.Trim())
                    ?? throw new InvalidOperationException("Syllabus wise plan not found");
            }

            var plans = await _repository.GetPlansAsync();
            return plans.Count switch
            {
                0 => throw new InvalidOperationException("No syllabus wise plan exists yet"),
                1 => plans[0],
                _ => throw new InvalidOperationException("planId is required - there is more than one syllabus wise plan")
            };
        }

        public static SyllabusWisePaymentDto ToDto(SyllabusWisePayment payment) => new()
        {
            PaymentId = payment.Id,
            UserId = payment.UserId,
            PlanId = payment.PlanId,
            PlanTitle = payment.PlanTitle,
            FolderId = payment.FolderId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            TransactionReference = payment.TransactionReference,
            PaymentScreenshot = payment.PaymentScreenshot,
            PaymentStatus = payment.PaymentStatus,
            VerifiedAt = payment.VerifiedAt,
            CreatedAt = payment.CreatedAt
        };
    }
}
