using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
public class AvanlancheTrigger : MonoBehaviour
{
   public ParticleSystem particle1;
   public ParticleSystem particle2;
   public ParticleSystem particle3;
   public ParticleSystem particle4;
   
   public ParticleSystem particle5;

   public ParticleSystem particle6;
   public ParticleSystem particle7;
   public static bool isAvalancheNear = false;
   public GameObject terrain;
   public GameObject warningObject;
   public GameObject secondUIPanel;

   private void Start()
   {
      secondUIPanel.SetActive(false);
   }

   private void OnTriggerEnter(Collider other)
   {
      if (other.CompareTag("Collider1"))
      {
         particle1.Play();
         particle2.Play();
         Debug.Log("triggered1");
      }
      if (other.CompareTag("Collider2"))
      {
         particle3.Play();
         particle4.Play();
         Debug.Log("triggered2");

      }

      if (other.CompareTag("Collider3"))
      {
         particle5.Play();
         particle6.Play();
         Debug.Log("triggered3");

      }
      if (other.CompareTag("Collider4"))
      {
         particle7.Play();
         warningObject.SetActive(false);
         isAvalancheNear = true;
         Debug.Log("Smoke Yaklaştı");
         transform.DOMoveY(402f, 3).From(348f);
         warningObject.transform.position = new Vector3(1706.13562f, 391.006714f, 4461.86816f);
         warningObject.transform.rotation = Quaternion.Euler(-70.541f, 0, 0);
         warningObject.SetActive(true);
         secondUIPanel.SetActive(true);

      }
      
   }
}
