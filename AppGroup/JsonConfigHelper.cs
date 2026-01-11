using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using File = System.IO.File;

namespace AppGroup
{
    public class JsonConfigHelper
    {
       
        public static string FindGroupNameByKey(int key) {
            try {
                string json = File.ReadAllText(GetDefaultConfigPath());
                var jsonDocument = JsonDocument.Parse(json);

                string keyString = key.ToString();
                if (jsonDocument.RootElement.TryGetProperty(keyString, out JsonElement groupElement)) {
                    if (groupElement.TryGetProperty("groupName", out JsonElement groupNameElement)) {
                        return groupNameElement.GetString() ?? string.Empty;
                    }
                }

                throw new Exception($"No group found for key '{key}'");
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error finding group name by key {key}: {ex.Message}");
                return string.Empty;
            }
        }
        public static int FindKeyByGroupName(string groupName) {
            string json = File.ReadAllText(GetDefaultConfigPath());
            var jsonDocument = JsonDocument.Parse(json);

            foreach (var property in jsonDocument.RootElement.EnumerateObject()) {
                if (property.Value.TryGetProperty("groupName", out JsonElement groupNameElement)) {
                    if (groupNameElement.GetString() == groupName) {
                        if (int.TryParse(property.Name, out int key)) {
                            return key;
                        }
                    }
                }
            }

            throw new Exception($"No key found for groupName '{groupName}'");
        }

        public static string ReadJsonFromFile(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    throw new FileNotFoundException($"JSON configuration file not found at: {filePath}");
                }

                return System.IO.File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading JSON file: {ex.Message}", ex);
            }
        }
        public static int GetNextGroupId() {
            string jsonFilePath = GetDefaultConfigPath();
            string jsonContent = System.IO.File.Exists(jsonFilePath) ? System.IO.File.ReadAllText(jsonFilePath) : "{}";
            JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();

            if (jsonObject.AsObject().Any()) {
                int maxGroupId = jsonObject.AsObject()
                    .Select(property => int.Parse(property.Key))
                    .Max();
                return maxGroupId + 1;
            }
            else {
                return 1;
            }
        }
        public static async Task<string> ReadJsonFromFileAsync(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    throw new FileNotFoundException($"JSON configuration file not found at: {filePath}");
                }

                return await System.IO.File.ReadAllTextAsync(filePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading JSON file: {ex.Message}", ex);
            }
        }
        
        public static string GetDefaultConfigPath(string fileName = "appgroups.json")
        {


            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string appDataPath = Path.Combine(localAppDataPath, "AppGroup");

            if (!Directory.Exists(appDataPath)) {
                Directory.CreateDirectory(appDataPath);
            }

            return Path.Combine(appDataPath, fileName);
        }
        public static void AddGroupToJson(string filePath, int groupId, string groupName, bool groupHeader, string groupIcon, int groupCol, bool showLabels, int labelSize, string labelPosition, Dictionary<string, (string tooltip, string args, string icon)> paths) {
            try {
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }
                if (!System.IO.File.Exists(filePath)) {
                    System.IO.File.WriteAllText(filePath, "{}");
                }
                string jsonContent = ReadJsonFromFile(filePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();
                JsonObject jsonPaths = new JsonObject();
                foreach (var path in paths) {
                    JsonObject pathDetails = new JsonObject
                    {
                { "tooltip", path.Value.tooltip },
                { "args", path.Value.args },
                { "icon", path.Value.icon }
            };
                    jsonPaths[path.Key] = pathDetails;
                }
                JsonObject newGroup = new JsonObject
                {
            { "groupName", groupName },
            { "groupHeader", groupHeader },
            { "groupCol", groupCol },
            { "groupIcon", groupIcon },
            { "showLabels", showLabels },
            { "labelSize", labelSize },
            { "labelPosition", labelPosition },
            { "path", jsonPaths }
        };
                jsonObject[groupId.ToString()] = newGroup;
                System.IO.File.WriteAllText(filePath, JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) {
                throw new Exception($"Error adding group to JSON file: {ex.Message}", ex);
            }
        }


        public static void DeleteGroupFromJson(string filePath, int groupId) {
            try {
                string jsonContent = ReadJsonFromFile(filePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();

                if (!jsonObject.AsObject().ContainsKey(groupId.ToString())) {
                    throw new KeyNotFoundException($"Group ID {groupId} not found in JSON file.");
                }

                string groupName = jsonObject[groupId.ToString()]?["groupName"]?.GetValue<string>();

                if (string.IsNullOrEmpty(groupName)) {
                    throw new InvalidOperationException($"Could not retrieve group name for Group ID {groupId}.");
                }

                jsonObject.AsObject().Remove(groupId.ToString());

                System.IO.File.WriteAllText(filePath, JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true }));

                string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath);

                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");
                //string groupsFolder = Path.Combine(exeDirectory, "Groups");
                string groupFolderPath = Path.Combine(groupsFolder, groupName);

                if (Directory.Exists(groupFolderPath)) {
                    Directory.Delete(groupFolderPath, true); 
                }
            }
            catch (Exception ex) {
                throw new Exception($"Error deleting group: {ex.Message}", ex);
            }
        }

      
        public static void DuplicateGroupInJson(string filePath, int groupId) {
            try {
                string jsonContent = ReadJsonFromFile(filePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();

                if (jsonObject.AsObject().ContainsKey(groupId.ToString())) {
                    JsonNode groupToDuplicate = jsonObject[groupId.ToString()];
                    int newGroupId = GetNextGroupId();

                    JsonObject duplicatedGroup = groupToDuplicate.AsObject().DeepClone() as JsonObject;
                    string originalGroupName = duplicatedGroup["groupName"]?.GetValue<string>() ?? "Group";
                    string newGroupName = GetUniqueGroupName(jsonObject, $"{originalGroupName} - Copy");

                    duplicatedGroup["groupName"] = newGroupName;

                    string originalGroupIcon = duplicatedGroup["groupIcon"]?.GetValue<string>() ?? string.Empty;
                    if (!string.IsNullOrEmpty(originalGroupIcon)) {
                        string newGroupIcon = originalGroupIcon.Replace(originalGroupName, newGroupName);
                        duplicatedGroup["groupIcon"] = newGroupIcon;
                    }

                    jsonObject[newGroupId.ToString()] = duplicatedGroup;

                    System.IO.File.WriteAllText(filePath, JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true }));

                    string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath);
                    string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                    string groupsFolder = Path.Combine(appDataPath, "Groups");
                    //string groupsFolder = Path.Combine(exeDirectory, "Groups");
                    string originalGroupFolderPath = Path.Combine(groupsFolder, originalGroupName);
                    string newGroupFolderPath = Path.Combine(groupsFolder, newGroupName);

                    if (Directory.Exists(originalGroupFolderPath)) {
                        CopyDirectory(originalGroupFolderPath, newGroupFolderPath, originalGroupName, newGroupName);
                    }
                }
                else {
                    throw new KeyNotFoundException($"Group ID {groupId} not found in JSON file.");
                }
            }
            catch (Exception ex) {
                throw new Exception($"Error duplicating group in JSON file: {ex.Message}", ex);
            }
        }

        private static string GetUniqueGroupName(JsonNode jsonObject, string baseName) {
            string uniqueName = baseName;
            int counter = 2;

            while (true) {
                bool nameExists = false;
                foreach (var group in jsonObject.AsObject()) {
                    if (group.Value["groupName"]?.GetValue<string>() == uniqueName) {
                        nameExists = true;
                        break;
                    }
                }

                if (!nameExists) {
                    break;
                }

                uniqueName = $"{baseName}({counter++})";
            }

            return uniqueName;
        }

        public static async Task LaunchAll(string groupName) {
            try {
                string filePath = GetDefaultConfigPath();
                string jsonContent = await ReadJsonFromFileAsync(filePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();

                foreach (var group in jsonObject.AsObject()) {
                    if (group.Value["groupName"]?.GetValue<string>() == groupName) {
                        JsonObject paths = group.Value["path"]?.AsObject();

                        if (paths != null) {
                            var allTasks = new List<Task>();

                            foreach (var pathEntry in paths) {
                                string path = pathEntry.Key;
                                string args = pathEntry.Value?["args"]?.GetValue<string>() ?? string.Empty;

                                allTasks.Add(Task.Run(() =>
                                {
                                    try {
                                        ProcessStartInfo startInfo = new ProcessStartInfo(path) {
                                            Arguments = args,
                                            UseShellExecute = true
                                        };
                                        Process.Start(startInfo);
                                    }
                                    catch (Exception ex) {
                                        Debug.WriteLine($"Error launching process: {ex.Message}");
                                    }
                                }));
                            }

                            await Task.WhenAll(allTasks);
                        }

                        break;
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error launching all paths under group '{groupName}': {ex.Message}");
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, string originalGroupName, string newGroupName) {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir)) {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destinationDir, fileName);

                if (fileName.Contains(originalGroupName)) {
                    string newFileName = fileName.Replace(originalGroupName, newGroupName);
                    destFile = Path.Combine(destinationDir, newFileName);
                }

                System.IO.File.Copy(file, destFile);

                FileAttributes attributes = System.IO.File.GetAttributes(file);
                System.IO.File.SetAttributes(destFile, attributes);

                if (Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) {
                    UpdateShortcutTarget(destFile, originalGroupName, newGroupName);
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir)) {
                string subDirName = Path.GetFileName(subDir);

                if (subDirName.Contains(originalGroupName)) {
                    subDirName = subDirName.Replace(originalGroupName, newGroupName);
                }

                string newSubDir = Path.Combine(destinationDir, subDirName);
                CopyDirectory(subDir, newSubDir, originalGroupName, newGroupName);

                FileAttributes attributes = new DirectoryInfo(subDir).Attributes;
                new DirectoryInfo(newSubDir).Attributes = attributes;
            }
        }

        public static void UpdateShortcutIcon(string shortcutPath, string originalGroupName, string newGroupName) {
            try {
                WshShell wshShell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);

                // Get the old icon location
                string oldIconLocation = shortcut.IconLocation;

                // Update the icon location
                string newIconLocation = oldIconLocation.Replace(originalGroupName, newGroupName);
                shortcut.IconLocation = newIconLocation;

                shortcut.Save();
            }
            catch (Exception ex) {
                throw new Exception($"Error updating shortcut icon: {ex.Message}", ex);
            }
        }



        private static void UpdateShortcutTarget(string shortcutPath, string originalGroupName, string newGroupName) {
            try {
                WshShell wshShell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);

                string targetPath = shortcut.TargetPath.Replace(originalGroupName, newGroupName);
                shortcut.TargetPath = targetPath;
                shortcut.Arguments = $"\"{newGroupName}\"";
                shortcut.Description = $"{newGroupName} - AppGroup Shortcut";

                // Update the icon location if necessary
                string iconPath = shortcut.IconLocation.Replace(originalGroupName, newGroupName);
                shortcut.IconLocation = iconPath;

                shortcut.Save();
            }
            catch (Exception ex) {
                throw new Exception($"Error updating shortcut target: {ex.Message}", ex);
            }
        }
        public static bool GroupExistsInJson(string groupName) {
            string jsonPath = GetDefaultConfigPath();
            if (File.Exists(jsonPath)) {
                string jsonContent = File.ReadAllText(jsonPath);
                using (JsonDocument document = JsonDocument.Parse(jsonContent)) {
                    JsonElement root = document.RootElement;

                    foreach (JsonProperty property in root.EnumerateObject()) {
                        if (property.Value.TryGetProperty("groupName", out JsonElement groupNameElement) &&
                            groupNameElement.GetString() == groupName) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public static bool GroupIdExists(int groupId) {
            string jsonPath = GetDefaultConfigPath();

            if (File.Exists(jsonPath)) {
                string jsonContent = File.ReadAllText(jsonPath);
                using (JsonDocument document = JsonDocument.Parse(jsonContent)) {
                    JsonElement root = document.RootElement;

                    foreach (JsonProperty property in root.EnumerateObject()) {
                        if (property.Value.TryGetProperty("groupId", out JsonElement groupIdElement) &&
                            groupIdElement.GetInt32() == groupId) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        public static void OpenGroupFolder(int groupId) {
            try {
                string filePath = GetDefaultConfigPath();
                string jsonContent = ReadJsonFromFile(filePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();

                if (jsonObject.AsObject().ContainsKey(groupId.ToString())) {
                    string groupName = jsonObject[groupId.ToString()]?["groupName"]?.GetValue<string>();

                    if (!string.IsNullOrEmpty(groupName)) {
                        string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath);
                        string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                        string groupsFolder = Path.Combine(appDataPath, "Groups");
                        string groupFolderPath = Path.Combine(groupsFolder, groupName);
                        //string groupsFolder = Path.Combine(exeDirectory, "Groups");
                        //string groupFolderPath = Path.Combine(groupsFolder, groupName);

                        if (Directory.Exists(groupFolderPath)) {
                            Process.Start(new ProcessStartInfo {
                                FileName = "explorer.exe",
                                Arguments = groupFolderPath,
                                UseShellExecute = true
                            });
                        }
                        else {
                            Debug.WriteLine($"The folder for group '{groupName}' does not exist.");
                        }
                    }
                    else {
                        Debug.WriteLine("Group name not found in the configuration.");
                    }
                }
                else {
                    Debug.WriteLine($"Group ID {groupId} not found in the configuration.");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error opening group folder: {ex.Message}");
            }
        }
    }
}
