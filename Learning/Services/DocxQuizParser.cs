using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LearningBackendAPI.Services
{
    public class ParsedQuizQuestion
    {
        public int DocumentNumber { get; set; }
        public string QuestionText { get; set; } = "";
        public string OptionA { get; set; } = "";
        public string OptionB { get; set; } = "";
        public string OptionC { get; set; } = "";
        public string OptionD { get; set; } = "";
        public string CorrectOption { get; set; } = "";
    }

    public class ParsedQuizIssue
    {
        public int DocumentNumber { get; set; }
        public string Reason { get; set; } = "";
    }

    public class DocxQuizParseResult
    {
        public int TotalFound { get; set; }
        public List<ParsedQuizQuestion> Questions { get; set; } = new();
        public List<ParsedQuizIssue> Skipped { get; set; } = new();
        public List<ParsedQuizIssue> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Reads questions out of a Word (.docx) file laid out as:
    ///   1. Question text
    ///   A) option  B) option ✔️  C) option  D) option
    /// A question starts with "N." at the beginning of a line, options are marked A) B) C) D) (inline or
    /// on separate lines; option D is optional), and the correct option carries a ✔ mark. "(விளக்கம்: ...)" explanations are
    /// ignored. Anything that doesn't fit is reported in Skipped rather than guessed.
    /// </summary>
    public static class DocxQuizParser
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        private static readonly Regex QuestionStart = new(@"(?m)^[ \t]*(\d{1,3})\.[ \t]+", RegexOptions.Compiled);
        private static readonly Regex Explanation = new(@"\((?:விளக்கம்|Explanation)\s*:.*?\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AnswerMark = new("[✔✅✓☑]️?", RegexOptions.Compiled);

        public static DocxQuizParseResult Parse(Stream docx)
        {
            var text = ExtractText(docx);
            text = Explanation.Replace(text, "");

            var result = new DocxQuizParseResult();
            var starts = QuestionStart.Matches(text);
            result.TotalFound = starts.Count;

            for (var i = 0; i < starts.Count; i++)
            {
                var number = int.Parse(starts[i].Groups[1].Value);
                var bodyStart = starts[i].Index + starts[i].Length;
                var bodyEnd = i + 1 < starts.Count ? starts[i + 1].Index : text.Length;
                ParseBlock(number, text.Substring(bodyStart, bodyEnd - bodyStart), result);
            }

            return result;
        }

        private static void ParseBlock(int number, string block, DocxQuizParseResult result)
        {
            // Absolute start/end positions of the "A)" "B)" "C)" "D)" markers within the block
            var markerStart = new int[4];
            var markerEnd = new int[4];
            var cursor = 0;
            for (var i = 0; i < 4; i++)
            {
                var letter = (char)('A' + i);
                var match = Regex.Match(block.Substring(cursor), $@"(?<![A-Za-z]){letter}\)");
                if (!match.Success && i == 3)
                {
                    // Option D is optional - treat a missing marker as an empty option D
                    markerStart[3] = block.Length;
                    markerEnd[3] = block.Length;
                    break;
                }
                if (!match.Success)
                {
                    var found = string.Concat(Enumerable.Range(0, i).Select(n => (char)('A' + n)));
                    result.Skipped.Add(new ParsedQuizIssue
                    {
                        DocumentNumber = number,
                        Reason = i == 0 ? "No options (A, B, C, D) found" : $"Option {letter} not found (only {found} found)"
                    });
                    return;
                }
                markerStart[i] = cursor + match.Index;
                markerEnd[i] = markerStart[i] + match.Length;
                cursor = markerEnd[i];
            }

            var question = Collapse(block.Substring(0, markerStart[0]));
            if (question.Length == 0)
            {
                result.Skipped.Add(new ParsedQuizIssue { DocumentNumber = number, Reason = "Question text is empty" });
                return;
            }

            var options = new string[4];
            var marked = new List<char>();
            var formulaLike = false;
            for (var i = 0; i < 4; i++)
            {
                var from = markerEnd[i];
                var to = i < 3 ? markerStart[i + 1] : block.Length;
                var segment = block.Substring(from, to - from);

                if (AnswerMark.IsMatch(segment))
                {
                    marked.Add((char)('A' + i));
                    segment = AnswerMark.Replace(segment, " ");
                }

                // An option is its first non-empty line - anything after it (e.g. the next section
                // heading following the last option) is not part of the option.
                var line = segment.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
                line = Collapse(line);

                var deduped = DeduplicateRepeatedText(line);
                if (deduped != line)
                {
                    formulaLike = true;
                    line = deduped;
                }

                if (line.Length == 0 && i == 3)
                {
                    // Option D may be empty, but it can't be the marked correct answer
                    if (marked.Contains('D'))
                    {
                        result.Skipped.Add(new ParsedQuizIssue { DocumentNumber = number, Reason = "Option D is marked correct but has no text" });
                        return;
                    }
                    result.Warnings.Add(new ParsedQuizIssue { DocumentNumber = number, Reason = "Option D is missing - imported with D empty" });
                }
                else if (line.Length == 0)
                {
                    result.Skipped.Add(new ParsedQuizIssue { DocumentNumber = number, Reason = $"Option {(char)('A' + i)} is empty" });
                    return;
                }
                options[i] = line;
            }

            if (marked.Count != 1)
            {
                result.Skipped.Add(new ParsedQuizIssue
                {
                    DocumentNumber = number,
                    Reason = marked.Count == 0 ? "No correct answer (✔) marked" : $"More than one correct answer marked ({string.Join(",", marked)})"
                });
                return;
            }

            if (formulaLike)
            {
                result.Warnings.Add(new ParsedQuizIssue
                {
                    DocumentNumber = number,
                    Reason = "Option text looked like a pasted formula (duplicated text); superscripts/fractions may be lost - please check"
                });
            }

            result.Questions.Add(new ParsedQuizQuestion
            {
                DocumentNumber = number,
                QuestionText = question,
                OptionA = options[0],
                OptionB = options[1],
                OptionC = options[2],
                OptionD = options[3],
                CorrectOption = marked[0].ToString()
            });
        }

        // Formulas pasted from the web come through as the same text twice ("P(r/100)2P(r/100)2")
        private static string DeduplicateRepeatedText(string value)
        {
            if (value.Length >= 6 && value.Length % 2 == 0)
            {
                var half = value.Length / 2;
                if (string.Equals(value.Substring(0, half), value.Substring(half), StringComparison.Ordinal))
                {
                    return value.Substring(0, half);
                }
            }
            return value;
        }

        private static string Collapse(string value) => Regex.Replace(value, @"\s+", " ").Trim();

        private static string ExtractText(Stream docx)
        {
            using var archive = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry("word/document.xml")
                ?? throw new InvalidOperationException("This is not a valid .docx file");

            XDocument document;
            using (var stream = entry.Open())
            {
                document = XDocument.Load(stream);
            }

            var builder = new StringBuilder();
            foreach (var paragraph in document.Descendants(W + "p"))
            {
                foreach (var node in paragraph.Descendants())
                {
                    if (node.Name == W + "t")
                    {
                        builder.Append(node.Value);
                    }
                    else if (node.Name == W + "br" || node.Name == W + "cr")
                    {
                        builder.Append('\n');
                    }
                    else if (node.Name == W + "tab")
                    {
                        builder.Append(' ');
                    }
                }
                builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
