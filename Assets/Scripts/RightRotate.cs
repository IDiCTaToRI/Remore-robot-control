using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RightRotate : MonoBehaviour
{
    // Start is called before the first frame update
    public void ButtonRightPressed()
    {
      

        StartCoroutine(GetRequest("https://vrbot-ctrl.avatar.keenetic.name/api/Board/Send?comandJson={\"CameraNumber\":1,\"AnswerIsRequired\":1,\"CameraMoveDir\":[{\"Axis\":1,\"MovDeg\":160},{\"Axis\":2,\"MovDeg\":90}],\"Code\":113}"));

    }

    // Update is called once per frame
    void Update()
    {
        
    }
    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            string[] pages = uri.Split('/');
            int page = pages.Length - 1;

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(pages[page] + ": Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(pages[page] + ": HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);
                    break;
            }
        }
    }
}
