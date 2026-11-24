using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class FadeSystem : MonoBehaviour
{
    public static FadeSystem Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;
    private VisualElement _fadeOverlay;
    private Coroutine _currentFadeCoroutine;
    private float _currentOpacity = 0f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        SetupUI();
    }

    private void Start()
    {
        GlobalUIEvents.OnFadeIn += (duration) => FadeIn(duration);
        GlobalUIEvents.OnFadeOut += (duration) => FadeOut(duration);
    }

    private void OnDisable()
    {
        GlobalUIEvents.OnFadeIn -= (duration) => FadeIn(duration);
        GlobalUIEvents.OnFadeOut -= (duration) => FadeOut(duration);
    }

    private void OnDestroy()
    {
        GlobalUIEvents.OnFadeIn -= (duration) => FadeIn(duration);
        GlobalUIEvents.OnFadeOut -= (duration) => FadeOut(duration);
    }

    private void SetupUI()
    {
        var root = uiDocument.rootVisualElement;

        _fadeOverlay = new VisualElement();
        _fadeOverlay.name = "GlobalFadeOverlay";

        _fadeOverlay.style.position = Position.Absolute;
        _fadeOverlay.style.left = 0;
        _fadeOverlay.style.top = 0;
        _fadeOverlay.style.right = 0;
        _fadeOverlay.style.bottom = 0;
        _fadeOverlay.style.opacity = 0f;
        _fadeOverlay.style.backgroundColor = Color.black;

        _fadeOverlay.pickingMode = PickingMode.Ignore;

        root.Add(_fadeOverlay);
    }

    public void FadeIn(float duration = 1f) => StartFade(1f, duration);
    public void FadeOut(float duration = 1f) => StartFade(0f, duration);

    private void StartFade(float target, float duration)
    {
        if (_currentFadeCoroutine != null) StopCoroutine(_currentFadeCoroutine);
        _currentFadeCoroutine = StartCoroutine(FadeRoutine(target, duration));
    }

    private IEnumerator FadeRoutine(float target, float duration)
    {
        float start = _currentOpacity;
        float elapsed = 0f;

        // Se o fade for para escurecer (In), bloqueia cliques no jogo
        _fadeOverlay.pickingMode = target > 0.5f ? PickingMode.Position : PickingMode.Ignore;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Suaviza a interpolação para não ser linear
            _currentOpacity = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, t));

            ApplyOpacity(_currentOpacity);
            yield return null;
        }

        _currentOpacity = target;
        ApplyOpacity(_currentOpacity);
        _currentFadeCoroutine = null;
    }

    private void ApplyOpacity(float opacity)
    {
        // Forçar a atualização do StyleFloat
        _fadeOverlay.style.opacity = new StyleFloat(Mathf.Clamp01(opacity));

        // Se estiver totalmente transparente, desativa o display para performance
        _fadeOverlay.style.display = opacity <= 0.001f ? DisplayStyle.None : DisplayStyle.Flex;
    }
}