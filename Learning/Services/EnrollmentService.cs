using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class EnrollmentService : IEnrollmentService
    {
        private readonly IEnrollmentRepository _enrollmentRepository;

        public EnrollmentService(IEnrollmentRepository enrollmentRepository)
        {
            _enrollmentRepository = enrollmentRepository;
        }

        public async Task<List<Enrollment>> GetAllEnrollmentsAsync()
        {
            return await _enrollmentRepository.GetAllAsync();
        }

        public async Task<List<Enrollment>> GetEnrollmentsByUserIdAsync(string userId)
        {
            return await _enrollmentRepository.GetByUserIdAsync(userId);
        }

        public async Task<Enrollment> GetEnrollmentByIdAsync(string id)
        {
            var enrollment = await _enrollmentRepository.GetByIdAsync(id);
            if (enrollment == null)
            {
                throw new KeyNotFoundException(Constants.Messages.EnrollmentNotFound);
            }
            return enrollment;
        }

        public async Task<Enrollment> UpdateStatusAsync(string id, string status, string? paymentMethod, string? transactionReference, string adminUserId)
        {
            if (!Constants.EnrollmentStatuses.All.Contains(status))
            {
                throw new InvalidOperationException(Constants.Messages.InvalidEnrollmentStatus);
            }

            if (!string.IsNullOrWhiteSpace(paymentMethod) && !Constants.PaymentMethods.All.Contains(paymentMethod))
            {
                throw new InvalidOperationException(Constants.Messages.InvalidPaymentMethod);
            }

            var enrollment = await _enrollmentRepository.GetByIdAsync(id);
            if (enrollment == null)
            {
                throw new KeyNotFoundException(Constants.Messages.EnrollmentNotFound);
            }

            enrollment.Status = status;

            if (!string.IsNullOrWhiteSpace(paymentMethod))
            {
                enrollment.PaymentMethod = paymentMethod;
            }

            if (!string.IsNullOrWhiteSpace(transactionReference))
            {
                enrollment.TransactionReference = transactionReference;
            }

            if (status == Constants.EnrollmentStatuses.Verified)
            {
                enrollment.VerifiedAt = DateTime.UtcNow;
                enrollment.VerifiedByAdminId = adminUserId;
            }
            else
            {
                enrollment.VerifiedAt = null;
                enrollment.VerifiedByAdminId = null;
            }

            await _enrollmentRepository.UpdateAsync(id, enrollment);
            return enrollment;
        }
    }
}
