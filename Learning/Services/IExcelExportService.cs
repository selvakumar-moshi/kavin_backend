using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IExcelExportService
    {
        byte[] GenerateRankListExcel(string quizTitle, string? batchTitle, List<RankListEntryDto> rankList);
        byte[] GenerateUserListExcel(List<User> users);
        byte[] GenerateCourseEnrollmentExcel(string courseName, List<CourseEnrollmentReportRow> rows);
    }
}
