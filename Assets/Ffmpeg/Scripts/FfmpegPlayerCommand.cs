using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FfmpegUnity
{
    public class FfmpegPlayerCommand : FfmpegCommand
    {
        public string InputOptions = "";

        public FfmpegPath.DefaultPath DefaultPath = FfmpegPath.DefaultPath.NONE;
        public string InputPath = "";

        public bool AutoStreamSettings = true;
        public FfmpegStream[] Streams = new FfmpegStream[] {
            new FfmpegStream()
            {
                CodecType = FfmpegStream.Type.VIDEO,
                Width = 640,
                Height = 480,
            },
            new FfmpegStream()
            {
                CodecType = FfmpegStream.Type.AUDIO,
                Channels = 2,
                SampleRate = 48000,
            },
        };
        public float FrameRate = 30f;

        public string PlayerOptions = "";

        public FfmpegPlayerVideoTexture[] VideoTextures;
        public AudioSource[] AudioSources;

        public bool SyncFrameRate = true;

        float time_ = 0f;
        bool addDeltaTime_ = false;

        // Current play time of video.
        // If you want to set, call SetTime().
        public float Time
        {
            get
            {
                if (time_ > Duration && Duration > 0f)
                {
                    return Duration;
                }
                return time_;
            }
            private set
            {
                time_ = value;
                addDeltaTime_ = false;
            }
        }

        float duration_ = 0f;

        // Duration of video.
        public float Duration
        {
            get
            {
                return duration_ - 1f / FrameRate * 2f;
            }
            private set
            {
                duration_ = value;
            }
        }
        public bool IsPlaying
        {
            get
            {
                return !isEnd_;
            }
        }

        public int Frames
        {
            get;
            private set;
        } = 0;

        protected bool StopPerFrame
        {
            get;
            set;
        } = false;

        bool isEnd_ = true;
        List<Thread> threads_ = new List<Thread>();

        Dictionary<int, byte[]> videoBuffers_ = new Dictionary<int, byte[]>();
        public void SetVideoBuffer(int id, byte[] newBuffer)
        {
            lock (videoBuffers_)
            {
                videoBuffers_[id] = newBuffer;
            }
        }
        Dictionary<int, int> widths_ = new Dictionary<int, int>();
        Dictionary<int, int> heights_ = new Dictionary<int, int>();

        Dictionary<int, List<float>> audioBuffers_ = new Dictionary<int, List<float>>();
        public void AddAudioBuffer(int id, float[] buffer)
        {
            lock (audioBuffers_[id])
            {
                audioBuffers_[id].AddRange(buffer);
            }
        }
        Dictionary<int, int> sampleRates_ = new Dictionary<int, int>();
        Dictionary<int, int> channels_ = new Dictionary<int, int>();

        public double TimeBase
        {
            get;
            private set;
        } = 0.0;

        bool isSettingTime_ = false;

        FfmpegPlayerImpBase playerImp_;

        string ffprobedPath_ = null;

        public bool IsEOF
        {
            get
            {
                if (playerImp_ == null)
                {
                    return false;
                }

                return playerImp_.IsEOF;
            }
        }

        [Serializable]
        public class FfmpegStream
        {
            public enum Type
            {
                OTHER = -1,
                VIDEO,
                AUDIO,
                DATA
            }
            public Type CodecType;
            public int Width;
            public int Height;
            public int Channels;
            public int SampleRate;
        }

        // Set play time of video.
        public void SetTime(float val)
        {
            if (!isSettingTime_ && IsRunning)
            {
                isSettingTime_ = true;
                StartCoroutine(setTimeCoroutine(val));
            }
            else if (!isSettingTime_)
            {
                Time = val;
            }
        }

        IEnumerator setTimeCoroutine(float val)
        {
            StopFfmpeg();

            while (IsRunning || IsPlaying)
            {
                yield return null;
            }

            Time = val;
            StartFfmpeg();

            while (!IsRunning || !IsPlaying)
            {
                yield return null;
            }

            isSettingTime_ = false;
        }

        protected override void Build()
        {
            RunOptions = "";

            if (AutoStreamSettings && ffprobedPath_ != FfmpegPath.PathWithDefault(DefaultPath, InputPath))
            {
                ffprobedPath_ = FfmpegPath.PathWithDefault(DefaultPath, InputPath);
                Streams = new FfmpegStream[0];
                StartCoroutine(allCoroutine());
            }
            else
            {
                StartCoroutine(ResetCoroutine());
            }
        }

        IEnumerator allCoroutine()
        {
            yield return FfprobeCoroutine(FfmpegPath.PathWithDefault(DefaultPath, InputPath));
            yield return ResetCoroutine();
        }

        protected IEnumerator FfprobeCoroutine(string inputPathAll)
        {
            List<FfmpegStream> ffmpegStreams = new List<FfmpegStream>();

            if (playerImp_ == null)
            {
                playerImp_ = FfmpegPlayerImpBase.GetNewInstance(this);
            }

            yield return playerImp_.OpenFfprobeReaderCoroutine(inputPathAll);
            TextReader reader = playerImp_.OpenFfprobeReader(inputPathAll);

            Thread ffprobeThread = new Thread(() =>
            {
                    FfmpegStream ffmpegStream = new FfmpegStream();

                                ffmpegStream.CodecType = FfmpegStream.Type.VIDEO;

                                ffmpegStream.Width = 1920; 

                                ffmpegStream.Height = 1080; 


                    ffmpegStreams.Add(ffmpegStream);

                playerImp_.CloseFfprobeReader();
            });
            ffprobeThread.Start();

            while (ffprobeThread.IsAlive)
            {
                yield return null;
            }

            Streams = Streams.Concat(ffmpegStreams).ToArray();
        }

        protected IEnumerator ResetCoroutine()
        {
            if (playerImp_ == null)
            {
                playerImp_ = FfmpegPlayerImpBase.GetNewInstance(this);
            }

            isEnd_ = false;
            playerImp_.IsEnd = false;

            if (TimeBase <= 0.0 && FrameRate > 0f)
            {
                TimeBase = 1.0 / FrameRate;
            }

            RunOptions += " -y -dump ";
            if (Time < Duration)
            {
                RunOptions += " -ss " + Time + " ";
            }
            RunOptions += " " + InputOptions + " ";

            if (!string.IsNullOrEmpty(InputPath))
            {
                yield return playerImp_.BuildBeginOptionsCoroutine(FfmpegPath.PathWithDefault(DefaultPath, InputPath));
                //RunOptions += playerImp_.BuildBeginOptions(FfmpegPath.PathWithDefault(DefaultPath, InputPath));
            }

            RunOptions += " " + PlayerOptions + " ";

            int streamCount = 0;
            int videoStreamCount = 0;
            int audioStreamCount = 0;
            foreach (var ffmpegStream in Streams)
            {
                if (ffmpegStream.CodecType == FfmpegStream.Type.VIDEO)
                {
                    if (videoStreamCount >= VideoTextures.Length)
                    {
                        continue;
                    }
                    var videoTexture = VideoTextures[videoStreamCount];

                    RunOptions += playerImp_.BuildVideoOptions(streamCount, ffmpegStream.Width, ffmpegStream.Height);

                    if (videoTexture.VideoTexture == null)
                    {
                        videoTexture.VideoTexture = new Texture2D(ffmpegStream.Width, ffmpegStream.Height, TextureFormat.RGBA32, false);
                    }
                    else if (videoTexture.VideoTexture is Texture2D && (videoTexture.VideoTexture.width != ffmpegStream.Width || videoTexture.VideoTexture.height != ffmpegStream.Height))
                    {
                        Texture2D oldTexture = (Texture2D)videoTexture.VideoTexture;
                        videoTexture.VideoTexture = new Texture2D(ffmpegStream.Width, ffmpegStream.Height, oldTexture.format, false);
                        Destroy(oldTexture);
                    }

                    widths_[streamCount] = ffmpegStream.Width;
                    heights_[streamCount] = ffmpegStream.Height;

                    int streamId = streamCount;
                    var thread = new Thread(() => { readVideo(streamId); });
                    thread.Start();
                    threads_.Add(thread);

                    videoStreamCount++;
                }
                else
                {
                    if (AudioSources == null || AudioSources.Length <= audioStreamCount)
                    {
                        continue;
                    }

                    var audioSource = AudioSources[audioStreamCount];

                    if (ffmpegStream.SampleRate != AudioSettings.outputSampleRate)
                    {
                        RunOptions += " -af asetrate=" + ffmpegStream.SampleRate + " -ar " + AudioSettings.outputSampleRate + " ";
                    }

                    RunOptions += playerImp_.BuildAudioOptions(streamCount, ffmpegStream.SampleRate, ffmpegStream.Channels);

                    audioSource.clip = AudioClip.Create("", AudioSettings.outputSampleRate * 2, ffmpegStream.Channels, AudioSettings.outputSampleRate, true);
                    audioSource.loop = true;
                    if (audioSource.playOnAwake)
                    {
                        audioSource.Play();
                    }

                    audioBuffers_[streamCount] = new List<float>();
                    sampleRates_[streamCount] = ffmpegStream.SampleRate;
                    channels_[streamCount] = ffmpegStream.Channels;

                    var playerAudio = audioSource.GetComponent<FfmpegPlayerAudio>();
                    if (playerAudio == null)
                    {
                        playerAudio = audioSource.gameObject.AddComponent<FfmpegPlayerAudio>();
                    }
                    playerAudio.StreamId = streamCount;
                    playerAudio.Player = this;

                    int streamId = streamCount;
                    var thread = new Thread(() => { readAudio(streamId); });
                    thread.Start();
                    threads_.Add(thread);

                    audioStreamCount++;
                }

                streamCount++;
            }

            IsGetStdErr = true;
            IsFinishedBuild = true;
            yield return startReadTime();
        }

        protected override void Clean()
        {
            if (playerImp_ == null)
            {
                return;
            }

            isEnd_ = true;
            playerImp_.IsEnd = true;

            foreach (var thread in threads_)
            {
                bool exited = thread.Join(1);
                while (!exited && IsRunning)
                {
                    exited = thread.Join(1);
                }
                if (!exited && !IsRunning)
                {
                    thread.Abort();
                }
            }
            threads_.Clear();

            if (playerImp_ != null)
            {
                playerImp_.Clean();
            }
        }

        protected override void CleanBuf()
        {
            if (playerImp_ == null)
            {
                return;
            }

            playerImp_.CleanBuf();
        }

        void readVideo(int streamId)
        {
            playerImp_.ReadVideo(streamId, widths_[streamId], heights_[streamId]);
        }

        void readAudio(int streamId)
        {
            playerImp_.ReadAudio(streamId, sampleRates_[streamId], channels_[streamId]);
        }

        IEnumerator startReadTime()
        {
            string readStr;

            do
            {
                readStr = GetStdErrLine();
                if (readStr == null)
                {
                    yield return null;
                }
                if (isEnd_)
                {
                    yield break;
                }
            } while (readStr == null || !readStr.StartsWith("stream #0:"));

            while (!isEnd_)
            {
                readStr = GetStdErrLine();
                if (readStr == null)
                {
                    yield return null;
                    continue;
                }

                if (!readStr.StartsWith("stream #0:"))
                {
                    continue;
                }

                do
                {
                    readStr = GetStdErrLine();
                    if (readStr == null)
                    {
                        yield return null;
                    }
                    if (isEnd_)
                    {
                        yield break;
                    }
                } while (readStr == null || !readStr.StartsWith("  dts="));

                string timeStr = readStr.Substring("  dts=".Length).Split(new string[] { "  pts=" }, StringSplitOptions.None)[0];
                float time;
                if (float.TryParse(timeStr, out time) && Time < time)
                {
                    Time = time;
                }
            }
        }

        protected bool WriteNextTexture()
        {
            bool ret = false;
            lock (videoBuffers_)
            {
                if (videoBuffers_ == null || videoBuffers_.Count <= 0)
                {
                    return false;
                }

                int videoLoop = 0;
                List<int> delKeys = new List<int>();
                foreach (var videoBuffer in videoBuffers_)
                {
                    if (videoBuffer.Value.Length <= 0)
                    {
                        videoLoop++;
                        continue;
                    }

                    Texture2D videoTexture;
                    if (VideoTextures[videoLoop].VideoTexture == null)
                    {
                        continue;
                    }
                    else if (VideoTextures[videoLoop].VideoTexture is RenderTexture)
                    {
                        videoTexture = new Texture2D(widths_[videoBuffer.Key], heights_[videoBuffer.Key], TextureFormat.RGBA32, false);
                    }
                    else
                    {
                        videoTexture = VideoTextures[videoLoop].VideoTexture as Texture2D;
                    }
                    if (videoTexture == null)
                    {
                        continue;
                    }

                    videoTexture.LoadRawTextureData(videoBuffer.Value);
                    videoTexture.Apply();

                    if (VideoTextures[videoLoop].VideoTexture is RenderTexture)
                    {
                        Graphics.Blit(videoTexture, VideoTextures[videoLoop].VideoTexture as RenderTexture);
                        Destroy(videoTexture);
                    }

                    videoLoop++;

                    ret = true;

                    delKeys.Add(videoBuffer.Key);
                }

                foreach (var key in delKeys)
                {
                    videoBuffers_.Remove(key);
                }
            }

            return ret;
        }

        protected override void Update()
        {
            base.Update();

            if (time_ >= Duration - 1f / FrameRate * 2f && Duration > 0f)
            {
                StopFfmpeg();
                Time = 0f;
                return;
            }

            if (!IsPlaying)
            {
                return;
            }

            if (!addDeltaTime_)
            {
                addDeltaTime_ = true;
            }
            else if (Duration > 0f)
            {
                //time_ += UnityEngine.Time.deltaTime;
            }

            if (!IsRunning)
            {
                return;
            }

            WriteNextTexture();
        }

        public void OnAudioFilterReadFromPlayerAudio(float[] data, int channels, int streamId)
        {
            lock (audioBuffers_[streamId])
            {
                int length = audioBuffers_[streamId].Count < data.Length ? audioBuffers_[streamId].Count : data.Length;

                /*
                if (audioBuffers_[streamId].Count > 48000 * channels)
                {
                    int delTempLength = audioBuffers_[streamId].Count - length * 2;
                    delTempLength = audioBuffers_[streamId].Count < delTempLength ? audioBuffers_[streamId].Count : delTempLength;
                    if (delTempLength > 0)
                    {
                        audioBuffers_[streamId].RemoveRange(0, delTempLength);
                    }
                }
                */

                for (int loop = 0; loop < length; loop++)
                {
                    data[loop] = audioBuffers_[streamId][loop];
                }
                if (length <= audioBuffers_[streamId].Count)
                {
                    audioBuffers_[streamId].RemoveRange(0, length);
                }
            }
        }

        public void StopPerFrameFunc(int streamId)
        {
            if (StopPerFrame)
            {
                while (!isEnd_)
                {
                    lock (videoBuffers_)
                    {
                        if (!videoBuffers_.ContainsKey(streamId))
                        {
                            break;
                        }
                    }

                    Thread.Sleep(1);
                }
            }
        }
    }
}
