using System.Collections.Generic;
using UnityEngine;

public class Turret : MonoBehaviour, IDamageable, IResettable
{
    // Detected = just spotted the player (alert anim), then Tracking
    enum State { Idle, Detected, Tracking, Charging, Cooldown, disabled }
    State state = State.Idle;

    [Header("Head tracking")]
    public Transform headPivot;                 // child that rotates (do NOT animate this in the Animator)
    public SpriteRenderer headRenderer;
    public float headTurnSpeed = 360f;
    public float headAngleOffset = 0f;          // 0 = art points RIGHT, -90 = UP, 180 = LEFT
    public bool flipHeadWhenAimingLeft = true;
    public bool limitHeadAngle = false;
    public float restAngle = 0f;
    public float maxAngleFromRest = 90f;

    [Header("Detection")]
    public float trackRange = 12f;
    public float shootRange = 8f;
    public bool needLOS = true;
    public LayerMask groundLayer;
    public float detectTime = .4f;              // how long the "spotted you" reaction lasts
    public GameObject alertIcon;                // I wont use this 

    [Header("Shooting")]
    public TurretProjectile projectilePrefab;
    public Transform firePoint;
    public float firePointDistance = .8f;
    public float chargeTime = .6f;
    public bool trackDuringCharge = true;
    public float fireCooldown = 1.5f;
    public float projectileSpeed = 9f;
    public int damage = 1;
    public bool majorDamage = false;
    public bool canBeParried = true;

    [Header("Line of sight (laser)")]
    public bool showLOS = true;
    public LineRenderer laser; // optional, one is created automatically if empty
    public Color trackLaserColor = new Color(1f, .2f, .2f, .25f);
    public Color chargeLaserColor = new Color(1f, .2f, .2f, 1f);
    public float trackLaserWidth = .03f;
    public float chargeLaserWidth = .1f;
    public int laserSortingOrder = 5;

    [Header("Animation")]
    public Animator anim;                       // put this on the body, not on headPivot
    public float shootAnimTime = .3f;
    static int IdleS = Animator.StringToHash("TurretIdle");         // rename to match your clips
    static int DetectS = Animator.StringToHash("TurretDetect");
    static int TrackS = Animator.StringToHash("TurretTrack");
    static int ChargeS = Animator.StringToHash("TurretCharge");
    static int ShootS = Animator.StringToHash("TurretShoot");
    static int DisabledS = Animator.StringToHash("TurretDisabled");
    int currentAnim;
    float shootAnimTimer;

    [Header("Health & hit reaction")]
    public int maxHealth = 3;
    public float hitFlashTime = .15f;
    public Color hitColor = Color.red;
    public Color chargeColor = new Color(1f, .6f, .3f);

    Transform player;
    SpriteRenderer[] renderers;
    Color[] baseColors;
    List<GameObject> liveProjectiles = new List<GameObject>();
    float timer;
    float flashTimer;
    float aimAngle;
    int health;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        if (anim == null) anim = GetComponentInChildren<Animator>();

        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;

        SetupLaser();
        if (alertIcon != null) alertIcon.SetActive(false);

        health = maxHealth;
        aimAngle = restAngle;
        ApplyHeadRotation();
        ResetManager.Register(this);
    }

    void OnDestroy()
    {
        ResetManager.Unregister(this);
    }

    void SetupLaser()
    {
        if (!showLOS) return;
        if (laser == null)
        {
            // fallback so it works with zero setup; assign your own LineRenderer + material for builds
            laser = gameObject.AddComponent<LineRenderer>();
            laser.material = new Material(Shader.Find("Sprites/Default"));
        }
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.sortingOrder = laserSortingOrder;
        laser.enabled = false;
    }

    void Update()
    {
        if (player == null) return;

        if (shootAnimTimer > 0f) shootAnimTimer -= Time.deltaTime;
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;

        Vector2 origin = HeadPos;
        Vector2 toPlayer = (Vector2)player.position - origin;
        float dist = toPlayer.magnitude;
        bool los = !needLOS || HasLOS(origin);
        bool inTrack = dist <= trackRange && los;
        bool inShoot = dist <= shootRange && los;

        switch (state)
        {
            case State.Idle:
                TurnHeadTo(restAngle);
                if (inTrack)
                {
                    state = State.Detected;
                    timer = 0f;
                }
                break;

            case State.Detected:
                TurnHeadTo(AngleOf(toPlayer));
                if (!inTrack) { state = State.Idle; timer = 0f; }
                else
                {
                    timer += Time.deltaTime;
                    if (timer >= detectTime)
                    {
                        timer = 0f;
                        state = State.Tracking;
                    }
                }
                break;

            case State.Tracking:
                TurnHeadTo(AngleOf(toPlayer));
                if (!inTrack) state = State.Idle;
                else if (inShoot)
                {
                    state = State.Charging;
                    timer = 0f;
                }
                break;

            case State.Charging:
                if (!inShoot) { timer = 0f; state = State.Tracking; }
                else
                {
                    if (trackDuringCharge) TurnHeadTo(AngleOf(toPlayer));
                    timer += Time.deltaTime;
                    if (timer >= chargeTime)
                    {
                        Shoot();
                        timer = 0f;
                        state = State.Cooldown;
                    }
                }
                break;

            case State.Cooldown:
                TurnHeadTo(AngleOf(toPlayer));
                timer += Time.deltaTime;
                if (timer >= fireCooldown)
                {
                    timer = 0f;
                    state = inTrack ? State.Tracking : State.Idle;
                }
                break;

            case State.disabled:
                break;
        }

        if (alertIcon != null) alertIcon.SetActive(state == State.Detected);
        UpdateLaser();
        UpdateAnimation();
        UpdateColor();
    }

    void UpdateAnimation()
    {
        if (anim == null) return;

        int newAnim;
        switch (state)
        {
            case State.Detected: newAnim = DetectS; break;
            case State.Tracking: newAnim = TrackS; break;
            case State.Charging: newAnim = ChargeS; break;
            case State.Cooldown: newAnim = shootAnimTimer > 0f ? ShootS : TrackS; break;
            case State.disabled: newAnim = DisabledS; break;
            default: newAnim = IdleS; break;
        }

        if (newAnim != currentAnim && anim.HasState(0, newAnim))
        {
            anim.Play(newAnim, 0, 0f);
            currentAnim = newAnim;
        }
    }

    void UpdateLaser()
    {
        if (laser == null) return;

        bool show = showLOS && (state == State.Detected || state == State.Tracking || state == State.Charging);
        laser.enabled = show;
        if (!show) return;

        float rad = aimAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 start = firePoint != null ? (Vector2)firePoint.position : HeadPos;

        RaycastHit2D hit = Physics2D.Raycast(start, dir, trackRange, groundLayer);
        Vector2 end = hit.collider != null ? hit.point : start + dir * trackRange;

        laser.SetPosition(0, start);
        laser.SetPosition(1, end);

        float t = state == State.Charging ? Mathf.Clamp01(timer / chargeTime) : 0f;
        Color c = Color.Lerp(trackLaserColor, chargeLaserColor, t);
        float w = Mathf.Lerp(trackLaserWidth, chargeLaserWidth, t);
        laser.startColor = c;
        laser.endColor = c;
        laser.startWidth = w;
        laser.endWidth = w;
    }

    Vector2 HeadPos => headPivot != null ? (Vector2)headPivot.position : (Vector2)transform.position;

    float AngleOf(Vector2 dir)
    {
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    void TurnHeadTo(float targetAngle)
    {
        if (limitHeadAngle)
        {
            float diff = Mathf.DeltaAngle(restAngle, targetAngle);
            targetAngle = restAngle + Mathf.Clamp(diff, -maxAngleFromRest, maxAngleFromRest);
        }
        aimAngle = Mathf.MoveTowardsAngle(aimAngle, targetAngle, headTurnSpeed * Time.deltaTime);
        ApplyHeadRotation();
    }

    void ApplyHeadRotation()
    {
        if (headPivot != null) headPivot.rotation = Quaternion.Euler(0f, 0f, aimAngle + headAngleOffset);
        if (headRenderer != null && flipHeadWhenAimingLeft)
            headRenderer.flipY = Mathf.Cos(aimAngle * Mathf.Deg2Rad) < 0f;
    }

    bool HasLOS(Vector2 origin)
    {
        return !Physics2D.Linecast(origin, player.position, groundLayer);
    }

    void Shoot()
    {
        if (projectilePrefab == null) { Debug.LogWarning("Turret has no projectile assigned"); return; }

        float rad = aimAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 spawn = firePoint != null ? (Vector2)firePoint.position : HeadPos + dir * firePointDistance;

        TurretProjectile proj = Instantiate(projectilePrefab, spawn, Quaternion.Euler(0f, 0f, aimAngle));
        proj.Launch(dir, projectileSpeed, damage, majorDamage, canBeParried, groundLayer);
        liveProjectiles.Add(proj.gameObject);

        shootAnimTimer = shootAnimTime;
    }

    void ClearProjectiles()
    {
        foreach (GameObject g in liveProjectiles) if (g != null) Destroy(g);
        liveProjectiles.Clear();
    }

    // ---------- health / reset ----------

    public void TakeDamage(int amount, Vector2 hitPoint, bool major = false)
    {
        if (state == State.disabled) return;

        health -= amount;
        flashTimer = hitFlashTime;
        if (health <= 0)
        {
            health = 0;
            state = State.disabled;
            ClearProjectiles();
            if (laser != null) laser.enabled = false;
            if (alertIcon != null) alertIcon.SetActive(false);
            UpdateAnimation(); // play the deactivation anim straight away
        }
    }

    public void ResetToStart()
    {
        health = maxHealth;
        state = State.Idle;
        timer = 0f;
        flashTimer = 0f;
        shootAnimTimer = 0f;
        aimAngle = restAngle;
        ApplyHeadRotation();
        ClearProjectiles();
        if (laser != null) laser.enabled = false;
        if (alertIcon != null) alertIcon.SetActive(false);
        currentAnim = 0; // forces the idle anim to replay
        UpdateAnimation();
        UpdateColor();
    }

    void UpdateColor()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (flashTimer > 0f) renderers[i].color = hitColor;
            else if (state == State.Charging) renderers[i].color = chargeColor;
            else renderers[i].color = baseColors[i];
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector2 pos = headPivot != null ? (Vector2)headPivot.position : (Vector2)transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, trackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, shootRange);
    }
}