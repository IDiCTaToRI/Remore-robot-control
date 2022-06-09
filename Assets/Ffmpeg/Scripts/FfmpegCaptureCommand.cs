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
    public class FfmpegCaptureCommand : FfmpegCommand
    {
        [Serializable]
        public class CaptureSource
        {
            public enum SourceType
            {
                Video_GameView,
                Video_Camera,
                Video_RenderTexture,
                Audio_AudioListener,
                Audio_AudioSource,
            }

            public SourceType Type = SourceType.Video_GameView;
            public int Width = -1;
            public int Height = 480;
            public int FrameRate = 30;
            public Camera SourceCamera = null;
            public RenderTexture SourceRenderTexture = null;
            public AudioSource SourceAudio = null;
            public bool Mute = false;
        }

        public CaptureSource[] CaptureSources = new CaptureSource[]
        {
            new CaptureSource()
            {
                Type = CaptureSource.SourceType.Video_GameView,
            },
            new CaptureSource()
            {
                Type = CaptureSource.SourceType.Audio_AudioListener,
            },
        };

        public string CaptureOptions = "";

        Dictionary<int, Texture2D> tempTextures_ = new Dictionary<int, Texture2D>();
        Dictionary<int, byte[]> videoBuffers_ = new Dictionary<int, byte[]>();

        Dictionary<int, List<float>> audioBuffers_ = new Dictionary<int, List<float>>();
        Dictionary<int, int> audioChannels_ = new Dictionary<int, int>();

        List<Thread> threads_ = new List<Thread>();

        FfmpegCaptureAudioListener captureAudioListener_ = null;

        Dictionary<int, RenderTexture> tempRenderTextures_ = new Dictionary<int, RenderTexture>();
        Dictionary<int, bool> reverse_ = new Dictionary<int, bool>();

        Shader flipShader_;
        Material flipMaterial_;

        FfmpegCaptureImpBase captureImp_;

        protected override void Build()
        {
            StartCoroutine(captureCoroutine());
        }

        protected override void Clean()
        {
            captureImp_.IsEnd = true;

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

            foreach (RenderTexture tex in tempRenderTextures_.Values)
            {
                Destroy(tex);
            }
            tempRenderTextures_.Clear();

            foreach (Texture2D tex in tempTextures_.Values)
            {
                Destroy(tex);
            }
            tempTextures_.Clear();

            videoBuffers_.Clear();
            audioBuffers_.Clear();

            if (captureAudioListener_ != null)
            {
                Destroy(captureAudioListener_.gameObject);
                captureAudioListener_ = null;
            }

            captureImp_.IsEnd = false;
        }

        protected virtual void ByteStart()
        {

        }

        IEnumerator captureCoroutine()
        {
            flipShader_ = Resources.Load<Shader>("FfmpegUnity/Shaders/FlipShader");
            flipMaterial_ = new Material(flipShader_);

            FindObjectOfType<AudioListener>().velocityUpdateMode = AudioVelocityUpdateMode.Dynamic;

#if UNITY_EDITOR_WIN && FFMPEG_UNITY_USE_BINARY_WIN
            captureImp_ = new FfmpegCaptureImpWinMonoBin(this);
#elif UNITY_EDITOR_WIN && !FFMPEG_UNITY_USE_BINARY_WIN
            captureImp_ = new FfmpegCaptureImpWinLib(this);
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            captureImp_ = new FfmpegCaptureImpUnixMonoBin(this);
#elif UNITY_STANDALONE_WIN && !FFMPEG_UNITY_USE_BINARY_WIN
            captureImp_ = new FfmpegCaptureImpWinLib(this);
#elif UNITY_STANDALONE_WIN && !ENABLE_IL2CPP
            captureImp_ = new FfmpegCaptureImpWinMonoBin(this);
#elif UNITY_STANDALONE_WIN && ENABLE_IL2CPP
            captureImp_ = new FfmpegCaptureImpWinIL2CPPBin(this);
#elif (UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX) && !ENABLE_IL2CPP
            captureImp_ = new FfmpegCaptureImpUnixMonoBin(this);
#elif (UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX) && ENABLE_IL2CPP
            captureImp_ = new FfmpegCaptureImpUnixIL2CPPBin(this);
#elif UNITY_ANDROID && FFMPEG_UNITY_USE_PIPE
            captureImp_ = new FfmpegCaptureImpAndroidPipe(this);
#elif UNITY_ANDROID && !FFMPEG_UNITY_USE_PIPE
            captureImp_ = new FfmpegCaptureImpAndroidMemory(this);
#elif UNITY_IOS && FFMPEG_UNITY_USE_PIPE
            captureImp_ = new FfmpegCaptureImpIOSPipe(this);
#elif UNITY_IOS && !FFMPEG_UNITY_USE_PIPE
            captureImp_ = new FfmpegCaptureImpIOSMemory(this);
#endif

            RunOptions = " -y -re ";

            for (int captureLoop = 0; captureLoop < CaptureSources.Length; captureLoop++)
            {
                string fileName = null;

                switch (CaptureSources[captureLoop].Type)
                {
                    case CaptureSource.SourceType.Video_GameView:
                        {
                            reverse_[captureLoop] = captureImp_.Reverse;

                            int width;
                            int height;
                            if (CaptureSources[captureLoop].Width <= 0 && CaptureSources[captureLoop].Height <= 0)
                            {
                                width = Screen.width;
                                height = Screen.height;
                            }
                            else if (CaptureSources[captureLoop].Width <= 0)
                            {
                                width = Screen.width * CaptureSources[captureLoop].Height / Screen.height;
                                height = CaptureSources[captureLoop].Height;
                            }
                            else if (CaptureSources[captureLoop].Height <= 0)
                            {
                                width = CaptureSources[captureLoop].Width;
                                height = Screen.height * CaptureSources[captureLoop].Width / Screen.width;
                            }
                            else
                            {
                                width = CaptureSources[captureLoop].Width;
                                height = CaptureSources[captureLoop].Height;
                            }
                            tempTextures_[captureLoop] = new Texture2D(width, height, TextureFormat.RGBA32, false);

                            fileName = captureImp_.GenerateCaptureFileNameVideo(width, height);

                            var captureId = captureLoop;
                            var thread = new Thread(() => { writeVideo(captureId, fileName, width, height); });
                            thread.Start();
                            threads_.Add(thread);

                            RunOptions += " -r " + CaptureSources[captureLoop].FrameRate + " -f rawvideo -s " + width + "x" + height + " -pix_fmt rgba -i \"" + fileName + "\" ";
                        }
                        break;
                    case CaptureSource.SourceType.Video_Camera:
                        {
                            reverse_[captureLoop] = true;

                            int baseWidth = (int)(Screen.width * CaptureSources[captureLoop].SourceCamera.rect.width);
                            int baseHeight = (int)(Screen.height * CaptureSources[captureLoop].SourceCamera.rect.height);
                            int width;
                            int height;
                            if (CaptureSources[captureLoop].Width <= 0 && CaptureSources[captureLoop].Height <= 0)
                            {
                                width = baseWidth;
                                height = baseHeight;
                            }
                            else if (CaptureSources[captureLoop].Width <= 0)
                            {
                                width = baseWidth * CaptureSources[captureLoop].Height / baseHeight;
                                height = CaptureSources[captureLoop].Height;
                            }
                            else if (CaptureSources[captureLoop].Height <= 0)
                            {
                                width = CaptureSources[captureLoop].Width;
                                height = baseHeight * CaptureSources[captureLoop].Width / baseWidth;
                            }
                            else
                            {
                                width = CaptureSources[captureLoop].Width;
                                height = CaptureSources[captureLoop].Height;
                            }
                            tempRenderTextures_[captureLoop] = new RenderTexture(width, height, 16);
                            tempTextures_[captureLoop] = new Texture2D(width, height, TextureFormat.RGBA32, false);

                            fileName = captureImp_.GenerateCaptureFileNameVideo(width, height);

                            var captureId = captureLoop;
                            var thread = new Thread(() => { writeVideo(captureId, fileName, width, height); });
                            thread.Start();
                            threads_.Add(thread);

                            RunOptions += " -r " + CaptureSources[captureLoop].FrameRate + " -f rawvideo -s " + width + "x" + height + " -pix_fmt rgba -i \"" + fileName + "\" ";
                        }
                        break;
                    case CaptureSource.SourceType.Video_RenderTexture:
                        {
                            reverse_[captureLoop] = true;

                            RenderTexture renderTexture = CaptureSources[captureLoop].SourceRenderTexture;
                            int width = renderTexture.width;
                            int height = renderTexture.height;
                            tempTextures_[captureLoop] = new Texture2D(width, height, TextureFormat.RGBA32, false);

                            fileName = captureImp_.GenerateCaptureFileNameVideo(width, height);

                            var captureId = captureLoop;
                            var thread = new Thread(() => { writeVideo(captureId, fileName, width, height); });
                            thread.Start();
                            threads_.Add(thread);

                            RunOptions += " -r " + CaptureSources[captureLoop].FrameRate + " -f rawvideo -s " + width + "x" + height + " -pix_fmt rgba -i \"" + fileName + "\" ";
                        }
                        break;
                    case CaptureSource.SourceType.Audio_AudioListener:
                        {
                            var channelMode = AudioSettings.GetConfiguration().speakerMode;
                            int channels = 1;
                            switch (channelMode)
                            {
                                case AudioSpeakerMode.Mono:
                                    channels = 1;
                                    break;
                                case AudioSpeakerMode.Stereo:
                                case AudioSpeakerMode.Prologic:
                                    channels = 2;
                                    break;
                                case AudioSpeakerMode.Quad:
                                    channels = 4;
                                    break;
                                case AudioSpeakerMode.Surround:
                                    channels = 5;
                                    break;
                                case AudioSpeakerMode.Mode5point1:
                                    channels = 6;
                                    break;
                                case AudioSpeakerMode.Mode7point1:
                                    channels = 8;
                                    break;
                            }

                            GameObject captureAudioListenerGameObj = new GameObject();
                            var audioSource = captureAudioListenerGameObj.AddComponent<AudioSource>();
                            audioSource.clip = AudioClip.Create("", AudioSettings.outputSampleRate, channels, AudioSettings.outputSampleRate, true);
                            audioSource.loop = true;

                            captureAudioListener_ = captureAudioListenerGameObj.AddComponent<FfmpegCaptureAudioListener>();
                            captureAudioListener_.StreamId = captureLoop;
                            captureAudioListener_.Channels = channels;

                            audioSource.Play();

                            fileName = captureImp_.GenerateCaptureFileNameAudio(AudioSettings.outputSampleRate, channels);

                            var captureId = captureLoop;
                            int sampleRate = AudioSettings.outputSampleRate;
                            var thread = new Thread(() => { writeAudio(captureId, fileName, sampleRate, channels); });
                            thread.Start();
                            threads_.Add(thread);

                            RunOptions += " -f f32le -ar " + AudioSettings.outputSampleRate + " -ac " + channels + " -i \"" + fileName + "\" ";
                        }
                        break;
                    case CaptureSource.SourceType.Audio_AudioSource:
                        {
                            var captureAudio = CaptureSources[captureLoop].SourceAudio.gameObject.AddComponent<FfmpegCaptureAudio>();
                            captureAudio.StreamId = captureLoop;
                            captureAudio.Capture = this;
                            captureAudio.Mute = CaptureSources[captureLoop].Mute;

                            while (!audioChannels_.ContainsKey(captureLoop))
                            {
                                yield return null;
                            }

                            fileName = captureImp_.GenerateCaptureFileNameAudio(AudioSettings.outputSampleRate, audioChannels_[captureLoop]);

                            var captureId = captureLoop;
                            int sampleRate = AudioSettings.outputSampleRate;
                            int audioThreadChannels = audioChannels_[captureLoop];
                            var thread = new Thread(() => {
                                writeAudio(captureId, fileName, sampleRate, audioThreadChannels);
                            });
                            thread.Start();
                            threads_.Add(thread);

                            RunOptions += " -f f32le -ar " + AudioSettings.outputSampleRate + " -ac " + audioChannels_[captureLoop] + " -i \"" + fileName + "\" ";
                        }
                        break;
                }
            }

            RunOptions += " " + CaptureOptions;

            ByteStart();

            IsFinishedBuild = true;

            while (!captureImp_.IsEnd)
            {
                yield return new WaitForEndOfFrame();

                for (int captureLoop = 0; captureLoop < CaptureSources.Length; captureLoop++)
                {
                    if (CaptureSources[captureLoop].Type <= CaptureSource.SourceType.Video_RenderTexture)
                    {
                        if (!tempTextures_.ContainsKey(captureLoop))
                        {
                            continue;
                        }

                        RenderTexture srcTexture = null;
                        switch (CaptureSources[captureLoop].Type)
                        {
                            case CaptureSource.SourceType.Video_GameView:
                                {
                                    RenderTexture screenTexture = RenderTexture.GetTemporary(Screen.width, Screen.height);
                                    ScreenCapture.CaptureScreenshotIntoRenderTexture(screenTexture);
                                    srcTexture = RenderTexture.GetTemporary(tempTextures_[captureLoop].width, tempTextures_[captureLoop].height);
                                    Graphics.Blit(screenTexture, srcTexture);
                                    RenderTexture.ReleaseTemporary(screenTexture);
                                }
                                break;
                            case CaptureSource.SourceType.Video_Camera:
                                {
                                    var camera = CaptureSources[captureLoop].SourceCamera;

                                    srcTexture = tempRenderTextures_[captureLoop];
                                    var tempTexture = camera.targetTexture;

                                    camera.targetTexture = srcTexture;

                                    camera.Render();

                                    camera.targetTexture = tempTexture;
                                }
                                break;
                            case CaptureSource.SourceType.Video_RenderTexture:
                                {
                                    srcTexture = CaptureSources[captureLoop].SourceRenderTexture;
                                    if (srcTexture == null)
                                    {
                                        UnityEngine.Debug.LogError("Error: SourceRenderTexture is not set.");
                                        yield break;
                                    }
                                }
                                break;
                        }

                        RenderTexture filpedTexture = srcTexture;
                        if (reverse_[captureLoop])
                        {
                            filpedTexture = RenderTexture.GetTemporary(srcTexture.width, srcTexture.height);
                            Graphics.Blit(srcTexture, filpedTexture, flipMaterial_);
                        }

                        var tempTextureActive = RenderTexture.active;

                        RenderTexture.active = filpedTexture;

                        tempTextures_[captureLoop].ReadPixels(new Rect(0, 0, filpedTexture.width, filpedTexture.height), 0, 0);
                        tempTextures_[captureLoop].Apply();

                        RenderTexture.active = tempTextureActive;

                        if (reverse_[captureLoop])
                        {
                            RenderTexture.ReleaseTemporary(filpedTexture);
                        }

                        var textureData = tempTextures_[captureLoop].GetRawTextureData<byte>().ToArray();
                        byte[] bufferData = new byte[tempTextures_[captureLoop].width * tempTextures_[captureLoop].height * 4];

                        Array.Copy(textureData, 0, bufferData, 0, bufferData.Length);
                        lock (videoBuffers_)
                        {
                            videoBuffers_[captureLoop] = bufferData;
                        }

                        if (CaptureSources[captureLoop].Type == CaptureSource.SourceType.Video_GameView)
                        {
                            RenderTexture.ReleaseTemporary(srcTexture);
                        }
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (captureImp_ != null && captureImp_.IsEnd)
            {
                StopFfmpeg();
                return;
            }

            if (captureAudioListener_ == null)
            {
                return;
            }
            if (!IsFinishedBuild || audioBuffers_ == null)
            {
                captureAudioListener_.ReadCount = 0;
                return;
            }

            if (!audioBuffers_.ContainsKey(captureAudioListener_.StreamId))
            {
                audioBuffers_[captureAudioListener_.StreamId] = new List<float>();
            }
            //lock (audioBuffers_[captureAudioListener_.StreamId])
            {
                audioBuffers_[captureAudioListener_.StreamId].AddRange(captureAudioListener_.Read());
            }
        }

        void writeVideo(int streamId, string pipeFileName, int width, int height)
        {
            captureImp_.WriteVideo(streamId, pipeFileName, width, height, videoBuffers_);
        }

        void writeAudio(int streamId, string pipeFileName, int sampleRate, int channels)
        {
            captureImp_.WriteAudio(streamId, pipeFileName, sampleRate, channels, audioBuffers_);
        }

        public void OnAudioFilterWriteToCaptureAudio(float[] data, int channels, int streamId)
        {
            audioChannels_[streamId] = channels;

            if (!audioBuffers_.ContainsKey(streamId))
            {
                audioBuffers_[streamId] = new List<float>();
            }
            //lock (audioBuffers_[streamId])
            {
                audioBuffers_[streamId].AddRange(data);
            }
        }
    }
}
