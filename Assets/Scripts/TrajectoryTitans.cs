using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryTitans
{
    public enum ScreenState { Menu, Garage, Daily, Settings, Battle, Result }
    public enum BattleMode { Campaign, QuickBattle, Training }

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
        public int[] tankLevels = new int[4] { 1, 1, 1, 1 };
        public int[] weaponLevels = new int[8] { 1, 1, 1, 1, 1, 1, 1, 1 };
        public string lastDaily = "";
        public bool sound = true;
        public bool vibration = true;
        public bool trajectory = true;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.8f;
    }

    public class WeaponData
    {
        public string name;
        public string description;
        public float damage;
        public float radius;
        public int projectiles;
        public Color color;
        public bool split;
        public bool heavy;
        public WeaponData(string n, string d, float dmg, float rad, int count, Color c, bool s = false, bool h = false)
        { name = n; description = d; damage = dmg; radius = rad; projectiles = count; color = c; split = s; heavy = h; }
    }

    public class TankData
    {
        public string name;
        public string trait;
        public Color body;
        public int health;
        public float armor;
        public TankData(string n, string t, Color c, int hp, float a)
        { name = n; trait = t; body = c; health = hp; armor = a; }
    }

    public class TankActor
    {
        public GameObject root;
        public Transform turret;
        public Transform muzzle;
        public float health;
        public float maxHealth;
        public float armor;
        public bool enemy;
        public float x;
        public float y;
        public float angle = 40f;
        public int shieldTurns;
    }

    public class ProjectileSim
    {
        public GameObject visual;
        public Vector2 position;
        public Vector2 velocity;
        public WeaponData weapon;
        public bool enemy;
        public float life;
    }

    public class GameRoot : MonoBehaviour
    {
        private static GameRoot instance;
        private SaveData save;
        private ScreenState screen = ScreenState.Menu;
        private BattleMode mode;
        private Camera cam;
        private Texture2D white;
        private GUIStyle titleStyle, h1Style, bodyStyle, buttonStyle, smallStyle, centerStyle;
        private readonly List<GameObject> worldObjects = new List<GameObject>();
        private readonly List<ProjectileSim> projectiles = new List<ProjectileSim>();
        private readonly List<GameObject> trajectoryDots = new List<GameObject>();
        private readonly List<string> combatLog = new List<string>();
        private TankActor player, enemy;
        private bool playerTurn = true;
        private bool battleEnded;
        private float turnDelay;
        private float aimAngle = 42f;
        private float shotPower = 50f;
        private int weaponIndex;
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

        private readonly WeaponData[] weapons = new WeaponData[]
        {
            new WeaponData("Comet Shell", "Reliable blast with balanced damage.", 28, 1.5f, 1, new Color(1f,.72f,.15f)),
            new WeaponData("Triple Spark", "Three lighter shells spread across the target.", 15, 1.1f, 3, new Color(.25f,.9f,1f)),
            new WeaponData("Meteor Core", "Heavy shell with a large shockwave.", 42, 2.2f, 1, new Color(1f,.25f,.12f), false, true),
            new WeaponData("Prism Splitter", "Splits into a three-part aerial strike.", 18, 1.2f, 3, new Color(.85f,.3f,1f), true),
            new WeaponData("Frost Capsule", "Moderate damage and weakens the next enemy shot.", 24, 1.6f, 1, new Color(.45f,.8f,1f)),
            new WeaponData("Quake Bomb", "Low velocity bomb with enormous impact radius.", 36, 2.8f, 1, new Color(.65f,.42f,.2f), false, true),
            new WeaponData("Needle Laser", "Small radius but excellent direct-hit damage.", 52, .65f, 1, new Color(1f,.2f,.6f)),
            new WeaponData("Solar Nova", "Legendary high-energy round.", 58, 2.4f, 1, new Color(1f,.9f,.25f), false, true)
        };

        private readonly TankData[] tanks = new TankData[]
        {
            new TankData("Dune Viper", "Balanced starter with precise handling", new Color(.15f,.75f,.38f), 100, .05f),
            new TankData("Iron Bison", "Heavy armor and increased durability", new Color(.2f,.55f,.9f), 125, .15f),
            new TankData("Crimson Lynx", "Aggressive frame with bonus weapon damage", new Color(.9f,.2f,.2f), 95, 0f),
            new TankData("Nova Warden", "Elite shield system and high survivability", new Color(.65f,.25f,.95f), 115, .1f)
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
            white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
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
            cam.orthographicSize = 7.2f;
            cam.backgroundColor = new Color(.07f, .11f, .2f);
            cameraBase = new Vector3(0, 1.6f, -10);
            cam.transform.position = cameraBase;
            DontDestroyOnLoad(c);
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * .067f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(1f, .82f, .24f);
            h1Style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * .04f), fontStyle = FontStyle.Bold };
            h1Style.normal.textColor = Color.white;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * .026f), wordWrap = true };
            bodyStyle.normal.textColor = new Color(.9f, .93f, 1f);
            smallStyle = new GUIStyle(bodyStyle) { fontSize = Mathf.RoundToInt(Screen.height * .021f) };
            centerStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * .032f), fontStyle = FontStyle.Bold };
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.yellow;
        }

        private void Update()
        {
            safe = Screen.safeArea;
            if (messageTimer > 0) messageTimer -= Time.deltaTime;

            // Improved camera shake system
            if (cameraShake > 0)
            {
                cameraShake -= Time.deltaTime;
                float shake = Mathf.Sin(Time.time * 45f) * cameraShakeIntensity * (cameraShake / 0.4f);
                cam.transform.position = cameraBase + new Vector3(shake * 0.6f, shake * 0.4f, 0);
            }
            else 
            {
                cam.transform.position = cameraBase;
            }

            if (screen != ScreenState.Battle || paused || battleEnded) return;
            UpdateProjectile();
            UpdateTankTurrets();
            UpdateTrajectory();
            if (!playerTurn && projectiles.Count == 0)
            {
                turnDelay -= Time.deltaTime;
                if (turnDelay <= 0) EnemyShoot();
            }
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
        }

        private void DrawBackdrop()
        {
            GUI.color = new Color(.035f, .06f, .12f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = new Color(.08f, .18f, .32f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * .43f), white);
            GUI.color = Color.white;
        }

        private void DrawTopCurrency()
        {
            float w = Screen.width * .18f;
            Box(new Rect(Screen.width - w - 20, 15, w, 45), new Color(.06f, .09f, .16f, .94f));
            GUI.Label(new Rect(Screen.width - w, 18, w - 10, 40), "COINS  " + save.coins + "    GEMS  " + save.gems, smallStyle);
        }

        private void DrawMenu()
        {
            DrawBackdrop();
            float sw = Screen.width, sh = Screen.height;
            GUI.Label(new Rect(0, sh * .06f, sw, sh * .12f), "TRAJECTORY TITANS", titleStyle);
            GUI.Label(new Rect(0, sh * .17f, sw, sh * .055f), "Aim smart. Master the wind. Rule the arena.", centerStyle);
            DrawTopCurrency();
            float bw = sw * .34f, bh = sh * .09f, x = (sw - bw) * .5f, y = sh * .29f;
            if (BigButton(new Rect(x, y, bw, bh), "CAMPAIGN")) { mode = BattleMode.Campaign; StartBattle(); }
            if (BigButton(new Rect(x, y + bh * 1.15f, bw, bh), "QUICK BATTLE")) { mode = BattleMode.QuickBattle; StartBattle(); }
            if (BigButton(new Rect(x, y + bh * 2.3f, bw, bh), "TRAINING RANGE")) { mode = BattleMode.Training; StartBattle(); }
            float sy = sh * .68f, smallW = sw * .18f;
            if (BigButton(new Rect(sw * .2f, sy, smallW, bh * .8f), "GARAGE")) screen = ScreenState.Garage;
            if (BigButton(new Rect(sw * .41f, sy, smallW, bh * .8f), "DAILY")) screen = ScreenState.Daily;
            if (BigButton(new Rect(sw * .62f, sy, smallW, bh * .8f), "SETTINGS")) screen = ScreenState.Settings;
            GUI.Label(new Rect(0, sh * .84f, sw, 40), "Level " + save.level + "  •  " + save.wins + " victories  •  Original 2D artillery prototype", centerStyle);
        }

        private void DrawGarage()
        {
            DrawBackdrop();
            GUI.Label(new Rect(30, 20, Screen.width * .55f, 70), "GARAGE & ARMORY", h1Style);
            DrawTopCurrency();
            if (BackButton()) { screen = ScreenState.Menu; Save(); return; }
            float top = Screen.height * .15f;
            GUI.Label(new Rect(40, top, 300, 45), "COMBAT VEHICLES", h1Style);
            for (int i = 0; i < tanks.Length; i++)
            {
                float x = 40 + i * (Screen.width - 100) / 4f;
                Rect r = new Rect(x, top + 55, (Screen.width - 140) / 4f, Screen.height * .24f);
                Box(r, save.selectedTank == i ? new Color(.16f, .45f, .55f, .95f) : new Color(.07f, .11f, .19f, .95f));
                GUI.color = tanks[i].body; GUI.DrawTexture(new Rect(r.x + 18, r.y + 18, r.width - 36, 36), white); GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 12, r.y + 62, r.width - 24, 35), tanks[i].name, smallStyle);
                GUI.Label(new Rect(r.x + 12, r.y + 96, r.width - 24, 55), tanks[i].trait, smallStyle);
                GUI.Label(new Rect(r.x + 12, r.y + r.height - 45, r.width - 24, 32), "HP " + tanks[i].health + "  LV." + save.tankLevels[i], smallStyle);
                if (GUI.Button(r, "", GUIStyle.none)) save.selectedTank = i;
            }
            float wy = Screen.height * .48f;
            GUI.Label(new Rect(40, wy, 350, 45), "WEAPON LOADOUT", h1Style);
            scroll = GUI.BeginScrollView(new Rect(35, wy + 45, Screen.width - 70, Screen.height * .37f), scroll, new Rect(0, 0, weapons.Length * 230, Screen.height * .31f));
            for (int i = 0; i < weapons.Length; i++)
            {
                Rect r = new Rect(i * 225, 5, 210, Screen.height * .28f);
                Box(r, save.selectedWeapon == i ? new Color(.45f, .25f, .08f, .95f) : new Color(.07f, .11f, .19f, .95f));
                GUI.color = weapons[i].color; GUI.DrawTexture(new Rect(r.x + 12, r.y + 12, r.width - 24, 10), white); GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 12, r.y + 30, r.width - 24, 34), weapons[i].name, smallStyle);
                GUI.Label(new Rect(r.x + 12, r.y + 65, r.width - 24, 70), weapons[i].description, smallStyle);
                GUI.Label(new Rect(r.x + 12, r.y + r.height - 48, r.width - 24, 34), "DMG " + Mathf.RoundToInt(weapons[i].damage) + "  LV." + save.weaponLevels[i], smallStyle);
                if (GUI.Button(r, "", GUIStyle.none)) save.selectedWeapon = i;
            }
            GUI.EndScrollView();
        }

        private void DrawDaily()
        {
            DrawBackdrop();
            GUI.Label(new Rect(30, 20, 600, 70), "DAILY COMMAND CENTER", h1Style);
            if (BackButton()) { screen = ScreenState.Menu; return; }
            DrawTopCurrency();
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            bool claimed = save.lastDaily == today;
            float w = Screen.width * .55f, h = Screen.height * .52f;
            Rect card = new Rect((Screen.width - w) / 2, Screen.height * .2f, w, h);
            Box(card, new Color(.08f, .13f, .23f, .97f));
            GUI.Label(new Rect(card.x + 25, card.y + 25, card.width - 50, 55), "TODAY'S SUPPLY DROP", h1Style);
            GUI.Label(new Rect(card.x + 25, card.y + 95, card.width - 50, 80), "Return every day to collect currency. Rewards support progression without blocking core gameplay.", bodyStyle);
            GUI.Label(new Rect(card.x + 25, card.y + 185, card.width - 50, 60), "REWARD: 250 COINS + 3 GEMS", h1Style);
            GUI.enabled = !claimed;
            if (BigButton(new Rect(card.x + card.width * .2f, card.y + card.height - 100, card.width * .6f, 65), claimed ? "ALREADY CLAIMED" : "CLAIM REWARD"))
            {
                save.coins += 250; save.gems += 3; save.lastDaily = today; Save(); Toast("Supply drop collected!");
            }
            GUI.enabled = true;
        }

        private void DrawSettings()
        {
            DrawBackdrop();
            GUI.Label(new Rect(30, 20, 500, 70), "SETTINGS", h1Style);
            if (BackButton()) { screen = ScreenState.Menu; Save(); return; }
            float x = Screen.width * .25f, y = Screen.height * .2f, w = Screen.width * .5f;
            Box(new Rect(x - 35, y - 25, w + 70, Screen.height * .55f), new Color(.07f,.11f,.19f,.97f));
            save.sound = GUI.Toggle(new Rect(x, y, w, 45), save.sound, "  Sound effects", bodyStyle);
            save.vibration = GUI.Toggle(new Rect(x, y + 65, w, 45), save.vibration, "  Vibration feedback", bodyStyle);
            save.trajectory = GUI.Toggle(new Rect(x, y + 130, w, 45), save.trajectory, "  Trajectory preview", bodyStyle);
            GUI.Label(new Rect(x, y + 200, 250, 40), "Music volume", bodyStyle);
            save.musicVolume = GUI.HorizontalSlider(new Rect(x + 250, y + 215, w - 270, 28), save.musicVolume, 0, 1);
            GUI.Label(new Rect(x, y + 260, 250, 40), "Effects volume", bodyStyle);
            save.sfxVolume = GUI.HorizontalSlider(new Rect(x + 250, y + 275, w - 270, 28), save.sfxVolume, 0, 1);
            GUI.Label(new Rect(x, y + 330, w, 90), "Privacy-ready MVP: no analytics, ads SDK, account login, or personal-data collection is included.", smallStyle);
        }

        private void StartBattle()
        {
            ClearWorld();
            screen = ScreenState.Battle;
            paused = false; battleEnded = false; playerTurn = true; turnNumber = 1;
            weaponIndex = save.selectedWeapon; aimAngle = 42; shotPower = 52;
            mapSeed = UnityEngine.Random.Range(1000, 999999); rng = new System.Random(mapSeed);
            wind = UnityEngine.Random.Range(-2.2f, 2.2f);
            CreateWorld();
            AddLog("Battle started. Read the wind and choose your shot.");
            Toast(mode == BattleMode.Training ? "Training range" : "Your turn");
        }

        private void CreateWorld()
        {
            cam.orthographicSize = 7.2f;
            cameraBase = new Vector3(0, 1.6f, -10);
            CreateRect("SkyGlow", new Vector2(0, 5), new Vector2(28, 10), new Color(.10f,.23f,.43f), -20);
            CreateCircle("Moon", new Vector2(8.5f, 5.2f), 1.2f, new Color(1f,.7f,.3f), -18);
            for (int i = 0; i < 12; i++) CreateCircle("Star", new Vector2(UnityEngine.Random.Range(-12f,12f), UnityEngine.Random.Range(3f,7f)), .05f, Color.white, -17);
            for (int x = -13; x <= 13; x++)
            {
                float y = TerrainHeight(x);
                CreateRect("Terrain", new Vector2(x, y - 3.2f), new Vector2(1.05f, 6.5f), new Color(.15f + (x%2==0?.02f:0), .32f, .19f), -2);
                CreateRect("Grass", new Vector2(x, y - .08f), new Vector2(1.08f, .22f), new Color(.28f,.62f,.25f), -1);
            }
            player = CreateTank(-8.5f, false, tanks[save.selectedTank]);
            int enemyTank = mode == BattleMode.Training ? 1 : UnityEngine.Random.Range(0, tanks.Length);
            enemy = CreateTank(8.4f, true, tanks[enemyTank]);
            if (mode == BattleMode.Training) { enemy.maxHealth = enemy.health = 999; }
            ShowTrajectory();
        }

        private TankActor CreateTank(float x, bool isEnemy, TankData data)
        {
            var t = new TankActor { enemy = isEnemy, x = x, armor = data.armor };
            t.y = TerrainHeight(x) + .55f;
            t.maxHealth = t.health = data.health + (isEnemy ? campaignStage * 3 : (save.tankLevels[save.selectedTank]-1) * 6);
            t.root = new GameObject(isEnemy ? "EnemyTank" : "PlayerTank"); Track(t.root);
            t.root.transform.position = new Vector3(t.x, t.y, 0);
            CreateChildRect(t.root.transform, "Hull", Vector2.zero, new Vector2(1.65f,.65f), data.body, 2);
            CreateChildRect(t.root.transform, "Track", new Vector2(0,-.42f), new Vector2(1.9f,.32f), new Color(.08f,.09f,.12f), 3);
            CreateChildCircle(t.root.transform, "WheelL", new Vector2(-.55f,-.42f), .24f, new Color(.25f,.27f,.3f), 4);
            CreateChildCircle(t.root.transform, "WheelR", new Vector2(.55f,-.42f), .24f, new Color(.25f,.27f,.3f), 4);
            var turretObj = CreateChildCircle(t.root.transform, "Turret", new Vector2(0,.42f), .48f, data.body * .9f, 4);
            t.turret = turretObj.transform;
            var barrel = CreateChildRect(t.turret, "Barrel", new Vector2(.65f,0), new Vector2(1.3f,.18f), new Color(.15f,.17f,.2f), 3);
            barrel.transform.localPosition = new Vector3(isEnemy ? -.65f : .65f, 0, 0);
            t.muzzle = new GameObject("Muzzle").transform;
            t.muzzle.SetParent(t.turret);
            t.muzzle.localPosition = new Vector3(isEnemy ? -1.35f : 1.35f, 0, 0);
            return t;
        }

        private void DrawBattleHUD()
        {
            float sw = Screen.width, sh = Screen.height;
            Box(new Rect(15, 15, sw * .28f, 70), new Color(.03f,.05f,.1f,.9f));
            GUI.Label(new Rect(30, 20, sw * .25f, 28), tanks[save.selectedTank].name, smallStyle);
            DrawHealth(new Rect(30, 52, sw * .24f, 18), player.health / player.maxHealth, new Color(.2f,.85f,.35f));
            Box(new Rect(sw - sw * .28f - 15, 15, sw * .28f, 70), new Color(.03f,.05f,.1f,.9f));
            GUI.Label(new Rect(sw - sw * .28f, 20, sw * .25f, 28), mode == BattleMode.Training ? "TARGET DUMMY" : "RIVAL UNIT", smallStyle);
            DrawHealth(new Rect(sw - sw * .26f, 52, sw * .24f, 18), enemy.health / enemy.maxHealth, new Color(.95f,.25f,.2f));
            Box(new Rect(sw * .405f, 12, sw * .19f, 62), new Color(.03f,.05f,.1f,.9f));
            GUI.Label(new Rect(sw * .415f, 18, sw * .17f, 45), "WIND " + (wind >= 0 ? "→ " : "← ") + Mathf.Abs(wind).ToString("0.0"), centerStyle);
            if (GUI.Button(new Rect(sw - 72, sh - 64, 55, 45), "II", buttonStyle)) paused = !paused;
            if (paused) { DrawPause(); return; }
            if (!playerTurn || projectiles.Count > 0) return;
            float panelH = sh * .22f;
            Box(new Rect(12, sh - panelH - 10, sw - 95, panelH), new Color(.03f,.05f,.1f,.92f));
            GUI.Label(new Rect(30, sh - panelH, 180, 36), "ANGLE " + Mathf.RoundToInt(aimAngle) + "°", smallStyle);
            aimAngle = GUI.HorizontalSlider(new Rect(185, sh - panelH + 13, sw * .25f, 30), aimAngle, 10, 80);
            GUI.Label(new Rect(30, sh - panelH + 50, 180, 36), "POWER " + Mathf.RoundToInt(shotPower), smallStyle);
            shotPower = GUI.HorizontalSlider(new Rect(185, sh - panelH + 63, sw * .25f, 30), shotPower, 20, 85);
            Rect wp = new Rect(sw * .5f, sh - panelH + 10, sw * .22f, 62);
            Box(wp, new Color(.08f,.11f,.19f,.95f));
            GUI.Label(new Rect(wp.x + 10, wp.y + 4, wp.width - 20, 28), weapons[weaponIndex].name, smallStyle);
            if (GUI.Button(new Rect(wp.x + 8, wp.y + 33, 45, 25), "<”)) weaponIndex = (weaponIndex - 1 + weapons.Length) % weapons.Length;
            if (GUI.Button(new Rect(wp.x + wp.width - 53, wp.y + 33, 45, 25), ">”)) weaponIndex = (weaponIndex + 1) % weapons.Length;
            if (BigButton(new Rect(sw * .75f, sh - panelH + 8, sw * .15f, panelH - 25), "FIRE")) PlayerShoot();
        }

        private void DrawPause()
        {
            Box(new Rect(Screen.width*.3f, Screen.height*.2f, Screen.width*.4f, Screen.height*.58f), new Color(.03f,.05f,.1f,.98f));
            GUI.Label(new Rect(Screen.width*.3f, Screen.height*.25f, Screen.width*.4f, 60), "PAUSED", titleStyle);
            if (BigButton(new Rect(Screen.width*.38f, Screen.height*.42f, Screen.width*.24f, 65), "RESUME")) paused = false;
            if (BigButton(new Rect(Screen.width*.38f, Screen.height*.55f, Screen.width*.24f, 65), "RESTART")) StartBattle();
            if (BigButton(new Rect(Screen.width*.38f, Screen.height*.68f, Screen.width*.24f, 55), "MAIN MENU")) { ClearWorld(); screen = ScreenState.Menu; }
        }

        private void PlayerShoot()
        {
            if (!playerTurn || projectiles.Count > 0) return;
            player.angle = aimAngle;
            Launch(player, weapons[weaponIndex], shotPower, false);
            playerTurn = false;
            AddLog("You fired " + weapons[weaponIndex].name + ".");
        }

        private void EnemyShoot()
        {
            if (projectiles.Count > 0 || battleEnded) return;
            float dx = Mathf.Abs(player.x - enemy.x);
            float selectedAngle = UnityEngine.Random.Range(34f, 58f);
            float rad = selectedAngle * Mathf.Deg2Rad;
            float gravity = 9.81f;
            float ideal = Mathf.Sqrt((dx * gravity) / Mathf.Max(.2f, Mathf.Sin(2 * rad))) * 4.1f;
            float difficultyError = mode == BattleMode.Campaign ? Mathf.Max(1.5f, 7f - campaignStage * .35f) : 5f;
            float power = ideal + UnityEngine.Random.Range(-difficultyError, difficultyError) - wind * .7f;
            int wi = mode == BattleMode.Training ? 0 : UnityEngine.Random.Range(0, Mathf.Min(weapons.Length, 2 + campaignStage / 2));
            enemy.angle = selectedAngle;
            Launch(enemy, weapons[wi], Mathf.Clamp(power, 24, 80), true);
            AddLog("Rival fired " + weapons[wi].name + ".");
        }

        private void Launch(TankActor source, WeaponData w, float power, bool fromEnemy)
        {
            int count = w.projectiles;
            for (int i = 0; i < count; i++)
            {
                float spread = count == 1 ? 0 : (i - (count - 1) * .5f) * 5f;
                float a = (source.angle + spread) * Mathf.Deg2Rad;
                float dir = fromEnemy ? -1 : 1;
                float scale = .24f;
                Vector2 vel = new Vector2(Mathf.Cos(a) * power * scale * dir, Mathf.Sin(a) * power * scale);
                var obj = CreateCircle("Projectile", source.muzzle.position, w.heavy ? .22f : .14f, w.color, 10);
                projectiles.Add(new ProjectileSim { visual = obj, position = source.muzzle.position, velocity = vel, weapon = w, enemy = fromEnemy });
            }
            ClearTrajectory();
        }

        private void UpdateProjectile()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                float dt = Time.deltaTime;
                p.life += dt;
                p.velocity += new Vector2(wind * .16f, -9.81f) * dt;
                p.position += p.velocity * dt;
                p.visual.transform.position = p.position;
                if (p.position.y <= TerrainHeight(p.position.x) || p.life > 10f || Mathf.Abs(p.position.x) > 15f)
                {
                    Explode(p);
                    if (p.visual) Destroy(p.visual);
                    projectiles.RemoveAt(i);
                }
            }
        }

        private void Explode(ProjectileSim p)
        {
            // === IMPROVED EXPLOSION VISUALS (Stage A) ===
            float impactRadius = p.weapon.radius;
            Vector2 impactPos = p.position;

            // Dynamic camera shake based on weapon power
            cameraShake = 0.38f;
            cameraShakeIntensity = Mathf.Clamp(impactRadius * 0.9f, 0.8f, 2.8f);

            // Central bright flash
            var flash = CreateCircle("ExplosionFlash", impactPos, impactRadius * 0.9f, Color.white, 15);
            flash.GetComponent<SpriteRenderer>().color = new Color(1f, 0.95f, 0.8f, 0.9f);
            Destroy(flash, 0.12f);

            // Main explosion core
            for (int i = 0; i < 18; i++)
            {
                float angle = i * 20f;
                Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * UnityEngine.Random.Range(0.1f, impactRadius * 0.55f);
                float size = UnityEngine.Random.Range(0.08f, 0.28f);
                var core = CreateCircle("ExplosionCore", impactPos + offset, size, p.weapon.color, 12);
                Destroy(core, UnityEngine.Random.Range(0.25f, 0.45f));
            }

            // Secondary debris / sparks
            for (int i = 0; i < 12; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * impactRadius * 0.7f;
                float size = UnityEngine.Random.Range(0.04f, 0.12f);
                var debris = CreateCircle("Debris", impactPos + offset, size, Color.Lerp(p.weapon.color, Color.white, 0.3f), 11);
                Destroy(debris, UnityEngine.Random.Range(0.15f, 0.35f));
            }

            // Extra ring effect for heavy weapons
            if (p.weapon.heavy)
            {
                var ring = CreateCircle("Shockwave", impactPos, impactRadius * 1.1f, new Color(1f, 0.6f, 0.2f, 0.6f), 9);
                Destroy(ring, 0.22f);
            }

            // Call terrain deformation
            DeformTerrain(impactPos, impactRadius);

            // Apply damage
            DamageTank(player, impactPos, p.weapon, p.enemy);
            DamageTank(enemy, impactPos, p.weapon, !p.enemy);

            if (save.vibration && Application.isMobilePlatform) Handheld.Vibrate();

            CheckBattleEnd();

            if (!battleEnded && projectiles.Count <= 1)
            {
                if (p.enemy) 
                { 
                    playerTurn = true; 
                    turnNumber++; 
                    wind = Mathf.Clamp(wind + UnityEngine.Random.Range(-.6f,.6f), -2.8f, 2.8f); 
                    Toast("Your turn"); 
                    ShowTrajectory(); 
                }
                else 
                { 
                    turnDelay = 1.25f; 
                }
            }
        }

        // Terrain Deformation
        private void DeformTerrain(Vector2 impact, float radius)
        {
            float deformRadius = radius * 1.6f;
            float maxDrop = Mathf.Clamp(radius * 0.85f, 0.6f, 2.2f);

            for (int i = worldObjects.Count - 1; i >= 0; i--)
            {
                var go = worldObjects[i];
                if (go == null) continue;

                bool isTerrain = go.name.StartsWith("Terrain") || go.name.StartsWith("Grass");
                if (!isTerrain) continue;

                float dist = Vector2.Distance(new Vector2(go.transform.position.x, go.transform.position.y), impact);
                if (dist > deformRadius) continue;

                float dropAmount = maxDrop * (1f - Mathf.Clamp01(dist / deformRadius));
                dropAmount = Mathf.Max(dropAmount, 0.25f);

                Vector3 pos = go.transform.position;
                pos.y -= dropAmount;
                go.transform.position = pos;

                if (go.name.StartsWith("Grass") && dist < radius * 0.7f)
                {
                    Destroy(go);
                    worldObjects.RemoveAt(i);
                }
            }
        }

        private void DamageTank(TankActor target, Vector2 impact, WeaponData w, bool allowed)
        {
            if (!allowed || target == null) return;
            float dist = Vector2.Distance(new Vector2(target.x, target.y), impact);
            if (dist > w.radius + .9f) return;
            float factor = 1f - Mathf.Clamp01(dist / (w.radius + 1f)) * .55f;
            float levelBonus = target.enemy ? 1f : 1f + (save.weaponLevels[weaponIndex]-1) * .05f;
            float dmg = w.damage * factor * levelBonus * (1f - target.armor);
            if (target.shieldTurns > 0) { dmg *= .5f; target.shieldTurns--; }
            target.health = Mathf.Max(0, target.health - dmg);
            Toast(Mathf.RoundToInt(dmg) + " damage!");
        }

        private void CheckBattleEnd()
        {
            if (enemy.health <= 0)
            {
                battleEnded = true; save.wins++; save.coins += mode == BattleMode.Campaign ? 180 + campaignStage * 20 : 100; save.xp += 80;
                if (mode == BattleMode.Campaign) campaignStage++;
                LevelCheck(); Save(); Invoke(nameof(WinResult), 1.2f);
            }
            else if (player.health <= 0)
            {
                battleEnded = true; save.losses++; save.coins += 20; save.xp += 15; LevelCheck(); Save(); Invoke(nameof(LoseResult), 1.2f);
            }
        }

        private void WinResult() { screen = ScreenState.Result; message = "VICTORY"; }
        private void LoseResult() { screen = ScreenState.Result; message = "DEFEAT"; }

        private void DrawResult()
        {
            DrawBackdrop();
            bool won = message == "VICTORY";
            GUI.Label(new Rect(0, Screen.height*.12f, Screen.width, 100), message, titleStyle);
            GUI.Label(new Rect(Screen.width*.25f, Screen.height*.3f, Screen.width*.5f, 120), won ? "Excellent shot command. Rewards have been added to your account." : "Study the wind, adjust power, and return stronger.", centerStyle);
            GUI.Label(new Rect(Screen.width*.25f, Screen.height*.46f, Screen.width*.5f, 60), "Coins: " + save.coins + "     Level: " + save.level + "     XP: " + save.xp, centerStyle);
            if (BigButton(new Rect(Screen.width*.32f, Screen.height*.62f, Screen.width*.17f, 70), "REMATCH")) StartBattle();
            if (BigButton(new Rect(Screen.width*.51f, Screen.height*.62f, Screen.width*.17f, 70), "MAIN MENU")) { ClearWorld(); screen = ScreenState.Menu; }
        }

        private void LevelCheck()
        {
            int needed = save.level * 150;
            while (save.xp >= needed) { save.xp -= needed; save.level++; save.gems += 5; needed = save.level * 150; }
        }

        private void UpdateTankTurrets()
        {
            if (player != null) player.turret.localRotation = Quaternion.Euler(0,0,aimAngle);
            if (enemy != null) enemy.turret.localRotation = Quaternion.Euler(0,0,180f-enemy.angle);
        }

        private void ShowTrajectory()
        {
            if (!save.trajectory || !playerTurn || player == null) return;
            for (int i = 1; i <= 20; i++)
            {
                var dot = CreateCircle("TrajectoryDot", Vector2.zero, .055f, new Color(1,1,1,.65f), 8);
                trajectoryDots.Add(dot);
            }
        }

        private void UpdateTrajectory()
        {
            if (!playerTurn || projectiles.Count > 0 || !save.trajectory) return;
            if (trajectoryDots.Count == 0) ShowTrajectory();
            float a = aimAngle * Mathf.Deg2Rad;
            Vector2 start = player.muzzle.position;
            Vector2 vel = new Vector2(Mathf.Cos(a)*shotPower*.24f, Mathf.Sin(a)*shotPower*.24f);
            for (int i = 0; i < trajectoryDots.Count; i++)
            {
                float t = (i+1)*.12f;
                Vector2 pos = start + vel*t + .5f*new Vector2(wind*.16f,-9.81f)*t*t;
                trajectoryDots[i].transform.position = pos;
                trajectoryDots[i].SetActive(pos.y > TerrainHeight(pos.x));
            }
        }

        private float TerrainHeight(float x)
        {
            float wave = Mathf.Sin((x + mapSeed * .001f) * .42f) * .75f + Mathf.Sin((x - mapSeed * .002f) * .19f) * .52f;
            return -1.5f + wave;
        }

        private void ClearTrajectory()
        {
            foreach (var d in trajectoryDots) if (d) Destroy(d);
            trajectoryDots.Clear();
        }

        private void ClearWorld()
        {
            foreach (var o in worldObjects) if (o) Destroy(o);
            worldObjects.Clear(); projectiles.Clear(); trajectoryDots.Clear(); player = null; enemy = null;
        }

        private GameObject CreateRect(string n, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(n); Track(go); go.transform.position = pos; go.transform.localScale = new Vector3(size.x,size.y,1);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = MakeSprite(false); sr.color = color; sr.sortingOrder = order; return go;
        }

        private GameObject CreateCircle(string n, Vector2 pos, float radius, Color color, int order)
        {
            var go = new GameObject(n); Track(go); go.transform.position = pos; go.transform.localScale = Vector3.one * radius * 2;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = MakeSprite(true); sr.color = color; sr.sortingOrder = order; return go;
        }

        private GameObject CreateChildRect(Transform parent, string n, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(n); go.transform.SetParent(parent); go.transform.localPosition = pos; go.transform.localScale = new Vector3(size.x,size.y,1);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = MakeSprite(false); sr.color = color; sr.sortingOrder = order; return go;
        }

        private GameObject CreateChildCircle(Transform parent, string n, Vector2 pos, float radius, Color color, int order)
        {
            var go = new GameObject(n); go.transform.SetParent(parent); go.transform.localPosition = pos; go.transform.localScale = Vector3.one * radius * 2;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = MakeSprite(true); sr.color = color; sr.sortingOrder = order; return go;
        }

        private Sprite squareSprite, circleSprite;
        private Sprite MakeSprite(bool circle)
        {
            if (!circle && squareSprite != null) return squareSprite;
            if (circle && circleSprite != null) return circleSprite;
            int s = 64; var tex = new Texture2D(s,s,TextureFormat.RGBA32,false); tex.filterMode = FilterMode.Bilinear;
            for (int y=0;y=s;y++) for(int x=0;x=s;x++)
            {
                bool inside = !circle || Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f)) <= 31.2f;
                tex.SetPixel(x,y,inside?Color.white:Color.clear);
            }
            tex.Apply(); var sp = Sprite.Create(tex,new Rect(0,0,s,s),new Vector2(.5f,.5f),64);
            if(circle) circleSprite=sp; else squareSprite=sp; return sp;
        }

        private void Track(GameObject go) { worldObjects.Add(go); }
        private void AddLog(string s) { combatLog.Insert(0,s); if(combatLog.Count>8) combatLog.RemoveAt(8); }
        private void Toast(string s) { message=s; messageTimer=1.8f; }

        private void DrawToast(string s)
        {
            float w=Screen.width*.36f; Box(new Rect((Screen.width-w)/2,Screen.height*.1f,w,48),new Color(.02f,.03f,.07f,.92f));
            GUI.Label(new Rect((Screen.width-w)/2+10,Screen.height*.1f+5,w-20,38),s,centerStyle);
        }

        private bool BackButton() { return GUI.Button(new Rect(18,Screen.height-62,150,45),"< BACK",buttonStyle); }
        private bool BigButton(Rect r,string text)
        {
            GUI.color = new Color(.16f,.42f,.72f,1); GUI.DrawTexture(r,white); GUI.color=Color.white;
            return GUI.Button(r,text,buttonStyle);
        }
        private void Box(Rect r,Color c) { GUI.color=c; GUI.DrawTexture(r,white); GUI.color=Color.white; }
        private void DrawHealth(Rect r,float p,Color c)
        {
            Box(r,new Color(.12f,.12f,.15f,1)); Rect f=r; f.width*=Mathf.Clamp01(p); Box(f,c);
        }

        private void Load()
        {
            string json=PlayerPrefs.GetString("TT_SAVE","");
            save=string.IsNullOrEmpty(json)?new SaveData():JsonUtility.FromJson<SaveData>(json);
            if(save==null) save=new SaveData();
            if(save.tankLevels==null||save.tankLevels.Length!=4) save.tankLevels=new int[4]{1,1,1,1};
            if(save.weaponLevels==null||save.weaponLevels.Length!=8) save.weaponLevels=new int[8]{1,1,1,1,1,1,1,1};
        }
        private void Save() { PlayerPrefs.SetString("TT_SAVE",JsonUtility.ToJson(save)); PlayerPrefs.Save(); }
        private void OnApplicationPause(bool pause) { if(pause) Save(); }
        private void OnApplicationQuit() { Save(); }
    }
}
