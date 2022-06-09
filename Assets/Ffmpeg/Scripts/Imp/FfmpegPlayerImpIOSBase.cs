#if UNITY_IOS

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace FfmpegUnity
{
    public abstract class FfmpegPlayerImpIOSBase : FfmpegPlayerImpBase
    {
        TextReader reader_;

        [DllImport("__Internal")]
        static extern IntPtr ffmpeg_ffprobeExecuteAsync(string command);
        [DllImport("__Internal")]
        static extern bool ffmpeg_isRunnning(IntPtr session);
        [DllImport("__Internal")]
        static extern int ffmpeg_getOutputLength(IntPtr session);
        [DllImport("__Internal")]
        static extern void ffmpeg_getOutput(IntPtr session, int startIndex, IntPtr output, int outputLength);
        [DllImport("__Internal")]
        static extern void ffmpeg_mkpipe(IntPtr output, int outputLength);
        [DllImport("__Internal")]
        static extern void ffmpeg_closePipe(string pipeName);

        public FfmpegPlayerImpIOSBase(FfmpegPlayerCommand playerCommand) : base(playerCommand)
        {
        }

        public override IEnumerator OpenFfprobeReaderCoroutine(string inputPathAll)
        {
            IntPtr ffprobeSession = ffmpeg_ffprobeExecuteAsync("-i \"" + inputPathAll + "\" -show_streams");

            while (ffmpeg_isRunnning(ffprobeSession))
            {
                yield return null;
            }

            int allocSize = ffmpeg_getOutputLength(ffprobeSession) + 1;
            IntPtr hglobal = Marshal.AllocHGlobal(allocSize);
            ffmpeg_getOutput(ffprobeSession, 0, hglobal, allocSize);
            string outputStr = Marshal.PtrToStringAuto(hglobal);
            Marshal.FreeHGlobal(hglobal);

            reader_ = new StringReader(outputStr);
        }

        public override TextReader OpenFfprobeReader(string inputPathAll)
        {
            return reader_;
        }

        public override void CloseFfprobeReader()
        {
            if (reader_ != null)
            {
                reader_.ReadToEnd();
                reader_.Dispose();
                reader_ = null;
            }
        }
    }
}

#endif