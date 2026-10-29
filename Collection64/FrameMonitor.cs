using System.Diagnostics;

namespace LHP2_Archi_Mod.Collection64;

// Frame times from the per-frame hook: a summary every minute and the spikes outside loadings
internal static class FrameMonitor
{
    private const double SlowFrameMs = 50;
    private const double SpikeMs = 100;
    private static long _last, _windowStart, _lastSpikeLog;
    private static int _lastMap, _frames, _slowFrames;
    private static double _totalMs, _maxMs;

    // Menus don't run the frame hook: the gap until the next frame is the time spent in the menu
    public static void Restart() => _last = 0;

    public static void OnFrame()
    {
        long now = Stopwatch.GetTimestamp();
        var game = Mod.GameInstance;
        int map = game?.MapID ?? 0;
        // Map changes and menus stop the hook for a while: their gap isn't a frame time
        if (_last == 0 || map != _lastMap || game?.PrevInMenu == true)
        {
            _last = now;
            _lastMap = map;
            if (_windowStart == 0)
                _windowStart = now;
            return;
        }

        double ms = (now - _last) * 1000.0 / Stopwatch.Frequency;
        _last = now;
        _frames++;
        _totalMs += ms;
        if (ms > _maxMs)
            _maxMs = ms;
        if (ms > SlowFrameMs)
            _slowFrames++;

        if (ms > SpikeMs && now - _lastSpikeLog > 5 * Stopwatch.Frequency)
        {
            _lastSpikeLog = now;
            Game.PrintToLog($"[Perf] Frame spike: {ms:F0} ms on map {map}");
        }

        if (now - _windowStart >= 60 * Stopwatch.Frequency)
        {
            Game.PrintToLog($"[Perf] Last minute: {_frames} frames, average {_totalMs / _frames:F1} ms, "
                + $"max {_maxMs:F0} ms, {_slowFrames} over {SlowFrameMs} ms");
            _windowStart = now;
            _frames = _slowFrames = 0;
            _totalMs = _maxMs = 0;
        }
    }
}
