using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterColliderTrigger : MonoBehaviour
{
    public static bool isTreeNear = false;
    public SceneManager sceneManager;
    public AudioSource treeWarningVoice;
    private bool isWarningVoicePlayed = false;
    public bool isExamScene = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Tree") && sceneManager.isSceneStart == true)
        {
            ScoreManager.Instance.DecreaseScore(20);
            isTreeNear = true;
            Debug.Log("Karakter İçeride");
            if (isWarningVoicePlayed == false && isExamScene == false)
            {
                treeWarningVoice.Play();
                isWarningVoicePlayed = true;

            }
          
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Tree") && sceneManager.isSceneStart == true)
        {
            isTreeNear = false;
            Debug.Log("Karakter Dışarıda");
        }
    }
}
