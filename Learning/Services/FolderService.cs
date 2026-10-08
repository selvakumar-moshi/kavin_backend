using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;

namespace LearningBackendAPI.Services
{
    public class FolderService : IFolderService
    {
        private readonly IQuizStructureRepository _repository;
        private readonly IQuizRepository _quizRepository;

        public FolderService(IQuizStructureRepository repository, IQuizRepository quizRepository)
        {
            _repository = repository;
            _quizRepository = quizRepository;
        }

        // ---------- Folders ----------

        public async Task<List<FolderResponse>> GetFoldersAsync()
        {
            var folders = await _repository.GetAllAsync<QuizFolder>();
            var subFolders = await _repository.GetAllAsync<QuizSubFolder>();
            return folders.Select(f => ToResponse(f, subFolders.Where(s => s.FolderId == f.Id))).ToList();
        }

        public async Task<FolderResponse> CreateFolderAsync(FolderRequest request)
        {
            var name = RequireName(request.Name, "Folder name");
            var folders = await _repository.GetAllAsync<QuizFolder>();
            if (folders.Any(f => Same(f.Name, name)))
            {
                throw new InvalidOperationException($"A folder named '{name}' already exists");
            }
            var folder = await _repository.CreateAsync(new QuizFolder { Name = name });
            return ToResponse(folder, Array.Empty<QuizSubFolder>());
        }

        public async Task<FolderResponse> UpdateFolderAsync(string id, FolderRequest request)
        {
            var folder = await FindAsync<QuizFolder>(id, "Folder");
            var subFolders = (await _repository.GetAllAsync<QuizSubFolder>()).Where(s => s.FolderId == id).ToList();

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim() != folder.Name)
            {
                var name = request.Name.Trim();
                var folders = await _repository.GetAllAsync<QuizFolder>();
                if (folders.Any(f => f.Id != id && Same(f.Name, name)))
                {
                    throw new InvalidOperationException($"A folder named '{name}' already exists");
                }
                folder.Name = name;
                await _repository.UpdateAsync(folder);

                // Quizzes keep the folder name for display - keep it in step
                foreach (var quiz in (await _quizRepository.GetAllAsync()).Where(q => q.FolderId == id))
                {
                    quiz.FolderName = name;
                    await _quizRepository.UpdateAsync(quiz.Id, quiz);
                }
            }
            return ToResponse(folder, subFolders);
        }

        public async Task DeleteFolderAsync(string id)
        {
            var folder = await FindAsync<QuizFolder>(id, "Folder");
            if ((await _repository.GetAllAsync<QuizSubFolder>()).Any(s => s.FolderId == id))
            {
                throw new InvalidOperationException($"Folder '{folder.Name}' still has sub folders - delete them first");
            }
            await EnsureNotUsedByQuizzesAsync(q => q.FolderId == id, $"Folder '{folder.Name}'");
            await _repository.DeleteAsync<QuizFolder>(id);
        }

        // ---------- Sub folders ----------

        public async Task<List<SubFolderResponse>> GetSubFoldersAsync(string? folderId)
        {
            var subFolders = await _repository.GetAllAsync<QuizSubFolder>();
            if (!string.IsNullOrWhiteSpace(folderId))
            {
                subFolders = subFolders.Where(s => s.FolderId == folderId).ToList();
            }
            return subFolders.Select(ToResponse).ToList();
        }

        public async Task<SubFolderResponse> CreateSubFolderAsync(SubFolderRequest request)
        {
            var name = RequireName(request.Name, "Sub folder name");
            if (string.IsNullOrWhiteSpace(request.FolderId))
            {
                throw new InvalidOperationException("Folder is required for a sub folder");
            }
            var folder = await FindAsync<QuizFolder>(request.FolderId, "Folder");

            var siblings = (await _repository.GetAllAsync<QuizSubFolder>()).Where(s => s.FolderId == folder.Id);
            if (siblings.Any(s => Same(s.Name, name)))
            {
                throw new InvalidOperationException($"'{folder.Name}' already has a sub folder named '{name}'");
            }
            return ToResponse(await _repository.CreateAsync(new QuizSubFolder { Name = name, FolderId = folder.Id }));
        }

        public async Task<SubFolderResponse> UpdateSubFolderAsync(string id, SubFolderRequest request)
        {
            var subFolder = await FindAsync<QuizSubFolder>(id, "Sub folder");

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim() != subFolder.Name)
            {
                var name = request.Name.Trim();
                var siblings = (await _repository.GetAllAsync<QuizSubFolder>()).Where(s => s.FolderId == subFolder.FolderId && s.Id != id);
                if (siblings.Any(s => Same(s.Name, name)))
                {
                    throw new InvalidOperationException($"This folder already has a sub folder named '{name}'");
                }
                subFolder.Name = name;
                await _repository.UpdateAsync(subFolder);

                foreach (var quiz in (await _quizRepository.GetAllAsync()).Where(q => q.SubFolderId == id))
                {
                    quiz.SubFolderName = name;
                    await _quizRepository.UpdateAsync(quiz.Id, quiz);
                }
            }
            return ToResponse(subFolder);
        }

        public async Task DeleteSubFolderAsync(string id)
        {
            var subFolder = await FindAsync<QuizSubFolder>(id, "Sub folder");
            await EnsureNotUsedByQuizzesAsync(q => q.SubFolderId == id, $"Sub folder '{subFolder.Name}'");
            await _repository.DeleteAsync<QuizSubFolder>(id);
        }

        // ---------- Quiz validation ----------

        public async Task<FolderSelection> ValidateAsync(string? folderId, string? subFolderId)
        {
            var hasFolder = !string.IsNullOrWhiteSpace(folderId);
            var hasSubFolder = !string.IsNullOrWhiteSpace(subFolderId);

            if (!hasFolder && !hasSubFolder)
            {
                return new FolderSelection();
            }
            if (!hasFolder)
            {
                throw new InvalidOperationException("Folder is required when a sub folder is given");
            }

            var folders = await _repository.GetAllAsync<QuizFolder>();
            var folder = folders.FirstOrDefault(f => f.Id == folderId!.Trim())
                ?? throw new InvalidOperationException("Folder not found");

            var subFolders = (await _repository.GetAllAsync<QuizSubFolder>()).Where(s => s.FolderId == folder.Id).ToList();
            QuizSubFolder? subFolder = null;
            if (subFolders.Count > 0)
            {
                if (!hasSubFolder)
                {
                    throw new InvalidOperationException($"Sub folder is required for folder '{folder.Name}' ({string.Join(", ", subFolders.Select(s => s.Name))})");
                }
                subFolder = subFolders.FirstOrDefault(s => s.Id == subFolderId!.Trim())
                    ?? throw new InvalidOperationException($"Sub folder not found in folder '{folder.Name}'");
            }
            else if (hasSubFolder)
            {
                throw new InvalidOperationException($"Folder '{folder.Name}' has no sub folders");
            }

            return new FolderSelection
            {
                FolderId = folder.Id,
                FolderName = folder.Name,
                SubFolderId = subFolder?.Id,
                SubFolderName = subFolder?.Name
            };
        }

        // ---------- Helpers ----------

        private async Task<T> FindAsync<T>(string id, string label) where T : QuizStructureItem
        {
            var all = await _repository.GetAllAsync<T>();
            return all.FirstOrDefault(x => x.Id == id)
                ?? throw new KeyNotFoundException($"{label} not found");
        }

        private async Task EnsureNotUsedByQuizzesAsync(Func<Quiz, bool> isUsing, string what)
        {
            var count = (await _quizRepository.GetAllAsync()).Count(isUsing);
            if (count > 0)
            {
                throw new InvalidOperationException($"{what} is used by {count} quiz(zes) and can't be deleted");
            }
        }

        private static FolderResponse ToResponse(QuizFolder folder, IEnumerable<QuizSubFolder> subFolders) => new()
        {
            Id = folder.Id,
            Name = folder.Name,
            SubFolders = subFolders.Select(ToResponse).ToList()
        };

        private static SubFolderResponse ToResponse(QuizSubFolder subFolder) => new()
        {
            Id = subFolder.Id,
            Name = subFolder.Name,
            FolderId = subFolder.FolderId
        };

        private static string RequireName(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{label} is required");
            }
            return value.Trim();
        }

        private static bool Same(string? a, string? b)
            => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
