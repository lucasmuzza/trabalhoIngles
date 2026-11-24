using System;
using System.Collections;
using Unity.Netcode;
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
    private bool subscribedToNetSceneManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        fadeSystem = FadeSystem.Instance;
        TrySubscribeToNetcodeSceneEvents();
    }

    private void Update()
    {
        if (!subscribedToNetSceneManager)
            TrySubscribeToNetcodeSceneEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleNetSceneLoadCompleted;
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleNetSceneEvent;
            }
            Instance = null;
        }
    }

    private void TrySubscribeToNetcodeSceneEvents()
    {
        if (subscribedToNetSceneManager) return;
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) return;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleNetSceneLoadCompleted;
        NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleNetSceneEvent;
        subscribedToNetSceneManager = true;
    }

    public void SetInitialGameplayScene(string sceneName)
    {
        CurrentGameplayScene = sceneName;
    }

    /// <summary>
    /// Called on all peers when the network match transition begins: fades to black
    /// and, on the server, triggers Netcode's synchronized scene load.
    /// </summary>
    public void BeginNetworkTransition(string targetSceneName, LoadSceneMode loadMode = LoadSceneMode.Single)
    {
        if (string.IsNullOrEmpty(targetSceneName) || isTransitioning)
            return;

        StartCoroutine(NetworkTransitionRoutine(targetSceneName, loadMode));
    }

    private IEnumerator NetworkTransitionRoutine(string targetSceneName, LoadSceneMode loadMode)
    {
        isTransitioning = true;

        if (fadeSystem != null)
        {
            fadeSystem.FadeIn(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(targetSceneName, loadMode);
        }
    }

    private void HandleNetSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
        {
            if (!string.IsNullOrEmpty(sceneEvent.SceneName))
                CurrentGameplayScene = sceneEvent.SceneName;
        }
    }

    private void HandleNetSceneLoadCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        System.Collections.Generic.List<ulong> clientsCompleted,
        System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        if (!string.IsNullOrEmpty(sceneName))
            CurrentGameplayScene = sceneName;

        StartCoroutine(FinishFadeRoutine());
    }

    private IEnumerator FinishFadeRoutine()
    {
        if (fadeSystem != null)
        {
            fadeSystem.FadeOut(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        isTransitioning = false;
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

        if (string.IsNullOrEmpty(CurrentGameplayScene))
        {
            string activeScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(activeScene) && activeScene != targetSceneName)
            {
                CurrentGameplayScene = activeScene;
            }
            else
            {
                Debug.LogError("[TransitionSystem]: Não é possível mudar de cena porque a cena atual não foi definida!");
                return;
            }
        }

        if (isTransitioning || CurrentGameplayScene == targetSceneName)
            return;

        StartCoroutine(LocalTransitionRoutine(CurrentGameplayScene, targetSceneName, onComplete));
    }

    private IEnumerator LocalTransitionRoutine(string oldScene, string newScene, Action onComplete)
    {
        isTransitioning = true;

        if (fadeSystem != null)
        {
            fadeSystem.FadeIn(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(oldScene);
        if (unloadOp != null)
        {
            while (!unloadOp.isDone)
                yield return null;
        }

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
        onComplete?.Invoke();
    }
}