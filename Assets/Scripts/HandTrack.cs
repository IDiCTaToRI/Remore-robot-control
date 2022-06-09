using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Networking;


public class HandTrack : MonoBehaviour
{
     private Vector2 moveVector2;
     float xMoveCoord = 0;
     float yMoveCoord = 0;
     float speedValue;

    void Update()
    {
        if (InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.primary2DAxis,out moveVector2))    
                {
                //Debug.Log("CommonUsages.primary2DAxis:" + "x:"+moveVector2.x+"y:"+moveVector2.y);
                if(moveVector2.x > (-0.5) && moveVector2.x < 0.5 && moveVector2.y > 0.5 && moveVector2.y < 1){

                	xMoveCoord = 0;
                	yMoveCoord = 0;
                	speedValue = 50;
                	}
                if(moveVector2.x > (-0.5) && moveVector2.x < 0.5 && moveVector2.y < (-0.5) && moveVector2.y > (-1)){

                	xMoveCoord = 1;
                	yMoveCoord = 1;
                	speedValue = 50;
                	}
                if(moveVector2.x < (-0.5) && moveVector2.x > (-1) && moveVector2.y > (-0.5) && moveVector2.y < 0.4){
                	xMoveCoord = 0;
                	yMoveCoord = 1;
                	speedValue = 50;
                	}
                if(moveVector2.x > (0.5) && moveVector2.x < 1 && moveVector2.y > (-0.5) && moveVector2.y < 0.4){

                	xMoveCoord = 1;
                	yMoveCoord = 0;
                	speedValue = 50;
                	}
                if(moveVector2.x == 0 && moveVector2.y == 0){
                	xMoveCoord = 0;
                	yMoveCoord = 0;
                	speedValue = 0;
                	}
                }     
    }
    	

    IEnumerator GetRequest(string uri)
    {
    	
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            // Request and wait for the desired page.
            //webRequest.SetRequestHeader("Keep-Alive", "600");
            //yield return new WaitForSeconds(1);
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

    void Start(){
    	StartCoroutine(ExampleCoroutine());
    }
    
    IEnumerator ExampleCoroutine()
    {
    	StartCoroutine(GetRequest("https://alex-ctrl.avatar.keenetic.name/api/Board/Send?comandJson={\"AnswerIsRequired\":1,\"Engines\":[{\"EngineNumber\":1,\"Direct\":" + xMoveCoord.ToString() + ",\"Speed\":" + speedValue.ToString() + ",\"WorkTimeMs\":511},{\"EngineNumber\":2,\"Direct\":" + yMoveCoord.ToString() + ",\"Speed\":" + speedValue.ToString() + ",\"WorkTimeMs\":511}],\"Code\":81}"));
        //StartCoroutine(GetRequest("https://vrbot-ctrl.avatar.keenetic.name/api/Board/Send?comandJson={\"CameraNumber\":1,\"AnswerIsRequired\":1,\"CameraMoveDir\":[{\"Axis\":1,\"MovDeg\":" + Ycoord.ToString() + "},{\"Axis\":2,\"MovDeg\":" + Xcoord.ToString() + "}],\"Code\":113}"));
    	 yield return new WaitForSeconds(0.5f);
         StartCoroutine(ExampleCoroutine());
    }
}