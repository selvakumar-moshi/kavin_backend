using Amazon.S3;
using Amazon.S3.Model;
using LearningBackendAPI.Config;
using Microsoft.Extensions.Options;

namespace LearningBackendAPI.Services
{
    public class S3FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly S3Settings _settings;

        public S3FileStorageService(IAmazonS3 s3Client, IOptions<S3Settings> settings)
        {
            _s3Client = s3Client;
            _settings = settings.Value;
        }

        public async Task<(string Url, string Key)> UploadPdfAsync(IFormFile file, string folder, string? fileName = null)
        {
            var resolvedFileName = SanitizeFileName(Path.GetFileName(fileName ?? file.FileName));
            if (!resolvedFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                resolvedFileName += ".pdf";
            }
            var key = $"{folder}/{resolvedFileName}";

            using var stream = file.OpenReadStream();
            var putRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = "application/pdf"
            };

            await _s3Client.PutObjectAsync(putRequest);

            var url = $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{key}";
            return (url, key);
        }

        public async Task<(string Url, string Key)> UploadImageAsync(IFormFile file, string folder, string? fileName = null)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => throw new InvalidOperationException("Only JPG, PNG, or WEBP images are allowed")
            };

            var resolvedFileName = SanitizeFileName(Path.GetFileName(fileName ?? file.FileName));
            var key = $"{folder}/{resolvedFileName}";

            using var stream = file.OpenReadStream();
            var putRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(putRequest);

            var url = $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{key}";
            return (url, key);
        }

        public async Task<(string Url, string Key)> UploadDocxAsync(IFormFile file, string folder, string? fileName = null)
        {
            var resolvedFileName = SanitizeFileName(Path.GetFileName(fileName ?? file.FileName));
            if (!resolvedFileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                resolvedFileName += ".docx";
            }
            var key = $"{folder}/{resolvedFileName}";

            using var stream = file.OpenReadStream();
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            });

            var url = $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{key}";
            return (url, key);
        }

        public async Task<(string Url, string Key)> CopyAsync(string sourceKey, string folder)
        {
            // Unique name so the copy never overwrites (or is overwritten by) another file
            var key = $"{folder}/{Path.GetFileName(sourceKey)}";

            await _s3Client.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = _settings.BucketName,
                SourceKey = sourceKey,
                DestinationBucket = _settings.BucketName,
                DestinationKey = key
            });

            var url = $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{key}";
            return (url, key);
        }

        public async Task DeleteAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key
            });
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Guid.NewGuid().ToString();
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(fileName.Select(c => invalidChars.Contains(c) || c == ' ' ? '_' : c).ToArray());
        }
    }
}
