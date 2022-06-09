#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX

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
    public class FfmpegCommandImpMacIL2CPPBin : FfmpegCommandImpBase
    {
        [DllImport("__Internal")]
        static extern int unity_system(string command);
        [DllImport("__Internal")]
        static extern IntPtr unity_popen(string command, string type);
        [DllImport("__Internal")]
        static extern int unity_pclose(IntPtr stream);
        [DllImport("__Internal")]
        static extern IntPtr unity_fgets(IntPtr s, int n, IntPtr stream);
        [DllImport("__Internal")]
        static extern void unity_ignoreSignals();
        [DllImport("__Internal")]
        static extern int unity_fputc(int c, IntPtr fp);

        List<string> stdErrStrs_ = new List<string>();
        string tempStr_ = "";
        Thread stdErrThread_ = null;
        bool isFinished_ = false;

        bool sendQCommand_ = false;

        public FfmpegCommandImpMacIL2CPPBin(FfmpegCommand command) : base(command)
        {
        }

        public override IEnumerator StartFfmpegCoroutine(string options)
        {
            options = ParseOptions(options);

            unity_ignoreSignals();
            isFinished_ = false;

            string fileName = "ffmpeg";
#if !FFMPEG_UNITY_USE_OUTER_WIN
            fileName = Application.streamingAssetsPath + "/_FfmpegUnity_temp/ffmpeg";
#endif
            stdErrThread_ = new Thread(() =>
            {
                IntPtr stdErrFp = unity_popen("\"" + fileName + "\" " + options + " 2>&1 >/dev/null", "r+");
                IntPtr stdErrBufferHandler = Marshal.AllocHGlobal(1024);

                while (!LoopExit)
                {
                    IntPtr retPtr = unity_fgets(stdErrBufferHandler, 1024, stdErrFp);
                    if (retPtr == IntPtr.Zero)
                    {
                        LoopExit = true;
                        break;
                    }

                    string outputStr = tempStr_ + Marshal.PtrToStringAuto(stdErrBufferHandler);
                    if (outputStr.EndsWith("\n"))
                    {
                        stdErrStrs_.Add(outputStr);
                        if (outputStr.StartsWith("Press [q] to stop"))
                        {
                            sendQCommand_ = true;
                        }
                        tempStr_ = "";
                    }
                    else
                    {
                        tempStr_ = outputStr;
                    }
                }

                if (sendQCommand_)
                {
                    unity_fputc((int)'q', stdErrFp);
                }

                IntPtr waitPtr;
                do
                {
                    waitPtr = unity_fgets(stdErrBufferHandler, 1024, stdErrFp);
                } while (waitPtr != IntPtr.Zero);

                Marshal.FreeHGlobal(stdErrBufferHandler);
                unity_pclose(stdErrFp);

                isFinished_ = true;
            });
            stdErrThread_.Start();

            yield break;
        }

        public override void StopFfmpeg()
        {
            if (stdErrThread_ != null)
            {
                stdErrThread_.Join();
                stdErrThread_ = null;
            }

            sendQCommand_ = false;
        }

        public override bool IsRunning
        {
            get
            {
                return !isFinished_ && IsAlreadyBuild;
            }
        }

        public override string StdErrLine(List<string> stdErrListForGetLine)
        {
            if (stdErrStrs_.Count <= 0)
            {
                return null;
            }
            string ret = stdErrStrs_[0];
            stdErrStrs_.RemoveAt(0);
            stdErrListForGetLine.Add(ret);
            return ret;
        }
    }
}

#endif