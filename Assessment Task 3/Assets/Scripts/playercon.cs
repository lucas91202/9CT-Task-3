using UnityEngine;
using System.Collections;
// this was made for multiple people as why there are so many comments
// quick warning, this script will house all the player functions and will be crazy hard to debug, if you have any specific problems please contact me and ill try patching it!
// all code is changeable and can be modified to your liking
// this script will have to be well understood to be modified, so please read the comments and understand the code before changing it
// remember, if the variable is public it can be changed within unity itself for ur testing
public class playercon : MonoBehaviour
{
    // some common prefixes i use: (Note: will not be the same across scripts)
    // D = dashing or variables that link to dashing
    // W = wall jumping or holding / or variables that link to that
    // S = animation states
    public float speed = 10f;
    public float Jheight = 10f; 
    float Jbuffer; 
    Rigidbody2D rb; 
    Animator anim;
    SpriteRenderer sr;
    [HideInInspector] public bool ParryMovementLocked;
    [HideInInspector] public Vector2 move;
    [HideInInspector] public bool Grounded; // checks if player is on the ground
    [HideInInspector] public int attackState;
    [HideInInspector] public bool DashWindup;
    float knockTimer;
    int currentState;
    public float coyoteTime = 0.12f; // this is the coyote time variable
    float coyoteTimer;
    // if you don't know what coyote time is, its genuinely just Wile E. Coyote (watch the cartoon!)
    public float jumpBufferTime = 0.12f; // this for jump buffering

    static int IdleS = Animator.StringToHash("standing"); // yeah im not using the animator controller (remove if you are using it)
    static int WalkS = Animator.StringToHash("walking");
    static int DashS = Animator.StringToHash("Mukdash");
    static int JumpS = Animator.StringToHash("jumping");
    static int WallSlideS = Animator.StringToHash("MukWallSlide");

    [Header("Wall Slide Variables")]
    public float WSlideSpeed = .5f; // this is the variables for a wall slide, if you want to remove it, comment the code in the FixedUpdate function and add a wall slide animation
    public Transform groundCheck;
    public Transform wallCheckLeft;
    public Transform wallCheckRight;
    public LayerMask groundLayer;
    public float checkRadius = 0.1f;
    // the last 5 variables above are for checking if the player is on a wall or on the ground
    bool WallSlide; 
    bool OnWall;
    bool touchLeft; 
    bool touchRight;
    bool pushingWall;
    public Vector2 Wjumpf = new Vector2(12f, 14f); // adjust as neccessary, X = horizontal jump force, Y = vertical jump force
    public float WjumpLock = 0.15f;
    float WjumpTimer;

    [Header("Dash Variables")]
    public float Dspeed = 45f;
    public float Dtime = .08f;
    public float Dcooldown = .4f;
    public float Dfreeze = .05f; 
    public float DexitBoost = 10f;
    public float DexitYBoost = 8f; 
    public float Ddecay = 60f; 
    public TrailRenderer dashTrail; // just like celeste it makes copies of the character while dashing
    bool Dfrozen; // checks if the player controls are frozen
    [HideInInspector] public bool Dashing;
    bool Dqueue; // checks if shift was pressed and sees if dashing is allowed
    bool CanDash = true;
    float Dtimer; // how much time is left on the dash
    float Dboost; // the horizontal momentum you keep after the dash
    float Dcooler; // the cooldown timer 
    Vector2 Ddir; // the direction of the current dash that is normalized so that digonals don't magically giga increase ur speed
    float defaultGrav; // resets the player's gravity back to the amount registered normally
    public bool DashAvailable => CanDash && Dcooler <= 0f;
    public bool WallSliding => WallSlide;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        defaultGrav = rb.gravityScale;
        if (dashTrail != null) dashTrail.emitting = false;
    }

    void Update()
    {
        // basic movement
        move.x = 0;
        if (Input.GetKey("right") || Input.GetKey("d")) move.x = 1;
        else if (Input.GetKey("left") || Input.GetKey("a")) move.x = -1;

        move.y = 0;
        if (Input.GetKey("up") || Input.GetKey("w")) move.y = 1;
        else if (Input.GetKey("down") || Input.GetKey("s")) move.y = -1; // this is for dashing in the direction you are pressing

        // jumping action
        if (Jbuffer > 0) Jbuffer -= Time.deltaTime;
        if (Input.GetKeyDown("space")) Jbuffer = jumpBufferTime;
        //else if (Input.GetKey("down") || Input.GetKey("s")) move.y = -1; if you want to add crouching action, uncomment this line and add crouch animation

        if (WjumpTimer <= 0 && !Dashing) 
        {
            if (move.x > 0) sr.flipX = true;
            else if (move.x < 0) sr.flipX = false;
        }

        if (Input.GetKeyDown("left shift") || Input.GetKeyDown("right shift")) Dqueue = true; // dashing action (remove if not utilizing sprinting)

        Grounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        touchLeft = Physics2D.OverlapCircle(wallCheckLeft.position, checkRadius, groundLayer);
        touchRight = Physics2D.OverlapCircle(wallCheckRight.position, checkRadius, groundLayer);
        // the above three lines are for checking if the player is grounded or touching a wall you have to use a gameobject with a circle collider as the groundcheck and wallcheck, and set the layer of the ground to "ground" or whatever you want, just make sure to change the layer in the inspector
        OnWall = (touchLeft || touchRight) && !Grounded;

        pushingWall = (touchLeft && move.x < 0) || (touchRight && move.x > 0);
        WallSlide = OnWall && rb.linearVelocity.y <= 0.1f && pushingWall; // wall slide only when pushing against the wall

        if (Grounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;

        if (Grounded && !Dashing) CanDash = true;

        int newState; // these are for the animation states, if you want to add more animations, add them here and in the animator controller
        if (Dashing) newState = DashS;
        else if (attackState != 0) newState = attackState;
        else if (WallSlide) newState = WallSlideS;
        else if (!Grounded) newState = JumpS;
        else if (move.x == 0) newState = IdleS;
        else newState = WalkS;
        if (newState != currentState)
        {
            anim.Play(newState);
            currentState = newState;
        }
    }

    IEnumerator DashFreeze() // this is for a more punchy dash effect by freezing movement
    {
        Dfrozen = true;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(Dfreeze);
        Time.timeScale = 1f;
        Dfrozen = false;
        BeginDashMove();
    }
    public void Knockback(Vector2 vel, float time)
    {
        rb.linearVelocity = vel;
        knockTimer = time;
    }
    public void ResetState()
    {
        Dashing = false;
        Dqueue = false;
        Dboost = 0f;
        knockTimer = 0f;
        WjumpTimer = 0f;
        Jbuffer = 0f;
        attackState = 0;
        rb.gravityScale = defaultGrav;
        if (dashTrail != null) dashTrail.emitting = false;
    }
    public bool RestoreDash()
    {
        if (DashAvailable) return false;
        CanDash = true;
        Dcooler = 0f;
        return true;
    }
    public Vector2 GetMouseOrMoveDirection()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            Ray mouseRay = camera.ScreenPointToRay(Input.mousePosition);
            Plane playerPlane = new Plane(Vector3.forward, transform.position);
            if (playerPlane.Raycast(mouseRay, out float distance))
            {
                Vector2 mouseDirection = (Vector2)mouseRay.GetPoint(distance) - (Vector2)transform.position;
                if (mouseDirection != Vector2.zero) return mouseDirection.normalized;
            }
        }

        if (move != Vector2.zero) return move.normalized;
        return sr.flipX ? Vector2.right : Vector2.left;
    }
    Vector2 GetDashDir() // finds the direction the dash will travel
    {
        return GetMouseOrMoveDirection();
    }
 
    public void FaceDirection(Vector2 direction)
    {
        if (direction.x > 0f) sr.flipX = true;
        else if (direction.x < 0f) sr.flipX = false;
    }

    void BeginDashMove()
    {
        Ddir = GetDashDir(); // dashes in ur direction
        if (Ddir.x != 0f) sr.flipX = Ddir.x > 0f; 
        if (dashTrail != null) dashTrail.emitting = true;
    }
 
    void EndDash()
    {
        Dashing = false;
        rb.gravityScale = defaultGrav;
        Dboost = Ddir.x * DexitBoost; // keeps a little momentum after the dash ends
        if (dashTrail != null) dashTrail.emitting = false;
    }
    void FixedUpdate() // actual movement 
    {
        if (WjumpTimer > 0) WjumpTimer -= Time.fixedDeltaTime;
        if (Dcooler > 0) Dcooler -= Time.fixedDeltaTime;
        if (knockTimer > 0f)
        {
            knockTimer -= Time.fixedDeltaTime;
            Dqueue = false;
            return;
        }
        Vector2 vel = rb.linearVelocity;
        
        if (Dqueue)
        {
            if (!Dashing && CanDash && Dcooler <= 0)
            {
                Dashing = true;
                Dtimer = Dtime;
                CanDash = false;
                Dcooler = Dcooldown;
                if (dashTrail != null) dashTrail.emitting = true;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                Ddir = GetDashDir();
                if (Ddir.x != 0) sr.flipX = Ddir.x > 0f; 
                WjumpTimer = 0f;

                if (Dfreeze > 0f) StartCoroutine(DashFreeze());
                else BeginDashMove();
            }
            Dqueue = false;
        }

        if (Dashing)
        {
            if (Dfrozen) return;

            Dtimer -= Time.fixedDeltaTime;
            if (Dtimer <= 0f)
            {
                EndDash();
                vel.x = move.x * speed;
                vel.y = Ddir.y * DexitYBoost;
            }
            else
            {
                rb.linearVelocity = Ddir * Dspeed;
                return;
            }
        }
        else
        {
            if (WjumpTimer <= 0f) vel.x = move.x * speed;
        }
        Dboost = Mathf.MoveTowards(Dboost, 0f, Ddecay * Time.fixedDeltaTime);
        if (WjumpTimer <= 0f) vel.x += Dboost;

        if (Jbuffer > 0f) 
        {
            if (coyoteTimer > 0f) // for coyote timeS
            {
                vel.y = Jheight;
                Jbuffer = 0f;
                coyoteTimer = 0f;
            }
            else if (OnWall) // for the wall jump
            {
                float awayDir = touchLeft ? 1f : -1f;
                vel = new Vector2(awayDir * Wjumpf.x, Wjumpf.y);
                WjumpTimer = WjumpLock;
                sr.flipX = awayDir > 0f;
                Jbuffer = 0f;
            } // if none are met then the buffer will be active till it expires or the player lands
        }

        if (WallSlide && vel.y <= .1f) vel.y = -WSlideSpeed; // wall slide speed
        rb.linearVelocity = vel;
    }
}
// this was actual hell to write - Pradhyot.N 20/10/26 (dont remove this comment, its the signature)
//L dp mxvw d olwwoh eurnhq, iroorz pb hbhv
//Brx sxw brxu ihdu lqvlgh d jluo wkdw'v eurnhq lqvlgh
//Mxvw krsh brxu zlvk iru mrb zloo frph rxw wuxh (Zloo frph rxw wuxh)
//Sxoolqj sdshu rxw pb vzhhwv, L'p sudblqj iru wlsv
//Exw rqob iloohg zlwk eulwwoh sduwv olnh d edj ixoo ri fklsv
//Zkr dp L wr ghqb zkhuh L'p iurp?
//Zkdw L'p iurp? - fkruxv ri Srwhwr fkls eb URPD
// shift 3 