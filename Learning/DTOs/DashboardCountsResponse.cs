namespace LearningBackendAPI.DTOs
{
    public class DashboardCountsResponse
    {
        public long TotalUsers { get; set; }
        public long TotalCourses { get; set; }
        public long TotalBatch { get; set; }
        public long TotalStudyMaterial { get; set; }
        public long TotalVideoMaterial { get; set; }
        public long TotalQuestion { get; set; }
    }
}
