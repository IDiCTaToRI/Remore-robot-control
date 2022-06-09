using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Networking;

public class VRHandTest : MonoBehaviour
{
    private Vector2 moveVector2;
    float xMoveCoord = 0;
    float yMoveCoord = 0;
    void Start()
    {
        
    }

    void Update()
    {
        if (InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.primary2DAxis,out moveVector2))    
                {
                //Debug.Log("CommonUsages.primary2DAxis:" + "x:"+moveVector2.x+"y:"+moveVector2.y);
                if(moveVector2.x > (-0.5) && moveVector2.x < 0.5 && moveVector2.y > 0.5){
                	Debug.Log("Vpered");
                	xMoveCoord = 0;
                	yMoveCoord = 0;

                	}
                if(moveVector2.x > (-0.5) && moveVector2.x < 0.5 && moveVector2.y < (-0.5)){
                	Debug.Log("Nazad");
                	xMoveCoord = 1;
                	yMoveCoord = 1;

                	}
                if(moveVector2.x < (-0.5) && moveVector2.x > (-1) && moveVector2.y > (-0.5) && moveVector2.y < 0.4){
                	Debug.Log("Vlevo");
                	xMoveCoord = 1;
                	yMoveCoord = 0;
 
                	}
                if(moveVector2.x > (0.5) && moveVector2.x < 1 && moveVector2.y > (-0.5) && moveVector2.y < 0.4){
                	Debug.Log("Vpravo");
                	xMoveCoord = 0;
                	yMoveCoord = 1;
                	}
                if(moveVector2.x == 0 && moveVector2.y == 0){
                	Debug.Log("Stoim");
                	xMoveCoord = 0;
                	yMoveCoord = 0;

                	}
                }     
            
        }
}


