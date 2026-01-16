using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using MyPostXAgent.Core.Services.AI;

namespace MyPostXAgent.UI.Helpers;

/// <summary>
/// Helper สำหรับตรวจสอบและติดตั้ง Ollama ตอนเริ่มโปรแกรม
/// </summary>
public static class OllamaStartupHelper
{
    /// <summary>
    /// ตรวจสอบและติดตั้ง Ollama อัตโนมัติถ้ายังไม่มี
    /// </summary>
    public static async Task EnsureOllamaReadyAsync(IServiceProvider services)
    {
        try
        {
            var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            var logger = services.GetService<ILogger<OllamaSetupService>>();
            var setupService = new OllamaSetupService(httpClientFactory.CreateClient(), logger: logger);

            var status = await setupService.CheckFullStatusAsync();

            if (status.IsReady)
            {
                System.Diagnostics.Debug.WriteLine($"Ollama ready: {status.Message}");
                return;
            }

            // ถ้ายังไม่พร้อม ให้แสดง dialog ถามผู้ใช้
            if (!status.IsInstalled)
            {
                var result = MessageBox.Show(
                    "ระบบต้องการ Ollama สำหรับ AI\n\n" +
                    "ต้องการให้ติดตั้ง Ollama อัตโนมัติหรือไม่?\n\n" +
                    "(ขนาดไฟล์ประมาณ 200 MB)",
                    "ติดตั้ง Ollama",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var setupResult = await setupService.SetupOllamaAsync(defaultModel: "llama3.2:3b");

                    if (!setupResult.Success)
                    {
                        MessageBox.Show(
                            $"ไม่สามารถติดตั้ง Ollama ได้:\n{setupResult.Message}\n\n" +
                            "โปรแกรมจะใช้ AI provider อื่นแทน (ถ้ามี)",
                            "แจ้งเตือน",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show(
                            "ติดตั้ง Ollama สำเร็จ!\n\n" +
                            "ระบบพร้อมใช้งาน AI แล้ว",
                            "สำเร็จ",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            else if (!status.IsRunning)
            {
                // Ollama ติดตั้งแล้วแต่ไม่ทำงาน - ลองเริ่ม service
                System.Diagnostics.Debug.WriteLine("Starting Ollama service...");
                var started = await setupService.StartOllamaServiceAsync();

                if (started)
                {
                    System.Diagnostics.Debug.WriteLine("Ollama service started successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to start Ollama service");
                }
            }
            else if (!status.HasModels)
            {
                // ไม่มี model - ดาวน์โหลด default model
                var result = MessageBox.Show(
                    "Ollama ติดตั้งแล้วแต่ยังไม่มี AI model\n\n" +
                    "ต้องการดาวน์โหลด model หรือไม่?\n" +
                    "(ขนาดประมาณ 2 GB)",
                    "ดาวน์โหลด AI Model",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var pulled = await setupService.PullModelAsync("llama3.2:3b");

                    if (pulled)
                    {
                        MessageBox.Show(
                            "ดาวน์โหลด AI Model สำเร็จ!\n\n" +
                            "ระบบพร้อมใช้งานแล้ว",
                            "สำเร็จ",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ollama setup error: {ex.Message}");
            // ไม่ให้ crash - โปรแกรมยังทำงานได้โดยใช้ AI provider อื่น
        }
    }
}
