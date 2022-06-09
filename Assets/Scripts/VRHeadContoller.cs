using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class VRHeadContoller : MonoBehaviour
{
   public Transform rotate;
   float varmy; // is right-left
   float varyou; // is up-down
   float Xcoord;
   float Ycoord;
    public void Update()
    {

    	varyou = rotate.rotation.eulerAngles.x;
        varmy = rotate.rotation.eulerAngles.y;
        ParamsStats();
        //StartCoroutine(ExampleCoroutine());
    }

    private void ParamsStats(){
        
        if(varmy <= 90 && varyou <= 90){
            Xcoord = Mathf.CeilToInt(90 - varyou);
            Ycoord = Mathf.CeilToInt(90 + varmy);

        }
        if(varmy <= 90 && varyou > 90){
            Xcoord = Mathf.CeilToInt(90 + (360 - varyou));
            Ycoord = Mathf.CeilToInt(90 + varmy);

        }
        if(varmy > 90 && varyou <= 90){
            Xcoord = Mathf.CeilToInt(90 - varyou);
            Ycoord = Mathf.CeilToInt(90 - (360 - varmy));

        }
            if(varmy > 90 && varyou > 90){
            Xcoord = Mathf.CeilToInt(90 + (360 - varyou));
            Ycoord = Mathf.CeilToInt(90 - (360 - varmy));

        }
    }

    void Start(){
    	StartCoroutine(StartusCoroutine());
    }
    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
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

        IEnumerator ExampleCoroutine()
    {
        StartCoroutine(GetRequest("https://vrbot-ctrl.avatar.keenetic.name/api/Board/Send?comandJson={\"CameraNumber\":1,\"AnswerIsRequired\":1,\"CameraMoveDir\":[{\"Axis\":1,\"MovDeg\":" + Ycoord.ToString() + "},{\"Axis\":2,\"MovDeg\":" + Xcoord.ToString() + "}],\"Code\":113}"));
    	 yield return new WaitForSeconds(2);
         StartCoroutine(ExampleCoroutine());
    }
    IEnumerator StartusCoroutine()
    {
        StartCoroutine(GetRequest("https://vrbot-ctrl.avatar.keenetic.name/api/Board/Send?comandJson={\"CameraNumber\":1,\"AnswerIsRequired\":1,\"CameraMoveDir\":[{\"Axis\":1,\"MovDeg\":90},{\"Axis\":2,\"MovDeg\":90}],\"Code\":113}"));
         yield return new WaitForSeconds(4);
         StartCoroutine(ExampleCoroutine());
         Debug.Log("Started");
    }
    
}
