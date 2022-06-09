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
    public abstract class FfmpegPlayerImpAndroidBase : FfmpegPlayerImpBase
    {
        protected string DataDir
        {
            get;
            private set;
        }

        TextReader reader_;

        string options_;

        public FfmpegPlayerImpAndroidBase(FfmpegPlayerCommand playerCommand) : base(playerCommand)
        {
            using (AndroidJavaClass jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = jc.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            using (AndroidJavaObject info = context.Call<AndroidJavaObject>("getApplicationInfo"))
            {
                DataDir = info.Get<string>("dataDir");
            }
        }

        public override IEnumerator OpenFfprobeReaderCoroutine(string inputPathAll)
        {
            if (inputPathAll.Contains(Application.streamingAssetsPath))
            {
                yield return PlayerCommand.StreamingAssetsCopyPath(inputPathAll);
                inputPathAll = PlayerCommand.PathInStreamingAssetsCopy;
            }

            string outputStr;

            using (AndroidJavaClass ffprobe = new AndroidJavaClass("com.arthenica.ffmpegkit.FFprobeKit"))
            using (AndroidJavaObject ffprobeSession = ffprobe.CallStatic<AndroidJavaObject>("execute", "-i \"" + inputPathAll + "\" -show_streams"))
            {
                outputStr = ffprobeSession.Call<string>("getOutput");
            }

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

        public override IEnumerator BuildBeginOptionsCoroutine(string path)
        {
            options_ = " -i \"";

            if (path.Contains(Application.streamingAssetsPath))
            {
                if (string.IsNullOrEmpty(PlayerCommand.PathInStreamingAssetsCopy))
                {
                    yield return PlayerCommand.StreamingAssetsCopyPath(path);
                }
                options_ += PlayerCommand.PathInStreamingAssetsCopy;
            }
            else
            {
                options_ += path;
            }

            options_ += "\" ";
        }

        public override string BuildBeginOptions(string path)
        {
            return options_;
        }
    }
}
