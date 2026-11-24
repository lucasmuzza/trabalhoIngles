using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(NetworkObject))]
public class PlayerOverheadTypingUI : NetworkBehaviour
{
    [Header("2D Overhead Positioning")]
    [SerializeField] private UIDocument overheadUIDocument;
    [SerializeField] private Vector2 worldOffset2D = new Vector2(0f, 1.8f);
    [SerializeField] private string labelElementName = "OverheadTypingLabel";

    private readonly NetworkVariable<FixedString64Bytes> currentTypedText =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString32Bytes> feedbackBadge =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Label typingLabel;
    private Camera mainCamera;

    private void Awake()
    {
        if (overheadUIDocument == null)
            overheadUIDocument = GetComponentInChildren<UIDocument>();

        if (overheadUIDocument != null)
        {
            typingLabel = overheadUIDocument.rootVisualElement.Q<Label>(labelElementName);
            if (typingLabel == null)
            {
                typingLabel = new Label { name = labelElementName };
                typingLabel.style.position = Position.Absolute;
                typingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                typingLabel.style.fontSize = 22;
                typingLabel.style.color = Color.white;
                typingLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.68f);
                typingLabel.style.paddingLeft = 10;
                typingLabel.style.paddingRight = 10;
                typingLabel.style.paddingTop = 4;
                typingLabel.style.paddingBottom = 4;
                typingLabel.style.borderTopLeftRadius = 6;
                typingLabel.style.borderTopRightRadius = 6;
                typingLabel.style.borderBottomLeftRadius = 6;
                typingLabel.style.borderBottomRightRadius = 6;
                overheadUIDocument.rootVisualElement.Add(typingLabel);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        mainCamera = Camera.main;

        currentTypedText.OnValueChanged += OnTypedTextChanged;
        feedbackBadge.OnValueChanged += OnFeedbackChanged;

        RefreshLabelText();
    }

    public override void OnNetworkDespawn()
    {
        currentTypedText.OnValueChanged -= OnTypedTextChanged;
        feedbackBadge.OnValueChanged -= OnFeedbackChanged;
        base.OnNetworkDespawn();
    }

    private void LateUpdate()
    {
        if (!IsSpawned || typingLabel == null) return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null) return;

        Vector3 worldPos = new Vector3(
            transform.position.x + worldOffset2D.x,
            transform.position.y + worldOffset2D.y,
            transform.position.z
        );

        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // Hide if behind camera or outside screen bounds in 2D
        if (screenPos.z < 0f || screenPos.x < 0f || screenPos.x > Screen.width || screenPos.y < 0f || screenPos.y > Screen.height)
        {
            typingLabel.style.display = DisplayStyle.None;
            return;
        }

        typingLabel.style.display = DisplayStyle.Flex;

        float uiX = screenPos.x - (typingLabel.resolvedStyle.width * 0.5f);
        float uiY = (Screen.height - screenPos.y) - typingLabel.resolvedStyle.height;

        typingLabel.style.left = uiX;
        typingLabel.style.top = uiY;
    }

    private void OnTypedTextChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        RefreshLabelText();
    }

    private void OnFeedbackChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        RefreshLabelText();
    }

    private void RefreshLabelText()
    {
        if (typingLabel == null) return;

        string typed = currentTypedText.Value.ToString();
        string badge = feedbackBadge.Value.ToString();

        if (!string.IsNullOrEmpty(badge))
            typingLabel.text = $"{typed}  [{badge}]";
        else if (!string.IsNullOrEmpty(typed))
            typingLabel.text = typed;
        else
            typingLabel.text = "...";
    }

    [ServerRpc()]
    public void SyncTypedTextServerRpc(FixedString64Bytes newText, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        currentTypedText.Value = newText;
        feedbackBadge.Value = "";
    }

    public void ServerSetSubmissionResult(bool isCorrect, string submittedWord)
    {
        if (!IsServer) return;

        currentTypedText.Value = submittedWord;
        feedbackBadge.Value = isCorrect ? "OK" : "X";
    }

    public void ServerClearTypedText()
    {
        if (!IsServer) return;

        currentTypedText.Value = "";
        feedbackBadge.Value = "";
    }
}