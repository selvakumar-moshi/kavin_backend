using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class FreeMaterialService : IFreeMaterialService
    {
        private readonly IStudyMaterialRepository _studyMaterialRepository;
        private readonly IVideoMaterialRepository _videoMaterialRepository;

        public FreeMaterialService(
            IStudyMaterialRepository studyMaterialRepository,
            IVideoMaterialRepository videoMaterialRepository)
        {
            _studyMaterialRepository = studyMaterialRepository;
            _videoMaterialRepository = videoMaterialRepository;
        }

        private static readonly Dictionary<string, Func<FreeMaterialDto, string?>> SearchFields = new()
        {
            ["title"] = m => m.Title,
            ["description"] = m => m.Description,
            ["batchtitle"] = m => m.BatchTitle
        };
        private static readonly string[] DefaultSearchFields = { "title", "description", "batchTitle" };

        public async Task<List<FreeMaterialDto>> GetFreeMaterialsAsync(string? courseId, string? searchTerm)
        {
            var studyMaterials = await _studyMaterialRepository.GetAllAsync();
            var videoMaterials = await _videoMaterialRepository.GetAllAsync();

            var items = new List<FreeMaterialDto>();
            items.AddRange(studyMaterials
                .Where(m => m.MaterialToView == Constants.MaterialAccess.Free)
                .Select(ToFreeMaterialDto));
            items.AddRange(videoMaterials
                .Where(m => m.MaterialToView == Constants.MaterialAccess.Free)
                .Select(ToFreeMaterialDto));

            if (!string.IsNullOrWhiteSpace(courseId) && !string.Equals(courseId, "All", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(m => m.CourseId == courseId).ToList();
            }

            var filtered = TextSearchHelper.ApplyFilter(items, searchTerm, null, SearchFields, DefaultSearchFields);
            return filtered.OrderByDescending(m => m.CreatedAt).ToList();
        }

        private static FreeMaterialDto ToFreeMaterialDto(StudyMaterial m) => new()
        {
            Id = m.Id,
            MaterialType = "Study",
            Title = m.Title,
            Description = m.Description,
            CourseId = m.CourseId,
            BatchId = m.BatchId,
            BatchTitle = m.BatchTitle,
            PdfUrl = m.PdfUrl,
            CreatedAt = m.CreatedAt
        };

        private static FreeMaterialDto ToFreeMaterialDto(VideoMaterial m) => new()
        {
            Id = m.Id,
            MaterialType = "Video",
            Title = m.Title,
            Description = m.Description,
            CourseId = m.CourseId,
            BatchId = m.BatchId,
            BatchTitle = m.BatchTitle,
            YoutubeLink = m.YoutubeLink,
            CreatedAt = m.CreatedAt
        };
    }
}
