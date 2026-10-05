using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Services
{
    public interface IExcelExportService
    {
        byte[] GenerateRankListExcel(string quizTitle, string? batchTitle, List<RankListEntryDto> rankList);
        byte[] GenerateCourseEnrollmentExcel(string courseName, List<CourseEnrollmentReportRow> rows);
    }
}
