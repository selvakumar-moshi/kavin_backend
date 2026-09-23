using ClosedXML.Excel;
using LearningBackendAPI.DTOs;

namespace LearningBackendAPI.Services
{
    public class ExcelExportService : IExcelExportService
    {
        private static readonly string[] RankListHeaders =
        {
            "Student Name", "District", "Correct Answers", "Wrong Answers", "Total Questions", "Score","Rank"
        };

        public byte[] GenerateRankListExcel(string quizTitle, List<RankListEntryDto> rankList)
        {
            using var workbook = CreateRankListTemplate(quizTitle);
            var worksheet = workbook.Worksheet(1);

            FillRankListRows(worksheet, rankList);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static XLWorkbook CreateRankListTemplate(string quizTitle)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Rank List");

            var titleRange = worksheet.Range(1, 1, 1, RankListHeaders.Length);
            titleRange.Merge();
            worksheet.Cell(1, 1).Value = $"Rank List - {quizTitle}";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;

            for (var i = 0; i < RankListHeaders.Length; i++)
            {
                var cell = worksheet.Cell(3, i + 1);
                cell.Value = RankListHeaders[i];
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
                worksheet.Cell(row, 2).Value = entry.District ?? "";
                worksheet.Cell(row, 3).Value = entry.CorrectCount;
                worksheet.Cell(row, 4).Value = entry.TotalQuestions - entry.CorrectCount;
                worksheet.Cell(row, 5).Value = entry.TotalQuestions;
                worksheet.Cell(row, 6).Value = entry.Score;
                worksheet.Cell(row, 7).Value = entry.Rank;
            }

            worksheet.Columns().AdjustToContents();
        }
    }
}
