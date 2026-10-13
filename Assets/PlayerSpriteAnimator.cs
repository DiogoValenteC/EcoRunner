using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public float visualScale = 2f;

    [Header("Animation Speed")]
    public float walkFps = 7f;
    public float runFps = 10f;
    public float maxRunFps = 20f;
    public float oneShotFps = 14f;
    public float runStartSpeedRatio = 0.45f;
    public int jumpHoldFrame = 5;

    [Header("Frames")]
    public Sprite[] walkFrames;
    public Sprite[] runFrames;
    public Sprite[] jumpFrames;
    public Sprite[] airJumpFrames;
    public Sprite[] dashFrames;
    public Sprite[] hitFrames;
    public Sprite[] deathFrames;

    Player player;
    Sprite[] currentFrames;
    string currentState;
    float frameTimer;
    int frameIndex;
    bool oneShotActive;
    string oneShotState;
    bool currentLoop;
    bool holdingJumpFrame;
    bool finishingHeldJump;

    private void Awake()
    {
        player = GetComponent<Player>();
        SetupRenderer();
        LoadSpritesIfNeeded();
        PlayLoop("Walk", walkFrames, true);
    }

    private void Update()
    {
        if (player == null || spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.flipX = player.runDirection < 0;

        if (holdingJumpFrame)
        {
            UpdateHeldJumpAnimation();
            return;
        }

        if (oneShotActive)
        {
            AdvanceAnimation(oneShotFps, false);
            return;
        }

        UpdateLoopState();

        if (currentState == "DeathHeld")
        {
            SetCurrentSprite();
            return;
        }

        AdvanceAnimation(GetLoopFps(), currentLoop);
    }

    public void PlayJump()
    {
        holdingJumpFrame = false;
        finishingHeldJump = false;
        PlayOneShot("Jump", jumpFrames);
    }

    public void PlayAirJump()
    {
        holdingJumpFrame = false;
        finishingHeldJump = false;
        PlayOneShot("AirJump", airJumpFrames);
    }

    public void PlayDash()
    {
        holdingJumpFrame = false;
        finishingHeldJump = false;

        Sprite[] cleanDashFrames = player != null && player.isGrounded ? runFrames : airJumpFrames;

        if (cleanDashFrames == null || cleanDashFrames.Length == 0)
        {
            cleanDashFrames = jumpFrames;
        }

        PlayOneShot("Dash", cleanDashFrames);
    }

    public void PlayHit()
    {
        holdingJumpFrame = false;
        finishingHeldJump = false;
        PlayOneShot("Hit", hitFrames);
    }

    public void PlayDeath()
    {
        if (currentState == "Death" || currentState == "DeathHeld")
        {
            return;
        }

        holdingJumpFrame = false;
        finishingHeldJump = false;
        PlayOneShot("Death", deathFrames);
    }

    void SetupRenderer()
    {
        if (spriteRenderer != null)
        {
            return;
        }

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.transform.localScale = new Vector3(visualScale, visualScale, 1f);
    }

    void UpdateLoopState()
    {
        if (player.isDead)
        {
            if (currentState == "DeathHeld" || currentState == "Death")
            {
                return;
            }

            PlayLoop("DeathLoop", deathFrames, false);
            return;
        }

        if (!player.isGrounded)
        {
            if (currentState == "Jump" || currentState == "AirJump" || currentState == "JumpHeld" || currentState == "AirJumpHeld")
            {
                return;
            }

            PlayLoop("JumpLoop", jumpFrames, false);
            return;
        }

        float speedRatio = player.maxVelocity <= 0f ? 0f : player.velocity.x / player.maxVelocity;

        if (speedRatio >= runStartSpeedRatio)
        {
            PlayLoop("Run", runFrames, true);
        }
        else
        {
            PlayLoop("Walk", walkFrames, true);
        }
    }

    float GetLoopFps()
    {
        if (currentState == "Run")
        {
            float speedRatio = player.maxVelocity <= 0f ? 0f : Mathf.Clamp01(player.velocity.x / player.maxVelocity);
            return Mathf.Lerp(runFps, maxRunFps, speedRatio);
        }

        if (currentState == "Walk")
        {
            float speedRatio = player.maxVelocity <= 0f ? 0f : Mathf.Clamp01(player.velocity.x / (player.maxVelocity * runStartSpeedRatio));
            return Mathf.Lerp(walkFps * 0.75f, walkFps, speedRatio);
        }

        return oneShotFps;
    }

    void PlayLoop(string state, Sprite[] frames, bool loop)
    {
        if (frames == null || frames.Length == 0 || currentState == state)
        {
            return;
        }

        currentState = state;
        currentFrames = frames;
        frameIndex = 0;
        frameTimer = 0f;
        oneShotActive = false;
        currentLoop = loop;
        SetCurrentSprite();
    }

    void PlayOneShot(string state, Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        currentState = state;
        oneShotState = state;
        currentFrames = frames;
        frameIndex = 0;
        frameTimer = 0f;
        oneShotActive = true;
        currentLoop = false;
        SetCurrentSprite();
    }

    void AdvanceAnimation(float fps, bool loop)
    {
        if (currentFrames == null || currentFrames.Length == 0 || fps <= 0f)
        {
            return;
        }

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / fps;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex++;

            if (frameIndex >= currentFrames.Length)
            {
                if (loop)
                {
                    frameIndex = 0;
                }
                else
                {
                    frameIndex = currentFrames.Length - 1;
                    oneShotActive = false;
                    bool holdDeathFrame =
                        oneShotState == "Death" ||
                        currentState == "DeathLoop" ||
                        currentState == "DeathHeld";
                    currentState = holdDeathFrame ? "DeathHeld" : "";
                    oneShotState = "";

                    if (!holdDeathFrame)
                    {
                        UpdateLoopState();
                    }

                    break;
                }
            }

            if (ShouldHoldJumpFrame())
            {
                frameIndex = GetJumpHoldFrameIndex();
                oneShotActive = false;
                holdingJumpFrame = true;
                currentState = oneShotState == "AirJump" ? "AirJumpHeld" : "JumpHeld";
                SetCurrentSprite();
                return;
            }
        }

        SetCurrentSprite();
    }

    void UpdateHeldJumpAnimation()
    {
        SetCurrentSprite();

        if (!player.isGrounded || finishingHeldJump)
        {
            return;
        }

        holdingJumpFrame = false;
        finishingHeldJump = true;
        oneShotActive = true;
        currentState = oneShotState == "AirJump" ? "AirJumpLand" : "JumpLand";
        frameTimer = 0f;
        AdvanceAnimation(oneShotFps, false);
    }

    bool ShouldHoldJumpFrame()
    {
        if (player.isGrounded)
        {
            return false;
        }

        if (oneShotState != "Jump" && oneShotState != "AirJump")
        {
            return false;
        }

        return frameIndex >= GetJumpHoldFrameIndex();
    }

    int GetJumpHoldFrameIndex()
    {
        if (currentFrames == null || currentFrames.Length == 0)
        {
            return 0;
        }

        return Mathf.Clamp(jumpHoldFrame, 0, currentFrames.Length - 1);
    }

    void SetCurrentSprite()
    {
        if (spriteRenderer == null || currentFrames == null || currentFrames.Length == 0)
        {
            return;
        }

        frameIndex = Mathf.Clamp(frameIndex, 0, currentFrames.Length - 1);
        spriteRenderer.sprite = currentFrames[frameIndex];
    }

    void LoadSpritesIfNeeded()
    {
        if (walkFrames == null || walkFrames.Length == 0)
        {
            walkFrames = LoadSprites("CharacterKaique/andando");
        }

        if (runFrames == null || runFrames.Length == 0)
        {
            runFrames = LoadSprites("CharacterKaique/corrida_sem_fundo");
        }

        if (jumpFrames == null || jumpFrames.Length == 0)
        {
            jumpFrames = LoadSprites("CharacterKaique/salto_sem_fundo");
        }

        if (airJumpFrames == null || airJumpFrames.Length == 0)
        {
            airJumpFrames = LoadSprites("CharacterKaique/salto_no_ar");
        }

        if (dashFrames == null || dashFrames.Length == 0)
        {
            dashFrames = LoadSprites("CharacterKaique/dash_sem_fundo");
        }

        if (hitFrames == null || hitFrames.Length == 0)
        {
            hitFrames = LoadSprites("CharacterKaique/Colisao");
        }

        if (deathFrames == null || deathFrames.Length == 0)
        {
            deathFrames = LoadSprites("CharacterKaique/morte_sem_fundo");
        }
    }

    Sprite[] LoadSprites(string resourcePath)
    {
        Sprite[] assets = Resources.LoadAll<Sprite>(resourcePath);
        List<Sprite> sprites = new List<Sprite>();

        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i];

            if (sprite != null && IsUsableAnimationFrame(sprite))
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort(
            delegate (Sprite a, Sprite b)
            {
                return ExtractFrameIndex(a.name).CompareTo(ExtractFrameIndex(b.name));
            }
        );

        return sprites.ToArray();
    }

    bool IsUsableAnimationFrame(Sprite sprite)
    {
        Rect rect = sprite.rect;

        if (rect.width < 40f || rect.height < 40f)
        {
            return false;
        }

        if (rect.height > 360f || rect.width > 360f)
        {
            return false;
        }

        return true;
    }

    int ExtractFrameIndex(string spriteName)
    {
        int underscore = spriteName.LastIndexOf('_');

        if (underscore < 0 || underscore >= spriteName.Length - 1)
        {
            return 0;
        }

        int value;
        return int.TryParse(spriteName.Substring(underscore + 1), out value) ? value : 0;
    }
}
