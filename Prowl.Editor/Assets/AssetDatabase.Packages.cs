﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

using BepuPhysics.CollisionDetection;

using Prowl.Runtime;
using Prowl.Runtime.Utils;

using SemVersion;

namespace Prowl.Editor.Assets;

#warning TODO: Support other sources then just Github, like GitLab, BitBucket, etc, Could also look into NuGet packages

public class GithubPackageMetaData
{
    public string name { get; set; } = "";
    public string description { get; set; } = "";
    public string author { get; set; } = "";
    public string iconurl { get; set; } = "";
    public string license { get; set; } = "";
    public Repository repository { get; set; } = new();
    public string homepage { get; set; } = "";
    public Dictionary<string, string> dependencies { get; set; } = [];
}

public class Repository
{
    [JsonPropertyName("url")]
    public string githubPath { get; set; } = "";
}

public static partial class AssetDatabase
{
    private static DirectoryInfo s_packagesPath;
    private static readonly Dictionary<string, GithubPackageMetaData> s_installedPackages = new(StringComparer.OrdinalIgnoreCase);
    private static HttpClient s_httpClient;
    private static JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    #region Public Methods

    /// <summary>
    /// Exports all assets to build packages at the specified destination directory.
    /// </summary>
    /// <param name="destination">The destination directory.</param>
    public static void ExportAllBuildPackages(DirectoryInfo destination)
    {
        if (!destination.Exists)
        {
            Debug.LogError("Cannot export package, Folder does not exist.");
            return;
        }

        // Get all assets
        var assets = assetGuidToMeta.Keys.ToArray();
        ExportBuildPackages(assets, destination);
    }

    /// <summary>
    /// Exports build packages for the specified assets to the destination directory.
    /// </summary>
    /// <param name="assetsToExport">The assets to export.</param>
    /// <param name="destination">The destination directory.</param>
    public static void ExportBuildPackages(Guid[] assetsToExport, DirectoryInfo destination)
    {
        if (!destination.Exists)
        {
            Debug.LogError("Cannot export package, Folder does not exist.");
            return;
        }

        int packageIndex = 0;
        Debug.Log($"Creating First Package {packageIndex}");
        FileInfo firstPackage = new(Path.Combine(destination.FullName, $"Data{packageIndex++}.prowl"));

        // Create the package
        var package = AssetBundle.CreateNew(firstPackage);
        int count = 0;
        int maxCount = assetsToExport.Length;
        foreach (var assetGuid in assetsToExport)
        {
            var asset = LoadAsset(assetGuid);
            if (asset == null)
            {
                Debug.LogError($"Failed to load asset {assetGuid}.");
                continue;
            }

#warning TODO: We need to do (package.SizeInGB + SizeOfAsset > 4f) instead of just SizeInGB but for now this works
            if (package.SizeInGB > 3f)
            {
                Debug.Log($"Packing, Reached 4GB...");
                package.Dispose();
                Debug.Log($"Creating New Package {packageIndex}");
                FileInfo next = new(Path.Combine(destination.FullName, $"Data{packageIndex++}.prowl"));
                package = AssetBundle.CreateNew(next);
            }

            if (TryGetFile(assetGuid, out var assetPath))
            {
                package.AddAsset(ToRelativePath(assetPath), assetGuid, asset);
            }

            count++;
            if (count % 10 == 0 || count >= maxCount - 5)
            {
                float percentComplete = (count / (float)maxCount) * 100f;
                Debug.Log($"Exporting Assets To Stream: {count}/{maxCount} - {percentComplete}%");
            }
        }
        Debug.Log($"Packing...");
        package.Dispose();
    }

    /// <summary>
    /// Exports a package from the specified directory.
    /// </summary>
    /// <param name="directory">The directory to export the package from.</param>
    /// <param name="includeDependencies">Whether to include dependencies in the package.</param>
    public static void ExportProwlPackage(DirectoryInfo directory, bool includeDependencies = false)
    {
#warning TODO: Handle Dependencies, We do track them now, just need to support that here
        if (includeDependencies) throw new NotImplementedException("Dependency tracking is not implemented yet.");

        FileDialogContext imFileDialogInfo = new()
        {
            title = "Export Package",
            parentDirectory = Project.Active.ProjectDirectory,
            resultName = "New Package.prowlpackage",
            type = FileDialogType.SaveFile,
            OnComplete = (path) =>
            {
                var file = new FileInfo(path);
                if (File.Exists(file.FullName))
                {
                    Debug.LogError("Cannot export package, File already exists.");
                    return;
                }

                // If no extension (or wrong extension) add .scene
                if (!file.Extension.Equals(".prowlpackage", StringComparison.OrdinalIgnoreCase))
                    file = new FileInfo(file.FullName + ".prowlpackage");

                // Create the package
                // Shh, but packages are just zip files ;)
                using Stream destination = file.OpenWrite();
                ZipFile.CreateFromDirectory(directory.FullName, destination);
            }
        };
    }

    /// <summary>
    /// Imports a package from the specified file.
    /// </summary>
    /// <param name="packageFile">The package file to import.</param>
    public static void ImportProwlPackage(FileInfo packageFile)
    {
        if (!File.Exists(packageFile.FullName))
        {
            Debug.LogError("Cannot import package, File does not exist.");
            return;
        }

        // Extract the package
        using Stream source = packageFile.OpenRead();
        ZipFile.ExtractToDirectory(packageFile.FullName, Project.Active.PackagesDirectory.FullName);

#warning TODO: Handle if we already have the asset in our asset database (just dont import it)

        Update();
    }

    #endregion

    #region Git Packages

    private static void ValidateIsReady()
    {
        if (!Project.HasProject) throw new Exception("Cannot load packages, No project is open.");
        ArgumentNullException.ThrowIfNullOrEmpty(Project.Active?.PackagesDirectory.FullName);
    }

    public static async Task ValidatePackages()
    {
        ValidateIsReady();

        DirectoryInfo packagesPath = Project.Active!.PackagesDirectory;
        string packagesJsonPath = Path.Combine(packagesPath.FullName, "Packages.json");

        // Load Packages.json
        Dictionary<string, string> packageVersions = [];
        if (File.Exists(packagesJsonPath))
        {
            packageVersions = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(packagesJsonPath)) ?? [];
        }

        bool validateAgain = false;
        foreach ((string githubRepo, string versionRange) in packageVersions)
        {
            string githubPath = ConvertToPath(githubRepo) ?? throw new Exception($"Invalid GitHub repository path: {githubRepo}");
            string fileSafeName = githubPath.Replace('/', '.');
            string packageDir = Path.Combine(packagesPath.FullName, fileSafeName);

            try
            {
                // Check if package exists and get its version
                SemanticVersion? currentVersion = await GetInstalledVersion(githubPath);
                if (Directory.Exists(packageDir) && currentVersion == null)
                {
                    // If we can't get the version, treat as not installed
                    Debug.LogWarning($"Failed to get version for package {githubPath}, Uninstalling...");
                    DeleteDirectoryRetries(packageDir);
                }

                // If not installed or wrong version, install/update it
                var range = new SemVersion.Parser.Range(versionRange);
                if (currentVersion is null || !range.IsSatisfied(currentVersion))
                {
                    // Get available versions and find the best match
                    List<SemanticVersion> versions = await GetVersions(githubPath);
                    SemanticVersion? bestVersion = FindBestVersion(versions, versionRange)
                        ?? throw new Exception($"No version matching '{versionRange}' found for {githubPath}");

                    // Ensure directory exists
                    Directory.CreateDirectory(packageDir);

                    if (currentVersion is null)
                    {
                        // Clone the repository
                        await ExecuteGitCommand($"clone \"https://github.com/{githubPath}.git\" \"{packageDir}\"");
                    }

                    // Fetch tags and checkout the correct version
                    await ExecuteGitCommand($"-C \"{packageDir}\" fetch --tags");
                    await ExecuteGitCommand($"-C \"{packageDir}\" checkout -f tags/{versionRange}");

                    // Verify package.json matches the repository
                    string packageJsonPath = Path.Combine(packageDir, "package.json");
                    if (!File.Exists(packageJsonPath))
                        throw new Exception($"package.json not found in repository {githubPath}");

                    GithubPackageMetaData package = JsonSerializer.Deserialize<GithubPackageMetaData>(
                        File.ReadAllText(packageJsonPath)) ?? throw new Exception("Failed to parse package.json");

                    package.repository.githubPath = ConvertToPath(package.repository.githubPath)
                        ?? throw new Exception("Invalid GitHub repository path in package.json");

                    if (!package.repository.githubPath.Equals(githubPath, StringComparison.OrdinalIgnoreCase))
                        throw new Exception($"Repository in package.json does not match the requested repository: {package.repository.githubPath}");

                    // Handle dependencies
                    if (package.dependencies != null && package.dependencies.Count > 0)
                    {
                        // Add dependencies to Packages.json if not already there
                        bool changed = false;
                        foreach ((string depRepo, string depVersion) in package.dependencies)
                        {
                            if (!packageVersions.ContainsKey(depRepo))
                            {
                                packageVersions[depRepo] = depVersion;
                                changed = true;
                            }
                            else
                            {
                                // We have the dependency already, Check if the installed version is Less then the required version
                                // We always prioritize the latest version of a package
                                SemanticVersion? installedVersion = await GetInstalledVersion(depRepo);
                                if (installedVersion is not null && installedVersion < SemanticVersion.Parse(depVersion))
                                {
                                    packageVersions[depRepo] = depVersion;
                                    changed = true;
                                    validateAgain = true;
                                }
                            }
                        }

                        // Save updated Packages.json
                        if (changed)
                            File.WriteAllText(packagesJsonPath, JsonSerializer.Serialize(packageVersions, s_jsonOptions));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to validate package {githubPath}: {ex.Message}");

                // Cleanup on failure
                if (Directory.Exists(packageDir))
                    DeleteDirectoryRetries(packageDir);

                throw;
            }
        }

        // Recursively validate to handle new dependencies
        if (validateAgain)
            await ValidatePackages();
    }

    public static void LoadPackages()
    {
        ValidateIsReady();

        s_packagesPath = Project.Active!.PackagesDirectory;
        s_httpClient = new HttpClient();

        s_installedPackages.Clear();
        DirectoryInfo[] packageDirs = s_packagesPath.GetDirectories();

        foreach (DirectoryInfo dir in packageDirs)
        {
            string packageJsonPath = Path.Combine(dir.FullName, "package.json");
            if (File.Exists(packageJsonPath))
            {
                string packageJson = File.ReadAllText(packageJsonPath);
                GithubPackageMetaData? package = JsonSerializer.Deserialize<GithubPackageMetaData>(packageJson);
                if (package != null)
                {
                    s_installedPackages[package.repository.githubPath] = package;
                }
            }
        }
    }

    public static IEnumerable<GithubPackageMetaData> GetInstalledPackages()
    {
        return s_installedPackages.Values;
    }

    public static async Task<SemanticVersion?> GetInstalledVersion(string githubRepo)
    {
        ValidateIsReady();

        githubRepo = ConvertToPath(githubRepo) ?? throw new Exception("Invalid GitHub repository path");
        string packageDir = Path.Combine(s_packagesPath.FullName, Path.GetFileNameWithoutExtension(githubRepo));

        try
        {
            string result = await ExecuteGitCommand($"-C \"{packageDir}\" describe --tags");
            return string.IsNullOrWhiteSpace(result) ? null : SemanticVersion.Parse(result.Trim());
        }
        catch (Exception)
        {
            // If no tags are found or other error
            return null;
        }
    }

    public static GithubPackageMetaData? GetInstalledPackage(string githubRepo)
    {
        githubRepo = ConvertToPath(githubRepo) ?? throw new Exception("Invalid GitHub repository path");

        return s_installedPackages.TryGetValue(githubRepo, out GithubPackageMetaData? package)
            ? package
            : null;
    }

    /// <param name="githubRepo">The GitHub repository path, ex: 'ProwlEngine/Prowl', 'username/repository'</param>
    public static async Task<GithubPackageMetaData> GetDetails(string githubRepo)
    {
        githubRepo = ConvertToPath(githubRepo) ?? throw new Exception("Invalid GitHub repository path");

        // Convert git URL to raw GitHub URL
        string rawUrl = $"https://raw.githubusercontent.com/{githubRepo}/refs/heads/master/package.json";

        try
        {
            string response = await s_httpClient.GetStringAsync(rawUrl);
            return JsonSerializer.Deserialize<GithubPackageMetaData>(response);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to fetch package.json from {rawUrl}", ex);
        }
    }

    /// <param name="githubRepo">The GitHub repository path, ex: 'ProwlEngine/Prowl', 'username/repository'</param>
    public static async Task<List<SemanticVersion>> GetVersions(string githubRepo)
    {
        githubRepo = ConvertToPath(githubRepo) ?? throw new Exception("Invalid GitHub repository path");

        string url = $"https://github.com/{githubRepo}.git";
        string result = await ExecuteGitCommand($"ls-remote --tags {url}");
        var versions = new List<SemanticVersion>();

        foreach (string line in result.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Extract tag name from refs/tags/
            string[] parts = line.Split("refs/tags/");
            if (parts.Length == 2)
            {
                // Remove ^{} suffix that some tags have
                string tag = parts[1].Replace("^{}", "");
                var version = SemanticVersion.Parse(tag);
                if (!versions.Contains(version))
                    versions.Add(version);
            }
        }

        return versions;
    }

    private static SemanticVersion? FindBestVersion(List<SemanticVersion> semanticVersions, string versionRange)
    {
        try
        {
            var range = new SemVersion.Parser.Range(versionRange);

            // Filter versions that match the range and order by version (descending)
            return semanticVersions
                .Where(v => range.IsSatisfied(v))
                .OrderByDescending(v => v)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to evaluate version range '{versionRange}': {ex.Message}");
        }
    }

    public static async Task<GithubPackageMetaData> InstallPackage(string githubRepo, string versionRange)
    {
        ValidateIsReady();

        githubRepo = ConvertToPath(githubRepo) ?? throw new Exception("Invalid GitHub repository path");

        // Get available versions and find the best match
        List<SemanticVersion> versions = await GetVersions(githubRepo);
        SemanticVersion? bestVersion = FindBestVersion(versions, versionRange)
            ?? throw new Exception($"No version matching '{versionRange}' found for {githubRepo}");

        string packageDir = Path.Combine(s_packagesPath.FullName, Path.GetFileNameWithoutExtension(githubRepo));
        Directory.CreateDirectory(packageDir);

        // Check if package is already installed
        Task<SemanticVersion?> existingVersion = GetInstalledVersion(githubRepo);
        bool isUpdate = existingVersion != null;
        if (isUpdate)
        {
            if (existingVersion.Equals(bestVersion))
            {
                // Package already installed at best matching version
                return s_installedPackages[githubRepo];
            }
        }

        LockUpdate();

        try
        {
            string url = $"https://github.com/{githubRepo}.git";

            if (!isUpdate)
            {
                // Clone the repository
                await ExecuteGitCommand($"clone \"{url}\" \"{packageDir}\"");
            }

            // Fetch tags
            await ExecuteGitCommand($"-C \"{packageDir}\" fetch --tags");

            // Checkout the specific tag
            await ExecuteGitCommand($"-C \"{packageDir}\" checkout -f tags/\"{bestVersion}\"");

            // Checkout or reset to the specific version
            //await ExecuteGitCommand($"-C {packageDir} checkout tags/{bestVersion}");

            // Read package.json
            string packageJsonPath = Path.Combine(packageDir, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                throw new Exception($"package.json not found in repository {githubRepo}");
            }

            string packageJson = File.ReadAllText(packageJsonPath);
            GithubPackageMetaData? package = JsonSerializer.Deserialize<GithubPackageMetaData>(packageJson) ?? throw new Exception("Failed to parse package.json");
            package.repository.githubPath = ConvertToPath(package.repository.githubPath) ?? throw new Exception("Invalid GitHub repository path");

            if (!package.repository.githubPath.Equals(githubRepo, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Repository in package.json does not match the requested repository: {package.repository.githubPath}");
            }

            // Install dependencies first
            if (package.dependencies != null)
            {
                //foreach (KeyValuePair<string, string> dependency in package.dependencies)
                //{
                //    await InstallPackage(dependency.Key, dependency.Value.TrimStart('^'));
                //}
            }

            s_installedPackages[package.repository.githubPath] = package;
            return package;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to install package {githubRepo}: {ex.Message}");

            UninstallPackage(githubRepo);

            throw;
        }
        finally
        {
            UnlockUpdate();
        }
    }

    public static void UninstallPackage(string githubRepo)
    {
        ValidateIsReady();

        githubRepo = ConvertToPath(githubRepo) ?? throw new Exception("Invalid GitHub repository path");

        LockUpdate();
        try
        {
            //if (s_installedPackages.ContainsKey(githubRepo))
            {
                string packageDir = Path.Combine(s_packagesPath.FullName, Path.GetFileNameWithoutExtension(githubRepo));
                DeleteDirectoryRetries(packageDir);
                s_installedPackages.Remove(githubRepo);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to uninstall package {githubRepo}: {ex.Message}");
            throw;
        }
        finally
        {
            UnlockUpdate();
        }
    }

    private static void DeleteDirectoryRetries(string packageDir)
    {
        int index = 0;
        while (Directory.Exists(packageDir))
        {
            try
            {
                DeleteDirectory(packageDir);
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                if (index > 10)
                {
                    Debug.LogError("Failed to delete package directory, too many attempts.");
                    break;
                }
            }
            index++;
        }
    }

    /// <summary>
    /// Depth-first recursive delete, with handling for descendant 
    /// directories open in Windows Explorer.
    /// </summary>
    private static void DeleteDirectory(string path)
    {
        foreach (string directory in Directory.GetDirectories(path))
        {
            DeleteDirectory(directory);
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
            Directory.Delete(path, true);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(path, true);
        }
    }

    private static string? ConvertToPath(string githubRepo)
    {
        // They may pass in the full URL, so we need to convert it to just the Username/Repository format

        // Handle null or empty
        if (string.IsNullOrWhiteSpace(githubRepo))
            return null;

        // if it has .git at the end, remove it
        if (githubRepo.EndsWith(".git"))
            githubRepo = githubRepo.Substring(0, githubRepo.Length - 4);

        // Already in correct format (Username/Repository)
        if (!githubRepo.Contains("github.com") && githubRepo.Count(c => c == '/') == 1)
            return githubRepo;

        // Handle different URL formats
        if (githubRepo.Contains("github.com"))
        {
            // Handle SSH format: git@github.com:Username/Repository
            if (githubRepo.StartsWith("git@"))
            {
                return githubRepo.Split(':')[1];
            }

            // Handle HTTPS format: https://github.com/Username/Repository
            var parts = githubRepo.Split("github.com/");
            if (parts.Length == 2)
            {
                return parts[1];
            }
        }

        return null;
    }

    private static async Task<string> ExecuteGitCommand(string arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Git command failed: {error}");
        }

        return output;
    }

    #endregion

}
