using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FfmpegUnity
{
    public class FfmpegCommandImpAndroid : FfmpegCommandImpBase
    {
        AndroidJavaObject ffmpegSession_ = null;
        string[] outputAllLine_ = new string[0];
        int outputAllLinePos_ = 0;
        int stdErrPos_ = 0;

        public FfmpegCommandImpAndroid(FfmpegCommand command) : base(command)
        {
        }

        public override IEnumerator StartFfmpegCoroutine(string options)
        {
            using (AndroidJavaClass configClass = new AndroidJavaClass("com.arthenica.ffmpegkit.FFmpegKitConfig"))
            {
                AndroidJavaObject paramVal = new AndroidJavaClass("com.arthenica.ffmpegkit.Signal").GetStatic<AndroidJavaObject>("SIGXCPU");
                configClass.CallStatic("ignoreSignal", paramVal);
            }

            stdErrPos_ = 0;

            options = options.Replace("{PERSISTENT_DATA_PATH}", Application.persistentDataPath)
                .Replace("{TEMPORARY_CACHE_PATH}", Application.temporaryCachePath)
                .Replace("\r\n", "\n").Replace("\\\n", " ").Replace("^\n", " ").Replace("\n", " ");
            yield return CommandObj.StreamingAssetsCopyOptions(options);
            options = options.Replace("{STREAMING_ASSETS_PATH}", Application.temporaryCachePath + "/FfmpegUnity_temp");

            using (AndroidJavaClass ffmpeg = new AndroidJavaClass("com.arthenica.ffmpegkit.FFmpegKit"))
            {
                ffmpegSession_ = ffmpeg.CallStatic<AndroidJavaObject>("executeAsync", options);
            }
        }

        public override void StopFfmpeg()
        {
            if (ffmpegSession_ != null)
            {
                ffmpegSession_.Call("cancel");
                while (IsRunning)
                {
                    Thread.Sleep(1);
                }
                ffmpegSession_.Dispose();
                ffmpegSession_ = null;
            }
        }

        public override bool IsRunning
        {
            get
            {
                if (ffmpegSession_ == null)
                {
                    return false;
                }
                var state = ffmpegSession_.Call<AndroidJavaObject>("getReturnCode");
                return state == null;
            }
        }

        public override string StdErrLine(List<string> stdErrListForGetLine)
        {
            if (ffmpegSession_ == null)
            {
                return null;
            }
            if (outputAllLine_.Length <= outputAllLinePos_)
            {
                string outputAll = ffmpegSession_.Call<string>("getOutput").Substring(stdErrPos_);
                if (string.IsNullOrWhiteSpace(outputAll))
                {
                    return null;
                }
                outputAllLine_ = outputAll.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                outputAllLinePos_ = 0;
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
