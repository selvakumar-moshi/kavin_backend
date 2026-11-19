using ClosedXML.Excel;
using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;

namespace LearningBackendAPI.Services
{
    public class ExcelExportService : IExcelExportService
    {
        private static readonly string[] RankListHeaders =
        {
            "Student Name", "District", "Correct Answers", "Wrong Answers", "Total Questions", "Marks Scored", "Total Marks", "Score", "Rank", "Batch"
        };

        private static readonly string[] CourseEnrollmentHeaders =
        {
            "Application No","Student Name","District", "Email","Phone Number", "Batch", "Status"
        };

        public byte[] GenerateRankListExcel(string quizTitle, string? batchTitle, List<RankListEntryDto> rankList)
        {
            var title = string.IsNullOrWhiteSpace(batchTitle)
                ? $"Rank List - {quizTitle}"
                : $"Rank List - {quizTitle} ({batchTitle})";
            using var workbook = CreateTemplate("Rank List", title, RankListHeaders);
            var worksheet = workbook.Worksheet(1);

            FillRankListRows(worksheet, rankList);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static readonly string[] UserListHeaders =
        {
            "Application No", "User Name", "Email", "Phone Number", "District", "Joined At"
        };

        public byte[] GenerateUserListExcel(List<User> users)
        {
            using var workbook = CreateTemplate("Users", "Users", UserListHeaders);
            var worksheet = workbook.Worksheet(1);

            for (var i = 0; i < users.Count; i++)
            {
                var user = users[i];
                var row = 4 + i;

                worksheet.Cell(row, 1).Value = user.ApplicationNo ?? "";
                worksheet.Cell(row, 2).Value = $"{user.FirstName} {user.LastName}".Trim();
                worksheet.Cell(row, 3).Value = user.Email;
                // Text, so a leading zero or long number isn't turned into a number by Excel
                worksheet.Cell(row, 4).SetValue(user.PhoneNumber ?? "");
                worksheet.Cell(row, 5).Value = user.District ?? "";
                worksheet.Cell(row, 6).Value = user.CreatedAt;
                worksheet.Cell(row, 6).Style.DateFormat.Format = "dd-MMM-yyyy HH:mm";
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] GenerateCourseEnrollmentExcel(string courseName, List<CourseEnrollmentReportRow> rows)
        {
            using var workbook = CreateTemplate("Enrollments", $"Course - {courseName}", CourseEnrollmentHeaders);
            var worksheet = workbook.Worksheet(1);

            FillCourseEnrollmentRows(worksheet, rows);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static XLWorkbook CreateTemplate(string sheetName, string titleText, string[] headers)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            var titleRange = worksheet.Range(1, 1, 1, headers.Length);
            titleRange.Merge();
            worksheet.Cell(1, 1).Value = titleText;
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;

            for (var i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            return workbook;
        }

        private static void FillRankListRows(IXLWorksheet worksheet, List<RankListEntryDto> rankList)
        {
            const int firstDataRow = 4;

            for (var i = 0; i < rankList.Count; i++)
            {
                var entry = rankList[i];
                var row = firstDataRow + i;

                worksheet.Cell(row, 1).Value = $"{entry.FirstName} {entry.LastName}".Trim();
                worksheet.Cell(row, 10).Value = entry.BatchTitle ?? "";
                worksheet.Cell(row, 2).Value = entry.District ?? "";
                worksheet.Cell(row, 3).Value = entry.CorrectCount;
                worksheet.Cell(row, 4).Value = entry.TotalQuestions - entry.CorrectCount;
                worksheet.Cell(row, 5).Value = entry.TotalQuestions;
                worksheet.Cell(row, 6).Value = entry.ScoredMarks;
                worksheet.Cell(row, 7).Value = entry.TotalMarks;
                worksheet.Cell(row, 8).Value = entry.Score;
                worksheet.Cell(row, 9).Value = entry.Rank;
            }

            worksheet.Columns().AdjustToContents();
        }

        private static void FillCourseEnrollmentRows(IXLWorksheet worksheet, List<CourseEnrollmentReportRow> rows)
        {
            const int firstDataRow = 4;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var excelRow = firstDataRow + i;

                worksheet.Cell(excelRow, 1).Value = row.ApplicationNo ?? "";
                worksheet.Cell(excelRow, 2).Value = $"{row.FirstName} {row.LastName}".Trim();
                worksheet.Cell(excelRow, 3).Value = row.District ?? "";
                worksheet.Cell(excelRow, 4).Value = row.Email;
                worksheet.Cell(excelRow, 5).Value = row.PhoneNumber;
                worksheet.Cell(excelRow, 6).Value = row.BatchTitle ?? "";
                worksheet.Cell(excelRow, 7).Value = row.Status;
            }

            worksheet.Columns().AdjustToContents();
        }
    }
}
