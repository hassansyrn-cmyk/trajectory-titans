using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryTitans
{
    public enum ScreenState { Menu, Garage, Daily, Settings, Battle, Result }
    public enum BattleMode { Campaign, QuickBattle, Training }
    public enum TerrainPreset { RollingHills, CentralMountain, Valley, SteppedCanyon, RockyCover, CraterField, AsymmetricHighground, RidgeBridge }
    public enum WeaponId { StandardShell, HeavyShell, ClusterBurst, TripleShot, BunkerBreaker, MortarArc, RicochetRound, Airburst, SmokeRound, MineDrop, FireWeapon, Rocket, IceWeapon, PlasmaWeapon, LaserWeapon }
    public enum TankId { Scout, Balanced, Heavy, Artillery, Siege }

    [Serializable]
    public class SaveData
    {
        public int coins = 500;
        public int gems = 20;
        public int level = 1;
        public int xp = 0;
        public int wins = 0;
        public int losses = 0;
        public int selectedTank = 0;
        public int selectedWeapon = 0;
        public int[] tankLevels = new int[5] { 1, 1, 1, 1, 1 };
        public int[] weaponLevels = new int[15] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
        public string lastDaily = "";
        public bool sound = true;
        public bool vibration = true;
        public bool trajectory = true;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.8f;
        public bool reducedMotion = false; // Accessibility feature
        public int graphicsQuality = 1; // 0 = Low, 1 = Medium, 2 = High
    }

    public static class GameConfig
    {
        public const float Gravity = 9.81f;
        public const float WindFactor = 0.18f;
        public const float MaxFuel = 100f;
        public const float FuelCostRate = 12f;
        public const float MaxShotPower = 85f;
        public const float MinShotPower = 20f;
        public const float MaxAngle = 80f;
        public const float MinAngle = 10f;
        public const float TurnStartDelay = 1.25f;
        public const float ScreenShakeDuration = 0.5f;
    }

    public class WeaponData
    {
        public WeaponId id;
        public string name;
        public string description;
        public float damage;
        public float radius;
        public int projectiles;
        public Color color;
        public float gravityMultiplier = 1.0f;
        public float windMultiplier = 1.0f;
        public int initialAmmo;
        public string specialtyInfo;
        public WeaponData(WeaponId id, string n, string d, float dmg, float rad, int count, Color c, int ammo, float grav = 1f, float wind = 1f, string spec = "")
        {
            this.id = id; name = n; description = d; damage = dmg; radius = rad; projectiles = count;
            color = c; initialAmmo = ammo; gravityMultiplier = grav; windMultiplier = wind; specialtyInfo = spec;
        }
    }

    public class TankData
    {
        public TankId id;
        public string name;
        public string trait;
        public Color body;
        public int health;
        public float armor;
        public float fuel;
        public float speed;
        public TankData(TankId id, string n, string t, Color c, int hp, float a, float f, float s)
        {
            this.id = id; name = n; trait = t; body = c; health = hp; armor = a; fuel = f; speed = s;
        }
    }

    public class TankActor
    {
        public GameObject root;
        public Transform turret;
        public Transform muzzle;
        public Transform barrel;
        public float recoilOffset;
        public Transform[] wheels;
        public float health;
        public float maxHealth;
        public float armor;
        public bool enemy;
        public float x;
        public float y;
        public float angle = 45f;
        public int shieldTurns;
        public float moveVel;
        public float wheelRot;
        public float fuel;
        public float speedMultiplier;
        public TankId archetype;
        public float slopeAngle;
        public int[] ammoInBattle;
    }

    public class ProjectileSim
    {
        public GameObject visual;
        public Vector2 position;
        public Vector2 velocity;
        public WeaponData weapon;
        public bool enemy;
        public float life;
        public float trailTimer;
        public int bounces;
        public bool hasSplit;
    }

    public class MineActor
    {
        public GameObject visual;
        public Vector2 position;
        public float radius = 1.4f;
        public float damage = 22f;
        public int remainingTurns = 3;
    }

    public class DropActor
    {
        public GameObject visual;
        public Vector2 position;
        public string type; // "HP", "Ammo", "Fuel", "Shield"
        public int remainingTurns = 4;
    }

    public class ParticleSim
    {
        public GameObject visual;
        public Vector2 pos;
        public Vector2 vel;
        public float life;
        public float maxLife;
        public Color startColor;
        public Color endColor;
    }

    public class FloatText
    {
        public Vector2 pos;
        public string text;
        public float life;
        public Color color;
    }

    public class GameRoot : MonoBehaviour
    {
        private static GameRoot instance;
        private SaveData save;
        private ScreenState screen = ScreenState.Menu;
        private BattleMode mode;
        private Camera cam;
        private Texture2D white, gradientBg, panelBg, healthBarFull, healthBarEmpty, borderTex;
        private GUIStyle titleStyle, h1Style, bodyStyle, buttonStyle, smallStyle, centerStyle, damageStyle, cardStyle, selectedStyle;
        private readonly List<GameObject> worldObjects = new List<GameObject>();
        private readonly List<ProjectileSim> projectiles = new List<ProjectileSim>();
        private readonly List<GameObject> trajectoryDots = new List<GameObject>();
        private readonly List<string> combatLog = new List<string>();
        private readonly List<FloatText> floatTexts = new List<FloatText>();
        private readonly List<MineActor> activeMines = new List<MineActor>();
        private readonly List<DropActor> activeDrops = new List<DropActor>();
        private readonly List<ParticleSim> activeParticles = new List<ParticleSim>();
        private TankActor player, enemy;
        private GameObject terrainMeshGo;
        private Mesh terrainMesh;
        private bool playerTurn = true;
        private bool isWeaponDrawerOpen = false;
        private bool battleEnded;
        private float turnDelay;
        private float aimAngle = 45f;
        private float shotPower = 50f;
        private int selectedWeaponIndex;
        private float wind;
        private int turnNumber;
        private int mapSeed;
        private System.Random rng;
        private float messageTimer;
        private string message = "";
        private Rect safe;
        private bool paused;
        private Vector2 scroll;
        private int campaignStage = 1;
        private float cameraShake;
        private float cameraShakeIntensity;
        private Vector3 cameraBase;
        private float turnBannerTimer;
        private string turnBannerText = "";

        // Authoritative Terrain Data
        private float[] terrainHeights;
        private const float TerrainStart = -15.0f;
        private const float TerrainEnd = 15.0f;
        private const float TerrainStep = 0.15f;
        private int terrainPointsCount;
        private TerrainPreset activePreset;

        // Touch Control Fields
        private int joystickFingerId = -1;
        private int aimFingerId = -1;
        private Vector2 joystickStart;
        private Vector2 joystickCurrent;
        private bool isJoystickDragging;
        private float joystickRadius = 60f;
        private float joystickDeadZone = 12f;

        private Vector2 aimStart;
        private Vector2 aimCurrent;
        private bool isAimingDragging;
        private Vector2 touchAimVisualOrigin;

        // Custom Graphics Cache
        private Dictionary<Color, Texture2D> colorTextures = new Dictionary<Color, Texture2D>();
        private Sprite squareSprite, circleSprite;

        // Continuous movement input
        private float playerMoveInput;

        // registries
        private readonly WeaponData[] weapons = new WeaponData[]
        {
            new WeaponData(WeaponId.StandardShell, "Standard Shell", "Reliable direct-fire projectile with moderate splash.", 28, 1.5f, 1, new Color(1f, .72f, .15f), 999, 1f, 1f, "All-Rounder"),
            new WeaponData(WeaponId.HeavyShell, "Heavy Shell", "Slow and heavy round. Enormous direct-hit damage.", 45, 2.0f, 1, new Color(1f, .25f, .12f), 5, 1.25f, 0.6f, "Tank Buster"),
            new WeaponData(WeaponId.ClusterBurst, "Cluster Burst", "Splits into 4 sub-projectiles near the top of its path.", 16, 1.2f, 1, new Color(.85f, .3f, 1f), 6, 0.95f, 1.1f, "Splitter"),
            new WeaponData(WeaponId.TripleShot, "Triple Shot", "Launches 3 smaller shells sequentially in a slight spread.", 14, 1.1f, 3, new Color(.25f, .9f, 1f), 8, 1.0f, 1.15f, "Area Suppress"),
            new WeaponData(WeaponId.BunkerBreaker, "Bunker Breaker", "Extreme direct damage, but practically zero blast radius.", 65, 0.45f, 1, new Color(0.95f, 0.15f, 0.35f), 4, 1.1f, 0.8f, "Precision Piercer"),
            new WeaponData(WeaponId.MortarArc, "Mortar Arc", "Heavy gravity round. Excels at striking behind high mountains.", 38, 2.4f, 1, new Color(.65f, .42f, .2f), 6, 1.6f, 0.5f, "Anti-Cover"),
            new WeaponData(WeaponId.RicochetRound, "Ricochet Round", "Bounces once on the terrain before detonating on next impact.", 32, 1.6f, 1, new Color(0.15f, 0.85f, 0.45f), 5, 1.0f, 1.0f, "Trickshot Bounce"),
            new WeaponData(WeaponId.Airburst, "Airburst Shell", "Automatically detonates exactly 1.4 seconds after firing.", 26, 2.5f, 1, new Color(1f, 0.9f, 0.25f), 6, 0.85f, 1.3f, "Mid-Air Detonation"),
            new WeaponData(WeaponId.SmokeRound, "Smoke Round", "Inflicts minimal damage but creates dense, defensive cover.", 3, 3.2f, 1, new Color(0.7f, 0.72f, 0.78f), 4, 0.9f, 1.5f, "Defensive Smoke"),
            new WeaponData(WeaponId.MineDrop, "Mine Dispenser", "Deploys a visible, persistent surface proximity hazard.", 15, 1.8f, 1, new Color(0.88f, 0.78f, 0.1f), 3, 1.1f, 1.0f, "Area Denial"),
            new WeaponData(WeaponId.FireWeapon, "Fire Shot", "Incendiary projectile leaving flame trails and scorching the soil.", 30, 2.2f, 1, new Color(1.0f, 0.4f, 0.0f), 8, 0.9f, 0.8f, "Elemental Incendiary"),
            new WeaponData(WeaponId.Rocket, "Rocket Strike", "Self-propelled missile with a steady smoke and flame exhaust trail.", 34, 1.8f, 1, new Color(1.0f, 0.6f, 0.1f), 6, 0.7f, 1.2f, "High Velocity"),
            new WeaponData(WeaponId.IceWeapon, "Cryo Capsule", "Crystalline projectile leaving frost trails and ice shard debris.", 25, 1.6f, 1, new Color(0.2f, 0.8f, 1.0f), 8, 1.0f, 1.0f, "Elemental Frost"),
            new WeaponData(WeaponId.PlasmaWeapon, "Plasma Bolt", "Animated violet electrical core that detonates into a plasma splash.", 36, 2.6f, 1, new Color(0.6f, 0.2f, 0.9f), 5, 0.8f, 1.1f, "Annihilation Core"),
            new WeaponData(WeaponId.LaserWeapon, "Laser Cannon", "Fires a charged, layered beam directly across its path with direct impact.", 40, 0.3f, 1, new Color(1.0f, 0.1f, 0.4f), 4, 0.0f, 0.0f, "Precision Beam")
        };

        private readonly TankData[] tanks = new TankData[]
        {
            new TankData(TankId.Balanced, "Dune Viper", "Precise standard unit. Good fuel efficiency.", new Color(.18f, .78f, .42f), 100, .05f, 100f, 2.8f),
            new TankData(TankId.Heavy, "Iron Bison", "Reinforced frame. Thick armor but slow movement.", new Color(.22f, .52f, .92f), 135, .16f, 70f, 1.9f),
            new TankData(TankId.Scout, "Crimson Lynx", "High mobility chassis. Light armor and ultra fuel capacity.", new Color(.92f, .22f, .22f), 85, 0f, 150f, 3.8f),
            new TankData(TankId.Artillery, "Nova Warden", "Fires special high-arc rounds. Moderate durability.", new Color(.68f, .28f, .95f), 110, .09f, 90f, 2.4f),
            new TankData(TankId.Siege, "Titan Behemoth", "Slow assault powerhouse with bonus shielding.", new Color(.85f, .5f, .12f), 125, .12f, 80f, 2.2f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("TrajectoryTitans_GameRoot");
            instance = go.AddComponent<GameRoot>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            // Build fundamental procedural textures
            white = CreateSolidTexture(Color.white);
            gradientBg = CreateGradientTexture(new Color(0.04f, 0.05f, 0.12f), new Color(0.09f, 0.12f, 0.28f), 32, 32);
            panelBg = CreateSolidTexture(new Color(0.04f, 0.05f, 0.1f, 0.94f));
            healthBarFull = CreateSolidTexture(new Color(0.22f, 0.85f, 0.38f));
            healthBarEmpty = CreateSolidTexture(new Color(0.12f, 0.12f, 0.15f));
            borderTex = CreateSolidTexture(new Color(0.2f, 0.35f, 0.6f, 1f));

            Load();
            SetupCamera();
        }

        private void Start() { BuildStyles(); }

        private void SetupCamera()
        {
            var c = new GameObject("Main Camera");
            cam = c.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 7.4f;
            cam.backgroundColor = new Color(0.04f, 0.06f, 0.12f);
            cameraBase = new Vector3(0, 1.7f, -10);
            cam.transform.position = cameraBase;
            DontDestroyOnLoad(c);
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * .067f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(1f, .85f, .25f);
            h1Style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * .042f), fontStyle = FontStyle.Bold };
            h1Style.normal.textColor = Color.white;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * .027f), wordWrap = true };
            bodyStyle.normal.textColor = new Color(.92f, .94f, 1f);
            smallStyle = new GUIStyle(bodyStyle) { fontSize = Mathf.RoundToInt(Screen.height * .022f) };
            centerStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * .031f), fontStyle = FontStyle.Bold };
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.yellow;
            damageStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * .035f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            damageStyle.normal.textColor = new Color(1f, .3f, .2f);

            cardStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * .021f), alignment = TextAnchor.MiddleCenter };
            cardStyle.normal.textColor = Color.white;
            selectedStyle = new GUIStyle(cardStyle) { fontStyle = FontStyle.Bold };
            selectedStyle.normal.textColor = Color.yellow;
        }

        private void Update()
        {
            safe = Screen.safeArea;
            if (messageTimer > 0) messageTimer -= Time.deltaTime;
            if (turnBannerTimer > 0) turnBannerTimer -= Time.deltaTime;

            for (int i = floatTexts.Count - 1; i >= 0; i--)
            {
                floatTexts[i].life -= Time.deltaTime;
                floatTexts[i].pos.y += Time.deltaTime * 2.0f;
                if (floatTexts[i].life <= 0) floatTexts.RemoveAt(i);
            }

            // Screen Shake Effect
            if (cameraShake > 0 && !save.reducedMotion)
            {
                cameraShake -= Time.deltaTime;
                float t = cameraShake / GameConfig.ScreenShakeDuration;
                float shakeX = Mathf.Sin(Time.time * 65f) * cameraShakeIntensity * t;
                float shakeY = Mathf.Cos(Time.time * 50f) * cameraShakeIntensity * 0.6f * t;
                cam.transform.position = cameraBase + new Vector3(shakeX * 0.8f, shakeY * 0.6f, 0);
            }
            else cam.transform.position = cameraBase;

            if (screen != ScreenState.Battle || paused || battleEnded) return;

            // Update Particle Sim
            UpdateParticles();

            // Process Touch and Mouse Input for Joystick/Aiming
            ProcessGameplayInput();

            // Handle Continuous Movement
            UpdatePlayerMovement();

            // Handle Projectile Movement with Sub-stepping Physics
            UpdateProjectile();

            // Update Slope Alignments
            UpdateTankSlopeAlignments();

            // Update Recoil barrel positions
            UpdateRecoil(player);
            UpdateRecoil(enemy);

            // Update Trajectory dots
            UpdateTrajectory();

            // Smooth Camera Focus (Focus on active projectile, or reset to active tank)
            UpdateCameraTracking();

            if (!playerTurn && projectiles.Count == 0)
            {
                turnDelay -= Time.deltaTime;
                if (turnDelay <= 0) EnemyThinkAndShoot();
            }
        }

        private readonly List<GameObject> particlePool = new List<GameObject>();

        private GameObject GetOrCreateParticleGo(Vector2 pos, float radius, Color startColor)
        {
            for (int i = 0; i < particlePool.Count; i++)
            {
                var go = particlePool[i];
                if (go != null && !go.activeSelf)
                {
                    go.transform.position = pos;
                    go.transform.localScale = Vector3.one * radius * 2f;
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = startColor;
                        sr.enabled = true;
                    }
                    go.SetActive(true);
                    return go;
                }
            }

            int budget = 60;
            if (save != null)
            {
                if (save.graphicsQuality == 0) budget = 30; // Low (50%)
                else if (save.graphicsQuality == 2) budget = 90; // High (150%)
            }

            if (particlePool.Count < budget)
            {
                var go = CreateCircleGameObject("PooledParticle", pos, radius, startColor, 13);
                particlePool.Add(go);
                return go;
            }

            if (activeParticles.Count > 0)
            {
                var oldest = activeParticles[0];
                activeParticles.RemoveAt(0);
                var go = oldest.visual;
                if (go != null)
                {
                    go.transform.position = pos;
                    go.transform.localScale = Vector3.one * radius * 2f;
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = startColor;
                        sr.enabled = true;
                    }
                    go.SetActive(true);
                    return go;
                }
            }

            return CreateCircleGameObject("PooledParticle", pos, radius, startColor, 13);
        }

        private void ClearParticlePool()
        {
            foreach (var go in particlePool)
            {
                if (go != null) Destroy(go);
            }
            particlePool.Clear();
        }

        private void UpdateParticles()
        {
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                var p = activeParticles[i];
                p.life -= Time.deltaTime;
                p.pos += p.vel * Time.deltaTime;
                p.vel.y -= GameConfig.Gravity * 0.15f * Time.deltaTime; // slight drift/gravity
                p.visual.transform.position = p.pos;
                float t = Mathf.Clamp01(p.life / p.maxLife);
                Color currentColor = Color.Lerp(p.endColor, p.startColor, t);
                p.visual.GetComponent<SpriteRenderer>().color = currentColor;

                if (p.life <= 0)
                {
                    p.visual.SetActive(false);
                    activeParticles.RemoveAt(i);
                }
            }
        }

        private void SpawnParticle(Vector2 position, Vector2 velocity, Color start, Color end, float radius, float duration)
        {
            var go = GetOrCreateParticleGo(position, radius, start);
            activeParticles.Add(new ParticleSim
            {
                visual = go,
                pos = position,
                vel = velocity,
                life = duration,
                maxLife = duration,
                startColor = start,
                endColor = end
            });
        }

        private void UpdatePlayerMovement()
        {
            if (player == null || !playerTurn || projectiles.Count > 0)
            {
                playerMoveInput = 0;
                if (player != null) player.moveVel = 0;
                return;
            }

            float speed = tanks[save.selectedTank].speed;
            float targetVel = playerMoveInput * speed;
            player.moveVel = Mathf.MoveTowards(player.moveVel, targetVel, Time.deltaTime * 10f);

            if (Mathf.Abs(player.moveVel) < 0.01f)
            {
                player.moveVel = 0;
                return;
            }

            float cost = Mathf.Abs(player.moveVel) * Time.deltaTime * GameConfig.FuelCostRate;
            if (player.fuel < cost)
            {
                player.moveVel = 0;
                playerMoveInput = 0;
                return;
            }
            player.fuel -= cost;

            float newX = player.x + player.moveVel * Time.deltaTime;
            if (newX < -13.5f || newX > 13.5f) { player.moveVel = 0; return; }
            if (Mathf.Abs(newX - enemy.x) < 2.5f) { player.moveVel = 0; return; }

            float targetY = GetTerrainHeight(newX) + 0.62f;
            if (Mathf.Abs(targetY - player.y) > 0.85f) { player.moveVel *= 0.25f; } // drag on steep hill

            player.x = newX;
            player.y = Mathf.Lerp(player.y, targetY, Time.deltaTime * 14f);
            player.root.transform.position = new Vector3(player.x, player.y, 0);

            player.wheelRot -= player.moveVel * Time.deltaTime * 300f;
            if (player.wheels != null)
            {
                foreach (var w in player.wheels)
                {
                    if (w != null) w.localRotation = Quaternion.Euler(0, 0, player.wheelRot);
                }
            }

            // Check Drop Pickup collision
            CheckDropPickups(player);
        }

        private void ProcessGameplayInput()
        {
            if (!playerTurn || projectiles.Count > 0 || battleEnded)
            {
                isJoystickDragging = false;
                isAimingDragging = false;
                joystickFingerId = -1;
                aimFingerId = -1;
                playerMoveInput = 0;
                return;
            }

            // Clean Pointer/Touch tracking
            if (Application.isMobilePlatform || Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    Vector2 screenPos = t.position;
                    // Invert y for GUI/touch coordination logic
                    float touchYGUI = Screen.height - screenPos.y;

                    if (t.phase == TouchPhase.Began)
                    {
                        // Check virtual joystick zone (bottom left)
                        if (screenPos.x < Screen.width * 0.35f && touchYGUI > Screen.height * 0.5f)
                        {
                            joystickFingerId = t.fingerId;
                            joystickStart = screenPos;
                            joystickCurrent = screenPos;
                            isJoystickDragging = true;
                        }
                        // Check Aiming dragging zone (right portion of screen, avoiding FIRE and selector)
                        else if (screenPos.x > Screen.width * 0.45f && touchYGUI < Screen.height * 0.72f)
                        {
                            if (isWeaponDrawerOpen) continue;
                            aimFingerId = t.fingerId;
                            aimStart = screenPos;
                            aimCurrent = screenPos;
                            isAimingDragging = true;
                            touchAimVisualOrigin = screenPos;
                        }
                    }
                    else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                    {
                        if (t.fingerId == joystickFingerId)
                        {
                            joystickCurrent = screenPos;
                        }
                        else if (t.fingerId == aimFingerId)
                        {
                            aimCurrent = screenPos;
                            UpdateDragToAim(aimStart, aimCurrent);
                        }
                    }
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        if (t.fingerId == joystickFingerId)
                        {
                            isJoystickDragging = false;
                            joystickFingerId = -1;
                            playerMoveInput = 0;
                        }
                        else if (t.fingerId == aimFingerId)
                        {
                            isAimingDragging = false;
                            aimFingerId = -1;
                        }
                    }
                }
            }
            else
            {
                // Desktop Mouse input emulation
                Vector3 mPos = Input.mousePosition;
                float mYGUI = Screen.height - mPos.y;

                if (Input.GetMouseButtonDown(0))
                {
                    if (mPos.x < Screen.width * 0.35f && mYGUI > Screen.height * 0.5f)
                    {
                        joystickStart = mPos;
                        joystickCurrent = mPos;
                        isJoystickDragging = true;
                    }
                    else if (mPos.x > Screen.width * 0.45f && mYGUI < Screen.height * 0.72f)
                    {
                        if (isWeaponDrawerOpen) return;
                        aimStart = mPos;
                        aimCurrent = mPos;
                        isAimingDragging = true;
                        touchAimVisualOrigin = mPos;
                    }
                }
                else if (Input.GetMouseButton(0))
                {
                    if (isJoystickDragging)
                    {
                        joystickCurrent = mPos;
                    }
                    else if (isAimingDragging)
                    {
                        aimCurrent = mPos;
                        UpdateDragToAim(aimStart, aimCurrent);
                    }
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    isJoystickDragging = false;
                    isAimingDragging = false;
                    playerMoveInput = 0;
                }
            }

            // Translate Joystick Drag to tank move input
            if (isJoystickDragging)
            {
                float deltaX = joystickCurrent.x - joystickStart.x;
                float dist = Mathf.Abs(deltaX);
                if (dist > joystickDeadZone)
                {
                    float clampVal = Mathf.Clamp(deltaX, -joystickRadius, joystickRadius);
                    playerMoveInput = clampVal / joystickRadius;
                }
                else
                {
                    playerMoveInput = 0;
                }
            }
        }

        private void UpdateDragToAim(Vector2 start, Vector2 current)
        {
            Vector2 delta = current - start;
            // Scale drag to match screen resolution smoothly
            float sensitivityFactor = 1200f / Screen.width;
            float angleDelta = delta.x * 0.12f * sensitivityFactor;
            float powerDelta = delta.y * 0.15f * sensitivityFactor;

            aimAngle = Mathf.Clamp(aimAngle + angleDelta, GameConfig.MinAngle, GameConfig.MaxAngle);
            shotPower = Mathf.Clamp(shotPower + powerDelta, GameConfig.MinShotPower, GameConfig.MaxShotPower);

            // Re-anchor start slightly to avoid scrolling off indefinitely
            aimStart = Vector2.Lerp(aimStart, current, 0.08f);
        }

        private void OnGUI()
        {
            if (titleStyle == null) BuildStyles();
            GUI.depth = -20;
            switch (screen)
            {
                case ScreenState.Menu: DrawMenu(); break;
                case ScreenState.Garage: DrawGarage(); break;
                case ScreenState.Daily: DrawDaily(); break;
                case ScreenState.Settings: DrawSettings(); break;
                case ScreenState.Battle: DrawBattleHUD(); break;
                case ScreenState.Result: DrawResult(); break;
            }
            if (messageTimer > 0) DrawToast(message);

            // Turn Banner overlay
            if (screen == ScreenState.Battle && turnBannerTimer > 0)
            {
                DrawTurnBanner();
            }

            // Float Text damage and items labels
            if (screen == ScreenState.Battle)
            {
                foreach (var ft in floatTexts)
                {
                    Vector3 screenPos = cam.WorldToScreenPoint(new Vector3(ft.pos.x, ft.pos.y, 0));
                    float alpha = Mathf.Clamp01(ft.life / 0.95f);
                    damageStyle.normal.textColor = new Color(ft.color.r, ft.color.g, ft.color.b, alpha);
                    GUI.Label(new Rect(screenPos.x - 100, Screen.height - screenPos.y - 30, 200, 60), ft.text, damageStyle);
                }
            }
        }

        private void DrawBackdrop()
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), gradientBg);
        }

        private void DrawTopCurrency()
        {
            float w = Screen.width * 0.25f;
            Box(new Rect(Screen.width - w - 24, 16, w, 44), new Color(0.04f, 0.06f, 0.14f, 0.96f));
            GUI.Label(new Rect(Screen.width - w - 12, 22, w - 12, 34), "COINS: " + save.coins + "   GEMS: " + save.gems, smallStyle);
        }

        private void DrawMenu()
        {
            DrawBackdrop();
            float sw = Screen.width, sh = Screen.height;
            GUI.Label(new Rect(0, sh * 0.08f, sw, sh * 0.14f), "TRAJECTORY TITANS", titleStyle);
            GUI.Label(new Rect(0, sh * 0.21f, sw, sh * 0.05f), "Command original combat machines and dominate the battlefield.", centerStyle);
            DrawTopCurrency();

            float bw = sw * 0.32f, bh = sh * 0.09f, x = (sw - bw) * 0.5f, y = sh * 0.32f;
            if (BigButton(new Rect(x, y, bw, bh), "CAMPAIGN MODE")) { mode = BattleMode.Campaign; StartBattle(); }
            if (BigButton(new Rect(x, y + bh * 1.25f, bw, bh), "QUICK MATCH")) { mode = BattleMode.QuickBattle; StartBattle(); }
            if (BigButton(new Rect(x, y + bh * 2.5f, bw, bh), "TRAINING RANGE")) { mode = BattleMode.Training; StartBattle(); }

            float sy = sh * 0.72f, smallW = sw * 0.16f;
            if (BigButton(new Rect(sw * 0.22f, sy, smallW, bh * 0.8f), "GARAGE")) screen = ScreenState.Garage;
            if (BigButton(new Rect(sw * 0.42f, sy, smallW, bh * 0.8f), "DAILY GIFT")) screen = ScreenState.Daily;
            if (BigButton(new Rect(sw * 0.62f, sy, smallW, bh * 0.8f), "SETTINGS")) screen = ScreenState.Settings;

            GUI.Label(new Rect(0, sh * 0.86f, sw, 40), "Commander Level " + save.level + "   |   " + save.wins + " Wins   " + save.losses + " Losses", centerStyle);
        }

        private void DrawGarage()
        {
            DrawBackdrop();
            GUI.Label(new Rect(40, 20, Screen.width * 0.5f, 60), "GARAGE & ARMORY", h1Style);
            DrawTopCurrency();
            if (BackButton()) { screen = ScreenState.Menu; Save(); return; }

            float top = Screen.height * 0.15f;
            GUI.Label(new Rect(45, top, 300, 40), "SELECT BATTLE VEHICLE", h1Style);
            for (int i = 0; i < tanks.Length; i++)
            {
                float x = 40 + i * (Screen.width - 80) / 5f;
                Rect r = new Rect(x, top + 50, (Screen.width - 120) / 5f, Screen.height * 0.24f);
                Box(r, save.selectedTank == i ? new Color(0.12f, 0.35f, 0.55f, 0.98f) : new Color(0.05f, 0.08f, 0.14f, 0.95f));

                // Hull visual swatch
                GUI.color = tanks[i].body;
                GUI.DrawTexture(new Rect(r.x + 15, r.y + 12, r.width - 30, 24), white);
                GUI.color = Color.white;

                GUI.Label(new Rect(r.x + 8, r.y + 44, r.width - 16, 26), tanks[i].name, smallStyle);
                GUI.Label(new Rect(r.x + 8, r.y + 74, r.width - 16, 50), tanks[i].trait, smallStyle);
                GUI.Label(new Rect(r.x + 8, r.y + r.height - 32, r.width - 16, 26), "HP " + tanks[i].health + "  LVL " + save.tankLevels[i], smallStyle);

                if (GUI.Button(r, "", GUIStyle.none)) save.selectedTank = i;
            }

            float wy = Screen.height * 0.49f;
            GUI.Label(new Rect(45, wy, 350, 40), "WEAPON INVENTORY", h1Style);
            scroll = GUI.BeginScrollView(new Rect(40, wy + 42, Screen.width - 80, Screen.height * 0.36f), scroll, new Rect(0, 0, weapons.Length * 210, Screen.height * 0.3f));
            for (int i = 0; i < weapons.Length; i++)
            {
                Rect r = new Rect(i * 205, 5, 195, Screen.height * 0.28f);
                Box(r, save.selectedWeapon == i ? new Color(0.45f, 0.2f, 0.05f, 0.98f) : new Color(0.05f, 0.08f, 0.14f, 0.95f));

                GUI.color = weapons[i].color;
                GUI.DrawTexture(new Rect(r.x + 10, r.y + 10, r.width - 20, 8), white);
                GUI.color = Color.white;

                GUI.Label(new Rect(r.x + 10, r.y + 24, r.width - 20, 28), weapons[i].name, smallStyle);
                GUI.Label(new Rect(r.x + 10, r.y + 54, r.width - 20, 70), weapons[i].description, smallStyle);
                GUI.Label(new Rect(r.x + 10, r.y + r.height - 36, r.width - 20, 28), "DMG " + Mathf.RoundToInt(weapons[i].damage) + "  LVL " + save.weaponLevels[i], smallStyle);

                if (GUI.Button(r, "", GUIStyle.none)) save.selectedWeapon = i;
            }
            GUI.EndScrollView();
        }

        private void DrawDaily()
        {
            DrawBackdrop();
            GUI.Label(new Rect(40, 20, 600, 60), "DAILY OPERATIONS COMMAND", h1Style);
            if (BackButton()) { screen = ScreenState.Menu; return; }
            DrawTopCurrency();

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            bool claimed = save.lastDaily == today;
            float w = Screen.width * 0.6f, h = Screen.height * 0.54f;
            Rect card = new Rect((Screen.width - w) / 2, Screen.height * 0.22f, w, h);
            Box(card, new Color(0.06f, 0.1f, 0.18f, 0.98f));

            GUI.Label(new Rect(card.x + 30, card.y + 25, card.width - 60, 45), "DAILY SUPPLY DROP", h1Style);
            GUI.Label(new Rect(card.x + 30, card.y + 85, card.width - 60, 80), "Log in daily to claim free credits and gems to upgrade your arsenal.", bodyStyle);
            GUI.Label(new Rect(card.x + 30, card.y + 175, card.width - 60, 50), "OFFER: 350 COINS + 5 GEMS", h1Style);

            GUI.enabled = !claimed;
            if (BigButton(new Rect(card.x + card.width * 0.2f, card.y + card.height - 90, card.width * 0.6f, 60), claimed ? "COLLECTED TODAY" : "COLLECT SUPPLY"))
            {
                save.coins += 350;
                save.gems += 5;
                save.lastDaily = today;
                Save();
                Toast("Supplies secured! 350 Coins, 5 Gems added.");
            }
            GUI.enabled = true;
        }

        private void DrawSettings()
        {
            DrawBackdrop();
            GUI.Label(new Rect(40, 20, 500, 60), "SETTINGS", h1Style);
            if (BackButton()) { screen = ScreenState.Menu; Save(); return; }

            float x = Screen.width * 0.25f, y = Screen.height * 0.18f, w = Screen.width * 0.5f;
            Box(new Rect(x - 40, y - 15, w + 80, Screen.height * 0.62f), new Color(0.04f, 0.08f, 0.15f, 0.98f));

            save.sound = GUI.Toggle(new Rect(x, y, w, 40), save.sound, "  Play Sound Effects", bodyStyle);
            save.vibration = GUI.Toggle(new Rect(x, y + 55, w, 40), save.vibration, "  Tactile Haptic Vibration", bodyStyle);
            save.trajectory = GUI.Toggle(new Rect(x, y + 110, w, 40), save.trajectory, "  Enable Trajectory Preview", bodyStyle);
            save.reducedMotion = GUI.Toggle(new Rect(x, y + 165, w, 40), save.reducedMotion, "  Reduced Motion (Mute Camera Shake)", bodyStyle);

            GUI.Label(new Rect(x, y + 225, 220, 36), "Graphics Quality", bodyStyle);
            string qText = save.graphicsQuality == 0 ? "LOW" : (save.graphicsQuality == 2 ? "HIGH" : "MEDIUM");
            if (GUI.Button(new Rect(x + 230, y + 225, 160, 30), "QUALITY: " + qText, buttonStyle))
            {
                save.graphicsQuality = (save.graphicsQuality + 1) % 3;
            }

            GUI.Label(new Rect(x, y + 275, 220, 36), "Music Soundtrack", bodyStyle);
            save.musicVolume = GUI.HorizontalSlider(new Rect(x + 230, y + 287, w - 240, 24), save.musicVolume, 0, 1);

            GUI.Label(new Rect(x, y + 325, 220, 36), "Weapons Sound FX", bodyStyle);
            save.sfxVolume = GUI.HorizontalSlider(new Rect(x + 230, y + 337, w - 240, 24), save.sfxVolume, 0, 1);
        }

        private void StartBattle()
        {
            ClearWorld();
            floatTexts.Clear();
            activeMines.Clear();
            activeDrops.Clear();
            activeParticles.Clear();

            screen = ScreenState.Battle;
            paused = false;
            battleEnded = false;
            playerTurn = true;
            turnNumber = 1;
            selectedWeaponIndex = save.selectedWeapon;
            aimAngle = 45f;
            shotPower = 50f;

            mapSeed = UnityEngine.Random.Range(1000, 999999);
            rng = new System.Random(mapSeed);

            // Select a random preset for the map
            activePreset = (TerrainPreset)UnityEngine.Random.Range(0, 8);

            wind = UnityEngine.Random.Range(-3.5f, 3.5f);

            CreateBattleWorld();
            TriggerTurnBanner("PLAYER TURN");
        }

        private void CreateBattleWorld()
        {
            cam.orthographicSize = 7.4f;
            cameraBase = new Vector3(0, 1.7f, -10);

            // Dynamic Atmospheric Parallax Background
            CreateRectGameObject("SkyGlow", new Vector2(0, 8f), new Vector2(34, 5), new Color(0.04f, 0.05f, 0.12f), -35);
            CreateRectGameObject("MountainFar", new Vector2(-6f, 2.2f), new Vector2(12, 4.5f), new Color(0.06f, 0.1f, 0.19f), -30);
            CreateRectGameObject("MountainMid", new Vector2(4f, 1.5f), new Vector2(14, 3.8f), new Color(0.08f, 0.13f, 0.24f), -28);

            // Moon + Glow Effects
            CreateCircleGameObject("MoonGlow", new Vector2(-7.5f, 5.8f), 2.1f, new Color(1f, 0.78f, 0.45f, 0.1f), -26);
            CreateCircleGameObject("Moon", new Vector2(-7.5f, 5.8f), 1.2f, new Color(1f, 0.88f, 0.65f), -25);

            // Atmospheric Floating Stars
            for (int i = 0; i < 35; i++)
            {
                float sx = UnityEngine.Random.Range(-14.5f, 14.5f);
                float sy = UnityEngine.Random.Range(2.0f, 8.2f);
                float sz = UnityEngine.Random.Range(0.035f, 0.1f);
                float sa = UnityEngine.Random.Range(0.35f, 0.95f);
                CreateCircleGameObject("Star", new Vector2(sx, sy), sz, new Color(1f, 1f, 1f, sa), -24);
            }

            // Generate Authoritative TerrainHeights Array
            terrainPointsCount = Mathf.RoundToInt((TerrainEnd - TerrainStart) / TerrainStep) + 1;
            terrainHeights = new float[terrainPointsCount];

            GenerateAuthoritativeHeights();

            // Spawn visual blocks representing terrain heights
            RenderTerrainBlocks();

            // Spawn Player
            player = CreateTank(tanks[save.selectedTank], -9.0f, false);
            player.ammoInBattle = new int[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                player.ammoInBattle[i] = weapons[i].initialAmmo;
            }

            // Spawn Enemy AI
            int enemyTankChoice = (mode == BattleMode.Training) ? 0 : UnityEngine.Random.Range(0, tanks.Length);
            enemy = CreateTank(tanks[enemyTankChoice], 9.0f, true);
            enemy.ammoInBattle = new int[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                enemy.ammoInBattle[i] = weapons[i].initialAmmo;
            }

            if (mode == BattleMode.Training)
            {
                enemy.maxHealth = enemy.health = 9999f;
            }

            // Trigger safe settling
            UpdateTankSlopeAlignments();
            ShowTrajectory();
        }

        private void GenerateAuthoritativeHeights()
        {
            float seedOffset = mapSeed * 0.0013f;
            for (int i = 0; i < terrainPointsCount; i++)
            {
                float x = TerrainStart + i * TerrainStep;
                float height = -1.6f;

                switch (activePreset)
                {
                    case TerrainPreset.RollingHills:
                        height += Mathf.Sin(x * 0.35f + seedOffset) * 0.95f + Mathf.Cos(x * 0.7f) * 0.45f;
                        break;
                    case TerrainPreset.CentralMountain:
                        float distCenter = Mathf.Abs(x);
                        height += Mathf.Max(0, 4.2f - distCenter * 0.65f) + Mathf.Sin(x * 0.5f) * 0.3f;
                        break;
                    case TerrainPreset.Valley:
                        height += Mathf.Min(0, Mathf.Abs(x) * 0.55f - 2.8f);
                        break;
                    case TerrainPreset.SteppedCanyon:
                        float rawHeight = Mathf.Sin(x * 0.3f) * 1.8f;
                        height += Mathf.RoundToInt(rawHeight * 1.4f) * 0.7f;
                        break;
                    case TerrainPreset.RockyCover:
                        height += Mathf.Sin(x * 0.4f) * 0.6f + (Mathf.PingPong(x * 1.1f, 1.2f) - 0.6f);
                        break;
                    case TerrainPreset.CraterField:
                        height += Mathf.Sin(x * 0.3f) * 0.4f + Mathf.Cos(x * 0.8f) * 0.5f;
                        // pre-deform crater markers
                        if (Mathf.Abs(x - 3.0f) < 1.5f) height -= 1.2f;
                        if (Mathf.Abs(x + 4.0f) < 1.8f) height -= 1.5f;
                        break;
                    case TerrainPreset.AsymmetricHighground:
                        height += (x * 0.22f) + Mathf.Sin(x * 0.4f) * 0.5f;
                        break;
                    case TerrainPreset.RidgeBridge:
                        if (Mathf.Abs(x) < 3.5f)
                            height += 1.8f + Mathf.Cos(x * 0.4f) * 0.4f;
                        else
                            height += -0.5f + Mathf.Sin(x * 0.5f) * 0.4f;
                        break;
                }

                terrainHeights[i] = Mathf.Clamp(height, -4.5f, 4.8f);
            }
        }

        private float GetTerrainHeight(float x)
        {
            if (x <= TerrainStart) return terrainHeights[0];
            if (x >= TerrainEnd) return terrainHeights[terrainPointsCount - 1];

            float relativeIndex = (x - TerrainStart) / TerrainStep;
            int lowIndex = Mathf.RoundToInt(relativeIndex - 0.5f);
            int highIndex = lowIndex + 1;

            if (lowIndex < 0) return terrainHeights[0];
            if (highIndex >= terrainPointsCount) return terrainHeights[terrainPointsCount - 1];

            float t = relativeIndex - lowIndex;
            return Mathf.Lerp(terrainHeights[lowIndex], terrainHeights[highIndex], t);
        }

        private void SetTerrainHeight(float x, float value)
        {
            if (x < TerrainStart || x > TerrainEnd) return;
            float relativeIndex = (x - TerrainStart) / TerrainStep;
            int idx = Mathf.RoundToInt(relativeIndex);
            if (idx >= 0 && idx < terrainPointsCount)
            {
                terrainHeights[idx] = Mathf.Clamp(value, -4.5f, 4.8f);
            }
        }

        private void RenderTerrainBlocks()
        {
            RenderTerrainMesh();
        }

        private void RenderTerrainMesh()
        {
            if (terrainHeights == null || terrainPointsCount == 0) return;

            if (terrainMeshGo == null)
            {
                terrainMeshGo = new GameObject("SmoothTerrainMesh");
                TrackGameObject(terrainMeshGo);
                var filter = terrainMeshGo.AddComponent<MeshFilter>();
                var renderer = terrainMeshGo.AddComponent<MeshRenderer>();

                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("UI/Default");
                renderer.material = new Material(shader);

                terrainMesh = new Mesh();
                filter.mesh = terrainMesh;
            }

            int numPoints = terrainPointsCount;
            Vector3[] vertices = new Vector3[numPoints * 3];
            Color[] colors = new Color[numPoints * 3];
            int[] triangles = new int[(numPoints - 1) * 12]; // 4 triangles (12 indices) per segment

            Color grassColor = new Color(0.26f, 0.65f, 0.22f);
            Color dirtColor = new Color(0.28f, 0.18f, 0.12f);
            Color stoneColor = new Color(0.12f, 0.08f, 0.06f);

            for (int i = 0; i < numPoints; i++)
            {
                float x = TerrainStart + i * TerrainStep;
                float y = terrainHeights[i];

                vertices[i * 3 + 0] = new Vector3(x, y, 0);                 // Top Grass surface
                vertices[i * 3 + 1] = new Vector3(x, y - 0.18f, 0);         // Subsurface dirt transition
                vertices[i * 3 + 2] = new Vector3(x, -5.0f, 0);             // Bottom dark stone boundary

                colors[i * 3 + 0] = grassColor;
                colors[i * 3 + 1] = dirtColor;
                colors[i * 3 + 2] = stoneColor;
            }

            int triIndex = 0;
            for (int i = 0; i < numPoints - 1; i++)
            {
                int currTop = i * 3 + 0;
                int currMid = i * 3 + 1;
                int currBot = i * 3 + 2;

                int nextTop = (i + 1) * 3 + 0;
                int nextMid = (i + 1) * 3 + 1;
                int nextBot = (i + 1) * 3 + 2;

                // Triangles for Grass Strip (top to mid)
                triangles[triIndex++] = currTop;
                triangles[triIndex++] = nextTop;
                triangles[triIndex++] = currMid;

                triangles[triIndex++] = currMid;
                triangles[triIndex++] = nextTop;
                triangles[triIndex++] = nextMid;

                // Triangles for Subsurface Earth (mid to bot)
                triangles[triIndex++] = currMid;
                triangles[triIndex++] = nextMid;
                triangles[triIndex++] = currBot;

                triangles[triIndex++] = currBot;
                triangles[triIndex++] = nextMid;
                triangles[triIndex++] = nextBot;
            }

            terrainMesh.Clear();
            terrainMesh.vertices = vertices;
            terrainMesh.colors = colors;
            terrainMesh.triangles = triangles;
            terrainMesh.RecalculateBounds();
        }

        private TankActor CreateTank(TankData data, float startX, bool isEnemy)
        {
            var t = new TankActor
            {
                enemy = isEnemy,
                x = startX,
                armor = data.armor,
                archetype = data.id,
                fuel = data.fuel,
                speedMultiplier = data.speed
            };

            t.y = GetTerrainHeight(startX) + 0.62f;
            t.maxHealth = t.health = data.health + (isEnemy ? campaignStage * 5 : (save.tankLevels[(int)data.id] - 1) * 8);

            t.root = new GameObject(isEnemy ? "EnemyTank" : "PlayerTank");
            TrackGameObject(t.root);
            t.root.transform.position = new Vector3(t.x, t.y, 0);

            Color coreColor = data.body;
            Color darkTone = coreColor * 0.45f;
            Color metalGray = new Color(0.42f, 0.44f, 0.48f);
            Color highlight = Color.Lerp(coreColor, Color.white, 0.35f);
            Color shadowColor = new Color(0.05f, 0.05f, 0.08f, 0.6f);

            // Distinct High-Fidelity Modular Tank Assemblies
            switch (data.id)
            {
                case TankId.Scout:
                    // Compact, aerodynamic sloped front frame
                    CreateChildRect(t.root.transform, "TrackBase", new Vector2(0, -0.5f), new Vector2(1.7f, 0.32f), Color.black, 1);
                    CreateChildRect(t.root.transform, "BeltFront", new Vector2(-0.85f, -0.5f), new Vector2(0.12f, 0.32f), Color.black, 2);
                    CreateChildRect(t.root.transform, "BeltBack", new Vector2(0.85f, -0.5f), new Vector2(0.12f, 0.32f), Color.black, 2);

                    // Sloped upper hull
                    CreateChildRect(t.root.transform, "HullBase", new Vector2(0, -0.1f), new Vector2(1.5f, 0.42f), coreColor, 5);
                    CreateChildRect(t.root.transform, "SlopedArmor", new Vector2(isEnemy ? -0.3f : 0.3f, 0.02f), new Vector2(0.8f, 0.22f), highlight, 6);
                    CreateChildRect(t.root.transform, "Vents", new Vector2(isEnemy ? 0.45f : -0.45f, -0.05f), new Vector2(0.35f, 0.08f), Color.black, 6);

                    // Antenna attachment
                    CreateChildRect(t.root.transform, "Antenna", new Vector2(isEnemy ? 0.5f : -0.5f, 0.52f), new Vector2(0.04f, 0.72f), metalGray, 4);

                    // Sleek compact Turret and hatch
                    var scoutTurret = CreateChildCircle(t.root.transform, "Turret", new Vector2(-0.08f, 0.32f), 0.38f, darkTone, 8);
                    t.turret = scoutTurret.transform;
                    CreateChildCircle(t.turret, "Hatch", new Vector2(0f, 0.28f), 0.12f, shadowColor, 9);
                    break;

                case TankId.Heavy:
                    // Massive armored multi-layered silhouette
                    CreateChildRect(t.root.transform, "TrackBase", new Vector2(0, -0.55f), new Vector2(2.5f, 0.48f), Color.black, 1);
                    CreateChildRect(t.root.transform, "BeltFront", new Vector2(-1.25f, -0.55f), new Vector2(0.15f, 0.48f), Color.black, 2);
                    CreateChildRect(t.root.transform, "BeltBack", new Vector2(1.25f, -0.55f), new Vector2(0.15f, 0.48f), Color.black, 2);

                    // Multi-tiered armored block hull
                    CreateChildRect(t.root.transform, "HullBase", new Vector2(0, 0.1f), new Vector2(2.3f, 0.75f), coreColor, 5);
                    CreateChildRect(t.root.transform, "ArmorShield1", new Vector2(-0.45f, 0.15f), new Vector2(1.0f, 0.32f), highlight, 6);
                    CreateChildRect(t.root.transform, "ArmorShield2", new Vector2(0.45f, 0.15f), new Vector2(1.0f, 0.32f), darkTone, 6);
                    CreateChildRect(t.root.transform, "Vents", new Vector2(isEnemy ? 0.85f : -0.85f, 0.35f), new Vector2(0.42f, 0.12f), Color.black, 6);

                    // Broad robust Turret and double hatch
                    var heavyTurret = CreateChildCircle(t.root.transform, "Turret", new Vector2(0f, 0.62f), 0.55f, darkTone, 8);
                    t.turret = heavyTurret.transform;
                    CreateChildRect(t.turret, "HatchLeft", new Vector2(-0.16f, 0.42f), new Vector2(0.18f, 0.08f), shadowColor, 9);
                    CreateChildRect(t.turret, "HatchRight", new Vector2(0.16f, 0.42f), new Vector2(0.18f, 0.08f), shadowColor, 9);
                    break;

                case TankId.Artillery:
                    // Long frame chassis with high support struts
                    CreateChildRect(t.root.transform, "TrackBase", new Vector2(0, -0.52f), new Vector2(2.1f, 0.38f), Color.black, 1);

                    // Rear loaded launching base
                    CreateChildRect(t.root.transform, "HullBase", new Vector2(0, 0.04f), new Vector2(1.9f, 0.62f), coreColor, 5);
                    CreateChildRect(t.root.transform, "PistonsSupport", new Vector2(isEnemy ? 0.65f : -0.65f, 0.42f), new Vector2(0.15f, 0.55f), metalGray, 4);

                    // Elevated Turret Assembly
                    var artTurret = CreateChildCircle(t.root.transform, "Turret", new Vector2(isEnemy ? 0.3f : -0.3f, 0.55f), 0.46f, darkTone, 8);
                    t.turret = artTurret.transform;
                    CreateChildRect(t.turret, "LensScope", new Vector2(isEnemy ? -0.2f : 0.2f, 0.25f), new Vector2(0.15f, 0.15f), Color.cyan, 9);
                    break;

                case TankId.Siege:
                    // Dual blocky plates with heavy-riveted armor panels
                    CreateChildRect(t.root.transform, "TrackBase", new Vector2(0, -0.52f), new Vector2(2.4f, 0.45f), Color.black, 1);
                    CreateChildRect(t.root.transform, "HullBase", new Vector2(0, 0.08f), new Vector2(2.1f, 0.72f), coreColor, 5);

                    // Heavy layered iron siding
                    CreateChildRect(t.root.transform, "SidingLeft", new Vector2(-0.65f, 0.08f), new Vector2(0.72f, 0.42f), darkTone, 6);
                    CreateChildRect(t.root.transform, "SidingRight", new Vector2(0.65f, 0.08f), new Vector2(0.72f, 0.42f), highlight, 6);

                    // Blocky heavy Turret with a massive bezel
                    var siegeTurret = CreateChildRect(t.root.transform, "Turret", new Vector2(0f, 0.58f), new Vector2(1.1f, 0.66f), darkTone, 8);
                    t.turret = siegeTurret.transform;
                    CreateChildCircle(t.turret, "HeavyHatch", new Vector2(0f, 0.28f), 0.18f, Color.black, 9);
                    break;

                default: // Balanced
                    // Classic robust armored chassis
                    CreateChildRect(t.root.transform, "TrackBase", new Vector2(0, -0.52f), new Vector2(2.1f, 0.4f), Color.black, 1);
                    CreateChildRect(t.root.transform, "HullBase", new Vector2(0, 0.04f), new Vector2(1.8f, 0.62f), coreColor, 5);
                    CreateChildRect(t.root.transform, "SideSkirt", new Vector2(0, -0.15f), new Vector2(1.9f, 0.12f), darkTone, 6);
                    CreateChildRect(t.root.transform, "ExhaustVent", new Vector2(isEnemy ? 0.65f : -0.65f, 0.22f), new Vector2(0.28f, 0.15f), shadowColor, 6);

                    // Circular command turret
                    var balTurret = CreateChildCircle(t.root.transform, "Turret", new Vector2(0f, 0.5f), 0.5f, darkTone, 8);
                    t.turret = balTurret.transform;
                    CreateChildCircle(t.turret, "CommandHatch", new Vector2(0f, 0.32f), 0.15f, shadowColor, 9);
                    break;
            }

            // Varied wheel sizes (Sprocket wheels at ends, larger road wheels in middle)
            t.wheels = new Transform[4];
            float[] wx = { -0.72f, -0.24f, 0.24f, 0.72f };
            float[] wr = { 0.16f, 0.23f, 0.23f, 0.16f }; // drive sprockets vs road wheels
            for (int i = 0; i < 4; i++)
            {
                var w = CreateChildCircle(t.root.transform, "Wheel" + i, new Vector2(wx[i], -0.52f), wr[i], metalGray, 3);
                // Draw cool sprocket spoke details on wheels
                CreateChildRect(w.transform, "Spoke1", Vector2.zero, new Vector2(wr[i] * 1.6f, 0.04f), Color.black, 4);
                CreateChildRect(w.transform, "Spoke2", Vector2.zero, new Vector2(0.04f, wr[i] * 1.6f), Color.black, 4);
                t.wheels[i] = w.transform;
            }

            // High-fidelity gun barrel assemblies
            float dir = isEnemy ? -1f : 1f;
            var barrelLength = data.id == TankId.Artillery ? 1.7f : (data.id == TankId.Siege ? 1.8f : 1.4f);
            var barrelWidth = data.id == TankId.Siege ? 0.28f : 0.16f;

            var barObj = CreateChildRect(t.turret, "Barrel", new Vector2(barrelLength * 0.5f * dir, 0), new Vector2(barrelLength, barrelWidth), new Color(0.18f, 0.2f, 0.25f), 7);
            t.barrel = barObj.transform;

            // Visible reinforced muzzle muzzle brake attachment
            CreateChildRect(t.barrel, "MuzzleBrake", new Vector2(dir * 0.45f, 0f), new Vector2(0.18f, barrelWidth * 1.5f), Color.black, 8);

            t.muzzle = new GameObject("Muzzle").transform;
            t.muzzle.SetParent(t.turret);
            t.muzzle.localPosition = new Vector3(barrelLength * dir, 0f, 0f);

            return t;
        }

        private void UpdateTankSlopeAlignments()
        {
            if (player != null && player.root != null)
            {
                player.y = GetTerrainHeight(player.x) + 0.62f;
                player.root.transform.position = new Vector3(player.x, player.y, 0);

                float x1 = player.x - 0.45f;
                float x2 = player.x + 0.45f;
                float y1 = GetTerrainHeight(x1);
                float y2 = GetTerrainHeight(x2);
                player.slopeAngle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
                player.root.transform.rotation = Quaternion.Euler(0, 0, player.slopeAngle);

                // Cannon independently rotatable
                player.turret.localRotation = Quaternion.Euler(0, 0, aimAngle - player.slopeAngle);
            }

            if (enemy != null && enemy.root != null)
            {
                enemy.y = GetTerrainHeight(enemy.x) + 0.62f;
                enemy.root.transform.position = new Vector3(enemy.x, enemy.y, 0);

                float x1 = enemy.x - 0.45f;
                float x2 = enemy.x + 0.45f;
                float y1 = GetTerrainHeight(x1);
                float y2 = GetTerrainHeight(x2);
                enemy.slopeAngle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
                enemy.root.transform.rotation = Quaternion.Euler(0, 0, enemy.slopeAngle);

                // Cannon independently rotatable
                enemy.turret.localRotation = Quaternion.Euler(0, 0, (180f - enemy.angle) - enemy.slopeAngle);
            }
        }

        private void UpdateRecoil(TankActor t)
        {
            if (t == null || t.barrel == null) return;
            t.recoilOffset = Mathf.MoveTowards(t.recoilOffset, 0f, Time.deltaTime * 3.5f);
            float dir = t.enemy ? -1f : 1f;
            float barrelLength = t.archetype == TankId.Artillery ? 1.7f : (t.archetype == TankId.Siege ? 1.8f : 1.4f);
            t.barrel.localPosition = new Vector3((barrelLength * 0.5f - t.recoilOffset) * dir, 0, 0);
        }

        private void ProcessProjectileBouncesAndMines(ProjectileSim p)
        {
            // Ricochet Round bounce
            if (p.weapon.id == WeaponId.RicochetRound && p.bounces < 1)
            {
                p.bounces++;
                p.velocity = new Vector2(p.velocity.x * 0.85f, Mathf.Abs(p.velocity.y) * 0.75f);
                SpawnExplosionFlash(p.position, p.weapon.radius * 0.5f, p.weapon.color);
                return;
            }

            Explode(p);
        }

        private void UpdateProjectile()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                float dt = Time.deltaTime;
                p.life += dt;

                // Sub-stepping to avoid tunneling
                float subStep = 0.05f;
                float timeToSim = dt;
                bool destroyed = false;

                while (timeToSim > 0f)
                {
                    float currentStep = Mathf.Min(timeToSim, subStep);

                    p.velocity += new Vector2(wind * GameConfig.WindFactor * p.weapon.windMultiplier, -GameConfig.Gravity * p.weapon.gravityMultiplier) * currentStep;
                    p.position += p.velocity * currentStep;

                    // Cluster Burst automatic splitting at apex of its path
                    if (p.weapon.id == WeaponId.ClusterBurst && !p.hasSplit && p.velocity.y < -0.2f && p.life > 0.45f)
                    {
                        p.hasSplit = true;
                        TriggerClusterSplit(p);
                        destroyed = true;
                        break;
                    }

                    // Airburst automatic detonation
                    if (p.weapon.id == WeaponId.Airburst && p.life >= 1.40f)
                    {
                        Explode(p);
                        destroyed = true;
                        break;
                    }

                    // Terrain Collision
                    float groundY = GetTerrainHeight(p.position.x);
                    if (p.position.y <= groundY)
                    {
                        p.position.y = groundY;
                        ProcessProjectileBouncesAndMines(p);
                        destroyed = true;
                        break;
                    }

                    // Tank Collision check
                    if (CheckTankCollision(p, player) || CheckTankCollision(p, enemy))
                    {
                        Explode(p);
                        destroyed = true;
                        break;
                    }

                    // Boundary exit
                    if (Mathf.Abs(p.position.x) > 15.5f || p.position.y < -5.5f)
                    {
                        destroyed = true;
                        break;
                    }

                    timeToSim -= currentStep;
                }

                if (destroyed)
                {
                    if (p.visual != null) Destroy(p.visual);
                    projectiles.RemoveAt(i);
                }
                else
                {
                    if (p.visual != null)
                    {
                        p.visual.transform.position = p.position;
                        if (p.velocity.sqrMagnitude > 0.01f)
                        {
                            float angle = Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg;
                            p.visual.transform.rotation = Quaternion.Euler(0, 0, angle);
                        }
                    }

                    p.trailTimer -= dt;
                    if (p.trailTimer <= 0)
                    {
                        p.trailTimer = 0.035f;
                        Color trailCol = p.weapon.color;
                        float trailSize = UnityEngine.Random.Range(0.06f, 0.12f);
                        float trailDuration = 0.35f;

                        if (p.weapon.id == WeaponId.Rocket || p.weapon.id == WeaponId.FireWeapon)
                        {
                            trailCol = Color.Lerp(Color.red, Color.yellow, UnityEngine.Random.value);
                            trailSize = UnityEngine.Random.Range(0.08f, 0.16f);
                            trailDuration = 0.22f;
                        }
                        else if (p.weapon.id == WeaponId.IceWeapon)
                        {
                            trailCol = new Color(0.4f, 0.9f, 1.0f, 0.6f);
                            trailSize = UnityEngine.Random.Range(0.05f, 0.1f);
                        }
                        else if (p.weapon.id == WeaponId.PlasmaWeapon)
                        {
                            trailCol = new Color(0.7f, 0.3f, 1.0f, 0.7f);
                            trailSize = UnityEngine.Random.Range(0.08f, 0.14f);
                        }
                        else if (p.weapon.id == WeaponId.LaserWeapon)
                        {
                            trailCol = new Color(1.0f, 0.1f, 0.4f, 0.8f);
                            trailSize = 0.1f;
                            trailDuration = 0.15f;
                        }

                        var trail = CreateCircleGameObject("Trail", p.position, trailSize, new Color(trailCol.r, trailCol.g, trailCol.b, 0.5f), 9);
                        Destroy(trail, trailDuration);
                    }
                }
            }
        }

        private bool CheckTankCollision(ProjectileSim p, TankActor tank)
        {
            if (tank == null || tank.root == null) return false;
            float dist = Vector2.Distance(p.position, new Vector2(tank.x, tank.y));
            return dist <= 0.85f; // compact hitbox
        }

        private void TriggerClusterSplit(ProjectileSim p)
        {
            // Splits into 4 deterministic shells downwards
            for (int k = 0; k < 4; k++)
            {
                float spreadAngle = -45f - k * 30f; // wide downwards fan
                float rad = spreadAngle * Mathf.Deg2Rad;
                Vector2 newVel = new Vector2(Mathf.Cos(rad) * 4f, Mathf.Sin(rad) * 4f);

                var splitProj = CreateCircleGameObject("SplitProj", p.position, 0.12f, p.weapon.color, 12);
                projectiles.Add(new ProjectileSim
                {
                    visual = splitProj,
                    position = p.position,
                    velocity = newVel,
                    weapon = p.weapon,
                    enemy = p.enemy,
                    trailTimer = 0f
                });
            }
        }

        private void Explode(ProjectileSim p)
        {
            Vector2 impactPos = p.position;
            float r = p.weapon.radius;

            // Trigger visual polish: screen shake, camera impact zoom
            cameraShake = GameConfig.ScreenShakeDuration;
            cameraShakeIntensity = Mathf.Clamp(r * 1.35f, 1.2f, 3.5f);

            SpawnExplosionFlash(impactPos, r, p.weapon.color);

            // Deform authoritative terrain array and rerender
            DeformTerrain(impactPos, p.weapon);

            // Mine Placement weapon logic
            if (p.weapon.id == WeaponId.MineDrop)
            {
                DeployMine(impactPos);
            }
            else
            {
                // Apply Splash Damage once to each tank
                ApplyExplosionDamage(player, impactPos, p.weapon);
                ApplyExplosionDamage(enemy, impactPos, p.weapon);
            }

            if (save.vibration && Application.isMobilePlatform)
            {
                Handheld.Vibrate();
            }

            CheckBattleEnd();

            // Next Turn Transitions without blocking the loop
            if (!battleEnded && projectiles.Count <= 1)
            {
                if (p.enemy)
                {
                    playerTurn = true;
                    player.fuel = tanks[(int)player.archetype].fuel;
                    playerMoveInput = 0;
                    turnNumber++;
                    wind = Mathf.Clamp(wind + UnityEngine.Random.Range(-0.85f, 0.85f), -3.5f, 3.5f);

                    // Periodic item crates
                    if (turnNumber % 3 == 0) SpawnRandomBattleDrop();

                    TriggerTurnBanner("PLAYER TURN");
                }
                else
                {
                    turnDelay = 1.3f;
                }
            }
        }

        private void SpawnExplosionFlash(Vector2 pos, float radius, Color color)
        {
            var flash = CreateCircleGameObject("Flash", pos, radius * 1.2f, Color.white, 16);
            flash.GetComponent<SpriteRenderer>().color = new Color(1f, 0.98f, 0.8f, 0.95f);
            Destroy(flash, 0.12f);

            // Procedural Fire and Smoke Particles
            for (int i = 0; i < 18; i++)
            {
                float angle = i * (360f / 18f) + UnityEngine.Random.Range(-12f, 12f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector2 vel = dir * UnityEngine.Random.Range(1.8f, 5.2f);
                SpawnParticle(pos, vel, color, Color.red, UnityEngine.Random.Range(0.12f, 0.32f), UnityEngine.Random.Range(0.3f, 0.65f));
            }

            // Smoke trails
            for (int i = 0; i < 8; i++)
            {
                Vector2 vel = new Vector2(UnityEngine.Random.Range(-1.5f, 1.5f), UnityEngine.Random.Range(1.0f, 3.5f));
                SpawnParticle(pos, vel, new Color(0.15f, 0.15f, 0.18f, 0.8f), new Color(0.3f, 0.3f, 0.35f, 0f), UnityEngine.Random.Range(0.35f, 0.65f), UnityEngine.Random.Range(0.8f, 1.5f));
            }

            // Sparkles
            for (int i = 0; i < 12; i++)
            {
                Vector2 vel = UnityEngine.Random.insideUnitCircle * radius * 3f;
                SpawnParticle(pos, vel, Color.yellow, new Color(1, 0.5f, 0, 0), 0.06f, UnityEngine.Random.Range(0.2f, 0.45f));
            }
        }

        private void DeformTerrain(Vector2 impact, WeaponData w)
        {
            if (w == null) return;

            float craterWidth = w.radius * 1.5f;
            float craterDepth = w.radius * 0.9f;

            // Custom Profiles based on WeaponId
            switch (w.id)
            {
                case WeaponId.StandardShell:
                    craterWidth = w.radius * 1.6f;
                    craterDepth = w.radius * 0.95f;
                    break;
                case WeaponId.HeavyShell:
                    craterWidth = w.radius * 1.8f;
                    craterDepth = w.radius * 1.3f;
                    break;
                case WeaponId.BunkerBreaker:
                    craterWidth = w.radius * 0.8f;
                    craterDepth = w.radius * 2.8f; // Extremely deep and narrow
                    break;
                case WeaponId.MortarArc:
                    craterWidth = w.radius * 1.2f;
                    craterDepth = w.radius * 1.4f;
                    break;
                case WeaponId.ClusterBurst:
                    craterWidth = w.radius * 1.1f;
                    craterDepth = w.radius * 0.45f; // shallow
                    break;
                case WeaponId.Rocket:
                    craterWidth = w.radius * 1.5f;
                    craterDepth = w.radius * 0.9f;
                    break;
                case WeaponId.FireWeapon:
                    craterWidth = w.radius * 1.7f;
                    craterDepth = w.radius * 0.5f; // shallow scorched
                    break;
                case WeaponId.IceWeapon:
                    craterWidth = w.radius * 1.2f;
                    craterDepth = w.radius * 0.4f; // small shallow
                    break;
                case WeaponId.PlasmaWeapon:
                    craterWidth = w.radius * 2.0f;
                    craterDepth = w.radius * 0.8f; // smooth wide
                    break;
                case WeaponId.LaserWeapon:
                    craterWidth = w.radius * 0.4f;
                    craterDepth = w.radius * 0.15f; // minimal deformation
                    break;
                case WeaponId.MineDrop:
                    craterWidth = w.radius * 1.0f;
                    craterDepth = w.radius * 0.6f;
                    break;
            }

            if (craterWidth <= 0.05f) return;

            for (int i = 0; i < terrainPointsCount; i++)
            {
                float tx = TerrainStart + i * TerrainStep;
                float dist = Mathf.Abs(tx - impact.x);

                if (dist < craterWidth)
                {
                    float factor = 1f - (dist / craterWidth);
                    float drop = 0f;

                    // Apply different curves for visual variety
                    switch (w.id)
                    {
                        case WeaponId.BunkerBreaker:
                        case WeaponId.LaserWeapon:
                            // Sharp needle parabolic curve
                            drop = craterDepth * Mathf.Pow(factor, 4f);
                            break;
                        case WeaponId.HeavyShell:
                        case WeaponId.PlasmaWeapon:
                            // Smooth semi-circular/cosine curve
                            drop = craterDepth * Mathf.Cos((dist / craterWidth) * (Mathf.PI * 0.5f));
                            break;
                        default:
                            // Rounded parabolic
                            drop = craterDepth * factor * factor;
                            break;
                    }

                    float originalHeight = terrainHeights[i];
                    terrainHeights[i] = Mathf.Clamp(originalHeight - drop, -4.5f, 4.8f);
                }
            }

            RenderTerrainBlocks();
            UpdateTankSlopeAlignments();
        }

        private void DeployMine(Vector2 pos)
        {
            if (activeMines.Count >= 3)
            {
                // Remove oldest
                if (activeMines[0].visual != null) Destroy(activeMines[0].visual);
                activeMines.RemoveAt(0);
            }

            var go = CreateCircleGameObject("Mine", pos, 0.28f, Color.yellow, 10);
            CreateChildCircle(go.transform, "InnerCore", Vector2.zero, 0.12f, Color.red, 11);
            activeMines.Add(new MineActor { visual = go, position = pos });
            AddLog("Area denial mine deployed on surface.");
        }

        private void SpawnRandomBattleDrop()
        {
            string[] types = { "HP", "Ammo", "Fuel", "Shield" };
            string type = types[UnityEngine.Random.Range(0, types.Length)];
            float rx = UnityEngine.Random.Range(-11f, 11f);
            float ry = GetTerrainHeight(rx) + 0.35f;

            Color c = Color.white;
            if (type == "HP") c = Color.green;
            else if (type == "Ammo") c = Color.red;
            else if (type == "Fuel") c = Color.yellow;
            else if (type == "Shield") c = Color.cyan;

            var go = CreateRectGameObject("Drop", new Vector2(rx, ry), new Vector2(0.55f, 0.55f), c, 9);
            CreateChildRect(go.transform, "CrossH", Vector2.zero, new Vector2(0.35f, 0.08f), Color.white, 10);
            CreateChildRect(go.transform, "CrossV", Vector2.zero, new Vector2(0.08f, 0.35f), Color.white, 10);

            activeDrops.Add(new DropActor { visual = go, position = new Vector2(rx, ry), type = type });
            AddLog("Supply drop landed on battlefield!");
        }

        private void CheckDropPickups(TankActor tank)
        {
            for (int i = activeDrops.Count - 1; i >= 0; i--)
            {
                var d = activeDrops[i];
                float dist = Mathf.Abs(tank.x - d.position.x);
                if (dist <= 0.95f)
                {
                    if (d.type == "HP")
                    {
                        tank.health = Mathf.Min(tank.maxHealth, tank.health + 30f);
                        SpawnTextIndicator(d.position, "+30 HP", Color.green);
                    }
                    else if (d.type == "Ammo")
                    {
                        for (int k = 0; k < tank.ammoInBattle.Length; k++)
                        {
                            tank.ammoInBattle[k] = System.Math.Min(weapons[k].initialAmmo, tank.ammoInBattle[k] + 2);
                        }
                        SpawnTextIndicator(d.position, "Ammo Refill", Color.red);
                    }
                    else if (d.type == "Fuel")
                    {
                        tank.fuel = tanks[(int)tank.archetype].fuel;
                        SpawnTextIndicator(d.position, "Fuel Refill", Color.yellow);
                    }
                    else if (d.type == "Shield")
                    {
                        tank.shieldTurns = 2;
                        SpawnTextIndicator(d.position, "Shield Active", Color.cyan);
                    }

                    if (d.visual != null) Destroy(d.visual);
                    activeDrops.RemoveAt(i);
                }
            }
        }

        private void ApplyExplosionDamage(TankActor target, Vector2 impact, WeaponData w)
        {
            if (target == null) return;
            float dist = Vector2.Distance(new Vector2(target.x, target.y), impact);
            if (dist > w.radius + 1.2f) return;

            // Damage falloff
            float factor = 1f - Mathf.Clamp01(dist / (w.radius + 1.2f)) * 0.6f;
            float multiplier = target.enemy ? 1f : 1f + (save.weaponLevels[(int)w.id] - 1) * 0.06f;
            float dmg = w.damage * factor * multiplier * (1f - target.armor);

            if (target.shieldTurns > 0)
            {
                dmg *= 0.4f; // 60% mitigation
                target.shieldTurns--;
            }

            target.health = Mathf.Max(0f, target.health - dmg);

            SpawnTextIndicator(new Vector2(target.x, target.y + 1.4f), Mathf.RoundToInt(dmg).ToString(), target.enemy ? Color.red : Color.yellow);
        }

        private void SpawnTextIndicator(Vector2 pos, string txt, Color c)
        {
            floatTexts.Add(new FloatText
            {
                pos = pos,
                text = txt,
                life = 1.25f,
                color = c
            });
        }

        private void UpdateCameraTracking()
        {
            if (projectiles.Count > 0)
            {
                // Smoothly pan camera to track active projectile
                Vector2 targetPos = projectiles[0].position;
                targetPos.y = Mathf.Max(targetPos.y, 1.2f); // Floor clamp
                Vector3 dest = new Vector3(Mathf.Clamp(targetPos.x, -10f, 10f), Mathf.Clamp(targetPos.y + 0.5f, 1.5f, 6.5f), -10);
                cam.transform.position = Vector3.Lerp(cam.transform.position, dest, Time.deltaTime * 6f);
                cameraBase = cam.transform.position;
            }
            else
            {
                // Focus on the active player
                float tx = playerTurn ? player.x : enemy.x;
                float ty = playerTurn ? player.y : enemy.y;
                Vector3 dest = new Vector3(Mathf.Clamp(tx, -8f, 8f), Mathf.Clamp(ty + 1.4f, 1.6f, 4.2f), -10);
                cam.transform.position = Vector3.Lerp(cam.transform.position, dest, Time.deltaTime * 5f);
                cameraBase = cam.transform.position;
            }
        }

        private void EnemyThinkAndShoot()
        {
            if (projectiles.Count > 0 || battleEnded) return;

            // Simple "Thinking" overlay
            TriggerTurnBanner("RIVAL DECIDING...");

            // Run AI Simulator and fire (Phase 6)
            RunAISimulator();
        }

        private void RunAISimulator()
        {
            float difficultyError = 6.0f;
            if (mode == BattleMode.Campaign)
            {
                difficultyError = Mathf.Max(1.0f, 8.0f - campaignStage * 0.75f);
            }

            // Standard Ballistic solver (Phase 6)
            float dx = player.x - enemy.x; // signed distance
            float dy = player.y - enemy.y;

            // Choose appropriate weapon from available Ammo
            int chosenWeaponIdx = 0;
            for (int i = weapons.Length - 1; i >= 0; i--)
            {
                if (enemy.ammoInBattle[i] > 0)
                {
                    chosenWeaponIdx = i;
                    break;
                }
            }

            WeaponData w = weapons[chosenWeaponIdx];

            // Deterministic solver
            float targetAngle = 45f;
            float targetPower = 52f;

            // Hard tactical calculations (Samples multiple combos to find best target)
            float bestDist = 9999f;
            for (float testAng = 25f; testAng <= 75f; testAng += 10f)
            {
                float a = testAng * Mathf.Deg2Rad;
                float scale = 0.24f;
                // Solve quadratic search or quick simulation loop
                float testPower = Mathf.Sqrt(Mathf.Abs(dx) * GameConfig.Gravity / Mathf.Max(0.1f, Mathf.Sin(2f * a))) / scale;
                testPower = Mathf.Clamp(testPower, GameConfig.MinShotPower, GameConfig.MaxShotPower);

                float simX = enemy.x;
                float simY = enemy.muzzle.position.y;
                float scaleDir = -1f; // Firing left
                Vector2 vel = new Vector2(Mathf.Cos(180f * Mathf.Deg2Rad - a) * testPower * scale, Mathf.Sin(a) * testPower * scale);

                // Quick simulation trace
                for (int step = 0; step < 40; step++)
                {
                    float simDt = 0.08f;
                    vel += new Vector2(wind * GameConfig.WindFactor * w.windMultiplier, -GameConfig.Gravity * w.gravityMultiplier) * simDt;
                    simX += vel.x * simDt;
                    simY += vel.y * simDt;

                    if (simY <= GetTerrainHeight(simX))
                    {
                        float finalDist = Mathf.Abs(simX - player.x);
                        if (finalDist < bestDist)
                        {
                            bestDist = finalDist;
                            targetAngle = testAng;
                            targetPower = testPower;
                        }
                        break;
                    }
                }
            }

            // Apply Controlled Difficulty-Based Inaccuracy Error
            if (mode != BattleMode.Training)
            {
                float angleErr = UnityEngine.Random.Range(-difficultyError, difficultyError);
                float powerErr = UnityEngine.Random.Range(-difficultyError * 0.5f, difficultyError * 0.5f);
                targetAngle = Mathf.Clamp(targetAngle + angleErr, GameConfig.MinAngle, GameConfig.MaxAngle);
                targetPower = Mathf.Clamp(targetPower + powerErr, GameConfig.MinShotPower, GameConfig.MaxShotPower);
            }

            enemy.angle = targetAngle;
            enemy.ammoInBattle[chosenWeaponIdx]--;

            // Faint pause then launch projectile sequentially
            InvokeLaunchEnemy(w, targetPower);
        }

        private void InvokeLaunchEnemy(WeaponData w, float power)
        {
            LaunchSequence(enemy, w, power, true);
            AddLog("Enemy fired " + w.name + ".");
        }

        private void PlayerShoot()
        {
            if (!playerTurn || projectiles.Count > 0) return;

            WeaponData w = weapons[selectedWeaponIndex];
            if (player.ammoInBattle[selectedWeaponIndex] <= 0)
            {
                Toast("NO AMMUNITION!");
                return;
            }

            player.ammoInBattle[selectedWeaponIndex]--;
            player.angle = aimAngle;
            LaunchSequence(player, w, shotPower, false);
            playerTurn = false;
            AddLog("You launched " + w.name + ".");
        }

        private void LaunchSequence(TankActor source, WeaponData w, float power, bool isEnemy)
        {
            source.recoilOffset = 0.45f;
            int count = w.projectiles;
            for (int i = 0; i < count; i++)
            {
                float spread = (count == 1) ? 0f : (i - (count - 1) * 0.5f) * 4.5f;
                float finalAng = (source.angle + spread);
                float finalRad = finalAng * Mathf.Deg2Rad;

                float scale = 0.24f;
                Vector2 vel;
                if (isEnemy)
                {
                    // Facing Left (180 deg offset)
                    float worldRad = (180f - finalAng) * Mathf.Deg2Rad;
                    vel = new Vector2(Mathf.Cos(worldRad) * power * scale, Mathf.Sin(worldRad) * power * scale);
                }
                else
                {
                    // Facing Right
                    vel = new Vector2(Mathf.Cos(finalRad) * power * scale, Mathf.Sin(finalRad) * power * scale);
                }

                // Spawns perfectly at visible barrel muzzle Brake!
                var visualObj = CreateCircleGameObject("Projectile", source.muzzle.position, 0.16f, w.color, 12);
                var projSim = new ProjectileSim
                {
                    visual = visualObj,
                    position = source.muzzle.position,
                    velocity = vel,
                    weapon = w,
                    enemy = isEnemy,
                    trailTimer = 0f
                };
                StyleProjectileVisual(projSim);
                projectiles.Add(projSim);
            }
            ClearTrajectory();
        }

        private void StyleProjectileVisual(ProjectileSim p)
        {
            if (p == null || p.visual == null) return;

            var rootSr = p.visual.GetComponent<SpriteRenderer>();
            if (rootSr != null) rootSr.enabled = false;

            Color c = p.weapon.color;
            Color shadowColor = new Color(0.05f, 0.05f, 0.08f, 0.6f);

            switch (p.weapon.id)
            {
                case WeaponId.StandardShell:
                    CreateChildRect(p.visual.transform, "Body", Vector2.zero, new Vector2(0.25f, 0.12f), Color.gray, 12);
                    CreateChildCircle(p.visual.transform, "Nose", new Vector2(0.125f, 0f), 0.06f, Color.yellow, 13);
                    CreateChildRect(p.visual.transform, "Fin", new Vector2(-0.125f, 0f), new Vector2(0.06f, 0.16f), Color.black, 11);
                    break;

                case WeaponId.HeavyShell:
                    CreateChildRect(p.visual.transform, "Body", Vector2.zero, new Vector2(0.35f, 0.22f), Color.gray, 12);
                    CreateChildCircle(p.visual.transform, "Nose", new Vector2(0.175f, 0f), 0.11f, Color.red, 13);
                    CreateChildRect(p.visual.transform, "Band", new Vector2(-0.06f, 0f), new Vector2(0.06f, 0.24f), Color.yellow, 13);
                    break;

                case WeaponId.BunkerBreaker:
                    CreateChildRect(p.visual.transform, "Body", Vector2.zero, new Vector2(0.42f, 0.08f), new Color(0.75f, 0.78f, 0.82f), 12);
                    CreateChildCircle(p.visual.transform, "Nose", new Vector2(0.21f, 0f), 0.04f, Color.black, 13);
                    break;

                case WeaponId.Rocket:
                    CreateChildRect(p.visual.transform, "Body", Vector2.zero, new Vector2(0.38f, 0.14f), Color.white, 12);
                    CreateChildCircle(p.visual.transform, "Nose", new Vector2(0.19f, 0f), 0.07f, Color.red, 13);
                    CreateChildRect(p.visual.transform, "FinL", new Vector2(-0.16f, 0.09f), new Vector2(0.08f, 0.06f), Color.red, 11);
                    CreateChildRect(p.visual.transform, "FinR", new Vector2(-0.16f, -0.09f), new Vector2(0.08f, 0.06f), Color.red, 11);
                    break;

                case WeaponId.FireWeapon:
                    if (rootSr != null)
                    {
                        rootSr.enabled = true;
                        rootSr.color = Color.yellow;
                    }
                    CreateChildCircle(p.visual.transform, "OuterFlame", Vector2.zero, 0.22f, new Color(1.0f, 0.35f, 0f, 0.85f), 11);
                    break;

                case WeaponId.IceWeapon:
                    var crystal = CreateChildRect(p.visual.transform, "Crystal", Vector2.zero, new Vector2(0.18f, 0.18f), new Color(0.4f, 0.9f, 1f, 0.9f), 12);
                    crystal.transform.localRotation = Quaternion.Euler(0, 0, 45);
                    CreateChildCircle(p.visual.transform, "Core", Vector2.zero, 0.06f, Color.white, 13);
                    break;

                case WeaponId.PlasmaWeapon:
                    if (rootSr != null)
                    {
                        rootSr.enabled = true;
                        rootSr.color = Color.white;
                    }
                    CreateChildCircle(p.visual.transform, "OuterShield", Vector2.zero, 0.26f, new Color(0.6f, 0.15f, 0.92f, 0.6f), 11);
                    CreateChildCircle(p.visual.transform, "InnerEnergy", Vector2.zero, 0.18f, new Color(0.85f, 0.3f, 1.0f, 0.95f), 12);
                    break;

                case WeaponId.LaserWeapon:
                    var beam = CreateChildRect(p.visual.transform, "Beam", Vector2.zero, new Vector2(1.2f, 0.15f), new Color(1.0f, 0.1f, 0.4f, 0.95f), 12);
                    CreateChildRect(beam.transform, "Core", Vector2.zero, new Vector2(1.2f, 0.05f), Color.white, 13);
                    break;

                case WeaponId.SmokeRound:
                    CreateChildRect(p.visual.transform, "Canister", Vector2.zero, new Vector2(0.26f, 0.14f), new Color(0.55f, 0.6f, 0.55f), 12);
                    CreateChildCircle(p.visual.transform, "Tip", new Vector2(0.13f, 0f), 0.07f, Color.green, 13);
                    break;

                case WeaponId.MortarArc:
                    CreateChildCircle(p.visual.transform, "Body", Vector2.zero, 0.14f, new Color(0.35f, 0.35f, 0.35f), 12);
                    CreateChildRect(p.visual.transform, "Tail", new Vector2(-0.15f, 0f), new Vector2(0.14f, 0.08f), Color.black, 11);
                    CreateChildRect(p.visual.transform, "Fin", new Vector2(-0.22f, 0f), new Vector2(0.04f, 0.22f), Color.gray, 11);
                    break;

                default:
                    if (rootSr != null)
                    {
                        rootSr.enabled = true;
                        rootSr.color = c;
                    }
                    break;
            }
        }

        private void CheckBattleEnd()
        {
            if (enemy.health <= 0)
            {
                battleEnded = true;
                save.wins++;
                save.coins += (mode == BattleMode.Campaign) ? 220 + campaignStage * 30 : 120;
                save.xp += 100;
                if (mode == BattleMode.Campaign) campaignStage++;
                LevelCheck();
                Save();
                Invoke(nameof(WinResult), 1.2f);
            }
            else if (player.health <= 0)
            {
                battleEnded = true;
                save.losses++;
                save.coins += 35;
                save.xp += 20;
                LevelCheck();
                Save();
                Invoke(nameof(LoseResult), 1.2f);
            }
        }

        private void WinResult() { screen = ScreenState.Result; message = "VICTORY"; }
        private void LoseResult() { screen = ScreenState.Result; message = "DEFEAT"; }

        private void DrawBattleHUD()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            // Respect Safe Area insets (env guidelines)
            float safeL = safe.x;
            float safeR = sw - (safe.x + safe.width);
            float safeB = sh - (safe.y + safe.height);

            // 1. TOP HUD (Left: Player, Right: Enemy, Center: Turn indicator & wind)
            float panelW = sw * 0.28f;
            Box(new Rect(16 + safeL, 12, panelW, 70), new Color(0.04f, 0.06f, 0.12f, 0.94f));
            GUI.Label(new Rect(28 + safeL, 16, panelW - 24, 26), tanks[save.selectedTank].name, smallStyle);
            DrawProgress(new Rect(28 + safeL, 46, panelW - 24, 20), (float)player.health / (float)player.maxHealth, new Color(0.25f, 0.85f, 0.4f));

            Box(new Rect(sw - panelW - 16 - safeR, 12, panelW, 70), new Color(0.04f, 0.06f, 0.12f, 0.94f));
            GUI.Label(new Rect(sw - panelW - 4 - safeR, 16, panelW - 24, 26), (mode == BattleMode.Training) ? "TARGET DUMMY" : "OPPONENT", smallStyle);
            DrawProgress(new Rect(sw - panelW - 4 - safeR, 46, panelW - 24, 20), (float)enemy.health / (float)enemy.maxHealth, new Color(0.92f, 0.26f, 0.22f));

            // Wind Center HUD
            Box(new Rect(sw * 0.38f, 12, sw * 0.24f, 54), new Color(0.04f, 0.06f, 0.12f, 0.94f));
            string arrow = wind >= 0f ? "-->" : "<--";
            GUI.Label(new Rect(sw * 0.38f, 16, sw * 0.24f, 40), "WIND: " + arrow + " " + Mathf.Abs(wind).ToString("0.0"), centerStyle);

            if (GUI.Button(new Rect(sw - 74 - safeR, sh - 60, 56, 44), "II", buttonStyle)) paused = !paused;
            if (paused) { DrawPause(); return; }

            // Block interactive actions during turns
            if (!playerTurn || projectiles.Count > 0)
            {
                if (!playerTurn && projectiles.Count == 0)
                {
                    Box(new Rect(sw * 0.35f, sh * 0.4f, sw * 0.3f, 54), new Color(0.04f, 0.05f, 0.1f, 0.94f));
                    GUI.Label(new Rect(sw * 0.35f, sh * 0.41f, sw * 0.3f, 40), "OPPONENT THINKING...", centerStyle);
                }
                playerMoveInput = 0;
                return;
            }

            // 2. BOTTOM PANEL HUD (Optimized responsive layout)
            float botH = sh * 0.22f; // Compact: 22% of screen height
            float leftW = sw * 0.33f;
            float rightW = sw * 0.25f;
            float midW = sw - leftW - rightW - 24f - safeL - safeR;

            // Bottom Left: Joystick and Fuel Points Meter
            Rect rLeft = new Rect(12 + safeL, sh - botH - 8, leftW, botH);
            Box(rLeft, new Color(0.04f, 0.05f, 0.1f, 0.5f)); // Transparent

            // Draw Virtual Joystick
            float joyX = rLeft.x + 65f;
            float joyY = rLeft.y + rLeft.height * 0.5f;
            GUI.color = new Color(0.12f, 0.18f, 0.32f, 0.6f);
            GUI.DrawTexture(new Rect(joyX - joystickRadius * 0.8f, joyY - joystickRadius * 0.8f, joystickRadius * 1.6f, joystickRadius * 1.6f), white);
            GUI.color = Color.white;

            float thumbX = joyX + (isJoystickDragging ? (joystickCurrent.x - joystickStart.x) : 0f);
            thumbX = Mathf.Clamp(thumbX, joyX - joystickRadius * 0.8f, joyX + joystickRadius * 0.8f);
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(thumbX - 16f, joyY - 16f, 32f, 32f), white);
            GUI.color = Color.white;

            // Fuel points layout
            float fuelPct = player.fuel / tanks[(int)player.archetype].fuel;
            GUI.Label(new Rect(rLeft.x + 135f, rLeft.y + 10f, leftW - 145f, 22), "FUEL METER", smallStyle);
            DrawProgress(new Rect(rLeft.x + 135f, rLeft.y + 34f, leftW - 145f, 12), fuelPct, new Color(0.92f, 0.72f, 0.18f));
            GUI.Label(new Rect(rLeft.x + 135f, rLeft.y + 50f, leftW - 145f, 36), "HOLD TO MOVE", smallStyle);

            // Bottom Center: Collapsible Weapon Selector Capsule / Scroll Drawer
            Rect rMid = new Rect(rLeft.xMax + 6, sh - botH - 8, midW, botH);
            Box(rMid, new Color(0.04f, 0.05f, 0.1f, 0.65f));

            if (!isWeaponDrawerOpen)
            {
                // Collapsed: Selected Weapon Capsule
                var currentW = weapons[selectedWeaponIndex];

                // Quick Switch Left
                if (GUI.Button(new Rect(rMid.x + 6, rMid.y + (rMid.height - 36) * 0.5f, 32, 36), "<", buttonStyle))
                {
                    selectedWeaponIndex = (selectedWeaponIndex - 1 + weapons.Length) % weapons.Length;
                }

                // Center Information Button (Click to Expand Drawer)
                Rect capsuleBtn = new Rect(rMid.x + 44, rMid.y + 6, rMid.width - 88, rMid.height - 12);
                GUI.color = new Color(0.08f, 0.14f, 0.28f, 0.8f);
                GUI.DrawTexture(capsuleBtn, white);
                GUI.color = Color.white;

                // Color swatch
                GUI.color = currentW.color;
                GUI.DrawTexture(new Rect(capsuleBtn.x + 12, capsuleBtn.y + (capsuleBtn.height - 12) * 0.5f, 12, 12), white);
                GUI.color = Color.white;

                string capsuleText = currentW.name.ToUpper() + " (" + player.ammoInBattle[selectedWeaponIndex] + ")  [+] SELECT";
                if (GUI.Button(capsuleBtn, capsuleText, smallStyle))
                {
                    isWeaponDrawerOpen = true;
                }

                // Quick Switch Right
                if (GUI.Button(new Rect(rMid.xMax - 38, rMid.y + (rMid.height - 36) * 0.5f, 32, 36), ">", buttonStyle))
                {
                    selectedWeaponIndex = (selectedWeaponIndex + 1) % weapons.Length;
                }
            }
            else
            {
                // Expanded horizontal scrollable drawer
                GUI.Label(new Rect(rMid.x + 10, rMid.y + 6, 120, 22), "SELECT WEAPON", smallStyle);
                if (GUI.Button(new Rect(rMid.xMax - 75, rMid.y + 4, 70, 22), "[x] CLOSE", smallStyle))
                {
                    isWeaponDrawerOpen = false;
                }

                scroll = GUI.BeginScrollView(new Rect(rMid.x + 6, rMid.y + 28, rMid.width - 12, rMid.height - 34), scroll, new Rect(0, 0, weapons.Length * 115, rMid.height - 45));
                for (int i = 0; i < weapons.Length; i++)
                {
                    Rect itemRect = new Rect(i * 110 + 4, 4, 102, rMid.height - 42);
                    bool isSelected = selectedWeaponIndex == i;
                    Box(itemRect, isSelected ? new Color(0.48f, 0.22f, 0.05f, 0.85f) : new Color(0.08f, 0.12f, 0.22f, 0.75f));

                    GUI.color = weapons[i].color;
                    GUI.DrawTexture(new Rect(itemRect.x + 6, itemRect.y + 6, itemRect.width - 12, 5), white);
                    GUI.color = Color.white;

                    GUI.Label(new Rect(itemRect.x + 4, itemRect.y + 14, itemRect.width - 8, 22), weapons[i].name, smallStyle);
                    GUI.Label(new Rect(itemRect.x + 4, itemRect.y + 38, itemRect.width - 8, 18), "AMMO: " + player.ammoInBattle[i], smallStyle);

                    if (GUI.Button(itemRect, "", GUIStyle.none))
                    {
                        selectedWeaponIndex = i;
                        isWeaponDrawerOpen = false;
                    }
                }
                GUI.EndScrollView();
            }

            // Bottom Right: Touch Drag-to-Aim indicators & FIRE
            Rect rRight = new Rect(rMid.xMax + 6, sh - botH - 8, rightW, botH);
            Box(rRight, new Color(0.04f, 0.05f, 0.1f, 0.5f)); // Transparent

            GUI.Label(new Rect(rRight.x + 10, rRight.y + 8, rRight.width - 20, 22), "ANG: " + Mathf.RoundToInt(aimAngle) + "°  PWR: " + Mathf.RoundToInt(shotPower), smallStyle);

            // Circular fire button
            float fireRadius = 26f;
            float fireX = rRight.x + rRight.width * 0.5f;
            float fireY = rRight.y + rRight.height * 0.62f;
            GUI.color = new Color(0.85f, 0.15f, 0.15f, 0.9f);
            if (GUI.Button(new Rect(fireX - fireRadius, fireY - fireRadius, fireRadius * 2, fireRadius * 2), "FIRE", buttonStyle))
            {
                if (!isWeaponDrawerOpen) PlayerShoot();
            }
            GUI.color = Color.white;

            // Drag-to-Aim gesture hint line (Phase 3)
            if (isAimingDragging)
            {
                GUI.Label(new Rect(sw * 0.45f, sh * 0.15f, 250, 45), "DRAGGING AIM ACTIVE", centerStyle);
            }
        }

        private void DrawPause()
        {
            Box(new Rect(Screen.width * 0.32f, Screen.height * 0.22f, Screen.width * 0.36f, Screen.height * 0.56f), new Color(0.04f, 0.05f, 0.1f, 0.98f));
            GUI.Label(new Rect(Screen.width * 0.32f, Screen.height * 0.26f, Screen.width * 0.36f, 50), "GAME PAUSED", titleStyle);
            if (BigButton(new Rect(Screen.width * 0.38f, Screen.height * 0.38f, Screen.width * 0.24f, 55), "RESUME PLAY")) paused = false;
            if (BigButton(new Rect(Screen.width * 0.38f, Screen.height * 0.49f, Screen.width * 0.24f, 55), "RESTART BATTLE")) StartBattle();
            if (BigButton(new Rect(Screen.width * 0.38f, Screen.height * 0.6f, Screen.width * 0.24f, 50), "MAIN MENU")) { ClearWorld(); screen = ScreenState.Menu; }
        }

        private void DrawResult()
        {
            DrawBackdrop();
            bool won = message == "VICTORY";
            GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 90), message, titleStyle);
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.32f, Screen.width * 0.6f, 100),
                won ? "Outstanding tactical performance! Rewards allocated." : "Study the wind patterns, adjust ballistic trajectories and reload.", centerStyle);

            GUI.Label(new Rect(0, Screen.height * 0.48f, Screen.width, 40), "CREDITS: " + save.coins + "   GEMS: " + save.gems + "   LEVEL: " + save.level, centerStyle);

            if (BigButton(new Rect(Screen.width * 0.3f, Screen.height * 0.62f, Screen.width * 0.18f, 65), "REMATCH")) StartBattle();
            if (BigButton(new Rect(Screen.width * 0.52f, Screen.height * 0.62f, Screen.width * 0.18f, 65), "EXIT TO MENU")) { ClearWorld(); screen = ScreenState.Menu; }
        }

        private void TriggerTurnBanner(string text)
        {
            turnBannerText = text;
            turnBannerTimer = 1.35f;
        }

        private void DrawTurnBanner()
        {
            float w = Screen.width * 0.5f;
            float h = 60f;
            Rect r = new Rect((Screen.width - w) / 2, Screen.height * 0.3f, w, h);
            Box(r, new Color(0.12f, 0.18f, 0.35f, 0.94f));
            GUI.Label(r, turnBannerText, titleStyle);
        }

        private void LevelCheck()
        {
            int required = save.level * 160;
            while (save.xp >= required)
            {
                save.xp -= required;
                save.level++;
                save.gems += 6;
                required = save.level * 160;
            }
        }

        private void ShowTrajectory()
        {
            if (!save.trajectory || !playerTurn || player == null) return;
            // Short Trajectory Preview: 5 faded dots representing 15-25% of path (Phase 3)
            for (int i = 0; i < 6; i++)
            {
                var dot = CreateCircleGameObject("TrajectoryDot", Vector2.zero, 0.05f + i * 0.01f, new Color(1f, 1f, 1f, 0.65f - i * 0.1f), 7);
                trajectoryDots.Add(dot);
            }
        }

        private void UpdateTrajectory()
        {
            if (!playerTurn || projectiles.Count > 0 || !save.trajectory) return;
            if (trajectoryDots.Count == 0) ShowTrajectory();

            float a = aimAngle * Mathf.Deg2Rad;
            Vector2 start = player.muzzle.position;
            Vector2 vel = new Vector2(Mathf.Cos(a) * shotPower * 0.24f, Mathf.Sin(a) * shotPower * 0.24f);

            WeaponData w = weapons[selectedWeaponIndex];

            for (int i = 0; i < trajectoryDots.Count; i++)
            {
                // Scale factors matched to projectile gravity/wind multipliers perfectly!
                float t = (i + 1) * 0.08f;
                Vector2 pos = start + vel * t + 0.5f * new Vector2(wind * GameConfig.WindFactor * w.windMultiplier, -GameConfig.Gravity * w.gravityMultiplier) * t * t;
                trajectoryDots[i].transform.position = pos;
                trajectoryDots[i].SetActive(pos.y > GetTerrainHeight(pos.x));
            }
        }

        private void ClearTrajectory()
        {
            foreach (var d in trajectoryDots) if (d != null) Destroy(d);
            trajectoryDots.Clear();
        }

        private void ClearWorld()
        {
            ClearParticlePool();
            foreach (var o in worldObjects) if (o != null) Destroy(o);
            worldObjects.Clear();
            projectiles.Clear();
            trajectoryDots.Clear();
            floatTexts.Clear();
            activeMines.Clear();
            activeDrops.Clear();
            activeParticles.Clear();
            player = null;
            enemy = null;
            terrainMeshGo = null;
            terrainMesh = null;
        }

        private GameObject CreateRectGameObject(string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            TrackGameObject(go);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetCustomSprite(false);
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        private GameObject CreateCircleGameObject(string name, Vector2 pos, float radius, Color color, int order)
        {
            var go = new GameObject(name);
            TrackGameObject(go);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * radius * 2f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetCustomSprite(true);
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        private GameObject CreateChildRect(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetCustomSprite(false);
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        private GameObject CreateChildCircle(Transform parent, string name, Vector2 pos, float radius, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * radius * 2f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetCustomSprite(true);
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        private Sprite GetCustomSprite(bool circle)
        {
            if (!circle && squareSprite != null) return squareSprite;
            if (circle && circleSprite != null) return circleSprite;

            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = !circle || Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) <= 31.2f;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }
            tex.Apply();

            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64);
            if (circle) circleSprite = sp; else squareSprite = sp;
            return sp;
        }

        private void TrackGameObject(GameObject go) { worldObjects.Add(go); }
        private void AddLog(string s) { combatLog.Insert(0, s); if (combatLog.Count > 8) combatLog.RemoveAt(8); }
        private void Toast(string s) { message = s; messageTimer = 1.7f; }

        private void DrawToast(string s)
        {
            float w = Screen.width * 0.38f;
            Box(new Rect((Screen.width - w) / 2, Screen.height * 0.11f, w, 44), new Color(0.04f, 0.05f, 0.1f, 0.95f));
            GUI.Label(new Rect((Screen.width - w) / 2 + 10, Screen.height * 0.11f + 5, w - 20, 34), s, centerStyle);
        }

        private bool BackButton() { return GUI.Button(new Rect(16, Screen.height - 60, 140, 44), "< BACK", buttonStyle); }

        private bool BigButton(Rect r, string text)
        {
            GUI.color = new Color(0.12f, 0.32f, 0.65f, 1f);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            return GUI.Button(r, text, buttonStyle);
        }

        private void Box(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
        }

        private void DrawProgress(Rect r, float value, Color color)
        {
            Box(r, healthBarEmpty.GetPixel(0,0));
            Rect filledRect = r;
            filledRect.width *= Mathf.Clamp01(value);
            Box(filledRect, color);
        }

        // Texture creation helpers
        private Texture2D CreateSolidTexture(Color color)
        {
            if (colorTextures.TryGetValue(color, out var tex)) return tex;
            tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            colorTextures[color] = tex;
            return tex;
        }

        private Texture2D CreateGradientTexture(Color top, Color bottom, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / h);
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString("TT_SAVE_V2", "");
            save = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            if (save == null) save = new SaveData();
            if (save.tankLevels == null || save.tankLevels.Length != 5) save.tankLevels = new int[5] { 1, 1, 1, 1, 1 };
            if (save.weaponLevels == null || save.weaponLevels.Length != 15)
            {
                var newLevels = new int[15];
                for (int i = 0; i < 15; i++)
                {
                    newLevels[i] = (save.weaponLevels != null && i < save.weaponLevels.Length) ? save.weaponLevels[i] : 1;
                }
                save.weaponLevels = newLevels;
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString("TT_SAVE_V2", JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        private void OnApplicationPause(bool pause) { if (pause) Save(); }
        private void OnApplicationQuit() { Save(); }
    }
}
