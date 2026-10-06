using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JunimoOrchestra.Audio
{
    /// <summary>
    /// Puts a live audio thread ahead of the machine's other work. On macOS that's the user-interactive quality of
    /// service: at the default one, background work (a browser, Spotlight indexing, a busy loop per core) held the song's
    /// audio up for long enough that the output ran dry. Elsewhere a raised thread priority. (On macOS a thread priority
    /// would take the thread out of the quality-of-service system, so it isn't set there.)
    /// </summary>
    internal static class AudioThread
    {
        private const int QosClassUserInteractive = 0x21;

        /// <summary>Raise the calling thread; returns what was done, or why not.</summary>
        public static string Raise()
        {
            if (OperatingSystem.IsMacOS())
            {
                int error = pthread_set_qos_class_self_np(QosClassUserInteractive, 0);
                return error == 0 ? "user-interactive" : $"couldn't be raised (error {error})";
            }
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            return "above normal";
        }

        [DllImport("/usr/lib/libSystem.dylib")]
        private static extern int pthread_set_qos_class_self_np(int qosClass, int relativePriority);
    }
}
