using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Frog.Client;

/// <summary>API interne pour smoke tests gameplay client (assembly test via InternalsVisibleTo).</summary>
internal static class ClientSmokeTestAccess
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);

    private static Action<Func<bool>, TimeSpan>? _pumpUntil;

    public static void SetPumpUntilForTest(Action<Func<bool>, TimeSpan> pumpUntil)
    {
        _pumpUntil = pumpUntil;
    }

    public static void ResetHooks()
    {
        _pumpUntil = null;
    }

    public static void PumpUntil(Func<bool> predicate, TimeSpan timeout)
    {
        if (_pumpUntil is { } pump)
        {
            pump(predicate, timeout);
            return;
        }

        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        if (!predicate())
        {
            throw new TimeoutException("Client smoke pump timed out.");
        }
    }

    public static MainShellForm CreateAndShowMainShell()
    {
        var form = new MainShellForm();
        form.Show();
        PumpUntil(() => form.Visible, DefaultTimeout);
        return form;
    }

    public static void CloseMainShell(MainShellForm form)
    {
        if (form.IsDisposed)
        {
            return;
        }

        form.Close();
        try
        {
            PumpUntil(() => form.IsDisposed, TimeSpan.FromSeconds(10));
        }
        catch
        {
            form.Dispose();
        }
    }

    public static string ScreenshotDirectory =>
        Path.Combine(FindRepositoryRoot(), "artifacts", "phase-07-gameplay-client");

    public static string Phase8ScreenshotDirectory =>
        Path.Combine(FindRepositoryRoot(), "artifacts", "phase-08-gameplay-client");

    public static void SavePhase8Screenshot(Form form, string fileName)
    {
        var directory = Path.GetFullPath(Phase8ScreenshotDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height));
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(path, ImageFormat.Png);
        if (new FileInfo(path).Length == 0)
        {
            throw new InvalidOperationException($"Empty screenshot: {path}");
        }
    }

    public static void SaveScreenshot(Form form, string fileName)
    {
        var directory = Path.GetFullPath(ScreenshotDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height));
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(path, ImageFormat.Png);
        if (new FileInfo(path).Length == 0)
        {
            throw new InvalidOperationException($"Empty screenshot: {path}");
        }
    }

    internal static string FindRepositoryRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Frog.Creator.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }
}
