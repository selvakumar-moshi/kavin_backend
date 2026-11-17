using LearningBackendAPI.Models;
using LearningBackendAPI.Utils;
using MongoDB.Driver;

namespace LearningBackendAPI.Repositories
{
    public class SyllabusWiseRepository : ISyllabusWiseRepository
    {
        private readonly IMongoCollection<SyllabusWisePlan> _plans;
        private readonly IMongoCollection<SyllabusWisePayment> _payments;

        public SyllabusWiseRepository(IMongoDatabase database)
        {
            _plans = database.GetCollection<SyllabusWisePlan>("SyllabusWisePlans");
            _payments = database.GetCollection<SyllabusWisePayment>("SyllabusWisePayments");
        }

        public async Task<List<SyllabusWisePlan>> GetPlansAsync()
            => await _plans.Find(_ => true).SortBy(p => p.Id).ToListAsync();

        public async Task<SyllabusWisePlan?> GetPlanByIdAsync(string id)
            => await _plans.Find(p => p.Id == id).FirstOrDefaultAsync();

        public async Task<SyllabusWisePlan> CreatePlanAsync(SyllabusWisePlan plan)
        {
            plan.CreatedAt = DateTime.UtcNow;
            await _plans.InsertOneAsync(plan);
            return plan;
        }

        public async Task UpdatePlanAsync(SyllabusWisePlan plan)
        {
            plan.UpdatedAt = DateTime.UtcNow;
            await _plans.ReplaceOneAsync(p => p.Id == plan.Id, plan);
        }

        public async Task<bool> DeletePlanAsync(string id)
        {
            var result = await _plans.DeleteOneAsync(p => p.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<List<SyllabusWisePayment>> GetPaymentsAsync(string? userId = null)
        {
            var find = string.IsNullOrWhiteSpace(userId)
                ? _payments.Find(_ => true)
                : _payments.Find(p => p.UserId == userId);
            return await find.SortByDescending(p => p.CreatedAt).ToListAsync();
        }

        public async Task<SyllabusWisePayment?> GetPaymentByIdAsync(string id)
            => await _payments.Find(p => p.Id == id).FirstOrDefaultAsync();

        public async Task<SyllabusWisePayment?> GetPaymentAsync(string userId, string planId)
            => await _payments.Find(p => p.UserId == userId && p.PlanId == planId).FirstOrDefaultAsync();

        public async Task<SyllabusWisePayment> CreatePaymentAsync(SyllabusWisePayment payment)
        {
            payment.CreatedAt = DateTime.UtcNow;
            await _payments.InsertOneAsync(payment);
            return payment;
        }

        public async Task UpdatePaymentAsync(SyllabusWisePayment payment)
        {
            payment.UpdatedAt = DateTime.UtcNow;
            await _payments.ReplaceOneAsync(p => p.Id == payment.Id, payment);
        }

        public async Task<SyllabusWisePayment?> GetPaymentByTransactionReferenceAsync(string transactionReference)
            => await _payments.Find(p => p.TransactionReference == transactionReference).FirstOrDefaultAsync();

        public async Task<List<SyllabusWisePayment>> GetVerifiedPaymentsAsync(string userId)
            => await _payments.Find(p => p.UserId == userId && p.PaymentStatus == Constants.PaymentStatuses.Verified).ToListAsync();
    }
}
