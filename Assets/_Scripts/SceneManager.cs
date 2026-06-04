using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : MonoBehaviour
{
    public bool isSceneStart = false;

    public void StartScene()
    {
        isSceneStart = true;
    }
    
    public void RestartScene()
    {
        // Başına tam yolunu yazarak çakışmayı önlüyoruz:
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneName);
    }

    // ❌ UYGULAMADAN ÇIKMA METODU
    public void QuitGame()
    {
        Debug.Log("Uygulamadan çıkış yapılıyor...");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    public void LoadNextScene()
    {
        // Mevcut sahnenin sırasını (Build Index) alıp 1 artırıyoruz
        int nextSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;

        // Eğer sonraki indeks toplam sahne sayısından küçükse sahneyi yükle
        if (nextSceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.LogWarning("Sırada başka sahne yok! Build Settings listesinin sonundasınız.");
        }
    }
}