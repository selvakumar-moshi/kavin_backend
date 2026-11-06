namespace LearningBackendAPI.Services
{
    // What a student's verified syllabus wise payments unlock: each paid plan covers a folder (or sub folder)
    // and everything nested below it; a plan with no folder covers everything.
    public class SyllabusAccess
    {
        public bool All { get; set; }
        public HashSet<string> FolderIds { get; } = new();
        public HashSet<string> SubFolderIds { get; } = new();

        public bool Covers(string? folderId, string? subFolderId) => All
            || (folderId != null && FolderIds.Contains(folderId))
            || (subFolderId != null && SubFolderIds.Contains(subFolderId));
    }
}
