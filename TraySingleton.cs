using System;
using System.Threading;

namespace DigiSign
{
    /// <summary>
    /// System-wide named primitives ensuring exactly one DigiSign tray icon exists at a time,
    /// regardless of how many processes (listener, idle tray companion, batch invocations) are running.
    /// </summary>
    internal static class TraySingleton
    {
        private const string MutexName = @"Global\DigiSign_TrayPresence";
        private const string ExitSignalName = @"Global\DigiSign_TrayExitRequested";

        private static Mutex mutex;

        /// <summary>Attempts to take ownership of the tray-presence slot for this process's lifetime.</summary>
        public static bool TryAcquire()
        {
            try
            {
                mutex = new Mutex(initiallyOwned: false, MutexName);
                bool acquired;
                try
                {
                    acquired = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    // Previous owner crashed without releasing - we now own it.
                    acquired = true;
                }

                if (!acquired)
                {
                    mutex.Dispose();
                    mutex = null;
                }
                return acquired;
            }
            catch (Exception ex)
            {
                Logger.Warning($"TraySingleton.TryAcquire failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Non-owning probe: is some process currently holding the tray-presence slot?</summary>
        public static bool IsHeld()
        {
            try
            {
                using (var probe = new Mutex(initiallyOwned: false, MutexName))
                {
                    bool acquired;
                    try
                    {
                        acquired = probe.WaitOne(0);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (acquired)
                        probe.ReleaseMutex();
                    return !acquired;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"TraySingleton.IsHeld failed: {ex.Message}");
                return false;
            }
        }

        public static void Release()
        {
            try
            {
                mutex?.ReleaseMutex();
            }
            catch (Exception ex)
            {
                Logger.Warning($"TraySingleton.Release failed: {ex.Message}");
            }
            finally
            {
                mutex?.Dispose();
                mutex = null;
            }
        }

        /// <summary>Signals whichever process currently holds the tray-presence slot to exit.</summary>
        public static void RequestOtherInstanceExit()
        {
            try
            {
                using (var exitSignal = new EventWaitHandle(false, EventResetMode.ManualReset, ExitSignalName))
                {
                    exitSignal.Set();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"TraySingleton.RequestOtherInstanceExit failed: {ex.Message}");
            }
        }

        /// <summary>Starts a background thread that invokes <paramref name="onExitRequested"/> when another process calls <see cref="RequestOtherInstanceExit"/>.</summary>
        public static void WatchForExitRequest(Action onExitRequested)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    using (var exitSignal = new EventWaitHandle(false, EventResetMode.ManualReset, ExitSignalName))
                    {
                        exitSignal.WaitOne();
                        exitSignal.Reset();
                        onExitRequested();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"TraySingleton.WatchForExitRequest failed: {ex.Message}");
                }
            })
            { IsBackground = true };
            thread.Start();
        }
    }
}
