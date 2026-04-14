using System.IO;
using StarAudioAssistant.App.Models;

namespace StarAudioAssistant.App.Services;

public static class TaskHealthService
{
    public static string? GetHealthIssue(TaskDefinition task)
    {
        if (string.IsNullOrWhiteSpace(task.AudioPath))
        {
            return "音频文件未设置";
        }

        if (!File.Exists(task.AudioPath))
        {
            return "音频文件不存在";
        }

        try
        {
            using var stream = File.Open(task.AudioPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length == 0)
            {
                return "音频文件为空";
            }
        }
        catch (UnauthorizedAccessException)
        {
            return "无权限读取音频文件";
        }
        catch (Exception ex)
        {
            return $"音频文件检测失败：{ex.Message}";
        }

        return null;
    }
}
