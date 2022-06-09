#if UNITY_IOS

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FfmpegUnity
{
    public class FfmpegCommandImpIOS : FfmpegCommandImpBase
    {
        [DllImport("__Internal")]
        static extern void ffmpeg_setup();
        [DllImport("__Internal")]
        static extern IntPtr ffmpeg_executeAsync(string command);
        [DllImport("__Internal")]
        static extern void ffmpeg_cancel(IntPtr session);
        [DllImport("__Internal")]
        static extern int ffmpeg_getOutputLength(IntPtr session);
        [DllImport("__Internal")]
        static extern void ffmpeg_getOutput(IntPtr session, int startIndex, IntPtr output, int outputLength);
        [DllImport("__Internal")]
        static extern bool ffmpeg_isRunnning(IntPtr session);

        IntPtr session_ = IntPtr.Zero;
        string[] outputAllLine_ = new string[0];
        int outputAllLinePos_ = 0;
        int stdErrPos_ = 0;

        Thread stdErrThread_ = null;
        bool stdErrStop_ = false;

        public FfmpegCommandImpIOS(FfmpegCommand command) : base(command)
        {
        }

        public override IEnumerator StartFfmpegCoroutine(string options)
        {
            options = ParseOptions(options);

            ffmpeg_setup();

            session_ = ffmpeg_executeAsync(options);

            yield break;
        }

        public override void StopFfmpeg()
        {
            if (stdErrThread_ != null)
            {
                stdErrStop_ = true;
                stdErrThread_.Join();
                stdErrThread_ = null;
                stdErrStop_ = false;
            }

            if (session_ != IntPtr.Zero)
            {
                if (IsRunning)
                {
                    ffmpeg_cancel(session_);
                    while (IsRunning)
                    {
                        Thread.Sleep(1);
                    }
                }
                session_ = IntPtr.Zero;
            }
        }

        public override bool IsRunning
        {
            get
            {
                if (session_ == IntPtr.Zero)
                {
                    return false;
                }
                return ffmpeg_isRunnning(session_);
            }
        }

        void getStdErr()
        {
            while (!stdErrStop_)
            {
                if (outputAllLine_.Length <= outputAllLinePos_)
                {
                    int allLength = ffmpeg_getOutputLength(session_);
                    if (allLength <= stdErrPos_)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    int allocSize = allLength + 1 - stdErrPos_;
                    IntPtr hglobal = Marshal.AllocHGlobal(allocSize);

                    ffmpeg_getOutput(session_, stdErrPos_, hglobal, allocSize);

                    string outputStr = Marshal.PtrToStringAuto(hglobal);
                    outputAllLine_ = outputStr.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                    outputAllLinePos_ = 0;

                    Marshal.FreeHGlobal(hglobal);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        public override string StdErrLine(List<string> stdErrListForGetLine)
        {
            if (session_ == IntPtr.Zero)
            {
                return null;
            }

            if (stdErrThread_ == null)
            {
                stdErrThread_ = new Thread(getStdErr);
                stdErrThread_.Start();
            }

            if (outputAllLine_.Length <= outputAllLinePos_)
            {
                return null;
            }

            string ret = outputAllLine_[outputAllLinePos_];
            outputAllLinePos_++;
            if (outputAllLine_.Length == outputAllLinePos_)
            {
                stdErrPos_ += ret.Length;
            }
            else
            {
                stdErrPos_ += ret.Length + Environment.NewLine.Length;
            }

            stdErrListForGetLine.Add(ret);
            return ret;
        }
    }
}

#endif