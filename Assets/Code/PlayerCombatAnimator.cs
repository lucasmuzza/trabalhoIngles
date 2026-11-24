using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PlayerCombatAnimator : NetworkBehaviour
{
    [Header("2D Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string hitTriggerName = "Hit";

    [Header("2D Attack Hitbox Collider")]
    [Tooltip("Trigger Collider2D active during the attack swing. Keep disabled by default.")]
    [SerializeField] private Collider2D attackHitbox2D;
    [SerializeField] private float autoWindowDuration = 0.45f;

    private bool isAttackWindowOpen;
    private Coroutine hitboxWindowRoutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        SetHitboxActive(false);
    }

    /// <summary>
    /// Called on the server when this player wins the typing check and attacks.
    /// </summary>
    public void ServerStartAttack()
    {
        if (!IsServer) return;

        PlayAttackAnimationClientRpc();

        if (hitboxWindowRoutine != null)
            StopCoroutine(hitboxWindowRoutine);

        hitboxWindowRoutine = StartCoroutine(ServerAttackWindowCoroutine());
    }

    private IEnumerator ServerAttackWindowCoroutine()
    {
        isAttackWindowOpen = true;
        SetHitboxActive(true);

        yield return new WaitForSeconds(autoWindowDuration);

        isAttackWindowOpen = false;
        SetHitboxActive(false);
        hitboxWindowRoutine = null;
    }

    // Optional: Animation Event on your 2D Attack clip
    public void EnableAttackHitbox()
    {
        if (!IsServer) return;
        isAttackWindowOpen = true;
        SetHitboxActive(true);
    }

    // Optional: Animation Event on your 2D Attack clip
    public void DisableAttackHitbox()
    {
        if (!IsServer) return;
        isAttackWindowOpen = false;
        SetHitboxActive(false);
    }

    private void SetHitboxActive(bool active)
    {
        if (attackHitbox2D != null)
            attackHitbox2D.enabled = active;
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        if (animator == null) return;
        animator.ResetTrigger(attackTriggerName);
        animator.SetTrigger(attackTriggerName);
    }

    /// <summary>
    /// Called on the server when hit by the opponent's 2D attack collider.
    /// </summary>
    public void ServerForceHitAnimation()
    {
        if (!IsServer) return;
        PlayHitAnimationClientRpc();
    }

    [ClientRpc]
    private void PlayHitAnimationClientRpc()
    {
        if (animator == null) return;
        animator.ResetTrigger(hitTriggerName);
        animator.SetTrigger(hitTriggerName);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer || !isAttackWindowOpen) return;

        PlayerCombatAnimator targetCombat = other.GetComponentInParent<PlayerCombatAnimator>();
        if (targetCombat == null || targetCombat == this) return;

        // Ignore if the other collider is another attack hitbox
        if (other == targetCombat.attackHitbox2D) return;

        isAttackWindowOpen = false;
        SetHitboxActive(false);
        targetCombat.ServerForceHitAnimation();
    }
}