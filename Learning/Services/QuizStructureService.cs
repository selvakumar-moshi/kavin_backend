using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class QuizStructureService : IQuizStructureService
    {
        private readonly IQuizStructureRepository _repository;
        private readonly IQuizRepository _quizRepository;

        public QuizStructureService(IQuizStructureRepository repository, IQuizRepository quizRepository)
        {
            _repository = repository;
            _quizRepository = quizRepository;
        }

        // ---------- Subjects ----------

        public async Task<List<QuizSubjectResponse>> GetSubjectsAsync()
            => await ToResponsesAsync(await _repository.GetAllAsync<QuizSubject>());

        public async Task<QuizSubjectResponse> CreateSubjectAsync(QuizSubjectRequest request)
        {
            var name = RequireName(request.Name, "Subject name");
            var subjects = await _repository.GetAllAsync<QuizSubject>();
            EnsureUnique(subjects.Select(s => s.Name), name, "subject");

            var subject = new QuizSubject
            {
                Name = name,
                Standards = await ResolveLinksAsync(request.Standards)
            };
            return (await ToResponsesAsync(new[] { await _repository.CreateAsync(subject) }))[0];
        }

        public async Task<QuizSubjectResponse> UpdateSubjectAsync(string id, QuizSubjectRequest request)
        {
            var subject = await FindAsync<QuizSubject>(id, "Subject");

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim() != subject.Name)
            {
                var name = request.Name.Trim();
                var subjects = await _repository.GetAllAsync<QuizSubject>();
                EnsureUnique(subjects.Where(s => s.Id != id).Select(s => s.Name), name, "subject");
                await EnsureNotUsedByQuizzesAsync(q => Same(q.Subject, subject.Name), $"Subject '{subject.Name}'", "renamed");
                subject.Name = name;
            }

            if (request.Standards != null)
            {
                var links = await ResolveLinksAsync(request.Standards);
                if (links.Count > 0 && (await _repository.GetAllAsync<QuizCategory>()).Any(c => c.SubjectId == id))
                {
                    throw new InvalidOperationException($"'{subject.Name}' has categories, so its standards belong to the categories - link them there");
                }
                subject.Standards = links;
            }

            await _repository.UpdateAsync(subject);
            return (await ToResponsesAsync(new[] { subject }))[0];
        }

        public async Task DeleteSubjectAsync(string id)
        {
            var subject = await FindAsync<QuizSubject>(id, "Subject");
            if ((await _repository.GetAllAsync<QuizCategory>()).Any(c => c.SubjectId == id))
            {
                throw new InvalidOperationException($"Subject '{subject.Name}' still has categories - delete them first");
            }
            await EnsureNotUsedByQuizzesAsync(q => Same(q.Subject, subject.Name), $"Subject '{subject.Name}'", "deleted");
            await _repository.DeleteAsync<QuizSubject>(id);
        }

        // ---------- Categories ----------

        public async Task<List<QuizCategoryResponse>> GetCategoriesAsync(string? subject)
        {
            var categories = await _repository.GetAllAsync<QuizCategory>();
            if (!string.IsNullOrWhiteSpace(subject))
            {
                var matched = (await _repository.GetAllAsync<QuizSubject>()).FirstOrDefault(s => Same(s.Name, subject));
                categories = categories.Where(c => c.SubjectId == matched?.Id).ToList();
            }
            return await ToResponsesAsync(categories);
        }

        public async Task<QuizCategoryResponse> CreateCategoryAsync(QuizCategoryRequest request)
        {
            var name = RequireName(request.Name, "Category name");
            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                throw new InvalidOperationException("Subject is required for a category");
            }
            var subject = (await _repository.GetAllAsync<QuizSubject>()).FirstOrDefault(s => Same(s.Name, request.Subject))
                ?? throw new KeyNotFoundException($"Subject '{request.Subject.Trim()}' not found");
            if (subject.Standards.Count > 0)
            {
                throw new InvalidOperationException($"'{subject.Name}' already has standards linked directly - unlink them before adding categories");
            }

            var categories = await _repository.GetAllAsync<QuizCategory>();
            EnsureUnique(categories.Where(c => c.SubjectId == subject.Id).Select(c => c.Name), name, $"category in {subject.Name}");

            var category = new QuizCategory
            {
                Name = name,
                SubjectId = subject.Id,
                Standards = await ResolveLinksAsync(request.Standards)
            };
            return (await ToResponsesAsync(new[] { await _repository.CreateAsync(category) }))[0];
        }

        public async Task<QuizCategoryResponse> UpdateCategoryAsync(string id, QuizCategoryRequest request)
        {
            var category = await FindAsync<QuizCategory>(id, "Category");
            var subject = await FindAsync<QuizSubject>(category.SubjectId, "Subject");

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim() != category.Name)
            {
                var name = request.Name.Trim();
                var categories = await _repository.GetAllAsync<QuizCategory>();
                EnsureUnique(categories.Where(c => c.SubjectId == category.SubjectId && c.Id != id).Select(c => c.Name), name, $"category in {subject.Name}");
                await EnsureNotUsedByQuizzesAsync(q => Same(q.Subject, subject.Name) && Same(q.Category, category.Name), $"Category '{category.Name}'", "renamed");
                category.Name = name;
            }

            if (request.Standards != null)
            {
                category.Standards = await ResolveLinksAsync(request.Standards);
            }

            await _repository.UpdateAsync(category);
            return (await ToResponsesAsync(new[] { category }))[0];
        }

        public async Task DeleteCategoryAsync(string id)
        {
            var category = await FindAsync<QuizCategory>(id, "Category");
            var subject = await FindAsync<QuizSubject>(category.SubjectId, "Subject");
            await EnsureNotUsedByQuizzesAsync(q => Same(q.Subject, subject.Name) && Same(q.Category, category.Name), $"Category '{category.Name}'", "deleted");
            await _repository.DeleteAsync<QuizCategory>(id);
        }

        // ---------- Standards ----------

        public async Task<List<QuizStandardResponse>> GetStandardsAsync()
            => (await _repository.GetAllAsync<QuizStandard>()).OrderBy(s => s.Number).Select(ToResponse).ToList();

        public async Task<QuizStandardResponse> CreateStandardAsync(QuizStandardRequest request)
        {
            var number = RequireNumber(request.Standard);
            var standards = await _repository.GetAllAsync<QuizStandard>();
            if (standards.Any(s => s.Number == number))
            {
                throw new InvalidOperationException($"Standard {number} already exists");
            }
            return ToResponse(await _repository.CreateAsync(new QuizStandard { Number = number }));
        }

        public async Task<QuizStandardResponse> UpdateStandardAsync(string id, QuizStandardRequest request)
        {
            var standard = await FindAsync<QuizStandard>(id, "Standard");

            if (request.Standard != null && request.Standard != standard.Number)
            {
                var number = RequireNumber(request.Standard);
                var standards = await _repository.GetAllAsync<QuizStandard>();
                if (standards.Any(s => s.Id != id && s.Number == number))
                {
                    throw new InvalidOperationException($"Standard {number} already exists");
                }
                await EnsureNotUsedByQuizzesAsync(q => q.Standard == standard.Number, $"Standard {standard.Number}", "renumbered");
                standard.Number = number;
                await _repository.UpdateAsync(standard);
            }
            return ToResponse(standard);
        }

        public async Task DeleteStandardAsync(string id)
        {
            var standard = await FindAsync<QuizStandard>(id, "Standard");
            var linked = (await _repository.GetAllAsync<QuizSubject>()).Any(s => s.Standards.Any(l => l.StandardId == id))
                || (await _repository.GetAllAsync<QuizCategory>()).Any(c => c.Standards.Any(l => l.StandardId == id));
            if (linked)
            {
                throw new InvalidOperationException($"Standard {standard.Number} is linked to a subject or category - unlink it first");
            }
            await EnsureNotUsedByQuizzesAsync(q => q.Standard == standard.Number, $"Standard {standard.Number}", "deleted");
            await _repository.DeleteAsync<QuizStandard>(id);
        }

        // ---------- Parts ----------

        public async Task<List<QuizPartResponse>> GetPartsAsync()
            => (await _repository.GetAllAsync<QuizPart>()).Select(ToResponse).ToList();

        public async Task<QuizPartResponse> CreatePartAsync(QuizPartRequest request)
        {
            var name = RequireName(request.Name, "Part name");
            var parts = await _repository.GetAllAsync<QuizPart>();
            EnsureUnique(parts.Select(p => p.Name), name, "part");
            return ToResponse(await _repository.CreateAsync(new QuizPart { Name = name }));
        }

        public async Task<QuizPartResponse> UpdatePartAsync(string id, QuizPartRequest request)
        {
            var part = await FindAsync<QuizPart>(id, "Part");

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim() != part.Name)
            {
                var name = request.Name.Trim();
                var parts = await _repository.GetAllAsync<QuizPart>();
                EnsureUnique(parts.Where(p => p.Id != id).Select(p => p.Name), name, "part");
                await EnsureNotUsedByQuizzesAsync(q => Same(q.Part, part.Name), $"Part '{part.Name}'", "renamed");
                part.Name = name;
                await _repository.UpdateAsync(part);
            }
            return ToResponse(part);
        }

        public async Task DeletePartAsync(string id)
        {
            var part = await FindAsync<QuizPart>(id, "Part");
            var linked = (await _repository.GetAllAsync<QuizSubject>()).Any(s => s.Standards.Any(l => l.PartIds.Contains(id)))
                || (await _repository.GetAllAsync<QuizCategory>()).Any(c => c.Standards.Any(l => l.PartIds.Contains(id)));
            if (linked)
            {
                throw new InvalidOperationException($"Part '{part.Name}' is used by a subject or category - remove it there first");
            }
            await EnsureNotUsedByQuizzesAsync(q => Same(q.Part, part.Name), $"Part '{part.Name}'", "deleted");
            await _repository.DeleteAsync<QuizPart>(id);
        }

        // ---------- Tree + validation ----------

        public async Task<object> GetTreeAsync()
        {
            var data = await LoadAsync();

            List<object> StandardList(IEnumerable<QuizStandardLink> links) => links
                .Select(l => (Standard: data.StandardsById.GetValueOrDefault(l.StandardId), Link: l))
                .Where(x => x.Standard != null)
                .OrderBy(x => x.Standard!.Number)
                .Select(x => (object)new
                {
                    standard = x.Standard!.Number,
                    parts = x.Link.PartIds
                        .Select(pid => data.PartsById.GetValueOrDefault(pid)?.Name)
                        .Where(n => n != null)
                        .ToList()
                })
                .ToList();

            return new
            {
                subjects = data.Subjects.Select(s => new
                {
                    subject = s.Name,
                    categories = data.Categories
                        .Where(c => c.SubjectId == s.Id)
                        .Select(c => new { category = c.Name, standards = StandardList(c.Standards) })
                        .ToList(),
                    standards = StandardList(s.Standards)
                }).ToList()
            };
        }

        public async Task<QuizClassification> ValidateAsync(string? subject, string? category, int? standard, string? part)
        {
            var hasSubject = !string.IsNullOrWhiteSpace(subject);
            var hasCategory = !string.IsNullOrWhiteSpace(category);
            var hasPart = !string.IsNullOrWhiteSpace(part);

            if (!hasSubject && !hasCategory && standard == null && !hasPart)
            {
                return new QuizClassification();
            }
            if (!hasSubject)
            {
                throw new InvalidOperationException("Subject is required");
            }

            var data = await LoadAsync();

            var matchedSubject = data.Subjects.FirstOrDefault(s => Same(s.Name, subject))
                ?? throw new InvalidOperationException($"Subject must be one of: {Join(data.Subjects.Select(s => s.Name))}");

            var categories = data.Categories.Where(c => c.SubjectId == matchedSubject.Id).ToList();
            QuizCategory? matchedCategory = null;
            List<QuizStandardLink> links;
            if (categories.Count > 0)
            {
                if (!hasCategory)
                {
                    throw new InvalidOperationException($"Category is required for {matchedSubject.Name} ({Join(categories.Select(c => c.Name))})");
                }
                matchedCategory = categories.FirstOrDefault(c => Same(c.Name, category))
                    ?? throw new InvalidOperationException($"Category must be one of: {Join(categories.Select(c => c.Name))}");
                links = matchedCategory.Standards;
            }
            else
            {
                if (hasCategory)
                {
                    throw new InvalidOperationException($"{matchedSubject.Name} has no category");
                }
                links = matchedSubject.Standards;
            }

            var offered = links
                .Where(l => data.StandardsById.ContainsKey(l.StandardId))
                .Select(l => (Number: data.StandardsById[l.StandardId].Number, Link: l))
                .OrderBy(x => x.Number)
                .ToList();

            if (standard == null)
            {
                throw new InvalidOperationException($"Standard is required ({Join(offered.Select(x => x.Number.ToString()))})");
            }
            var matched = offered.FirstOrDefault(x => x.Number == standard);
            if (matched.Link == null)
            {
                throw new InvalidOperationException($"Standard must be one of: {Join(offered.Select(x => x.Number.ToString()))}");
            }

            var parts = matched.Link.PartIds
                .Select(id => data.PartsById.GetValueOrDefault(id))
                .Where(p => p != null)
                .Select(p => p!)
                .ToList();

            string? matchedPart = null;
            if (parts.Count > 0)
            {
                if (!hasPart)
                {
                    throw new InvalidOperationException($"Part is required for standard {standard} ({Join(parts.Select(p => p.Name))})");
                }
                matchedPart = (parts.FirstOrDefault(p => Same(p.Name, part)) ?? MatchPartByNumber(parts, part!))?.Name
                    ?? throw new InvalidOperationException($"Part must be one of: {Join(parts.Select(p => p.Name))}");
            }
            else if (hasPart)
            {
                throw new InvalidOperationException($"Standard {standard} has no parts for {matchedSubject.Name}");
            }

            return new QuizClassification
            {
                Subject = matchedSubject.Name,
                Category = matchedCategory?.Name,
                Standard = matched.Number,
                Part = matchedPart
            };
        }

        public async Task SeedDefaultsAsync()
        {
            await MigrateLegacyLinksAsync();

            if ((await _repository.GetAllAsync<QuizSubject>()).Count > 0
                || (await _repository.GetAllAsync<QuizCategory>()).Count > 0
                || (await _repository.GetAllAsync<QuizStandard>()).Count > 0
                || (await _repository.GetAllAsync<QuizPart>()).Count > 0)
            {
                return;
            }

            var partIds = new List<string>();
            foreach (var name in new[] { "Part-1", "Part-2", "Part-3" })
            {
                partIds.Add((await _repository.CreateAsync(new QuizPart { Name = name })).Id);
            }

            var standardIdsByNumber = new Dictionary<int, string>();
            foreach (var number in new[] { 6, 7, 8, 9, 10, 11, 12 })
            {
                standardIdsByNumber[number] = (await _repository.CreateAsync(new QuizStandard { Number = number })).Id;
            }

            // Standards 6 and 7 have the three parts; 8-12 have none
            List<QuizStandardLink> DefaultLinks() => standardIdsByNumber
                .Select(kv => new QuizStandardLink
                {
                    StandardId = kv.Value,
                    PartIds = kv.Key <= 7 ? new List<string>(partIds) : new List<string>()
                }).ToList();

            await _repository.CreateAsync(new QuizSubject { Name = "Tamil", Standards = DefaultLinks() });
            var gk = await _repository.CreateAsync(new QuizSubject { Name = "GK" });
            foreach (var name in new[] { "Social", "Science" })
            {
                await _repository.CreateAsync(new QuizCategory { Name = name, SubjectId = gk.Id, Standards = DefaultLinks() });
            }
        }

        // One-time: the first version kept parts on the standard (shared by every subject). Copy those
        // parts onto each subject/category's own link, so nothing changes for existing data.
        private async Task MigrateLegacyLinksAsync()
        {
            var subjects = await _repository.GetAllAsync<QuizSubject>();
            var categories = await _repository.GetAllAsync<QuizCategory>();
            var standards = (await _repository.GetAllAsync<QuizStandard>()).ToDictionary(s => s.Id);

            List<QuizStandardLink> ToLinks(List<string> standardIds) => standardIds
                .Where(standards.ContainsKey)
                .Select(id => new QuizStandardLink { StandardId = id, PartIds = new List<string>(standards[id].LegacyPartIds ?? new List<string>()) })
                .ToList();

            foreach (var subject in subjects.Where(s => s.LegacyStandardIds is { Count: > 0 } && s.Standards.Count == 0))
            {
                subject.Standards = ToLinks(subject.LegacyStandardIds!);
                subject.LegacyStandardIds = null;
                await _repository.UpdateAsync(subject);
            }
            foreach (var category in categories.Where(c => c.LegacyStandardIds is { Count: > 0 } && c.Standards.Count == 0))
            {
                category.Standards = ToLinks(category.LegacyStandardIds!);
                category.LegacyStandardIds = null;
                await _repository.UpdateAsync(category);
            }
        }

        // ---------- Helpers ----------

        private sealed record StructureData(
            List<QuizSubject> Subjects,
            List<QuizCategory> Categories,
            Dictionary<string, QuizStandard> StandardsById,
            Dictionary<string, QuizPart> PartsById);

        private async Task<StructureData> LoadAsync()
        {
            var standards = await _repository.GetAllAsync<QuizStandard>();
            var parts = await _repository.GetAllAsync<QuizPart>();
            return new StructureData(
                await _repository.GetAllAsync<QuizSubject>(),
                await _repository.GetAllAsync<QuizCategory>(),
                standards.ToDictionary(s => s.Id),
                parts.ToDictionary(p => p.Id));
        }

        private async Task<T> FindAsync<T>(string id, string label) where T : QuizStructureItem
        {
            var all = await _repository.GetAllAsync<T>();
            return all.FirstOrDefault(x => x.Id == id)
                ?? throw new KeyNotFoundException($"{label} not found");
        }

        // Turns [{ standard: 6, parts: ["Part-1"] }, { standard: 12 }] into stored links. Every standard and
        // part must already exist; a standard may be listed once; a standard without parts has none.
        private async Task<List<QuizStandardLink>> ResolveLinksAsync(List<QuizStandardLinkRequest>? requested)
        {
            if (requested == null || requested.Count == 0)
            {
                return new List<QuizStandardLink>();
            }

            var standards = await _repository.GetAllAsync<QuizStandard>();
            var parts = await _repository.GetAllAsync<QuizPart>();
            var links = new List<QuizStandardLink>();

            foreach (var item in requested)
            {
                if (item.Standard == null)
                {
                    throw new InvalidOperationException("Each entry in standards needs a \"standard\" number");
                }
                var standard = standards.FirstOrDefault(s => s.Number == item.Standard)
                    ?? throw new InvalidOperationException($"Standard {item.Standard} not found - create it first");
                if (links.Any(l => l.StandardId == standard.Id))
                {
                    throw new InvalidOperationException($"Standard {item.Standard} is listed more than once");
                }

                var partIds = new List<string>();
                foreach (var name in (item.Parts ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    var part = parts.FirstOrDefault(p => Same(p.Name, name))
                        ?? throw new InvalidOperationException($"Part '{name.Trim()}' not found - create it first");
                    if (!partIds.Contains(part.Id))
                    {
                        partIds.Add(part.Id);
                    }
                }
                links.Add(new QuizStandardLink { StandardId = standard.Id, PartIds = partIds });
            }
            return links;
        }

        private async Task<List<QuizSubjectResponse>> ToResponsesAsync(IEnumerable<QuizSubject> subjects)
        {
            var (standards, parts) = await NamesAsync();
            return subjects.Select(s => new QuizSubjectResponse
            {
                Id = s.Id,
                Name = s.Name,
                Standards = ToLinkResponses(s.Standards, standards, parts)
            }).ToList();
        }

        private async Task<List<QuizCategoryResponse>> ToResponsesAsync(IEnumerable<QuizCategory> categories)
        {
            var (standards, parts) = await NamesAsync();
            var subjects = (await _repository.GetAllAsync<QuizSubject>()).ToDictionary(s => s.Id, s => s.Name);
            return categories.Select(c => new QuizCategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Subject = subjects.GetValueOrDefault(c.SubjectId, ""),
                Standards = ToLinkResponses(c.Standards, standards, parts)
            }).ToList();
        }

        private async Task<(Dictionary<string, int> Standards, Dictionary<string, string> Parts)> NamesAsync()
        {
            var standards = (await _repository.GetAllAsync<QuizStandard>()).ToDictionary(s => s.Id, s => s.Number);
            var parts = (await _repository.GetAllAsync<QuizPart>()).ToDictionary(p => p.Id, p => p.Name);
            return (standards, parts);
        }

        private static List<QuizStandardLinkResponse> ToLinkResponses(
            IEnumerable<QuizStandardLink> links, Dictionary<string, int> standards, Dictionary<string, string> parts)
            => links
                .Where(l => standards.ContainsKey(l.StandardId))
                .OrderBy(l => standards[l.StandardId])
                .Select(l => new QuizStandardLinkResponse
                {
                    Standard = standards[l.StandardId],
                    Parts = l.PartIds.Where(parts.ContainsKey).Select(id => parts[id]).ToList()
                }).ToList();

        private static QuizStandardResponse ToResponse(QuizStandard standard) => new() { Id = standard.Id, Standard = standard.Number };

        private static QuizPartResponse ToResponse(QuizPart part) => new() { Id = part.Id, Name = part.Name };

        // Quizzes store the subject/category/standard/part values themselves, so one that is in use can't be renamed or removed
        private async Task EnsureNotUsedByQuizzesAsync(Func<Quiz, bool> isUsing, string what, string action)
        {
            var count = (await _quizRepository.GetAllAsync()).Count(isUsing);
            if (count > 0)
            {
                throw new InvalidOperationException($"{what} is used by {count} quiz(zes) and can't be {action}");
            }
        }

        private static string RequireName(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{label} is required");
            }
            return value.Trim();
        }

        private static int RequireNumber(int? number)
        {
            if (number == null || number <= 0)
            {
                throw new InvalidOperationException("Standard number must be a positive number");
            }
            return number.Value;
        }

        private static void EnsureUnique(IEnumerable<string> existing, string name, string label)
        {
            if (existing.Any(e => Same(e, name)))
            {
                throw new InvalidOperationException($"A {label} named '{name}' already exists");
            }
        }

        private static bool Same(string? a, string? b)
            => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string Join(IEnumerable<string> values) => string.Join(", ", values);

        // "1" or "part 1" -> the part named "Part-1"
        private static QuizPart? MatchPartByNumber(List<QuizPart> parts, string value)
        {
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length == 0 ? null : parts.FirstOrDefault(p => Same(p.Name, $"Part-{digits}"));
        }
    }
}
