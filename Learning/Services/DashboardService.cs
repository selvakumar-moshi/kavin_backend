using LearningBackendAPI.DTOs;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IUserRepository _userRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly IStudyMaterialRepository _studyMaterialRepository;
        private readonly IVideoMaterialRepository _videoMaterialRepository;
        private readonly IQuizRepository _quizRepository;

        public DashboardService(
            IUserRepository userRepository,
            ICourseRepository courseRepository,
            IBatchRepository batchRepository,
            IStudyMaterialRepository studyMaterialRepository,
            IVideoMaterialRepository videoMaterialRepository,
            IQuizRepository quizRepository)
        {
            _userRepository = userRepository;
            _courseRepository = courseRepository;
            _batchRepository = batchRepository;
            _studyMaterialRepository = studyMaterialRepository;
            _videoMaterialRepository = videoMaterialRepository;
            _quizRepository = quizRepository;
        }

        public async Task<DashboardCountsResponse> GetCountsAsync()
        {
            var totalUsers = await _userRepository.CountByRoleAsync(Constants.Roles.User);
            var totalCourses = await _courseRepository.CountAsync();
            var totalBatch = await _batchRepository.CountAsync();
            var totalStudyMaterial = await _studyMaterialRepository.CountAsync();
            var totalVideoMaterial = await _videoMaterialRepository.CountAsync();
            var totalQuestion = await _quizRepository.CountTotalQuestionsAsync();

            return new DashboardCountsResponse
            {
                TotalUsers = totalUsers,
                TotalCourses = totalCourses,
                TotalBatch = totalBatch,
                TotalStudyMaterial = totalStudyMaterial,
                TotalVideoMaterial = totalVideoMaterial,
                TotalQuestion = totalQuestion
            };
        }
    }
}
