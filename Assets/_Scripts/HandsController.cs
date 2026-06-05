using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HandsController : MonoBehaviour
{
    public SceneManager sceneManager;
    private bool leftHandInside = false;
    private bool rightHandInside = false;
    private bool bothHandsInside = false;
    public AudioSource audioSource;
    private bool isVoiceOver = false;
    public bool isExamScene = false;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("LeftHand") && SafeAreaController.isPlayerInside == true)
        {
            leftHandInside = true;
            CheckHands();
        }

        if (other.CompareTag("RightHand") && SafeAreaController.isPlayerInside == true )
        {
            rightHandInside = true;
            CheckHands();
        }
    }

    public void CheckHands()
    {
        if (leftHandInside && rightHandInside && ParticleController.IsAducationSceneFinished == false && isVoiceOver == false && sceneManager.isSceneStart == true && SafeAreaController.isPlayerInside == true)
        {
            ScoreManager.Instance.AddScore(30);
            if (isExamScene == false)
            {
                audioSource.Play();

            }
            isVoiceOver = true;
         bothHandsInside = true;   
         Debug.Log("[System] İki elide girdi");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("LeftHand"))
        {
            leftHandInside = false;
        }

        if (other.CompareTag("RightHand"))
        {
            rightHandInside = false;
        }
    }
}
