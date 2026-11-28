using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public interface IExcelExportService
    {
        byte[] GenerateRankListExcel(string quizTitle, string? batchTitle, List<RankListEntryDto> rankList);
        /// <summary>One row per enrollment of each student (a student with none gets one row with the enrollment columns empty).</summary>
        byte[] GenerateUserListExcel(List<User> users, ILookup<string, Enrollment> enrollmentsByUser);
        byte[] GenerateCourseEnrollmentExcel(string courseName, List<CourseEnrollmentReportRow> rows);
    }
}
