using System.ComponentModel.DataAnnotations;

namespace LearningBackendAPI.DTOs
{
    public class VideoMaterialRequest
    {
        public string? Title { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string? Description { get; set; }
        public string? CourseId { get; set; }
        public string? BatchId { get; set; }
        public string? YoutubeLink { get; set; }
        public string? MaterialToView { get; set; }
    }
}
