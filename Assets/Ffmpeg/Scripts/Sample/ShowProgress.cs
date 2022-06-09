using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FfmpegUnity.Sample
{
    public class ShowProgress : MonoBehaviour
    {
        public Text TextUI;
        public FfmpegCommand TargetCommand;

        IEnumerator Start()
        {
            TargetCommand.GetProgressOnScript = true;
            TargetCommand.StartFfmpeg();

            TextUI.text = "Status: Is Starting...";

            while (!TargetCommand.IsRunning)
            {
                yield return null;
            }

            while (TargetCommand.IsRunning)
            {
                TextUI.text = "Progress: " + string.Format("{0:0.00}", TargetCommand.Progress * 100f) + "%\nRemainingTime: " + TargetCommand.RemainingTime.ToString(@"hh\:mm\:ss\.ff") + string.Format(" ({0:0.00}x)", TargetCommand.Speed);
                yield return null;
            }

            TextUI.text = "Status: Is Finished";
        }
    }
}
