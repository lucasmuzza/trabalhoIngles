
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance { get; private set; }

    public Vector2 consoleNagivateInput;

    [Header("Input Config")]
    [SerializeField] private float movementDeadzone = 0.15f;
    [SerializeField] private PlayerInput playerInput;

    [Header("Debugging")]
    [SerializeField] private bool showDebugLogs = true;

    public event Action OnPausePressed;

    public event Action OnReturnPressed;
    public event Action OnConfirmPressed;

    // Console Actions
    public event Action<bool> OnConsoleToggle;
    public event Action OnConsoleConfirm;
    public event Action OnConsoleClosed;

    private bool isConsoleOpen;

    public void SwitchToUI()
    {
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");
    }

    public void SwitchToGameplay()
    {
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Gameplay");
    }

    public void SwitchToConsole()
    {
        if (playerInput == null)
            return;

        playerInput.SwitchCurrentActionMap("Console");
    }

    public void HandleType(InputAction.CallbackContext context)
    {

    }

    public void HandleReturn(InputAction.CallbackContext context)
    {
        if (context.performed)
            OnReturnPressed?.Invoke();
    }

    public void HandlePause(InputAction.CallbackContext context)
    {
        if (context.performed)
            OnPausePressed?.Invoke();
    }

    public void OnConsoleNavigateInput(
        InputAction.CallbackContext context)
    {
        if (context.performed)
            consoleNagivateInput = context.ReadValue<Vector2>();
    }

    public void OnConsoleToggleInput(
        InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isConsoleOpen = !isConsoleOpen;
            OnConsoleToggle?.Invoke(isConsoleOpen);
        }
    }

    public void OnConsoleConfirmInput(
        InputAction.CallbackContext context)
    {
        if (context.performed)
            OnConsoleConfirm?.Invoke();
    }

    public void OnConsoleCloseInput(
        InputAction.CallbackContext context)
    {
        if (context.performed)
            OnConsoleClosed?.Invoke();
    }

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

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();
    }

    private void Update()
    {

    }
}
