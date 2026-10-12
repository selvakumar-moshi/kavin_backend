using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;

namespace LearningBackendAPI.Services
{
    public class FolderService : IFolderService
    {
        private const int MaxDepth = 10;

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
            return folders.Select(f => ToResponse(f, subFolders)).ToList();
        }

        public async Task<FolderResponse> CreateFolderAsync(FolderRequest request)
        {
            var name = RequireName(request.Name, "Folder name");
            var folders = await _repository.GetAllAsync<QuizFolder>();
            if (folders.Any(f => Same(f.Name, name)))
            {
                throw new InvalidOperationException($"A folder named '{name}' already exists");
            }
            // Check the whole tree first so a bad branch doesn't leave a half-created folder behind
            ValidateTree(request.SubFolders, name, 2);

            var folder = await _repository.CreateAsync(new QuizFolder { Name = name });
            return new FolderResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                SubFolders = await CreateTreeAsync(folder.Id, request.SubFolders)
            };
        }

        public async Task<FolderResponse> UpdateFolderAsync(string id, FolderRequest request)
        {
            var folder = await FindAsync<QuizFolder>(id, "Folder");

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

            if (request.SubFolders is { Count: > 0 })
            {
                await AddTreeAsync(folder.Id, folder.Name, request.SubFolders, 2);
            }
            return ToResponse(folder, await _repository.GetAllAsync<QuizSubFolder>());
        }

        public async Task DeleteFolderAsync(string id)
        {
            var folder = await FindAsync<QuizFolder>(id, "Folder");
            if ((await _repository.GetAllAsync<QuizSubFolder>()).Any(s => s.FolderId == id))
            {
                throw new InvalidOperationException($"Folder '{folder.Name}' still has sub folders - delete them first");
            }
            await EnsureNotUsedByQuizzesAsync(q => q.FolderId == id, $"Folder '{folder.Name}'", "can't be deleted");
            await _repository.DeleteAsync<QuizFolder>(id);
        }

        // ---------- Sub folders ----------

        public async Task<List<SubFolderResponse>> GetSubFoldersAsync(string? folderId)
        {
            var all = await _repository.GetAllAsync<QuizSubFolder>();
            // With folderId: that parent's direct children (each carrying its own nested sub folders)
            var listed = string.IsNullOrWhiteSpace(folderId)
                ? all
                : all.Where(s => s.FolderId == folderId).ToList();
            return listed.Select(s => ToResponse(s, all)).ToList();
        }

        public async Task<SubFolderResponse> CreateSubFolderAsync(SubFolderRequest request)
        {
            var name = RequireName(request.Name, "Sub folder name");
            if (string.IsNullOrWhiteSpace(request.FolderId))
            {
                throw new InvalidOperationException("Folder is required for a sub folder");
            }
            var parentId = request.FolderId.Trim();
            var all = await _repository.GetAllAsync<QuizSubFolder>();

            // The parent is a folder, or another sub folder (any depth)
            string parentName;
            var parentFolder = (await _repository.GetAllAsync<QuizFolder>()).FirstOrDefault(f => f.Id == parentId);
            if (parentFolder != null)
            {
                parentName = parentFolder.Name;
            }
            else
            {
                var parentSub = all.FirstOrDefault(s => s.Id == parentId)
                    ?? throw new KeyNotFoundException("Folder not found");
                parentName = parentSub.Name;
                // Quizzes filed here point at this exact sub folder, so it has to stay the deepest level
                await EnsureNotUsedByQuizzesAsync(q => q.SubFolderId == parentId, $"Sub folder '{parentName}'", "can't get sub folders");
            }

            if (all.Any(s => s.FolderId == parentId && Same(s.Name, name)))
            {
                throw new InvalidOperationException($"'{parentName}' already has a sub folder named '{name}'");
            }
            ValidateTree(request.SubFolders, name, Depth(parentId, all) + 3);

            var subFolder = await _repository.CreateAsync(new QuizSubFolder { Name = name, FolderId = parentId });
            var response = ToResponse(subFolder);
            response.SubFolders = await CreateTreeAsync(subFolder.Id, request.SubFolders);
            return response;
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

            if (request.SubFolders is { Count: > 0 })
            {
                var all = await _repository.GetAllAsync<QuizSubFolder>();
                await AddTreeAsync(id, subFolder.Name, request.SubFolders, Depth(id, all) + 2);
            }
            return ToResponse(subFolder, await _repository.GetAllAsync<QuizSubFolder>());
        }

        public async Task DeleteSubFolderAsync(string id)
        {
            var subFolder = await FindAsync<QuizSubFolder>(id, "Sub folder");
            if ((await _repository.GetAllAsync<QuizSubFolder>()).Any(s => s.FolderId == id))
            {
                throw new InvalidOperationException($"Sub folder '{subFolder.Name}' still has sub folders - delete them first");
            }
            await EnsureNotUsedByQuizzesAsync(q => q.SubFolderId == id, $"Sub folder '{subFolder.Name}'", "can't be deleted");
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

            var all = await _repository.GetAllAsync<QuizSubFolder>();
            var topLevel = all.Where(s => s.FolderId == folder.Id).ToList();
            QuizSubFolder? subFolder = null;
            if (topLevel.Count > 0)
            {
                if (!hasSubFolder)
                {
                    throw new InvalidOperationException($"Sub folder is required for folder '{folder.Name}' ({string.Join(", ", topLevel.Select(s => s.Name))})");
                }
                subFolder = all.FirstOrDefault(s => s.Id == subFolderId!.Trim());
                if (subFolder == null || !IsInside(subFolder, folder.Id, all))
                {
                    throw new InvalidOperationException($"Sub folder not found in folder '{folder.Name}'");
                }

                // A quiz is filed under the deepest level, so a sub folder that has its own sub folders can't be picked
                var deeper = all.Where(s => s.FolderId == subFolder.Id).ToList();
                if (deeper.Count > 0)
                {
                    throw new InvalidOperationException($"Sub folder '{subFolder.Name}' has more sub folders - pick one of them ({string.Join(", ", deeper.Select(s => s.Name))})");
                }
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

        // The sub folder itself plus everything below it - so filtering on a middle level finds quizzes in its leaves
        public async Task<HashSet<string>> GetSubtreeIdsAsync(string subFolderId)
        {
            var all = await _repository.GetAllAsync<QuizSubFolder>();
            var ids = new HashSet<string> { subFolderId };
            var queue = new Queue<string>();
            queue.Enqueue(subFolderId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var child in all.Where(s => s.FolderId == current))
                {
                    if (ids.Add(child.Id))
                    {
                        queue.Enqueue(child.Id);
                    }
                }
            }
            return ids;
        }

        // ---------- Helpers ----------

        private async Task<T> FindAsync<T>(string id, string label) where T : QuizStructureItem
        {
            var all = await _repository.GetAllAsync<T>();
            return all.FirstOrDefault(x => x.Id == id)
                ?? throw new KeyNotFoundException($"{label} not found");
        }

        private async Task EnsureNotUsedByQuizzesAsync(Func<Quiz, bool> isUsing, string what, string restriction)
        {
            var count = (await _quizRepository.GetAllAsync()).Count(isUsing);
            if (count > 0)
            {
                throw new InvalidOperationException($"{what} is used by {count} quiz(zes) and {restriction}");
            }
        }

        // Adds a tree under an existing node. A name that already exists under a parent is reused (and its own
        // children are merged the same way), so sending the same tree twice doesn't duplicate anything.
        // Everything is checked before the first node is written.
        private async Task AddTreeAsync(string parentId, string parentName, List<SubFolderNodeRequest> nodes, int depth)
        {
            ValidateTree(nodes, parentName, depth);
            var all = await _repository.GetAllAsync<QuizSubFolder>();
            var usedByQuizzes = (await _quizRepository.GetAllAsync())
                .Where(q => q.SubFolderId != null).Select(q => q.SubFolderId!).ToHashSet();

            CheckMerge(parentId, parentName, nodes, all, usedByQuizzes);
            await MergeTreeAsync(parentId, nodes, all);
        }

        private static void CheckMerge(string parentId, string parentName, List<SubFolderNodeRequest>? nodes, List<QuizSubFolder> all, HashSet<string> used)
        {
            foreach (var node in nodes ?? new List<SubFolderNodeRequest>())
            {
                var name = node.Name!.Trim();
                var existing = all.FirstOrDefault(s => s.FolderId == parentId && Same(s.Name, name));
                if (existing == null)
                {
                    if (used.Contains(parentId))
                    {
                        throw new InvalidOperationException($"Sub folder '{parentName}' is used by quiz(zes) and can't get sub folders");
                    }
                    continue;
                }
                CheckMerge(existing.Id, existing.Name, node.SubFolders, all, used);
            }
        }

        private async Task MergeTreeAsync(string parentId, List<SubFolderNodeRequest>? nodes, List<QuizSubFolder> all)
        {
            foreach (var node in nodes ?? new List<SubFolderNodeRequest>())
            {
                var name = node.Name!.Trim();
                var existing = all.FirstOrDefault(s => s.FolderId == parentId && Same(s.Name, name));
                if (existing == null)
                {
                    var created = await _repository.CreateAsync(new QuizSubFolder { Name = name, FolderId = parentId });
                    await CreateTreeAsync(created.Id, node.SubFolders);
                }
                else
                {
                    await MergeTreeAsync(existing.Id, node.SubFolders, all);
                }
            }
        }

        // Names present, no duplicates among siblings, not absurdly deep. depth = level of the nodes' parent + 1
        private static void ValidateTree(List<SubFolderNodeRequest>? nodes, string parentName, int depth)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }
            if (depth > MaxDepth)
            {
                throw new InvalidOperationException($"Folders can be at most {MaxDepth} levels deep");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in nodes)
            {
                var name = RequireName(node.Name, "Sub folder name");
                if (!seen.Add(name))
                {
                    throw new InvalidOperationException($"'{parentName}' has more than one sub folder named '{name}'");
                }
                ValidateTree(node.SubFolders, name, depth + 1);
            }
        }

        // Creates the nodes and everything below them, parents before children
        private async Task<List<SubFolderResponse>> CreateTreeAsync(string parentId, List<SubFolderNodeRequest>? nodes)
        {
            var created = new List<SubFolderResponse>();
            foreach (var node in nodes ?? new List<SubFolderNodeRequest>())
            {
                var subFolder = await _repository.CreateAsync(new QuizSubFolder { Name = node.Name!.Trim(), FolderId = parentId });
                var response = ToResponse(subFolder);
                response.SubFolders = await CreateTreeAsync(subFolder.Id, node.SubFolders);
                created.Add(response);
            }
            return created;
        }

        // How many sub folder levels sit above the parent (a folder = 0, its sub folders = 1, ...)
        private static int Depth(string parentId, List<QuizSubFolder> all)
        {
            var depth = 0;
            var current = all.FirstOrDefault(s => s.Id == parentId);
            while (current != null && depth <= all.Count)
            {
                depth++;
                current = all.FirstOrDefault(s => s.Id == current.FolderId);
            }
            return depth;
        }

        // Walks up the parents; true when the sub folder sits anywhere under the folder
        private static bool IsInside(QuizSubFolder subFolder, string folderId, List<QuizSubFolder> all)
        {
            var current = subFolder;
            for (var i = 0; i <= all.Count && current != null; i++)
            {
                if (current.FolderId == folderId)
                {
                    return true;
                }
                current = all.FirstOrDefault(s => s.Id == current.FolderId);
            }
            return false;
        }

        // Nested responses: every sub folder carries its own children, down to the leaves
        private static FolderResponse ToResponse(QuizFolder folder, List<QuizSubFolder> all) => new()
        {
            Id = folder.Id,
            Name = folder.Name,
            SubFolders = all.Where(s => s.FolderId == folder.Id).Select(s => ToResponse(s, all)).ToList()
        };

        private static SubFolderResponse ToResponse(QuizSubFolder subFolder, List<QuizSubFolder> all)
        {
            var response = ToResponse(subFolder);
            response.SubFolders = all.Where(s => s.FolderId == subFolder.Id).Select(s => ToResponse(s, all)).ToList();
            return response;
        }

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
