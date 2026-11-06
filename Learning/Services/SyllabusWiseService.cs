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

        public SyllabusWiseService(ISyllabusWiseRepository repository, IFolderService folderService)
        {
            _repository = repository;
            _folderService = folderService;
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

        public async Task<SyllabusWisePaymentDto> SubmitPaymentAsync(string userId, SyllabusWisePaymentRequest request)
        {
            // The payer is whoever is signed in. A userId in the body is only accepted when it's their own.
            if (!string.IsNullOrWhiteSpace(request.UserId) && request.UserId.Trim() != userId)
            {
                throw new InvalidOperationException("userId doesn't match the signed-in student");
            }

            var status = Constants.PaymentStatuses.Normalize(request.PaymentStatus);
            var plan = await ResolvePlanAsync(request.PlanId);

            var payment = await _repository.GetPaymentAsync(userId, plan.Id);
            if (payment?.PaymentStatus == Constants.PaymentStatuses.Verified)
            {
                // Already verified: a repeated or late call never takes access away (only the admin can change it)
                return ToDto(payment);
            }

            var isNew = payment == null;
            payment ??= new SyllabusWisePayment { UserId = userId, PlanId = plan.Id };
            payment.PlanTitle = plan.Title;
            payment.Amount = plan.Amount;
            payment.FolderId = plan.FolderId;
            payment.PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? payment.PaymentMethod : request.PaymentMethod.Trim();
            payment.TransactionReference = string.IsNullOrWhiteSpace(request.TransactionReference) ? payment.TransactionReference : request.TransactionReference.Trim();
            payment.PaymentStatus = status;
            payment.VerifiedAt = status == Constants.PaymentStatuses.Verified ? DateTime.UtcNow : null;

            if (isNew)
            {
                await _repository.CreatePaymentAsync(payment);
            }
            else
            {
                await _repository.UpdatePaymentAsync(payment);
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

        public async Task<SyllabusWisePaymentDto> UpdatePaymentStatusAsync(string id, SyllabusWisePaymentStatusRequest request)
        {
            var payment = await _repository.GetPaymentByIdAsync(id)
                ?? throw new KeyNotFoundException("Payment not found");

            payment.PaymentStatus = Constants.PaymentStatuses.Normalize(request.PaymentStatus);
            payment.VerifiedAt = payment.PaymentStatus == Constants.PaymentStatuses.Verified ? DateTime.UtcNow : null;
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
            PaymentStatus = payment.PaymentStatus,
            VerifiedAt = payment.VerifiedAt,
            CreatedAt = payment.CreatedAt
        };
    }
}
