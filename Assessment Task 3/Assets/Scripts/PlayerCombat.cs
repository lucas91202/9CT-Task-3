using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[RequireComponent(typeof(playercon))]
public class PlayerCombat : MonoBehaviour, IDamageable
{
    // some common prefixes i use: (Note: will not be the same across scripts)
    // A = Attacking or variables that relate to attacking
    // P = Period, like grace period for example
    // S = animation states
    playercon pc;
    SpriteRenderer sr;
    Rigidbody2D rb;
    Color baseColor;
    Animator anim;
    
    [Header("Parry animation timing")]
    public float parryHitAnimTime = .2f;
    public float perfectParryAnimTime = 1f;
    float parryAnimTimer;
    int parryAnim;
    public bool ParryAnimationActive => pTimer > 0f || parryAnimTimer > 0f;

    [Header("Parry")]
    public KeyCode pKey = KeyCode.Mouse1;
    public float pWindow = .25f;
    public float PerfectpWindow = 0f;
    public float pCooldown = .6f;
    public float pSuccessGrace = .3f;
    public Color pColor = Color.cyan;
    public Color counterReadyColor = Color.yellow;
    float pTimer;
    float pCooldownTimer;
    float pGraceTimer;

    [Header("attack anims")]
    public float AAnimTime = .25f;
    static int AttackS = Animator.StringToHash("MukuroAttack");
    static int AttackUpS = Animator.StringToHash("MukuroAttackUp");
    static int AttackDownS = Animator.StringToHash("MukuroAttackDown");
    static int ParryStartS = Animator.StringToHash("PARRY");
    static int ParryHitS = Animator.StringToHash("PARRYHIT");
    static int PerfectParryS = Animator.StringToHash("MukuroParryPerfect");
    int attackAnim;
    float AAnimTimer;

    [Header("Perfect Parry")]
    public ParryProjectile parryProjectile;
    public float counterWindow = 2f;
    public float projectileSpawnOffset = .8f;
    float counterTimer;
    public bool autoCounter = true;
    public bool CounterReady => counterTimer > 0f;

    [Header("Parry FX")]
    public GameObject parryFx; 
    public GameObject perfectParryFx;
    public float parryFxOffset = .6f;
    public float parryFxLifeTime = 1f;

    [Header("Player Health")]
    public int maxHealth = 5;
    public int health;
    bool dead;
    public int Health => health; // for other scripts to read
    public bool IsDead => dead;

    [Header("I-frames")]
    public float hitIFrames = 1f;
    public float dashGraceP = .1f;
    public float flashTime = .08f;
    float iTimer;
    float gracePTimer;
    bool wasDashing;
    bool flashing;

    [Header("Attack")]
    public KeyCode Akey = KeyCode.Mouse0;
    public LayerMask enemyLayer;
    public int ADamage = 1;
    public float AReach = 1f;
    public float ARadius = .7f;
    public float ACooldown = .35f;
    public GameObject slash; // to show the attack
    public float slashLifeTime = .15f;
    float ATimer;

    [Header("Knockback")]
    public Vector2 HitKnockback = new Vector2(8f, 6f); //X means ur pushed sideways and Y is pushed up
    public float  KnockTime = .2f;

    [Header("death & checkpoint")]
    public FailPopup failPopup;
    public float failDelay = 1f;
    public float respawnDelay = 1f;
    public float RespawnIFrames = 1.5f;
    Vector2 checkpoint;
    static bool hasCheckpoint;
    static Vector2 savedCheckpoint;
    static string savedScene;
    public ScreenFade screenFade;
    public float fadeTime = .4f;
    public float blackHold = .15f;


    public bool Immune
    {
        get{return iTimer > 0f || gracePTimer > 0f || pc.Dashing;}
    }
    

    void Start()
    {
        pc = GetComponent<playercon>();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        baseColor = sr.color;
        health = maxHealth;
        checkpoint = transform.position;
        if(hasCheckpoint && savedScene == SceneManager.GetActiveScene().name)
        {
            checkpoint = savedCheckpoint;
            transform.position = savedCheckpoint;
        }
    }

    void Update()
    {
        if (parryAnimTimer > 0f) parryAnimTimer -= Time.deltaTime;
        if (pTimer > 0f) pTimer -= Time.deltaTime;
        if (pCooldownTimer > 0f) pCooldownTimer -= Time.deltaTime;
        if (pGraceTimer > 0f) pGraceTimer -= Time.deltaTime;
        if (counterTimer > 0f) counterTimer -= Time.deltaTime;
        if (dead) return;
        if (iTimer > 0f) iTimer -= Time.deltaTime;
        if (ATimer > 0f) ATimer -= Time.deltaTime;
        if (AAnimTimer > 0f) AAnimTimer -= Time.deltaTime;
        bool dashing = pc.Dashing;
        if(wasDashing && !dashing) gracePTimer = dashGraceP;
        wasDashing = dashing;
        if (parryAnimTimer > 0f && anim != null && anim.HasState(0, parryAnim)) pc.attackState = parryAnim;
        else if (pTimer > 0f && anim != null && anim.HasState(0, ParryStartS)) pc.attackState = ParryStartS;
        else pc.attackState = AAnimTimer > 0f ? attackAnim : 0;
        if (gracePTimer > 0f) gracePTimer -= Time.deltaTime;
        if (dashing) AAnimTimer = 0f;
        UpdateFlash();
        pc.ParryMovementLocked = ParryAnimationActive;
        if (Input.GetKey(pKey) && CanParry(dashing)) StartParry();
        if (Input.GetKey(Akey) && ATimer <= 0f && CanAttack && pTimer <= 0f)
        {
            if (CounterReady) FireCounter();
            else attack();
        }
        pc.ParryMovementLocked = ParryAnimationActive;
        Debug.Log("animation state: " + pc.attackState);
    }
    
    void die(bool majour)
    {
        dead = true;
        sr.color = baseColor;
        pc.attackState = 0;
        pc.enabled = false;
        rb.linearVelocity = Vector2.zero;
        if (majour) Invoke(nameof(goToFailScreen), failDelay);
        else StartCoroutine(RespawnRoutine());
    }
 
    void FireCounter()
    {
        ATimer = ACooldown;
        counterTimer = 0f;
        LaunchCounter(GetADir());
    }

    void LaunchCounter(Vector2 dir)
    {
        if (parryProjectile == null) { Debug.Log("No Projectile Assigned"); return; }
        pc.FaceDirection(dir);
        Vector2 spawn = (Vector2)transform.position + dir * projectileSpawnOffset;
        ParryProjectile p = Instantiate(parryProjectile, spawn, Quaternion.identity);
        p.Launch(dir, enemyLayer);
    }

    void goToFailScreen()
    {
        if (failPopup != null) failPopup.show();
        else Debug.LogWarning("No fail popup assigned");
    }

    bool CanParry(bool dashing)
    {
        return !dashing && pTimer <= 0f && pCooldownTimer <= 0f;
    }

    bool CanAttack => !pc.Dashing && !pc.DashWindup && !pc.WallSliding;

    void StartParry()
    {
        pTimer = pWindow;
        pCooldownTimer = pCooldown;
        AAnimTimer = 0f;
        parryAnimTimer = 0f;
        pc.ParryMovementLocked = true;
    }

    void OnParried(Vector2 from)
    {
        bool perfect = pTimer > pWindow - PerfectpWindow;
        pTimer = 0f;
        pGraceTimer = pSuccessGrace;
        if (perfect)
        {
            pCooldownTimer = 0f;
            if (autoCounter)
            {
                Vector2 dir = from - (Vector2)transform.position;
                if (dir.sqrMagnitude < .001f) dir = sr.flipX ? Vector2.right : Vector2.left;
                LaunchCounter(dir.normalized);
            }
            else counterTimer = counterWindow; // old behaviour: store it and fire on next attack
        }
        SetParryAnimation(perfect ? PerfectParryS : ParryHitS, perfect ? perfectParryAnimTime : parryHitAnimTime);
        SpawnParryFx(from, perfect);
    }
    void SpawnParryFx(Vector2 from, bool perfect)
    {
        GameObject prefab = (perfect && perfectParryFx != null) ? perfectParryFx : parryFx;
        if (prefab == null) return;
        Vector2 dir = from - (Vector2)transform.position;
        if (dir.sqrMagnitude < .001f) dir = sr.flipX ? Vector2.right : Vector2.left;
        dir.Normalize();
        Vector2 pos = (Vector2)transform.position + dir * parryFxOffset;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        GameObject fx = Instantiate(prefab, pos, Quaternion.Euler(0f, 0f, angle));
        Destroy(fx, parryFxLifeTime);
    }

    void SetParryAnimation(int state, float duration)
    {
        if (anim == null || !anim.HasState(0, state)) return;
        parryAnim = state;
        parryAnimTimer = duration;
        pc.ParryMovementLocked = ParryAnimationActive;
    }

    void ClearParry()
    {
        pTimer = 0f;
        pCooldownTimer = 0f;
        pGraceTimer = 0f;
        counterTimer = 0f;
        parryAnimTimer = 0f;
        pc.ParryMovementLocked = false;
    }

    public void SetCheckPoint(Vector2 pos)
    {
        checkpoint = pos;
        hasCheckpoint = true;
        savedCheckpoint = pos;
        savedScene = SceneManager.GetActiveScene().name;
    }

    void UpdateFlash()
    {
        if (iTimer > 0f)
        {
            bool visible = Mathf.FloorToInt(iTimer / flashTime) % 2 == 0;
            Color c = baseColor;
            c.a = visible ? 1f : .3f;
            sr.color = c;
            flashing = true;
        }
        else if (flashing)
        {
            sr.color = baseColor;
            flashing = false;
        }
    }

    public void TakeDamage(int amount, Vector2 from, bool majour = false)
    {
        ApplyDamage(amount, from, majour, true);
    }

    public void ApplyDamage(int amount, Vector2 from, bool majour = false)
    {
        ApplyDamage(amount, from, majour, true);
    }

        void ClearpWindowOnly()
        {
            pTimer = 0f;
        }

    void ApplyDamage(int amount, Vector2 from, bool majour, bool canParry)
    {
        if (dead || Immune) return;
 
        if (canParry && pTimer > 0f)
        {
            OnParried(from);
            return;
        }
 
        ClearpWindowOnly();
        health = Mathf.Max(0, health - amount);
        Debug.Log("Health: "+ health + "/" + maxHealth);
 
        if (health == 0)
        {
            die(majour);
            return;
        }
        iTimer = hitIFrames;
 
        float side = transform.position.x >= from.x ? 1f: -1f;
        pc.Knockback(new Vector2(side * HitKnockback.x, HitKnockback.y), KnockTime);
    }

    public void TakeProjectileDamage(int amount, Vector2 from, bool majour = false)
    {
        ApplyDamage(amount, from, majour, false);
    }

    public void Kill()
    {
        if (dead) return;
        health = 0;
        die(false);
    }

    public bool Heal(int amount)
    {
        if (dead || amount <= 0 || health >= maxHealth) return false;
        health = Mathf.Min(maxHealth, health + amount);
        return true;
    }

    Vector2 GetADir()
    {
        return pc.GetMouseOrMoveDirection();
    }

    System.Collections.IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        if (screenFade != null) yield return screenFade.FadeTo(1f, fadeTime);
        
        health = maxHealth;
        transform.position = checkpoint;
        rb.linearVelocity = Vector2.zero;
        pc.ResetState();
        ATimer = 0f;
        AAnimTimer = 0f;
        ResetManager.ResetAll();

        yield return new WaitForSeconds(blackHold);

        rb.linearVelocity = Vector2.zero;
        dead = false;
        pc.enabled = true;
        iTimer = RespawnIFrames;
        if (screenFade != null) yield return screenFade.FadeTo(0f, fadeTime);
    }

    int PickAAnim(Vector2 dir)
    {
        if (dir.y > .5f && anim.HasState(0, AttackUpS)) return AttackUpS;
        if (dir.y < -.5f && anim.HasState(0, AttackDownS)) return AttackDownS;
        return AttackS;
    }
    void attack()
    {
        ATimer = ACooldown;
        Vector2 dir = GetADir();
        Vector2 point = (Vector2)transform.position + dir * AReach;

        if (anim != null)
        {
            int hash = PickAAnim(dir);
            if (anim.HasState(0, hash))
            {
                attackAnim = hash;
                AAnimTimer = AAnimTime;
                anim.Play(hash, 0, 0f);
            }
        }
 
        if (slash != null)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            GameObject fx = Instantiate(slash, point, Quaternion.Euler(0f, 0f, angle), transform);
            Destroy(fx, slashLifeTime);
        }
 
        Collider2D[] hits = Physics2D.OverlapCircleAll(point, ARadius, enemyLayer);
        HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>(); // so an enemy with 2 colliders doesn't get hit twice
        foreach (Collider2D h in hits)
        {
            IDamageable target = h.GetComponentInParent<IDamageable>();
            if (target == null || !alreadyHit.Add(target)) continue;
            target.TakeDamage(ADamage, transform.position);
        }
    }
    void OnDrawGizmosSelected() // aren't gizmos just the best?
    {
        Gizmos.color = Color.red;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Gizmos.DrawWireSphere((Vector2)transform.position + d * AReach, ARadius);
        }
    }
}
// I LOVE CAMEL SCRIPT RAHHHHH -pradhyot.N 23/10/2026
//Vr fdq zh zdqghu iru d vshoo dqg olyh lq sdudooho?
//L zdqw lw wr eh wuxh, wr eh olnh brx
//Pb khduw vlqjv d fkruxv rxw ri wxqh
//Dqg L frxog ohdyh lw rq d vkhoi ru nhhs lw wr pbvhoi
//Exw qrwklqj frxog frqfhdo wkh wklqjv L ihho
//Pb oryh, fdq brx whdfk ph wr eh uhdo? (Fdq brx whdfk ph wr eh uhdo?)
//泣きたくなっても それでも かき集め  - fkruxv ri pdfklqh oryh eb mdplh S
// (shift 3)
