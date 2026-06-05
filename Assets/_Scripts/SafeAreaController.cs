using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class SafeAreaController : MonoBehaviour
{
    public static bool isPlayerInside = false;
    public AudioSource audioSource;
    private bool isAudioPlayed = false;
    public SceneManager sceneManager;
    public bool isExamScene = false;
    

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (isAudioPlayed == false && sceneManager.isSceneStart == true && ParticleController.IsAducationSceneFinished == false)
            {
                ScoreManager.Instance.AddScore(40);
                if (isExamScene == false)
                {
                    audioSource.Play(); 
  
                }
                isAudioPlayed = true;

            }
            isPlayerInside = true;
           Debug.Log("[System] player in safe area");
            
        }
       
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
        }
    }
}
