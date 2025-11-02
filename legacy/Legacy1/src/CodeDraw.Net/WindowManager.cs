using System.Collections.Concurrent;
using MarcoZechner.CodeDrawDotNet.Extensions;
using MarcoZechner.MathDotNet;
using Silk.NET.Windowing;

namespace MarcoZechner.CodeDrawDotNet;

internal static class WindowManager
{
    private static Task? _runTask = null;
    private static readonly CancellationTokenSource _cancellationTokenSource = new();
    private static readonly ConcurrentQueue<IWindow> _windowsToCreate = new();
    private static readonly List<IWindow> _windowsRunning = [];
    private static readonly Queue<IWindow> _windowsToClose = [];
    private static Vector2 _currentAutoOffset = new(50, 50);
    private static readonly Vector2 _autoOffsetStep = new(50, 50);
    public static bool HasOpenWindows => _windowsRunning.Count > 0 || !_windowsToCreate.IsEmpty;

    public static void AddWindow(IWindow window)
    {
        _windowsToCreate.Enqueue(window);
        _runTask ??= CreateRunTask();
    }

    private static Task CreateRunTask()
    {
        return Task.Run(static () =>
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                UpdateWindows();

                CreateWindows();

                CloseWindows();
            }
            _runTask = null;
        });
    }

    private static void UpdateWindows()
    {
        foreach (var window in _windowsRunning)
        {
            try
            {
                if (window.IsClosing)
                {
                    window.Reset();
                    window.Dispose();
                    _windowsToClose.Enqueue(window);
                    continue;
                }
                window.DoEvents();
                window.DoUpdate();
                window.DoRender();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in window {window.Title}: {e.Message}");
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing window {window.Title}: {ex.Message}");
                }
            }
        }
    }

    private static void CreateWindows()
    {
        while (_windowsToCreate.TryDequeue(out var window))
        {
            if (window.Position == new Vector2(-1, -1).ToSilkI())
            {
                window.Position = _currentAutoOffset.ToSilkI();
                _currentAutoOffset += _autoOffsetStep;
                window.Load += () =>
                {
                    var monitor = window.Monitor;
                    var monitorSize = monitor?.VideoMode.Resolution;
                    if (monitorSize == null) return;
                    if (window.Position.X + window.Size.X > monitorSize.Value.X)
                    {
                        _currentAutoOffset = new(_autoOffsetStep.X, _currentAutoOffset.Y);
                        window.Position = new Vector2(_currentAutoOffset.X, window.Position.Y).ToSilkI();
                    }
                    if (window.Position.Y + window.Size.Y > monitorSize.Value.Y)
                    {
                        _currentAutoOffset = new(_currentAutoOffset.X, _autoOffsetStep.Y);
                        window.Position = new Vector2(window.Position.X, _currentAutoOffset.Y).ToSilkI();
                    }
                };
            }
            window.Initialize();
            _windowsRunning.Add(window);
        }
    }

    private static void CloseWindows()
    {
        while (_windowsToClose.Count > 0)
        {
            var window = _windowsToClose.Dequeue();
            _windowsRunning.Remove(window);
            if (_windowsRunning.Count == 0)
                _cancellationTokenSource.Cancel();
        }
    }
}