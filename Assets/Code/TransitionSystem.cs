using System;
using System.Collections;
using UnityConsole;
using UnityEngine;
using UnityEngine.SceneManagement;

[ConsoleContainer("transitionSystem")]
public class TransitionSystem : MonoBehaviour
{
    public static TransitionSystem Instance { get; private set; }

    [Header("Configurações de Transição")]
    [SerializeField] private float fadeDuration = 1.5f;
    private FadeSystem fadeSystem;

    public string CurrentGameplayScene { get; private set; }
    private bool isTransitioning;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        fadeSystem = FadeSystem.Instance;
    }

    public void SetInitialGameplayScene(string sceneName)
    {
        CurrentGameplayScene = sceneName;
    }

    [ConsoleCommand(name: "load_scene", description: "Carrega a cena com o nome passado ex MainMenu")]
    public void LoadScene(string targetSceneName)
    {
        LoadScene(targetSceneName, null);
    }

    public void LoadScene(string targetSceneName, Action onComplete)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[TransitionSystem]: Nome da cena alvo está vazio!");
            return;
        }

        if (isTransitioning || CurrentGameplayScene == targetSceneName)
            return;

        if (string.IsNullOrEmpty(CurrentGameplayScene))
        {
            Debug.LogError("[TransitionSystem]: Não é possível mudar de cena porque a cena atual não foi definida!");
            return;
        }

        StartCoroutine(TransitionRoutine(CurrentGameplayScene, targetSceneName, onComplete));
    }

    private IEnumerator TransitionRoutine(string oldScene, string newScene, Action onComplete)
    {
        isTransitioning = true;

        if (fadeSystem != null)
        {
            fadeSystem.FadeIn(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        Debug.Log($"[TransitionSystem]: Descarregando cena antiga assincronamente: {oldScene}");
        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(oldScene);
        if (unloadOp != null)
        {
            while (!unloadOp.isDone)
                yield return null;
        }

        Debug.Log($"[TransitionSystem]: Carregando nova cena de forma aditiva: {newScene}");
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(newScene, LoadSceneMode.Additive);
        if (loadOp != null)
        {
            while (!loadOp.isDone)
                yield return null;
        }

        Scene newlyLoadedScene = SceneManager.GetSceneByName(newScene);
        if (newlyLoadedScene.IsValid())
        {
            SceneManager.SetActiveScene(newlyLoadedScene);
        }

        CurrentGameplayScene = newScene;

        if (fadeSystem != null)
        {
            fadeSystem.FadeOut(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        isTransitioning = false;
        Debug.Log($"[TransitionSystem]: Transição para a cena '{newScene}' concluída com sucesso.");
        onComplete?.Invoke();
    }
}